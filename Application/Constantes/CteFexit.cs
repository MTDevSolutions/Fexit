namespace Application.Constantes;

/// <summary>Único lugar con estos valores; nunca strings sueltos.</summary>
public static class CteFexit
{
    // Modo de la acción. Lo que Dixit verifica mandando modoEsperado (§5).
    public const string ModoLectura = "lectura";
    public const string ModoEscritura = "escritura";

    // Tipos de parámetro de una acción (spec 2026-09-12 §2.1). A lo sumo uno de cada tipo por
    // acción: es lo que deja fija la herramienta del modelo del lado de Dixit.
    public const string ParametroTexto = "texto";
    public const string ParametroOpcion = "opcion";
    public const string ParametroEntero = "entero";
    public static readonly string[] TiposParametro = [ParametroTexto, ParametroOpcion, ParametroEntero];

    // Tipo de equipo: por esto switchea la factory de ejecutores, NO por protocolo (§10.9), para que
    // un sensor IoT sea una clase nueva y no un rediseño.
    public const string TipoEquipoPlc = "plc";
    public const string TipoEquipoCartel = "cartel";
    public static readonly string[] TiposEquipo = [TipoEquipoPlc, TipoEquipoCartel];

    // Protocolo: detalle interno del ejecutor de PLC.
    public const string ProtocoloSiemensS7 = "SiemensS7";
    public const string ProtocoloModbusTcp = "ModbusTcp";

    // El SDK de Huidu (copiado de axControlBE). Sólo lo atiende el ejecutor de cartel.
    public const string ProtocoloHuiduSdk = "HuiduSdk";

    // Cartel: colores del texto, y los modos de una acción fija.
    public static readonly string[] ColoresCartel = ["verde", "blanco", "rojo", "amarillo"];
    public const string CartelModoTexto = "texto";
    public const string CartelModoLogo = "logo";
    public const string CartelModoPantallaVerde = "pantalla_verde";
    public const string CartelModoPantallaRoja = "pantalla_roja";
    public static readonly string[] CartelModos = [CartelModoTexto, CartelModoLogo, CartelModoPantallaVerde, CartelModoPantallaRoja];

    /// <summary>
    /// El simulador de §9. Es un protocolo más y no un modo de arranque especial: así el circuito
    /// Dixit → Fexit → respuesta se cierra contra una instancia REAL de Fexit sin PLC y sin ramas
    /// condicionales en el código de producción.
    /// </summary>
    public const string ProtocoloSimulado = "Simulado";

    // Modelo de CPU, sólo Siemens. Son exactamente los cinco que conoce S7netplus: si aparece uno que
    // no está acá, la librería no lo sabe manejar y no alcanza con agregar la constante.
    public const string ModeloS7200 = "S7200";
    public const string ModeloS7300 = "S7300";
    public const string ModeloS7400 = "S7400";
    public const string ModeloS71200 = "S71200";
    public const string ModeloS71500 = "S71500";

    // Tipos de dirección. Los cuatro primeros son Modbus, el resto Siemens; los nombres son los del
    // enum TipoDireccion que toman los drivers copiados de axControlBE.
    public const string Coil = "Coil";
    public const string DiscreteInput = "DiscreteInput";
    public const string HoldingRegister = "HoldingRegister";
    public const string InputRegister = "InputRegister";
    public const string S7Bit = "S7Bit";
    public const string S7Byte = "S7Byte";
    public const string S7Word = "S7Word";
    public const string S7DWord = "S7DWord";
    public const string S7Real = "S7Real";

    public static bool EsModoValido(string? m) => m is ModoLectura or ModoEscritura;

    public static readonly string[] Protocolos = [ProtocoloSiemensS7, ProtocoloModbusTcp, ProtocoloSimulado, ProtocoloHuiduSdk];

    public static readonly string[] Modelos = [ModeloS7200, ModeloS7300, ModeloS7400, ModeloS71200, ModeloS71500];

    public static readonly string[] TiposDireccion =
        [Coil, DiscreteInput, HoldingRegister, InputRegister, S7Bit, S7Byte, S7Word, S7DWord, S7Real];
}
