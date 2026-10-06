using Microsoft.EntityFrameworkCore;
using ReservasSalas.Api.Data;
using ReservasSalas.Api.Endpoints;
using ReservasSalas.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Logs estructurados en JSON fuera de desarrollo (en desarrollo, formato legible).
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(opciones =>
    {
        opciones.IncludeScopes = true;
        opciones.UseUtcTimestamp = true;
        opciones.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    });
}

// La cadena de conexión se lee al crear el DbContext (no al arrancar), así los tests de
// integración pueden reemplazarla.
builder.Services.AddDbContext<ReservasDbContext>((servicios, opciones) =>
{
    var cadena = servicios.GetRequiredService<IConfiguration>().GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default.");
    opciones.UseNpgsql(cadena);
});

// El reloj se inyecta para poder controlar "ahora" en los tests.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SalaService>();
builder.Services.AddScoped<ReservaService>();

builder.Services.AddProblemDetails(opciones =>
    opciones.CustomizeProblemDetails = contexto =>
        contexto.ProblemDetails.Extensions["traceId"] = contexto.HttpContext.TraceIdentifier);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<ReservasDbContext>("postgres");

var app = builder.Build();

// Excepciones no controladas y códigos de error sin cuerpo → Problem Details.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapSalasEndpoints();
app.MapReservasEndpoints();

if (app.Configuration.GetValue("Database:InicializarAlArrancar", true))
{
    await DatabaseInitializer.InicializarAsync(app.Services, app.Lifetime.ApplicationStopping);
}

app.Run();

// Visible para WebApplicationFactory<Program> en los tests de integración.
public partial class Program
{
}
