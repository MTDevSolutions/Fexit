using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Infrastructure.Data.Repositorios;
using Microsoft.AspNetCore.Mvc;
using Web.Controllers;

namespace Application.Tests;

public class CatalogoAbmTests
{
    private static CatalogoController Controller(DbDePrueba prueba) =>
        new(new CatalogoRepository(prueba.CrearContext()));

    private static ControladorRequest Controlador(string nombre = "bomba3", string protocolo = CteFexit.ProtocoloSiemensS7) =>
        new(nombre, CteFexit.TipoEquipoPlc, "10.0.0.20", 102, protocolo, 0, 1);

    private static AccionRequest Escritura(long controladorId, string codigo = "arrancar_bomba3") =>
        new(codigo, "Arranca la bomba", CteFexit.ModoEscritura, controladorId,
            "DB1.DBX0.0", CteFexit.S7Bit, 1, true, true);

    private static AccionRequest Lectura(long controladorId, string codigo = "estado_bomba3") =>
        new(codigo, "Estado de la bomba", CteFexit.ModoLectura, controladorId, null, null, null, true, true);

    private static long CrearControlador(DbDePrueba prueba, ControladorRequest? req = null) =>
        Valor<long>(Controller(prueba).CrearControlador(req ?? Controlador(), default).GetAwaiter().GetResult());

    private static T Valor<T>(ActionResult<T> resultado) =>
        (T)((ObjectResult)resultado.Result!).Value!;

    [Fact]
    public async Task CrearUnControladorLoDevuelveEnElListado()
    {
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearControlador(Controlador(), default);

        var controladores = Valor(await Controller(prueba).ListarControladores(default));

        Assert.Equal("bomba3", Assert.Single(controladores).Nombre);
    }

    [Fact]
    public async Task UnProtocoloDesconocidoSeRechazaConMensajeUtil()
    {
        // El CHECK de la base lo frena igual, pero como DbUpdateException: un 500 ilegible para quien
        // está cargando. Acá es un 400 que dice cuáles son los válidos.
        using var prueba = new DbDePrueba();

        var ex = await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearControlador(Controlador(protocolo: "Profibus"), default));

        Assert.Contains(CteFexit.ProtocoloModbusTcp, ex.Message);
    }

    [Fact]
    public async Task UnModeloDeCpuDesconocidoSeRechazaConMensajeUtil()
    {
        // El modelo se valida aunque el protocolo lo ignore: si en un Modbus se dejara pasar
        // cualquier cosa, el día que ese controlador cambie a SiemensS7 la fila quedaría con un modelo
        // inválido y el error saldría recién al ejecutar, como PlcException genérica.
        using var prueba = new DbDePrueba();

        var ex = await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearControlador(Controlador() with { Modelo = "LOGO8" }, default));

        Assert.Contains(CteFexit.ModeloS71500, ex.Message);
    }

    [Fact]
    public async Task DosControladoresConElMismoNombreSeRechaza()
    {
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearControlador(Controlador(), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearControlador(Controlador(), default));
    }

    [Fact]
    public async Task UnNombreDeControladorQueSoloDifiereEnEspaciosSeRechaza()
    {
        // El chequeo de duplicado comparaba el nombre crudo y guardaba el trimeado, así que
        // " bomba3 " pasaba el chequeo contra un "bomba3" existente y después chocaba contra el
        // índice único: DbUpdateException, o sea el 500 ilegible que esta capa existe para evitar.
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearControlador(Controlador(), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearControlador(Controlador() with { Nombre = "  bomba3  " }, default));
    }

    [Fact]
    public async Task ElErrorDeUnControladorQueNoExisteHablaDelControlador()
    {
        // El 404 del ABM decía "La acción no existe" para un controlador que falta, y quien carga el
        // catálogo por curl se va a volver loco buscando en el lugar equivocado. En la EJECUCIÓN el
        // mensaje sigue siendo el genérico a propósito: ahí el 404 no puede distinguir.
        using var prueba = new DbDePrueba();

        var ex = await Assert.ThrowsAsync<AccionNoEncontradaException>(
            () => Controller(prueba).BorrarControlador(999, default));

        Assert.Contains("controlador", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnEnclavamientoSeCuelgaDelControlador()
    {
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);

        await Controller(prueba).CrearEnclavamiento(
            controladorId, new EnclavamientoRequest("DB1.DBX1.0", CteFexit.S7Bit, "Portón", [1], 1), default);

        var lista = Valor(await Controller(prueba).ListarEnclavamientos(controladorId, default));
        Assert.Equal("Portón", Assert.Single(lista).Nombre);
    }

    [Fact]
    public async Task UnEnclavamientoSinValoresOkSeRechaza()
    {
        // Se cargaría igual (la columna acepta '[]'), y después el evaluador lo daría SIEMPRE fuera
        // de condición: la escritura quedaría bloqueada para siempre y nadie sabría por qué. Es
        // mejor que no entre.
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearEnclavamiento(
                controladorId, new EnclavamientoRequest("DB1.DBX1.0", CteFexit.S7Bit, "Portón", [], 1), default));
    }

    [Fact]
    public async Task UnEnclavamientoDeUnControladorQueNoExisteSeRechaza()
    {
        using var prueba = new DbDePrueba();

        await Assert.ThrowsAsync<AccionNoEncontradaException>(
            () => Controller(prueba).CrearEnclavamiento(
                999, new EnclavamientoRequest("DB1.DBX1.0", CteFexit.S7Bit, "Portón", [1], 1), default));
    }

    [Fact]
    public async Task UnaEscrituraSinDireccionSeRechaza()
    {
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);
        var sinDireccion = Escritura(controladorId) with { Direccion = null };

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearAccion(sinDireccion, default));
    }

    [Fact]
    public async Task UnaEscrituraEnUnTipoDeSoloLecturaSeRechaza()
    {
        // InputRegister no se puede escribir. Cargarla dejaría una fila que falla recién al
        // ejecutarse, con un mensaje del driver que no dice que el problema es la carga.
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);
        var mala = Escritura(controladorId) with { TipoDireccion = CteFexit.InputRegister };

        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(prueba).CrearAccion(mala, default));
    }

    [Fact]
    public async Task UnaLecturaNoNecesitaDireccionNiValor()
    {
        // Una lectura lee los enclavamientos del controlador: no tiene dirección propia.
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);

        await Controller(prueba).CrearAccion(Lectura(controladorId), default);

        Assert.Single(Valor(await Controller(prueba).ListarAcciones(default)));
    }

    [Fact]
    public async Task DosAccionesConElMismoCodigoSeRechaza()
    {
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);
        await Controller(prueba).CrearAccion(Escritura(controladorId), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearAccion(Escritura(controladorId), default));
    }

    [Fact]
    public async Task DeshabilitarUnaAccionLaSacaDelCatalogoPublicado()
    {
        // El ABM la sigue listando (hay que poder volver a habilitarla); GET /acciones no.
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);
        await Controller(prueba).CrearAccion(Escritura(controladorId), default);
        var id = Valor(await Controller(prueba).ListarAcciones(default)).Single().Id;

        await Controller(prueba).EditarAccion(id, Escritura(controladorId) with { Habilitada = false }, default);

        Assert.Single(Valor(await Controller(prueba).ListarAcciones(default)));
        Assert.Empty(await new AccionRepository(prueba.CrearContext()).ListarAsync(default));
    }

    [Fact]
    public async Task BorrarUnControladorConAccionesSeRechaza()
    {
        // FK Restrict: borrarlo dejaría acciones apuntando a la nada, y Dixit las seguiría ofreciendo
        // hasta que alguien las ejecute. Primero se borran las acciones.
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);
        await Controller(prueba).CrearAccion(Escritura(controladorId), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).BorrarControlador(controladorId, default));
    }

    [Fact]
    public async Task BorrarUnControladorSinAccionesSeLlevaSusEnclavamientos()
    {
        using var prueba = new DbDePrueba();
        var controladorId = CrearControlador(prueba);
        await Controller(prueba).CrearEnclavamiento(
            controladorId, new EnclavamientoRequest("DB1.DBX1.0", CteFexit.S7Bit, "Portón", [1], 1), default);

        await Controller(prueba).BorrarControlador(controladorId, default);

        using var ctx = prueba.CrearContext();
        Assert.Empty(ctx.Controladores);
        Assert.Empty(ctx.Enclavamientos);
    }

    [Fact]
    public async Task ElAbmDeControladoresSiMuestraLaIp()
    {
        // A diferencia de GET /acciones, acá la IP SÍ sale: es el endpoint de configuración y quien
        // llega tiene la clave de la instalación. La promesa de §6 es sobre lo que Fexit le contesta
        // a Dixit en la ejecución, no sobre su propio ABM.
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearControlador(Controlador(), default);

        var controladores = Valor(await Controller(prueba).ListarControladores(default));

        Assert.Equal("10.0.0.20", Assert.Single(controladores).Ip);
    }
}
