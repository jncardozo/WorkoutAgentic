using System.Diagnostics;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.WhatsappMedia;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Tests.Features.Nlu;

// Pipeline etapa-por-etapa con falsos: audio → descarga → STT → NLU → resolver.
// Outputs tipados (`AnalizarMensajeAsync`) para aserción directa, sin logs.
public class WhatsappCapturaPipelineTests
{
    private static readonly IReadOnlyList<CatalogoEjercicio> Catalogo =
        CatalogoEjercicioSeed.Crear()
            .Select((e, i) => new CatalogoEjercicio(
                i + 1, e.NombreCanonico, e.Apodos, e.GrupoMuscular, e.UsaPeso, e.UsaTiempo))
            .ToList();

    [Fact]
    public async Task AudioRecorreTodaLaCadenaHastaResolver()
    {
        var mensaje = MensajeAudio();
        var pipeline = CrearPipeline(
            downloader: new StubDownloader(WhatsappAudioArchivo.Ok([1, 2, 3], "audio/ogg", 3)),
            stt: new StubStt(TranscripcionAudio.Ok("hice remos con 50 y pico")),
            classifiers: new StubClassifier(ClasificacionIntent.Ok(IntentoCaptura.CargaSerie, 0.95)),
            extractor: new StubExtractor(EntidadesExtraidas.Ok(new EntidadesNlu(
                EjercicioTexto: "remos", SerieOrdinal: null, CantidadSeries: null, Reps: null,
                PesoKg: null, Unidad: null, TiempoSegundos: null, DescansoSegundos: null,
                CampoAclarar: "peso", PreguntaAclarar: "¿Con cuánto peso?"))),
            catalogo: new StubCatalogo(Catalogo));

        var analisis = await pipeline.AnalizarAsync(mensaje, CancellationToken.None);

        Assert.NotNull(analisis);
        Assert.Equal(WhatsappMessageKind.Audio, analisis.MensajeKind);
        Assert.Equal("hice remos con 50 y pico", analisis.TextoProcesado);
        Assert.NotNull(analisis.Transcripcion);                   // etapa 1 presente y auditable
        Assert.Equal(IntentoCaptura.CargaSerie, analisis.Clasificacion.Intento);
        Assert.NotNull(analisis.Entidades);
        Assert.Equal(EstadoResolucion.Matcheado, analisis.ResolucionEjercicio!.Estado);
        Assert.Equal("Remo con mancuernas", analisis.ResolucionEjercicio.EjercicioCanonico);
    }

    [Fact]
    public async Task TextoVaDirectoAlNlu_SinTranscripcion()
    {
        var pipeline = CrearPipeline(
            downloader: new StubDownloader(WhatsappAudioArchivo.Fallo("no aplica", "-", false)),
            stt: new StubStt(TranscripcionAudio.Fallo("no aplica", null)),
            classifiers: new StubClassifier(ClasificacionIntent.Ok(IntentoCaptura.CargaEjercicio, 0.8)),
            extractor: new StubExtractor(EntidadesExtraidas.Ok(new EntidadesNlu(
                EjercicioTexto: "el pecho", SerieOrdinal: null, CantidadSeries: null, Reps: 12,
                PesoKg: null, Unidad: null, TiempoSegundos: null, DescansoSegundos: null,
                CampoAclarar: "ejercicio", PreguntaAclarar: null))),
            catalogo: new StubCatalogo(Catalogo));

        var analisis = await pipeline.AnalizarAsync(
            new IncomingWhatsappMessage(
                MessageId: "wamid.1", From: "5215512345678", Kind: WhatsappMessageKind.Text,
                TextBody: "anotame el pecho, la máquina con pulley, 3x12", AudioMediaId: null,
                AudioMimeType: null, AudioDurationSeconds: null, AudioSha256: null),
            CancellationToken.None);

        Assert.NotNull(analisis);
        Assert.Equal(WhatsappMessageKind.Text, analisis.MensajeKind);
        Assert.Null(analisis.Transcripcion);
        Assert.Equal("anotame el pecho, la máquina con pulley, 3x12", analisis.TextoProcesado);
        // NLU §6: "el pecho" → ambigüedad del catálogo con sugerencias.
        Assert.Equal(EstadoResolucion.Ambiguo, analisis.ResolucionEjercicio!.Estado);
        Assert.True(analisis.ResolucionEjercicio.Sugerencias.Count >= 2);
    }

    [Fact]
    public async Task AudioSinMediaId_SeDescarta()
    {
        var downloader = new StubDownloader(WhatsappAudioArchivo.Ok([1], "audio/ogg", 1));
        var pipeline = CrearPipeline(
            downloader,
            new StubStt(TranscripcionAudio.Ok("x")),
            new StubClassifier(ClasificacionIntent.Ok(IntentoCaptura.Invalido, 0)),
            new StubExtractor(EntidadesExtraidas.Fallo("no aplica")),
            new StubCatalogo(Catalogo));

        var analisis = await pipeline.AnalizarAsync(
            new IncomingWhatsappMessage("wamid.2", "5215512345678", WhatsappMessageKind.Audio,
                TextBody: null, AudioMediaId: null, AudioMimeType: null,
                AudioDurationSeconds: 10, AudioSha256: "abc"),
            CancellationToken.None);

        Assert.Null(analisis);
        Assert.Equal(0, downloader.Llamadas);   // sin media id no se intenta descargar
    }

    [Fact]
    public async Task DescargaFallida_SeDescartaSinInventarTexto()
    {
        var pipeline = CrearPipeline(
            downloader: new StubDownloader(WhatsappAudioArchivo.Fallo("Media no disponible", "x", false)),
            stt: new StubTranscripcionSpia(),
            classifiers: new StubClassifier(ClasificacionIntent.Ok(IntentoCaptura.CargaSerie, 0.5)),
            extractor: new StubExtractor(EntidadesExtraidas.Fallo("no aplica")),
            catalogo: new StubCatalogo(Catalogo));

        var analisis = await pipeline.AnalizarAsync(MensajeAudio(), CancellationToken.None);

        Assert.Null(analisis);                  // etapa falló → sin texto inventado
    }

    [Fact]
    public async Task TranscripcionFallida_SeDescarta()
    {
        var pipeline = CrearPipeline(
            downloader: new StubDownloader(WhatsappAudioArchivo.Ok([1], "audio/ogg", 1)),
            stt: new StubStt(TranscripcionAudio.Fallo("Transcripción vacía", null)),
            classifiers: new StubClassifier(ClasificacionIntent.Ok(IntentoCaptura.CargaSerie, 0.5)),
            extractor: new StubExtractor(EntidadesExtraidas.Fallo("no aplica")),
            catalogo: new StubCatalogo(Catalogo));

        var analisis = await pipeline.AnalizarAsync(MensajeAudio(), CancellationToken.None);

        Assert.Null(analisis);
    }

    [Fact]
    public async Task ClasificadorFallido_QuedaRegistradoSinResolverDatos()
    {
        var pipeline = CrearPipeline(
            downloader: new StubDownloader(WhatsappAudioArchivo.Ok([1], "audio/ogg", 1)),
            stt: new StubStt(TranscripcionAudio.Ok("hola que tal")),
            classifiers: new StubClassifier(ClasificacionIntent.Fallo("Fallo de red: timeout")),
            extractor: new StubExtractor(EntidadesExtraidas.Fallo("Fallo de red: timeout")),
            catalogo: new StubCatalogo(Catalogo));

        var analisis = await pipeline.AnalizarAsync(MensajeAudio(), CancellationToken.None);

        Assert.NotNull(analisis);                              // el análisis existe (auditable)
        Assert.False(analisis.Clasificacion.Exito);
        Assert.Equal("Fallo de red: timeout", analisis.Clasificacion.ErrorTitulo);
        Assert.Null(analisis.Entidades);
        Assert.Null(analisis.ResolucionEjercicio);             // sin entidades crudas → sin resolver
    }

    [Fact]
    public async Task OtroTipoDeMensaje_QueNoSeProcesa()
    {
        var pipeline = CrearPipeline(
            downloader: new StubDownloader(WhatsappAudioArchivo.Fallo("no aplica", "-", false)),
            stt: new StubStt(TranscripcionAudio.Fallo("no aplica", null)),
            classifiers: new StubClassifier(ClasificacionIntent.Ok(IntentoCaptura.Invalido, 0)),
            extractor: new StubExtractor(EntidadesExtraidas.Fallo("no aplica")),
            catalogo: new StubCatalogo(Catalogo));

        var analisis = await pipeline.AnalizarAsync(
            new IncomingWhatsappMessage("wamid.3", "5215512345678", WhatsappMessageKind.Other,
                TextBody: null, AudioMediaId: null, AudioMimeType: null,
                AudioDurationSeconds: null, AudioSha256: null),
            CancellationToken.None);

        Assert.Null(analisis);
    }

    private static IncomingWhatsappMessage MensajeAudio() =>
        new("wamid.0", "5215512345678", WhatsappMessageKind.Audio,
            TextBody: null, AudioMediaId: "media-1", AudioMimeType: "audio/ogg; codecs=opus",
            AudioDurationSeconds: 9, AudioSha256: "abc");

    private static WhatsappCapturaPipeline CrearPipeline(
        IWhatsappAudioDownloader downloader,
        ISpeechToText stt,
        IIntentClassifier classifiers,
        IEntityExtractor extractor,
        IWhatsappCatalogoEjercicios catalogo) =>
        new(downloader, stt, classifiers, extractor, catalogo,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsappCapturaPipeline>.Instance);

    private sealed class StubDownloader(WhatsappAudioArchivo resultado) : IWhatsappAudioDownloader
    {
        public int Llamadas { get; private set; }

        public Task<WhatsappAudioArchivo> DescargarAsync(string mediaId, CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(resultado);
        }
    }

    private sealed class StubStt(TranscripcionAudio resultado) : ISpeechToText
    {
        public Task<TranscripcionAudio> TranscribirAsync(byte[] audio, string mimeType, CancellationToken cancellationToken) =>
            Task.FromResult(resultado);
    }

    private sealed class StubTranscripcionSpia : ISpeechToText
    {
        public Task<TranscripcionAudio> TranscribirAsync(byte[] audio, string mimeType, CancellationToken cancellationToken) =>
            throw new UnreachableException("la descarga falló: el STT no debería llamarse");
    }

    private sealed class StubClassifier(ClasificacionIntent resultado) : IIntentClassifier
    {
        public Task<ClasificacionIntent> ClasificarAsync(string texto, CancellationToken cancellationToken) =>
            Task.FromResult(resultado);
    }

    private sealed class StubExtractor(EntidadesExtraidas resultado) : IEntityExtractor
    {
        public Task<EntidadesExtraidas> ExtraerAsync(string texto, CancellationToken cancellationToken) =>
            Task.FromResult(resultado);
    }

    private sealed class StubCatalogo(IReadOnlyList<CatalogoEjercicio> items) : IWhatsappCatalogoEjercicios
    {
        public Task<IReadOnlyList<CatalogoEjercicio>> LeerAsync(CancellationToken cancellationToken) =>
            Task.FromResult(items);
    }
}
