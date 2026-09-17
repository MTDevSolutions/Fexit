using Domain.Entities;

namespace Application.Dtos;

/// <summary>
/// Una acción con todo lo que hace falta para ejecutarla: su equipo, el controlador por el que se
/// llega y los enclavamientos de ese equipo, ya resueltos. Existe para que el ejecutor no tenga que
/// volver a la base — y, más importante, para que no pueda: los enclavamientos que evalúa son los
/// del EQUIPO (§4.2), no una lista propia de la acción, y pasárselos ya resueltos hace que no haya
/// otra opción.
///
/// Van el equipo Y el controlador porque son dos cosas distintas y las dos hacen falta: el equipo es
/// de qué se habla ("Barrera 1") y el controlador es por dónde se llega. Derivarlo acá, una sola vez,
/// evita que cada ejecutor tenga que acordarse de hacer accion.Equipo.Controlador.
/// </summary>
public record AccionAEjecutar(
    Accion Accion, Equipo Equipo, Controlador Controlador, IReadOnlyList<Enclavamiento> Enclavamientos,
    ValoresParametros? Valores = null);
