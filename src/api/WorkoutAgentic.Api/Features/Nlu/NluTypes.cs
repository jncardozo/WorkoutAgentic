using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Features.Nlu;

// ─── Intent spec NLU §1 (nombres del spec; representación del dominio) ───
public enum IntentoCaptura
{
    CargaEjercicio,
    CargaSerie,
    Cierre,
    AyudaConsulta,
    Invalido,
}

// Etapa 2: clasificación de intención. Exito=false → el worker deriva a
// aclaración/error de etapa (nunca a un intent inventado).
public sealed record ClasificacionIntent(
    bool Exito,
    IntentoCaptura Intento,
    double? Confianza,
    string? ErrorTitulo)
{
    public static ClasificacionIntent Ok(IntentoCaptura intento, double? confianza) =>
        new(Exito: true, Intento: intento, Confianza: confianza, ErrorTitulo: null);

    public static ClasificacionIntent Fallo(string titulo) =>
        new(Exito: false, Intento: IntentoCaptura.Invalido, Confianza: null, ErrorTitulo: titulo);
}

// Etapa 3: entidades normalizadas del spec §2. TODO nullable por diseño:
// regla madre "no inventar" — el null significa "el alumno no lo dijo".
public sealed record EntidadesNlu(
    string? EjercicioTexto,
    int? SerieOrdinal,
    int? CantidadSeries,
    int? Reps,
    decimal? PesoKg,
    Unidad? Unidad,
    int? TiempoSegundos,
    int? DescansoSegundos,
    string? CampoAclarar,
    string? PreguntaAclarar);

public sealed record EntidadesExtraidas(
    bool Exito,
    EntidadesNlu? Entidades,
    string? ErrorTitulo)
{
    public static EntidadesExtraidas Ok(EntidadesNlu entidades) => new(true, entidades, null);

    public static EntidadesExtraidas Fallo(string titulo) => new(false, null, titulo);
}

// Etapa 4: análisis completo de un mensaje, producto del pipeline
// (auditable: transcripción + clasificación + entidades + resolución).
public sealed record AnalisisCaptura(
    WhatsappMessageKind MensajeKind,
    string TextoProcesado,
    TranscripcionAudio? Transcripcion,
    ClasificacionIntent Clasificacion,
    EntidadesNlu? Entidades,
    ResultadoResolucionEjercicio? ResolucionEjercicio);
