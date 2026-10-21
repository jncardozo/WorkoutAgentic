namespace WorkoutAgentic.Api.Features.Nlu;

// Resultado tipado de la transcripción (ADR-0002 S1: interfaz para poder
// cambiar de proveedor sin tocar el pipeline).
public sealed record TranscripcionAudio(
    bool Exito,
    string? Texto,
    string? ErrorTitulo,
    string? ErrorDetails)
{
    public static TranscripcionAudio Ok(string texto) =>
        new(Exito: true, Texto: texto, ErrorTitulo: null, ErrorDetails: null);

    public static TranscripcionAudio Fallo(string titulo, string? detalle) =>
        new(Exito: false, Texto: null, ErrorTitulo: titulo, ErrorDetails: detalle);
}

// Etapa 1 del pipeline: audio → texto en español. Los errores son del tipo
// de dominio; el flujo deriva a pedir reenvío, JAMÁS a adivinar.
public interface ISpeechToText
{
    Task<TranscripcionAudio> TranscribirAsync(byte[] audio, string mimeType, CancellationToken cancellationToken);
}
