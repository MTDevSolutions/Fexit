using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Tests;

/// <summary>
/// El renombre no puede perder filas ni relaciones: la instalación del cliente ya tiene el PLC de
/// portería y el cartel D16 cargados a mano, con sus acciones probadas contra el fierro real.
///
/// Migrar desde cero dejaría las tablas vacías y el test pasaría sin probar nada — un UPDATE que
/// matchea cero filas no falla. Por eso se migra hasta la migración ANTERIOR a esta, se insertan las
/// filas sobre el esquema viejo que esa migración dejó, y recién ahí se corre el resto.
/// </summary>
public class RenombreControladorTests : IDisposable
{
    private const string _migracionAnterior = "20260912203354_AccionesConParametros";

    private readonly SqliteConnection _ancla;
    private readonly string _cs;

    public RenombreControladorTests()
    {
        _cs = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared;Foreign Keys=True";
        _ancla = new SqliteConnection(_cs);
        _ancla.Open();
    }

    public void Dispose() => _ancla.Dispose();

    private FexitDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<FexitDbContext>().UseSqlite(_cs).Options);

    [Fact]
    public void Migrar_conserva_los_equipos_como_controladores_y_sus_acciones()
    {
        // Esquema VIEJO, tal como lo dejó la migración anterior.
        using (var ctx = CrearContext())
        {
            ctx.GetService<IMigrator>().Migrate(_migracionAnterior);
            ctx.Database.ExecuteSqlRaw("""
                INSERT INTO Equipos (Id, Nombre, TipoEquipo, Ip, Puerto, Protocolo, Modelo, Rack, Slot)
                VALUES (1,'PLC Porteria','plc','192.168.226.103',102,'SiemensS7','S71200',0,1);
                INSERT INTO Acciones (Id, Codigo, Descripcion, Modo, EquipoId, Direccion, TipoDireccion, Valor,
                    UsaEnclavamientos, DefinicionParametrosJson, ConfigJson, Habilitada)
                VALUES (1,'bajar_barrera_ingreso','Baja la barrera','escritura',1,
                    'DB1.DBX0.0','S7Bit',1,1,NULL,NULL,1);
                """);
        }

        using var ctxNuevo = CrearContext();
        ctxNuevo.Database.Migrate();

        var controlador = ctxNuevo.Controladores.Single();
        Assert.Equal("PLC Porteria", controlador.Nombre);
        Assert.Equal("192.168.226.103", controlador.Ip);

        var accion = ctxNuevo.Acciones.Single();
        Assert.Equal(controlador.Id, accion.ControladorId);
    }
}
