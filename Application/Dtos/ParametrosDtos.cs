namespace Application.Dtos;

/// <summary>
/// Un parámetro que declara una acción (spec 2026-09-12 §2.1). Se publica en GET /acciones: es lo
/// que Dixit copia al dar de alta la fila y contra lo que verifica lo que extrajo el modelo. Los
/// campos que no aplican al tipo van en null.
/// </summary>
public record DefinicionParametro(
    string Nombre, string Tipo, string Etiqueta, bool Requerido,
    int? LargoMaximo = null, List<string>? Opciones = null, string? PorDefecto = null,
    int? Minimo = null, int? Maximo = null);

/// <summary>
/// Los valores ya validados de un pedido. Uno por tipo, porque la definición admite a lo sumo uno
/// de cada: el ejecutor no tiene que saber cómo se llama el parámetro, sólo de qué tipo es.
/// </summary>
public record ValoresParametros(string? Texto, string? Opcion, int? Entero)
{
    public static readonly ValoresParametros Ninguno = new(null, null, null);
}
