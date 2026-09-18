using Application.Dtos;

namespace Application.Interfaces;

public interface IEstadoRepository
{
    Task<IReadOnlyList<EstadoPublicadoDto>> ListarPublicadosAsync(CancellationToken ct);
}
