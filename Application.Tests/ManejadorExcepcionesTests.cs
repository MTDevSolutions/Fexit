using Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Middleware;

namespace Application.Tests;

public class ManejadorExcepcionesTests
{
    private static async Task<(int Status, string Cuerpo)> Correr(Exception? aTirar)
    {
        var middleware = new ManejadorExcepciones(
            _ => aTirar is null ? Task.CompletedTask : Task.FromException(aTirar),
            NullLogger<ManejadorExcepciones>.Instance);

        var ctx = new DefaultHttpContext();
        var cuerpo = new MemoryStream();
        ctx.Response.Body = cuerpo;

        await middleware.InvokeAsync(ctx);

        return (ctx.Response.StatusCode, System.Text.Encoding.UTF8.GetString(cuerpo.ToArray()));
    }

    [Fact]
    public async Task SinExcepcion_NoTocaLaRespuesta()
    {
        var (status, _) = await Correr(null);

        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task AccionNoEncontrada_Da404()
    {
        // §6: código inexistente o deshabilitado. Dixit lo cierra en fallido definitivo, sin reintento.
        var (status, _) = await Correr(new AccionNoEncontradaException());

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task ModoNoCoincide_Da409()
    {
        // §6: es un error de CARGA, no de red. Dixit lo cierra en fallido definitivo y alguien tiene
        // que mirar la fila del catálogo.
        var (status, _) = await Correr(new ModoNoCoincideException());

        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public async Task EquipoInalcanzable_Da502()
    {
        var (status, _) = await Correr(new EquipoInalcanzableException());

        Assert.Equal(StatusCodes.Status502BadGateway, status);
    }

    [Fact]
    public async Task ParametrosInvalidos_Da400ConElMensaje()
    {
        var (status, cuerpo) = await Correr(new ParametrosInvalidosException("Falta el parámetro 'minutos'."));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("minutos", cuerpo);
    }

    [Fact]
    public async Task ConfigInvalida_Da500()
    {
        var (status, _) = await Correr(new ConfigInvalidaException("La acción de escritura está incompleta."));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
    }

    [Fact]
    public async Task ConfigInvalidaEnElAbm_Da400()
    {
        // La misma excepción vale 400 en /catalogo y 500 en la ejecución: allá el que se equivocó es
        // quien manda el pedido, acá es una fila guardada que está rota.
        var middleware = new ManejadorExcepciones(
            _ => Task.FromException(new ConfigInvalidaException("Protocolo desconocido.")),
            NullLogger<ManejadorExcepciones>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/catalogo/controladores";
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task UnaExcepcionNoPrevista_Da500YNoFiltraNada()
    {
        // Lo más importante de este test: el mensaje de una excepción cualquiera puede traer una ruta
        // de archivo, un host o un stack. Nada de eso puede salir por la API — va al log.
        var (status, cuerpo) = await Correr(new InvalidOperationException("C:\\App_Data\\fexit.db corrupto en 10.0.0.20"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("10.0.0.20", cuerpo);
        Assert.DoesNotContain("App_Data", cuerpo);
    }

    [Fact]
    public async Task ElCuerpoDelErrorNoTraeLaCausaTecnica()
    {
        // EquipoInalcanzableException lleva la causa real en InnerException justamente para que se
        // loguee y no se serialice. Si esto fallara, host y puerto terminarían en la columna Error
        // del comando de Dixit, que un superadmin ve.
        var (_, cuerpo) = await Correr(
            new EquipoInalcanzableException(new IOException("No route to host 10.0.0.20:102")));

        Assert.DoesNotContain("10.0.0.20", cuerpo);
        Assert.DoesNotContain("No route", cuerpo);
    }
}
