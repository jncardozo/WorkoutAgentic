namespace WorkoutAgentic.Api.Data.Entities;

// Snapshot write-once de la planilla confirmada por el alumno.
// Las filas se congelan al confirmar y no se editan en el MVP.
public class PlanillaConfirmada
{
    public int Id { get; set; }

    public int SesionId { get; set; }

    // Sesión de la que nace el snapshot (1:1, la sesión confirmada queda sin efectos).
    public SesionCaptura? Sesion { get; set; }

    public int AlumnoId { get; set; }
    public Alumno? Alumno { get; set; }

    public DateTime ConfirmadaUtc { get; set; }

    // Cuadrícula congelada: es exactamente lo que confirmó el alumno.
    public List<PlanillaFila> Filas { get; set; } = [];
}
