namespace WorkoutAgentic.Api.Data.Entities;

// Invitación del profesor (S6): código corto que vincula a un alumno.
public class Invitacion
{
    public int Id { get; set; }

    // Longitud canónica sin definir aún (arquitectura §4 habla de 8 chars,
    // ADR S6 de "código corto"); la fija el slice AuthApi.
    public string Codigo { get; set; } = string.Empty;

    public int ProfesorId { get; set; }
    public Profesor? Profesor { get; set; }

    // El alumno queda asignado cuando acepta la invitación.
    public int? AlumnoId { get; set; }
    public Alumno? Alumno { get; set; }

    public DateTime ExpiraUtc { get; set; }

    public DateTime? UsadaUtc { get; set; }
}
