using Application.Constantes;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class EquipoConfiguration : IEntityTypeConfiguration<Equipo>
{
    public void Configure(EntityTypeBuilder<Equipo> b)
    {
        b.ToTable("Equipos", t =>
        {
            // Un protocolo que ninguna factory atiende sólo fallaría el día que alguien ejecuta una
            // acción de este equipo. La base lo rechaza al cargarlo.
            t.HasCheckConstraint("CK_Equipos_Protocolo",
                $"Protocolo IN ('{CteFexit.ProtocoloSiemensS7}','{CteFexit.ProtocoloModbusTcp}','{CteFexit.ProtocoloSimulado}')");
            t.HasCheckConstraint("CK_Equipos_TipoEquipo", $"TipoEquipo IN ('{CteFexit.TipoEquipoPlc}')");
            t.HasCheckConstraint("CK_Equipos_Puerto", "Puerto > 0 AND Puerto <= 65535");
            // Los cinco que conoce S7netplus. Un modelo equivocado no da un error legible: da una
            // PlcException genérica en la primera lectura, y quien la ve revisa el cableado.
            t.HasCheckConstraint("CK_Equipos_Modelo",
                $"Modelo IN ({EnclavamientoConfiguration.Enumerado(CteFexit.Modelos)})");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedOnAdd().HasAnnotation("Sqlite:Autoincrement", true);
        b.Property(e => e.Nombre).IsRequired();
        b.Property(e => e.TipoEquipo).IsRequired().HasDefaultValue(CteFexit.TipoEquipoPlc);
        b.Property(e => e.Ip).IsRequired();
        b.Property(e => e.Puerto).IsRequired();
        b.Property(e => e.Protocolo).IsRequired();
        b.Property(e => e.Modelo).IsRequired().HasDefaultValue(CteFexit.ModeloS7300);
        b.Property(e => e.Rack).HasDefaultValue(0);
        b.Property(e => e.Slot).HasDefaultValue(0);

        b.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("IX_Equipos_Nombre");
    }
}
