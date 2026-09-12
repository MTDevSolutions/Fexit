using System.Text.Json;
using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Services;

namespace Application.Tests;

public class ValidadorParametrosTests
{
    private static readonly DefinicionParametro Texto =
        new("texto", CteFexit.ParametroTexto, "texto a mostrar", true, LargoMaximo: 10);
    private static readonly DefinicionParametro Color =
        new("color", CteFexit.ParametroOpcion, "color", false,
            Opciones: ["verde", "blanco", "rojo", "amarillo"], PorDefecto: "blanco");
    private static readonly DefinicionParametro Minutos =
        new("minutos", CteFexit.ParametroEntero, "minutos", true, Minimo: 1, Maximo: 60);
    private static readonly DefinicionParametro NotaOpcional =
        new("nota", CteFexit.ParametroTexto, "nota", false, LargoMaximo: 20);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void SinDefinicionYSinValores_DevuelveNinguno()
    {
        Assert.Equal(ValoresParametros.Ninguno, ValidadorParametros.ValidarValores([], null));
    }

    [Fact]
    public void SinDefinicion_CualquierParametroSeRechaza()
    {
        // Una acción que no declara parámetros no puede recibir uno: sería un canal nuevo que nadie
        // configuró. Hoy son todas las acciones de PLC sin parámetro.
        Assert.Throws<ParametrosInvalidosException>(
            () => ValidadorParametros.ValidarValores([], Json("""{"minutos":5}""")));
    }

    [Fact]
    public void TextoYColorValidos_SeNormalizan()
    {
        var v = ValidadorParametros.ValidarValores([Texto, Color], Json("""{"texto":"  Hola  ","color":"Amarillo"}"""));

        Assert.Equal("Hola", v.Texto);
        Assert.Equal("amarillo", v.Opcion);
    }

    [Fact]
    public void OpcionAusente_TomaElDefault()
    {
        var v = ValidadorParametros.ValidarValores([Texto, Color], Json("""{"texto":"Hola"}"""));

        Assert.Equal("blanco", v.Opcion);
    }

    [Theory]
    [InlineData("""{"color":"rojo"}""")]                 // falta el requerido
    [InlineData("""{"texto":""}""")]                     // vacío
    [InlineData("""{"texto":"Esto es muy largo"}""")]    // > 10
    [InlineData("""{"texto":5}""")]                      // tipo equivocado
    [InlineData("""{"texto":"Hola","color":"violeta"}""")] // opción inexistente
    [InlineData("""{"texto":"Hola","otro":"x"}""")]      // no declarado
    public void ValoresInvalidos_SeRechazan(string json)
    {
        Assert.Throws<ParametrosInvalidosException>(
            () => ValidadorParametros.ValidarValores([Texto, Color], Json(json)));
    }

    [Fact]
    public void LosCaracteresDeControlSeSacan()
    {
        // El escape del BEL llega tal cual al parser de JSON, que lo convierte en el caracter.
        var v = ValidadorParametros.ValidarValores([Texto], Json("{\"texto\":\"Ho\\u0007la\"}"));

        Assert.Equal("Hola", v.Texto);
    }

    [Theory]
    [InlineData("""{"minutos":5}""", 5)]
    [InlineData("""{"minutos":60}""", 60)]
    public void EnteroEnRango_Pasa(string json, int esperado)
    {
        Assert.Equal(esperado, ValidadorParametros.ValidarValores([Minutos], Json(json)).Entero);
    }

    [Theory]
    [InlineData("""{"minutos":0}""")]
    [InlineData("""{"minutos":61}""")]
    [InlineData("""{"minutos":2.5}""")]
    [InlineData("""{"minutos":"5"}""")]
    public void EnteroInvalido_SeRechaza(string json)
    {
        Assert.Throws<ParametrosInvalidosException>(
            () => ValidadorParametros.ValidarValores([Minutos], Json(json)));
    }

    [Fact]
    public void DefinicionConDosDelMismoTipo_EsConfigInvalida()
    {
        Assert.Throws<ConfigInvalidaException>(() => ValidadorParametros.ValidarDefinicion([Texto, Texto with { Nombre = "otro" }]));
    }

    [Theory]
    [InlineData("""[{"nombre":"x","tipo":"fecha","etiqueta":"x","requerido":true}]""")]
    [InlineData("""[{"nombre":"m","tipo":"entero","etiqueta":"m","requerido":true,"minimo":10,"maximo":1}]""")]
    [InlineData("""[{"nombre":"c","tipo":"opcion","etiqueta":"c","requerido":false,"opciones":["a"],"porDefecto":"b"}]""")]
    [InlineData("""[{"nombre":"t","tipo":"texto","etiqueta":"t","requerido":true}]""")] // texto sin largo máximo
    [InlineData("""no es json""")]
    public void DefinicionesIncoherentes_SonConfigInvalida(string json)
    {
        Assert.Throws<ConfigInvalidaException>(
            () => ValidadorParametros.ValidarDefinicion(ValidadorParametros.LeerDefinicion(json)));
    }

    [Fact]
    public void OpcionalSinDefaultYAusente_NoTiraYQuedaEnNull()
    {
        // Sin este test la rama "no vino, no es requerido, no tiene PorDefecto" del `continue` en
        // ValidarValores queda sin red: si mañana empezara a exigir el parámetro o a tirar, la
        // suite seguiría verde.
        var v = ValidadorParametros.ValidarValores([NotaOpcional], Json("{}"));

        Assert.Null(v.Texto);
    }

    [Fact]
    public void DefinicionNula_EsListaVacia()
    {
        Assert.Empty(ValidadorParametros.LeerDefinicion(null));
    }
}
