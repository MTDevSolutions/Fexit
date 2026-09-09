using System.Collections.Concurrent;
using Application.Constantes;
using Application.Interfaces;

namespace Infrastructure.Drivers.Simulado;

/// <summary>
/// El simulador de equipo que pide §9 del spec, para cerrar el circuito Dixit → Fexit → respuesta
/// sin PLC real. Es un protocolo más y no un modo de arranque especial: se carga un Equipo con
/// Protocolo="Simulado" y el resto del sistema no se entera. Nada de #if ni de flags.
///
/// Guarda lo escrito en memoria por (tipo, dirección) y lo devuelve al leer, para que un test pueda
/// comprobar que la escritura LLEGÓ. Una dirección que nadie escribió lee 0, como un equipo recién
/// arrancado — así una lectura de enclavamientos contra el simulador da "fuera de condición" para
/// valoresOk=[1], que es el caso interesante.
///
/// Respeta el mismo contrato que los drivers reales, incluido tirar InvalidOperationException al
/// leer sin conectar. Si fuera más permisivo, el ejecutor pasaría los tests contra el simulado y
/// fallaría contra un PLC, que es peor que no tener simulador.
///
/// El estado es estático (por IP) a propósito: la factory crea un driver nuevo por ejecución, y sin
/// esto una escritura y la lectura siguiente no se verían entre sí.
/// </summary>
public sealed class DriverSimulado(string ip, int puerto) : IPlcDriver
{
    /// <summary>IP reservada para probar el camino de "equipo inalcanzable" (502 en §6) sin desenchufar nada.</summary>
    public const string IpQueFalla = "10.255.255.255";

    private static readonly ConcurrentDictionary<string, byte[]> Memoria = new();

    private bool _conectado;

    public bool IsConnected => _conectado;

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (ip == IpQueFalla)
            throw new IOException("No se pudo establecer la conexión con el equipo.");

        _conectado = true;
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _conectado = false;
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(TipoDireccionPlc tipo, string direccion, int longitud,
                                  CancellationToken cancellationToken = default)
    {
        if (!_conectado)
            throw new InvalidOperationException("No hay conexión establecida con el equipo.");

        var ancho = Math.Max(1, longitud);
        return Task.FromResult(
            Memoria.TryGetValue(Clave(tipo, direccion), out var datos) && datos.Length == ancho
                ? datos
                : new byte[ancho]);
    }

    public Task WriteAsync(TipoDireccionPlc tipo, string direccion, byte[] datos)
    {
        if (!_conectado)
            throw new InvalidOperationException("No hay conexión establecida con el equipo.");

        Memoria[Clave(tipo, direccion)] = datos;
        return Task.CompletedTask;
    }

    public void Dispose() => _conectado = false;

    private string Clave(TipoDireccionPlc tipo, string direccion) => $"{ip}:{puerto}:{tipo}:{direccion}";
}
