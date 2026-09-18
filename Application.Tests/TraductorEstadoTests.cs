using Application.Services;
using Domain.Entities;

namespace Application.Tests;

/// <summary>
/// La traducción es lo único que separa "1" de "levantada". Es lógica pura y se prueba entera: si se
/// equivoca, el usuario recibe una afirmación prolija y falsa sobre un fierro que se mueve.
/// </summary>
public class TraductorEstadoTests
{
    private static Estado ConEtiquetas() => new()
    {
        Codigo = "posicion_barrera_1", Nombre = "Posición", Direccion = "I0.0", TipoDireccion = "S7Bit",
        Etiquetas = new Dictionary<int, string> { [0] = "baja", [1] = "levantada" }
    };

    private static Estado ConUnidad(string unidad, int decimales) => new()
    {
        Codigo = "nivel_silo_3", Nombre = "Nivel", Direccion = "DB2.DBW10", TipoDireccion = "S7Word",
        Unidad = unidad, Decimales = decimales
    };

    [Theory]
    [InlineData(0, "baja")]
    [InlineData(1, "levantada")]
    public void Etiqueta_por_valor(int valor, string esperado) =>
        Assert.Equal(esperado, TraductorEstado.Traducir(ConEtiquetas(), valor));

    [Fact]
    public void Valor_sin_etiqueta_se_marca_como_desconocido_con_el_crudo()
    {
        // Ni mentir con una etiqueta ajena ni voltear la lectura: el número es lo único que delata
        // una fila mal cargada o un programa de PLC que cambió.
        Assert.Equal("desconocido (3)", TraductorEstado.Traducir(ConEtiquetas(), 3));
    }

    [Fact]
    public void Unidad_sin_decimales() =>
        Assert.Equal("78 %", TraductorEstado.Traducir(ConUnidad("%", 0), 78));

    [Fact]
    public void Unidad_con_decimales_divide_el_entero_del_plc()
    {
        // El PLC manda enteros: 235 con 1 decimal son 23.5 °C. Sin esto el usuario lee "235 °C".
        Assert.Equal("23,5 °C", TraductorEstado.Traducir(ConUnidad("°C", 1), 235));
    }

    [Fact]
    public void Sin_etiquetas_ni_unidad_devuelve_el_numero_pelado()
    {
        // No debería pasar (el CHECK lo impide), pero una fila migrada a mano puede llegar así.
        var estado = new Estado { Codigo = "x", Nombre = "X", Direccion = "I0.0", TipoDireccion = "S7Bit" };
        Assert.Equal("42", TraductorEstado.Traducir(estado, 42));
    }
}
