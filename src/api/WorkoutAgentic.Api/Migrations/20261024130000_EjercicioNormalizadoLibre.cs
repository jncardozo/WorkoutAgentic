using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations;

/// <inheritdoc />
public partial class EjercicioNormalizadoLibre : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "EjercicioCanonico",
            table: "RegistroParcial",
            type: "TEXT",
            maxLength: 120,
            nullable: true);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "EjercicioCanonico",
            table: "RegistroParcial");
}
