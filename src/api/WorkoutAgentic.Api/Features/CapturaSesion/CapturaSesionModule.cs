using Microsoft.Extensions.DependencyInjection;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Registro del slice de sesión (máquina de estados + respuesta de captura).
internal static class CapturaSesionModule
{
    public static IServiceCollection AddCapturaSesion(this IServiceCollection services)
    {
        services.AddScoped<ISesionCapturaServicio, SesionCapturaServicio>();
        return services;
    }
}
