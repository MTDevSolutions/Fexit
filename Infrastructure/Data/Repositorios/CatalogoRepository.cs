using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositorios;

/// <summary>
/// Carga del catálogo. Valida ANTES de guardar y no deja que el CHECK de la base sea quien avise:
/// una DbUpdateException es un 500 ilegible para quien está cargando, y no dice cuál de las diez
/// columnas está mal. Los CHECK siguen ahí como segunda capa, para la fila cargada por SQL.
/// </summary>
public class CatalogoRepository(FexitDbContext ctx) : ICatalogoRepository
{
    public async Task<long> CrearControladorAsync(ControladorRequest req, CancellationToken ct)
    {
        if (!CteFexit.Protocolos.Contains(req.Protocolo))
            throw new ConfigInvalidaException(
                $"Protocolo desconocido. Los válidos son: {string.Join(", ", CteFexit.Protocolos)}.");
        if (!CteFexit.TiposEquipo.Contains(req.TipoEquipo))
            throw new ConfigInvalidaException(
                $"Tipo de equipo desconocido. Los válidos son: {string.Join(", ", CteFexit.TiposEquipo)}.");
        // El protocolo tiene que ser del tipo: un cartel sólo habla Huidu, y un PLC nunca.
        if ((req.TipoEquipo == CteFexit.TipoEquipoCartel) != (req.Protocolo == CteFexit.ProtocoloHuiduSdk))
            throw new ConfigInvalidaException(
                $"Un controlador {CteFexit.TipoEquipoCartel} usa el protocolo {CteFexit.ProtocoloHuiduSdk}, y ningún otro tipo lo usa.");
        // Se valida SIEMPRE, aunque en Modbus y en Simulado se ignore: si se dejara pasar cualquier
        // cosa ahí, el día que ese controlador cambie a SiemensS7 la fila quedaría con un modelo
        // inválido y el error saldría recién al ejecutar.
        if (!CteFexit.Modelos.Contains(req.Modelo))
            throw new ConfigInvalidaException(
                $"Modelo de CPU desconocido. Los válidos son: {string.Join(", ", CteFexit.Modelos)}.");
        if (string.IsNullOrWhiteSpace(req.Nombre) || string.IsNullOrWhiteSpace(req.Ip))
            throw new ConfigInvalidaException("Faltan el nombre o la IP del controlador.");
        if (req.Puerto <= 0 || req.Puerto > 65535)
            throw new ConfigInvalidaException("El puerto está fuera de rango.");

        var nombre = req.Nombre.Trim();
        if (await ctx.Controladores.AnyAsync(e => e.Nombre == nombre, ct))
            throw new ConfigInvalidaException("Ya hay un controlador con ese nombre.");

        var controlador = new Controlador
        {
            Nombre = nombre, TipoEquipo = req.TipoEquipo, Ip = req.Ip.Trim(),
            Puerto = req.Puerto, Protocolo = req.Protocolo, Modelo = req.Modelo,
            Rack = req.Rack, Slot = req.Slot,
        };
        ctx.Controladores.Add(controlador);
        await ctx.SaveChangesAsync(ct);
        return controlador.Id;
    }

    public async Task<IReadOnlyList<ControladorDto>> ListarControladoresAsync(CancellationToken ct) =>
        await ctx.Controladores.AsNoTracking().OrderBy(e => e.Nombre)
            .Select(e => new ControladorDto(
                e.Id, e.Nombre, e.TipoEquipo, e.Ip, e.Puerto, e.Protocolo, e.Modelo, e.Rack, e.Slot))
            .ToListAsync(ct);

    public async Task BorrarControladorAsync(long id, CancellationToken ct)
    {
        var controlador = await ctx.Controladores.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new AccionNoEncontradaException("El controlador no existe.");

        // FK Restrict: borrarlo dejaría equipos apuntando a la nada y, con ellos, sus acciones y sus
        // enclavamientos. Se avisa acá en vez de dejar tirar la FK con una DbUpdateException, que es
        // el 500 ilegible que esta capa existe para evitar. Desde el 2026-09-17 el que cuelga del
        // controlador es el EQUIPO: preguntar por acciones directas ya no encontraría ninguna.
        if (await ctx.Equipos.AnyAsync(e => e.ControladorId == id, ct))
            throw new ConfigInvalidaException("El controlador tiene equipos cargados. Borralos primero.");

        ctx.Controladores.Remove(controlador);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<long> CrearEnclavamientoAsync(long equipoId, EnclavamientoRequest req, CancellationToken ct)
    {
        if (!await ctx.Equipos.AnyAsync(e => e.Id == equipoId, ct))
            throw new AccionNoEncontradaException("El equipo no existe.");
        if (string.IsNullOrWhiteSpace(req.Direccion) || string.IsNullOrWhiteSpace(req.Nombre))
            throw new ConfigInvalidaException("Faltan la dirección o el nombre del enclavamiento.");
        if (!CteFexit.TiposDireccion.Contains(req.TipoDireccion))
            throw new ConfigInvalidaException("Tipo de dirección desconocido.");
        // Sin valoresOk, el evaluador lo daría SIEMPRE fuera de condición y la escritura quedaría
        // bloqueada para siempre, sin que nadie sepa por qué. Mejor que no entre.
        if (req.ValoresOk is null || req.ValoresOk.Count == 0)
            throw new ConfigInvalidaException("El enclavamiento no tiene valores en condición.");

        var fila = new Enclavamiento
        {
            EquipoId = equipoId, Direccion = req.Direccion.Trim(), TipoDireccion = req.TipoDireccion,
            Nombre = req.Nombre.Trim(), ValoresOk = [.. req.ValoresOk], Orden = req.Orden,
        };
        ctx.Enclavamientos.Add(fila);
        await ctx.SaveChangesAsync(ct);
        return fila.Id;
    }

    public async Task<IReadOnlyList<EnclavamientoDto>> ListarEnclavamientosAsync(long equipoId, CancellationToken ct) =>
        await ctx.Enclavamientos.AsNoTracking().Where(e => e.EquipoId == equipoId)
            .OrderBy(e => e.Orden).ThenBy(e => e.Id)
            .Select(e => new EnclavamientoDto(
                e.Id, e.EquipoId, e.Direccion, e.TipoDireccion, e.Nombre, e.ValoresOk, e.Orden))
            .ToListAsync(ct);

    public async Task BorrarEnclavamientoAsync(long id, CancellationToken ct)
    {
        var fila = await ctx.Enclavamientos.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new AccionNoEncontradaException("El enclavamiento no existe.");
        ctx.Enclavamientos.Remove(fila);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<long> CrearAccionAsync(AccionRequest req, CancellationToken ct)
    {
        await ValidarAccionAsync(req, idQueSeEdita: null, ct);

        var accion = new Accion
        {
            Codigo = req.Codigo.Trim(), Descripcion = req.Descripcion.Trim(), Modo = req.Modo,
            EquipoId = req.EquipoId, Direccion = req.Direccion?.Trim(), TipoDireccion = req.TipoDireccion,
            Valor = req.Valor, UsaEnclavamientos = req.UsaEnclavamientos, Habilitada = req.Habilitada,
            DefinicionParametrosJson = NoVacio(req.DefinicionParametrosJson), ConfigJson = NoVacio(req.ConfigJson),
        };
        ctx.Acciones.Add(accion);
        await ctx.SaveChangesAsync(ct);
        return accion.Id;
    }

    public async Task<IReadOnlyList<AccionCatalogoDto>> ListarAccionesAsync(CancellationToken ct) =>
        // El ABM lista TODAS, deshabilitadas incluidas: hay que poder volver a habilitarlas. El que
        // filtra es el catálogo publicado (AccionRepository.ListarAsync).
        await ctx.Acciones.AsNoTracking().OrderBy(a => a.Codigo)
            .Select(a => new AccionCatalogoDto(
                a.Id, a.Codigo, a.Descripcion, a.Modo, a.EquipoId,
                a.Direccion, a.TipoDireccion, a.Valor, a.UsaEnclavamientos, a.Habilitada,
                a.DefinicionParametrosJson, a.ConfigJson))
            .ToListAsync(ct);

    // Vacío es más fácil de mandar por accidente que null, y la columna tiene que quedar en null
    // para que "sin parámetros"/"sin config" sea una sola representación, no dos.
    private static string? NoVacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task EditarAccionAsync(long id, AccionRequest req, CancellationToken ct)
    {
        var accion = await ctx.Acciones.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new AccionNoEncontradaException();

        await ValidarAccionAsync(req, idQueSeEdita: id, ct);

        accion.Codigo = req.Codigo.Trim();
        accion.Descripcion = req.Descripcion.Trim();
        accion.Modo = req.Modo;
        accion.EquipoId = req.EquipoId;
        accion.Direccion = req.Direccion?.Trim();
        accion.TipoDireccion = req.TipoDireccion;
        accion.Valor = req.Valor;
        accion.UsaEnclavamientos = req.UsaEnclavamientos;
        accion.Habilitada = req.Habilitada;
        accion.DefinicionParametrosJson = NoVacio(req.DefinicionParametrosJson);
        accion.ConfigJson = NoVacio(req.ConfigJson);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task BorrarAccionAsync(long id, CancellationToken ct)
    {
        var accion = await ctx.Acciones.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new AccionNoEncontradaException();
        ctx.Acciones.Remove(accion);
        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Las mismas reglas para el alta y para la edición, en un solo lugar: si divergieran, se podría
    /// cargar una fila válida y después editarla hasta dejarla rota.
    /// </summary>
    private async Task ValidarAccionAsync(AccionRequest req, long? idQueSeEdita, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Codigo) || string.IsNullOrWhiteSpace(req.Descripcion))
            throw new ConfigInvalidaException("Faltan el código o la descripción de la acción.");
        if (!CteFexit.EsModoValido(req.Modo))
            throw new ConfigInvalidaException($"Modo desconocido. Los válidos son: {CteFexit.ModoLectura}, {CteFexit.ModoEscritura}.");
        // El tipo de equipo decide qué es una fila válida (un cartel no lleva dirección; un PLC sí),
        // y desde el 2026-09-17 la acción no guarda el controlador: se llega por el equipo. Si el
        // equipo no existe, se corta acá y no en la FK.
        var controlador = await ctx.Equipos.AsNoTracking().Where(e => e.Id == req.EquipoId)
            .Select(e => e.Controlador).FirstOrDefaultAsync(ct)
            ?? throw new ConfigInvalidaException("El equipo de la acción no existe.");

        var codigo = req.Codigo.Trim();
        if (await ctx.Acciones.AnyAsync(a => a.Codigo == codigo && a.Id != idQueSeEdita, ct))
            throw new ConfigInvalidaException("Ya hay una acción con ese código.");

        var defs = ValidadorParametros.LeerDefinicion(req.DefinicionParametrosJson);
        ValidadorParametros.ValidarDefinicion(defs);

        if (req.Modo == CteFexit.ModoLectura)
        {
            if (defs.Count > 0)
                throw new ConfigInvalidaException("Una acción de lectura no lleva parámetros.");
            if (controlador.TipoEquipo == CteFexit.TipoEquipoCartel)
                throw new ConfigInvalidaException("Un cartel no admite acciones de lectura.");
            return;
        }

        if (controlador.TipoEquipo == CteFexit.TipoEquipoCartel)
            ValidarEscrituraCartel(req, defs);
        else
            ValidarEscrituraPlc(req, defs);
    }

    private static void ValidarEscrituraPlc(AccionRequest req, List<DefinicionParametro> defs)
    {
        if (string.IsNullOrWhiteSpace(req.Direccion) || string.IsNullOrWhiteSpace(req.TipoDireccion)
            || req.Valor is null)
            throw new ConfigInvalidaException("Una acción de escritura necesita dirección, tipo de dirección y valor.");
        if (!CteFexit.TiposDireccion.Contains(req.TipoDireccion))
            throw new ConfigInvalidaException("Tipo de dirección desconocido.");
        if (!TipoDireccionPlcParser.EsEscribible(TipoDireccionPlcParser.Parsear(req.TipoDireccion)))
            throw new ConfigInvalidaException("Ese tipo de dirección es de sólo lectura: no admite una acción de escritura.");

        if (defs.Any(d => d.Tipo != CteFexit.ParametroEntero))
            throw new ConfigInvalidaException("Una acción de PLC sólo admite un parámetro entero.");
        if (defs.Count == 0)
            return;

        // El usuario manda sólo el valor; dónde se escribe lo dice esta config privada (§3.2).
        var cfg = ConfigPlcParametro.Leer(req.ConfigJson);
        var tipoParametro = TipoDireccionPlcParser.Parsear(cfg.TipoDireccionParametro);
        if (!TipoDireccionPlcParser.EsEscribible(tipoParametro))
            throw new ConfigInvalidaException("El tipo de dirección del parámetro es de sólo lectura.");

        ValidarRangoEntero(defs[0], tipoParametro);
    }

    /// <summary>
    /// El ABM no puede aceptar un rango que la ejecución no pueda escribir: si lo hiciera, Dixit
    /// daría por bueno un valor "dentro de rango" que <see cref="ConversorValores.ABytes"/> recién
    /// rechaza en plena ejecución, con un mensaje que además culpa a la dirección y no al valor.
    /// Mismo criterio de ancho que usa esa conversión, para que nunca diverjan.
    /// </summary>
    private static void ValidarRangoEntero(DefinicionParametro def, TipoDireccionPlc tipoParametro)
    {
        if (tipoParametro is TipoDireccionPlc.Coil or TipoDireccionPlc.S7Bit)
            throw new ConfigInvalidaException(
                "Un parámetro entero no puede escribir en un bit: sólo admite 0 o 1, no un rango.");

        if (def.Minimo < 0)
            throw new ConfigInvalidaException("El mínimo del parámetro no puede ser negativo.");

        var ancho = TipoDireccionPlcParser.AnchoEnBytes(tipoParametro);
        var tope = ancho == 1 ? 0xFF : ancho == 2 ? 0xFFFF : int.MaxValue;
        if (def.Maximo > tope)
            throw new ConfigInvalidaException(
                $"El máximo del parámetro ({def.Maximo}) no entra en {ancho} byte(s) para {tipoParametro}: el tope es {tope}.");
    }

    private static void ValidarEscrituraCartel(AccionRequest req, List<DefinicionParametro> defs)
    {
        if (req.UsaEnclavamientos)
            throw new ConfigInvalidaException("Un cartel no tiene enclavamientos.");

        if (defs.Count == 0)
        {
            ConfigCartel.Leer(req.ConfigJson);   // fija: tiene que decir qué mostrar
            return;
        }

        if (!string.IsNullOrWhiteSpace(req.ConfigJson))
            throw new ConfigInvalidaException("Un cartel de texto libre no lleva config fija.");
        if (!defs.Any(d => d.Tipo == CteFexit.ParametroTexto && d.Requerido))
            throw new ConfigInvalidaException("Un cartel de texto libre necesita un parámetro de texto requerido.");
        if (defs.Any(d => d.Tipo == CteFexit.ParametroEntero))
            throw new ConfigInvalidaException("Un cartel no admite parámetros enteros.");
        var color = defs.FirstOrDefault(d => d.Tipo == CteFexit.ParametroOpcion);
        if (color is not null && color.Opciones!.Any(o => !CteFexit.ColoresCartel.Contains(o.ToLowerInvariant())))
            throw new ConfigInvalidaException(
                $"Los colores válidos del cartel son: {string.Join(", ", CteFexit.ColoresCartel)}.");
    }
}
