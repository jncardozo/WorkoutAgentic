using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Etapa 4 del pipeline del Voice Tracker (spec NLU §5): aplica el análisis
// sobre el estado persistido de SesionCaptura y produce la respuesta del bot.
// Reglas de dominio en docs/domain/business-rules.md; decisiones del slice en
// ADR-0004. Regla madre: si falta o es ambiguo → aclaración, jamás inventado.
//
// Regla del dictado (ADR-0013 + ADR-0014): el alumno carga en DOS pasos.
// PASO 1 "EJERCICIO + SERIES" (ej.: "press de banca 4 series", o "sillón,
// segunda serie" puntual) crea filas con ejercicio y número de serie SOLAMENTE.
// PASO 2 "NRO DE SERIE + REPS y/o PESO" (ej.: "primera serie 12 repeticiones
// con 20 kilos", sin nombrar ejercicio) llena la fila de ese ordinal del
// EJERCICIO ACTIVO de la sesión. Lo dictado fuera de cualquiera de las dos
// reglas responde la guía y NO toca el estado: nunca se inventa un dato.
internal sealed class SesionCapturaServicio(
    CapturasDbContext db,
    ILogger<SesionCapturaServicio> logger) : ISesionCapturaServicio
{
    // [SUPUESTO ADR-0004]: pausa automática tras 2 h sin actividad.
    internal static readonly TimeSpan InactividadMaxima = TimeSpan.FromHours(2);

    // Vida útil de la pregunta viva; caducada, solo "seguir" reanuda el flujo.
    internal static readonly TimeSpan VidaAclaracion = TimeSpan.FromHours(24);

    // Una libra avoirdupois = 0,45359237 kg exactos (conversión determinística).
    private const decimal LibrasPorKilo = 0.45359237m;

    // Tope de series por dictado (ADR-0013): un lote mayorsale solo puede
    // ser un ruido de voz; fuera del tope se responde la guía, sin crear
    // filas ni interpretar el número. [SUPUESTO: 30 cubre cualquier sesión.]
    internal const int MaxSeriesLote = 30;

    // Sesiones que atienden mensajes: Confirmada no; Lista admite cargas
    // nuevas y "seguir" (reabren, ADR-0012) y el resto del diálogo pide
    // confirmar en la SPA.
    private static readonly EstadoSesionCaptura[] EstadosOperables =
    [
        EstadoSesionCaptura.EnCurso,
        EstadoSesionCaptura.EsperandoAclaracion,
        EstadoSesionCaptura.Expirada,
        EstadoSesionCaptura.Lista,
    ];

    public async Task<CapturaRespuesta> AplicarTextoAsync(
        int alumnoId,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos.SingleOrDefaultAsync(a => a.Id == alumnoId, cancellationToken)
                     ?? throw new InvalidOperationException(
                         $"Alumno {alumnoId} inexistente: el borde valida 404 antes de aplicar.");
        return await NucleoAsync(alumno, analisis, ahora, cancellationToken);
    }

    // Núcleo de identificado de sesión + resolución de aclaraciones + intent.
    private async Task<CapturaRespuesta> NucleoAsync(
        Alumno alumno,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var sesion = await SesionRecienteAsync(alumno.Id, cancellationToken);

        if (sesion is null)
        {
            sesion = new SesionCaptura
            {
                AlumnoId = alumno.Id,
                Estado = EstadoSesionCaptura.EnCurso,
                UltimaActividadUtc = ahora,
            };
            db.SesionesCaptura.Add(sesion);
            await db.SaveChangesAsync(cancellationToken); // la cabecera hace falta con Id antes de las filas
            logger.LogInformation("Sesión de captura nueva para el alumno {Alumno}.", alumno.Id);
        }
        else if (sesion.Estado == EstadoSesionCaptura.Lista)
        {
            // Carga cerrada que espera confirmación en la SPA. "seguir" la
            // REABRE (ADR-0012), y un dictado que cumple la regla (ejercicio +
            // series, ADR-0013) la reabre y el flujo sigue abajo; el resto del
            // diálogo (guía, ayuda, cortesía) pide confirmar sin tocar estado.
            if (ParseadorAclaraciones.EsSeguir(analisis.TextoProcesado))
            {
                sesion.Estado = sesion.AclaracionPendiente is null
                    ? EstadoSesionCaptura.EnCurso
                    : EstadoSesionCaptura.EsperandoAclaracion;
                sesion.UltimaActividadUtc = ahora;
                sesion.RowVersion++;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Sesión {Id} reabierta por el alumno (estaba Lista).", sesion.Id);
                return new(
                    sesion.AclaracionPendiente is null
                        ? TextosBot.Seguimos
                        : TextosBot.SeguimosConPendiente,
                    sesion.Id,
                    AclaracionCreada: false);
            }

            if (!EsCapturaBase(analisis))
            {
                // Dictado de carga fuera de la regla → el bot enseña la regla
                // (sin abrir la planilla); ayuda, cortesía e inválidos solo
                // recuerdan el paso por la SPA. El análisis fallido no decide
                // por el alumno (ADR-0012/0013).
                return new(
                    EsIntentoDeCarga(analisis) ? TextosBot.FueraDeRegla : TextosBot.PlanillaYaLista,
                    sesion.Id,
                    AclaracionCreada: false);
            }

            // Dictado en regla sobre una carga cerrada: reabre y el flujo
            // sigue abajo (el próximo cierre vuelve a dejarla en Lista).
            sesion.Estado = EstadoSesionCaptura.EnCurso;
            logger.LogInformation(
                "Sesión {Id} reabierta por una captura de carga del alumno {Alumno}.",
                sesion.Id, alumno.Id);
        }
        else if (sesion.Estado == EstadoSesionCaptura.Expirada || ahora - sesion.UltimaActividadUtc > InactividadMaxima)
        {
            // Pausa por inactividad persistida: solo "seguir" reanuda, conservando
            // la pregunta viva que hubiera quedado (spec NLU §5).
            sesion.Estado = EstadoSesionCaptura.Expirada;
            await db.SaveChangesAsync(cancellationToken);

            if (ParseadorAclaraciones.EsSeguir(analisis.TextoProcesado))
            {
                sesion.Estado = sesion.AclaracionPendiente is null
                    ? EstadoSesionCaptura.EnCurso
                    : EstadoSesionCaptura.EsperandoAclaracion;
                sesion.UltimaActividadUtc = ahora;
                sesion.RowVersion++;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Sesión {Id} reanudada por el alumno.", sesion.Id);
                return new(
                    sesion.AclaracionPendiente is null ? TextosBot.Seguimos : TextosBot.SeguimosConPendiente,
                    sesion.Id,
                    AclaracionCreada: false);
            }

            logger.LogInformation("Sesión {Id} pausada por inactividad del alumno {Alumno}.", sesion.Id, alumno.Id);
            return new(TextosBot.Pausada, sesion.Id, AclaracionCreada: false);
        }

        sesion.UltimaActividadUtc = ahora;
        sesion.RowVersion++; // columna simple, no token EF: el worker serializa (ADR-0004)

        CapturaRespuesta respuesta;
        if (sesion.AclaracionPendiente is { } aclaracion)
        {
            respuesta = await ResolverPendienteOIntentarAsync(sesion, aclaracion, analisis, cancellationToken);
        }
        else
        {
            respuesta = await ProcesarIntentoAsync(sesion, analisis, ahora, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return respuesta;
    }

    // ¿El mensaje es un intento de carga (llámetese ejercicio o series)? Solo
    // sirve para elegir la respuesta en la sesión Lista; la creación de filas
    // exige además la regla completa (EsCapturaBase).
    private static bool EsIntentoDeCarga(AnalisisCaptura analisis) =>
        analisis.Clasificacion is { Exito: true, Intento: IntentoCaptura.CargaEjercicio or IntentoCaptura.CargaSerie };

    // ¿El dictado carga el DATO de una serie ya creada (regla 2, ADR-0014)?
    // "NRO DE SERIE + REPS y/o PESO" SIN nombrar ejercicio: la fila es la de
    // ese ordinal del EJERCICIO ACTIVO (el alumno ya la armó en el paso 1,
    // ADR-0013). Exige ordinal y al menos un dato hablado: un ordinal suelto
    // o un mensaje con ejercicio nombrado pertenecen a la regla base.
    private static bool EsCargaDatosSerie(AnalisisCaptura analisis)
    {
        if (!EsIntentoDeCarga(analisis)
            || analisis.Entidades is not { } entidades
            || entidades.EjercicioTexto is { Length: > 0 }
            || entidades.SerieOrdinal is not >= 1)
        {
            return false;
        }

        return entidades.Reps is not null
            || entidades.PesoKg is not null
            || entidades.Unidad == Unidad.PesoCorporal;
    }

    // ¿El dictado cumple la REGLA BASE (ADR-0013)? Un ejercicio nombrado
    // por el alumno (crudo extraído del mensaje o resuelto por el pipeline) +
    // una cantidad de series razonable, o una serie puntual. Es la única
    // puerta por la que nacen filas y se reabre una carga cerrada: fuera de
    // la regla solo responde la guía, sin usar el ejercicio activo.
    private static bool EsCapturaBase(AnalisisCaptura analisis)
    {
        if (!EsIntentoDeCarga(analisis)
            || analisis.Entidades?.EjercicioTexto is not { Length: > 0 })
        {
            return false;
        }

        var cantidad = analisis.Entidades!.CantidadSeries;
        var ordinal = analisis.Entidades.SerieOrdinal;
        return (cantidad is >= 1 and <= MaxSeriesLote) || ordinal >= 1;
    }

    // ─── Cuenta y sesión ────────────────────────────────────────────────────────

    private Task<SesionCaptura?> SesionRecienteAsync(int alumnoId, CancellationToken cancellationToken) =>
        db.SesionesCaptura
            .Include(s => s.AclaracionPendiente)
            .Where(s => s.AlumnoId == alumnoId && EstadosOperables.Contains(s.Estado))
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // ─── Aclaración pendiente vs intents ───────────────────────────────────────

    private async Task<CapturaRespuesta> ResolverPendienteOIntentarAsync(
        SesionCaptura sesion,
        AclaracionPendiente aclaracion,
        AnalisisCaptura analisis,
        CancellationToken cancellationToken)
    {
        // CIERRE domina: abandona lo pendiente (UX; ADR-0004) y cierra en Lista.
        if (analisis.Clasificacion is { Exito: true, Intento: IntentoCaptura.Cierre })
        {
            await AbandonarPendienteAsync(sesion, cancellationToken);
            sesion.Estado = EstadoSesionCaptura.Lista;
            return new(TextosBot.Cierre, sesion.Id, AclaracionCreada: false);
        }

        // Análisis FALLIDO del LLM (cuota, red, salida no parseable): el
        // mensaje puede ser perfectamente válido, solo no se pudo leer. NO
        // cuenta reintento ni abandona el pendiente: quemar un intento por
        // una falla de infraestructura condena el flujo (bug visto en campo,
        // 2026-10-23: 2 mensajes válidos + Gemini 429 ⇒ pendiente abandonada).
        if (!analisis.Clasificacion.Exito)
        {
            logger.LogInformation(
                "NLU caído con aclaración {SesionId}/{Campo} viva: se pide reenvío sin reintento.",
                sesion.Id, aclaracion.Campo);
            return new(TextosBot.FalloLectura, sesion.Id, AclaracionCreada: false);
        }

        // Dictado de datos de serie (regla 2, ADR-0014) con la pregunta del
        // ejercicio viva: se APLICA el dato sobre el lote vivo y la pregunta
        // SIGUE viva (decisión del producto, 2026-10-23): el alumno no tiene
        // por qué respondela antes de seguir dictando. Sin reintento: el dato
        // no es una respuesta (errónea) a la pregunta.
        if (aclaracion.Campo == "ejercicio" && EsCargaDatosSerie(analisis))
        {
            var respuestaDato = await AplicarDatosSerieAsync(sesion, analisis, cancellationToken);
            return new(
                TextosBot.ConRecordatorioPendiente(respuestaDato.MensajeAlumno),
                sesion.Id,
                AclaracionCreada: false);
        }

        var resuelta = await IntentarResolverAsync(sesion, aclaracion, analisis, cancellationToken);
        if (resuelta is { } respuesta)
        {
            return respuesta;
        }

        // Respuesta no legible: reintento; al segundo, la fila queda marcada
        // pendiente y el flujo sigue (spec NLU §4).
        aclaracion.Reintento++;
        if (aclaracion.Reintento < 2)
        {
            return new(
                TextosBot.Reintento(await PreguntaReintentoAsync(aclaracion, cancellationToken)),
                sesion.Id,
                AclaracionCreada: false);
        }

        logger.LogInformation("Aclaración {SesionId}/{Campo} abandonada tras 2 reintentos.", sesion.Id, aclaracion.Campo);
        await AbandonarPendienteAsync(sesion, cancellationToken);
        sesion.Estado = EstadoSesionCaptura.EnCurso;
        return new(TextosBot.DespedirSeriePendiente, sesion.Id, AclaracionCreada: false);
    }

    // Único tipo de aclaración viva (ADR-0013): el ejercicio. La respuesta del
    // alumno se interpreta determinísticamente sobre el texto crudo, SIN LLM;
    // si nada matchea → null = reintento (jamás un dato inventado).
    private async Task<CapturaRespuesta?> IntentarResolverAsync(
        SesionCaptura sesion,
        AclaracionPendiente aclaracion,
        AnalisisCaptura analisis,
        CancellationToken cancellationToken)
    {
        return aclaracion.Campo switch
        {
            "ejercicio" => await ResolverEjercicioAclaracionAsync(sesion, aclaracion, analisis, cancellationToken),

            // Campo no previsto por la regla (pendientes previos al cambio,
            // p. ej. de peso): reintento; nada inventado ni "rescatado".
            _ => null,
        };
    }

    private async Task<CapturaRespuesta?> ResolverEjercicioAclaracionAsync(
        SesionCaptura sesion,
        AclaracionPendiente aclaracion,
        AnalisisCaptura analisis,
        CancellationToken cancellationToken)
    {
        var texto = analisis.TextoProcesado;
        var contexto = ParsearContexto(aclaracion.Contexto);
        var filas = await FilasDelContextoAsync(contexto, sesion.Id, cancellationToken);
        if (ParseadorAclaraciones.EsOtro(texto))
        {
            foreach (var fila in filas)
            {
                fila.EjercicioCanonico = fila.EjercicioCrudo ?? contexto.Crudo;
                fila.DescripcionPendiente = false;
                fila.PendienteAclaracion = false;
            }

            QuitarAclaracion(sesion);
            sesion.Estado = EstadoSesionCaptura.EnCurso;
            return new(
                TextosBot.EjercicioComoTexto(contexto.Crudo ?? "el ejercicio"),
                sesion.Id,
                AclaracionCreada: false);
        }

        var entidades = analisis.Entidades;
        var nombre = entidades?.EjercicioNormalizado ?? entidades?.EjercicioTexto;
        if (string.IsNullOrWhiteSpace(nombre) || filas.Count == 0)
        {
            return null;
        }

        foreach (var fila in filas)
        {
            fila.EjercicioCanonico = nombre;
            fila.PendienteAclaracion = false;
            fila.DescripcionPendiente = false;
        }

        QuitarAclaracion(sesion);
        sesion.Estado = EstadoSesionCaptura.EnCurso;
        return await ContinuarLoteAsync(sesion, filas, cancellationToken);
    }

    // ─── Lotes de series ───────────────────────────────────────────────────────

    // Continúa el lote tras resolver el ejercicio (ADR-0013): el dictado solo
    // arma filas (los datos de serie se completan después), así que acá NO se
    // pregunta faltante alguno — se confirma el lote y el flujo queda listo.
    private async Task<CapturaRespuesta> ContinuarLoteAsync(
        SesionCaptura sesion,
        List<RegistroParcial> filas,
        CancellationToken cancellationToken)
    {
        if (filas.Count == 0)
        {
            DefensivaSinFilas(sesion);
            return new(TextosBot.DespedirSeriePendiente, sesion.Id, AclaracionCreada: false);
        }

        QuitarAclaracion(sesion);
        foreach (var fila in filas)
        {
            fila.PendienteAclaracion = false;
        }

        // La numeración sigue a las filas REALES del lote resuelto (nunca un
        // contador encontrado en un estado raro): el próximo lote continúa.
        sesion.OrdenSerieSiguiente = Math.Max(
            sesion.OrdenSerieSiguiente, filas.Max(f => f.NroSerie) + 1);
        sesion.Estado = EstadoSesionCaptura.EnCurso;

        var nombre = filas[0].EjercicioCanonico
            ?? filas[0].Ejercicio?.NombreCanonico
            ?? filas[0].EjercicioCrudo
            ?? "el ejercicio";

        return new(
            TextosBot.SeriesAnotadasBase(
                nombre,
                filas.Min(f => f.NroSerie), filas.Max(f => f.NroSerie)),
            sesion.Id,
            AclaracionCreada: false);
    }

    // Salida sin filas (contexto sin ids de registro): el flujo queda libre y
    // la pregunta se cierra; nada se inventa sobre filas inexistentes.
    private void DefensivaSinFilas(SesionCaptura sesion)
    {
        QuitarAclaracion(sesion);
        sesion.Estado = EstadoSesionCaptura.EnCurso;
    }

    // Dictado en regla (ADR-0013/0014): reparte entre la regla base (lote de
    // filas) y la de datos de serie según el CONTENIDO del análisis — así un
    // intent mal etiquetado por el LLM no rompe el flujo determinístico.
    private async Task<CapturaRespuesta> AplicarDictadoAsync(
        SesionCaptura sesion,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        // Regla 2 (ADR-0014) primero: datos de serie sin ejercicio nombrado.
        if (EsCargaDatosSerie(analisis))
        {
            return await AplicarDatosSerieAsync(sesion, analisis, cancellationToken);
        }

        if (!EsCapturaBase(analisis))
        {
            // Dictado fuera de la regla: guía única, sin filas y SIN apoyarse
            // en el ejercicio activo (decisión de dominio, ADR-0013).
            return new(TextosBot.FueraDeRegla, sesion.Id, AclaracionCreada: false);
        }

        var bloqueo = await ObtenerBloqueoPorSeriesIncompletasAsync(sesion.Id, cancellationToken);
        if (bloqueo is not null)
        {
            return new(bloqueo, sesion.Id, AclaracionCreada: false);
        }

        var entidades = analisis.Entidades!;
        // La IA propone un nombre normalizado y conserva la frase original. La
        // captura es texto libre: no se resuelve ni se valida contra un catálogo.
        var nombreEjercicio = entidades.EjercicioNormalizado ?? entidades.EjercicioTexto!;
        var aclaracionAbierta = entidades.CampoAclarar == "ejercicio";
        var ultimoLote = await db.RegistrosParciales
            .Where(r => r.SesionId == sesion.Id)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var nombreUltimoLote = ultimoLote?.EjercicioCanonico
            ?? ultimoLote?.Ejercicio?.NombreCanonico
            ?? ultimoLote?.EjercicioCrudo;
        if (nombreUltimoLote is null
            || !string.Equals(nombreUltimoLote, nombreEjercicio, StringComparison.OrdinalIgnoreCase))
        {
            sesion.OrdenSerieSiguiente = 1;
        }
        sesion.EjercicioActivoId = null;

        // La serie ordinal explícita ("tercera serie") gana como base; si solo
        // hay cantidad ("4 series"), el lote continúa la numeración del
        // ejercicio activo (1..n en el primero). Con el ejercicio AÚN sin
        // resolver la numeración es provisional: arranca en el ordinal o en
        // 1 — el contador del activo puede pertenecer a OTRO ejercicio y no
        // corresponde heredarlo (ADR-0013).
        var cantidad = entidades.CantidadSeries is { } c && c >= 1 ? c : 1;
        var nroBase = entidades.SerieOrdinal ?? sesion.OrdenSerieSiguiente;
        var filas = new List<RegistroParcial>(cantidad);

        for (var i = 0; i < cantidad; i++)
        {
            filas.Add(new RegistroParcial
            {
                SesionId = sesion.Id,
                EjercicioId = null,
                EjercicioCrudo = entidades.EjercicioTexto, // lo que dijo (auditoría)
                EjercicioCanonico = nombreEjercicio,
                NroSerie = nroBase + i,
                Reps = null,    // ADR-0013/0014: el lote nace con ejercicio + serie;
                PesoKg = null,  // el dato de cada serie llega con la regla 2
                Unidad = null,
                TiempoSeg = null,
                DescripcionPendiente = false,
                PendienteAclaracion = aclaracionAbierta,
                CreadoUtc = ahora,
            });
        }

        db.RegistrosParciales.AddRange(filas);
        // Los ids de las filas entran en el contexto de la pregunta viva:
        // hay que guardar antes de armar la aclaración.
        await db.SaveChangesAsync(cancellationToken);
        sesion.OrdenSerieSiguiente = Math.Max(sesion.OrdenSerieSiguiente, nroBase + cantidad);

        if (aclaracionAbierta)
        {
            CrearAclaracion(sesion, "ejercicio", filas.Select(f => f.Id), entidades.EjercicioTexto, ahora);
            sesion.Estado = EstadoSesionCaptura.EsperandoAclaracion;
            return new(TextosBot.PreguntaEjercicioAbierta(entidades.PreguntaAclarar), sesion.Id, AclaracionCreada: true);
        }

        return new(
            TextosBot.SeriesAnotadasBase(nombreEjercicio, nroBase, nroBase + cantidad - 1),
            sesion.Id,
            AclaracionCreada: false);
    }

    // Regla 2 (ADR-0014): "NRO DE SERIE + REPS y/o PESO" SIN ejercicio nombrado
    // llena la fila de ese ordinal del ÚLTIMO LOTE armado (el lote vivo que el
    // alumno está cargando — véase «Estás cargando» en la SPA), sea de un
    // ejercicio del catálogo o de TEXTO LIBRE (cantidad de series de «otro»).
    // El objetivo ya NO es el EjercicioActivoId heredado: dictar datos después
    // de un lote pendiente de resolver (SinMatch) debe tocar ESE lote.
    // Nada inventado: la fila que no existe se avisa (no se crea), solo se
    // escriben los valores DICHO y el último dictado gana. Unidad.Tiempo no
    // entra en la regla: el tiempo se retiró del MVP (ADR-0014).
    private async Task<CapturaRespuesta> AplicarDatosSerieAsync(
        SesionCaptura sesion,
        AnalisisCaptura analisis,
        CancellationToken cancellationToken)
    {
        var entidades = analisis.Entidades!;
        var ordinal = entidades.SerieOrdinal!.Value;

        var todas = await db.RegistrosParciales
            .Where(r => r.SesionId == sesion.Id)
            .ToListAsync(cancellationToken);

        // Lote vivo = las filas del último paso 1 (las comparten CreadoUtc;
        // edición y arranque de datos no lo tocan). Sin filas: sin contexto.
        var creadoVivo = todas.Count > 0 ? todas.Max(r => r.CreadoUtc) : default(DateTime?);
        var lote = creadoVivo is { } vivo
            ? todas.Where(r => r.CreadoUtc == vivo).OrderBy(r => r.Id).ToList()
            : [];

        if (lote.Count == 0)
        {
            // Datos de serie sin contexto: primero el paso 1 (ejercicio +
            // series); el dictado no apoya en filas ajenas ni las adivina.
            return new(TextosBot.FueraDeRegla, sesion.Id, AclaracionCreada: false);
        }

        var filaModelo = lote[0];
        var nombre = filaModelo.EjercicioCanonico
            ?? filaModelo.Ejercicio?.NombreCanonico
            ?? filaModelo.EjercicioCrudo
            ?? "el ejercicio";

        var fila = lote.FirstOrDefault(r => r.NroSerie == ordinal);
        if (fila is null)
        {
            logger.LogInformation(
                "Datos de serie sin fila: serie {Ordinal} no está en el lote vivo de '{Nombre}' (sesión {Sesion}).",
                ordinal, nombre, sesion.Id);
            return new(TextosBot.SerieSinFila(nombre, ordinal), sesion.Id, AclaracionCreada: false);
        }

        // Solo se escribe lo DICTADO (regla madre: nada inventado); lo demás
        // de la fila queda como estaba; repetir un valor lo sobrescribe ("último
        // dictado gana"). "50" suelto → kg (lo fija NormalizarValores).
        var (peso, unidad) = NormalizarValores(entidades.PesoKg, entidades.Unidad);
        if (peso is not null)
        {
            fila.PesoKg = peso;
            fila.Unidad = unidad == Unidad.Tiempo ? null : unidad;
        }
        else if (entidades.Unidad == Unidad.PesoCorporal)
        {
            fila.PesoKg = null;
            fila.Unidad = Unidad.PesoCorporal;
        }

        if (entidades.Reps is { } reps)
        {
            fila.Reps = reps;
        }

        // El dato llegó: la fila ya no "espera al bot" en la cuadrícula.
        fila.PendienteAclaracion = false;

        var faltantes = ReglaCompletitudSeries.CamposFaltantes(fila);
        var mensajeSerie = TextosBot.SerieConDatos(
            nombre,
            ordinal,
            TextosBot.DetalleSerie(fila.Reps, fila.PesoKg, fila.Unidad));
        if (faltantes.Count > 0)
        {
            mensajeSerie += $" Para seguir, falta {string.Join(" y ", faltantes)} de esta serie.";
        }

        return new(
            mensajeSerie,
            sesion.Id,
            AclaracionCreada: false);
    }

    private async Task<string?> ObtenerBloqueoPorSeriesIncompletasAsync(
        int sesionId,
        CancellationToken cancellationToken)
    {
        var filas = await db.RegistrosParciales
            .Where(fila => fila.SesionId == sesionId)
            .ToListAsync(cancellationToken);
        if (filas.Count == 0)
        {
            return null;
        }

        // Aunque el paso de datos opera sobre el lote vivo, ningún ejercicio
        // puede quedar incompleto cuando se inicia el siguiente.
        var incompletas = filas
            .Where(fila => !ReglaCompletitudSeries.EstaCompleta(fila))
            .ToList();
        if (incompletas.Count == 0)
        {
            return null;
        }

        return ReglaCompletitudSeries.MensajeParaCompletar(incompletas);
    }

    // ─── Intents sin aclaración previa ─────────────────────────────────────────

    private async Task<CapturaRespuesta> ProcesarIntentoAsync(
        SesionCaptura sesion,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        if (!analisis.Clasificacion.Exito)
        {
            // Etapa de análisis fallida: nada de datos → pedir reenvío sin tocar estado.
            return new CapturaRespuesta(TextosBot.FalloLectura, sesion.Id, AclaracionCreada: false);
        }

        // "seguir" con sesión viva sin pendientes: cortesía de retoma.
        if (sesion.Estado == EstadoSesionCaptura.EnCurso && ParseadorAclaraciones.EsSeguir(analisis.TextoProcesado))
        {
            return new CapturaRespuesta(TextosBot.Seguimos, sesion.Id, AclaracionCreada: false);
        }

        switch (analisis.Clasificacion.Intento)
        {
            case IntentoCaptura.Cierre:
                sesion.Estado = EstadoSesionCaptura.Lista;
                return new CapturaRespuesta(TextosBot.Cierre, sesion.Id, AclaracionCreada: false);

            case IntentoCaptura.AyudaConsulta:
                return new CapturaRespuesta(TextosBot.Ayuda, sesion.Id, AclaracionCreada: false);

            case IntentoCaptura.CargaEjercicio:
            case IntentoCaptura.CargaSerie:
                if (analisis.Entidades is null)
                {
                    return new CapturaRespuesta(TextosBot.FalloLectura, sesion.Id, AclaracionCreada: false);
                }

                // La disyuntiva lote base vs datos de serie vs guía la decide
                // AplicarDictadoAsync (por CONTENIDO, ADR-0013/0014); los dos
                // intents entran acá porque el LLM puede confundirlos en la voz.
                return await AplicarDictadoAsync(sesion, analisis, ahora, cancellationToken);

            default:
                return new CapturaRespuesta(TextosBot.Cortesia, sesion.Id, AclaracionCreada: false);
        }
    }

    // ─── Preguntas y helpers de aclaración ─────────────────────────────────────

    // Re-pregunta en reintento (aclaración de EJERCICIO, la única que existe:
    // ADR-0013): re-deriva TODO determinísticamente (incluida la lista de
    // sugerencias), sin nueva llamada al LLM.
    private async Task<string> PreguntaReintentoAsync(AclaracionPendiente aclaracion, CancellationToken cancellationToken)
    {
        if (aclaracion.Campo != "ejercicio")
        {
            return TextosBot.Reintento(TextosBot.FueraDeRegla);
        }

        var contexto = ParsearContexto(aclaracion.Contexto);
        if (contexto.Crudo is not { Length: > 0 })
        {
            return TextosBot.Reintento(TextosBot.PreguntaEjercicio(null, []));
        }

        return TextosBot.Reintento(TextosBot.PreguntaEjercicioAbierta(null));
    }

    private async Task<List<RegistroParcial>> FilasDelContextoAsync(
        ContextoAclaracion contexto, int sesionId, CancellationToken cancellationToken)
    {
        if (contexto.RegistroIds.Length == 0)
        {
            return [];
        }

        return await db.RegistrosParciales
            .Where(r => r.SesionId == sesionId && contexto.RegistroIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    private void CrearAclaracion(SesionCaptura sesion, string campo, IEnumerable<int> registroIds, string? crudo, DateTime ahora)
    {
        // 1:1 con la sesión: una pregunta viva, no pueden coexistir.
        if (sesion.AclaracionPendiente is { } previa)
        {
            db.AclaracionesPendientes.Remove(previa);
            sesion.AclaracionPendiente = null;
        }

        var aclaracion = new AclaracionPendiente
        {
            SesionId = sesion.Id,
            Campo = campo,
            Contexto = SerializarContexto(new ContextoAclaracion([.. registroIds], crudo)),
            ExpiraUtc = ahora.Add(VidaAclaracion),
        };
        db.AclaracionesPendientes.Add(aclaracion);
        sesion.AclaracionPendiente = aclaracion;
    }

    private void QuitarAclaracion(SesionCaptura sesion)
    {
        if (sesion.AclaracionPendiente is not { } pendiente)
        {
            return;
        }

        db.AclaracionesPendientes.Remove(pendiente);
        sesion.AclaracionPendiente = null;
    }

    // Abandono (2 reintentos fallidos o CIERRE): la pregunta se borra; las
    // filas SIN ejercicio quedan como descripción libre, con su crudo intacto.
    private async Task AbandonarPendienteAsync(SesionCaptura sesion, CancellationToken cancellationToken)
    {
        var contexto = ParsearContexto(sesion.AclaracionPendiente?.Contexto);
        if (contexto.RegistroIds.Length > 0)
        {
            var filas = await db.RegistrosParciales
                .Where(r => r.SesionId == sesion.Id && contexto.RegistroIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            foreach (var fila in filas.Where(f => f.EjercicioId is null))
            {
                fila.DescripcionPendiente = true;
            }
        }

        QuitarAclaracion(sesion);
    }

    // ─── Contexto JSON mínimo de la pregunta ───────────────────────────────────

    // {"registroIds":[1,2],"crudo":"press 12"}: lo mínimo para re-derivar el
    // resto en la respuesta (determinismo primero, ADR-0004).
    internal sealed record ContextoAclaracion(int[] RegistroIds, string? Crudo);

    private static string SerializarContexto(ContextoAclaracion contexto) =>
        JsonSerializer.Serialize(new { registroIds = contexto.RegistroIds, crudo = contexto.Crudo });

    private static ContextoAclaracion ParsearContexto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new([], null);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var ids = new List<int>();
            if (root.TryGetProperty("registroIds", out var arreglo) && arreglo.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arreglo.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var id))
                    {
                        ids.Add(id);
                    }
                }
            }

            string? crudo = root.TryGetProperty("crudo", out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString()
                : null;

            return new([.. ids], crudo);
        }
        catch (JsonException)
        {
            // Contexto corrupto: la aclaración no se puede resolver → reintento
            // + abandono, sin crash ni datos inventados.
            return new([], null);
        }
    }

    // ─── Normalización de valores hablados ─────────────────────────────────────

    // Conversiones EXPLÍCITAS del servicio (ADR-0004): lb→kg con factor fijo;
    // null significa "no lo dijo", jamás un valor por defecto inventado.
    // internal para reuso: el CRUD de la SPA aplica el MISMO criterio.
    internal static (decimal? PesoKg, Unidad? Unidad) NormalizarValores(decimal? peso, Unidad? unidad)
    {
        if (unidad == Unidad.Lb && peso is decimal enLibras)
        {
            return (Math.Round(enLibras * LibrasPorKilo, 2, MidpointRounding.AwayFromZero), Unidad.Kg);
        }

        if (unidad == Unidad.PesoCorporal)
        {
            return (null, Unidad.PesoCorporal);
        }

        if (peso is not null && unidad is null)
        {
            return (peso, Unidad.Kg); // "50" suelto → kg (spec NLU §2)
        }

        return (peso, unidad);
    }
}
