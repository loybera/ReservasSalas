using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ReservasSalas.Api.Data;

/// <summary>
/// Aplica Data/Schema.sql al arrancar. Para este ejercicio alcanza; en un sistema real
/// usaría migraciones de EF Core ejecutadas como un paso separado del despliegue
/// (ver "Decisiones" en el README).
/// </summary>
public static class DatabaseInitializer
{
    private const int MaximoIntentos = 5;
    private const string RecursoEsquema = "ReservasSalas.Api.Data.Schema.sql";

    public static async Task InicializarAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservasDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        var esquema = await LeerEsquemaAsync(cancellationToken);

        for (var intento = 1; ; intento++)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(esquema, cancellationToken);
                logger.LogInformation("Esquema de base de datos verificado");
                return;
            }
            catch (NpgsqlException ex) when (intento < MaximoIntentos)
            {
                logger.LogWarning(ex, "La base de datos no respondió (intento {Intento} de {Maximo}); se reintenta", intento, MaximoIntentos);
                await Task.Delay(TimeSpan.FromSeconds(2 * intento), cancellationToken);
            }
        }
    }

    private static async Task<string> LeerEsquemaAsync(CancellationToken cancellationToken)
    {
        await using var stream = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream(RecursoEsquema)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido {RecursoEsquema}.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
