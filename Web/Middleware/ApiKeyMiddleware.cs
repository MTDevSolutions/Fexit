using System.Security.Cryptography;
using System.Text;
using Application.Settings;
using Microsoft.Extensions.Options;

namespace Web.Middleware;

/// <summary>
/// La única defensa de Fexit además de la red (§7). No autoriza a NADIE: no hay roles, usuarios ni
/// proyectos de este lado. Sólo distingue "viene de Dixit" de "viene de cualquier otra cosa que
/// llegue a la red de planta".
///
/// La comparación es de tiempo constante. No porque un atacante en la red de planta vaya a montar un
/// ataque de timing sobre HTTP, sino porque la alternativa (==) no es más simple ni más rápida de
/// escribir, y esta no hay que volver a mirarla nunca.
/// </summary>
public class ApiKeyMiddleware(RequestDelegate siguiente, FexitSettings settings)
{
    public ApiKeyMiddleware(RequestDelegate siguiente, IOptions<FexitSettings> settings)
        : this(siguiente, settings.Value) { }

    private static readonly PathString[] RutasLibres = [new("/health"), new("/swagger")];

    public async Task InvokeAsync(HttpContext ctx)
    {
        // StartsWithSegments y NO StartsWith de texto: "/healthcheck-de-alguien".StartsWith("/health")
        // da true, así que cualquier ruta futura cuyo nombre empiece con esas letras se saltearía la
        // clave sin que nadie lo note. StartsWithSegments exige que después venga "/" o el fin de la
        // ruta. Hoy no hay ninguna ruta así; el punto es que agregarla no puede abrir un agujero.
        if (RutasLibres.Any(r => ctx.Request.Path.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase)))
        {
            await siguiente(ctx);
            return;
        }

        // Clave sin configurar = instalación a medio hacer. Rechaza TODO, incluido el pedido que no
        // manda header: comparar contra "" dejaría entrar justamente a quien no manda nada.
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || !EsLaClave(ctx.Request.Headers["X-Api-Key"]))
        {
            // El cuerpo no dice si faltaba el header o si la clave no coincidía: las dos cosas le
            // informan algo a quien está probando desde la red. El detalle, al log.
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "No autorizado." });
            return;
        }

        await siguiente(ctx);
    }

    private bool EsLaClave(string? enviada) =>
        !string.IsNullOrEmpty(enviada)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(enviada), Encoding.UTF8.GetBytes(settings.ApiKey));
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UsarApiKeyDeFexit(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiKeyMiddleware>();
}
