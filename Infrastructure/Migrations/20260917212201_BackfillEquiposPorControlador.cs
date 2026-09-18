using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Los datos de la mudanza de los enclavamientos y las acciones al equipo: un sector, un equipo
    /// por controlador, y cada fila apuntada a su equipo. El cambio de esquema —borrar ControladorId,
    /// dejar EquipoId en NOT NULL, poner las FK— es la migración SIGUIENTE, no ésta.
    ///
    /// Van separadas y en este orden, la de datos PRIMERO, porque EF/SQLite no ejecuta una migración
    /// en el orden en que uno la escribe: cualquier operación que toque columnas o FK se convierte en
    /// una reconstrucción de tabla (CREATE ef_temp / INSERT SELECT / DROP / RENAME) que se arma con el
    /// MODELO DESTINO de esa migración y se agrupa al final. En una sola migración eso borraba
    /// ControladorId antes de que el backfill llegara a leerlo, y con él la única forma de saber a qué
    /// equipo va cada fila. Acá no hay ninguna operación de esquema de EF: son sentencias SQL y se
    /// emiten en orden.
    ///
    /// Por eso también la columna EquipoId se agrega con un ALTER TABLE crudo y NULLABLE. No es
    /// descuido: en ese instante todavía no existe ningún equipo, así que NOT NULL es imposible de
    /// cumplir, y un default 0 dejaría filas apuntando a un equipo inexistente que SQLite ya no vuelve
    /// a revisar — una acción sin enclavamientos, sin error visible, que es justamente el accidente
    /// que esta tarea existe para evitar. Nullable falla ruidoso: la reconstrucción de la migración
    /// siguiente copia los datos a una columna NOT NULL, y una fila sin equipo aborta la migración.
    /// </summary>
    public partial class BackfillEquiposPorControlador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un sector para lo que ya existe. El nombre lo cambia un humano después, por el ABM.
            migrationBuilder.Sql(
                "INSERT INTO Sectores (Nombre, Orden) SELECT 'General', 0 WHERE NOT EXISTS (SELECT 1 FROM Sectores);");

            // Un equipo por controlador, con SU nombre. Esto es lo que preserva el comportamiento: si
            // todos los enclavamientos del PLC eran de una sola cosa, "todos los del equipo" sigue
            // dando el mismo conjunto, y ninguna escritura cambia de precondición sin que nadie lo
            // pida. Un humano después parte ese equipo en los de verdad, mirando la planta.
            migrationBuilder.Sql("""
                INSERT INTO Equipos (Nombre, Descripcion, SectorId, ControladorId)
                SELECT c.Nombre, '', (SELECT Id FROM Sectores WHERE Nombre = 'General'), c.Id
                FROM Controladores c
                WHERE NOT EXISTS (SELECT 1 FROM Equipos e WHERE e.ControladorId = c.Id)
                ORDER BY c.Id;
                """);

            // ALTER TABLE crudo y no AddColumn<long>(): un AddColumn de EF arrastra la reconstrucción
            // de tabla que este orden existe para evitar.
            migrationBuilder.Sql("ALTER TABLE Enclavamientos ADD COLUMN EquipoId INTEGER NULL;");
            migrationBuilder.Sql("ALTER TABLE Acciones ADD COLUMN EquipoId INTEGER NULL;");

            migrationBuilder.Sql(
                "UPDATE Enclavamientos SET EquipoId = (SELECT e.Id FROM Equipos e WHERE e.ControladorId = Enclavamientos.ControladorId);");
            migrationBuilder.Sql(
                "UPDATE Acciones SET EquipoId = (SELECT e.Id FROM Equipos e WHERE e.ControladorId = Acciones.ControladorId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los equipos creados acá no se borran: para cuando se corre este Down, un humano ya pudo
            // haberles colgado estados y haberlos renombrado. Se va sólo la columna, que es lo único
            // que esta migración agregó al esquema.
            migrationBuilder.Sql("ALTER TABLE Acciones DROP COLUMN EquipoId;");
            migrationBuilder.Sql("ALTER TABLE Enclavamientos DROP COLUMN EquipoId;");
        }
    }
}
