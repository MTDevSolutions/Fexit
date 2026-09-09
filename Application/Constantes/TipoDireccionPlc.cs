namespace Application.Constantes;

/// <summary>
/// Qué área o registro tocar. Los valores y los nombres son EXACTAMENTE los del enum TipoDireccion
/// de axControlBE, porque los drivers se copian de ahí y switchean sobre esto: cambiarle un nombre
/// obliga a editar los dos drivers, que es justo lo que no queremos tener que hacer.
///
/// Los cuatro primeros son Modbus; del 4 al 8, Siemens.
/// </summary>
public enum TipoDireccionPlc
{
    Coil = 0,              // Modbus 0x01 (Read), 0x05/0x0F (Write)
    DiscreteInput = 1,     // Modbus 0x02 (Read) — sólo lectura
    HoldingRegister = 2,   // Modbus 0x03 (Read), 0x06/0x10 (Write)
    InputRegister = 3,     // Modbus 0x04 (Read) — sólo lectura
    S7Bit = 4,
    S7Byte = 5,
    S7Word = 6,
    S7DWord = 7,
    S7Real = 8
}

public static class TipoDireccionPlcParser
{
    /// <summary>
    /// El valor viene de una columna TEXT que carga un humano por endpoint. El CHECK de la base ya
    /// limita cuáles se pueden guardar, pero no el casing, así que acá se ignora.
    /// </summary>
    public static TipoDireccionPlc Parsear(string? texto) =>
        Enum.TryParse<TipoDireccionPlc>(texto, ignoreCase: true, out var tipo)
            ? tipo
            : throw new ArgumentException($"Tipo de dirección desconocido: '{texto}'.", nameof(texto));

    /// <summary>Si el tipo admite escritura. DiscreteInput e InputRegister son de sólo lectura.</summary>
    public static bool EsEscribible(TipoDireccionPlc tipo) =>
        tipo is not (TipoDireccionPlc.DiscreteInput or TipoDireccionPlc.InputRegister);

    /// <summary>Cuántos bytes ocupa. Es el ancho que el driver valida al escribir.</summary>
    public static int AnchoEnBytes(TipoDireccionPlc tipo) => tipo switch
    {
        TipoDireccionPlc.Coil or TipoDireccionPlc.DiscreteInput
            or TipoDireccionPlc.S7Bit or TipoDireccionPlc.S7Byte => 1,
        TipoDireccionPlc.HoldingRegister or TipoDireccionPlc.InputRegister
            or TipoDireccionPlc.S7Word => 2,
        TipoDireccionPlc.S7DWord or TipoDireccionPlc.S7Real => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), $"Tipo de dirección desconocido: {tipo}.")
    };
}
