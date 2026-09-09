using Domain.Entities;

namespace Application.Dtos;

/// <summary>
/// Una acción con todo lo que hace falta para ejecutarla: su equipo y los enclavamientos de ese
/// equipo, ya resueltos. Existe para que el ejecutor no tenga que volver a la base — y, más
/// importante, para que no pueda: los enclavamientos que evalúa son los del equipo (§4.2), no una
/// lista propia de la acción, y pasárselos ya resueltos hace que no haya otra opción.
/// </summary>
public record AccionAEjecutar(Accion Accion, Equipo Equipo, IReadOnlyList<Enclavamiento> Enclavamientos);
