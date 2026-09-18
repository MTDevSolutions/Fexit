using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Tests;

/// <summary>
/// El backfill es la parte peligrosa del plan: si se equivoca, una acción de escritura pierde una
/// precondición y nadie se entera hasta que algo se mueva cuando no debía. Se prueba contra datos
/// insertados de verdad y no contra el modelo de EF: un UPDATE que no matchea nada no falla,
/// simplemente no hace nada, y el test pasaría igual.
///
/// Por eso se migra hasta la migración ANTERIOR a las dos de esta tarea, se insertan las filas sobre
/// el esquema que ESA migración dejó —y no con un CREATE TABLE a mano, que se desincroniza del
/// esquema real— y recién ahí se corre el resto. Mismo patrón que RenombreControladorTests.
/// </summary>
public class BackfillEquiposTests : IDisposable
{
    private const string _migracionAnterior = "20260917210946_EstadosDePlanta";

    private readonly SqliteConnection _ancla;
    private readonly string _cs;

    public BackfillEquiposTests()
    {
        _cs = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared;Foreign Keys=True";
        _ancla = new SqliteConnection(_cs);
        _ancla.Open();
    }

    public void Dispose() => _ancla.Dispose();

    private FexitDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<FexitDbContext>().UseSqlite(_cs).Options);

    [Fact]
    public async Task Cada_accion_conserva_exactamente_los_enclavamientos_que_evalua_hoy()
    {
        // Estado previo: dos controladores, cada uno con sus enclavamientos y sus acciones.
        await using (var ctx = CrearContext())
        {
            ctx.GetService<IMigrator>().Migrate(_migracionAnterior);
            await ctx.Database.ExecuteSqlRawAsync("""
                INSERT INTO Controladores (Id, Nombre, TipoEquipo, Ip, Puerto, Protocolo, Modelo, Rack, Slot)
                VALUES (1,'PLC Porteria','plc','10.0.0.1',102,'SiemensS7','S71200',0,1),
                       (2,'Cartel D16','cartel','10.0.0.2',5005,'HuiduSdk','S7300',0,0);

                INSERT INTO Enclavamientos (Id, ControladorId, Direccion, TipoDireccion, Nombre, ValoresOk, Orden)
                VALUES (1,1,'DB1.DBX1.0','S7Bit','Porton de playa','[1]',0),
                       (2,1,'DB1.DBX1.1','S7Bit','Semaforo en rojo','[1]',1);

                INSERT INTO Acciones (Id, Codigo, Descripcion, Modo, ControladorId, Direccion, TipoDireccion,
                    Valor, UsaEnclavamientos, DefinicionParametrosJson, ConfigJson, Habilitada)
                VALUES (1,'bajar_barrera','Baja','escritura',1,'DB1.DBX0.0','S7Bit',1,1,NULL,NULL,1),
                       (2,'mostrar_cartel','Muestra','escritura',2,NULL,NULL,NULL,0,NULL,NULL,1);
                """);
        }

        await using var ctxNuevo = CrearContext();
        await ctxNuevo.Database.MigrateAsync();

        // Un equipo por controlador, con su nombre. Se asserta el par nombre↔controlador y no el
        // orden de los ids: lo que importa es que cada equipo sea EL de su controlador.
        var equipos = await ctxNuevo.Equipos.OrderBy(e => e.ControladorId).ToListAsync();
        Assert.Equal(2, equipos.Count);
        Assert.Equal("PLC Porteria", equipos.Single(e => e.ControladorId == 1).Nombre);
        Assert.Equal("Cartel D16", equipos.Single(e => e.ControladorId == 2).Nombre);
        Assert.All(equipos, e => Assert.True(e.SectorId > 0));

        // Y lo que importa: el conjunto de enclavamientos de cada acción no cambió.
        var bajarBarrera = await ctxNuevo.Acciones.SingleAsync(a => a.Codigo == "bajar_barrera");
        var suyos = await ctxNuevo.Enclavamientos.Where(e => e.EquipoId == bajarBarrera.EquipoId)
            .Select(e => e.Nombre).OrderBy(n => n).ToListAsync();
        Assert.Equal(["Porton de playa", "Semaforo en rojo"], suyos);

        var cartel = await ctxNuevo.Acciones.SingleAsync(a => a.Codigo == "mostrar_cartel");
        Assert.Empty(await ctxNuevo.Enclavamientos.Where(e => e.EquipoId == cartel.EquipoId).ToListAsync());

        // Ninguna fila quedó sin equipo: es lo único que distingue "el UPDATE anduvo" de "el UPDATE
        // no matcheó nada", que en SQL es exactamente el mismo silencio.
        Assert.True(bajarBarrera.EquipoId > 0);
        Assert.True(cartel.EquipoId > 0);
        Assert.NotEqual(bajarBarrera.EquipoId, cartel.EquipoId);
    }
}
