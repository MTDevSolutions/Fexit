using IoC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Tests;

public class AndamiajeTests
{
    [Fact]
    public void ElWiringSeResuelveSinTirar()
    {
        // No comprueba comportamiento: comprueba que las seis referencias están bien puestas y que
        // AgregarFexit se puede llamar. Es el test que va a fallar primero, y con el mensaje más
        // claro, si una tarea posterior registra un servicio con una dependencia que no existe.
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var services = new ServiceCollection().AgregarFexit(config);

        Assert.NotNull(services.BuildServiceProvider());
    }
}
