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
}
