using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IoC;

/// <summary>
/// Único punto de wiring del servicio. Arranca vacío a propósito: existe para que Program.cs no
/// tenga que cambiar en cada tarea siguiente, sólo este archivo.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AgregarFexit(this IServiceCollection services, IConfiguration config)
    {
        return services;
    }
}
