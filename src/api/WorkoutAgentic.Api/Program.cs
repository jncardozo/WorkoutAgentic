using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.Auth;
using WorkoutAgentic.Api.Features.Legal;
using Microsoft.AspNetCore.Antiforgery;
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
builder.Services.AddStudentAuth(builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Redirección HTTPS solo fuera de Development; en dev el proxy de la SPA usa http.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        {
            await Results.Problem(statusCode: 400, title: "La sesión del formulario venció. Recargá la página e intentá nuevamente.").ExecuteAsync(context);
            return;
        }
    }
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});

// Los endpoints reales entran por vertical slices.
app.MapCapturaTexto();
app.MapPlanillaApi();
app.MapStudentAuth();
app.MapLegalAgreement();
app.MapGet("/", () => "WorkoutAgentic API");

app.Run();

public partial class Program;
