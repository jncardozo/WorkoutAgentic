using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.Whatsapp;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Etapa 4 del pipeline del Voice Tracker (spec NLU §5): aplica el análisis
// sobre el estado persistido de SesionCaptura y produce la respuesta del bot.
// Reglas de dominio en docs/domain/business-rules.md; decisiones del slice en
// ADR-0004. Regla madre: si falta o es ambiguo → aclaración, jamás inventado.
internal sealed class SesionCapturaServicio(
    CapturasDbContext db,
    IWhatsappCatalogoEjercicios catalogo,
    ILogger<SesionCapturaServicio> logger) : ISesionCapturaServicio
{
    // [SUPUESTO ADR-0004]: pausa automática tras 2 h sin actividad.
    internal static readonly TimeSpan InactividadMaxima = TimeSpan.FromHours(2);

    // Vida útil de la pregunta viva; caducada, solo "seguir" reanuda el flujo.
    internal static readonly TimeSpan VidaAclaracion = TimeSpan.FromHours(24);

    // Una libra avoirdupois = 0,45359237 kg exactos (conversión determinística).
    private const decimal LibrasPorKilo = 0.45359237m;

    // Sesiones que atienden mensajes: Confirmada no; Lista responde la
    // invitación a confirmar en la app, sin abrir carga nueva.
    private static readonly EstadoSesionCaptura[] EstadosOperables =
    [
        EstadoSesionCaptura.EnCurso,
        EstadoSesionCaptura.EsperandoAclaracion,
        EstadoSesionCaptura.Expirada,
        EstadoSesionCaptura.Lista,
    ];

    public async Task<CapturaRespuesta> AplicarAsync(
        IncomingWhatsappMessage mensaje,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var telefono = WhatsappPhone.Canonizar(mensaje.From);
        var alumno = await AlumnoPorTelefonoAsync(telefono, cancellationToken);
        return await NucleoAsync(alumno, mensaje, analisis, ahora, cancellationToken);
    }

    public async Task<CapturaRespuesta> AplicarTextoAsync(
        int alumnoId,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos.SingleOrDefaultAsync(a => a.Id == alumnoId, cancellationToken)
                     ?? throw new InvalidOperationException(
                         $"Alumno {alumnoId} inexistente: el borde valida 404 antes de aplicar.");
        return await NucleoAsync(alumno, mensaje: null, analisis, ahora, cancellationToken);
    }

    // Núcleo de identificado de sesión + resolución de aclaraciones + intent
    // (compartido por WhatsApp y la PWA; ADR-0009). El mensaje es único
    // si viene de WhatsApp y aporta la auditoría de audio.
    private async Task<CapturaRespuesta> NucleoAsync(
        Alumno alumno,
        IncomingWhatsappMessage? mensaje,
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
            logger.LogInformation(
                "Sesión de captura nueva para el alumno {Alumno} (origen {Origen}).",
                alumno.Id, mensaje is null ? "PWA" : "WhatsApp");
        }
        else if (sesion.Estado == EstadoSesionCaptura.Lista)
        {
            // CIERRE ya hecho: la planilla espera confirmación en la SPA.
            return new(TextosBot.PlanillaYaLista, sesion.Id, AclaracionCreada: false);
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

        RegistrarTranscripcion(sesion, mensaje, analisis, ahora);

        CapturaRespuesta respuesta;
        if (sesion.AclaracionPendiente is { } aclaracion)
        {
            respuesta = await ResolverPendienteOIntentarAsync(sesion, aclaracion, analisis, ahora, cancellationToken);
        }
        else
        {
            respuesta = await ProcesarIntentoAsync(sesion, analisis, ahora, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return respuesta;
    }

    public async Task MarcarMensajeBotAsync(int sesionId, string? mensajeBotWaId, CancellationToken cancellationToken)
    {
        if (mensajeBotWaId is null)
        {
            return;
        }

        var aclaracion = await db.AclaracionesPendientes.FindAsync([sesionId], cancellationToken);
        if (aclaracion is null)
        {
            return; // pudo ya haberse resuelto cuando confirmó el envío
        }

        aclaracion.MensajeBotWaId = mensajeBotWaId;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ─── Cuenta y sesión ────────────────────────────────────────────────────────

    private async Task<Alumno> AlumnoPorTelefonoAsync(string telefono, CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos.SingleOrDefaultAsync(a => a.TelefonoWa == telefono, cancellationToken);
        if (alumno is not null)
        {
            return alumno;
        }

        // Primer contacto por WhatsApp: alta por teléfono (ADR-0004).
        alumno = new Alumno { TelefonoWa = telefono, Estado = "ACTIVO" };
        db.Alumnos.Add(alumno);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Alumno creado por primer contacto desde WhatsApp: {Telefono}.", telefono);
        return alumno;
    }

    private Task<SesionCaptura?> SesionRecienteAsync(int alumnoId, CancellationToken cancellationToken) =>
        db.SesionesCaptura
            .Include(s => s.AclaracionPendiente)
            .Where(s => s.AlumnoId == alumnoId && EstadosOperables.Contains(s.Estado))
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // ─── Auditoría de voz ──────────────────────────────────────────────────────

    private void RegistrarTranscripcion(SesionCaptura sesion, IncomingWhatsappMessage? mensaje, AnalisisCaptura analisis, DateTime ahora)
    {
        if (mensaje is null || mensaje.Kind != WhatsappMessageKind.Audio || mensaje.AudioMediaId is null)
        {
            return;
        }

        if (analisis.Transcripcion is not { Exito: true, Texto: not null } transcripcion)
        {
            return; // etapa fallida: el pipeline ya dejó su log de error
        }

        // MediaId de Graph, nunca la URL temporal (convención WhatsApp).
        db.Transcripciones.Add(new Transcripcion
        {
            SesionId = sesion.Id,
            MediaId = mensaje.AudioMediaId,
            TextoCrudo = transcripcion.Texto,
            Proveedor = "Gemini", // ADR-0002 S1
            DuracionSeg = mensaje.AudioDurationSeconds,
            RecibidoUtc = ahora,
        });
    }

    // ─── Aclaración pendiente vs intents ───────────────────────────────────────

    private async Task<CapturaRespuesta> ResolverPendienteOIntentarAsync(
        SesionCaptura sesion,
        AclaracionPendiente aclaracion,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        // CIERRE domina: abandona lo pendiente (UX; ADR-0004) y cierra en Lista.
        if (analisis.Clasificacion is { Exito: true, Intento: IntentoCaptura.Cierre })
        {
            await AbandonarPendienteAsync(sesion, cancellationToken);
            sesion.Estado = EstadoSesionCaptura.Lista;
            return new(TextosBot.Cierre, sesion.Id, AclaracionCreada: false);
        }

        var resuelta = await IntentarResolverAsync(sesion, aclaracion, analisis, ahora, cancellationToken);
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

    private async Task<CapturaRespuesta?> IntentarResolverAsync(
        SesionCaptura sesion,
        AclaracionPendiente aclaracion,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var texto = analisis.TextoProcesado;
        var contexto = ParsearContexto(aclaracion.Contexto);
        var filas = await FilasDelContextoAsync(contexto, sesion.Id, cancellationToken);

        switch (aclaracion.Campo)
        {
            case "peso":
                var peso = ParseadorAclaraciones.ParsearPeso(texto);
                if (peso is null)
                {
                    return null;
                }

                foreach (var fila in filas)
                {
                    fila.PesoKg = peso;
                    fila.Unidad ??= Unidad.Kg; // valor hablado sin unidad → kg
                }

                return await ContinuarLoteAsync(sesion, filas, ahora, cancellationToken);

            case "reps":
                var reps = ParseadorAclaraciones.ParsearEntero(texto);
                if (reps is null)
                {
                    return null;
                }

                foreach (var fila in filas)
                {
                    fila.Reps = reps;
                }

                return await ContinuarLoteAsync(sesion, filas, ahora, cancellationToken);

            case "tiempo":
                var segundos = ParseadorAclaraciones.ParsearEntero(texto);
                if (segundos is null)
                {
                    return null;
                }

                foreach (var fila in filas)
                {
                    fila.TiempoSeg = segundos;
                    fila.Unidad = Unidad.Tiempo;
                }

                return await ContinuarLoteAsync(sesion, filas, ahora, cancellationToken);

            case "serie":
                var serie = ParseadorAclaraciones.ParsearEntero(texto);
                if (serie is null)
                {
                    return null;
                }

                foreach (var fila in filas)
                {
                    fila.NroSerie = serie.Value;
                }

                sesion.OrdenSerieSiguiente = Math.Max(sesion.OrdenSerieSiguiente, serie.Value + 1);
                return await ContinuarLoteAsync(sesion, filas, ahora, cancellationToken);

            case "ejercicio":
                return await ResolverEjercicioAclaracionAsync(sesion, contexto, filas, texto, ahora, cancellationToken);

            default:
                // Campo no previsto por el spec: reintento; nada inventado.
                return null;
        }
    }

    private async Task<CapturaRespuesta?> ResolverEjercicioAclaracionAsync(
        SesionCaptura sesion,
        ContextoAclaracion contexto,
        List<RegistroParcial> filas,
        string texto,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        // "1"/"2"/"3": elección enumerada sobre sugerencias re-derivadas con el
        // MISMO resolver determinístico desde el crudo guardado en el contexto.
        var opcion = ParseadorAclaraciones.ParsearEntero(texto);
        if (opcion is { } indice)
        {
            var sugerencias = await SugerenciasDeContextoAsync(contexto.Crudo, cancellationToken);
            if (indice > sugerencias.Count)
            {
                return null;
            }

            return await FijarEjercicioAsync(sesion, filas, sugerencias, indice, ahora, cancellationToken);
        }

        if (ParseadorAclaraciones.EsOtro(texto))
        {
            foreach (var fila in filas)
            {
                fila.DescripcionPendiente = true; // el crudo queda como descripción libre
                fila.PendienteAclaracion = false;
            }

            QuitarAclaracion(sesion);
            sesion.Estado = EstadoSesionCaptura.EnCurso;
            return new(
                TextosBot.EjercicioComoTexto(contexto.Crudo ?? "el ejercicio"),
                sesion.Id,
                AclaracionCreada: false);
        }

        // Respuesta textual: se resuelve como ejercicio, determinístico.
        if (texto.Length == 0)
        {
            return null;
        }

        var porNombre = CatalogoEjercicioResolver.Resolver(texto, await catalogo.LeerAsync(cancellationToken));
        if (porNombre.Estado == EstadoResolucion.Matcheado
            && porNombre.EjercicioId is { } id && porNombre.EjercicioCanonico is { } canonico)
        {
            return await FijarEjercicioAsync(
                sesion, filas, new List<SugerenciaEjercicio> { new(id, canonico, 0) }, 1, ahora, cancellationToken);
        }

        return null; // ambiguo o sin match → reintento con la misma lista
    }

    // ─── Lotes de series ───────────────────────────────────────────────────────

    // Continúa el lote tras aplicar un dato: pregunta el próximo faltante o
    // cierra. Las filas comparten estado — se marca/desmarca el lote entero.
    private async Task<CapturaRespuesta> ContinuarLoteAsync(
        SesionCaptura sesion,
        List<RegistroParcial> filas,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        if (filas.Count == 0)
        {
            QuitarAclaracion(sesion);
            sesion.Estado = EstadoSesionCaptura.EnCurso;
            return new(TextosBot.DespedirSeriePendiente, sesion.Id, AclaracionCreada: false);
        }

        QuitarAclaracion(sesion);

        var ejercicio = filas[0].EjercicioId is int id
            ? (await catalogo.LeerAsync(cancellationToken)).FirstOrDefault(e => e.Id == id)
            : null;

        var faltante = CampoFaltante(filas[0], ejercicio);
        if (faltante is null)
        {
            foreach (var fila in filas)
            {
                fila.PendienteAclaracion = false;
            }

            sesion.Estado = EstadoSesionCaptura.EnCurso;
            return new(TextosBot.RegistroConfirmado(DetalleSerie(filas[0])), sesion.Id, AclaracionCreada: false);
        }

        foreach (var fila in filas)
        {
            fila.PendienteAclaracion = true;
        }

        CrearAclaracion(sesion, faltante, filas.Select(f => f.Id), filas[0].EjercicioCrudo, ahora);
        sesion.Estado = EstadoSesionCaptura.EsperandoAclaracion;
        return new(
            PreguntaPor(faltante, ejercicio?.NombreCanonico ?? filas[0].EjercicioCrudo ?? "esa serie"),
            sesion.Id,
            AclaracionCreada: true);
    }

    // Alta de ejercicio (CARGA_EJERCICIO) y de series (CARGA_SERIE): mismas
    // reglas; granularidad POR SERIE — "3x12" ⇒ 3 filas (ADR-0004).
    private async Task<CapturaRespuesta> AplicarCargaAsync(
        SesionCaptura sesion,
        EntidadesNlu entidades,
        ResultadoResolucionEjercicio? resolucion,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var catalogoItems = await catalogo.LeerAsync(cancellationToken);

        // Prioridad: el ejercicio resuelto en ESTE mensaje gana; cuando el
        // alumno nombró otro (ambiguo/sin match) NO se usa el activo; sin
        // mención, la serie cae sobre el activo (spec NLU §2: "segunda serie…").
        CatalogoEjercicio? ejercicio = null;
        if (resolucion is { Estado: EstadoResolucion.Matcheado, EjercicioId: int idResuelto })
        {
            ejercicio = catalogoItems.FirstOrDefault(e => e.Id == idResuelto);
            if (ejercicio is not null && sesion.EjercicioActivoId != idResuelto)
            {
                sesion.EjercicioActivoId = idResuelto;
                sesion.OrdenSerieSiguiente = 1; // ejercicio nuevo reinicia la numeración
            }
        }
        else if (resolucion is null && sesion.EjercicioActivoId is int idActivo)
        {
            ejercicio = catalogoItems.FirstOrDefault(e => e.Id == idActivo);
        }

        var (pesoKg, unidad) = NormalizarValores(entidades.PesoKg, entidades.Unidad);
        if (entidades.TiempoSegundos is not null && unidad is null)
        {
            unidad = Unidad.Tiempo;
        }

        var cantidad = entidades.CantidadSeries is > 1 ? entidades.CantidadSeries.Value : 1;
        var nroBase = entidades.SerieOrdinal ?? sesion.OrdenSerieSiguiente;
        List<RegistroParcial> filas = [];

        // Sin ningún dato de serie no se anota fila: alta "de palabra".
        var hayDatosSerie = entidades.Reps is not null
            || pesoKg is not null
            || unidad is not null
            || entidades.TiempoSegundos is not null;

        if (hayDatosSerie)
        {
            for (var i = 0; i < cantidad; i++)
            {
                filas.Add(new RegistroParcial
                {
                    SesionId = sesion.Id,
                    EjercicioId = ejercicio?.Id,
                    EjercicioCrudo = entidades.EjercicioTexto, // solo si lo dijo
                    NroSerie = nroBase + i,
                    Reps = entidades.Reps,
                    PesoKg = pesoKg,
                    Unidad = unidad,
                    TiempoSeg = entidades.TiempoSegundos,
                    DescripcionPendiente = false,
                    PendienteAclaracion = false,
                    CreadoUtc = ahora,
                });
            }

            db.RegistrosParciales.AddRange(filas);
            // Los ids de las filas entran en el contexto de la pregunta viva:
            // hay que guardar antes de armar la aclaración.
            await db.SaveChangesAsync(cancellationToken);
            sesion.OrdenSerieSiguiente = Math.Max(sesion.OrdenSerieSiguiente, nroBase + cantidad);
        }

        // Alta "de palabra" (sólo el ejercicio nombrado): nada que validar aún,
        // se invita a cargar la primera serie (spec NLU §5).
        if (ejercicio is not null && filas.Count == 0)
        {
            return new(
                TextosBot.EjercicioAnotado(ejercicio.NombreCanonico, ejercicio.UsaTiempo),
                sesion.Id,
                AclaracionCreada: false);
        }

        // Sin ejercicio no hay tipo contra qué validar los faltantes.
        var campoFaltante = ejercicio is null ? "ejercicio" : CampoFaltante(filas[0], ejercicio);

        if (campoFaltante is null)
        {
            // Lote completo: confirmamos y quedamos listos para la próxima serie.
            return new(
                TextosBot.SeriesAnotadas(
                    ejercicio!.NombreCanonico, nroBase, nroBase + cantidad - 1, DetalleSerie(filas[0])),
                sesion.Id,
                AclaracionCreada: false);
        }

        if (campoFaltante == "ejercicio")
        {
            // Con datos: filas pendientes ya creadas, referenciadas por id en el
            // contexto. Sin datos: solo la pregunta (alto el ejercicio, nada aún).
            if (!hayDatosSerie)
            {
                filas.Clear();
            }
            else
            {
                foreach (var fila in filas)
                {
                    fila.PendienteAclaracion = true;
                }
            }

            List<string> nombres = resolucion?.Sugerencias.Count is > 0
                ? [.. resolucion.Sugerencias.Select(s => s.NombreCanonico)]
                : [];
            var pregunta = resolucion is { Estado: EstadoResolucion.Ambiguo } && nombres.Count > 0
                ? TextosBot.PreguntaEjercicioAmbiguo(nombres)
                : TextosBot.PreguntaEjercicio(entidades.EjercicioTexto, nombres);

            CrearAclaracion(sesion, "ejercicio", filas.Select(f => f.Id), entidades.EjercicioTexto, ahora);
            sesion.Estado = EstadoSesionCaptura.EsperandoAclaracion;
            return new(pregunta, sesion.Id, AclaracionCreada: true);
        }

        // Faltante puntual (peso/reps/tiempo) sobre filas ya creadas.
        foreach (var fila in filas)
        {
            fila.PendienteAclaracion = true;
        }

        CrearAclaracion(sesion, campoFaltante, filas.Select(f => f.Id), entidades.EjercicioTexto, ahora);
        sesion.Estado = EstadoSesionCaptura.EsperandoAclaracion;
        return new(PreguntaPor(campoFaltante, ejercicio!.NombreCanonico), sesion.Id, AclaracionCreada: true);
    }

    // ─── Intents sin aclaración previa ─────────────────────────────────────────

    private Task<CapturaRespuesta> ProcesarIntentoAsync(
        SesionCaptura sesion,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        if (!analisis.Clasificacion.Exito)
        {
            // Etapa de análisis fallida: nada de datos → pedir reenvío sin tocar estado.
            return Task.FromResult(new CapturaRespuesta(TextosBot.FalloLectura, sesion.Id, AclaracionCreada: false));
        }

        // "seguir" con sesión viva sin pendientes: cortesía de retoma.
        if (sesion.Estado == EstadoSesionCaptura.EnCurso && ParseadorAclaraciones.EsSeguir(analisis.TextoProcesado))
        {
            return Task.FromResult(new CapturaRespuesta(TextosBot.Seguimos, sesion.Id, AclaracionCreada: false));
        }

        switch (analisis.Clasificacion.Intento)
        {
            case IntentoCaptura.Cierre:
                sesion.Estado = EstadoSesionCaptura.Lista;
                return Task.FromResult(new CapturaRespuesta(TextosBot.Cierre, sesion.Id, AclaracionCreada: false));

            case IntentoCaptura.AyudaConsulta:
                return Task.FromResult(new CapturaRespuesta(TextosBot.Ayuda, sesion.Id, AclaracionCreada: false));

            case IntentoCaptura.CargaEjercicio:
            case IntentoCaptura.CargaSerie:
                if (analisis.Entidades is null)
                {
                    return Task.FromResult(new CapturaRespuesta(TextosBot.FalloLectura, sesion.Id, AclaracionCreada: false));
                }

                return AplicarCargaAsync(
                    sesion, analisis.Entidades, analisis.ResolucionEjercicio, ahora, cancellationToken);

            default:
                return Task.FromResult(new CapturaRespuesta(TextosBot.Cortesia, sesion.Id, AclaracionCreada: false));
        }
    }

    // ─── Preguntas y helpers de aclaración ─────────────────────────────────────

    // Próximo campo REQUERIDO según el tipo de ejercicio (ADR-0004): no se
    // pregunta peso para dominadas ni reps para plancha. El servicio NO confía
    // ciegamente en CampoAclarar del LLM: valida contra el catálogo.
    internal static string? CampoFaltante(RegistroParcial fila, CatalogoEjercicio? ejercicio)
    {
        if (ejercicio?.UsaPeso is true
            && fila.PesoKg is null
            && fila.Unidad != Unidad.PesoCorporal)
        {
            return "peso";
        }

        if (ejercicio?.UsaTiempo is true && fila.TiempoSeg is null)
        {
            return "tiempo";
        }

        // En ejercicios por tiempo (plancha) no se piden reps: se pide el tiempo.
        if (ejercicio?.UsaTiempo is not true && fila.Reps is null)
        {
            return "reps";
        }

        return null;
    }

    private static string PreguntaPor(string campo, string referencia) => campo switch
    {
        "peso" => TextosBot.PreguntaPeso(referencia),
        "reps" => TextosBot.PreguntaReps(referencia),
        "tiempo" => TextosBot.PreguntaTiempo(referencia),
        _ => TextosBot.Reintento("¿Me lo repetís de otra manera?"),
    };

    // Re-pregunta en reintento: re-deriva TODO determinísticamente (incluida la
    // lista de sugerencias), sin nueva llamada al LLM.
    private async Task<string> PreguntaReintentoAsync(AclaracionPendiente aclaracion, CancellationToken cancellationToken)
    {
        var contexto = ParsearContexto(aclaracion.Contexto);

        if (aclaracion.Campo == "ejercicio")
        {
            if (contexto.Crudo is not { Length: > 0 })
            {
                return TextosBot.PreguntaEjercicio(null, []);
            }

            var resolucion = CatalogoEjercicioResolver.Resolver(contexto.Crudo, await catalogo.LeerAsync(cancellationToken));
            var nombres = resolucion.Sugerencias.Select(s => s.NombreCanonico).ToList();
            return resolucion.Estado == EstadoResolucion.Ambiguo
                ? TextosBot.PreguntaEjercicioAmbiguo(nombres)
                : TextosBot.PreguntaEjercicio(contexto.Crudo, nombres);
        }

        var filas = await FilasDelContextoAsync(contexto, aclaracion.SesionId, cancellationToken);
        var ejercicio = filas.Count > 0 && filas[0].EjercicioId is int id
            ? (await catalogo.LeerAsync(cancellationToken)).FirstOrDefault(e => e.Id == id)
            : null;
        var referencia = ejercicio?.NombreCanonico ?? filas.FirstOrDefault()?.EjercicioCrudo ?? "esa serie";
        return PreguntaPor(aclaracion.Campo, referencia);
    }

    // Sugerencias (con ids) re-derivadas del crudo guardado: misma lista
    // determinística que ofreció la pregunta original.
    private async Task<List<SugerenciaEjercicio>> SugerenciasDeContextoAsync(string? crudo, CancellationToken cancellationToken)
    {
        if (crudo is not { Length: > 0 })
        {
            return [];
        }

        var resolucion = CatalogoEjercicioResolver.Resolver(crudo, await catalogo.LeerAsync(cancellationToken));
        return [.. resolucion.Sugerencias];
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

    // Fija el ejercicio en TODAS las filas del lote y en la sesión (un ejercicio
    // nuevo reinicia la numeración de series); después continúa el lote.
    private async Task<CapturaRespuesta> FijarEjercicioAsync(
        SesionCaptura sesion,
        List<RegistroParcial> filas,
        IReadOnlyList<SugerenciaEjercicio> opciones,
        int indiceElegido,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var elegido = opciones[indiceElegido - 1];

        foreach (var fila in filas)
        {
            fila.EjercicioId = elegido.Id;
            fila.DescripcionPendiente = false;
            // EjercicioCrudo se conserva: es lo que dijo el alumno (auditoría).
        }

        if (sesion.EjercicioActivoId != elegido.Id)
        {
            sesion.EjercicioActivoId = elegido.Id;
            sesion.OrdenSerieSiguiente = 1;
        }

        QuitarAclaracion(sesion);

        if (filas.Count == 0)
        {
            // Alta "de palabra" resuelta: la pregunta viva se cierra y el flujo
            // queda libre para la próxima serie.
            sesion.Estado = EstadoSesionCaptura.EnCurso;
            var leido = (await catalogo.LeerAsync(cancellationToken)).FirstOrDefault(e => e.Id == elegido.Id);
            return new(
                TextosBot.EjercicioAnotado(elegido.NombreCanonico, porTiempo: leido?.UsaTiempo is true),
                sesion.Id,
                AclaracionCreada: false);
        }

        return await ContinuarLoteAsync(sesion, filas, ahora, cancellationToken);
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

    // "12 reps con 45 kg" / "12 reps al cuerpo" / "60 s": lo anotado, tal cual.
    private static string DetalleSerie(RegistroParcial fila)
    {
        if (fila.Unidad == Unidad.Tiempo && fila.TiempoSeg is { } segundos)
        {
            return $"{segundos} s";
        }

        if (fila.PesoKg is { } peso && fila.Unidad != Unidad.PesoCorporal)
        {
            var pesoTexto = peso.ToString("0.##", CultureInfo.InvariantCulture);
            return fila.Reps is { } reps ? $"{reps} reps con {pesoTexto} kg" : $"con {pesoTexto} kg";
        }

        if (fila.Unidad == Unidad.PesoCorporal)
        {
            return fila.Reps is { } repsCuerpo ? $"{repsCuerpo} reps al cuerpo" : "al cuerpo";
        }

        return fila.Reps is { } repsSolos ? $"{repsSolos} reps" : "sin datos";
    }
}
