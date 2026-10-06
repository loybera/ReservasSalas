namespace ReservasSalas.Api.Domain;

public sealed class Sala
{
    public const int LargoMaximoNombre = 100;

    // Constructor para EF Core.
    private Sala()
    {
    }

    public Guid Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public int Capacidad { get; private set; }

    public static Resultado<Sala> Crear(string? nombre, int capacidad)
    {
        var errores = new Dictionary<string, string[]>();
        var nombreLimpio = nombre?.Trim() ?? string.Empty;

        if (nombreLimpio.Length == 0)
        {
            errores["nombre"] = ["El nombre es obligatorio."];
        }
        else if (nombreLimpio.Length > LargoMaximoNombre)
        {
            errores["nombre"] = [$"El nombre no puede superar los {LargoMaximoNombre} caracteres."];
        }

        if (capacidad <= 0)
        {
            errores["capacidad"] = ["La capacidad debe ser mayor que cero."];
        }

        if (errores.Count > 0)
        {
            return Error.Validacion(errores);
        }

        return Resultado<Sala>.Exito(new Sala
        {
            Id = Guid.CreateVersion7(),
            Nombre = nombreLimpio,
            Capacidad = capacidad
        });
    }
}
