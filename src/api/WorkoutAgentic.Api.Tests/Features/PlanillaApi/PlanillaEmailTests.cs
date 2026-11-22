using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.PlanillaApi;

public sealed class PlanillaEmailTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-email-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public PlanillaEmailTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
            builder.ConfigureServices(services => services.PostConfigure<EmailDeliveryOptions>(options => options.Provider = "smtp"));
            // No test can accidentally use a real configured email provider.
            builder.ConfigureServices(services => services.PostConfigure<CorreoSmtpOptions>(options =>
            {
                options.Host = "";
                options.FromAddress = "sender@example.test";
                options.UserName = "";
                options.Password = "";
                options.EnableSsl = false;
            }));
        });
    }

    [Theory]
    [InlineData("pdf", "application/pdf", "pdf")]
    [InlineData("excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")]
    public async Task SendEmail_WithHistoricalSheet_SendsCorrectSnapshotAttachment(string format, string contentType, string extension)
    {
        using var smtp = new LocalSmtpServer();
        using var client = await AuthTestClient.ForSeededStudent(_factory);
        var plan = await SeedPlanAsync();
        ConfigureSmtp(smtp.Port);
        var download = await client.GetByteArrayAsync($"/api/planillas/confirmadas/{plan.Id}/{format}?alumnoId={plan.AlumnoId}");

        var response = await client.PostAsJsonAsync($"/api/planillas/confirmadas/{plan.Id}/correo",
            new { alumnoId = plan.AlumnoId, correo = "qa@example.test", formato = format });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await smtp.Message.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains("RCPT TO:<qa@example.test>", smtp.Commands, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(contentType, message);
        Assert.Contains($"planilla-{plan.Id}.{extension}", message);
        // Decode the SMTP MIME attachment and compare it to the download of the
        // historical snapshot. This exercises SmtpClient, not a mocked sender.
        var attachment = Regex.Match(message,
            @"Content-Transfer-Encoding: base64\r\n(?:(?!\r\n\r\n).)*\r\n\r\n(?<data>.*?)\r\n--",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(attachment.Success, "No se encontró el adjunto MIME base64.");
        var bytes = Convert.FromBase64String(attachment.Groups["data"].Value);
        if (format == "pdf") Assert.Equal(download, bytes);
        else
        {
            // XLSX ZIP entry timestamps may differ between generation calls.
            using var sentZip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
            using var downloadedZip = new System.IO.Compression.ZipArchive(new MemoryStream(download));
            foreach (var entry in downloadedZip.Entries)
            {
                using var expected = new StreamReader(entry.Open());
                using var actual = new StreamReader(sentZip.GetEntry(entry.FullName)!.Open());
                Assert.Equal(await expected.ReadToEndAsync(), await actual.ReadToEndAsync());
            }
        }
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        Assert.Equal(40m, (await db.PlanillasConfirmadas.Include(p => p.Filas).SingleAsync()).Filas.Single().PesoKg);
        Assert.Equal(99m, (await db.RegistrosParciales.SingleAsync()).PesoKg);
    }

    [Fact]
    public async Task EmailStatus_WithoutProviderConfiguration_ReturnsFalseWithoutExposingSecrets()
    {
        using var client = await AuthTestClient.ForSeededStudent(_factory);
        var response = await client.GetAsync("/api/planillas/correo/estado");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"configured\":false}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmailStatus_WithSmtpConfiguration_ReturnsTrueWithoutContactingProvider()
    {
        using var client = await AuthTestClient.ForSeededStudent(_factory);
        ConfigureSmtp(25252);
        var response = await client.GetAsync("/api/planillas/correo/estado");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"configured\":true}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmailStatus_WithoutSession_Returns401()
    {
        using var client = AuthTestClient.Anonymous(_factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/planillas/correo/estado")).StatusCode);
    }

    [Fact]
    public async Task SendEmail_WithoutSmtp_Returns503()
    {
        using var client = await AuthTestClient.ForSeededStudent(_factory);
        var plan = await SeedPlanAsync();
        var response = await client.PostAsJsonAsync($"/api/planillas/confirmadas/{plan.Id}/correo",
            new { alumnoId = plan.AlumnoId, correo = "qa@example.test", formato = "pdf" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_WhenSmtpRejectsRecipient_Returns502()
    {
        using var smtp = new LocalSmtpServer(rejectRecipient: true);
        using var client = await AuthTestClient.ForSeededStudent(_factory);
        var plan = await SeedPlanAsync();
        ConfigureSmtp(smtp.Port);
        var response = await client.PostAsJsonAsync($"/api/planillas/confirmadas/{plan.Id}/correo",
            new { alumnoId = plan.AlumnoId, correo = "qa@example.test", formato = "pdf" });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData("invalid", "pdf")]
    [InlineData("qa@example.test", "csv")]
    [InlineData("qa@example.test", null)]
    public async Task SendEmail_WithInvalidInput_Returns400(string email, string? format)
    {
        using var client = await AuthTestClient.ForSeededStudent(_factory);
        var plan = await SeedPlanAsync();
        var response = await client.PostAsJsonAsync($"/api/planillas/confirmadas/{plan.Id}/correo",
            new { alumnoId = plan.AlumnoId, correo = email, formato = format });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_WithForeignSheet_Returns404()
    {
        using var client = await AuthTestClient.ForSeededStudent(_factory);
        var plan = await SeedPlanAsync(foreign: true);
        var response = await client.PostAsJsonAsync($"/api/planillas/confirmadas/{plan.Id}/correo",
            new { alumnoId = 1, correo = "qa@example.test", formato = "pdf" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private void ConfigureSmtp(int port)
    {
        var options = _factory.Services.GetRequiredService<IOptions<CorreoSmtpOptions>>().Value;
        options.Host = "127.0.0.1";
        options.Port = port;
    }

    private async Task<PlanillaConfirmada> SeedPlanAsync(bool foreign = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        var student = foreign ? new Alumno { Nombre = "Otra persona" } : await db.Alumnos.FirstAsync();
        var session = new SesionCaptura { Alumno = student, Estado = EstadoSesionCaptura.Confirmada, UltimaActividadUtc = DateTime.UtcNow };
        var plan = new PlanillaConfirmada
        {
            Alumno = student, Sesion = session, ConfirmadaUtc = DateTime.UtcNow.AddDays(-5),
            Filas = [new PlanillaFila { Orden = 1, EjercicioCanonical = "Sentadilla histórica", NroSerie = 1, Reps = 10, PesoKg = 40, Unidad = Unidad.Kg, Comments = "efectiva", Observations = "Sin molestias" }],
        };
        db.PlanillasConfirmadas.Add(plan);
        db.RegistrosParciales.Add(new RegistroParcial
        {
            Sesion = new SesionCaptura { Alumno = student, Estado = EstadoSesionCaptura.EnCurso, UltimaActividadUtc = DateTime.UtcNow },
            EjercicioCrudo = "Carga actual", NroSerie = 1, Reps = 3, PesoKg = 99, Unidad = Unidad.Kg,
        });
        await db.SaveChangesAsync();
        return plan;
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_dbPath + suffix); }
            catch (IOException) { /* SQLite may still hold a pooled connection. */ }
        }
    }

    // Minimal SMTP receiver bound exclusively to loopback, with no external mail.
    private sealed class LocalSmtpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(30));
        private readonly TaskCompletionSource<string> _message = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _server;
        public List<string> Commands { get; } = [];
        public Task<string> Message => _message.Task;
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public LocalSmtpServer(bool rejectRecipient = false)
        {
            _listener.Start();
            _server = ReceiveAsync(rejectRecipient);
        }

        private async Task ReceiveAsync(bool rejectRecipient)
        {
            try
            {
                using var connection = await _listener.AcceptTcpClientAsync(_stop.Token);
                await using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
                await writer.WriteLineAsync("220 localhost SMTP test");
                while (await reader.ReadLineAsync(_stop.Token) is { } line)
                {
                    Commands.Add(line);
                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                        await writer.WriteLineAsync("250 localhost");
                    else if (line.StartsWith("RCPT", StringComparison.OrdinalIgnoreCase))
                        await writer.WriteLineAsync(rejectRecipient ? "550 Recipient rejected" : "250 OK");
                    else if (line == "DATA")
                    {
                        await writer.WriteLineAsync("354 End with dot");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync(_stop.Token) is { } body && body != ".")
                            data.Append(body.StartsWith("..") ? body[1..] : body).Append("\r\n");
                        await writer.WriteLineAsync("250 Message accepted");
                        _message.TrySetResult(data.ToString());
                    }
                    else if (line == "QUIT")
                    {
                        await writer.WriteLineAsync("221 Bye");
                        break;
                    }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException)
            {
                _message.TrySetException(exception);
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _ = _server.ContinueWith(_ => _stop.Dispose(), TaskScheduler.Default);
        }
    }
}
