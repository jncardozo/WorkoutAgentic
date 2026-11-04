namespace WorkoutAgentic.Api.Data.Entities;

// Cuenta del alumno: carga y confirma planillas en la SPA.
public class Alumno
{
    public int Id { get; set; }

    // Nombre visible del alumno.
    public string Nombre { get; set; } = string.Empty;

    // Email de login SPA (S6), si ya existe.
    public string? Email { get; set; }

    // Estado de la cuenta; el slice AuthApi (S6) define el set de valores.
    public string Estado { get; set; } = "ACTIVO";

    public List<SesionCaptura> Sesiones { get; set; } = [];

    public List<PlanillaConfirmada> PlanillasConfirmadas { get; set; } = [];
}
