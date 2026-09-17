namespace Domain.Entities;

/// <summary>
/// Una condición del equipo y los valores con los que se considera en condición.
///
/// Cuelga del EQUIPO y no de la acción, y es la decisión de diseño de §4.2. Los usan los dos
/// lados: la escritura los evalúa como precondición y aborta antes de escribir, y la lectura los
/// devuelve como tabla para contestar "¿por qué no arranca la bomba?". Si cada acción llevara su
/// propia lista, "arrancar bomba 3" y "estado de bomba 3" tendrían dos listas de lo mismo, y el día
/// que se agrega un enclavamiento se actualiza una y se olvida la otra: el diagnóstico informaría los
/// tres viejos y le afirmaría al usuario, con total seguridad, que está todo en condición. Con una
/// sola lista por equipo no pueden divergir, porque son la misma fila.
///
/// Del EQUIPO y no del controlador (spec 2026-09-17 §2.2): un mismo PLC atiende la barrera, el radar
/// y el semáforo, y "el portón de playa está abierto" no es una precondición del PLC — es de la
/// barrera. Colgados del controlador, bajar la barrera exigía además las condiciones del semáforo.
/// </summary>
public class Enclavamiento
{
    public long Id { get; set; }
    public long EquipoId { get; set; }
    public Equipo? Equipo { get; set; }

    /// <summary>Dirección cruda en el controlador del equipo. Nunca sale de Fexit, ni en un error.</summary>
    public string Direccion { get; set; } = default!;

    /// <summary>Qué área/registro leer: S7Bit, HoldingRegister, Coil, etc.</summary>
    public string TipoDireccion { get; set; } = default!;

    /// <summary>Lo único que ve el usuario ("Portón de playa"). Va como columna de la tabla.</summary>
    public string Nombre { get; set; } = default!;

    /// <summary>
    /// Con qué valores está en condición. LISTA y no valor único (§10.5): un selector con varias
    /// posiciones válidas está en condición con más de un valor, y modelarlo como igualdad obligaría
    /// a rehacer la config el día que aparezca el primero.
    /// </summary>
    public List<int> ValoresOk { get; set; } = [];

    /// <summary>Orden de presentación en la tabla del diagnóstico.</summary>
    public int Orden { get; set; }
}
