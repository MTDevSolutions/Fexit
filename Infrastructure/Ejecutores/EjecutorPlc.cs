using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;

namespace Infrastructure.Ejecutores;

/// <summary>
/// Ejecuta acciones sobre un PLC, hable S7 o Modbus: el protocolo lo resuelve la factory de drivers
/// y no llega hasta acá (§10.9).
///
/// En la escritura, la precondición se evalúa ANTES y aborta sin escribir (§4.5). Nunca hay
/// escritura parcial: esa garantía existía en Dixit y se muda de lado, no se pierde en la mudanza.
/// </summary>
public class EjecutorPlc(IPlcDriverFactory fabrica) : IEjecutorAccion
{
    public string TipoEquipo => CteFexit.TipoEquipoPlc;

    public async Task<ResultadoAccion> EjecutarAsync(AccionAEjecutar accion, CancellationToken ct)
    {
        using var driver = fabrica.Crear(accion.Equipo);

        try
        {
            await driver.ConnectAsync(ct);

            return accion.Accion.Modo == CteFexit.ModoLectura
                ? await LeerAsync(driver, accion, ct)
                : await EscribirAsync(driver, accion, ct);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            // Dirección mal escrita: los drivers la rechazan ANTES de tocar la red (S7Address.Parse
            // tira FormatException, ModbusTcpDriver tira ArgumentException si no parsea como ushort),
            // así que no es un problema de comunicación. Sin este catch caía en EquipoInalcanzable y
            // Dixit reintentaba cinco veces contra un equipo sano, mientras el usuario revisaba el
            // cableado por un typo en el catálogo. Mismo criterio que la guarda de EsEscribible.
            // ArgumentOutOfRangeException (valor que no entra en el tipo) hereda de ArgumentException
            // y cae acá también: también es un error de config, no de red.
            throw new ConfigInvalidaException("La dirección configurada no tiene un formato válido.", ex);
        }
        catch (Exception ex) when (ex is not ConfigInvalidaException and not OperationCanceledException)
        {
            // Cualquier fallo hablando con el equipo se traduce acá. El mensaje del driver trae host
            // y puerto, así que no puede subir tal cual: la causa va en InnerException, que se
            // loguea de este lado y no sale por la API.
            throw new EquipoInalcanzableException(ex);
        }
        finally
        {
            // Sin esto, un socket queda abierto por cada fallo y a las horas el proceso se queda sin
            // sockets. En una planta eso se manifiesta como "el sistema dejó de responder", sin pista.
            await driver.DisconnectAsync();
        }
    }

    private static async Task<ResultadoAccion> LeerAsync(
        IPlcDriver driver, AccionAEjecutar accion, CancellationToken ct)
    {
        var lecturas = await LeerEnclavamientosAsync(driver, accion, ct);
        return EvaluadorEnclavamientos.ArmarTabla(lecturas);
    }

    private static async Task<ResultadoAccion> EscribirAsync(
        IPlcDriver driver, AccionAEjecutar accion, CancellationToken ct)
    {
        var fila = accion.Accion;

        // Se valida antes de conectar nada útil: es una falla determinista y reintentarla da lo
        // mismo. El CHECK de la base ya lo impide, pero una fila migrada a mano no pasó por ahí.
        if (string.IsNullOrWhiteSpace(fila.Direccion) || string.IsNullOrWhiteSpace(fila.TipoDireccion)
            || fila.Valor is null)
            throw new ConfigInvalidaException("La acción de escritura está incompleta.");

        var tipo = ParsearTipo(fila.TipoDireccion);
        if (!TipoDireccionPlcParser.EsEscribible(tipo))
            throw new ConfigInvalidaException("El tipo de dirección de la acción es de sólo lectura.");

        if (fila.UsaEnclavamientos)
        {
            // Sin el else, una acción marcada como "necesita precondición" sobre un equipo sin
            // enclavamientos cargados escribía igual, en silencio: la intención declarada se degradaba
            // sola. Se puede llegar acá borrando enclavamientos por el ABM, que no mira si alguna
            // acción del equipo los declara. Falla cerrado a propósito: el actuador no se mueve.
            if (accion.Enclavamientos.Count == 0)
                throw new ConfigInvalidaException(
                    "La acción exige verificar enclavamientos y el equipo no tiene ninguno cargado.");

            // Si esta lectura falla, sube como EquipoInalcanzable y NO se escribe: no se sabe en qué
            // estado está el equipo, y ante la duda el actuador no se mueve.
            var lecturas = await LeerEnclavamientosAsync(driver, accion, ct);
            if (!EvaluadorEnclavamientos.TodosEnCondicion(lecturas))
                return new ResultadoAccion(false, EvaluadorEnclavamientos.DetalleDelAborto(lecturas), [], []);
        }

        // El parámetro va ANTES que el comando: cuando el PLC ve el comando, el valor ya está (§3.2).
        // Si esta escritura falla, sube y el comando no se escribe: nada se movió.
        if (accion.Valores?.Entero is int valorParametro)
        {
            var cfg = ConfigPlcParametro.Leer(fila.ConfigJson);
            var tipoParametro = ParsearTipo(cfg.TipoDireccionParametro);
            if (!TipoDireccionPlcParser.EsEscribible(tipoParametro))
                throw new ConfigInvalidaException("El tipo de dirección del parámetro es de sólo lectura.");
            await driver.WriteAsync(tipoParametro, cfg.DireccionParametro,
                ConversorValores.ABytes(valorParametro, tipoParametro));
        }

        await driver.WriteAsync(tipo, fila.Direccion, ConversorValores.ABytes(fila.Valor.Value, tipo));

        return new ResultadoAccion(true, "Escritura realizada.", [], []);
    }

    private static async Task<List<LecturaEnclavamiento>> LeerEnclavamientosAsync(
        IPlcDriver driver, AccionAEjecutar accion, CancellationToken ct)
    {
        var lecturas = new List<LecturaEnclavamiento>();
        foreach (var enclavamiento in accion.Enclavamientos)
        {
            var tipo = ParsearTipo(enclavamiento.TipoDireccion);
            var datos = await driver.ReadAsync(
                tipo, enclavamiento.Direccion, TipoDireccionPlcParser.AnchoEnBytes(tipo), ct);
            lecturas.Add(new LecturaEnclavamiento(enclavamiento, ConversorValores.AEntero(datos)));
        }
        return lecturas;
    }

    private static TipoDireccionPlc ParsearTipo(string texto)
    {
        try
        {
            return TipoDireccionPlcParser.Parsear(texto);
        }
        catch (ArgumentException ex)
        {
            // Config, no red: se traduce para que Dixit la cierre en fallido definitivo y no la
            // reintente cinco veces contra un equipo que está perfecto.
            throw new ConfigInvalidaException("El tipo de dirección configurado no existe.", ex);
        }
    }
}
