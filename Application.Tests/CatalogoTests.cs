using Application.Constantes;
using Domain.Entities;
using Infrastructure.Data.Repositorios;
using Microsoft.AspNetCore.Mvc;
using Web.Controllers;

namespace Application.Tests;

public class CatalogoTests
{
    private static Equipo NuevoEquipo(string nombre = "bomba3") => new()
    {
        Nombre = nombre, TipoEquipo = CteFexit.TipoEquipoPlc, Ip = "10.0.0.20", Puerto = 102,
        Protocolo = CteFexit.ProtocoloSiemensS7, Rack = 0, Slot = 1,
    };

    private static Accion NuevaAccion(string codigo, long equipoId, string modo, bool habilitada = true) => new()
    {
        Codigo = codigo,
        Descripcion = "Descripción de " + codigo,
        Modo = modo,
        EquipoId = equipoId,
        Direccion = modo == CteFexit.ModoEscritura ? "DB1.DBX0.0" : null,
        TipoDireccion = modo == CteFexit.ModoEscritura ? CteFexit.S7Bit : null,
        Valor = modo == CteFexit.ModoEscritura ? 1 : null,
        UsaEnclavamientos = true,
        Habilitada = habilitada,
    };

    private static AccionesController NuevoController(DbDePrueba prueba) =>
        new(new AccionRepository(prueba.CrearContext()), new ServicioAccionesNoUsado());

    private static long SembrarEquipo(DbDePrueba prueba)
    {
        using var ctx = prueba.CrearContext();
        var equipo = NuevoEquipo();
        ctx.Equipos.Add(equipo);
        ctx.SaveChanges();
        return equipo.Id;
    }

    [Fact]
    public async Task ElCatalogoDevuelveCodigoDescripcionYModo()
    {
        // Los tres campos del contrato de §3, y NADA más: ni IP, ni dirección, ni el id del equipo.
        // AccionRemotaDto no tiene esas propiedades, así que no hay forma de que se cuelen.
        using var prueba = new DbDePrueba();
        var equipoId = SembrarEquipo(prueba);
        using (var ctx = prueba.CrearContext())
        {
            ctx.Acciones.Add(NuevaAccion("abrir_barrera_ingreso", equipoId, CteFexit.ModoEscritura));
            ctx.SaveChanges();
        }

        var resultado = await NuevoController(prueba).Listar(default);

        var fila = Assert.Single(Datos(resultado));
        Assert.Equal("abrir_barrera_ingreso", fila.Codigo);
        Assert.Equal("Descripción de abrir_barrera_ingreso", fila.Descripcion);
        Assert.Equal(CteFexit.ModoEscritura, fila.Modo);
    }

    [Fact]
    public async Task LaRespuestaNoTraeNadaDelEquipo()
    {
        // Explícito y por serialización, no por inspección de propiedades: es la promesa de §6 —
        // ningún mensaje que salga de Fexit lleva IP, dirección ni puerto.
        using var prueba = new DbDePrueba();
        var equipoId = SembrarEquipo(prueba);
        using (var ctx = prueba.CrearContext())
        {
            ctx.Acciones.Add(NuevaAccion("abrir_barrera", equipoId, CteFexit.ModoEscritura));
            ctx.SaveChanges();
        }

        var json = System.Text.Json.JsonSerializer.Serialize(Datos(await NuevoController(prueba).Listar(default)));

        Assert.DoesNotContain("10.0.0.20", json);
        Assert.DoesNotContain("DB1.DBX0.0", json);
        Assert.DoesNotContain("102", json);
    }

    [Fact]
    public async Task LasAccionesDeshabilitadasNoAparecen()
    {
        // El catálogo es lo que Dixit ofrece al superadmin en el alta: ofrecer una deshabilitada
        // sería ofrecer algo que va a dar 404 al ejecutarse.
        using var prueba = new DbDePrueba();
        var equipoId = SembrarEquipo(prueba);
        using (var ctx = prueba.CrearContext())
        {
            ctx.Acciones.Add(NuevaAccion("viva", equipoId, CteFexit.ModoLectura));
            ctx.Acciones.Add(NuevaAccion("apagada", equipoId, CteFexit.ModoLectura, habilitada: false));
            ctx.SaveChanges();
        }

        var resultado = await NuevoController(prueba).Listar(default);

        Assert.Equal("viva", Assert.Single(Datos(resultado)).Codigo);
    }

    [Fact]
    public async Task UnaDefinicionRotaNoTumbaElListado()
    {
        // M2: una fila cargada por SQL (no por el ABM, que la hubiera rechazado) con la definición de
        // parámetros ilegible se degrada a "sin parámetros" en el LISTADO, no tumba el catálogo
        // entero. La ejecución sigue fallando fuerte por su propio camino: eso no se toca acá.
        using var prueba = new DbDePrueba();
        var equipoId = SembrarEquipo(prueba);
        using (var ctx = prueba.CrearContext())
        {
            var rota = NuevaAccion("rota", equipoId, CteFexit.ModoLectura);
            rota.DefinicionParametrosJson = "esto no es json";
            var sana = NuevaAccion("sana", equipoId, CteFexit.ModoLectura);
            ctx.Acciones.Add(rota);
            ctx.Acciones.Add(sana);
            ctx.SaveChanges();
        }

        var resultado = await NuevoController(prueba).Listar(default);

        var filas = Datos(resultado);
        Assert.Equal(2, filas.Count);
        Assert.Empty(filas.Single(f => f.Codigo == "rota").Parametros);
        Assert.Empty(filas.Single(f => f.Codigo == "sana").Parametros);
    }

    [Fact]
    public async Task CatalogoVacio_DevuelveListaVaciaYNoNull()
    {
        // Es el estado de una instalación recién puesta, y Dixit tiene que poder pintar "todavía no
        // hay acciones cargadas" sin reventar.
        using var prueba = new DbDePrueba();

        var resultado = await NuevoController(prueba).Listar(default);

        Assert.Empty(Datos(resultado));
    }

    [Fact]
    public async Task ElCatalogoVieneOrdenadoPorCodigo()
    {
        // Determinista a propósito: es una lista que un humano lee en un combo del ABM de Dixit.
        using var prueba = new DbDePrueba();
        var equipoId = SembrarEquipo(prueba);
        using (var ctx = prueba.CrearContext())
        {
            ctx.Acciones.Add(NuevaAccion("zeta", equipoId, CteFexit.ModoLectura));
            ctx.Acciones.Add(NuevaAccion("alfa", equipoId, CteFexit.ModoLectura));
            ctx.SaveChanges();
        }

        var resultado = await NuevoController(prueba).Listar(default);

        Assert.Equal(["alfa", "zeta"], Datos(resultado).Select(a => a.Codigo));
    }

    [Fact]
    public async Task ElCatalogoPublicaLosParametrosPeroNuncaLaConfig()
    {
        using var prueba = new DbDePrueba();
        var equipoId = SembrarEquipo(prueba);
        using (var ctx = prueba.CrearContext())
        {
            var accion = NuevaAccion("abrir_barrera_tiempo", equipoId, CteFexit.ModoEscritura);
            accion.DefinicionParametrosJson = """[{"nombre":"minutos","tipo":"entero","etiqueta":"minutos","requerido":true,"minimo":1,"maximo":60}]""";
            accion.ConfigJson = """{"direccionParametro":"DB10.DBW4","tipoDireccionParametro":"S7Word"}""";
            ctx.Acciones.Add(accion);
            ctx.SaveChanges();
        }

        var fila = Assert.Single(Datos(await NuevoController(prueba).Listar(default)));

        Assert.Equal("minutos", Assert.Single(fila.Parametros).Nombre);
        Assert.DoesNotContain("DB10", System.Text.Json.JsonSerializer.Serialize(fila));
    }

    [Fact]
    public async Task BuscarUnCodigoQueNoExiste_DevuelveNull()
    {
        using var prueba = new DbDePrueba();

        var encontrada = await new AccionRepository(prueba.CrearContext())
            .BuscarPorCodigoAsync("no_existe", default);

        Assert.Null(encontrada);
    }

    private static IReadOnlyList<Application.Dtos.AccionRemotaDto> Datos(
        ActionResult<IReadOnlyList<Application.Dtos.AccionRemotaDto>> resultado) =>
        (IReadOnlyList<Application.Dtos.AccionRemotaDto>)Assert.IsType<OkObjectResult>(resultado.Result).Value!;
}
