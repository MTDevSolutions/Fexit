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
    private readonly bool _falla;
    private readonly int _valorPorDefecto;

    public bool IsConnected { get; private set; }
    public bool ConectoAlgunaVez { get; private set; }
    public List<(TipoDireccionPlc Tipo, string Direccion, byte[] Datos)> Escrituras { get; } = [];
    public Exception? TiraAlConectar { get; set; }
    public Exception? TiraAlLeer { get; set; }
    public Exception? TiraAlEscribir { get; set; }

    public DriverFalso() { }

    /// <summary>
    /// Para los tests de LectorEstados, que arman equipos por código y no por dirección: con
    /// <paramref name="falla"/> simula un controlador caído (el connect tira, como haría un socket
    /// contra una IP que no responde) y con <paramref name="valor"/> programa el mismo crudo para
    /// cualquier dirección que no se haya cargado explícitamente con <see cref="Con"/>.
    /// </summary>
    public DriverFalso(bool falla, int valor)
    {
        _falla = falla;
        _valorPorDefecto = valor;
    }

    /// <summary>Programa lo que va a devolver una dirección. El ancho lo decide el tipo.</summary>
    public DriverFalso Con(string direccion, params byte[] datos)
    {
        _valores[direccion] = datos;
        return this;
    }

    /// <summary>
    /// Simula el equipo que no rechaza la conexión pero tampoco la acepta: el connect no vuelve nunca
    /// por su cuenta. Es lo único que reproduce, sin red, el caso que colgaba 21 s (un PLC apagado o
    /// detrás de un firewall que descarta el SYN); quien tiene que cortarlo es el deadline del
    /// ejecutor, no el driver.
    /// </summary>
    public bool CuelgaAlConectar { get; set; }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (TiraAlConectar is not null) throw TiraAlConectar;
        // El mensaje no importa: LectorEstados no lo mira, sólo el hecho de que falló. Lo único que
        // le interesa a EquipoInalcanzableException del lado real es que el mensaje del socket no
        // suba tal cual, y acá no hay socket real que lo traiga.
        if (_falla) throw new InvalidOperationException("Controlador caído (simulado).");
        if (CuelgaAlConectar)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        IsConnected = true;
        ConectoAlgunaVez = true;
        return true;
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
        if (_valores.TryGetValue(direccion, out var d)) return Task.FromResult(d);
        return Task.FromResult(IntABytes(_valorPorDefecto, Math.Max(1, longitud)));
    }

    private static byte[] IntABytes(int valor, int longitud)
    {
        var datos = new byte[longitud];
        for (var i = longitud - 1; i >= 0; i--)
        {
            datos[i] = (byte)(valor & 0xFF);
            valor >>= 8;
        }
        return datos;
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
    public IPlcDriver Crear(Controlador controlador) => driver;
}
