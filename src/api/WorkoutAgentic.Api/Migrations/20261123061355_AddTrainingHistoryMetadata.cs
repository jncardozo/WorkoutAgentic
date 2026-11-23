using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingHistoryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedUtc",
                table: "PlanillaConfirmada",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "PlanillaConfirmada",
                type: "TEXT",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedUtc",
                table: "PlanillaConfirmada");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "PlanillaConfirmada");
        }
    }
}
