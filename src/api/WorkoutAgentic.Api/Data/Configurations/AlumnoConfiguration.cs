using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class AlumnoConfiguration : IEntityTypeConfiguration<Alumno>
{
    public void Configure(EntityTypeBuilder<Alumno> builder)
    {
        builder.ToTable("Alumno");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nombre).HasMaxLength(120);
        builder.Property(a => a.Email).HasMaxLength(180);
        builder.Property(a => a.Estado).HasMaxLength(20);

        // Email opcional de login SPA; único cuando exista (los NULL quedan aparte).
        builder.HasIndex(a => a.Email).IsUnique();
    }
}
