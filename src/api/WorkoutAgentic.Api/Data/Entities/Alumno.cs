namespace WorkoutAgentic.Api.Data.Entities;

// Cuenta ALUMNO (glosario): carga en la PWA (o por WhatsApp si el canal
// volviera) y confirma planillas en la SPA.
public class Alumno
{
    public int Id { get; set; }

    // Vinculación con el profesor (nullable hasta aceptar la invitación).
    public int? ProfesorId { get; set; }
    public Profesor? Profesor { get; set; }

    // Nombre de pila para las listas del profesor (alta explícita, ADR-0010).
    public string Nombre { get; set; } = string.Empty;

    // Teléfono canónico de WhatsApp (internacional, sin +), ÚNICO cuando
    // existe: desde el pivot es opcional (el alta explícita no lo exige).
    public string? TelefonoWa { get; set; }

    // Email de login SPA (S6), si ya existe.
    public string? Email { get; set; }

    // Estado de la cuenta; el slice AuthApi (S6) define el set de valores.
    public string Estado { get; set; } = "ACTIVO";

    public List<SesionCaptura> Sesiones { get; set; } = [];

    public List<PlanillaConfirmada> PlanillasConfirmadas { get; set; } = [];
}
