using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> b)
    {
        b.ToTable("Sectores");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedOnAdd().HasAnnotation("Sqlite:Autoincrement", true);
        b.Property(e => e.Nombre).IsRequired();
        b.Property(e => e.Orden).HasDefaultValue(0);

        b.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("IX_Sectores_Nombre");
    }
}
