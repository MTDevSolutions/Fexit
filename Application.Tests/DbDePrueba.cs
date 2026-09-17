using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests;

/// <summary>
/// BD SQLite in-memory compartida entre conexiones (Cache=Shared) que vive mientras la conexión
/// "ancla" esté abierta. Schema por EnsureCreated (rápido; la migración tiene su propio test).
/// Cada instancia usa un nombre único. Foreign Keys=True: sin eso SQLite ignora las FK y los tests
/// de integridad referencial pasarían en verde sin probar nada.
/// </summary>
public sealed class DbDePrueba : IDisposable
{
    private readonly SqliteConnection _ancla;
    public string ConnectionString { get; }

    public DbDePrueba()
    {
        ConnectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared;Foreign Keys=True";
        _ancla = new SqliteConnection(ConnectionString);
        _ancla.Open();
        using var ctx = CrearContext();
        ctx.Database.EnsureCreated();
    }

    public FexitDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<FexitDbContext>().UseSqlite(ConnectionString).Options);

    /// <summary>
    /// Cuelga un equipo de un controlador ya creado, con un sector compartido. Desde el 2026-09-17 los
    /// enclavamientos y las acciones cuelgan del EQUIPO, y el ABM de equipos todavía no existe: hasta
    /// que exista, los tests del catálogo siembran el equipo por acá en vez de por HTTP.
    /// </summary>
    public long SembrarEquipo(long controladorId, string nombre = "Equipo de prueba")
    {
        using var ctx = CrearContext();
        var sector = ctx.Sectores.FirstOrDefault();
        if (sector is null)
        {
            sector = new Sector { Nombre = "General", Orden = 0 };
            ctx.Sectores.Add(sector);
            ctx.SaveChanges();
        }

        var equipo = new Equipo
        {
            Nombre = nombre, Descripcion = "d", SectorId = sector.Id, ControladorId = controladorId,
        };
        ctx.Equipos.Add(equipo);
        ctx.SaveChanges();
        return equipo.Id;
    }

    public void Dispose() => _ancla.Dispose();
}
