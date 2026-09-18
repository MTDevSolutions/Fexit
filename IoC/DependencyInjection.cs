using Application.Interfaces;
using Application.Services;
using Application.Settings;
using Infrastructure.Data;
using Infrastructure.Data.Repositorios;
using Infrastructure.Drivers;
using Infrastructure.Drivers.Huidu;
using Infrastructure.Ejecutores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IoC;

/// <summary>
/// Único punto de wiring del servicio.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AgregarFexit(this IServiceCollection services, IConfiguration config)
    {
        var settings = config.GetSection("FexitSettings").Get<FexitSettings>() ?? new FexitSettings();
        services.Configure<FexitSettings>(config.GetSection("FexitSettings"));

        // Sin connection string no hay nada que inicializar: en los tests de wiring la sección no
        // existe y el ServiceProvider tiene que poder construirse igual.
        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            services.AddDbContext<FexitDbContext>(o => o.UseSqlite(settings.ConnectionString));

            // Fail fast en el wiring, igual que DixitBE: si la migración no corre, la app no levanta.
            new FexitDbInitializer(settings.ConnectionString).Inicializar();
        }

        services.AddScoped<IAccionRepository, AccionRepository>();
        services.AddScoped<ICatalogoRepository, CatalogoRepository>();
        services.AddScoped<IEstadoRepository, EstadoRepository>();

        // La factory de drivers no toca la base: fuera del if, para que no dependa de que exista
        // connection string. Singleton y no Scoped: no tiene estado propio, sólo lee el timeout de
        // los settings. Los drivers que crea sí tienen estado y los dispone quien los pide.
        services.AddSingleton<IPlcDriverFactory, PlcDriverFactory>();

        // Se registra como IEjecutorAccion y no como EjecutorPlc: el servicio de la tarea 7 resuelve
        // IEnumerable<IEjecutorAccion> y elige por TipoEquipo. Sumar un EjecutorMqtt es una línea
        // igual a ésta.
        services.AddScoped<IEjecutorAccion, EjecutorPlc>();

        // Cartel: sin estado propio entre pedidos (conecta, manda, espera confirmación y cierra en
        // cada llamada), así que el transporte puede ser singleton igual que la factory de PLC.
        services.AddScoped<IEjecutorAccion, EjecutorCartel>();
        services.AddSingleton<ITransporteCartel, TransporteCartelHuidu>();

        services.AddScoped<IServicioAcciones, ServicioAcciones>();

        return services;
    }
}
