using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// Migrations al arrancar, fail fast: si tira, la app no levanta. Mismo criterio que DixitBE — una
/// app corriendo contra un esquema viejo hace daño en silencio, no levantar se ve enseguida.
/// Corre en el wiring de DI, antes de que exista el ServiceProvider, así que construye su propio
/// contexto desde la connection string.
/// </summary>
public class FexitDbInitializer(string connectionString)
{
    public void Inicializar()
    {
        var carpeta = Path.GetDirectoryName(RutaDelArchivo(connectionString));
        if (!string.IsNullOrWhiteSpace(carpeta))
            Directory.CreateDirectory(carpeta);

        using var ctx = new FexitDbContext(
            new DbContextOptionsBuilder<FexitDbContext>().UseSqlite(connectionString).Options);

        ctx.Database.Migrate();

        // WAL: permite copiar el archivo con la app corriendo, que en una planta sin ventana de
        // mantenimiento es la única forma de sacar un backup. En BD in-memory (tests) es no-op.
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    private static string RutaDelArchivo(string cs) =>
        new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(cs).DataSource;
}
