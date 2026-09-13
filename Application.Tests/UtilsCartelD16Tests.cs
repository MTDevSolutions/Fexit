using System.Text.Json;
using Application.Constantes;
using Application.Dtos;
using Application.Services;
using Infrastructure.Drivers.Huidu;

namespace Application.Tests;

public class UtilsCartelD16Tests
{
    private static MensajeCartel Msj(string texto, ModoMensajeCartel modo) => new(texto, modo);

    [Theory]
    [InlineData("HOLA")]
    [InlineData("MENSAJE LARGO QUE HACE SCROLL")]
    public void NoDejaPlaceholdersSinReemplazar(string texto)
    {
        Assert.DoesNotContain("{", UtilsCartelD16.BuildDinamico(Msj(texto, ModoMensajeCartel.Blanco)));
    }

    [Theory]
    [InlineData(ModoMensajeCartel.FullVerde, "#00ff00")]
    [InlineData(ModoMensajeCartel.FullRojo, "#ff0000")]
    public void ModoPleno_PintaElFondoDeSuColorSinTexto(ModoMensajeCartel modo, string color)
    {
        var xml = UtilsCartelD16.BuildDinamico(Msj("RETROCEDA", modo));

        Assert.Contains($@"background=""{color}""", xml);
        Assert.Contains("<string></string>", xml);
        Assert.DoesNotContain("RETROCEDA", xml);
    }

    [Fact]
    public void TextoCorto_VaFijoConLetraChica()
    {
        // La regla de largo que pidió Maciel es la que ya tenía axControlBE: hasta 6, fijo.
        var xml = UtilsCartelD16.BuildDinamico(Msj("HOLA", ModoMensajeCartel.Amarillo));

        Assert.Contains(@"in=""0""", xml);
        Assert.Contains(@"size=""19""", xml);
        Assert.Contains("#ffff80", xml);
    }

    [Fact]
    public void TextoLargo_HaceScroll()
    {
        var xml = UtilsCartelD16.BuildDinamico(Msj("Hola bienvenido", ModoMensajeCartel.Verde));

        Assert.Contains(@"in=""26""", xml);
        Assert.Contains("Hola bienvenido - ", xml);
    }

    [Fact]
    public void ElTextoSeEscapaParaElXml()
    {
        var xml = UtilsCartelD16.BuildDinamico(Msj("A<B & C", ModoMensajeCartel.Blanco));

        Assert.Contains("A&lt;B &amp; C", xml);
    }

    [Fact]
    public void AcentosYEnieLleganIntactosHastaElXml()
    {
        // Recorre el camino completo del parámetro (spec 2026-09-12): del valor recibido, validado
        // por ValidadorParametros, hasta el XML que arma UtilsCartelD16. Sin este test, un mal
        // manejo de codificación en cualquiera de los dos pasos no tiene red.
        var def = new DefinicionParametro("texto", CteFexit.ParametroTexto, "texto a mostrar", true, LargoMaximo: 40);
        var valores = ValidadorParametros.ValidarValores(
            [def], JsonDocument.Parse("""{"texto":"Bienvenido al señor Pérez"}""").RootElement);

        var xml = UtilsCartelD16.BuildDinamico(Msj(valores.Texto!, ModoMensajeCartel.Blanco));

        Assert.Contains("Bienvenido al señor Pérez", xml);
    }
}
