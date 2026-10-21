var builder = WebApplication.CreateBuilder(args);

// Registro de servicios — cada feature agrega lo suyo desde Features/<Nombre>/.
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Ping para verificar el scaffold; los endpoints reales entran por vertical slices.
app.MapGet("/", () => "WorkoutAgentic API");

app.Run();
