using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data;

/// <summary>
/// Sólo para `dotnet ef` en tiempo de diseño: la app real arma el contexto por DI con la connection
/// string del appsettings. La ruta de acá no se usa nunca en runtime.
/// </summary>
public class FexitDbContextFactory : IDesignTimeDbContextFactory<FexitDbContext>
{
    public FexitDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FexitDbContext>().UseSqlite("Data Source=fexit-design.db").Options);
}
