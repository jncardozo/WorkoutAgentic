namespace WorkoutAgentic.Api.Features.Whatsapp;

// Ventana de 24 horas (docs/conventions/whatsapp.md): dentro de las 24 h del
// último mensaje del usuario se puede enviar texto/media libre; fuera, solo
// plantillas aprobadas por Meta. La decisión final la toma el flujo de captura.
public static class WhatsappWindow
{
    public static readonly TimeSpan Duracion = TimeSpan.FromHours(24);

    // Si el alumno no escribió o su último mensaje tiene 24 h o más, el flujo
    // DEBE usar plantilla en lugar de texto libre.
    public static bool PermiteTextoLibre(
        DateTimeOffset ahoraUtc,
        DateTimeOffset? ultimoMensajeAlumnoUtc) =>
        ultimoMensajeAlumnoUtc is { } ultimo
        && ahoraUtc - ultimo < Duracion;
}
