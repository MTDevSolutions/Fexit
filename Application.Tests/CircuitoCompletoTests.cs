using System.Net;
using System.Net.Http.Json;
using Application.Constantes;
using Application.Dtos;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application.Tests;

/// <summary>
/// El circuito de §9, sin PLC: levanta la app entera, carga un controlador con Protocolo=Simulado por el
/// ABM y ejecuta contra él por HTTP. Prueba lo que ninguna otra prueba de este plan toca: que el
/// middleware, el ruteo, la serialización y la BD estén bien enchufados entre sí.
///
/// Cada instancia usa una BD SQLite propia en disco temporal, y no in-memory: la app real corre
/// Migrate() en el wiring, y una BD in-memory se evaporaría entre ese wiring y el primer pedido.
/// </summary>
public class CircuitoCompletoTests : IClassFixture<FexitEnMemoria>
{
    private readonly FexitEnMemoria _app;

    public CircuitoCompletoTests(FexitEnMemoria app) => _app = app;

    private HttpClient ClienteConClave()
    {
        var cliente = _app.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", FexitEnMemoria.Clave);
        return cliente;
    }

    /// <summary>
    /// El equipo del que cuelgan la acción y los enclavamientos. Va por EF y no por HTTP porque el
    /// ABM de equipos todavía no existe; lo que este test cuida es el circuito de EJECUCIÓN, que
    /// sigue yendo de punta a punta por HTTP.
    /// </summary>
    private long SembrarEquipo(long controladorId, string nombre)
    {
        using var scope = _app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<FexitDbContext>();

        var sector = ctx.Sectores.FirstOrDefault();
        if (sector is null)
        {
            sector = new Sector { Nombre = "General", Orden = 0 };
            ctx.Sectores.Add(sector);
            ctx.SaveChanges();
        }

        var equipo = new Equipo
        {
            Nombre = nombre, Descripcion = "d", SectorId = sector.Id, ControladorId = controladorId,
        };
        ctx.Equipos.Add(equipo);
        ctx.SaveChanges();
        return equipo.Id;
    }

    [Fact]
    public async Task SinLaClave_NingunEndpointContesta()
    {
        var cliente = _app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/acciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/catalogo/controladores")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task CargarUnControladorSimuladoYEjecutarUnaEscritura()
    {
        var cliente = ClienteConClave();
        var sufijo = Guid.NewGuid().ToString("N")[..6];

        var controladorId = await Crear<long>(cliente, "/catalogo/controladores", new ControladorRequest(
            $"bomba_{sufijo}", CteFexit.TipoEquipoPlc, "10.0.0.50", 502, CteFexit.ProtocoloSimulado, 0, 0));
        var equipoId = SembrarEquipo(controladorId, "equipo_" + sufijo);

        var codigo = $"arrancar_{sufijo}";
        await Crear<long>(cliente, "/catalogo/acciones", new AccionRequest(
            codigo, "Arranca la bomba", CteFexit.ModoEscritura, equipoId,
            "40001", CteFexit.HoldingRegister, 1, UsaEnclavamientos: false, Habilitada: true));

        // Aparece en el catálogo publicado, que es lo que Dixit lee para el alta.
        var catalogo = await cliente.GetFromJsonAsync<List<AccionRemotaDto>>("/acciones");
        Assert.Contains(catalogo!, a => a.Codigo == codigo && a.Modo == CteFexit.ModoEscritura);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/acciones/{codigo}/ejecutar", new EjecutarAccionRequest(CteFexit.ModoEscritura));

        respuesta.EnsureSuccessStatusCode();
        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoAccion>();
        Assert.True(resultado!.Exito);
        Assert.Equal("Escritura realizada.", resultado.Detalle);
    }

    [Fact]
    public async Task ElModoQueNoCoincideDa409PorHttp()
    {
        // La defensa de §5, de punta a punta. Es el test que prueba que la excepción del servicio
        // atraviesa el middleware y sale como 409 y no como 500.
        var cliente = ClienteConClave();
        var sufijo = Guid.NewGuid().ToString("N")[..6];

        var controladorId = await Crear<long>(cliente, "/catalogo/controladores", new ControladorRequest(
            $"barrera_{sufijo}", CteFexit.TipoEquipoPlc, "10.0.0.51", 502, CteFexit.ProtocoloSimulado, 0, 0));
        var equipoId = SembrarEquipo(controladorId, "equipo_" + sufijo);
        var codigo = $"abrir_{sufijo}";
        await Crear<long>(cliente, "/catalogo/acciones", new AccionRequest(
            codigo, "Abre la barrera", CteFexit.ModoEscritura, equipoId,
            "1", CteFexit.Coil, 1, UsaEnclavamientos: false, Habilitada: true));

        var respuesta = await cliente.PostAsJsonAsync(
            $"/acciones/{codigo}/ejecutar", new EjecutarAccionRequest(CteFexit.ModoLectura));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnCodigoQueNoExisteDa404PorHttp()
    {
        var respuesta = await ClienteConClave().PostAsJsonAsync(
            "/acciones/no_existe_nada/ejecutar", new EjecutarAccionRequest(CteFexit.ModoEscritura));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnaLecturaDevuelveLaTablaDeEnclavamientosPorHttp()
    {
        // El caso que más importa que funcione punta a punta: la tabla viaja serializada y Dixit la
        // mete en el carril del SQL hacia la segunda pasada. Con el controlador simulado en 0 y valoresOk
        // en [1], tiene que salir "no".
        var cliente = ClienteConClave();
        var sufijo = Guid.NewGuid().ToString("N")[..6];

        var controladorId = await Crear<long>(cliente, "/catalogo/controladores", new ControladorRequest(
            $"silo_{sufijo}", CteFexit.TipoEquipoPlc, "10.0.0.52", 502, CteFexit.ProtocoloSimulado, 0, 0));
        var equipoId = SembrarEquipo(controladorId, "equipo_" + sufijo);
        await Crear<long>(cliente, $"/catalogo/equipos/{equipoId}/enclavamientos",
            new EnclavamientoRequest("40010", CteFexit.HoldingRegister, "Portón de playa", [1], 1));

        var codigo = $"estado_{sufijo}";
        await Crear<long>(cliente, "/catalogo/acciones", new AccionRequest(
            codigo, "Estado del silo", CteFexit.ModoLectura, equipoId,
            null, null, null, UsaEnclavamientos: true, Habilitada: true));

        var respuesta = await cliente.PostAsJsonAsync(
            $"/acciones/{codigo}/ejecutar", new EjecutarAccionRequest(CteFexit.ModoLectura));

        respuesta.EnsureSuccessStatusCode();
        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoAccion>();
        Assert.True(resultado!.Exito);
        Assert.Equal(["enclavamiento", "en condicion"], resultado.Columnas);
        var fila = Assert.Single(resultado.Filas);
        Assert.Equal("Portón de playa", fila["enclavamiento"]?.ToString());
        Assert.Equal("no", fila["en condicion"]?.ToString());
    }

    [Fact]
    public async Task LaPrecondicionQueNoDaVuelveComo200ConExitoFalse()
    {
        // §6: precondición no cumplida es 200 con exito:false, NO un código de error. La acción se
        // ejecutó y decidió no escribir.
        var cliente = ClienteConClave();
        var sufijo = Guid.NewGuid().ToString("N")[..6];

        var controladorId = await Crear<long>(cliente, "/catalogo/controladores", new ControladorRequest(
            $"bomba2_{sufijo}", CteFexit.TipoEquipoPlc, "10.0.0.53", 502, CteFexit.ProtocoloSimulado, 0, 0));
        var equipoId = SembrarEquipo(controladorId, "equipo_" + sufijo);
        await Crear<long>(cliente, $"/catalogo/equipos/{equipoId}/enclavamientos",
            new EnclavamientoRequest("40020", CteFexit.HoldingRegister, "Térmica", [1], 1));

        var codigo = $"arrancar2_{sufijo}";
        await Crear<long>(cliente, "/catalogo/acciones", new AccionRequest(
            codigo, "Arranca", CteFexit.ModoEscritura, equipoId,
            "40021", CteFexit.HoldingRegister, 1, UsaEnclavamientos: true, Habilitada: true));

        var respuesta = await cliente.PostAsJsonAsync(
            $"/acciones/{codigo}/ejecutar", new EjecutarAccionRequest(CteFexit.ModoEscritura));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoAccion>();
        Assert.False(resultado!.Exito);
        Assert.Equal("Precondición no cumplida: Térmica.", resultado.Detalle);
    }

    [Fact]
    public async Task ElControladorInalcanzableDa502()
    {
        var cliente = ClienteConClave();
        var sufijo = Guid.NewGuid().ToString("N")[..6];

        var controladorId = await Crear<long>(cliente, "/catalogo/controladores", new ControladorRequest(
            $"caido_{sufijo}", CteFexit.TipoEquipoPlc, "10.255.255.255", 502, CteFexit.ProtocoloSimulado, 0, 0));
        var equipoId = SembrarEquipo(controladorId, "equipo_" + sufijo);
        var codigo = $"tocar_{sufijo}";
        await Crear<long>(cliente, "/catalogo/acciones", new AccionRequest(
            codigo, "Toca algo", CteFexit.ModoEscritura, equipoId,
            "1", CteFexit.Coil, 1, UsaEnclavamientos: false, Habilitada: true));

        var respuesta = await cliente.PostAsJsonAsync(
            $"/acciones/{codigo}/ejecutar", new EjecutarAccionRequest(CteFexit.ModoEscritura));

        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        Assert.DoesNotContain("10.255.255.255", await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<T> Crear<T>(HttpClient cliente, string ruta, object cuerpo)
    {
        var respuesta = await cliente.PostAsJsonAsync(ruta, cuerpo);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>())!;
    }
}

/// <summary>
/// La app entera, con una BD SQLite temporal propia y una clave conocida. Se comparte entre los tests
/// de la clase (IClassFixture) porque levantar el host es caro y ninguno de ellos pisa al otro: cada
/// uno usa códigos y nombres con sufijo único.
///
/// Implementa IAsyncDisposable y no IAsyncLifetime: WebApplicationFactory&lt;T&gt; ya expone
/// IAsyncDisposable con ValueTask DisposeAsync(), y esa es la firma que xUnit 2.9 invoca para limpiar
/// un IClassFixture cuando la implementa. IAsyncLifetime pide Task DisposeAsync(), que choca con la de
/// la base — el "new" del brief no alcanza a resolver esa colisión de forma portable. El requisito es
/// el comportamiento (la BD temporal se borra al terminar la clase), no la interfaz puntual.
/// </summary>
public class FexitEnMemoria : WebApplicationFactory<Program>, IAsyncDisposable
{
    public const string Clave = "clave-de-prueba";

    private readonly string _archivo = Path.Combine(Path.GetTempPath(), $"fexit-test-{Guid.NewGuid():N}.db");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FexitSettings:ApiKey"] = Clave,
            ["FexitSettings:ConnectionString"] = $"Data Source={_archivo}",
            ["FexitSettings:TimeoutEquipoMs"] = "1000",
        }));
        return base.CreateHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Borrar el archivo y los del WAL. Si queda, no rompe nada: es Temp.
        foreach (var sufijo in new[] { "", "-wal", "-shm" })
            try { File.Delete(_archivo + sufijo); } catch { /* mejor esfuerzo */ }
    }
}
