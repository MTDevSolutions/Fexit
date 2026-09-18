using System.Globalization;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Gemelo de Application/Services/Negocio/TraductorEstado.cs en el repo DixitBE (mismo método, mismos
/// casos de test, adaptado ahí a EstadoDisponible en vez de Estado). Se duplica a propósito: los dos
/// repos son soluciones separadas y compartir de verdad pediría un paquete NuGet interno para ~25
/// líneas de lógica pura. Un cambio de reglas acá va también allá (y viceversa).
///
/// Valor crudo → texto. Es la pieza que hace que al modelo le llegue "levantada" y no "1": si le
/// llegara el número tendría que adivinar el sentido, y adivinaría distinto según el día.
///
/// Lógica pura, sin driver ni base, y con sus tests: de acá depende que una afirmación sobre un
/// fierro que se mueve sea cierta.
/// </summary>
public static class TraductorEstado
{
    /// <summary>Lo que va en la celda cuando la lectura falló. No es un valor: es la ausencia de uno.</summary>
    public const string SinLectura = "—";

    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    public static string Traducir(Estado estado, int valor)
    {
        if (estado.Etiquetas is { Count: > 0 })
            return estado.Etiquetas.TryGetValue(valor, out var etiqueta)
                ? etiqueta
                // Ni una etiqueta ajena (mentir) ni una excepción (voltear la lectura entera). El
                // crudo entre paréntesis es lo único que delata la fila mal cargada, y no es secreto:
                // el secreto es la dirección.
                : $"desconocido ({valor})";

        if (!string.IsNullOrWhiteSpace(estado.Unidad))
        {
            var numero = estado.Decimales > 0
                ? (valor / Math.Pow(10, estado.Decimales)).ToString($"F{estado.Decimales}", CulturaArgentina)
                : valor.ToString(CulturaArgentina);
            return $"{numero} {estado.Unidad}";
        }

        // Fila sin traducción declarada: el CHECK lo impide, pero una fila migrada a mano no pasó por
        // ahí. El número pelado es preferible a una excepción que voltee toda la consulta.
        return valor.ToString(CultureInfo.InvariantCulture);
    }
}
