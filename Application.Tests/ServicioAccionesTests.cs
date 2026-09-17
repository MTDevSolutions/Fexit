using System.Text.Json;
using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;

namespace Application.Tests;

public class ServicioAccionesTests
{
    private sealed class RepoFalso(AccionAEjecutar? resultado) : IAccionRepository
    {
        public string? CodigoBuscado { get; private set; }

        public Task<IReadOnlyList<AccionRemotaDto>> ListarAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AccionRemotaDto>>([]);

        public Task<AccionAEjecutar?> BuscarPorCodigoAsync(string codigo, CancellationToken ct)
        {
            CodigoBuscado = codigo;
            return Task.FromResult(resultado);
        }
    }

    private sealed class EjecutorFalso(string tipoEquipo, ResultadoAccion? resultado = null) : IEjecutorAccion
    {
        public bool Ejecuto { get; private set; }
        public AccionAEjecutar? Recibida { get; private set; }
        public string TipoEquipo => tipoEquipo;

        public Task<ResultadoAccion> EjecutarAsync(AccionAEjecutar accion, CancellationToken ct)
        {
            Ejecuto = true;
            Recibida = accion;
            return Task.FromResult(resultado ?? new ResultadoAccion(true, "Escritura realizada.", [], []));
        }
    }

    private static Controlador Controlador(string tipoEquipo = CteFexit.TipoEquipoPlc) => new()
    {
        Id = 1, Nombre = "bomba3", TipoEquipo = tipoEquipo, Ip = "10.0.0.20", Puerto = 102,
        Protocolo = CteFexit.ProtocoloSiemensS7, Rack = 0, Slot = 1,
    };

    private static AccionAEjecutar Accion(
        string modo, string tipoEquipo = CteFexit.TipoEquipoPlc, string? definicion = null) =>
        new(new Accion
        {
            Codigo = "abrir_barrera", Descripcion = "d", Modo = modo, ControladorId = 1,
            Direccion = "DB1.DBX0.0", TipoDireccion = CteFexit.S7Bit, Valor = 1,
            UsaEnclavamientos = false, Habilitada = true, DefinicionParametrosJson = definicion,
        }, Controlador(tipoEquipo), []);

    [Fact]
    public async Task ConElModoCorrecto_Ejecuta()
    {
        var ejecutor = new EjecutorFalso(CteFexit.TipoEquipoPlc);
        var servicio = new ServicioAcciones(new RepoFalso(Accion(CteFexit.ModoEscritura)), [ejecutor]);

        var resultado = await servicio.EjecutarAsync("abrir_barrera", CteFexit.ModoEscritura, default);

        Assert.True(ejecutor.Ejecuto);
        Assert.True(resultado.Exito);
    }

    [Fact]
    public async Task UnCodigoQueNoExiste_TiraAccionNoEncontrada()
    {
        var servicio = new ServicioAcciones(new RepoFalso(null), [new EjecutorFalso(CteFexit.TipoEquipoPlc)]);

        await Assert.ThrowsAsync<AccionNoEncontradaException>(
            () => servicio.EjecutarAsync("no_existe", CteFexit.ModoEscritura, default));
    }

    [Fact]
    public async Task ElModoQueNoCoincide_TiraModoNoCoincideYNoEjecuta()
    {
        // LA defensa de borde de §5. Dixit cree que es lectura, en Fexit es escritura: si esto no
        // frenara, una escritura real saldría del chat normal sin pasar por /action, y ningún guard
        // de Dixit podría verlo porque Dixit ya no sabe qué hace cada código.
        var ejecutor = new EjecutorFalso(CteFexit.TipoEquipoPlc);
        var servicio = new ServicioAcciones(new RepoFalso(Accion(CteFexit.ModoEscritura)), [ejecutor]);

        await Assert.ThrowsAsync<ModoNoCoincideException>(
            () => servicio.EjecutarAsync("abrir_barrera", CteFexit.ModoLectura, default));

        Assert.False(ejecutor.Ejecuto);
    }

    [Fact]
    public async Task ElCrucePorElOtroLadoTambienFrena()
    {
        // El cruce simétrico: Dixit cree escritura, Fexit dice lectura. Menos peligroso, pero es un
        // error de carga que hay que ver, no algo que se ejecute igual.
        var ejecutor = new EjecutorFalso(CteFexit.TipoEquipoPlc);
        var servicio = new ServicioAcciones(new RepoFalso(Accion(CteFexit.ModoLectura)), [ejecutor]);

        await Assert.ThrowsAsync<ModoNoCoincideException>(
            () => servicio.EjecutarAsync("abrir_barrera", CteFexit.ModoEscritura, default));

        Assert.False(ejecutor.Ejecuto);
    }

    [Fact]
    public async Task UnModoEsperadoQueNoEsNinguno_TiraModoNoCoincide()
    {
        var servicio = new ServicioAcciones(
            new RepoFalso(Accion(CteFexit.ModoEscritura)), [new EjecutorFalso(CteFexit.TipoEquipoPlc)]);

        await Assert.ThrowsAsync<ModoNoCoincideException>(
            () => servicio.EjecutarAsync("abrir_barrera", "cualquier_cosa", default));
    }

    [Fact]
    public async Task ElEjecutorSeEligePorTipoDeEquipo()
    {
        // §10.9: la factory switchea por tipo de equipo. El día que entre un EjecutorMqtt, este test
        // es el que garantiza que no se elija el de PLC.
        var plc = new EjecutorFalso(CteFexit.TipoEquipoPlc);
        var otro = new EjecutorFalso("mqtt");
        var servicio = new ServicioAcciones(new RepoFalso(Accion(CteFexit.ModoEscritura, "mqtt")), [plc, otro]);

        await servicio.EjecutarAsync("abrir_barrera", CteFexit.ModoEscritura, default);

        Assert.False(plc.Ejecuto);
        Assert.True(otro.Ejecuto);
    }

    [Fact]
    public async Task UnTipoDeEquipoSinEjecutor_TiraConfigInvalida()
    {
        var servicio = new ServicioAcciones(
            new RepoFalso(Accion(CteFexit.ModoEscritura, "mqtt")), [new EjecutorFalso(CteFexit.TipoEquipoPlc)]);

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => servicio.EjecutarAsync("abrir_barrera", CteFexit.ModoEscritura, default));
    }

    [Fact]
    public async Task ElCodigoSeBuscaSinEspaciosAlrededor()
    {
        // Barato y evita un 404 incomprensible por un espacio pegado en la carga del catálogo de
        // Dixit. No cambia el casing: los códigos son identificadores, no texto libre.
        var repo = new RepoFalso(Accion(CteFexit.ModoEscritura));
        var servicio = new ServicioAcciones(repo, [new EjecutorFalso(CteFexit.TipoEquipoPlc)]);

        await servicio.EjecutarAsync("  abrir_barrera  ", CteFexit.ModoEscritura, default);

        Assert.Equal("abrir_barrera", repo.CodigoBuscado);
    }

    [Fact]
    public async Task UnaPrecondicionQueNoDaNoEsUnaExcepcion()
    {
        // Es un 200 con exito:false (§6). La acción se ejecutó y decidió no escribir: eso no es un
        // error del servicio, es su respuesta, y el usuario tiene que leer el detalle.
        var abortado = new ResultadoAccion(false, "Precondición no cumplida: Portón.", [], []);
        var servicio = new ServicioAcciones(
            new RepoFalso(Accion(CteFexit.ModoEscritura)),
            [new EjecutorFalso(CteFexit.TipoEquipoPlc, abortado)]);

        var resultado = await servicio.EjecutarAsync("abrir_barrera", CteFexit.ModoEscritura, default);

        Assert.False(resultado.Exito);
        Assert.Equal("Precondición no cumplida: Portón.", resultado.Detalle);
    }

    [Fact]
    public async Task LosValoresValidadosLleganAlEjecutor()
    {
        var ejecutor = new EjecutorFalso(CteFexit.TipoEquipoPlc);
        var def = """[{"nombre":"minutos","tipo":"entero","etiqueta":"minutos","requerido":true,"minimo":1,"maximo":60}]""";
        var servicio = new ServicioAcciones(new RepoFalso(Accion(CteFexit.ModoEscritura, definicion: def)), [ejecutor]);

        await servicio.EjecutarAsync("abrir_barrera", CteFexit.ModoEscritura,
            JsonDocument.Parse("""{"minutos":5}""").RootElement, default);

        Assert.Equal(5, ejecutor.Recibida!.Valores!.Entero);
    }

    [Fact]
    public async Task ParametrosInvalidos_NoLleganAlEjecutor()
    {
        var ejecutor = new EjecutorFalso(CteFexit.TipoEquipoPlc);
        var servicio = new ServicioAcciones(new RepoFalso(Accion(CteFexit.ModoEscritura)), [ejecutor]);

        await Assert.ThrowsAsync<ParametrosInvalidosException>(() => servicio.EjecutarAsync("abrir_barrera",
            CteFexit.ModoEscritura, JsonDocument.Parse("""{"minutos":5}""").RootElement, default));

        Assert.False(ejecutor.Ejecuto);
    }
}
