using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    TipoEquipo = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "plc"),
                    Ip = table.Column<string>(type: "TEXT", nullable: false),
                    Puerto = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocolo = table.Column<string>(type: "TEXT", nullable: false),
                    Modelo = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "S7300"),
                    Rack = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipos", x => x.Id);
                    table.CheckConstraint("CK_Equipos_Modelo", "Modelo IN ('S7200','S7300','S7400','S71200','S71500')");
                    table.CheckConstraint("CK_Equipos_Protocolo", "Protocolo IN ('SiemensS7','ModbusTcp','Simulado')");
                    table.CheckConstraint("CK_Equipos_Puerto", "Puerto > 0 AND Puerto <= 65535");
                    table.CheckConstraint("CK_Equipos_TipoEquipo", "TipoEquipo IN ('plc')");
                });

            migrationBuilder.CreateTable(
                name: "Acciones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false),
                    Modo = table.Column<string>(type: "TEXT", nullable: false),
                    EquipoId = table.Column<long>(type: "INTEGER", nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", nullable: true),
                    TipoDireccion = table.Column<string>(type: "TEXT", nullable: true),
                    Valor = table.Column<int>(type: "INTEGER", nullable: true),
                    UsaEnclavamientos = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Habilitada = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acciones", x => x.Id);
                    table.CheckConstraint("CK_Acciones_EscrituraCompleta", "Modo <> 'escritura' OR (Direccion IS NOT NULL AND TipoDireccion IS NOT NULL AND Valor IS NOT NULL)");
                    table.CheckConstraint("CK_Acciones_Modo", "Modo IN ('lectura','escritura')");
                    table.CheckConstraint("CK_Acciones_TipoDireccion", "TipoDireccion IS NULL OR TipoDireccion IN ('Coil','DiscreteInput','HoldingRegister','InputRegister','S7Bit','S7Byte','S7Word','S7DWord','S7Real')");
                    table.ForeignKey(
                        name: "FK_Acciones_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enclavamientos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipoId = table.Column<long>(type: "INTEGER", nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", nullable: false),
                    TipoDireccion = table.Column<string>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    ValoresOk = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enclavamientos", x => x.Id);
                    table.CheckConstraint("CK_Enclavamientos_TipoDireccion", "TipoDireccion IN ('Coil','DiscreteInput','HoldingRegister','InputRegister','S7Bit','S7Byte','S7Word','S7DWord','S7Real')");
                    table.ForeignKey(
                        name: "FK_Enclavamientos_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acciones_Codigo",
                table: "Acciones",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acciones_EquipoId",
                table: "Acciones",
                column: "EquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_Enclavamientos_Equipo_Orden",
                table: "Enclavamientos",
                columns: new[] { "EquipoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_Nombre",
                table: "Equipos",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acciones");

            migrationBuilder.DropTable(
                name: "Enclavamientos");

            migrationBuilder.DropTable(
                name: "Equipos");
        }
    }
}
