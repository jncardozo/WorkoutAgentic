using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Features.WhatsappSender;

// Registro del slice de salida (patrón módulo, docs/conventions/dotnet.md).
internal static class WhatsappSenderModule
{
    public const string HttpClientName = "whatsapp-graph";

    // Backoff inline del worker: delays después del intento 1, 2 y 3. El cuarto
    // intento es el último: si falla, se descarta con log. Valores por defecto
    // según arquitectura (reintentos con backoff); tests inyectan delays en 0.
    public static readonly IReadOnlyList<TimeSpan> DefaultRetrySchedule =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
    ];

    public static IServiceCollection AddWhatsappSender(this IServiceCollection services)
    {
        services.AddOptions<WhatsappOptions>().BindConfiguration(WhatsappOptions.SectionName);
        services.AddHttpClient(HttpClientName);

        services.AddSingleton<IWhatsappOutboundQueue, ChannelWhatsappOutboundQueue>();
        services.AddSingleton<IWhatsappSender>(sp => new GraphWhatsappSender(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            sp.GetRequiredService<IOptions<WhatsappOptions>>(),
            sp.GetRequiredService<ILogger<GraphWhatsappSender>>()));
        services.AddSingleton<IReadOnlyList<TimeSpan>>(DefaultRetrySchedule);
        services.AddHostedService<WhatsappDeliveryWorker>();

        return services;
    }
}
