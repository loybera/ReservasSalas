using ReservasSalas.Api.Domain;

namespace ReservasSalas.Api.Endpoints;

/// <summary>Traduce los errores de negocio a respuestas Problem Details (RFC 9457).</summary>
internal static class ErrorHttp
{
    public static IResult ToProblem(this Error error) => error.Tipo switch
    {
        TipoError.Validacion => TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(error.Campos ?? new Dictionary<string, string[]>()),
            detail: error.Detalle,
            title: "Datos inválidos"),

        TipoError.NoEncontrado => TypedResults.Problem(
            detail: error.Detalle,
            statusCode: StatusCodes.Status404NotFound,
            title: "Recurso no encontrado"),

        TipoError.Conflicto => TypedResults.Problem(
            detail: error.Detalle,
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflicto"),

        TipoError.Prohibido => TypedResults.Problem(
            detail: error.Detalle,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Operación no permitida"),

        _ => throw new ArgumentOutOfRangeException(nameof(error), error.Tipo, "Tipo de error no contemplado.")
    };
}
