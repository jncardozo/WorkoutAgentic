using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutAgentic.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCapturasSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ejercicio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreCanonico = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Apodos = table.Column<string>(type: "TEXT", nullable: false),
                    GrupoMuscular = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    UsaPeso = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsaTiempo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ejercicio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profesor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Rol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profesor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Alumno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfesorId = table.Column<int>(type: "INTEGER", nullable: true),
                    TelefonoWa = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 180, nullable: true),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alumno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alumno_Profesor_ProfesorId",
                        column: x => x.ProfesorId,
                        principalTable: "Profesor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invitacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProfesorId = table.Column<int>(type: "INTEGER", nullable: false),
                    AlumnoId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExpiraUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsadaUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitacion_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitacion_Profesor_ProfesorId",
                        column: x => x.ProfesorId,
                        principalTable: "Profesor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SesionCaptura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlumnoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    EjercicioActivoId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrdenSerieSiguiente = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimaActividadUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionCaptura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionCaptura_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionCaptura_Ejercicio_EjercicioActivoId",
                        column: x => x.EjercicioActivoId,
                        principalTable: "Ejercicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AclaracionPendiente",
                columns: table => new
                {
                    SesionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Campo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Reintento = table.Column<int>(type: "INTEGER", nullable: false),
                    MensajeBotWaId = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiraUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AclaracionPendiente", x => x.SesionId);
                    table.ForeignKey(
                        name: "FK_AclaracionPendiente_SesionCaptura_SesionId",
                        column: x => x.SesionId,
                        principalTable: "SesionCaptura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanillaConfirmada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SesionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AlumnoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfirmadaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanillaConfirmada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanillaConfirmada_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanillaConfirmada_SesionCaptura_SesionId",
                        column: x => x.SesionId,
                        principalTable: "SesionCaptura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistroParcial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SesionId = table.Column<int>(type: "INTEGER", nullable: false),
                    EjercicioId = table.Column<int>(type: "INTEGER", nullable: true),
                    EjercicioCrudo = table.Column<string>(type: "TEXT", nullable: true),
                    DescripcionPendiente = table.Column<bool>(type: "INTEGER", nullable: false),
                    NroSerie = table.Column<int>(type: "INTEGER", nullable: false),
                    Reps = table.Column<int>(type: "INTEGER", nullable: true),
                    PesoKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PendienteAclaracion = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroParcial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistroParcial_Ejercicio_EjercicioId",
                        column: x => x.EjercicioId,
                        principalTable: "Ejercicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistroParcial_SesionCaptura_SesionId",
                        column: x => x.SesionId,
                        principalTable: "SesionCaptura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transcripcion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SesionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TextoCrudo = table.Column<string>(type: "TEXT", nullable: false),
                    Proveedor = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    DuracionSeg = table.Column<long>(type: "INTEGER", nullable: true),
                    RecibidoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transcripcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transcripcion_SesionCaptura_SesionId",
                        column: x => x.SesionId,
                        principalTable: "SesionCaptura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanillaFila",
                columns: table => new
                {
                    PlanillaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    EjercicioCanonical = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NroSerie = table.Column<int>(type: "INTEGER", nullable: false),
                    Reps = table.Column<int>(type: "INTEGER", nullable: true),
                    PesoKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanillaFila", x => new { x.PlanillaId, x.Orden });
                    table.ForeignKey(
                        name: "FK_PlanillaFila_PlanillaConfirmada_PlanillaId",
                        column: x => x.PlanillaId,
                        principalTable: "PlanillaConfirmada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alumno_Email",
                table: "Alumno",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alumno_ProfesorId",
                table: "Alumno",
                column: "ProfesorId");

            migrationBuilder.CreateIndex(
                name: "IX_Alumno_TelefonoWa",
                table: "Alumno",
                column: "TelefonoWa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ejercicio_NombreCanonico",
                table: "Ejercicio",
                column: "NombreCanonico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitacion_AlumnoId",
                table: "Invitacion",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitacion_Codigo",
                table: "Invitacion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitacion_ProfesorId",
                table: "Invitacion",
                column: "ProfesorId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanillaConfirmada_AlumnoId",
                table: "PlanillaConfirmada",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanillaConfirmada_SesionId",
                table: "PlanillaConfirmada",
                column: "SesionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profesor_Email",
                table: "Profesor",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroParcial_EjercicioId",
                table: "RegistroParcial",
                column: "EjercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroParcial_SesionId_NroSerie",
                table: "RegistroParcial",
                columns: new[] { "SesionId", "NroSerie" });

            migrationBuilder.CreateIndex(
                name: "IX_SesionCaptura_AlumnoId",
                table: "SesionCaptura",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionCaptura_EjercicioActivoId",
                table: "SesionCaptura",
                column: "EjercicioActivoId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionCaptura_Estado",
                table: "SesionCaptura",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Transcripcion_MediaId",
                table: "Transcripcion",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Transcripcion_SesionId",
                table: "Transcripcion",
                column: "SesionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AclaracionPendiente");

            migrationBuilder.DropTable(
                name: "Invitacion");

            migrationBuilder.DropTable(
                name: "PlanillaFila");

            migrationBuilder.DropTable(
                name: "RegistroParcial");

            migrationBuilder.DropTable(
                name: "Transcripcion");

            migrationBuilder.DropTable(
                name: "PlanillaConfirmada");

            migrationBuilder.DropTable(
                name: "SesionCaptura");

            migrationBuilder.DropTable(
                name: "Alumno");

            migrationBuilder.DropTable(
                name: "Ejercicio");

            migrationBuilder.DropTable(
                name: "Profesor");
        }
    }
}
