using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.Alumnos;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.CapturaTexto;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.PlanillaApi;

var builder = WebApplication.CreateBuilder(args);

// Registro de servicios — cada feature agrega lo suyo desde Features/<Nombre>/.
builder.Services.AddOpenApi();
builder.Services.AddCapturasDb(builder.Configuration);
builder.Services.AddNluCaptura();
builder.Services.AddCapturaSesion();
builder.Services.AddPlanillaApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Redirección HTTPS solo fuera de Development; en dev el proxy de la SPA usa http.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Los endpoints reales entran por vertical slices.
app.MapCapturaTexto();
app.MapPlanillaApi();
app.MapAlumnos();
app.MapGet("/", () => "WorkoutAgentic API");

app.Run();

public partial class Program;
