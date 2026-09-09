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

    private static EquipoRequest Equipo(string nombre = "bomba3", string protocolo = CteFexit.ProtocoloSiemensS7) =>
        new(nombre, CteFexit.TipoEquipoPlc, "10.0.0.20", 102, protocolo, 0, 1);

    private static AccionRequest Escritura(long equipoId, string codigo = "arrancar_bomba3") =>
        new(codigo, "Arranca la bomba", CteFexit.ModoEscritura, equipoId,
            "DB1.DBX0.0", CteFexit.S7Bit, 1, true, true);

    private static AccionRequest Lectura(long equipoId, string codigo = "estado_bomba3") =>
        new(codigo, "Estado de la bomba", CteFexit.ModoLectura, equipoId, null, null, null, true, true);

    private static long CrearEquipo(DbDePrueba prueba, EquipoRequest? req = null) =>
        Valor<long>(Controller(prueba).CrearEquipo(req ?? Equipo(), default).GetAwaiter().GetResult());

    private static T Valor<T>(ActionResult<T> resultado) =>
        (T)((ObjectResult)resultado.Result!).Value!;

    [Fact]
    public async Task CrearUnEquipoLoDevuelveEnElListado()
    {
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearEquipo(Equipo(), default);

        var equipos = Valor(await Controller(prueba).ListarEquipos(default));

        Assert.Equal("bomba3", Assert.Single(equipos).Nombre);
    }

    [Fact]
    public async Task UnProtocoloDesconocidoSeRechazaConMensajeUtil()
    {
        // El CHECK de la base lo frena igual, pero como DbUpdateException: un 500 ilegible para quien
        // está cargando. Acá es un 400 que dice cuáles son los válidos.
        using var prueba = new DbDePrueba();

        var ex = await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearEquipo(Equipo(protocolo: "Profibus"), default));

        Assert.Contains(CteFexit.ProtocoloModbusTcp, ex.Message);
    }

    [Fact]
    public async Task UnModeloDeCpuDesconocidoSeRechazaConMensajeUtil()
    {
        // El modelo se valida aunque el protocolo lo ignore: si en un Modbus se dejara pasar
        // cualquier cosa, el día que ese equipo cambie a SiemensS7 la fila quedaría con un modelo
        // inválido y el error saldría recién al ejecutar, como PlcException genérica.
        using var prueba = new DbDePrueba();

        var ex = await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearEquipo(Equipo() with { Modelo = "LOGO8" }, default));

        Assert.Contains(CteFexit.ModeloS71500, ex.Message);
    }

    [Fact]
    public async Task DosEquiposConElMismoNombreSeRechaza()
    {
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearEquipo(Equipo(), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearEquipo(Equipo(), default));
    }

    [Fact]
    public async Task UnNombreDeEquipoQueSoloDifiereEnEspaciosSeRechaza()
    {
        // El chequeo de duplicado comparaba el nombre crudo y guardaba el trimeado, así que
        // " bomba3 " pasaba el chequeo contra un "bomba3" existente y después chocaba contra el
        // índice único: DbUpdateException, o sea el 500 ilegible que esta capa existe para evitar.
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearEquipo(Equipo(), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearEquipo(Equipo() with { Nombre = "  bomba3  " }, default));
    }

    [Fact]
    public async Task ElErrorDeUnEquipoQueNoExisteHablaDelEquipo()
    {
        // El 404 del ABM decía "La acción no existe" para un equipo que falta, y quien carga el
        // catálogo por curl se va a volver loco buscando en el lugar equivocado. En la EJECUCIÓN el
        // mensaje sigue siendo el genérico a propósito: ahí el 404 no puede distinguir.
        using var prueba = new DbDePrueba();

        var ex = await Assert.ThrowsAsync<AccionNoEncontradaException>(
            () => Controller(prueba).BorrarEquipo(999, default));

        Assert.Contains("equipo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnEnclavamientoSeCuelgaDelEquipo()
    {
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);

        await Controller(prueba).CrearEnclavamiento(
            equipoId, new EnclavamientoRequest("DB1.DBX1.0", CteFexit.S7Bit, "Portón", [1], 1), default);

        var lista = Valor(await Controller(prueba).ListarEnclavamientos(equipoId, default));
        Assert.Equal("Portón", Assert.Single(lista).Nombre);
    }

    [Fact]
    public async Task UnEnclavamientoSinValoresOkSeRechaza()
    {
        // Se cargaría igual (la columna acepta '[]'), y después el evaluador lo daría SIEMPRE fuera
        // de condición: la escritura quedaría bloqueada para siempre y nadie sabría por qué. Es
        // mejor que no entre.
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearEnclavamiento(
                equipoId, new EnclavamientoRequest("DB1.DBX1.0", CteFexit.S7Bit, "Portón", [], 1), default));
    }

    [Fact]
    public async Task UnEnclavamientoDeUnEquipoQueNoExisteSeRechaza()
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
        var equipoId = CrearEquipo(prueba);
        var sinDireccion = Escritura(equipoId) with { Direccion = null };

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearAccion(sinDireccion, default));
    }

    [Fact]
    public async Task UnaEscrituraEnUnTipoDeSoloLecturaSeRechaza()
    {
        // InputRegister no se puede escribir. Cargarla dejaría una fila que falla recién al
        // ejecutarse, con un mensaje del driver que no dice que el problema es la carga.
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);
        var mala = Escritura(equipoId) with { TipoDireccion = CteFexit.InputRegister };

        await Assert.ThrowsAsync<ConfigInvalidaException>(() => Controller(prueba).CrearAccion(mala, default));
    }

    [Fact]
    public async Task UnaLecturaNoNecesitaDireccionNiValor()
    {
        // Una lectura lee los enclavamientos del equipo: no tiene dirección propia.
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);

        await Controller(prueba).CrearAccion(Lectura(equipoId), default);

        Assert.Single(Valor(await Controller(prueba).ListarAcciones(default)));
    }

    [Fact]
    public async Task DosAccionesConElMismoCodigoSeRechaza()
    {
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);
        await Controller(prueba).CrearAccion(Escritura(equipoId), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).CrearAccion(Escritura(equipoId), default));
    }

    [Fact]
    public async Task DeshabilitarUnaAccionLaSacaDelCatalogoPublicado()
    {
        // El ABM la sigue listando (hay que poder volver a habilitarla); GET /acciones no.
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);
        await Controller(prueba).CrearAccion(Escritura(equipoId), default);
        var id = Valor(await Controller(prueba).ListarAcciones(default)).Single().Id;

        await Controller(prueba).EditarAccion(id, Escritura(equipoId) with { Habilitada = false }, default);

        Assert.Single(Valor(await Controller(prueba).ListarAcciones(default)));
        Assert.Empty(await new AccionRepository(prueba.CrearContext()).ListarAsync(default));
    }

    [Fact]
    public async Task BorrarUnEquipoConAccionesSeRechaza()
    {
        // FK Restrict: borrarlo dejaría acciones apuntando a la nada, y Dixit las seguiría ofreciendo
        // hasta que alguien las ejecute. Primero se borran las acciones.
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);
        await Controller(prueba).CrearAccion(Escritura(equipoId), default);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => Controller(prueba).BorrarEquipo(equipoId, default));
    }

    [Fact]
    public async Task BorrarUnEquipoSinAccionesSeLlevaSusEnclavamientos()
    {
        using var prueba = new DbDePrueba();
        var equipoId = CrearEquipo(prueba);
        await Controller(prueba).CrearEnclavamiento(
            equipoId, new EnclavamientoRequest("DB1.DBX1.0", CteFexit.S7Bit, "Portón", [1], 1), default);

        await Controller(prueba).BorrarEquipo(equipoId, default);

        using var ctx = prueba.CrearContext();
        Assert.Empty(ctx.Equipos);
        Assert.Empty(ctx.Enclavamientos);
    }

    [Fact]
    public async Task ElAbmDeEquiposSiMuestraLaIp()
    {
        // A diferencia de GET /acciones, acá la IP SÍ sale: es el endpoint de configuración y quien
        // llega tiene la clave de la instalación. La promesa de §6 es sobre lo que Fexit le contesta
        // a Dixit en la ejecución, no sobre su propio ABM.
        using var prueba = new DbDePrueba();
        await Controller(prueba).CrearEquipo(Equipo(), default);

        var equipos = Valor(await Controller(prueba).ListarEquipos(default));

        Assert.Equal("10.0.0.20", Assert.Single(equipos).Ip);
    }
}
