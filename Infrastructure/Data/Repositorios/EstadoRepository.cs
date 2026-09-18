using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
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

    public async Task<IReadOnlyList<Estado>> BuscarParaLeerAsync(
        IReadOnlyList<string> codigos, CancellationToken ct) =>
        await ctx.Estados
            .Include(e => e.Equipo!).ThenInclude(eq => eq.Sector)
            .Include(e => e.Equipo!).ThenInclude(eq => eq.Controlador)
            .Where(e => codigos.Contains(e.Codigo))
            .ToListAsync(ct);
}
