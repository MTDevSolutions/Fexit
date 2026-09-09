using Application.Constantes;

namespace Application.Interfaces;

/// <summary>
/// Transporte contra un equipo. Misma forma que IPlcDriver de axControlBE, de donde se copian las
/// implementaciones, con dos diferencias: no lleva `PlcId` (Fexit no necesita saber a qué equipo
/// pertenece: se lo pasa la factory y el ejecutor ya lo tiene) y el tipo se llama TipoDireccionPlc.
/// </summary>
public interface IPlcDriver : IDisposable
{
    bool IsConnected { get; }

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();

    Task<byte[]> ReadAsync(TipoDireccionPlc tipo, string direccion, int longitud,
                           CancellationToken cancellationToken = default);
    Task WriteAsync(TipoDireccionPlc tipo, string direccion, byte[] datos);
}
