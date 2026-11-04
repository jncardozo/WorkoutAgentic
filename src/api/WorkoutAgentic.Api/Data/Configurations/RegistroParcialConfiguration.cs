using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class RegistroParcialConfiguration : IEntityTypeConfiguration<RegistroParcial>
{
    public void Configure(EntityTypeBuilder<RegistroParcial> builder)
    {
        builder.ToTable("RegistroParcial");
        builder.HasKey(r => r.Id);

        // NULL = unidad sin definir (aclaración en curso).
        builder.Property(r => r.Unidad)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null ? null : Enum.Parse<Unidad>(v))
            .HasMaxLength(20);

        builder.Property(r => r.EjercicioCanonico).HasMaxLength(120);

        builder.HasOne(r => r.Sesion)
            .WithMany(s => s.Registros)
            .HasForeignKey(r => r.SesionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Ejercicio)
            .WithMany(e => e.Registros)
            .HasForeignKey(r => r.EjercicioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Acceso típico del flujo: la serie n de una sesión.
        builder.HasIndex(r => new { r.SesionId, r.NroSerie });
        builder.HasIndex(r => r.EjercicioId);
    }
}
