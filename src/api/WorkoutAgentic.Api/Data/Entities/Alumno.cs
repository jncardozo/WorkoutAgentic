namespace WorkoutAgentic.Api.Data.Entities;

// Cuenta del alumno: carga y confirma planillas en la SPA.
public class Alumno
{
    public int Id { get; set; }

    // Nombre visible del alumno.
    public string Nombre { get; set; } = string.Empty;

    // Correo del alta; las credenciales se gestionan en ApplicationUser.
    public string? Email { get; set; }

    // Solo los alumnos ACTIVO pueden acceder a sus entrenamientos.
    public string Estado { get; set; } = "ACTIVO";

    public List<SesionCaptura> Sesiones { get; set; } = [];

    public List<PlanillaConfirmada> PlanillasConfirmadas { get; set; } = [];
}
