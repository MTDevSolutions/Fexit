using Application.Dtos;
using Infrastructure.Data.Repositorios;
using Xunit;

namespace Application.Tests;

public class EstadosPublicadosTests
{
    [Fact]
    public async Task El_catalogo_publicado_no_lleva_direccion_ni_controlador()
    {
        // La promesa de §3.2: que el DTO no tenga esas propiedades es lo que hace imposible que se
        // filtren, mejor que acordarse de no mapearlas.
        var props = typeof(EstadoPublicadoDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Direccion", props);
        Assert.DoesNotContain("TipoDireccion", props);
        Assert.DoesNotContain("ControladorId", props);
        Assert.DoesNotContain("Etiquetas", props);
    }

    [Fact]
    public async Task Lista_los_estados_con_su_equipo_y_su_sector()
    {
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var (barrera, _) = await EstadoCatalogoTests.SembrarDosEquiposAsync(ctx);
        ctx.Estados.Add(new Domain.Entities.Estado { EquipoId = barrera.Id, Codigo = "posicion_barrera_1",
            Nombre = "Posición", Descripcion = "posición de la barrera", Direccion = "I0.0",
            TipoDireccion = "S7Bit", Etiquetas = new() { [0] = "baja", [1] = "levantada" } });
        await ctx.SaveChangesAsync();

        var repo = new EstadoRepository(db.CrearContext());
        var publicados = await repo.ListarPublicadosAsync(default);

        var uno = Assert.Single(publicados);
        Assert.Equal("posicion_barrera_1", uno.Codigo);
        Assert.Equal("Barrera 1", uno.Equipo);
        Assert.Equal("Portería", uno.Sector);
    }
}
