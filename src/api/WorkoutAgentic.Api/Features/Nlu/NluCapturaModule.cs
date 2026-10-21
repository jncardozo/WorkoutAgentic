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
        services.AddHttpClient(HttpClientName);

        services.AddSingleton<ISpeechToText>(sp => new GeminiSpeechToText(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            sp.GetRequiredService<IOptions<GeminiOptions>>(),
            sp.GetRequiredService<ILogger<GeminiSpeechToText>>()));

        services.AddScoped<GeminiNlu>();
        services.AddScoped<IIntentClassifier>(sp => sp.GetRequiredService<GeminiNlu>());
        services.AddScoped<IEntityExtractor>(sp => sp.GetRequiredService<GeminiNlu>());

        services.AddScoped<IWhatsappCatalogoEjercicios, EjercicioCatalogoSource>();
        services.AddScoped<IMensajesNlu, WhatsappCapturaPipeline>();

        return services;
    }
}
