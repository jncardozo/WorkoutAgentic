using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.PlanillaApi;
using WorkoutAgentic.Api.Features.WhatsappMedia;
using WorkoutAgentic.Api.Features.WhatsappSender;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

var builder = WebApplication.CreateBuilder(args);

// Registro de servicios — cada feature agrega lo suyo desde Features/<Nombre>/.
builder.Services.AddOpenApi();
builder.Services.AddCapturasDb(builder.Configuration);
builder.Services.AddWhatsappWebhook();
builder.Services.AddWhatsappSender();
builder.Services.AddWhatsappMedia();
builder.Services.AddNluCaptura();
builder.Services.AddCapturaSesion();
builder.Services.AddPlanillaApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Redirección HTTPS solo fuera de Development: en dev el proxy de la SPA y
// los túneles del webhook hablan http directo (el perfil https queda
// disponible para pruebas manuales).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Los endpoints reales entran por vertical slices.
app.MapWhatsappWebhook();
app.MapPlanillaApi();
app.MapGet("/", () => "WorkoutAgentic API");

app.Run();

public partial class Program;
