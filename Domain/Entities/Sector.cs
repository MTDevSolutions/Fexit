namespace Domain.Entities;

/// <summary>
/// Una zona de la planta ("Portería", "Playa de camiones"). Existe para agrupar equipos en el
/// prompt y para contestar "¿cómo está la portería?". No sabe nada de redes ni de direcciones.
/// </summary>
public class Sector
{
    public long Id { get; set; }
    public string Nombre { get; set; } = default!;

    /// <summary>Orden de presentación. Los sectores se listan en un prompt que lee una persona.</summary>
    public int Orden { get; set; }

    public List<Equipo> Equipos { get; set; } = [];
}
