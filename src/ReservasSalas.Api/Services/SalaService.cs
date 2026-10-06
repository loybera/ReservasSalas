using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReservasSalas.Api.Contracts;
using ReservasSalas.Api.Data;
using ReservasSalas.Api.Domain;

namespace ReservasSalas.Api.Services;

public sealed class SalaService(ReservasDbContext db, ILogger<SalaService> logger)
{
    public async Task<Resultado<Sala>> CrearAsync(CrearSalaRequest request, CancellationToken cancellationToken)
    {
        var resultado = Sala.Crear(request.Nombre, request.Capacidad);
        if (!resultado.EsExito)
        {
            return resultado;
        }

        db.Salas.Add(resultado.Valor);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Error.Conflicto($"Ya existe una sala llamada \"{resultado.Valor.Nombre}\".");
        }

        logger.LogInformation("Sala {SalaId} creada con capacidad {Capacidad}", resultado.Valor.Id, resultado.Valor.Capacidad);
        return resultado;
    }

    public async Task<IReadOnlyList<Sala>> ListarAsync(CancellationToken cancellationToken) =>
        await db.Salas
            .AsNoTracking()
            .OrderBy(s => s.Nombre)
            .ToListAsync(cancellationToken);
}
