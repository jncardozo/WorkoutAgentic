using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class EjercicioConfiguration : IEntityTypeConfiguration<Ejercicio>
{
    public void Configure(EntityTypeBuilder<Ejercicio> builder)
    {
        builder.ToTable("Ejercicio");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.NombreCanonico).HasMaxLength(120);
        builder.Property(e => e.GrupoMuscular).HasMaxLength(60);

        // Nombre canónico único: es la llave del CatalogResolver.
        builder.HasIndex(e => e.NombreCanonico).IsUnique();
    }
}
