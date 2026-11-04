namespace WorkoutAgentic.Api.Data.Entities;

// Pregunta viva de captura: pausa el flujo hasta recibir respuesta
// (regla madre: no inventar). 1:1 con la sesión.
public class AclaracionPendiente
{
    // PK = SesionId: como mucho una aclaración viva por sesión.
    public int SesionId { get; set; }
    public SesionCaptura? Sesion { get; set; }

    // Dato faltante ("peso", "reps", "serie", "ejercicio", "unidad"…); el set
    // canónico lo fija el slice NLU siguiendo el spec.
    public string Campo { get; set; } = string.Empty;

    // Contexto mínimo para resolverla (JSON): {"registroId":N,"crudo":"…"}.
    // Lo demás se re-deriva de forma determinística (parser + CatalogResolver).
    public string? Contexto { get; set; }

    // Reintento actual: tras 2 reintentos sin resolver, la fila queda marcada
    // con RegistroParcial.PendienteAclaracion=true y el flujo continúa.
    public int Reintento { get; set; }

    public DateTime ExpiraUtc { get; set; }
}
