using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// El cambio de esquema de la mudanza al equipo: se va ControladorId de Enclavamientos y de
    /// Acciones, y EquipoId —que BackfillEquiposPorControlador ya dejó lleno— queda NOT NULL y con su
    /// FK y su índice. Está escrita a mano porque la migración anterior ya adelantó la mitad del
    /// esquema en SQL crudo, y el diff de EF contra el modelo sale vacío.
    ///
    /// En SQLite estas operaciones no se ejecutan una por una: EF las junta en una reconstrucción de
    /// tabla (CREATE ef_temp / INSERT SELECT / DROP / RENAME) armada con el modelo destino. Eso es
    /// justamente lo que se quiere acá, y de paso es la red: el INSERT SELECT copia EquipoId a una
    /// columna NOT NULL, así que si el backfill hubiera dejado una sola fila sin equipo la migración
    /// aborta en vez de dejar una acción que perdió sus precondiciones en silencio.
    ///
    /// Las dos FK conservan cada una el comportamiento que tenía contra el controlador, y la
    /// diferencia es deliberada: el enclavamiento va en Cascade —sin equipo no significa nada, y se
    /// vuelve a cargar—, la acción en Restrict —borrarla en silencio se nota recién cuando Dixit la
    /// pide y ya no está.
    /// </summary>
    public partial class EnclavamientosYAccionesAlEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Acciones_Controladores_ControladorId",
                table: "Acciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Enclavamientos_Controladores_ControladorId",
                table: "Enclavamientos");

            // Los índices viejos no se borran explícitamente y los nuevos no se crean acá: se van con
            // la tabla que la reconstrucción tira, y los del modelo los vuelve a crear ella misma.
            // Pedirlos a mano duplicaría cada CREATE INDEX en el script.
            migrationBuilder.DropColumn(
                name: "ControladorId",
                table: "Acciones");

            migrationBuilder.DropColumn(
                name: "ControladorId",
                table: "Enclavamientos");

            migrationBuilder.AlterColumn<long>(
                name: "EquipoId",
                table: "Acciones",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "EquipoId",
                table: "Enclavamientos",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Acciones_Equipos_EquipoId",
                table: "Acciones",
                column: "EquipoId",
                principalTable: "Equipos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enclavamientos_Equipos_EquipoId",
                table: "Enclavamientos",
                column: "EquipoId",
                principalTable: "Equipos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Acciones_Equipos_EquipoId",
                table: "Acciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Enclavamientos_Equipos_EquipoId",
                table: "Enclavamientos");

            migrationBuilder.DropIndex(
                name: "IX_Acciones_Equipo",
                table: "Acciones");

            migrationBuilder.DropIndex(
                name: "IX_Enclavamientos_Equipo_Orden",
                table: "Enclavamientos");

            migrationBuilder.AlterColumn<long>(
                name: "EquipoId",
                table: "Acciones",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "EquipoId",
                table: "Enclavamientos",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            // Se vuelve a poblar desde el equipo, que sabe de qué controlador es: sin esto la vuelta
            // atrás dejaría todas las filas apuntando al controlador 0.
            migrationBuilder.AddColumn<long>(
                name: "ControladorId",
                table: "Acciones",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ControladorId",
                table: "Enclavamientos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                "UPDATE Acciones SET ControladorId = (SELECT e.ControladorId FROM Equipos e WHERE e.Id = Acciones.EquipoId);");
            migrationBuilder.Sql(
                "UPDATE Enclavamientos SET ControladorId = (SELECT e.ControladorId FROM Equipos e WHERE e.Id = Enclavamientos.EquipoId);");

            migrationBuilder.CreateIndex(
                name: "IX_Acciones_ControladorId",
                table: "Acciones",
                column: "ControladorId");

            migrationBuilder.CreateIndex(
                name: "IX_Enclavamientos_Controlador_Orden",
                table: "Enclavamientos",
                columns: new[] { "ControladorId", "Orden" });

            migrationBuilder.AddForeignKey(
                name: "FK_Acciones_Controladores_ControladorId",
                table: "Acciones",
                column: "ControladorId",
                principalTable: "Controladores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enclavamientos_Controladores_ControladorId",
                table: "Enclavamientos",
                column: "ControladorId",
                principalTable: "Controladores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
