using Application.Interfaces;
using Application.Services;
using Application.Settings;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Application.Tests;

public class LectorEstadosTests
{
    /// <summary>Cuenta conexiones y puede fingir un controlador caído, por IP.</summary>
    private sealed class FabricaEspia(params string[] ipsCaidas) : IPlcDriverFactory
    {
        public List<string> IpsConectadas { get; } = [];
        public IPlcDriver Crear(Controlador c)
        {
            IpsConectadas.Add(c.Ip);
            return new DriverFalso(falla: ipsCaidas.Contains(c.Ip), valor: 1);
        }
    }

    /// <summary>Guarda las excepciones que se loguean, para probar que la causa real no se pierde.</summary>
    private sealed class LoggerEspia : ILogger<LectorEstados>
    {
        public List<Exception> ExcepcionesLogueadas { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) ExcepcionesLogueadas.Add(exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task Abre_una_conexion_por_controlador_y_no_una_por_senal()
    {
        var estados = SeisEstadosEnDosControladores();
        var fabrica = new FabricaEspia();

        await new LectorEstados(fabrica, Opciones(), Logger())
            .LeerAsync(estados, estados.Select(e => e.Codigo).ToList(), default);

        Assert.Equal(2, fabrica.IpsConectadas.Count);
        Assert.Equal(["10.0.0.1", "10.0.0.2"], fabrica.IpsConectadas.Order());
    }

    [Fact]
    public async Task Un_controlador_caido_deja_sus_filas_presentes_y_vacias()
    {
        // Si desaparecieran, el redactor escribiría "está todo bien" sin mencionar lo que no sabe, y
        // ese silencio se lee como buena noticia.
        var estados = SeisEstadosEnDosControladores();
        var lector = new LectorEstados(new FabricaEspia("10.0.0.2"), Opciones(), Logger());

        var r = await lector.LeerAsync(estados, estados.Select(e => e.Codigo).ToList(), default);

        Assert.Equal(6, r.Filas.Count);
        Assert.Equal(3, r.Filas.Count(f => (string?)f["valor"] == TraductorEstado.SinLectura));
        Assert.Contains("no se pudo leer", r.Detalle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_controlador_caido_loguea_la_causa_real()
    {
        // El detalle que sale por la API sólo dice "PLC B: no se pudo leer": sin este log, la causa
        // (timeout, rechazo de conexión, protocolo) se pierde para siempre, porque este endpoint
        // nunca propaga la excepción — a diferencia de EjecutorPlc, acá no hay 502 que
        // ManejadorExcepciones pueda loguear.
        var estados = SeisEstadosEnDosControladores();
        var logger = new LoggerEspia();
        var lector = new LectorEstados(new FabricaEspia("10.0.0.2"), Opciones(), logger);

        await lector.LeerAsync(estados, estados.Select(e => e.Codigo).ToList(), default);

        Assert.Single(logger.ExcepcionesLogueadas);
    }

    [Fact]
    public async Task Un_codigo_que_no_existe_se_reporta_y_no_voltea_el_pedido()
    {
        var estados = SeisEstadosEnDosControladores();
        var pedidos = estados.Select(e => e.Codigo).Append("no_existe").ToList();

        var r = await new LectorEstados(new FabricaEspia(), Opciones(), Logger())
            .LeerAsync(estados, pedidos, default);

        Assert.Equal(6, r.Filas.Count);
        Assert.Contains("no_existe", r.Detalle);
    }

    /// <summary>
    /// Tres estados en cada uno de dos controladores, cada uno con su equipo y su sector, todo en
    /// memoria: LectorEstados no toca la base, así que no hace falta DbDePrueba acá.
    /// </summary>
    private static List<Estado> SeisEstadosEnDosControladores()
    {
        var sector = new Sector { Id = 1, Nombre = "Portería" };

        var controladorA = new Controlador { Id = 1, Nombre = "PLC A", TipoEquipo = "plc", Ip = "10.0.0.1",
            Puerto = 102, Protocolo = "SiemensS7", Modelo = "S71200" };
        var controladorB = new Controlador { Id = 2, Nombre = "PLC B", TipoEquipo = "plc", Ip = "10.0.0.2",
            Puerto = 102, Protocolo = "SiemensS7", Modelo = "S71200" };

        var equipoA = new Equipo { Id = 1, Nombre = "Barrera 1", Descripcion = "d", SectorId = sector.Id,
            Sector = sector, ControladorId = controladorA.Id, Controlador = controladorA };
        var equipoB = new Equipo { Id = 2, Nombre = "Barrera 2", Descripcion = "d", SectorId = sector.Id,
            Sector = sector, ControladorId = controladorB.Id, Controlador = controladorB };

        List<Estado> EstadosDe(Equipo equipo, string prefijo) =>
        [
            new() { Id = equipo.Id * 10 + 1, EquipoId = equipo.Id, Equipo = equipo,
                Codigo = $"{prefijo}_1", Nombre = "Estado 1", Descripcion = "d",
                Direccion = "I0.0", TipoDireccion = "S7Bit",
                Etiquetas = new() { [0] = "apagado", [1] = "prendido" } },
            new() { Id = equipo.Id * 10 + 2, EquipoId = equipo.Id, Equipo = equipo,
                Codigo = $"{prefijo}_2", Nombre = "Estado 2", Descripcion = "d",
                Direccion = "I0.1", TipoDireccion = "S7Bit",
                Etiquetas = new() { [0] = "apagado", [1] = "prendido" } },
            new() { Id = equipo.Id * 10 + 3, EquipoId = equipo.Id, Equipo = equipo,
                Codigo = $"{prefijo}_3", Nombre = "Estado 3", Descripcion = "d",
                Direccion = "I0.2", TipoDireccion = "S7Bit",
                Etiquetas = new() { [0] = "apagado", [1] = "prendido" } },
        ];

        return [.. EstadosDe(equipoA, "a"), .. EstadosDe(equipoB, "b")];
    }

    private static IOptions<FexitSettings> Opciones() => Options.Create(new FexitSettings());

    private static ILogger<LectorEstados> Logger() => NullLogger<LectorEstados>.Instance;
}
