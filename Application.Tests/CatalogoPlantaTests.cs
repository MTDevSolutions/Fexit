using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests;

public class CatalogoPlantaTests
{
    [Fact]
    public async Task Un_controlador_puede_tener_varios_equipos_de_distintos_sectores()
    {
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();

        var porteria = new Sector { Nombre = "Portería", Orden = 1 };
        var playa = new Sector { Nombre = "Playa de camiones", Orden = 2 };
        var plc = new Controlador { Nombre = "PLC Porteria", TipoEquipo = "plc", Ip = "10.0.0.1",
            Puerto = 102, Protocolo = "SiemensS7", Modelo = "S71200", Rack = 0, Slot = 1 };
        ctx.AddRange(porteria, playa, plc);
        await ctx.SaveChangesAsync();

        ctx.Equipos.AddRange(
            new Equipo { Nombre = "Barrera 1", Descripcion = "Barrera de ingreso", SectorId = porteria.Id, ControladorId = plc.Id },
            new Equipo { Nombre = "Radar ingreso", Descripcion = "Radar de la barrera 1", SectorId = porteria.Id, ControladorId = plc.Id },
            new Equipo { Nombre = "Semaforo playa", Descripcion = "Semáforo de playa", SectorId = playa.Id, ControladorId = plc.Id });
        await ctx.SaveChangesAsync();

        var delPlc = await ctx.Equipos.Where(e => e.ControladorId == plc.Id).CountAsync();
        Assert.Equal(3, delPlc);
        Assert.Equal(2, await ctx.Equipos.CountAsync(e => e.SectorId == porteria.Id));
    }

    [Fact]
    public async Task No_se_puede_repetir_el_nombre_de_un_equipo()
    {
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var sector = new Sector { Nombre = "Portería", Orden = 1 };
        var plc = new Controlador { Nombre = "PLC", TipoEquipo = "plc", Ip = "10.0.0.1", Puerto = 102,
            Protocolo = "SiemensS7", Modelo = "S71200", Rack = 0, Slot = 1 };
        ctx.AddRange(sector, plc);
        await ctx.SaveChangesAsync();

        ctx.Equipos.Add(new Equipo { Nombre = "Barrera 1", Descripcion = "a", SectorId = sector.Id, ControladorId = plc.Id });
        await ctx.SaveChangesAsync();
        ctx.Equipos.Add(new Equipo { Nombre = "Barrera 1", Descripcion = "b", SectorId = sector.Id, ControladorId = plc.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }
}
