namespace WorkoutAgentic.Api.Features.WhatsappMedia;

// Resultado tipado de la descarga (docs/conventions/dotnet.md: tipos de
// resultado, no excepciones). NUNCA se expone audio.link: solo baja los bytes.
public sealed record WhatsappAudioArchivo(
    bool Exito,
    byte[]? Bytes,
    string? MimeType,
    long? TamanoBytes,
    string? ErrorTitulo,
    string? ErrorDetails,
    bool Retriable)
{
    public static WhatsappAudioArchivo Ok(byte[] bytes, string mimeType, long? tamanoBytes) =>
        new(Exito: true, Bytes: bytes, MimeType: mimeType, TamanoBytes: tamanoBytes,
            ErrorTitulo: null, ErrorDetails: null, Retriable: false);

    public static WhatsappAudioArchivo Fallo(string titulo, string detalles, bool retriable) =>
        new(Exito: false, Bytes: null, MimeType: null, TamanoBytes: null,
            ErrorTitulo: titulo, ErrorDetails: detalles, Retriable: retriable);
}

// Descarga la nota de voz por Media ID de Graph API (docs/conventions/whatsapp.md):
// GET /{media_id} → JSON con la url temporal → GET url con Bearer (2 pasos).
public interface IWhatsappAudioDownloader
{
    Task<WhatsappAudioArchivo> DescargarAsync(string mediaId, CancellationToken cancellationToken);
}
