namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Respuestas del bot al alumno (reglas de negocio §5): español neutro cercano,
// SIN tecnicismos del pipeline (nada de intents, JSON ni ids internos). Los
// datos faltantes nunca se muestran como si existieran.
internal static class TextosBot
{
    // Alta "de palabra": en ejercicios por tiempo se pregunta por los segundos,
    // no por reps/peso (el tipo de ejercicio manda; business-rules §2).
    public static string EjercicioAnotado(string canonico, bool porTiempo = false) =>
        porTiempo
            ? $"Anotado: {canonico}. ¿Cuántos segundos aguantaste en la serie?"
            : $"Anotado: {canonico}. Ahora pasame la serie: reps y peso (ej.: \"12 con 45\").";

    public static string SeriesAnotadas(string canonico, int desde, int hasta, string detalle)
    {
        if (desde == hasta)
        {
            return $"Serie {desde} de {canonico} anotada: {detalle}. ¿Siguiente serie? \"terminé\" cuando cierres.";
        }

        return $"Anotadas {hasta - desde + 1} series de {canonico} ({desde} a {hasta}): {detalle}. \"terminé\" cuando cierres.";
    }

    public static string RegistroConfirmado(string detalle) =>
        $"Listo: {detalle}. ¿Siguiente serie? \"terminé\" cuando cierres.";

    public static string PreguntaPeso(string canonico) =>
        $"¿Con cuánto peso tomaste esa serie de {canonico}?";

    public static string PreguntaReps(string canonico) =>
        $"¿Cuántas repeticiones fueron en esa serie de {canonico}?";

    public static string PreguntaTiempo(string canonico) =>
        $"¿Cuántos segundos fueron en esa serie de {canonico}? (min y segundos, ej.: \"1 y medio\" = 90 s)";

    public static string PreguntaEjercicio(string? crudo, IReadOnlyList<string> sugerencias)
    {
        if (sugerencias.Count == 0)
        {
            return "¿De qué ejercicio es esa serie? (ej.: \"prensa\")";
        }

        var opciones = OpcionesEnumeradas(sugerencias);
        if (crudo is null)
        {
            return $"¿De qué ejercicio se trata: {opciones}? Mandame el número, el nombre u \"otro\".";
        }

        return $"\"{crudo}\" no lo tengo en el catálogo. ¿Te referís a: {opciones}? Mandame el número, el nombre u \"otro\".";
    }

    public static string PreguntaEjercicioAmbiguo(IReadOnlyList<string> sugerencias) =>
        $"¿A cuál te referís: {OpcionesEnumeradas(sugerencias)}? Mandame el número, el nombre u \"otro\".";

    public static string EjercicioComoTexto(string crudo) =>
        $"Anotado como texto: \"{crudo}\". Tu profe lo va a reconocer en la planilla.";

    public static string Reintento(string pregunta) =>
        $"No entendí ese dato. {pregunta}";

    public static string Cierre =>
        "¡Listo! Cerré tu carga. Revisá la planilla y confirmala en la app; después la ve tu profe.";

    public static string PlanillaYaLista =>
        "Tu planilla ya está armada. Revisala y confirmala en la app.";

    public static string Pausada =>
        "Por inactividad pausé la carga anterior (lo anotado quedó guardado). Escribí \"seguir\" para continuarla.";

    public static string Seguimos =>
        "Dale, seguimos 💪 Mandame la serie y la cargamos.";

    public static string SeguimosConPendiente =>
        "Dale, seguimos 💪 Y volvamos a lo pendiente: cuando quieras me pasás el dato que falte.";

    public static string Cortesia =>
        "Acá anoto tus series 🙂 Probame con: \"sillón 12 con 45\", \"prensa 3x12 con 60\" o \"terminé\" cuando cierres.";

    public static string Ayuda =>
        "Te anoto tu entrenamiento por acá. Ejemplos:\n" +
        "• \"sillón primera serie 12 con 45\"\n" +
        "• \"prensa 3x12 con 60\" (tres series)\n" +
        "• \"segunda serie, 10 con 40\" (usa el ejercicio activo)\n" +
        "• \"plancha 1 minuto\", \"dominadas 10 al cuerpo\"\n" +
        "• \"terminé\" cierra la carga; \"seguir\" la retoma después de una pausa.";

    public static string FalloLectura =>
        "No pude leer bien esos datos 🙂 ¿Me los pasás de nuevo?";

    public static string DespedirSeriePendiente =>
        "La dejé marcada para completar después y seguimos con lo próximo.";

    private static string OpcionesEnumeradas(IReadOnlyList<string> nombres) =>
        string.Join(" ", nombres.Select((n, i) => $"{i + 1}) {n}"));
}
