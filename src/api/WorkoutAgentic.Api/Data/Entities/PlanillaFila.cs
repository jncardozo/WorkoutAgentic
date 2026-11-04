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
    // el snapshot es inmutable aunque el catálogo cambie después). Para filas
    // sin ejercicio (descripción libre) congela el texto crudo del alumno.
    public string EjercicioCanonical { get; set; } = string.Empty;

    public int NroSerie { get; set; }

    public int? Reps { get; set; }

    public decimal? PesoKg { get; set; }

    public Unidad? Unidad { get; set; }

    // Segundos de ejercicios por tiempo (plancha, dinámicas); congelado igual
    // que el resto del snapshot. Ver Data/Unidad: Unidad == Tiempo.
    public int? TiempoSeg { get; set; }

    // Marca congelada: la fila entró al snapshot con un dato faltante según su
    // tipo de ejercicio (la vista la marca; regla ADR-0004: la
    // información nunca se pierde en silencio).
    public bool Incompleta { get; set; }
}
