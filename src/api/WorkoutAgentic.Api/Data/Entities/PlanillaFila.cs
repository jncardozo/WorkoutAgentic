namespace WorkoutAgentic.Api.Data.Entities;

// Fila del snapshot confirmado (arquitectura §4): write-once, tabla separada
// de RegistroParcial (regla clave del doc — nunca mezclar).
public class PlanillaFila
{
    // Clave compuesta (PlanillaId, Orden) — configurada en Data/Configurations.
    public int PlanillaId { get; set; }
    public PlanillaConfirmada? Planilla { get; set; }

    public int Orden { get; set; }

    // Nombre canónico congelado en el momento de confirmar (texto, no FK:
    // el snapshot es inmutable aunque el catálogo cambie después).
    public string EjercicioCanonical { get; set; } = string.Empty;

    public int NroSerie { get; set; }

    public int? Reps { get; set; }

    public decimal? PesoKg { get; set; }

    public Unidad? Unidad { get; set; }
}
