namespace Domain.Entities;

/// <summary>
/// Un equipo de planta. Es el único lugar que sabe CÓMO se llega: IP, puerto, protocolo, rack y
/// slot. Dixit nunca ve nada de esto (§2 del spec): manda un código y se entera del resultado.
///
/// Rack y Slot son de Siemens y quedan en 0 para Modbus. Viven en la fila y no en una tabla aparte
/// por protocolo porque son dos enteros: una tabla por protocolo para eso sería ceremonia sin
/// beneficio, y el día que entre un protocolo con cinco campos propios se resuelve entonces.
/// </summary>
public class Equipo
{
    public long Id { get; set; }

    /// <summary>Para que un humano encuentre la fila ("bomba silo 3"). No lo ve Dixit ni el modelo.</summary>
    public string Nombre { get; set; } = default!;

    /// <summary>Qué ejecutor lo atiende. La factory switchea por esto, NO por protocolo (§10.9).</summary>
    public string TipoEquipo { get; set; } = default!;

    public string Ip { get; set; } = default!;
    public int Puerto { get; set; }

    /// <summary>SiemensS7 | ModbusTcp | Simulado. Detalle interno del ejecutor de PLC.</summary>
    public string Protocolo { get; set; } = default!;

    /// <summary>
    /// Sólo Siemens: qué CPU es (S7200 … S71500). Ignorado en Modbus y en Simulado, igual que Rack y
    /// Slot.
    ///
    /// Es una columna y no una constante fija porque S7netplus usa el CpuType para negociar el tamaño
    /// de PDU y para resolver el direccionamiento de DB: contra el modelo equivocado la primera
    /// lectura falla con una PlcException genérica, que nadie va a leer como "el modelo está mal" —
    /// van a revisar el cableado y la dirección. axControlBE tiene esta misma columna y su factory
    /// se olvida de pasarla, así que corre siempre en el default S7300; acá la factory SÍ la pasa,
    /// que es el paso que allá quedó sin hacer.
    /// </summary>
    public string Modelo { get; set; } = "S7300";

    /// <summary>Sólo Siemens. 0 en el resto.</summary>
    public int Rack { get; set; }

    /// <summary>Sólo Siemens. 0 en el resto.</summary>
    public int Slot { get; set; }

    public List<Enclavamiento> Enclavamientos { get; set; } = [];
}
