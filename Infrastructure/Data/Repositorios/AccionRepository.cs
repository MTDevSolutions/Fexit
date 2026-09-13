using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Data.Repositorios;

// El logger es opcional (default null → NullLogger) para no obligar a cada test existente que
// construye este repositorio a mano a pasar uno: la DI de producción siempre inyecta el real.
public class AccionRepository(FexitDbContext ctx, ILogger<AccionRepository>? logger = null) : IAccionRepository
{
    private readonly ILogger<AccionRepository> _logger = logger ?? NullLogger<AccionRepository>.Instance;

    public async Task<IReadOnlyList<AccionRemotaDto>> ListarAsync(CancellationToken ct)
    {
        var filas = await ctx.Acciones.AsNoTracking()
            .Where(a => a.Habilitada)
            .OrderBy(a => a.Codigo)
            .Select(a => new { a.Codigo, a.Descripcion, a.Modo, a.DefinicionParametrosJson })
            .ToListAsync(ct);

        // En memoria: la definición es JSON. ConfigJson ni se lee: no hay forma de que se cuele.
        // Una fila con la definición rota (cargada por SQL, no por el ABM) no puede tumbar el
        // catálogo entero: se degrada a "sin parámetros" acá, en el LISTADO. La EJECUCIÓN lee la
        // definición por su propio camino (AccionAEjecutar) y ahí sigue fallando fuerte a propósito:
        // un pedido con parámetros contra una definición ilegible no se puede ejecutar a ciegas.
        return filas.Select(a => new AccionRemotaDto(a.Codigo, a.Descripcion, a.Modo, LeerDefinicionTolerante(a.Codigo, a.DefinicionParametrosJson))).ToList();
    }

    private List<DefinicionParametro> LeerDefinicionTolerante(string codigo, string? json)
    {
        try
        {
            return ValidadorParametros.LeerDefinicion(json);
        }
        catch (ConfigInvalidaException ex)
        {
            _logger.LogWarning(ex, "La definición de parámetros de la acción '{Codigo}' no es un JSON válido; se publica sin parámetros.", codigo);
            return [];
        }
    }

    public async Task<AccionAEjecutar?> BuscarPorCodigoAsync(string codigo, CancellationToken ct)
    {
        // Deshabilitada se trata igual que inexistente: el filtro va acá y no en el llamador, así
        // ningún camino de ejecución puede olvidarse de aplicarlo.
        var accion = await ctx.Acciones.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Codigo == codigo && a.Habilitada, ct);
        if (accion is null)
            return null;

        var equipo = await ctx.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == accion.EquipoId, ct);
        if (equipo is null)
            return null;   // FK Restrict lo hace imposible; si pasa, es una BD tocada a mano.

        // Los enclavamientos son del EQUIPO (§4.2). Se traen siempre, aunque UsaEnclavamientos sea
        // false: quién los usa y cómo lo decide el ejecutor, y traerlos igual cuesta una consulta a
        // una tabla de tres filas.
        var enclavamientos = await ctx.Enclavamientos.AsNoTracking()
            .Where(e => e.EquipoId == equipo.Id)
            .OrderBy(e => e.Orden).ThenBy(e => e.Id)
            .ToListAsync(ct);

        return new AccionAEjecutar(accion, equipo, enclavamientos);
    }
}
