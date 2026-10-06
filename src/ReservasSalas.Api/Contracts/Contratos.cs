using ReservasSalas.Api.Domain;

namespace ReservasSalas.Api.Contracts;

// DTOs separados de las entidades: la API no expone el modelo de persistencia.

public sealed record CrearSalaRequest(string? Nombre, int Capacidad);

public sealed record SalaResponse(Guid Id, string Nombre, int Capacidad)
{
    public static SalaResponse Desde(Sala sala) => new(sala.Id, sala.Nombre, sala.Capacidad);
}

/// <param name="Inicio">Fecha y hora con zona horaria, en ISO 8601. Ej: 2026-10-06T09:00:00-03:00.</param>
public sealed record CrearReservaRequest(
    Guid SalaId,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string? OrganizadorEmail,
    int Asistentes);

public sealed record ReservaResponse(
    Guid Id,
    Guid SalaId,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string OrganizadorEmail,
    int Asistentes,
    DateTimeOffset CreadaEn,
    DateTimeOffset? CanceladaEn)
{
    public static ReservaResponse Desde(Reserva reserva) => new(
        reserva.Id,
        reserva.SalaId,
        reserva.Inicio,
        reserva.Fin,
        reserva.OrganizadorEmail,
        reserva.Asistentes,
        reserva.CreadaEn,
        reserva.CanceladaEn);
}
