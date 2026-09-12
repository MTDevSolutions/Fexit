using System.Text.Json;
using Application.Dtos;
using Application.Interfaces;

namespace Application.Tests;

/// <summary>
/// Tira si lo llaman: listar el catálogo no ejecuta nada. Un doble que devolviera un resultado
/// cualquiera dejaría pasar en silencio el día que el listado empiece a tocar un equipo.
/// </summary>
public sealed class ServicioAccionesNoUsado : IServicioAcciones
{
    public Task<ResultadoAccion> EjecutarAsync(
        string codigo, string modoEsperado, JsonElement? parametros, CancellationToken ct = default) =>
        throw new NotSupportedException("Este test no ejecuta acciones.");
}
