using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Ejecutores;

namespace Application.Tests;

public class EjecutorCartelTests
{
    private sealed class TransporteFalso(Exception? tira = null) : ITransporteCartel
    {
        public List<string> Enviados { get; } = [];

        public Task EnviarAsync(Controlador controlador, string xml, CancellationToken ct)
        {
            if (tira is not null) throw tira;
            Enviados.Add(xml);
            return Task.CompletedTask;
        }
    }

    private static Controlador Cartel() => new()
    {
        Id = 2, Nombre = "cartel_ingreso", TipoEquipo = CteFexit.TipoEquipoCartel, Ip = "10.0.0.30",
        Puerto = 10001, Protocolo = CteFexit.ProtocoloHuiduSdk,
    };

    private static Equipo Equipo() => new()
    {
        Id = 2, Nombre = "Cartel de ingreso", Descripcion = "Cartel LED de portería",
        SectorId = 1, ControladorId = 2,
    };

    private static Accion Escritura(string? config = null) => new()
    {
        Codigo = "cartel_ingreso_mensaje", Descripcion = "d", Modo = CteFexit.ModoEscritura, EquipoId = 2,
        Habilitada = true, ConfigJson = config,
    };

    [Fact]
    public void AtiendeElTipoCartel() =>
        Assert.Equal(CteFexit.TipoEquipoCartel, new EjecutorCartel(new TransporteFalso()).TipoEquipo);

    [Fact]
    public async Task TextoLibre_MandaElTextoYElColor()
    {
        var transporte = new TransporteFalso();
        var accion = new AccionAEjecutar(Escritura(), Equipo(), Cartel(), [], new ValoresParametros("Hola bienvenido", "amarillo", null));

        var r = await new EjecutorCartel(transporte).EjecutarAsync(accion, default);

        Assert.True(r.Exito);
        Assert.Equal("Se mostró «Hola bienvenido» en amarillo.", r.Detalle);
        Assert.Contains("Hola bienvenido", Assert.Single(transporte.Enviados));
        Assert.Contains("#ffff80", transporte.Enviados[0]);
    }

    [Theory]
    [InlineData("""{"modo":"logo"}""", "Se mostró el logo.")]
    [InlineData("""{"modo":"pantalla_verde"}""", "Se pintó el cartel de verde.")]
    [InlineData("""{"modo":"pantalla_roja"}""", "Se pintó el cartel de rojo.")]
    [InlineData("""{"modo":"texto","texto":"ESPERE","color":"amarillo"}""", "Se mostró «ESPERE» en amarillo.")]
    public async Task Fija_MandaLoQueDiceSuConfig(string config, string detalle)
    {
        var transporte = new TransporteFalso();

        var r = await new EjecutorCartel(transporte).EjecutarAsync(
            new AccionAEjecutar(Escritura(config), Equipo(), Cartel(), []), default);

        Assert.Equal(detalle, r.Detalle);
        Assert.Single(transporte.Enviados);
    }

    [Fact]
    public async Task SinTextoNiConfig_EsConfigInvalidaYNoManda()
    {
        var transporte = new TransporteFalso();

        await Assert.ThrowsAsync<ConfigInvalidaException>(() => new EjecutorCartel(transporte)
            .EjecutarAsync(new AccionAEjecutar(Escritura(), Equipo(), Cartel(), []), default));

        Assert.Empty(transporte.Enviados);
    }

    [Fact]
    public async Task SiElCartelNoConfirma_EsEquipoInalcanzableSinFiltrarLaIp()
    {
        var transporte = new TransporteFalso(new TimeoutException("10.0.0.30 no contestó"));
        var accion = new AccionAEjecutar(Escritura(), Equipo(), Cartel(), [], new ValoresParametros("Hola", "verde", null));

        var ex = await Assert.ThrowsAsync<EquipoInalcanzableException>(
            () => new EjecutorCartel(transporte).EjecutarAsync(accion, default));

        Assert.DoesNotContain("10.0.0.30", ex.Message);
    }

    [Fact]
    public async Task UnaLecturaSobreUnCartel_EsConfigInvalida()
    {
        var accion = Escritura();
        accion.Modo = CteFexit.ModoLectura;

        await Assert.ThrowsAsync<ConfigInvalidaException>(() => new EjecutorCartel(new TransporteFalso())
            .EjecutarAsync(new AccionAEjecutar(accion, Equipo(), Cartel(), []), default));
    }
}
