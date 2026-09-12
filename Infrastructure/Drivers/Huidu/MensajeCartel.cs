namespace Infrastructure.Drivers.Huidu;

/// <summary>
/// Mismos valores que CteCarteles.ModoMensajeCartel de axControlBE, de donde se copió el armado del
/// XML. 1 a 5: color de la letra sobre fondo negro; 6 y 7: el panel entero de ese color, sin texto.
/// </summary>
public enum ModoMensajeCartel { Logo = 1, Verde = 2, Blanco = 3, Rojo = 4, Amarillo = 5, FullVerde = 6, FullRojo = 7 }

public record MensajeCartel(string Texto, ModoMensajeCartel Modo);
