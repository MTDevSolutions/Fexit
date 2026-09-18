using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EstadosDePlanta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Estados",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipoId = table.Column<long>(type: "INTEGER", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Direccion = table.Column<string>(type: "TEXT", nullable: false),
                    TipoDireccion = table.Column<string>(type: "TEXT", nullable: false),
                    EtiquetasJson = table.Column<string>(type: "TEXT", nullable: true),
                    Unidad = table.Column<string>(type: "TEXT", nullable: true),
                    Decimales = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estados", x => x.Id);
                    table.CheckConstraint("CK_Estados_Decimales", "Decimales >= 0 AND Decimales <= 4");
                    table.CheckConstraint("CK_Estados_TipoDireccion", "TipoDireccion IN ('Coil','DiscreteInput','HoldingRegister','InputRegister','S7Bit','S7Byte','S7Word','S7DWord','S7Real')");
                    table.CheckConstraint("CK_Estados_UnaTraduccion", "(EtiquetasJson IS NOT NULL AND Unidad IS NULL) OR (EtiquetasJson IS NULL AND Unidad IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Estados_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Estados_Codigo",
                table: "Estados",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Estados_Equipo_Orden",
                table: "Estados",
                columns: new[] { "EquipoId", "Orden" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Estados");
        }
    }
}
