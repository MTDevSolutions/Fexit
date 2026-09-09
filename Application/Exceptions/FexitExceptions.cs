namespace Application.Exceptions;

/// <summary>
/// Las cuatro situaciones de §6 que no son "salió bien". Cada una mapea a un HTTP distinto, y ese
/// mapeo vive en un solo lugar (el middleware de la tarea 7).
///
/// TODOS los mensajes son fijos y no interpolan nada del equipo. No es paranoia: el texto de una
/// SocketException trae host y puerto desde .NET 5, y ese texto lo terminan escribiendo el log de
/// Dixit y la columna Error del comando, que un superadmin ve. La causa real va en InnerException,
/// que se loguea de este lado y no sale por la API.
/// </summary>
public class AccionNoEncontradaException()
    : Exception("La acción no existe.");

public class ModoNoCoincideException()
    : Exception("La acción no es del modo esperado.");

public class EquipoInalcanzableException(Exception? inner = null)
    : Exception("No se pudo comunicar con el equipo.", inner);

public class ConfigInvalidaException(string mensaje, Exception? inner = null)
    : Exception(mensaje, inner);
