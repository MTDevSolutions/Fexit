using Application.Constantes;
using Application.Services;

namespace Application.Tests;

public class ConversorValoresTests
{
    [Fact]
    public void UnBitLeidoDaCeroOUno()
    {
        // El driver de S7 ya normaliza el bit a un byte 0/1, y Modbus hace lo mismo con un coil.
        Assert.Equal(0, ConversorValores.AEntero([0]));
        Assert.Equal(1, ConversorValores.AEntero([1]));
    }

    [Fact]
    public void UnRegistroDeDosBytesSeLeeBigEndian()
    {
        // Los PLC hablan big-endian y las dos librerías devuelven los bytes en el orden del cable.
        // Si esto se leyera al revés, un 1 se convertiría en 256 y la comparación contra valoresOk
        // fallaría siempre, sin ningún error visible: el enclavamiento diría "fuera de condición"
        // para siempre. Es el bug más caro que puede esconder este archivo.
        Assert.Equal(1, ConversorValores.AEntero([0x00, 0x01]));
        Assert.Equal(256, ConversorValores.AEntero([0x01, 0x00]));
        Assert.Equal(65535, ConversorValores.AEntero([0xFF, 0xFF]));
    }

    [Fact]
    public void UnDWordDeCuatroBytesSeLeeBigEndian()
    {
        Assert.Equal(1, ConversorValores.AEntero([0x00, 0x00, 0x00, 0x01]));
        Assert.Equal(16_777_216, ConversorValores.AEntero([0x01, 0x00, 0x00, 0x00]));
    }

    [Fact]
    public void SinBytes_Tira()
    {
        // Una lectura vacía no es un 0: es un problema. Devolver 0 lo haría pasar por "fuera de
        // condición" y el operario buscaría un enclavamiento que en realidad nunca se leyó.
        Assert.Throws<ArgumentException>(() => ConversorValores.AEntero([]));
    }

    [Theory]
    [InlineData(TipoDireccionPlc.S7Bit, 1, new byte[] { 1 })]
    [InlineData(TipoDireccionPlc.Coil, 0, new byte[] { 0 })]
    [InlineData(TipoDireccionPlc.S7Word, 1, new byte[] { 0x00, 0x01 })]
    [InlineData(TipoDireccionPlc.HoldingRegister, 258, new byte[] { 0x01, 0x02 })]
    [InlineData(TipoDireccionPlc.S7DWord, 1, new byte[] { 0x00, 0x00, 0x00, 0x01 })]
    public void EscribirConvierteAlAnchoQuePideElTipo(TipoDireccionPlc tipo, int valor, byte[] esperado)
    {
        // El ancho lo manda el tipo de dirección, no el valor: escribir un 1 en un S7Word son dos
        // bytes, y el driver valida el largo. Mandar uno solo lo hace tirar.
        Assert.Equal(esperado, ConversorValores.ABytes(valor, tipo));
    }

    [Fact]
    public void EscribirUnBitConUnValorDistintoDeCeroDaUno()
    {
        // Un bit no puede valer 5. Se normaliza en vez de tirar: el catálogo lo carga un técnico, y
        // "valor: 5" en un bit es evidentemente "prendelo".
        Assert.Equal([1], ConversorValores.ABytes(5, TipoDireccionPlc.S7Bit));
    }

    [Fact]
    public void UnValorQueNoEntraEnElTipo_Tira()
    {
        // 70000 no entra en dos bytes. Truncar escribiría en el PLC un número que nadie pidió, que
        // en un actuador puede ser cualquier cosa.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ConversorValores.ABytes(70_000, TipoDireccionPlc.HoldingRegister));
    }

    [Theory]
    [InlineData("S7Bit", TipoDireccionPlc.S7Bit)]
    [InlineData("s7bit", TipoDireccionPlc.S7Bit)]
    [InlineData("HoldingRegister", TipoDireccionPlc.HoldingRegister)]
    [InlineData("Coil", TipoDireccionPlc.Coil)]
    public void ParsearElTipoNoDistingueMayusculas(string texto, TipoDireccionPlc esperado)
    {
        // El valor viene de una columna TEXT que carga un humano por endpoint. El CHECK de la base ya
        // limita los valores posibles, pero no el casing.
        Assert.Equal(esperado, TipoDireccionPlcParser.Parsear(texto));
    }

    [Fact]
    public void ParsearUnTipoDesconocido_Tira()
    {
        Assert.Throws<ArgumentException>(() => TipoDireccionPlcParser.Parsear("Profibus"));
    }
}
