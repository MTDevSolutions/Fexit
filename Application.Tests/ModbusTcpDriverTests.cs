using Infrastructure.Drivers.Modbus;

namespace Application.Tests;

public class ModbusTcpDriverTests
{
    [Theory]
    [InlineData((ushort)1, new byte[] { 0x00, 0x01 })]
    [InlineData((ushort)256, new byte[] { 0x01, 0x00 })]
    [InlineData((ushort)258, new byte[] { 0x01, 0x02 })]
    [InlineData((ushort)65535, new byte[] { 0xFF, 0xFF })]
    public void UnRegistroSeSerializaBigEndian(ushort valor, byte[] esperado)
    {
        // Con Buffer.BlockCopy esto daba el orden del host (little-endian en x86) y del otro lado
        // ConversorValores.AEntero lee big-endian: un registro que valía 1 se leía como 256. Los
        // valores asimétricos son los que lo detectan; 0 y 0xFFFF pasarían igual con el bug.
        Assert.Equal(esperado, ModbusTcpDriver.UShortArrayToBytes([valor]));
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x01 }, (ushort)1)]
    [InlineData(new byte[] { 0x01, 0x00 }, (ushort)256)]
    [InlineData(new byte[] { 0x01, 0x02 }, (ushort)258)]
    public void UnRegistroSeDeserializaBigEndian(byte[] datos, ushort esperado)
    {
        Assert.Equal([esperado], ModbusTcpDriver.BytesAUShortArray(datos));
    }

    [Fact]
    public void ElValorSobreviveLaVueltaCompleta()
    {
        // La prueba que ata las dos puntas: lo que ConversorValores produce para escribir tiene que
        // volver igual al leerlo. Es el ciclo que el bug rompía.
        foreach (var valor in new[] { 1, 2, 258, 4096, 65535 })
        {
            var bytes = Application.Services.ConversorValores.ABytes(
                valor, Application.Constantes.TipoDireccionPlc.HoldingRegister);
            var registros = ModbusTcpDriver.BytesAUShortArray(bytes);
            var vuelta = ModbusTcpDriver.UShortArrayToBytes(registros);

            Assert.Equal(valor, Application.Services.ConversorValores.AEntero(vuelta));
        }
    }
}
