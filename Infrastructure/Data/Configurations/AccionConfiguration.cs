using Application.Constantes;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class AccionConfiguration : IEntityTypeConfiguration<Accion>
{
    public void Configure(EntityTypeBuilder<Accion> b)
    {
        b.ToTable("Acciones", t =>
        {
            t.HasCheckConstraint("CK_Acciones_Modo",
                $"Modo IN ('{CteFexit.ModoLectura}','{CteFexit.ModoEscritura}')");
            t.HasCheckConstraint("CK_Acciones_TipoDireccion",
                $"TipoDireccion IS NULL OR TipoDireccion IN ({EnclavamientoConfiguration.Enumerado(CteFexit.TiposDireccion)})");
            // Una escritura sin dónde ni qué escribir no es ejecutable. Se valida también en el ABM
            // para dar un 400 legible, pero acá es donde no se puede saltear cargando por SQL.
            t.HasCheckConstraint("CK_Acciones_EscrituraCompleta",
                $"Modo <> '{CteFexit.ModoEscritura}' OR (Direccion IS NOT NULL AND TipoDireccion IS NOT NULL AND Valor IS NOT NULL)");
        });
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedOnAdd().HasAnnotation("Sqlite:Autoincrement", true);
        b.Property(a => a.Codigo).IsRequired();
        b.Property(a => a.Descripcion).IsRequired();
        b.Property(a => a.Modo).IsRequired();
        b.Property(a => a.UsaEnclavamientos).HasDefaultValue(false);
        b.Property(a => a.Habilitada).HasDefaultValue(true);

        b.HasOne(a => a.Equipo).WithMany()
            .HasForeignKey(a => a.EquipoId).OnDelete(DeleteBehavior.Restrict);

        // El codigo es lo que manda Dixit: único, o "abrir_barrera" podría resolver a dos filas
        // distintas y la ejecución sería no determinista.
        b.HasIndex(a => a.Codigo).IsUnique().HasDatabaseName("IX_Acciones_Codigo");
    }
}
