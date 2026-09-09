using IoC;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AgregarFexit(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// /health queda FUERA de la api key: es lo que mira el monitoreo de la planta, que no tiene por qué
// conocer la clave. No dice nada del catálogo ni de los equipos, así que no filtra nada.
app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

app.MapControllers();

app.Run();
