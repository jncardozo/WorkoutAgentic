namespace WorkoutAgentic.Api.Data.Entities;

// Cuenta de profesor (glosario): consulta planillas confirmadas de sus alumnos,
// solo lectura. Login SPA por email (S6).
public class Profesor
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    // Email de login de la SPA (S6): único.
    public string Email { get; set; } = string.Empty;

    // Hoy el único rol de acceso de esta tabla es PROFESOR (S6); el campo se
    // persiste para mantener el modelo descripto en arquitectura.md §4.
    public string Rol { get; set; } = "PROFESOR";

    public List<Invitacion> Invitaciones { get; set; } = [];

    public List<Alumno> Alumnos { get; set; } = [];
}
