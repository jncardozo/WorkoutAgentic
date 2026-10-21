namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Punto de registro del slice: opciones (secretos), cola, worker y rutas.
public static class WhatsappWebhookModule
{
    public static IServiceCollection AddWhatsappWebhook(this IServiceCollection services)
    {
        services.AddOptions<WhatsappOptions>()
            .BindConfiguration(WhatsappOptions.SectionName);

        services.AddSingleton<WhatsappSignatureValidator>();
        services.AddSingleton<IWhatsappMessageQueue, ChannelWhatsappMessageQueue>();
        services.AddHostedService<CapturaWorker>();

        return services;
    }

    public static IEndpointRouteBuilder MapWhatsappWebhook(this IEndpointRouteBuilder endpoints)
    {
        // GET (handshake) y POST (eventos) en la MISMA ruta: es la URL del
        // webhook que se configura en el dashboard de Meta.
        var group = endpoints.MapGroup("/api/whatsapp").WithTags("WhatsApp");

        group.MapGet("/", WhatsappWebhookHandlers.Verify).WithName("WhatsappVerify");
        group.MapPost("/", WhatsappWebhookHandlers.Receive).WithName("WhatsappReceive");

        return endpoints;
    }
}
