using Application.Dtos;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositorios;

public class EstadoRepository(FexitDbContext ctx) : IEstadoRepository
{
    public async Task<IReadOnlyList<EstadoPublicadoDto>> ListarPublicadosAsync(CancellationToken ct) =>
        await ctx.Estados
            .OrderBy(e => e.Equipo!.Sector!.Orden).ThenBy(e => e.Equipo!.Nombre).ThenBy(e => e.Orden)
            .Select(e => new EstadoPublicadoDto(e.Codigo, e.Nombre, e.Descripcion,
                                                e.Equipo!.Nombre, e.Equipo.Sector!.Nombre))
            .ToListAsync(ct);
}
