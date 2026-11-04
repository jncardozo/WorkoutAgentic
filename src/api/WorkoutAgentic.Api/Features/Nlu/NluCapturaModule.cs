using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkoutAgentic.Api.Features.Nlu;

// Registro del slice NLU (patrón módulo, docs/conventions/dotnet.md).
// El pipeline va SCOPED: CapturaWorker crea un scope por mensaje, de modo que
// el memo del GeminiNlu (una llamada de LLM por mensaje) no cruza mensajes.
internal static class NluCapturaModule
{
    public const string HttpClientName = "gemini";

    public static IServiceCollection AddNluCaptura(this IServiceCollection services)
    {
        services.AddOptions<GeminiOptions>().BindConfiguration(GeminiOptions.SectionName);
        // Los modelos con thinking pueden exceder el default de HttpClient
        // (100 s) en payloads grandes; el cliente de captura tolera llamadas
        // de hasta ~170 s, así que el timeout del proveedor va por debajo.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(150));

        services.AddScoped<GeminiNlu>();
        services.AddScoped<IIntentClassifier>(sp => sp.GetRequiredService<GeminiNlu>());
        services.AddScoped<IEntityExtractor>(sp => sp.GetRequiredService<GeminiNlu>());

        services.AddScoped<IMensajesNlu, CapturaTextoNlu>();

        return services;
    }
}
