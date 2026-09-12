using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccionesConParametros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_Protocolo",
                table: "Equipos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_TipoEquipo",
                table: "Equipos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Acciones_EscrituraCompleta",
                table: "Acciones");

            migrationBuilder.AddColumn<string>(
                name: "ConfigJson",
                table: "Acciones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinicionParametrosJson",
                table: "Acciones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_Protocolo",
                table: "Equipos",
                sql: "Protocolo IN ('SiemensS7','ModbusTcp','Simulado','HuiduSdk')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_TipoEquipo",
                table: "Equipos",
                sql: "TipoEquipo IN ('plc','cartel')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_Protocolo",
                table: "Equipos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipos_TipoEquipo",
                table: "Equipos");

            migrationBuilder.DropColumn(
                name: "ConfigJson",
                table: "Acciones");

            migrationBuilder.DropColumn(
                name: "DefinicionParametrosJson",
                table: "Acciones");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_Protocolo",
                table: "Equipos",
                sql: "Protocolo IN ('SiemensS7','ModbusTcp','Simulado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipos_TipoEquipo",
                table: "Equipos",
                sql: "TipoEquipo IN ('plc')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Acciones_EscrituraCompleta",
                table: "Acciones",
                sql: "Modo <> 'escritura' OR (Direccion IS NOT NULL AND TipoDireccion IS NOT NULL AND Valor IS NOT NULL)");
        }
    }
}
