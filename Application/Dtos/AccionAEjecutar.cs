using Domain.Entities;

namespace Application.Dtos;

/// <summary>
/// Una acción con todo lo que hace falta para ejecutarla: su controlador y los enclavamientos de ese
/// controlador, ya resueltos. Existe para que el ejecutor no tenga que volver a la base — y, más
/// importante, para que no pueda: los enclavamientos que evalúa son los del controlador (§4.2), no
/// una lista propia de la acción, y pasárselos ya resueltos hace que no haya otra opción.
/// </summary>
public record AccionAEjecutar(
    Accion Accion, Controlador Controlador, IReadOnlyList<Enclavamiento> Enclavamientos, ValoresParametros? Valores = null);
