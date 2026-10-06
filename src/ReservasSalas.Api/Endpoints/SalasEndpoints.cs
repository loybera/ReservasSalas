using ReservasSalas.Api.Contracts;
using ReservasSalas.Api.Services;

namespace ReservasSalas.Api.Endpoints;

public static class SalasEndpoints
{
    public static IEndpointRouteBuilder MapSalasEndpoints(this IEndpointRouteBuilder app)
    {
        var salas = app.MapGroup("/api/salas").WithTags("Salas");

        salas.MapPost("/", CrearAsync)
            .WithSummary("Crea una sala")
            .Produces<SalaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        salas.MapGet("/", ListarAsync)
            .WithSummary("Lista las salas")
            .Produces<IReadOnlyList<SalaResponse>>();

        salas.MapGet("/{salaId:guid}/reservas", ListarReservasDelDiaAsync)
            .WithSummary("Reservas activas de una sala en un día")
            .WithDescription("fecha en formato yyyy-MM-dd. zonaHoraria es opcional (IANA, por defecto UTC).")
            .Produces<IReadOnlyList<ReservaResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CrearAsync(
        CrearSalaRequest request,
        SalaService servicio,
        CancellationToken cancellationToken)
    {
        var resultado = await servicio.CrearAsync(request, cancellationToken);

        return resultado.EsExito
            ? TypedResults.Created($"/api/salas/{resultado.Valor.Id}", SalaResponse.Desde(resultado.Valor))
            : resultado.Error.ToProblem();
    }

    private static async Task<IResult> ListarAsync(SalaService servicio, CancellationToken cancellationToken)
    {
        var salas = await servicio.ListarAsync(cancellationToken);
        return TypedResults.Ok(salas.Select(SalaResponse.Desde).ToList());
    }

    private static async Task<IResult> ListarReservasDelDiaAsync(
        Guid salaId,
        DateOnly fecha,
        string? zonaHoraria,
        ReservaService servicio,
        CancellationToken cancellationToken)
    {
        var resultado = await servicio.ListarDelDiaAsync(salaId, fecha, zonaHoraria, cancellationToken);

        return resultado.EsExito
            ? TypedResults.Ok(resultado.Valor.Select(ReservaResponse.Desde).ToList())
            : resultado.Error.ToProblem();
    }
}
