using System.Net.Sockets;
using Application.Interfaces;
using Application.Settings;
using Domain.Entities;
using Infrastructure.Drivers.Huidu.SDK;
using Microsoft.Extensions.Options;

namespace Infrastructure.Drivers.Huidu;

/// <summary>
/// Una conexión por pedido (spec 2026-09-12 §3.3). axControlBE siempre usó el SDK con la conexión
/// abierta: que esto funcione así es lo que prueba la Tarea 13 contra el cartel real, y si no, se
/// rediseña ESTA clase (conexión persistente) sin tocar el ejecutor.
///
/// Orden: connect cancelable con timeout propio (el SDK no lo ofrece: ver el overload de
/// AddDevice(TcpClient, ...)) → negociación de versión y GUID de sesión, que el SDK dispara solo en
/// RaiseClientConnected → esperar que el cartel conteste esa negociación (sin GUID, SendFromXml manda
/// un guid vacío) → mandar el programa → esperar la respuesta a AddProgram → cerrar.
/// </summary>
public class TransporteCartelHuidu(IOptions<FexitSettings> settings) : ITransporteCartel
{
    public async Task EnviarAsync(Controlador controlador, string xml, CancellationToken ct)
    {
        using var manager = new HDCommunicationManager();

        // Un solo presupuesto para TODO el pedido, no uno por espera: con tres esperas en secuencia
        // (conectar, saludo, respuesta) usando cada una el TimeoutEquipoMs completo, el peor caso era
        // el TRIPLE del configurado — más que el timeout HTTP que tiene Dixit del otro lado. Con un
        // único deadline linkeado al ct del caller, el tope total es TimeoutEquipoMs.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(settings.Value.TimeoutEquipoMs);

        var saludo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respuesta = new TaskCompletionSource<ResolveInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.ResolvedInfoReport += (dispositivo, info) =>
        {
            if (!string.IsNullOrEmpty(dispositivo.SdkGuid))
                saludo.TrySetResult();
            if (info.method == SdkMethod.AddProgram.ToString())
                respuesta.TrySetResult(info);
        };

        // El TcpClient(ip, port) que usa el SDK conecta de forma síncrona y bloqueante, sin timeout ni
        // cancelación propios: contra una IP que no rechaza pero tampoco contesta, el hilo queda
        // colgado hasta el timeout TCP del sistema operativo (más de 20 s en Windows), sin que
        // TimeoutEquipoMs ni el ct del pedido lo corten. Por eso el connect se hace acá, cancelable, y
        // se le pasa al SDK ya conectado.
        var cliente = new TcpClient();
        try
        {
            await cliente.ConnectAsync(controlador.Ip, controlador.Puerto, deadline.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // El manager todavía no tomó este cliente: si no lo cerramos acá, nadie más lo hace. El
            // paso donde venció va en InnerException (la excepción original), nunca en el mensaje que
            // sube: ese mensaje puede traer la IP.
            cliente.Dispose();
            throw new IOException("El cartel no aceptó la conexión dentro del tiempo esperado.", ex);
        }
        catch
        {
            // Cancelación real del caller, o cualquier otro fallo (p. ej. de socket): se propaga tal
            // cual, como antes.
            cliente.Dispose();
            throw;
        }

        var dispositivo = manager.AddDevice(cliente, out var error);
        if (dispositivo is null)
        {
            // Mismo motivo: AddDevice no llegó a registrar el dispositivo en el manager, así que el
            // manager tampoco lo va a cerrar al terminar el using de acá arriba.
            cliente.Dispose();
            throw new IOException($"No se pudo conectar al cartel: {error}");
        }

        try
        {
            await saludo.Task.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new IOException("El cartel no saludó dentro del tiempo esperado.", ex);
        }

        dispositivo.SendFromXml(xml);

        ResolveInfo info;
        try
        {
            info = await respuesta.Task.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new IOException("El cartel no confirmó el programa dentro del tiempo esperado.", ex);
        }

        if (info.errorCode != ErrorCode.kSuccess)
            throw new IOException($"El cartel rechazó el programa: {info.errorCode}");
    }
}
