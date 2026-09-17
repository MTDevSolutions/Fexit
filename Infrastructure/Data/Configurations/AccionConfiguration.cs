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
            // CK_Acciones_EscrituraCompleta se eliminó el 2026-09-12: exigía dirección y valor en
            // TODA escritura, y una escritura de cartel no tiene ninguna de las dos. El CHECK no puede
            // ver el TipoEquipo del controlador, así que la regla vive en el ABM (según el tipo) y en
            // los ejecutores, que fallan cerrado con ConfigInvalida si la fila está incompleta.
        });
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedOnAdd().HasAnnotation("Sqlite:Autoincrement", true);
        b.Property(a => a.Codigo).IsRequired();
        b.Property(a => a.Descripcion).IsRequired();
        b.Property(a => a.Modo).IsRequired();
        b.Property(a => a.UsaEnclavamientos).HasDefaultValue(false);

        // Sin HasDefaultValue a propósito: con ese mapeo EF marca la columna ValueGeneratedOnAdd y
        // omite del INSERT toda propiedad cuyo valor sea el default del CLR — el default del CLR de
        // un bool es false, que es el OPUESTO del default que tendría la base (true). Una acción
        // creada explícitamente como deshabilitada se guardaría habilitada, en silencio. El default
        // de esta columna lo dueña C#: la propiedad nace en false y cada alta la setea explícita.
        b.Property(a => a.Habilitada);

        b.HasOne(a => a.Controlador).WithMany()
            .HasForeignKey(a => a.ControladorId).OnDelete(DeleteBehavior.Restrict);

        // El codigo es lo que manda Dixit: único, o "abrir_barrera" podría resolver a dos filas
        // distintas y la ejecución sería no determinista.
        b.HasIndex(a => a.Codigo).IsUnique().HasDatabaseName("IX_Acciones_Codigo");
    }
}
