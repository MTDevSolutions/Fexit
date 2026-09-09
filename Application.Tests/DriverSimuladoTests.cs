using Application.Constantes;
using Infrastructure.Drivers.Simulado;

namespace Application.Tests;

public class DriverSimuladoTests
{
    [Fact]
    public async Task LoQueSeEscribeEsLoQueSeLee()
    {
        // El simulador tiene que comportarse como un equipo: si se escribe un 1 y después se lee la
        // misma dirección, sale 1. Sin esto no sirve para cerrar el circuito de §9 — un simulador
        // que siempre devuelve lo mismo no prueba que la escritura llegó.
        using var driver = new DriverSimulado("10.0.0.99", 502);
        await driver.ConnectAsync();

        await driver.WriteAsync(TipoDireccionPlc.S7Bit, "DB1.DBX0.0", [1]);
        var leido = await driver.ReadAsync(TipoDireccionPlc.S7Bit, "DB1.DBX0.0", 1, default);

        Assert.Equal([1], leido);
    }

    [Fact]
    public async Task UnaDireccionQueNadieEscribioLeeCero()
    {
        // Un equipo recién arrancado tiene todo en 0. Así una lectura de enclavamientos contra el
        // simulador da "fuera de condición" para valoresOk=[1], que es el caso interesante de probar.
        using var driver = new DriverSimulado("10.0.0.99", 502);
        await driver.ConnectAsync();

        var leido = await driver.ReadAsync(TipoDireccionPlc.HoldingRegister, "40001", 2, default);

        Assert.Equal([0, 0], leido);
    }

    [Fact]
    public async Task SinConectar_Tira()
    {
        // Mismo contrato que los drivers reales: leer sin conexión es InvalidOperationException, no
        // un cero silencioso. Si el simulado fuera más permisivo, el ejecutor pasaría los tests
        // contra el simulado y fallaría contra un PLC.
        using var driver = new DriverSimulado("10.0.0.99", 502);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.ReadAsync(TipoDireccionPlc.S7Bit, "DB1.DBX0.0", 1, default));
    }

    [Fact]
    public async Task LaIpInalcanzableSimulaUnEquipoCaido()
    {
        // Una IP reservada para eso, para poder probar el camino de "equipo inalcanzable" (502 en
        // §6) sin desenchufar nada. Es lo que hace falta para el test de la tarea 7.
        using var driver = new DriverSimulado(DriverSimulado.IpQueFalla, 502);

        await Assert.ThrowsAnyAsync<Exception>(() => driver.ConnectAsync());
    }
}
