using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.Legal;

public sealed record AgreementView(string Version, string Title, string Text, string Hash, bool Available);

public sealed class AgreementDocument
{
    public AgreementView Current { get; }

    public AgreementDocument(IConfiguration configuration)
    {
        const string version = "2026-11-19.1";
        const string resourceName = "WorkoutAgentic.Agreement.v1.txt";
        using var stream = typeof(AgreementDocument).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("No se encontró el documento del acuerdo.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd().Replace("\r\n", "\n").Replace("{{Version}}", version);
        var available = true;
        foreach (var key in new[] { "OwnerName", "OwnerAddress", "ContactEmail" })
        {
            var value = configuration[$"Legal:{key}"]?.Trim();
            available &= !string.IsNullOrWhiteSpace(value);
            text = text.Replace("{{" + key + "}}", string.IsNullOrWhiteSpace(value) ? "[Dato pendiente de configuración]" : value);
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        Current = new(version, "Acuerdo de confidencialidad y condiciones de prueba", text, hash, available);
    }

    public Task<bool> HasAcceptedAsync(CapturasDbContext db, string userId, CancellationToken cancellationToken = default) =>
        Current.Available ? db.AgreementAcceptances.AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.DocumentHash == Current.Hash && a.Version == Current.Version, cancellationToken)
            : Task.FromResult(false);

    public static IResult AcceptanceRequired() => Results.Problem(statusCode: 403,
        title: "Aceptá el acuerdo de confidencialidad y las condiciones de prueba para continuar.",
        extensions: new Dictionary<string, object?> { ["code"] = "agreement_required" });
}
