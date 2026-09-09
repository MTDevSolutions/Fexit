using Application.Settings;
using Microsoft.AspNetCore.Http;
using Web.Middleware;

namespace Application.Tests;

public class ApiKeyMiddlewareTests
{
    private const string Clave = "la-clave-de-la-instalacion";

    private static async Task<(int Status, bool Siguio)> Correr(
        string? claveEnviada, string ruta = "/acciones", string claveConfigurada = Clave)
    {
        var siguio = false;
        var middleware = new ApiKeyMiddleware(
            _ => { siguio = true; return Task.CompletedTask; },
            new FexitSettings { ApiKey = claveConfigurada });

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = ruta;
        if (claveEnviada is not null)
            ctx.Request.Headers["X-Api-Key"] = claveEnviada;

        await middleware.InvokeAsync(ctx);
        return (ctx.Response.StatusCode, siguio);
    }

    [Fact]
    public async Task ConLaClaveCorrecta_Pasa()
    {
        var (_, siguio) = await Correr(Clave);

        Assert.True(siguio);
    }

    [Fact]
    public async Task SinClave_Devuelve401YNoSigue()
    {
        var (status, siguio) = await Correr(null);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(siguio);
    }

    [Fact]
    public async Task ConLaClaveEquivocada_Devuelve401YNoSigue()
    {
        var (status, siguio) = await Correr("otra-cosa");

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(siguio);
    }

    [Fact]
    public async Task HealthNoPideClave()
    {
        // Lo mira el monitoreo de la planta, que no tiene por qué conocer la clave, y no dice nada
        // del catálogo ni de los equipos.
        var (_, siguio) = await Correr(null, ruta: "/health");

        Assert.True(siguio);
    }

    [Fact]
    public async Task SwaggerNoPideClave()
    {
        var (_, siguio) = await Correr(null, ruta: "/swagger/index.html");

        Assert.True(siguio);
    }

    [Theory]
    [InlineData("/healthcheck-de-alguien")]
    [InlineData("/swaggerdocs")]
    [InlineData("/healthz")]
    public async Task UnaRutaQueSoloEmpiezaIgual_NoSeSalteaLaClave(string ruta)
    {
        // Con StartsWith de texto, "/healthcheck-de-alguien" empieza con "/health" y pasaba sin
        // clave. Hoy no existe ninguna ruta así, y ese es justamente el punto: el día que alguien
        // agregue /healthz o /swaggerdocs, no puede quedar abierta sin que nadie lo note. Es la única
        // defensa del servicio además de la red.
        var (status, siguio) = await Correr(null, ruta: ruta);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(siguio);
    }

    [Fact]
    public async Task SinClaveConfigurada_RechazaTodo()
    {
        // El caso del deploy a medio hacer. Con la clave vacía, comparar contra "" dejaría entrar a
        // cualquiera que NO mande el header: es exactamente al revés de lo que hay que hacer. Un
        // Fexit sin clave configurada está mal instalado y no atiende a nadie.
        var (status, siguio) = await Correr(null, claveConfigurada: "");

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(siguio);

        var (status2, siguio2) = await Correr("", claveConfigurada: "");
        Assert.Equal(StatusCodes.Status401Unauthorized, status2);
        Assert.False(siguio2);
    }

    [Fact]
    public async Task ElRechazoNoDiceNadaDeLaClave()
    {
        // Ni "falta el header", ni "la clave no coincide": las dos cosas le dicen algo a quien está
        // probando desde la red de planta. El detalle va al log, no a la respuesta.
        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask, new FexitSettings { ApiKey = Clave });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/acciones";
        var cuerpo = new MemoryStream();
        ctx.Response.Body = cuerpo;

        await middleware.InvokeAsync(ctx);

        var texto = System.Text.Encoding.UTF8.GetString(cuerpo.ToArray());
        Assert.DoesNotContain(Clave, texto);
        Assert.DoesNotContain("X-Api-Key", texto);
    }
}
