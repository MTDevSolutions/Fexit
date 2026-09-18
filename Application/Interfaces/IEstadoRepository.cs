using Application.Dtos;
using Domain.Entities;

namespace Application.Interfaces;

public interface IEstadoRepository
{
    Task<IReadOnlyList<EstadoPublicadoDto>> ListarPublicadosAsync(CancellationToken ct);

    /// <summary>
    /// Trae los estados pedidos, con Equipo y Equipo.Controlador incluidos: LectorEstados necesita
    /// los dos para agrupar por controlador y armar la tabla. Un código que no existe simplemente no
    /// aparece en el resultado — lo reporta el lector, no el repositorio.
    /// </summary>
    Task<IReadOnlyList<Estado>> BuscarParaLeerAsync(IReadOnlyList<string> codigos, CancellationToken ct);
}
