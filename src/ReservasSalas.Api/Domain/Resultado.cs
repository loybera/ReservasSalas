using System.Diagnostics.CodeAnalysis;

namespace ReservasSalas.Api.Domain;

public enum TipoError
{
    Validacion,
    NoEncontrado,
    Conflicto,
    Prohibido
}

/// <summary>
/// Error esperado del negocio. Los errores inesperados siguen siendo excepciones
/// y los resuelve el manejador global (500 con Problem Details).
/// </summary>
public sealed record Error(TipoError Tipo, string Detalle, IReadOnlyDictionary<string, string[]>? Campos = null)
{
    public static Error Validacion(IReadOnlyDictionary<string, string[]> campos) =>
        new(TipoError.Validacion, "La solicitud tiene datos inválidos.", campos);

    public static Error NoEncontrado(string detalle) => new(TipoError.NoEncontrado, detalle);

    public static Error Conflicto(string detalle) => new(TipoError.Conflicto, detalle);

    public static Error Prohibido(string detalle) => new(TipoError.Prohibido, detalle);
}

public sealed class Resultado<T>
{
    private Resultado(T? valor, Error? error)
    {
        Valor = valor;
        Error = error;
    }

    public T? Valor { get; }

    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Valor))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool EsExito => Error is null;

    public static Resultado<T> Exito(T valor) => new(valor, null);

    public static Resultado<T> Fallo(Error error) => new(default, error);

    public static implicit operator Resultado<T>(Error error) => Fallo(error);
}
