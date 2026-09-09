using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
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
    public async Task<long> CrearEquipoAsync(EquipoRequest req, CancellationToken ct)
    {
        if (!CteFexit.Protocolos.Contains(req.Protocolo))
            throw new ConfigInvalidaException(
                $"Protocolo desconocido. Los válidos son: {string.Join(", ", CteFexit.Protocolos)}.");
        if (req.TipoEquipo != CteFexit.TipoEquipoPlc)
            throw new ConfigInvalidaException($"Tipo de equipo desconocido. El único válido es: {CteFexit.TipoEquipoPlc}.");
        // Se valida SIEMPRE, aunque en Modbus y en Simulado se ignore: si se dejara pasar cualquier
        // cosa ahí, el día que ese equipo cambie a SiemensS7 la fila quedaría con un modelo inválido y
        // el error saldría recién al ejecutar.
        if (!CteFexit.Modelos.Contains(req.Modelo))
            throw new ConfigInvalidaException(
                $"Modelo de CPU desconocido. Los válidos son: {string.Join(", ", CteFexit.Modelos)}.");
        if (string.IsNullOrWhiteSpace(req.Nombre) || string.IsNullOrWhiteSpace(req.Ip))
            throw new ConfigInvalidaException("Faltan el nombre o la IP del equipo.");
        if (req.Puerto <= 0 || req.Puerto > 65535)
            throw new ConfigInvalidaException("El puerto está fuera de rango.");
        if (await ctx.Equipos.AnyAsync(e => e.Nombre == req.Nombre, ct))
            throw new ConfigInvalidaException("Ya hay un equipo con ese nombre.");

        var equipo = new Equipo
        {
            Nombre = req.Nombre.Trim(), TipoEquipo = req.TipoEquipo, Ip = req.Ip.Trim(),
            Puerto = req.Puerto, Protocolo = req.Protocolo, Modelo = req.Modelo,
            Rack = req.Rack, Slot = req.Slot,
        };
        ctx.Equipos.Add(equipo);
        await ctx.SaveChangesAsync(ct);
        return equipo.Id;
    }

    public async Task<IReadOnlyList<EquipoDto>> ListarEquiposAsync(CancellationToken ct) =>
        await ctx.Equipos.AsNoTracking().OrderBy(e => e.Nombre)
            .Select(e => new EquipoDto(
                e.Id, e.Nombre, e.TipoEquipo, e.Ip, e.Puerto, e.Protocolo, e.Modelo, e.Rack, e.Slot))
            .ToListAsync(ct);

    public async Task BorrarEquipoAsync(long id, CancellationToken ct)
    {
        var equipo = await ctx.Equipos.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new AccionNoEncontradaException();

        // FK Restrict: borrarlo dejaría acciones apuntando a la nada, y Dixit las seguiría ofreciendo
        // hasta que alguien las ejecute. Se avisa acá en vez de dejar tirar la FK.
        if (await ctx.Acciones.AnyAsync(a => a.EquipoId == id, ct))
            throw new ConfigInvalidaException("El equipo tiene acciones cargadas. Borralas primero.");

        // Los enclavamientos se van solos, por el Cascade.
        ctx.Equipos.Remove(equipo);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<long> CrearEnclavamientoAsync(long equipoId, EnclavamientoRequest req, CancellationToken ct)
    {
        if (!await ctx.Equipos.AnyAsync(e => e.Id == equipoId, ct))
            throw new AccionNoEncontradaException();
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
            ?? throw new AccionNoEncontradaException();
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
                a.Direccion, a.TipoDireccion, a.Valor, a.UsaEnclavamientos, a.Habilitada))
            .ToListAsync(ct);

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
        if (!await ctx.Equipos.AnyAsync(e => e.Id == req.EquipoId, ct))
            throw new ConfigInvalidaException("El equipo de la acción no existe.");

        var codigo = req.Codigo.Trim();
        if (await ctx.Acciones.AnyAsync(a => a.Codigo == codigo && a.Id != idQueSeEdita, ct))
            throw new ConfigInvalidaException("Ya hay una acción con ese código.");

        if (req.Modo == CteFexit.ModoEscritura)
        {
            if (string.IsNullOrWhiteSpace(req.Direccion) || string.IsNullOrWhiteSpace(req.TipoDireccion)
                || req.Valor is null)
                throw new ConfigInvalidaException("Una acción de escritura necesita dirección, tipo de dirección y valor.");
            if (!CteFexit.TiposDireccion.Contains(req.TipoDireccion))
                throw new ConfigInvalidaException("Tipo de dirección desconocido.");
            // InputRegister y DiscreteInput no se pueden escribir. Cargarla dejaría una fila que falla
            // recién al ejecutarse, con un mensaje del driver que no dice que el problema es la carga.
            if (!TipoDireccionPlcParser.EsEscribible(TipoDireccionPlcParser.Parsear(req.TipoDireccion)))
                throw new ConfigInvalidaException("Ese tipo de dirección es de sólo lectura: no admite una acción de escritura.");
        }
    }
}
