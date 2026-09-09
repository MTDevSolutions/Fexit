using Application.Constantes;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Driver en memoria con lo que hace falta para probar el ejecutor: valores preprogramados por
/// dirección, registro de lo escrito, y la posibilidad de simular que el equipo no responde.
///
/// Distinto de DriverSimulado, que es código de producción para el circuito de §9: éste vive sólo en
/// los tests y permite programar el estado del equipo, que es lo que hace falta para probar que la
/// precondición aborta.
/// </summary>
public sealed class DriverFalso : IPlcDriver
{
    private readonly Dictionary<string, byte[]> _valores = new();

    public bool IsConnected { get; private set; }
    public bool ConectoAlgunaVez { get; private set; }
    public List<(TipoDireccionPlc Tipo, string Direccion, byte[] Datos)> Escrituras { get; } = [];
    public Exception? TiraAlConectar { get; set; }
    public Exception? TiraAlLeer { get; set; }
    public Exception? TiraAlEscribir { get; set; }

    /// <summary>Programa lo que va a devolver una dirección. El ancho lo decide el tipo.</summary>
    public DriverFalso Con(string direccion, params byte[] datos)
    {
        _valores[direccion] = datos;
        return this;
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (TiraAlConectar is not null) throw TiraAlConectar;
        IsConnected = true;
        ConectoAlgunaVez = true;
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(TipoDireccionPlc tipo, string direccion, int longitud,
                                  CancellationToken cancellationToken = default)
    {
        if (TiraAlLeer is not null) throw TiraAlLeer;
        return Task.FromResult(_valores.TryGetValue(direccion, out var d) ? d : new byte[Math.Max(1, longitud)]);
    }

    public Task WriteAsync(TipoDireccionPlc tipo, string direccion, byte[] datos)
    {
        if (TiraAlEscribir is not null) throw TiraAlEscribir;
        Escrituras.Add((tipo, direccion, datos));
        return Task.CompletedTask;
    }

    public void Dispose() => IsConnected = false;
}

/// <summary>Devuelve siempre el mismo driver, para poder inspeccionarlo después de ejecutar.</summary>
public sealed class FactoriaFalsa(DriverFalso driver) : IPlcDriverFactory
{
    public IPlcDriver Crear(Equipo equipo) => driver;
}
