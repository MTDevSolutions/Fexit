using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;

namespace Application.Services;

/// <summary>
/// El único lugar que sabe qué es un parámetro válido (spec 2026-09-12 §2). Lo usan el ABM (para la
/// definición: ConfigInvalida, 400 en /catalogo) y la ejecución (para los valores:
/// ParametrosInvalidos, 400 siempre). Dixit verifica lo mismo antes de mandar, pero Fexit no le
/// cree: su copia de la definición puede estar vieja.
/// </summary>
public static class ValidadorParametros
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public static List<DefinicionParametro> LeerDefinicion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<DefinicionParametro>>(json, _json) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ConfigInvalidaException("La definición de parámetros no es un JSON válido.", ex);
        }
    }

    public static void ValidarDefinicion(IReadOnlyList<DefinicionParametro> defs)
    {
        foreach (var d in defs)
        {
            if (string.IsNullOrWhiteSpace(d.Nombre) || string.IsNullOrWhiteSpace(d.Etiqueta))
                throw new ConfigInvalidaException("Todo parámetro necesita nombre y etiqueta.");
            if (!CteFexit.TiposParametro.Contains(d.Tipo))
                throw new ConfigInvalidaException(
                    $"Tipo de parámetro desconocido. Los válidos son: {string.Join(", ", CteFexit.TiposParametro)}.");

            switch (d.Tipo)
            {
                case CteFexit.ParametroTexto when d.LargoMaximo is null or <= 0:
                    throw new ConfigInvalidaException("Un parámetro de texto necesita un largo máximo.");
                case CteFexit.ParametroOpcion when d.Opciones is null || d.Opciones.Count == 0:
                    throw new ConfigInvalidaException("Un parámetro de opción necesita la lista de opciones.");
                case CteFexit.ParametroOpcion when d.PorDefecto is not null
                                                   && !d.Opciones!.Contains(d.PorDefecto, StringComparer.OrdinalIgnoreCase):
                    throw new ConfigInvalidaException("El valor por defecto no está entre las opciones.");
                case CteFexit.ParametroEntero when d.Minimo is null || d.Maximo is null || d.Minimo > d.Maximo:
                    throw new ConfigInvalidaException("Un parámetro entero necesita un rango mínimo–máximo coherente.");
            }
        }

        if (defs.GroupBy(d => d.Tipo).Any(g => g.Count() > 1))
            throw new ConfigInvalidaException("Una acción admite a lo sumo un parámetro de cada tipo.");
        if (defs.GroupBy(d => d.Nombre).Any(g => g.Count() > 1))
            throw new ConfigInvalidaException("Hay dos parámetros con el mismo nombre.");
    }

    public static ValoresParametros ValidarValores(IReadOnlyList<DefinicionParametro> defs, JsonElement? valores)
    {
        var recibidos = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (valores is { ValueKind: JsonValueKind.Object } obj)
            foreach (var p in obj.EnumerateObject())
                recibidos[p.Name] = p.Value;
        else if (valores is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) })
            throw new ParametrosInvalidosException("Los parámetros tienen que ser un objeto.");

        foreach (var nombre in recibidos.Keys)
            if (!defs.Any(d => string.Equals(d.Nombre, nombre, StringComparison.OrdinalIgnoreCase)))
                throw new ParametrosInvalidosException($"La acción no admite el parámetro '{nombre}'.");

        string? texto = null, opcion = null;
        int? entero = null;

        foreach (var d in defs)
        {
            var vino = recibidos.TryGetValue(d.Nombre, out var valor) && valor.ValueKind != JsonValueKind.Null;
            if (!vino)
            {
                if (d.Tipo == CteFexit.ParametroOpcion && d.PorDefecto is not null)
                {
                    opcion = d.PorDefecto.ToLowerInvariant();
                    continue;
                }
                if (d.Requerido)
                    throw new ParametrosInvalidosException($"Falta el parámetro '{d.Etiqueta}'.");
                continue;
            }

            switch (d.Tipo)
            {
                case CteFexit.ParametroTexto:
                    texto = ValidarTexto(d, valor);
                    break;
                case CteFexit.ParametroOpcion:
                    opcion = ValidarOpcion(d, valor);
                    break;
                case CteFexit.ParametroEntero:
                    entero = ValidarEntero(d, valor);
                    break;
            }
        }

        return new ValoresParametros(texto, opcion, entero);
    }

    private static string ValidarTexto(DefinicionParametro d, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.String)
            throw new ParametrosInvalidosException($"'{d.Etiqueta}' tiene que ser un texto.");

        // Los de control (BEL, saltos raros) no se muestran bien en un panel y pueden romper el XML.
        // Se reemplazan por un espacio, no se borran: borrarlos pega las palabras ("Hola\nbienvenido"
        // → "Holabienvenido") y, de paso, esconde un intento de evasión tipo "pu\nto" en vez de
        // mostrarlo como "pu to". El trim y el tope de largo se aplican DESPUÉS, sobre el resultado ya
        // colapsado.
        var sinControl = new string(valor.GetString()!.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        var limpio = ColapsarEspacios(sinControl).Trim();
        if (limpio.Length == 0)
            throw new ParametrosInvalidosException($"'{d.Etiqueta}' está vacío.");
        if (limpio.Length > d.LargoMaximo)
            throw new ParametrosInvalidosException($"'{d.Etiqueta}' puede tener hasta {d.LargoMaximo} caracteres.");
        return limpio;
    }

    private static readonly Regex _espaciosRepetidos = new(" {2,}", RegexOptions.Compiled);

    private static string ColapsarEspacios(string s) => _espaciosRepetidos.Replace(s, " ");

    private static string ValidarOpcion(DefinicionParametro d, JsonElement valor)
    {
        var elegido = valor.ValueKind == JsonValueKind.String ? valor.GetString()!.Trim() : null;
        var canonica = d.Opciones!.FirstOrDefault(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));
        return canonica?.ToLowerInvariant()
            ?? throw new ParametrosInvalidosException(
                $"'{d.Etiqueta}' tiene que ser una de: {string.Join(", ", d.Opciones!)}.");
    }

    private static int ValidarEntero(DefinicionParametro d, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.Number || !valor.TryGetInt32(out var n))
            throw new ParametrosInvalidosException($"'{d.Etiqueta}' tiene que ser un número entero.");
        if (n < d.Minimo || n > d.Maximo)
            throw new ParametrosInvalidosException($"'{d.Etiqueta}' tiene que estar entre {d.Minimo} y {d.Maximo}.");
        return n;
    }
}
