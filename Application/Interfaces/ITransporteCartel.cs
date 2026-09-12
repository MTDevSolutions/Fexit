using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Manda un programa XML a un cartel y vuelve sólo cuando el cartel CONFIRMÓ (spec 2026-09-12 §3.3):
/// "encolado" no es "mostrado". Cualquier fallo, tira. Separado del ejecutor por lo mismo que
/// IPlcDriver: el ejecutor se prueba con un transporte falso, y el real se prueba contra el cartel.
/// </summary>
public interface ITransporteCartel
{
    Task EnviarAsync(Equipo equipo, string xml, CancellationToken ct);
}
