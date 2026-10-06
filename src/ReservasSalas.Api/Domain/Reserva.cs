namespace ReservasSalas.Api.Domain;

public enum ResultadoCancelacion
{
    Cancelada,
    YaEstabaCancelada,
    NoEsElOrganizador
}

public sealed class Reserva
{
    public static readonly TimeSpan DuracionMinima = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DuracionMaxima = TimeSpan.FromHours(4);

    // Constructor para EF Core.
    private Reserva()
    {
    }

    public Guid Id { get; private set; }

    public Guid SalaId { get; private set; }

    /// <summary>Siempre en UTC (offset cero).</summary>
    public DateTimeOffset Inicio { get; private set; }

    /// <summary>Siempre en UTC. El intervalo es [Inicio, Fin): el fin no se incluye.</summary>
    public DateTimeOffset Fin { get; private set; }

    public string OrganizadorEmail { get; private set; } = string.Empty;

    public int Asistentes { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public DateTimeOffset? CanceladaEn { get; private set; }

    public bool EstaCancelada => CanceladaEn is not null;

    /// <summary>
    /// Valida las reglas que dependen solo de los datos de la reserva.
    /// La regla de no superposición NO se valida acá: la garantiza la base de datos
    /// con una restricción de exclusión, que es lo único seguro ante requests simultáneos.
    /// </summary>
    public static Resultado<Reserva> Crear(
        Sala sala,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        string? organizadorEmail,
        int asistentes,
        DateTimeOffset ahora)
    {
        var errores = new Dictionary<string, string[]>();

        if (fin <= inicio)
        {
            errores["fin"] = ["La hora de fin debe ser posterior a la de inicio."];
        }
        else
        {
            var duracion = fin - inicio;
            if (duracion < DuracionMinima || duracion > DuracionMaxima)
            {
                errores["fin"] = ["La reserva debe durar entre 15 minutos y 4 horas."];
            }
        }

        if (inicio < ahora)
        {
            errores["inicio"] = ["No se puede reservar en el pasado."];
        }

        var email = Email.Normalizar(organizadorEmail);
        if (!Email.EsValido(email))
        {
            errores["organizadorEmail"] = ["El email del organizador no es válido."];
        }

        if (asistentes <= 0)
        {
            errores["asistentes"] = ["Debe haber al menos un asistente."];
        }
        else if (asistentes > sala.Capacidad)
        {
            errores["asistentes"] = [$"La sala admite como máximo {sala.Capacidad} personas."];
        }

        if (errores.Count > 0)
        {
            return Error.Validacion(errores);
        }

        return Resultado<Reserva>.Exito(new Reserva
        {
            Id = Guid.CreateVersion7(),
            SalaId = sala.Id,
            Inicio = inicio.ToUniversalTime(),
            Fin = fin.ToUniversalTime(),
            OrganizadorEmail = email,
            Asistentes = asistentes,
            CreadaEn = ahora.ToUniversalTime()
        });
    }

    /// <summary>Cancelación lógica: la reserva queda registrada pero libera el horario.</summary>
    public ResultadoCancelacion Cancelar(string? emailSolicitante, DateTimeOffset ahora)
    {
        if (Email.Normalizar(emailSolicitante) != OrganizadorEmail)
        {
            return ResultadoCancelacion.NoEsElOrganizador;
        }

        if (EstaCancelada)
        {
            return ResultadoCancelacion.YaEstabaCancelada;
        }

        CanceladaEn = ahora.ToUniversalTime();
        return ResultadoCancelacion.Cancelada;
    }
}
