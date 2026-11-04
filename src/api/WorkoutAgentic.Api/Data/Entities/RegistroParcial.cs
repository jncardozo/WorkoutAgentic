namespace WorkoutAgentic.Api.Data.Entities;

// Fila capturada y NO confirmada (glosario): editable/borrable hasta que el
// alumno confirme y se genere el snapshot.
public class RegistroParcial
{
    public int Id { get; set; }

    public int SesionId { get; set; }
    public SesionCaptura? Sesion { get; set; }

    // Relación opcional para filas históricas; la captura nueva usa texto libre.
    public int? EjercicioId { get; set; }
    public Ejercicio? Ejercicio { get; set; }

    // Frase literal tal como la dijo el alumno (auditoría).
    public string? EjercicioCrudo { get; set; }

    // Nombre libre normalizado por NLU; no depende de un catálogo global.
    public string? EjercicioCanonico { get; set; }

    // true cuando el texto crudo queda como descripción libre (sin match).
    public bool DescripcionPendiente { get; set; }

    public int NroSerie { get; set; }

    public int? Reps { get; set; }

    public decimal? PesoKg { get; set; }

    // NULL = todavía no se definió (aclaración en curso); ver Unidad para los valores.
    public Unidad? Unidad { get; set; }

    // Segundos de ejercicios por tiempo (plancha, dinámicas): unidad = Tiempo.
    public int? TiempoSeg { get; set; }

    public bool PendienteAclaracion { get; set; }

    public DateTime CreadoUtc { get; set; }
}
