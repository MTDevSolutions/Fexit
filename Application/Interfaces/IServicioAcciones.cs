using System.Text.Json;
using Application.Dtos;

namespace Application.Interfaces;

/// <summary>
/// El negocio de ejecutar: resuelve el código, verifica el modo y delega en el ejecutor del tipo de
/// equipo. Se implementa en la tarea 7; se declara acá porque el controller lo recibe desde ahora.
/// </summary>
public interface IServicioAcciones
{
    Task<ResultadoAccion> EjecutarAsync(
        string codigo, string modoEsperado, JsonElement? parametros, CancellationToken ct = default);
}
