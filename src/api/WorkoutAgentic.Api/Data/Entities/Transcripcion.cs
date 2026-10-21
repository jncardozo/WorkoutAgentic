namespace WorkoutAgentic.Api.Data.Entities;

// Auditoría del STT (glosario): se guarda el texto crudo para rastrear desvíos.
public class Transcripcion
{
    public int Id { get; set; }

    public int SesionId { get; set; }
    public SesionCaptura? Sesion { get; set; }

    // Media ID de Graph. NUNCA la URL audio.link (temporal) — docs/conventions/whatsapp.md.
    public string MediaId { get; set; } = string.Empty;

    public string TextoCrudo { get; set; } = string.Empty;

    // Proveedor STT (Gemini para el MVP, ADR S1).
    public string Proveedor { get; set; } = string.Empty;

    public long? DuracionSeg { get; set; }

    public DateTime RecibidoUtc { get; set; }
}
