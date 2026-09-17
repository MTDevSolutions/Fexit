using Application.Constantes;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class EnclavamientoConfiguration : IEntityTypeConfiguration<Enclavamiento>
{
    public void Configure(EntityTypeBuilder<Enclavamiento> b)
    {
        b.ToTable("Enclavamientos", t =>
            t.HasCheckConstraint("CK_Enclavamientos_TipoDireccion",
                $"TipoDireccion IN ({Enumerado(CteFexit.TiposDireccion)})"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedOnAdd().HasAnnotation("Sqlite:Autoincrement", true);
        b.Property(e => e.Direccion).IsRequired();
        b.Property(e => e.TipoDireccion).IsRequired();
        b.Property(e => e.Nombre).IsRequired();
        b.Property(e => e.Orden).HasDefaultValue(0);

        b.Property(e => e.ValoresOk).IsRequired()
            .HasConversion(Conversores.ListaIntAJson, Conversores.ListaIntComparer)
            .HasDefaultValueSql("'[]'");

        // Cascade: un enclavamiento sin controlador no significa nada, y borrar el controlador tiene
        // que llevárselos. Es la FK que hace imposible que las dos listas de §4.2 divergan.
        b.HasOne(e => e.Controlador).WithMany(eq => eq.Enclavamientos)
            .HasForeignKey(e => e.ControladorId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.ControladorId, e.Orden }).HasDatabaseName("IX_Enclavamientos_Controlador_Orden");
    }

    internal static string Enumerado(string[] valores) => string.Join(",", valores.Select(v => $"'{v}'"));
}
