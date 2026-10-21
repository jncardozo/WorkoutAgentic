using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Features.WhatsappMedia;

// Registro del slice de medios (patrón módulo, docs/conventions/dotnet.md).
internal static class WhatsappMediaModule
{
    public const string HttpClientName = "whatsapp-media";

    public static IServiceCollection AddWhatsappMedia(this IServiceCollection services)
    {
        services.AddOptions<WhatsappOptions>().BindConfiguration(WhatsappOptions.SectionName);
        services.AddHttpClient(HttpClientName);

        services.AddSingleton<IWhatsappAudioDownloader>(sp => new GraphWhatsappAudioDownloader(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            sp.GetRequiredService<IOptions<WhatsappOptions>>(),
            sp.GetRequiredService<ILogger<GraphWhatsappAudioDownloader>>()));

        return services;
    }
}
