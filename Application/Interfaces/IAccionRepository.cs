using Application.Dtos;

namespace Application.Interfaces;

public interface IAccionRepository
{
    /// <summary>El catálogo publicado: sólo las habilitadas, ordenado por código.</summary>
    Task<IReadOnlyList<AccionRemotaDto>> ListarAsync(CancellationToken ct);

    /// <summary>
    /// La acción lista para ejecutar, con su equipo y los enclavamientos de ese equipo. Null si el
    /// código no existe O está deshabilitada: para el llamador son el mismo caso, y de eso depende
    /// que el 404 no distinga (§6).
    /// </summary>
    Task<AccionAEjecutar?> BuscarPorCodigoAsync(string codigo, CancellationToken ct);
}
