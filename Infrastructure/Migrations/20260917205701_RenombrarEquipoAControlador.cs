using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Sólo renombre: la instalación del cliente ya tiene equipos y acciones cargados a mano, y un
    /// DropTable/CreateTable (lo que EF scaffoleó acá) se los llevaría puestos. RenameTable/
    /// RenameColumn/RenameIndex preservan las filas; los CHECK no se pueden renombrar in place en
    /// SQLite, así que se recrean con el nuevo nombre — Drop/Add, no Drop/Create de la tabla.
    /// </remarks>
    public partial class RenombrarEquipoAControlador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Equipos",
                newName: "Controladores");

            migrationBuilder.RenameColumn(
                name: "EquipoId",
                table: "Acciones",
                newName: "ControladorId");

            migrationBuilder.RenameColumn(
                name: "EquipoId",
                table: "Enclavamientos",
                newName: "ControladorId");

            migrationBuilder.RenameIndex(
                name: "IX_Equipos_Nombre",
                table: "Controladores",
                newName: "IX_Controladores_Nombre");

            migrationBuilder.RenameIndex(
                name: "IX_Acciones_EquipoId",
                table: "Acciones",
                newName: "IX_Acciones_ControladorId");

            migrationBuilder.RenameIndex(
                name: "IX_Enclavamientos_Equipo_Orden",
                table: "Enclavamientos",
                newName: "IX_Enclavamientos_Controlador_Orden");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_Protocolo",
                table: "Controladores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_TipoEquipo",
                table: "Controladores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_Puerto",
                table: "Controladores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_Modelo",
                table: "Controladores");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Controladores_Protocolo",
                table: "Controladores",
                sql: "Protocolo IN ('SiemensS7','ModbusTcp','Simulado','HuiduSdk')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Controladores_TipoEquipo",
                table: "Controladores",
                sql: "TipoEquipo IN ('plc','cartel')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Controladores_Puerto",
                table: "Controladores",
                sql: "Puerto > 0 AND Puerto <= 65535");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Controladores_Modelo",
                table: "Controladores",
                sql: "Modelo IN ('S7200','S7300','S7400','S71200','S71500')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Controladores_Protocolo",
                table: "Controladores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Controladores_TipoEquipo",
                table: "Controladores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Controladores_Puerto",
                table: "Controladores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Controladores_Modelo",
                table: "Controladores");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_Protocolo",
                table: "Controladores",
                sql: "Protocolo IN ('SiemensS7','ModbusTcp','Simulado','HuiduSdk')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_TipoEquipo",
                table: "Controladores",
                sql: "TipoEquipo IN ('plc','cartel')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_Puerto",
                table: "Controladores",
                sql: "Puerto > 0 AND Puerto <= 65535");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_Modelo",
                table: "Controladores",
                sql: "Modelo IN ('S7200','S7300','S7400','S71200','S71500')");

            migrationBuilder.RenameIndex(
                name: "IX_Controladores_Nombre",
                table: "Controladores",
                newName: "IX_Equipos_Nombre");

            migrationBuilder.RenameIndex(
                name: "IX_Acciones_ControladorId",
                table: "Acciones",
                newName: "IX_Acciones_EquipoId");

            migrationBuilder.RenameIndex(
                name: "IX_Enclavamientos_Controlador_Orden",
                table: "Enclavamientos",
                newName: "IX_Enclavamientos_Equipo_Orden");

            migrationBuilder.RenameColumn(
                name: "ControladorId",
                table: "Acciones",
                newName: "EquipoId");

            migrationBuilder.RenameColumn(
                name: "ControladorId",
                table: "Enclavamientos",
                newName: "EquipoId");

            migrationBuilder.RenameTable(
                name: "Controladores",
                newName: "Equipos");
        }
    }
}
