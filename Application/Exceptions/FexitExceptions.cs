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
/// <summary>
/// El recurso no existe. En la ejecución el mensaje es SIEMPRE el default: el 404 no distingue "no
/// existe" de "existe pero está deshabilitada" (§6), porque distinguir le confirmaría a quien
/// pregunta que el código existe. El ABM sí lo especializa — ahí quien llama ya tiene la clave de la
/// instalación, y un 404 que diga "no existe la acción" cuando lo que falta es un equipo manda a
/// buscar el problema al lugar equivocado.
/// </summary>
public class AccionNoEncontradaException(string mensaje = "La acción no existe.")
    : Exception(mensaje);

public class ModoNoCoincideException()
    : Exception("La acción no es del modo esperado.");

public class EquipoInalcanzableException(Exception? inner = null)
    : Exception("No se pudo comunicar con el equipo.", inner);

public class ConfigInvalidaException(string mensaje, Exception? inner = null)
    : Exception(mensaje, inner);

/// <summary>
/// Los parámetros del pedido no cumplen la definición de la acción. Siempre 400: quien se equivocó
/// es quien pidió. El mensaje nombra la etiqueta y el límite, nunca nada del equipo.
/// </summary>
public class ParametrosInvalidosException(string mensaje) : Exception(mensaje);
