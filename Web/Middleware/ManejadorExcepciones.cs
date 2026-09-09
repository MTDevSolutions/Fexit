using Application.Exceptions;

namespace Web.Middleware;

/// <summary>
/// La tabla de §6, en un solo lugar. Va en un middleware y no en el controller para que el próximo
/// endpoint no se olvide de una fila.
///
/// El cuerpo que sale lleva SÓLO el mensaje fijo de la excepción, que por construcción no nombra IP,
/// dirección ni puerto. La causa real (InnerException, stack) se loguea de este lado y no se
/// serializa nunca: ese texto lo terminaría escribiendo la columna Error del comando de Dixit, que
/// un superadmin ve.
/// </summary>
public class ManejadorExcepciones(RequestDelegate siguiente, ILogger<ManejadorExcepciones> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await siguiente(ctx);
        }
        catch (AccionNoEncontradaException ex)
        {
            await ResponderAsync(ctx, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ModoNoCoincideException ex)
        {
            // Error de carga, no de red: alguien tiene que mirar la fila del catálogo de Dixit.
            logger.LogWarning("Pedido con un modo que no coincide con el catálogo. Ruta={Ruta}", ctx.Request.Path);
            await ResponderAsync(ctx, StatusCodes.Status409Conflict, ex.Message);
        }
        catch (EquipoInalcanzableException ex)
        {
            logger.LogWarning(ex, "No se pudo comunicar con el equipo. Ruta={Ruta}", ctx.Request.Path);
            await ResponderAsync(ctx, StatusCodes.Status502BadGateway, ex.Message);
        }
        catch (ConfigInvalidaException ex)
        {
            logger.LogError(ex, "Configuración inválida en el catálogo. Ruta={Ruta}", ctx.Request.Path);
            await ResponderAsync(ctx, StatusCodes.Status500InternalServerError, ex.Message);
        }
        catch (Exception ex)
        {
            // Mensaje genérico a propósito: el de una excepción no prevista puede traer una ruta de
            // archivo, un host o parte de una consulta.
            logger.LogError(ex, "Error no previsto. Ruta={Ruta}", ctx.Request.Path);
            await ResponderAsync(ctx, StatusCodes.Status500InternalServerError, "Error interno.");
        }
    }

    private static async Task ResponderAsync(HttpContext ctx, int status, string mensaje)
    {
        if (ctx.Response.HasStarted)
            return;   // Ya se empezó a escribir la respuesta: cambiar el status acá tiraría.

        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = mensaje });
    }
}

public static class ManejadorExcepcionesExtensions
{
    public static IApplicationBuilder UsarManejadorDeExcepciones(this IApplicationBuilder app) =>
        app.UseMiddleware<ManejadorExcepciones>();
}
