using System.Text.Json;
using Application.Constantes;
using Application.Exceptions;

namespace Application.Dtos;

/// <summary>Lo que muestra una acción de cartel FIJA. Ver spec 2026-09-12 §3.3.</summary>
public record ConfigCartel(string Modo, string? Texto = null, string? Color = null)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public static ConfigCartel Leer(string? json)
    {
        ConfigCartel? c;
        try { c = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ConfigCartel>(json, _json); }
        catch (JsonException ex) { throw new ConfigInvalidaException("La config del cartel no es un JSON válido.", ex); }

        if (c is null || !CteFexit.CartelModos.Contains(c.Modo))
            throw new ConfigInvalidaException(
                $"La config del cartel necesita un modo: {string.Join(", ", CteFexit.CartelModos)}.");
        if (c.Modo == CteFexit.CartelModoTexto
            && (string.IsNullOrWhiteSpace(c.Texto) || !CteFexit.ColoresCartel.Contains(c.Color)))
            throw new ConfigInvalidaException(
                $"Un cartel fijo de texto necesita el texto y un color: {string.Join(", ", CteFexit.ColoresCartel)}.");
        return c;
    }
}

/// <summary>Dónde escribe el PLC el valor del parámetro entero. Ver spec 2026-09-12 §3.2.</summary>
public record ConfigPlcParametro(string DireccionParametro, string TipoDireccionParametro)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public static ConfigPlcParametro Leer(string? json)
    {
        ConfigPlcParametro? c;
        try { c = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ConfigPlcParametro>(json, _json); }
        catch (JsonException ex) { throw new ConfigInvalidaException("La config del parámetro no es un JSON válido.", ex); }

        if (c is null || string.IsNullOrWhiteSpace(c.DireccionParametro)
            || !CteFexit.TiposDireccion.Contains(c.TipoDireccionParametro))
            throw new ConfigInvalidaException("Un parámetro de PLC necesita direccionParametro y tipoDireccionParametro.");
        return c;
    }
}
