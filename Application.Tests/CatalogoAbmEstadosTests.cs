using Application.Dtos;
using Infrastructure.Data.Repositorios;

namespace Application.Tests;

public class CatalogoAbmEstadosTests
{
    private static EstadoRequest Bit(string codigo, string direccion) =>
        new(codigo, "Posición", "posición de la barrera", direccion, "S7Bit",
            new Dictionary<int, string> { [0] = "baja", [1] = "levantada" }, null, 0, 0);

    [Fact]
    public async Task El_alta_acepta_un_lote()
    {
        // Un importador con 200 señales no puede hacer 200 llamadas.
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var (barrera, _) = await EstadoCatalogoTests.SembrarDosEquiposAsync(ctx);
        var repo = new CatalogoRepository(db.CrearContext());

        var r = await repo.GuardarEstadosAsync(barrera.Id,
            [Bit("posicion_barrera_1", "I0.0"), Bit("radar_barrera_1", "I0.1")], default);

        Assert.Equal(2, r.Creados);
        Assert.Equal(0, r.Actualizados);
    }

    [Fact]
    public async Task Reimportar_actualiza_y_no_duplica()
    {
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var (barrera, _) = await EstadoCatalogoTests.SembrarDosEquiposAsync(ctx);
        var repo = new CatalogoRepository(db.CrearContext());

        await repo.GuardarEstadosAsync(barrera.Id, [Bit("posicion_barrera_1", "I0.0")], default);
        var r = await repo.GuardarEstadosAsync(barrera.Id, [Bit("posicion_barrera_1", "I0.5")], default);

        Assert.Equal(0, r.Creados);
        Assert.Equal(1, r.Actualizados);
        await using var verif = db.CrearContext();
        Assert.Single(verif.Estados);
        Assert.Equal("I0.5", verif.Estados.Single().Direccion);
    }

    [Fact]
    public async Task Una_direccion_ya_cargada_avisa_pero_deja_pasar()
    {
        // §2.4: la duplicación está permitida a propósito. El aviso es la única mitigación, y no
        // puede convertirse en un bloqueo.
        using var db = new DbDePrueba();
        await using var ctx = db.CrearContext();
        var (barrera, radar) = await EstadoCatalogoTests.SembrarDosEquiposAsync(ctx);
        var repo = new CatalogoRepository(db.CrearContext());

        await repo.GuardarEstadosAsync(radar.Id, [Bit("presencia_radar", "I0.1")], default);
        var r = await repo.GuardarEstadosAsync(barrera.Id, [Bit("radar_barrera_1", "I0.1")], default);

        Assert.Equal(1, r.Creados);
        Assert.Single(r.Avisos);
        Assert.Contains("Radar ingreso", r.Avisos[0]);
    }
}
