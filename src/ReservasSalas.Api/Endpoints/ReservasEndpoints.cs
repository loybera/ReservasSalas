using Microsoft.AspNetCore.Mvc;
using ReservasSalas.Api.Contracts;
using ReservasSalas.Api.Services;

namespace ReservasSalas.Api.Endpoints;

public static class ReservasEndpoints
{
    public const string HeaderUsuario = "X-User-Email";

    public static IEndpointRouteBuilder MapReservasEndpoints(this IEndpointRouteBuilder app)
    {
        var reservas = app.MapGroup("/api/reservas").WithTags("Reservas");

        reservas.MapPost("/", CrearAsync)
            .WithSummary("Crea una reserva")
            .Produces<ReservaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        reservas.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene una reserva")
            .Produces<ReservaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        reservas.MapDelete("/{id:guid}", CancelarAsync)
            .WithSummary("Cancela una reserva (solo el organizador)")
            .WithDescription($"El usuario se identifica con el header {HeaderUsuario}.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CrearAsync(
        CrearReservaRequest request,
        ReservaService servicio,
        CancellationToken cancellationToken)
    {
        var resultado = await servicio.CrearAsync(request, cancellationToken);

        return resultado.EsExito
            ? TypedResults.Created($"/api/reservas/{resultado.Valor.Id}", ReservaResponse.Desde(resultado.Valor))
            : resultado.Error.ToProblem();
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ReservaService servicio, CancellationToken cancellationToken)
    {
        var resultado = await servicio.ObtenerAsync(id, cancellationToken);

        return resultado.EsExito
            ? TypedResults.Ok(ReservaResponse.Desde(resultado.Valor))
            : resultado.Error.ToProblem();
    }

    private static async Task<IResult> CancelarAsync(
        Guid id,
        [FromHeader(Name = HeaderUsuario)] string? usuario,
        ReservaService servicio,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario))
        {
            return TypedResults.Problem(
                detail: $"Falta identificar al usuario con el header {HeaderUsuario}.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Usuario no identificado");
        }

        var resultado = await servicio.CancelarAsync(id, usuario, cancellationToken);

        return resultado.EsExito
            ? TypedResults.NoContent()
            : resultado.Error.ToProblem();
    }
}
