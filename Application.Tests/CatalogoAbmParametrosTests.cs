using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Infrastructure.Data.Repositorios;
using Microsoft.AspNetCore.Mvc;
using Web.Controllers;

namespace Application.Tests;

public class CatalogoAbmParametrosTests
{
    private const string DefTextoYColor =
        """[{"nombre":"texto","tipo":"texto","etiqueta":"texto a mostrar","requerido":true,"largoMaximo":80},{"nombre":"color","tipo":"opcion","etiqueta":"color","requerido":false,"opciones":["verde","blanco","rojo","amarillo"],"porDefecto":"blanco"}]""";
    private const string DefMinutos =
        """[{"nombre":"minutos","tipo":"entero","etiqueta":"minutos","requerido":true,"minimo":1,"maximo":60}]""";

    private static CatalogoController Controller(DbDePrueba p) => new(new CatalogoRepository(p.CrearContext()));

    private static long Crear(DbDePrueba p, ControladorRequest req) =>
        (long)((ObjectResult)Controller(p).CrearControlador(req, default).GetAwaiter().GetResult().Result!).Value!;

    // Devuelven el id del EQUIPO: es de quien cuelga la acción desde el 2026-09-17. El controlador
    // se crea igual, debajo, porque de él sale el TipoEquipo contra el que valida el ABM.
    private static long Cartel(DbDePrueba p) =>
        p.SembrarEquipo(
            Crear(p, new ControladorRequest("cartel_ingreso", CteFexit.TipoEquipoCartel, "10.0.0.30", 10001, CteFexit.ProtocoloHuiduSdk, 0, 0)),
            "Cartel de ingreso");

    private static long Plc(DbDePrueba p) =>
        p.SembrarEquipo(
            Crear(p, new ControladorRequest("barrera", CteFexit.TipoEquipoPlc, "10.0.0.20", 102, CteFexit.ProtocoloSiemensS7, 0, 1)),
            "Barrera 1");

    private static AccionRequest EscrituraCartel(long equipoId, string? def, string? config) =>
        new("cartel_ingreso_mensaje", "Muestra un texto", CteFexit.ModoEscritura, equipoId,
            null, null, null, false, true, def, config);

    [Fact]
    public async Task UnCartelLibreConTextoYColor_SeCarga()
    {
        using var p = new DbDePrueba();
        await Controller(p).CrearAccion(EscrituraCartel(Cartel(p), DefTextoYColor, null), default);
    }

    [Theory]
    [InlineData("""{"modo":"logo"}""")]
    [InlineData("""{"modo":"pantalla_roja"}""")]
    [InlineData("""{"modo":"texto","texto":"ESPERE","color":"amarillo"}""")]
    public async Task UnCartelFijo_SeCarga(string config)
    {
        using var p = new DbDePrueba();
        await Controller(p).CrearAccion(EscrituraCartel(Cartel(p), null, config), default);
    }

    [Theory]
    [InlineData(null, null)]                                                  // ni fija ni libre
    [InlineData(null, """{"modo":"texto","texto":"ESPERE"}""")]              // fija sin color
    [InlineData(DefMinutos, null)]                                            // un cartel no toma enteros
    [InlineData("""[{"nombre":"color","tipo":"opcion","etiqueta":"color","requerido":true,"opciones":["verde"]}]""", null)] // libre sin texto
    public async Task UnCartelMalCargado_SeRechaza(string? def, string? config)
    {
        using var p = new DbDePrueba();
        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(p).CrearAccion(EscrituraCartel(Cartel(p), def, config), default));
    }

    [Fact]
    public async Task UnCartelNoAdmiteLecturas()
    {
        using var p = new DbDePrueba();
        var req = new AccionRequest("estado_cartel", "d", CteFexit.ModoLectura, Cartel(p), null, null, null, false, true);
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearAccion(req, default));
    }

    [Fact]
    public async Task UnPlcConParametroEnteroYSuDireccion_SeCarga()
    {
        using var p = new DbDePrueba();
        var req = new AccionRequest("abrir_barrera_tiempo", "Abre la barrera por un tiempo", CteFexit.ModoEscritura, Plc(p),
            "DB1.DBX0.0", CteFexit.S7Bit, 1, false, true, DefMinutos,
            """{"direccionParametro":"DB10.DBW4","tipoDireccionParametro":"S7Word"}""");
        await Controller(p).CrearAccion(req, default);
    }

    [Theory]
    [InlineData(null)]                                                                      // sin dónde escribirlo
    [InlineData("""{"direccionParametro":"DB10.DBW4","tipoDireccionParametro":"InputRegister"}""")] // sólo lectura
    public async Task UnPlcConParametroSinDireccionValida_SeRechaza(string? config)
    {
        using var p = new DbDePrueba();
        var req = new AccionRequest("abrir_barrera_tiempo", "d", CteFexit.ModoEscritura, Plc(p),
            "DB1.DBX0.0", CteFexit.S7Bit, 1, false, true, DefMinutos, config);
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearAccion(req, default));
    }

    private static string DefEntero(int minimo, int maximo) =>
        $$"""[{"nombre":"minutos","tipo":"entero","etiqueta":"minutos","requerido":true,"minimo":{{minimo}},"maximo":{{maximo}}}]""";

    private static AccionRequest AccionConParametroPlc(long equipoId, string def, string tipoDireccionParametro) =>
        new("abrir_barrera_tiempo", "d", CteFexit.ModoEscritura, equipoId,
            "DB1.DBX0.0", CteFexit.S7Bit, 1, false, true, def,
            $$"""{"direccionParametro":"DB10.DBW4","tipoDireccionParametro":"{{tipoDireccionParametro}}"}""");

    // I2: el ABM no puede aceptar un rango que la ejecución no pueda escribir (spec de la revisión
    // final). El criterio es el mismo que usa ConversorValores.ABytes según el ancho del tipo.
    [Fact]
    public async Task UnMaximoQueNoEntraEnElAnchoDeLaDireccion_SeRechaza()
    {
        using var p = new DbDePrueba();
        var req = AccionConParametroPlc(Plc(p), DefEntero(0, 100000), CteFexit.S7Word);
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearAccion(req, default));
    }

    [Fact]
    public async Task UnMaximoQueEntraJustoEnElAnchoDeLaDireccion_SeAcepta()
    {
        using var p = new DbDePrueba();
        var req = AccionConParametroPlc(Plc(p), DefEntero(0, 65535), CteFexit.S7Word);
        await Controller(p).CrearAccion(req, default);
    }

    [Fact]
    public async Task UnMinimoNegativo_SeRechaza()
    {
        using var p = new DbDePrueba();
        var req = AccionConParametroPlc(Plc(p), DefEntero(-1, 60), CteFexit.S7Word);
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearAccion(req, default));
    }

    [Fact]
    public async Task UnParametroEnteroSobreUnBit_SeRechaza()
    {
        using var p = new DbDePrueba();
        var req = AccionConParametroPlc(Plc(p), DefEntero(0, 1), CteFexit.S7Bit);
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearAccion(req, default));
    }

    [Fact]
    public async Task UnPlcNoAdmiteParametroDeTexto()
    {
        using var p = new DbDePrueba();
        var req = new AccionRequest("x", "d", CteFexit.ModoEscritura, Plc(p),
            "DB1.DBX0.0", CteFexit.S7Bit, 1, false, true, DefTextoYColor);
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearAccion(req, default));
    }

    [Fact]
    public async Task UnaLecturaNoLlevaParametros()
    {
        using var p = new DbDePrueba();
        var req = new AccionRequest("estado", "d", CteFexit.ModoLectura, Plc(p), null, null, null, true, true, DefMinutos);
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearAccion(req, default));
    }

    [Fact]
    public async Task UnControladorCartelExigeElProtocoloHuidu()
    {
        using var p = new DbDePrueba();
        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(p).CrearControlador(
            new ControladorRequest("c", CteFexit.TipoEquipoCartel, "10.0.0.30", 10001, CteFexit.ProtocoloSiemensS7, 0, 0), default));
    }

    [Fact]
    public async Task ElListadoDelAbmMuestraDefinicionYConfig()
    {
        using var p = new DbDePrueba();
        await Controller(p).CrearAccion(EscrituraCartel(Cartel(p), DefTextoYColor, null), default);

        var lista = (IReadOnlyList<AccionCatalogoDto>)((ObjectResult)(await Controller(p).ListarAcciones(default)).Result!).Value!;

        Assert.Contains("largoMaximo", Assert.Single(lista).DefinicionParametrosJson);
    }
}
