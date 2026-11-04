using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations;

/// <summary>
/// Actualiza el snapshot del modelo activo después de retirar del runtime los
/// modelos históricos de WhatsApp y profesor. Conserva intacta la base existente.
/// </summary>
public partial class AlinearModeloActivo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intencionalmente sin DDL: las estructuras históricas quedan preservadas.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intencionalmente sin DDL: revertir el marcador no altera datos históricos.
    }
}
