using System.Net.Mail;

namespace ReservasSalas.Api.Domain;

internal static class Email
{
    public const int LargoMaximo = 254;

    public static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Espera un email ya normalizado. Rechaza formatos con nombre ("Ana &lt;ana@x.com&gt;").</summary>
    public static bool EsValido(string email) =>
        email.Length is > 0 and <= LargoMaximo
        && MailAddress.TryCreate(email, out var direccion)
        && direccion.Address == email;
}
