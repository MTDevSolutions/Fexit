using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Elige el driver por PROTOCOLO. Es la factory interna del ejecutor de PLC: la factory de arriba
/// switchea por tipo de equipo (§10.9), y el protocolo no se le escapa de este nivel.
/// </summary>
public interface IPlcDriverFactory
{
    IPlcDriver Crear(Equipo equipo);
}
