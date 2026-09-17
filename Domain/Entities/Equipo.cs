namespace Domain.Entities;

/// <summary>
/// La cosa que la gente nombra: "Barrera 1", "Radar ingreso", "Cartel de ingreso". Es equipo lo que
/// se nombra al preguntar o al ordenar — no lo que tiene acciones: el radar no tiene ninguna y es
/// equipo igual (spec 2026-09-17 §2.3).
///
/// Cuelga de UN controlador: sus estados, enclavamientos y acciones se leen y se escriben por ahí.
/// Que el PLC exponga la posición de la barrera y la señal del radar en el mismo bloque es un
/// detalle de cableado; una dirección no dice de quién es la señal.
/// </summary>
public class Equipo
{
    public long Id { get; set; }

    /// <summary>Lo que ve el usuario y por lo que pregunta. Único en toda la instalación.</summary>
    public string Nombre { get; set; } = default!;

    /// <summary>Para el prompt: qué es este equipo, en lenguaje de planta.</summary>
    public string Descripcion { get; set; } = default!;

    public long SectorId { get; set; }
    public Sector? Sector { get; set; }

    /// <summary>Por dónde se llega. Un controlador tiene N equipos; un equipo, un solo controlador.</summary>
    public long ControladorId { get; set; }
    public Controlador? Controlador { get; set; }

}
