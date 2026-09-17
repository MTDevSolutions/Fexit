using Application.Dtos;

namespace Application.Interfaces;

/// <summary>
/// Carga del catálogo. Separado de IAccionRepository a propósito: aquél es el camino de EJECUCIÓN, y
/// tiene que poder leerse entero para creerle. Mezclarle diez métodos de ABM lo volvería imposible.
/// </summary>
public interface ICatalogoRepository
{
    Task<long> CrearControladorAsync(ControladorRequest req, CancellationToken ct);
    Task<IReadOnlyList<ControladorDto>> ListarControladoresAsync(CancellationToken ct);
    Task BorrarControladorAsync(long id, CancellationToken ct);

    Task<long> CrearEnclavamientoAsync(long controladorId, EnclavamientoRequest req, CancellationToken ct);
    Task<IReadOnlyList<EnclavamientoDto>> ListarEnclavamientosAsync(long controladorId, CancellationToken ct);
    Task BorrarEnclavamientoAsync(long id, CancellationToken ct);

    Task<long> CrearAccionAsync(AccionRequest req, CancellationToken ct);
    Task<IReadOnlyList<AccionCatalogoDto>> ListarAccionesAsync(CancellationToken ct);
    Task EditarAccionAsync(long id, AccionRequest req, CancellationToken ct);
    Task BorrarAccionAsync(long id, CancellationToken ct);
}
