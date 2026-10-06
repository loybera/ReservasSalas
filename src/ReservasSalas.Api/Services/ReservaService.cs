using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReservasSalas.Api.Contracts;
using ReservasSalas.Api.Data;
using ReservasSalas.Api.Domain;

namespace ReservasSalas.Api.Services;

public sealed class ReservaService(ReservasDbContext db, TimeProvider reloj, ILogger<ReservaService> logger)
{
    public async Task<Resultado<Reserva>> CrearAsync(CrearReservaRequest request, CancellationToken cancellationToken)
    {
        var sala = await db.Salas
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SalaId, cancellationToken);

        if (sala is null)
        {
            return Error.NoEncontrado($"No existe la sala {request.SalaId}.");
        }

        var resultado = Reserva.Crear(
            sala,
            request.Inicio,
            request.Fin,
            request.OrganizadorEmail,
            request.Asistentes,
            reloj.GetUtcNow());

        if (!resultado.EsExito)
        {
            return resultado;
        }

        var reserva = resultado.Valor;
        db.Reservas.Add(reserva);

        try
        {
            // No se consulta antes si hay superposición: entre esa consulta y el INSERT
            // otro request podría reservar el mismo horario. La restricción de exclusión
            // de PostgreSQL decide de forma atómica.
            await db.SaveChangesAsync(cancellationToken);
        }
        // 23P01: la restricción rechazó la fila. 40P01: dos inserciones superpuestas simultáneas
        // se esperaron mutuamente y PostgreSQL abortó una; también es un conflicto de horario.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.DeadlockDetected
        })
        {
            logger.LogInformation(
                "Reserva rechazada por superposición en la sala {SalaId} entre {Inicio} y {Fin}",
                reserva.SalaId, reserva.Inicio, reserva.Fin);

            return Error.Conflicto("La sala ya tiene una reserva que se superpone con ese horario.");
        }

        // Sin el email en el log: es un dato personal y no hace falta para diagnosticar.
        logger.LogInformation(
            "Reserva {ReservaId} creada en la sala {SalaId} entre {Inicio} y {Fin}",
            reserva.Id, reserva.SalaId, reserva.Inicio, reserva.Fin);

        return resultado;
    }

    public async Task<Resultado<Reserva>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var reserva = await db.Reservas
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return reserva is null
            ? Error.NoEncontrado($"No existe la reserva {id}.")
            : Resultado<Reserva>.Exito(reserva);
    }

    public async Task<Resultado<Reserva>> CancelarAsync(Guid id, string emailSolicitante, CancellationToken cancellationToken)
    {
        var reserva = await db.Reservas.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reserva is null)
        {
            return Error.NoEncontrado($"No existe la reserva {id}.");
        }

        switch (reserva.Cancelar(emailSolicitante, reloj.GetUtcNow()))
        {
            case ResultadoCancelacion.NoEsElOrganizador:
                logger.LogWarning("Intento de cancelar la reserva {ReservaId} por alguien que no es el organizador", id);
                return Error.Prohibido("Solo el organizador puede cancelar la reserva.");

            case ResultadoCancelacion.YaEstabaCancelada:
                // Idempotente: cancelar dos veces no es un error.
                return Resultado<Reserva>.Exito(reserva);

            default:
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Reserva {ReservaId} cancelada", id);
                return Resultado<Reserva>.Exito(reserva);
        }
    }

    /// <summary>
    /// Reservas activas de una sala que tocan el día indicado, en la zona horaria pedida
    /// (por defecto UTC). Incluye las que empiezan el día anterior y terminan ese día.
    /// </summary>
    public async Task<Resultado<IReadOnlyList<Reserva>>> ListarDelDiaAsync(
        Guid salaId,
        DateOnly fecha,
        string? zonaHoraria,
        CancellationToken cancellationToken)
    {
        var zona = TimeZoneInfo.Utc;
        if (!string.IsNullOrWhiteSpace(zonaHoraria))
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(zonaHoraria, out var encontrada))
            {
                return Error.Validacion(new Dictionary<string, string[]>
                {
                    ["zonaHoraria"] = [$"Zona horaria desconocida: \"{zonaHoraria}\". Usa un identificador IANA, por ejemplo America/Santiago."]
                });
            }

            zona = encontrada;
        }

        if (!await db.Salas.AnyAsync(s => s.Id == salaId, cancellationToken))
        {
            return Error.NoEncontrado($"No existe la sala {salaId}.");
        }

        var desde = InicioDelDiaEnUtc(fecha, zona);
        var hasta = InicioDelDiaEnUtc(fecha.AddDays(1), zona);

        var reservas = await db.Reservas
            .AsNoTracking()
            .Where(r => r.SalaId == salaId
                        && r.CanceladaEn == null
                        && r.Inicio < hasta
                        && r.Fin > desde)
            .OrderBy(r => r.Inicio)
            .ToListAsync(cancellationToken);

        return Resultado<IReadOnlyList<Reserva>>.Exito(reservas);
    }

    private static DateTimeOffset InicioDelDiaEnUtc(DateOnly fecha, TimeZoneInfo zona)
    {
        var medianocheLocal = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(medianocheLocal, zona.GetUtcOffset(medianocheLocal)).ToUniversalTime();
    }
}
