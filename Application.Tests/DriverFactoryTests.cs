using Application.Constantes;
using Application.Settings;
using Domain.Entities;
using Infrastructure.Drivers;
using Infrastructure.Drivers.Modbus;
using Infrastructure.Drivers.Siemens;
using Infrastructure.Drivers.Simulado;

namespace Application.Tests;

public class DriverFactoryTests
{
    private static readonly FexitSettings Settings = new() { TimeoutEquipoMs = 3000 };

    private static Equipo Equipo(string protocolo, string modelo = CteFexit.ModeloS7300) => new()
    {
        Nombre = "equipo", TipoEquipo = CteFexit.TipoEquipoPlc, Ip = "10.0.0.20",
        Puerto = 502, Protocolo = protocolo, Modelo = modelo, Rack = 0, Slot = 1,
    };

    [Fact]
    public void SiemensS7_DaElDriverDeSiemens()
    {
        // Construir el driver NO abre la conexión: se puede probar la factory sin PLC.
        using var driver = new PlcDriverFactory(Settings).Crear(Equipo(CteFexit.ProtocoloSiemensS7));

        Assert.IsType<SiemensS7Driver>(driver);
    }

    [Fact]
    public void ModbusTcp_DaElDriverDeModbus()
    {
        using var driver = new PlcDriverFactory(Settings).Crear(Equipo(CteFexit.ProtocoloModbusTcp));

        Assert.IsType<ModbusTcpDriver>(driver);
    }

    [Fact]
    public void Simulado_DaElDriverSimulado()
    {
        using var driver = new PlcDriverFactory(Settings).Crear(Equipo(CteFexit.ProtocoloSimulado));

        Assert.IsType<DriverSimulado>(driver);
    }

    [Fact]
    public void UnProtocoloDesconocido_Tira()
    {
        // El CHECK de la base ya lo impide, pero la factory es la segunda capa: cubre una fila
        // cargada contra una BD vieja o migrada a mano.
        var fabrica = new PlcDriverFactory(Settings);

        Assert.Throws<NotSupportedException>(() => fabrica.Crear(Equipo("Profibus")));
    }

    [Theory]
    [InlineData(CteFexit.ModeloS7200)]
    [InlineData(CteFexit.ModeloS7300)]
    [InlineData(CteFexit.ModeloS71200)]
    [InlineData(CteFexit.ModeloS71500)]
    public void ElModeloDeCpuLlegaAlDriverDeSiemens(string modelo)
    {
        // Que el driver se construya sin tirar es lo que prueba que el modelo se está pasando y que
        // MapModeloToCpuType lo reconoce. Es el paso que axControlBE se saltea: su factory nunca pasa
        // plc.Modelo, así que corre siempre en el default y la columna que tienen no hace nada.
        using var driver = new PlcDriverFactory(Settings).Crear(Equipo(CteFexit.ProtocoloSiemensS7, modelo));

        Assert.IsType<SiemensS7Driver>(driver);
    }

    [Fact]
    public void UnModeloDeCpuDesconocido_Tira()
    {
        // Segunda capa del CHECK. Y tira al CONSTRUIR, no al leer: si se colara hasta la primera
        // lectura, el síntoma sería una PlcException genérica y nadie sospecharía del modelo.
        var fabrica = new PlcDriverFactory(Settings);

        Assert.Throws<NotSupportedException>(() => fabrica.Crear(Equipo(CteFexit.ProtocoloSiemensS7, "LOGO8")));
    }
}
