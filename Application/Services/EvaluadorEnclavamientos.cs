using Application.Dtos;
using Domain.Entities;

namespace Application.Services;

/// <summary>Un enclavamiento y lo que se leyó de él. Sin driver de por medio: esto ya está leído.</summary>
public record LecturaEnclavamiento(Enclavamiento Enclavamiento, int Valor);

/// <summary>
/// Traduce lecturas crudas a evaluación. Es la pieza donde vive §10.12: Fexit es el único que conoce
/// valoresOk, así que **acá** el entero se convierte en "sí"/"no" y nunca sale de este archivo hacia
/// arriba. Si el valor crudo llegara al LLM, tendría que adivinar qué significa un 0 en ese equipo.
///
/// Lógica pura, sin driver ni base: es la parte que se puede probar entera y hay que poder leer de
/// una sentada, porque de ella depende que una escritura peligrosa se aborte.
/// </summary>
public static class EvaluadorEnclavamientos
{
    private const string Si = "sí";
    private const string No = "no";

    public const string ColumnaNombre = "enclavamiento";
    public const string ColumnaCondicion = "en condicion";

    /// <summary>
    /// Con lista vacía da SIEMPRE false. Una lista vacía es config incompleta, y lo seguro es que
    /// aborte la escritura y alguien mire la fila. Interpretarla como "todo vale" dejaría escribir
    /// sin ninguna precondición, en silencio.
    /// </summary>
    public static bool EstaEnCondicion(Enclavamiento enclavamiento, int valor) =>
        enclavamiento.ValoresOk.Contains(valor);

    /// <summary>
    /// Con CERO enclavamientos da true: un equipo sin condiciones cargadas no bloquea nada. Es
    /// distinto de un enclavamiento con valoresOk vacío, que sí bloquea.
    /// </summary>
    public static bool TodosEnCondicion(IReadOnlyList<LecturaEnclavamiento> lecturas) =>
        lecturas.All(l => EstaEnCondicion(l.Enclavamiento, l.Valor));

    /// <summary>
    /// La tabla del diagnóstico, con la forma que Dixit ya usa para el SQL. Exito=true siempre: la
    /// lectura se ejecutó. Exito=false en una lectura significaría que NO se pudo leer, no que hay
    /// algo fuera de condición.
    /// </summary>
    public static ResultadoAccion ArmarTabla(IReadOnlyList<LecturaEnclavamiento> lecturas)
    {
        var filas = lecturas.Select(l => new Dictionary<string, object?>
        {
            [ColumnaNombre] = l.Enclavamiento.Nombre,
            [ColumnaCondicion] = EstaEnCondicion(l.Enclavamiento, l.Valor) ? Si : No,
        }).ToList();

        var fuera = lecturas.Count(l => !EstaEnCondicion(l.Enclavamiento, l.Valor));
        var detalle = fuera == 0 ? "Todo en condición." : $"{fuera} fuera de condición.";

        return new ResultadoAccion(true, detalle, [ColumnaNombre, ColumnaCondicion], filas);
    }

    /// <summary>
    /// El texto que ve el usuario cuando la escritura no procede. Nombra CUÁLES fallan: "precondición
    /// no cumplida" a secas obliga a llamar por teléfono a la planta. Sólo el nombre — ni dirección
    /// ni valor leído, que es la promesa de §6.
    /// </summary>
    public static string DetalleDelAborto(IReadOnlyList<LecturaEnclavamiento> lecturas)
    {
        var fuera = lecturas.Where(l => !EstaEnCondicion(l.Enclavamiento, l.Valor))
                            .Select(l => l.Enclavamiento.Nombre);
        return $"Precondición no cumplida: {string.Join(", ", fuera)}.";
    }
}
