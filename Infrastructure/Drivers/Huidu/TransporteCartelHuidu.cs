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
/// Orden: conectar (el SDK negocia versión y GUID de sesión solo, en RaiseClientConnected) → esperar
/// que el cartel conteste esa negociación (sin GUID, SendFromXml manda un guid vacío) → mandar el
/// programa → esperar la respuesta a AddProgram → cerrar.
/// </summary>
public class TransporteCartelHuidu(IOptions<FexitSettings> settings) : ITransporteCartel
{
    private const string MetodoAgregarPrograma = "AddProgram";

    public async Task EnviarAsync(Equipo equipo, string xml, CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(settings.Value.TimeoutEquipoMs);
        using var manager = new HDCommunicationManager();

        var saludo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respuesta = new TaskCompletionSource<ResolveInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.ResolvedInfoReport += (dispositivo, info) =>
        {
            if (!string.IsNullOrEmpty(dispositivo.SdkGuid))
                saludo.TrySetResult();
            if (info.method == MetodoAgregarPrograma)
                respuesta.TrySetResult(info);
        };

        var dispositivo = manager.AddDevice(equipo.Ip, equipo.Puerto, out var error)
            ?? throw new IOException($"No se pudo conectar al cartel: {error}");

        await saludo.Task.WaitAsync(timeout, ct);
        dispositivo.SendFromXml(xml);
        var info = await respuesta.Task.WaitAsync(timeout, ct);

        if (info.errorCode != ErrorCode.kSuccess)
            throw new IOException($"El cartel rechazó el programa: {info.errorCode}");
    }
}
