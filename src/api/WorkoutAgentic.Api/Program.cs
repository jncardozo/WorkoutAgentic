using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.WhatsappSender;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

var builder = WebApplication.CreateBuilder(args);

// Registro de servicios — cada feature agrega lo suyo desde Features/<Nombre>/.
builder.Services.AddOpenApi();
builder.Services.AddCapturasDb(builder.Configuration);
builder.Services.AddWhatsappWebhook();
builder.Services.AddWhatsappSender();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Los endpoints reales entran por vertical slices.
app.MapWhatsappWebhook();
app.MapGet("/", () => "WorkoutAgentic API");

app.Run();

public partial class Program;
