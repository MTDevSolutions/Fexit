using Application.Constantes;
using Application.Interfaces;
using Application.Settings;
using Domain.Entities;
using Infrastructure.Drivers.Modbus;
using Infrastructure.Drivers.Siemens;
using Infrastructure.Drivers.Simulado;
using Microsoft.Extensions.Options;

namespace Infrastructure.Drivers;

/// <summary>
/// Protocolo → driver. Es la factory INTERNA del ejecutor de PLC: la de afuera switchea por tipo de
/// equipo (§10.9), y el protocolo no sale de este nivel.
///
/// Construir el driver NO abre la conexión, así que esto se puede probar sin un PLC en la red.
/// </summary>
public class PlcDriverFactory(FexitSettings settings) : IPlcDriverFactory
{
    public PlcDriverFactory(IOptions<FexitSettings> settings) : this(settings.Value) { }

    public IPlcDriver Crear(Equipo equipo) => equipo.Protocolo switch
    {
        // El Modelo se pasa: es el paso que axControlBE se saltea, y por el que su columna Modelo
        // no hace nada. Un modelo desconocido tira acá, antes de tocar la red.
        CteFexit.ProtocoloSiemensS7 =>
            new SiemensS7Driver(equipo.Ip, equipo.Puerto, equipo.Rack, equipo.Slot, equipo.Modelo,
                                settings.TimeoutEquipoMs),
        CteFexit.ProtocoloModbusTcp =>
            new ModbusTcpDriver(equipo.Ip, equipo.Puerto, timeout: settings.TimeoutEquipoMs),
        CteFexit.ProtocoloSimulado =>
            new DriverSimulado(equipo.Ip, equipo.Puerto),
        // El CHECK de la base ya lo impide; esto cubre una fila cargada contra una BD vieja.
        _ => throw new NotSupportedException($"Protocolo no soportado: {equipo.Protocolo}")
    };
}
