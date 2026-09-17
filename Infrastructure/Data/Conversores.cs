using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Infrastructure.Data;

/// <summary>Mismo formato de almacenamiento que DixitBE: listas como JSON.</summary>
public static class Conversores
{
    public static readonly ValueConverter<List<int>, string> ListaIntAJson = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        s => JsonSerializer.Deserialize<List<int>>(s, (JsonSerializerOptions?)null) ?? new List<int>());

    public static readonly ValueComparer<List<int>> ListaIntComparer = new(
        (a, b) => (a ?? new List<int>()).SequenceEqual(b ?? new List<int>()),
        v => v.Aggregate(0, (h, i) => HashCode.Combine(h, i)),
        v => v.ToList());

    /// <summary>
    /// Etiquetas valor→texto como JSON. Las claves de un Dictionary&lt;int,string&gt; se serializan como
    /// strings en JSON y vuelven a int al deserializar: es el comportamiento estándar de
    /// System.Text.Json y no hace falta un converter propio.
    /// </summary>
    public static readonly ValueConverter<Dictionary<int, string>?, string?> EtiquetasAJson = new(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => v == null ? null : JsonSerializer.Deserialize<Dictionary<int, string>>(v, (JsonSerializerOptions?)null));

    public static readonly ValueComparer<Dictionary<int, string>?> EtiquetasComparer = new(
        (a, b) => a == null ? b == null : b != null && a.Count == b.Count && !a.Except(b).Any(),
        v => v == null ? 0 : v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value)),
        v => v == null ? null : new Dictionary<int, string>(v));
}
