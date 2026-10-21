using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

// Mapeo del doc de arquitectura §4. Las tablas conservan el nombre del dominio.
public sealed class ProfesorConfiguration : IEntityTypeConfiguration<Profesor>
{
    public void Configure(EntityTypeBuilder<Profesor> builder)
    {
        builder.ToTable("Profesor");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombre).HasMaxLength(120);
        builder.Property(p => p.Email).HasMaxLength(180);
        builder.Property(p => p.Rol).HasMaxLength(20);

        // Email de login SPA: único.
        builder.HasIndex(p => p.Email).IsUnique();
    }
}
