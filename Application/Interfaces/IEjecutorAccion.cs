using Application.Dtos;

namespace Application.Interfaces;

/// <summary>
/// Ejecuta una acción sobre un tipo de equipo. Misma forma que IManejadorAccion en Dixit, que es la
/// pieza que funciona bien y por eso sobrevive al rediseño (§4.4).
///
/// Sumar un controlador o un sensor IoT es una clase nueva más una línea de DI: el contrato no
/// cambia y la API tampoco.
/// </summary>
public interface IEjecutorAccion
{
    /// <summary>Valor de Controlador.TipoEquipo que este ejecutor atiende.</summary>
    string TipoEquipo { get; }

    Task<ResultadoAccion> EjecutarAsync(AccionAEjecutar accion, CancellationToken ct);
}
