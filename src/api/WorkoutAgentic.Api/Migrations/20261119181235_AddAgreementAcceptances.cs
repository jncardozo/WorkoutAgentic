using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreementAcceptances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgreementAcceptances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DocumentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DocumentText = table.Column<string>(type: "TEXT", nullable: false),
                    ParticipantName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ParticipantEmail = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementAcceptances_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementAcceptances_UserId_DocumentHash",
                table: "AgreementAcceptances",
                columns: new[] { "UserId", "DocumentHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementAcceptances");
        }
    }
}
