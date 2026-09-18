using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class EquipoConfiguration : IEntityTypeConfiguration<Equipo>
{
    public void Configure(EntityTypeBuilder<Equipo> b)
    {
        b.ToTable("Equipos");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedOnAdd().HasAnnotation("Sqlite:Autoincrement", true);
        b.Property(e => e.Nombre).IsRequired();
        b.Property(e => e.Descripcion).IsRequired().HasDefaultValue(string.Empty);

        // Restrict y no Cascade: borrar un controlador no puede llevarse en silencio los equipos y,
        // con ellos, sus acciones probadas contra el fierro. Que falle y que alguien mire.
        b.HasOne(e => e.Controlador).WithMany()
            .HasForeignKey(e => e.ControladorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Sector).WithMany(s => s.Equipos)
            .HasForeignKey(e => e.SectorId).OnDelete(DeleteBehavior.Restrict);

        // El nombre es lo que el usuario pronuncia y lo que el prompt muestra: dos "Barrera 1" harían
        // la respuesta no determinista.
        b.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("IX_Equipos_Nombre");
        b.HasIndex(e => new { e.SectorId, e.Nombre }).HasDatabaseName("IX_Equipos_Sector_Nombre");
    }
}
