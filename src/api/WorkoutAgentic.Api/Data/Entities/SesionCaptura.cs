namespace WorkoutAgentic.Api.Data.Entities;

// Estados del diálogo (arquitectura §3 y spec NLU §5). Se persiste como texto.
public enum EstadoSesionCaptura
{
    EnCurso,
    EsperandoAclaracion,
    Lista,
    Confirmada,
    Expirada,
}

// Máquina de estados del diálogo de captura: vive en la BD para sobrevivir
// reinicios y atenderse desde cualquier instancia (arquitectura §3).
public class SesionCaptura
{
    public int Id { get; set; }

    public int AlumnoId { get; set; }
    public Alumno? Alumno { get; set; }

    public EstadoSesionCaptura Estado { get; set; } = EstadoSesionCaptura.EnCurso;

    // El ejercicio activo recibe las cargas sueltas: "segunda serie, 10 con 40".
    public int? EjercicioActivoId { get; set; }
    public Ejercicio? EjercicioActivo { get; set; }

    public int OrdenSerieSiguiente { get; set; } = 1;

    public DateTime UltimaActividadUtc { get; set; }

    // Concurrencia de escritura (ADR S3): cabecera para evitar doble aplicación
    // de mensajes casi simultáneos del mismo alumno; el check se activa con el
    // slice del pipeline NLU.
    public long RowVersion { get; set; }

    public List<RegistroParcial> Registros { get; set; } = [];

    // Aclaración viva: 0 o 1 por sesión (1:1).
    public AclaracionPendiente? AclaracionPendiente { get; set; }

    // Snapshot después de la confirmación (1:1 con la sesión).
    public PlanillaConfirmada? PlanillaConfirmada { get; set; }
}
