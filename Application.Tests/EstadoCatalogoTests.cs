using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests;

public class EstadoCatalogoTests
{
    [Fact]
    public async Task La_misma_direccion_puede_cargarse_en_dos_equipos()
    {
        // Decisión explícita del spec §2.4: el bit del radar puede estar en el equipo Radar y también
        // en la Barrera 1. Se acepta que puedan divergir; el aviso lo da el ABM, no la base.
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var (barrera, radar) = await SembrarDosEquiposAsync(ctx);

        ctx.Estados.AddRange(
            new Estado { EquipoId = barrera.Id, Codigo = "radar_barrera_1", Nombre = "Radar",
                Direccion = "I0.1", TipoDireccion = "S7Bit",
                Etiquetas = new() { [0] = "hay alguien", [1] = "sin presencia" } },
            new Estado { EquipoId = radar.Id, Codigo = "presencia_radar", Nombre = "Presencia",
                Direccion = "I0.1", TipoDireccion = "S7Bit",
                Etiquetas = new() { [0] = "hay alguien", [1] = "sin presencia" } });

        await ctx.SaveChangesAsync();
        Assert.Equal(2, await ctx.Estados.CountAsync(e => e.Direccion == "I0.1"));
    }

    [Fact]
    public async Task Un_estado_no_puede_tener_etiquetas_y_unidad_a_la_vez()
    {
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var (barrera, _) = await SembrarDosEquiposAsync(ctx);

        ctx.Estados.Add(new Estado { EquipoId = barrera.Id, Codigo = "raro", Nombre = "Raro",
            Direccion = "I0.2", TipoDireccion = "S7Bit",
            Etiquetas = new() { [0] = "no" }, Unidad = "%" });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task El_codigo_es_unico_en_toda_la_instalacion()
    {
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var (barrera, radar) = await SembrarDosEquiposAsync(ctx);

        ctx.Estados.Add(new Estado { EquipoId = barrera.Id, Codigo = "repetido", Nombre = "A",
            Direccion = "I0.3", TipoDireccion = "S7Bit", Etiquetas = new() { [0] = "no" } });
        await ctx.SaveChangesAsync();

        ctx.Estados.Add(new Estado { EquipoId = radar.Id, Codigo = "repetido", Nombre = "B",
            Direccion = "I0.4", TipoDireccion = "S7Bit", Etiquetas = new() { [0] = "no" } });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    internal static async Task<(Equipo Barrera, Equipo Radar)> SembrarDosEquiposAsync(Infrastructure.Data.FexitDbContext ctx)
    {
        var sector = new Sector { Nombre = "Portería", Orden = 1 };
        var plc = new Controlador { Nombre = "PLC Porteria", TipoEquipo = "plc", Ip = "10.0.0.1",
            Puerto = 102, Protocolo = "SiemensS7", Modelo = "S71200", Rack = 0, Slot = 1 };
        ctx.AddRange(sector, plc);
        await ctx.SaveChangesAsync();

        var barrera = new Equipo { Nombre = "Barrera 1", Descripcion = "Barrera de ingreso", SectorId = sector.Id, ControladorId = plc.Id };
        var radar = new Equipo { Nombre = "Radar ingreso", Descripcion = "Radar de la barrera 1", SectorId = sector.Id, ControladorId = plc.Id };
        ctx.Equipos.AddRange(barrera, radar);
        await ctx.SaveChangesAsync();
        return (barrera, radar);
    }
}
