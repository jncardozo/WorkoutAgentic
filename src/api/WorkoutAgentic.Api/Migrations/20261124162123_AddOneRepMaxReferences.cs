using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOneRepMaxReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOneRepMax",
                table: "RegistroParcial",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOneRepMax",
                table: "PlanillaFila",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReferenceConfirmedUtc",
                table: "PlanillaFila",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReferenceOneRepMaxKg",
                table: "PlanillaFila",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferencePlanillaId",
                table: "PlanillaFila",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceRowOrder",
                table: "PlanillaFila",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOneRepMax",
                table: "RegistroParcial");

            migrationBuilder.DropColumn(
                name: "IsOneRepMax",
                table: "PlanillaFila");

            migrationBuilder.DropColumn(
                name: "ReferenceConfirmedUtc",
                table: "PlanillaFila");

            migrationBuilder.DropColumn(
                name: "ReferenceOneRepMaxKg",
                table: "PlanillaFila");

            migrationBuilder.DropColumn(
                name: "ReferencePlanillaId",
                table: "PlanillaFila");

            migrationBuilder.DropColumn(
                name: "ReferenceRowOrder",
                table: "PlanillaFila");
        }
    }
}
