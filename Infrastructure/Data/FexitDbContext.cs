using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// BD propia de Fexit (SQLite). Sólo configuración: la cola, la auditoría y el consumo son de Dixit
/// y no se duplican acá (§10.10). Schema por migrations; mapeo Fluent en Configurations/.
/// </summary>
public class FexitDbContext(DbContextOptions<FexitDbContext> options) : DbContext(options)
{
    public DbSet<Controlador> Controladores => Set<Controlador>();
    public DbSet<Enclavamiento> Enclavamientos => Set<Enclavamiento>();
    public DbSet<Accion> Acciones => Set<Accion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FexitDbContext).Assembly);
}
