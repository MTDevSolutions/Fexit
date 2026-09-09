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

    public void Dispose() => _ancla.Dispose();
}
