namespace WorkoutAgentic.Api.Data.Entities;

// Cuenta ALUMNO (glosario): carga por WhatsApp y confirma planillas en la SPA.
public class Alumno
{
    public int Id { get; set; }

    // Vinculación con el profesor (nullable hasta aceptar la invitación).
    public int? ProfesorId { get; set; }
    public Profesor? Profesor { get; set; }

    // Teléfono canónico de WhatsApp (internacional, sin +): único.
    public string TelefonoWa { get; set; } = string.Empty;

    // Email de login SPA (S6), si ya existe.
    public string? Email { get; set; }

    // Estado de la cuenta; el slice AuthApi (S6) define el set de valores.
    public string Estado { get; set; } = "ACTIVO";

    public List<SesionCaptura> Sesiones { get; set; } = [];

    public List<PlanillaConfirmada> PlanillasConfirmadas { get; set; } = [];
}
