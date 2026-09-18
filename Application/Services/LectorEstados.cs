using Application.Constantes;
using Application.Dtos;
using Application.Interfaces;
using Application.Settings;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Lee un conjunto de estados y arma la tabla. Agrupa por controlador y abre UNA conexión por cada
/// uno: la alternativa —una conexión por señal— no se nota con dos y se nota mucho con treinta.
///
/// El fallo es parcial y nunca total: si un controlador no contesta, sus filas vienen presentes con
/// el valor vacío y el detalle dice cuáles. Tirar 502 por un equipo caído haría perder la respuesta
/// de todo lo demás.
/// </summary>
public class LectorEstados(IPlcDriverFactory fabrica, IOptions<FexitSettings> settings, ILogger<LectorEstados> logger)
{
    public const string ColumnaSector = "sector";
    public const string ColumnaEquipo = "equipo";
    public const string ColumnaEstado = "estado";
    public const string ColumnaValor = "valor";

    public async Task<ResultadoAccion> LeerAsync(
        IReadOnlyList<Estado> estados, IReadOnlyList<string> codigosPedidos, CancellationToken ct)
    {
        var valores = new Dictionary<long, string>();
        var equiposCaidos = new List<string>();

        foreach (var grupo in estados.GroupBy(e => e.Equipo!.ControladorId))
        {
            var controlador = grupo.First().Equipo!.Controlador!;
            using var driver = fabrica.Crear(controlador);

            // Un solo presupuesto para todo el grupo, igual que EjecutorPlc: sin esto, TimeoutEquipoMs
            // sólo acotaría el read/write del socket YA conectado y nadie pondría límite al connect.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(settings.Value.TimeoutEquipoMs);

            try
            {
                await driver.ConnectAsync(deadline.Token);
                foreach (var estado in grupo)
                {
                    var tipo = TipoDireccionPlcParser.Parsear(estado.TipoDireccion);
                    var datos = await driver.ReadAsync(tipo, estado.Direccion,
                        TipoDireccionPlcParser.AnchoEnBytes(tipo), deadline.Token);
                    valores[estado.Id] = TraductorEstado.Traducir(estado, ConversorValores.AEntero(datos));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // El nombre del equipo sí puede salir: es lo que el usuario nombró. La dirección, la
                // IP y el mensaje del driver, no — pero acá adentro sí, porque esto es el log de este
                // lado y nunca sale por la API. Sin esto, la excepción real se perdía en silencio: a
                // diferencia de EjecutorPlc, este endpoint nunca la propaga (devuelve 200 con
                // resultado parcial a propósito), así que ManejadorExcepciones nunca llega a verla.
                logger.LogWarning(ex, "No se pudo leer el controlador {Controlador}.", controlador.Nombre);
                foreach (var e in grupo) equiposCaidos.Add(e.Equipo!.Nombre);
            }
            finally
            {
                // Sin esto, un socket queda abierto por cada fallo y a las horas el proceso se queda
                // sin sockets.
                await driver.DisconnectAsync();
            }
        }

        var filas = estados.Select(e => new Dictionary<string, object?>
        {
            [ColumnaSector] = e.Equipo!.Sector!.Nombre,
            [ColumnaEquipo] = e.Equipo.Nombre,
            [ColumnaEstado] = e.Nombre,
            [ColumnaValor] = valores.TryGetValue(e.Id, out var v) ? v : TraductorEstado.SinLectura,
        }).ToList();

        var desconocidos = codigosPedidos.Except(estados.Select(e => e.Codigo)).ToList();
        var detalle = Detalle(equiposCaidos.Distinct().ToList(), desconocidos);

        return new ResultadoAccion(true, detalle, [ColumnaSector, ColumnaEquipo, ColumnaEstado, ColumnaValor], filas);
    }

    private static string Detalle(List<string> caidos, List<string> desconocidos)
    {
        var partes = new List<string>();
        if (caidos.Count > 0) partes.Add($"No se pudo leer: {string.Join(", ", caidos)}.");
        if (desconocidos.Count > 0) partes.Add($"Códigos desconocidos: {string.Join(", ", desconocidos)}.");
        return partes.Count == 0 ? "Lectura completa." : string.Join(" ", partes);
    }
}
