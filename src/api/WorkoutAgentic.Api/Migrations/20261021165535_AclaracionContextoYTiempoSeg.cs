using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations
{
    /// <inheritdoc />
    public partial class AclaracionContextoYTiempoSeg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TiempoSeg",
                table: "RegistroParcial",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contexto",
                table: "AclaracionPendiente",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TiempoSeg",
                table: "RegistroParcial");

            migrationBuilder.DropColumn(
                name: "Contexto",
                table: "AclaracionPendiente");
        }
    }
}
