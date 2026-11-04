using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Respuestas del bot al alumno (reglas de negocio §5): español neutro cercano,
// SIN tecnicismos del pipeline (nada de intents, JSON ni ids internos). Los
// datos faltantes nunca se muestran como si existieran.
internal static class TextosBot
{
    private const string EjemploDatoSerie = "\"primera serie 12 repeticiones con 20 kilos\"";

    // Paso 1 (ADR-0013): el dictado arma filas con ejercicio + series SOLAMENTE;
    // luego hay que completar reps y peso de cada serie antes de seguir.
    public static string SeriesAnotadasBase(string nombre, int desde, int hasta)
    {
        if (desde == hasta)
        {
            return $"Serie {desde} de {nombre} anotada. Completá repeticiones y peso de esa serie antes de cargar otro ejercicio: {EjemploDatoSerie}.";
        }

        return $"Anotadas {hasta - desde + 1} series de {nombre} ({desde} a {hasta}). Completá repeticiones y peso de cada serie antes de cargar otro ejercicio: {EjemploDatoSerie}.";
    }

    // Paso 2 (ADR-0014): el dato de UNA serie del ejercicio que está cargando.
    public static string SerieConDatos(string nombre, int nro, string detalle) =>
        $"Serie {nro} de {nombre} anotada: {detalle}.";

    // Detalle legible de la serie (sin tecnicismos ni ceros inventados).
    public static string DetalleSerie(int? reps, decimal? pesoKg, Unidad? unidad)
    {
        var partes = new List<string>();

        if (reps is { } dichas)
        {
            partes.Add($"{dichas} reps");
        }

        if (unidad == Unidad.PesoCorporal)
        {
            partes.Add("al cuerpo");
        }
        else if (pesoKg is { } valor)
        {
            partes.Add($"con {valor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} kg");
        }

        return partes.Count > 0 ? string.Join(' ', partes) : "sin dato cargado";
    }

    // La serie que nombró el alumno no existe en la cuadrícula: aviso corto,
    // sin inventar filas (el alta manual vive en la app).
    public static string SerieSinFila(string nombre, int nro) =>
        $"No veo la serie {nro} de {nombre} en la cuadrícula. Fijate cuántas series dictaste, o agregá la fila desde la app (\"+ Agregar fila\").";

    // Dictado de datos aplicado con la pregunta del ejercicio viva: la
    // respuesta del dato más el recordatorio de que la pregunta sigue viva.
    public static string ConRecordatorioPendiente(string respuestaDato) =>
        $"{respuestaDato}\nY me sigue faltando aclarar la variante del ejercicio de ese lote.";

    // Guía de la regla (ADR-0013 + ADR-0014): cualquier dictado fuera de las
    // dos reglas responde esto y no toca el estado ni usa el ejercicio activo.
    public static string FueraDeRegla =>
        "Para anotar tus series, decime en un solo mensaje el ejercicio y las series. Ej.: \"press de banca 4 series\". Después pasale el dato a cada una: \"primera serie 12 repeticiones con 20 kilos\".";

    // Anota solo el ejercicio (rama defensiva: aclaración sin filas asociadas).
    public static string EjercicioAnotado(string canonico) =>
        $"Anotado: {canonico}.";

    public static string PreguntaEjercicio(string? crudo, IReadOnlyList<string> sugerencias)
    {
        return PreguntaEjercicioAbierta(null);
    }

    public static string PreguntaEjercicioAmbiguo(IReadOnlyList<string> sugerencias) =>
        PreguntaEjercicioAbierta(null);

    public static string PreguntaEjercicioAbierta(string? pregunta) =>
        string.IsNullOrWhiteSpace(pregunta)
            ? "¿Qué variante del ejercicio hiciste? Decime el nombre con tus palabras."
            : pregunta.Trim();

    public static string EjercicioComoTexto(string crudo) =>
        $"Anotado como texto: \"{crudo}\". Tu profe lo va a reconocer en la planilla.";

    public static string Reintento(string pregunta) =>
        $"No entendí ese dato. {pregunta}";

    public static string Cierre =>
        "¡Listo! Cerré tu carga. Revisá la planilla y confirmala en la app; después la ve tu profe.";

    public static string PlanillaYaLista =>
        "Tu planilla ya está armada. Confirmala en la app, o decí \"seguir\" para seguir cargando.";

    public static string Pausada =>
        "Por inactividad pausé la carga anterior (lo anotado quedó guardado). Escribí \"seguir\" para continuarla.";

    public static string Seguimos =>
        "Dale, seguimos 💪 Mandame la serie y la cargamos.";

    public static string SeguimosConPendiente =>
        "Dale, seguimos 💪 Y volvamos a lo pendiente: cuando quieras me pasás el dato que falte.";

    public static string Cortesia =>
        "Acá anoto tus series 🙂 Probame con: \"press de banca 4 series\", \"primera serie 12 con 20\" o \"terminé\" cuando cierres.";

    public static string Ayuda =>
        "Te anoto tu entrenamiento por acá. La carga va en dos pasos.\n" +
        "• PASO 1 · ejercicio + series en un mismo mensaje: \"press de banca 4 series\" (arma 4 filas) o \"sillón, segunda serie\".\n" +
        "• PASO 2 · completá repeticiones y peso (o \"al cuerpo\") de cada serie: \"primera serie 12 repeticiones con 20 kilos\". No podés cargar otro ejercicio hasta completar todas.\n" +
        "• \"terminé\" cierra la carga; \"seguir\" la retoma después de una pausa.";

    public static string FalloLectura =>
        "No pude leer bien esos datos 🙂 ¿Me los pasás de nuevo?";

    public static string DespedirSeriePendiente =>
        "La dejé marcada para completar después y seguimos con lo próximo.";

}
