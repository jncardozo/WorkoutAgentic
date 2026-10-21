using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Tests.Features.Whatsapp;

// Ventana de 24 h (docs/conventions/whatsapp.md §Ventana): dentro → texto libre;
// en el límite exacto o después → plantilla. Nunca inventamos la interacción:
// si no hay último mensaje del alumno, NO hay texto libre.
public class WhatsappWindowTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SinUltimoMensajeDelAlumno_NoPermiteTextoLibre() =>
        Assert.False(WhatsappWindow.PermiteTextoLibre(Ahora, ultimoMensajeAlumnoUtc: null));

    [Fact]
    public void MensajeReciente_PermiteTextoLibre() =>
        Assert.True(WhatsappWindow.PermiteTextoLibre(Ahora, Ahora.AddHours(-1)));

    [Fact]
    public void UnMinutoAntesDelLimite_PermiteTextoLibre() =>
        Assert.True(WhatsappWindow.PermiteTextoLibre(Ahora, Ahora.AddHours(24).AddSeconds(-1)));

    [Fact]
    public void ExactamenteVeinticuatroHoras_NoPermiteTextoLibre() =>
        Assert.False(WhatsappWindow.PermiteTextoLibre(Ahora, Ahora.AddHours(-24)));

    [Fact]
    public void UnSegundoDespuesDelLimite_NoPermiteTextoLibre() =>
        Assert.False(WhatsappWindow.PermiteTextoLibre(Ahora, Ahora.AddHours(-24).AddSeconds(-1)));

    [Fact]
    public void Duracion_SonVeinticuatroHoras() =>
        Assert.Equal(TimeSpan.FromHours(24), WhatsappWindow.Duracion);
}
