using Microsoft.Extensions.Logging.Abstractions;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.WhatsappSender;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Tests.Features.CapturaSesion;

// Orquestación del pipeline de sesión con falsos del NLU, del servicio y del
// sender: respuesta → destino del alumno, mensaje original citado, y referencia
// del id del bot dentro de la pregunta viva cuando la hubo.
public class CapturaSesionPipelineTests
{
    private const string Telefono = "5215512345678";
    private const string MensajeId = "wa-entrada-1";
    private static readonly AnalisisCaptura AnalisisVigente =
        new(
            WhatsappMessageKind.Text,
            "hice sillón",
            Transcripcion: null,
            ClasificacionIntent.Ok(IntentoCaptura.CargaEjercicio, 0.9),
            Entidades: null,
            ResolucionEjercicio: null);

    private static readonly CapturaRespuesta RespuestaBase = new("Tu serie quedó anotada.", 7, AclaracionCreada: false);
    private static readonly CapturaRespuesta RespuestaConAclaracion = new("¿Con cuánto peso?", 7, AclaracionCreada: true);

    [Fact]
    public async Task AnalisisFallido_NoEnviaNada()
    {
        var nlu = new StubNlu(null);
        var servicio = new StubServicio(RespuestaBase);
        var sender = new StubSender();
        var pipeline = new CapturaSesionPipeline(nlu, servicio, sender, LoggerFalso);

        await Ejecutar(pipeline);

        Assert.False(servicio.Aplico);
        Assert.Empty(sender.Enviados);
    }

    [Fact]
    public async Task Respuesta_SaleAlAlumnoCitandoSuMensaje()
    {
        var nlu = new StubNlu(AnalisisVigente);
        var servicio = new StubServicio(RespuestaBase);
        var sender = new StubSender();
        var pipeline = new CapturaSesionPipeline(nlu, servicio, sender, LoggerFalso);

        await Ejecutar(pipeline);

        Assert.True(servicio.Aplico);
        var envio = Assert.Single(sender.Enviados);
        Assert.Equal(Telefono, envio.To);
        Assert.Equal("Tu serie quedó anotada.", envio.Body);
        Assert.Equal(MensajeId, envio.ReplyToMessageId);
        Assert.Null(servicio.SesionMarcada); // sin aclaración no hay marca
    }

    [Fact]
    public async Task AclaracionCreada_QuedaReferenciadaAlMensajeDelBot()
    {
        var nlu = new StubNlu(AnalisisVigente);
        var servicio = new StubServicio(RespuestaConAclaracion);
        var sender = new StubSender();
        var pipeline = new CapturaSesionPipeline(nlu, servicio, sender, LoggerFalso);

        await Ejecutar(pipeline);

        Assert.Equal((7, "bot-1"), (servicio.SesionMarcada, servicio.MensajeBotMarcado));
    }

    [Fact]
    public async Task EnvioFallido_DevuelveSinCrashYSinMarcar()
    {
        var nlu = new StubNlu(AnalisisVigente);
        var servicio = new StubServicio(RespuestaConAclaracion);
        var sender = new StubSender
        {
            Resultado = new(Success: false, MessageId: null, ErrorCode: 470, ErrorTitle: "Sin saldo", ErrorDetails: null, Retriable: false),
        };
        var pipeline = new CapturaSesionPipeline(nlu, servicio, sender, LoggerFalso);

        await Ejecutar(pipeline);

        Assert.Single(sender.Enviados);
        Assert.Null(servicio.SesionMarcada);
    }

    private static Task Ejecutar(CapturaSesionPipeline pipeline) =>
        pipeline.ProcesarAsync(
            new IncomingWhatsappMessage(
                MensajeId,
                Telefono,
                WhatsappMessageKind.Text,
                "hice sillón",
                null, null, null, null),
            CancellationToken.None);

    private static readonly NullLogger<CapturaSesionPipeline> LoggerFalso =
        NullLogger<CapturaSesionPipeline>.Instance;

    private sealed class StubNlu(AnalisisCaptura? analisis) : IMensajesNlu
    {
        public Task<AnalisisCaptura?> AnalizarAsync(IncomingWhatsappMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(analisis);
    }

    private sealed class StubServicio(CapturaRespuesta respuesta) : ISesionCapturaServicio
    {
        public bool Aplico { get; private set; }

        public int? SesionMarcada { get; private set; }

        public string? MensajeBotMarcado { get; private set; }

        public Task<CapturaRespuesta> AplicarAsync(
            IncomingWhatsappMessage mensaje, AnalisisCaptura analisis, DateTime ahora, CancellationToken cancellationToken)
        {
            Aplico = true;
            return Task.FromResult(respuesta);
        }

        public Task MarcarMensajeBotAsync(int sesionId, string? mensajeBotWaId, CancellationToken cancellationToken)
        {
            SesionMarcada = sesionId;
            MensajeBotMarcado = mensajeBotWaId;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSender : IWhatsappSender
    {
        public List<WhatsappOutboundText> Enviados { get; } = [];

        public WhatsappSendResult Resultado { get; set; } = WhatsappSendResult.Exito("bot-1");

        public Task<WhatsappSendResult> SendTextAsync(WhatsappOutboundText message, CancellationToken cancellationToken)
        {
            Enviados.Add(message);
            return Task.FromResult(Resultado);
        }
    }
}
