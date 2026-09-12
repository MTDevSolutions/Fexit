namespace Domain.Entities;

/// <summary>
/// Lo que Fexit sabe hacer, y la única cosa que Dixit nombra. El Codigo ES la acción: los
/// parámetros que declara pueden cambiar con qué valor se hace, nunca qué se hace (spec
/// 2026-09-12 §9.1). Cuatro barreras son cuatro filas, no cuatro ejecutores (§10.2).
/// </summary>
public class Accion
{
    public long Id { get; set; }

    /// <summary>Lo que manda Dixit ("abrir_barrera_ingreso"). Único en toda la instalación.</summary>
    public string Codigo { get; set; } = default!;

    /// <summary>Lo que Dixit muestra al superadmin al elegir el código en el alta.</summary>
    public string Descripcion { get; set; } = default!;

    /// <summary>lectura | escritura. Lo que Fexit compara contra el modoEsperado del pedido (§5).</summary>
    public string Modo { get; set; } = default!;

    public long EquipoId { get; set; }
    public Equipo? Equipo { get; set; }

    /// <summary>Sólo escritura: dónde escribir. Null en las de lectura, que leen los enclavamientos.</summary>
    public string? Direccion { get; set; }

    /// <summary>Sólo escritura: qué área/registro. Null en las de lectura.</summary>
    public string? TipoDireccion { get; set; }

    /// <summary>Sólo escritura: qué valor escribir. Null en las de lectura.</summary>
    public int? Valor { get; set; }

    /// <summary>
    /// En una escritura: si evalúa los enclavamientos del equipo como precondición antes de escribir.
    /// El flag gobierna sólo las ESCRITURAS. Una lectura devuelve la tabla de enclavamientos del
    /// equipo siempre, tenga el flag como esté: una acción de lectura existe justamente para
    /// informarlos, y si el flag la vaciara la fila no tendría ningún sentido. Es false, por ejemplo, en una
    /// escritura sin condiciones (prender un cartel) o en un equipo que todavía no tiene la lista
    /// cargada.
    /// </summary>
    public bool UsaEnclavamientos { get; set; }

    /// <summary>
    /// Qué parámetros acepta (JSON, lista de DefinicionParametro). Se PUBLICA en GET /acciones.
    /// Null = ninguno, que es el caso de todas las acciones anteriores a 2026-09-12.
    /// </summary>
    public string? DefinicionParametrosJson { get; set; }

    /// <summary>
    /// Config técnica propia del tipo de equipo (JSON). NUNCA sale por la API de ejecución, igual que
    /// Direccion: la dirección donde un PLC recibe el tiempo, o el texto fijo de un cartel.
    /// </summary>
    public string? ConfigJson { get; set; }

    /// <summary>
    /// Una acción deshabilitada se comporta EXACTAMENTE igual que una inexistente: 404, sin
    /// distinguir (§6 y la decisión 1 de este plan). Distinguir le confirmaría a quien pregunta que
    /// el código existe.
    /// </summary>
    public bool Habilitada { get; set; }
}
