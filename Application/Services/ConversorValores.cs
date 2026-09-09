using Application.Constantes;

namespace Application.Services;

/// <summary>
/// Bytes ↔ entero, en un solo lugar. Vive acá y no repartido en los ejecutores porque el orden de
/// los bytes es el tipo de error que no falla: si se leyera al revés, un 1 se convertiría en 256, la
/// comparación contra valoresOk daría "fuera de condición" para siempre, y el operario buscaría un
/// problema físico que no existe. Un solo lugar, con sus tests.
/// </summary>
public static class ConversorValores
{
    /// <summary>
    /// Big-endian: es el orden del cable, y el que devuelven las dos librerías (S7netplus entrega
    /// los bytes crudos del PLC; NModbus arma los ushort y el driver los serializa en ese orden).
    /// </summary>
    public static int AEntero(byte[] datos)
    {
        if (datos is null || datos.Length == 0)
            // Una lectura vacía no es un 0: es un problema. Devolver 0 lo haría pasar por "fuera de
            // condición" y esconde el error real.
            throw new ArgumentException("La lectura no devolvió datos.", nameof(datos));

        var valor = 0;
        foreach (var b in datos)
            valor = (valor << 8) | b;
        return valor;
    }

    /// <summary>
    /// El ancho lo manda el TIPO de dirección, no la magnitud del valor: escribir un 1 en un S7Word
    /// son dos bytes, y el driver valida el largo antes de mandarlo.
    /// </summary>
    public static byte[] ABytes(int valor, TipoDireccionPlc tipo)
    {
        var ancho = TipoDireccionPlcParser.AnchoEnBytes(tipo);

        // Un bit no puede valer 5. Se normaliza en vez de tirar: el catálogo lo carga un técnico, y
        // "valor: 5" en un bit es evidentemente "prendelo".
        if (tipo is TipoDireccionPlc.Coil or TipoDireccionPlc.S7Bit)
            return [(byte)(valor != 0 ? 1 : 0)];

        var maximo = ancho == 1 ? 0xFF : ancho == 2 ? 0xFFFF : int.MaxValue;
        if (valor < 0 || valor > maximo)
            // Truncar escribiría en el PLC un número que nadie pidió, y en un actuador eso puede ser
            // cualquier cosa.
            throw new ArgumentOutOfRangeException(nameof(valor),
                $"El valor no entra en {ancho} byte(s) para el tipo {tipo}.");

        var datos = new byte[ancho];
        for (var i = ancho - 1; i >= 0; i--)
        {
            datos[i] = (byte)(valor & 0xFF);
            valor >>= 8;
        }
        return datos;
    }
}
