using Application.Interfaces;
using Application.Settings;
using Infrastructure.Data;
using Infrastructure.Data.Repositorios;
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

        return services;
    }
}
