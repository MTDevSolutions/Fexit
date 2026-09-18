namespace Domain.Entities;

/// <summary>
/// Una propiedad legible de un equipo: su posición, si el radar detecta a alguien, el nivel de un
/// silo. Un equipo tiene N estados, y "el estado del equipo" es el conjunto de todos: no existe como
/// fila, se arma al responder (spec 2026-09-17 §2.1).
///
/// La misma dirección PUEDE estar cargada en dos equipos (§2.4): no hay índice único que lo impida,
/// y es una decisión explícita. El alta avisa, no bloquea.
/// </summary>
public class Estado
{
    public long Id { get; set; }
    public long EquipoId { get; set; }
    public Equipo? Equipo { get; set; }

    /// <summary>Lo que nombra Dixit ("posicion_barrera_1"). Único en toda la instalación.</summary>
    public string Codigo { get; set; } = default!;

    /// <summary>Lo que ve el usuario como nombre de la fila ("Posición").</summary>
    public string Nombre { get; set; } = default!;

    /// <summary>Para el prompt: qué es esta variable, en lenguaje de planta.</summary>
    public string Descripcion { get; set; } = default!;

    /// <summary>Dirección cruda. NUNCA sale de Fexit, ni en un mensaje de error.</summary>
    public string Direccion { get; set; } = default!;

    /// <summary>Qué área leer: S7Bit, S7Word, HoldingRegister…</summary>
    public string TipoDireccion { get; set; } = default!;

    /// <summary>
    /// Qué significa cada valor. Es el dato que no existe en el fierro y que alguien tiene que
    /// declarar una vez: no hay forma de inferir que en este PLC el radar en 1 es SIN presencia.
    /// Excluyente con Unidad.
    /// </summary>
    public Dictionary<int, string>? Etiquetas { get; set; }

    /// <summary>Para valores numéricos: "%", "°C", "t". Excluyente con Etiquetas.</summary>
    public string? Unidad { get; set; }

    /// <summary>Cuántos decimales tiene el entero que manda el PLC. 235 con 1 decimal son 23,5.</summary>
    public int Decimales { get; set; }

    /// <summary>Orden de presentación dentro del equipo.</summary>
    public int Orden { get; set; }
}
