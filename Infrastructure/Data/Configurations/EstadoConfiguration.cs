using Application.Constantes;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class EstadoConfiguration : IEntityTypeConfiguration<Estado>
{
    public void Configure(EntityTypeBuilder<Estado> b)
    {
        b.ToTable("Estados", t =>
        {
            t.HasCheckConstraint("CK_Estados_TipoDireccion",
                $"TipoDireccion IN ({EnclavamientoConfiguration.Enumerado(CteFexit.TiposDireccion)})");
            // Una fila sin traducción se leería y se mostraría como un número pelado, que es
            // exactamente lo que la feature existe para evitar. Y las dos a la vez son una
            // contradicción: la base la rechaza en vez de dejar que gane una en silencio.
            t.HasCheckConstraint("CK_Estados_UnaTraduccion",
                "(EtiquetasJson IS NOT NULL AND Unidad IS NULL) OR (EtiquetasJson IS NULL AND Unidad IS NOT NULL)");
            t.HasCheckConstraint("CK_Estados_Decimales", "Decimales >= 0 AND Decimales <= 4");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedOnAdd().HasAnnotation("Sqlite:Autoincrement", true);
        b.Property(e => e.Codigo).IsRequired();
        b.Property(e => e.Nombre).IsRequired();
        b.Property(e => e.Descripcion).IsRequired().HasDefaultValue(string.Empty);
        b.Property(e => e.Direccion).IsRequired();
        b.Property(e => e.TipoDireccion).IsRequired();
        b.Property(e => e.Orden).HasDefaultValue(0);
        b.Property(e => e.Decimales).HasDefaultValue(0);

        b.Property(e => e.Etiquetas).HasColumnName("EtiquetasJson")
            .HasConversion(Conversores.EtiquetasAJson, Conversores.EtiquetasComparer);

        b.HasOne(e => e.Equipo).WithMany(eq => eq.Estados)
            .HasForeignKey(e => e.EquipoId).OnDelete(DeleteBehavior.Cascade);

        // El código es lo que nombra Dixit: único, o el pedido sería ambiguo.
        b.HasIndex(e => e.Codigo).IsUnique().HasDatabaseName("IX_Estados_Codigo");
        b.HasIndex(e => new { e.EquipoId, e.Orden }).HasDatabaseName("IX_Estados_Equipo_Orden");

        // NO hay índice único por (Controlador, Direccion, TipoDireccion): §2.4 lo decidió así. El
        // aviso de dirección repetida lo da el ABM (Tarea 5), no la base.
    }
}
