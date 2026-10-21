namespace WorkoutAgentic.Api.Data.Entities;

// Fila capturada y NO confirmada (glosario): editable/borrable hasta que el
// alumno confirme y se genere el snapshot. NUNCA sale así al profesor.
public class RegistroParcial
{
    public int Id { get; set; }

    public int SesionId { get; set; }
    public SesionCaptura? Sesion { get; set; }

    // El LLM desarrolla texto crudo; el CatalogResolver (determinístico) asigna
    // el id — nunca el modelo lo devuelve directo (ADR S2).
    public int? EjercicioId { get; set; }
    public Ejercicio? Ejercicio { get; set; }

    // Texto tal como lo dijo el alumno.
    public string? EjercicioCrudo { get; set; }

    // true cuando el texto crudo queda como descripción libre (sin match).
    public bool DescripcionPendiente { get; set; }

    public int NroSerie { get; set; }

    public int? Reps { get; set; }

    public decimal? PesoKg { get; set; }

    // NULL = todavía no se definió (aclaración en curso); ver Unidad para los valores.
    public Unidad? Unidad { get; set; }

    public bool PendienteAclaracion { get; set; }

    public DateTime CreadoUtc { get; set; }
}
