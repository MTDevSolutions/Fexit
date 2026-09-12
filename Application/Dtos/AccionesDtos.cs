using System.Text.Json;

namespace Application.Dtos;

/// <summary>
/// Una acción tal como la publica Fexit. Es lo que un superadmin ve en Dixit al dar de alta la fila
/// del catálogo: elige un Codigo de esta lista en vez de tipearlo, que es la única defensa real
/// contra un typo (§5, "cómo se mantienen sincronizados los dos catálogos").
///
/// Nunca trae dirección, IP ni tipo de dato: eso vive de este lado y Dixit no lo necesita. Que el
/// DTO no tenga esas propiedades es lo que hace imposible que se filtren, mejor que acordarse de no
/// mapearlas.
/// </summary>
public record AccionRemotaDto(string Codigo, string Descripcion, string Modo, List<DefinicionParametro> Parametros);

/// <summary>
/// Cuerpo del pedido de ejecución. Lo único que viaja además del código, y no describe la acción:
/// la verifica. Fexit contesta 409 si ese código no es de ese modo en su catálogo, que es lo que
/// impide que una fila mal cargada en Dixit como lectura termine ejecutando una escritura (§5).
///
/// Parametros: los valores del pedido (spec 2026-09-12 §2.2), validados SIEMPRE contra la
/// definición de este lado.
/// </summary>
public record EjecutarAccionRequest(string ModoEsperado, JsonElement? Parametros = null);

/// <summary>
/// Resultado de ejecutar una acción. Columnas y Filas calcan la forma que Dixit ya usa para el SQL,
/// así una lectura entra al carril que va hacia la segunda pasada del LLM sin inventar un segundo
/// camino: para el redactor es una tabla chiquita más.
///
/// Las filas traen la EVALUACIÓN, no el valor crudo (§10.12): "en condición: no", nunca "valor: 0".
/// Fexit es el único que conoce valoresOk; si le llegaran enteros pelados al LLM tendría que
/// adivinar qué significa un 0 en ese equipo, y va a adivinar mal con total seguridad.
/// </summary>
public record ResultadoAccion(
    bool Exito,
    string Detalle,
    List<string> Columnas,
    List<Dictionary<string, object?>> Filas);
