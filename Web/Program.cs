using IoC;
using Microsoft.OpenApi.Models;
using Serilog;
using Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Sólo para que aparezca el candado "Authorize" en la UI: Swashbuckle no infiere el header
    // de ApiKeyMiddleware solo, y sin esto no hay forma de setear X-Api-Key desde Swagger. No
    // reemplaza ni valida nada — la única verificación real sigue siendo el middleware.
    const string esquema = "ApiKey";
    c.AddSecurityDefinition(esquema, new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "La ApiKey de FexitSettings, la misma que valida ApiKeyMiddleware."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = esquema } },
            []
        }
    });
});
builder.Services.AgregarFexit(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UsarManejadorDeExcepciones();
app.UsarApiKeyDeFexit();

// /health queda FUERA de la api key: es lo que mira el monitoreo de la planta, que no tiene por qué
// conocer la clave. No dice nada del catálogo ni de los equipos, así que no filtra nada.
app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

app.MapControllers();

app.Run();

/// <summary>Para que WebApplicationFactory pueda tomar el host desde los tests.</summary>
public partial class Program;
