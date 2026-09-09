namespace Application.Settings;

/// <summary>
/// Toda la config del servicio. Una sola ApiKey por instalación: Fexit no autoriza a nadie (§7), la
/// clave sólo distingue "viene de Dixit" de "viene de cualquier otra cosa en la red".
/// </summary>
public class FexitSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Timeout de conexión y de lectura/escritura contra el equipo, en milisegundos.</summary>
    public int TimeoutEquipoMs { get; set; } = 3000;
}
