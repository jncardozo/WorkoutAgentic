using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "RegistroParcial",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observations",
                table: "RegistroParcial",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "PlanillaFila",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observations",
                table: "PlanillaFila",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comments",
                table: "RegistroParcial");

            migrationBuilder.DropColumn(
                name: "Observations",
                table: "RegistroParcial");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "PlanillaFila");

            migrationBuilder.DropColumn(
                name: "Observations",
                table: "PlanillaFila");
        }
    }
}
