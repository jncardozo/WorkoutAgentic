using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Registro del slice de sesión (máquina de estados + respuestas del bot).
// Este módulo es el ÚNICO que registra el ICapturaPipeline del worker: el
// análisis vive en Features.Nlu (IMensajesNlu) y se compone acá adentro.
internal static class CapturaSesionModule
{
    public static IServiceCollection AddCapturaSesion(this IServiceCollection services)
    {
        services.AddScoped<ISesionCapturaServicio, SesionCapturaServicio>();
        services.AddScoped<ICapturaPipeline, CapturaSesionPipeline>();
        return services;
    }
}
