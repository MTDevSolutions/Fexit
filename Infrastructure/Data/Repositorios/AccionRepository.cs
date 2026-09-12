using Application.Dtos;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositorios;

public class AccionRepository(FexitDbContext ctx) : IAccionRepository
{
    public async Task<IReadOnlyList<AccionRemotaDto>> ListarAsync(CancellationToken ct)
    {
        var filas = await ctx.Acciones.AsNoTracking()
            .Where(a => a.Habilitada)
            .OrderBy(a => a.Codigo)
            .Select(a => new { a.Codigo, a.Descripcion, a.Modo, a.DefinicionParametrosJson })
            .ToListAsync(ct);

        // En memoria: la definición es JSON. ConfigJson ni se lee: no hay forma de que se cuele.
        return filas.Select(a => new AccionRemotaDto(a.Codigo, a.Descripcion, a.Modo,
            ValidadorParametros.LeerDefinicion(a.DefinicionParametrosJson))).ToList();
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
