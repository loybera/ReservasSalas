using System.Net;
using System.Net.Http.Json;
using ReservasSalas.Api.Contracts;

namespace ReservasSalas.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class ReservasApiTests(ApiFactory factory)
{
    private const string Organizador = "ana@empresa.com";

    private readonly HttpClient _client = factory.CreateClient();

    // Un día futuro fijo a las 00:00 UTC; cada test usa su propia sala, así no se pisan.
    private static readonly DateTimeOffset Dia = new(DateTime.UtcNow.Date.AddDays(7), TimeSpan.Zero);

    [Fact]
    public async Task CrearReserva_Valida_Devuelve201ConLaReserva()
    {
        var salaId = await CrearSalaAsync();

        var respuesta = await ReservarAsync(salaId, Dia.AddHours(9), Dia.AddHours(10));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var reserva = await respuesta.Content.ReadFromJsonAsync<ReservaResponse>();
        Assert.NotNull(reserva);
        Assert.Equal($"/api/reservas/{reserva.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(Dia.AddHours(9), reserva.Inicio);
    }

    [Fact]
    public async Task CrearReserva_QueSeSuperpone_Devuelve409ConProblemDetails()
    {
        var salaId = await CrearSalaAsync();
        await ReservarAsync(salaId, Dia.AddHours(9), Dia.AddHours(10));

        var respuesta = await ReservarAsync(salaId, Dia.AddHours(9).AddMinutes(30), Dia.AddHours(11));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ReservasContiguas_NoSeSuperponen()
    {
        var salaId = await CrearSalaAsync();

        var primera = await ReservarAsync(salaId, Dia.AddHours(9), Dia.AddHours(10));
        var segunda = await ReservarAsync(salaId, Dia.AddHours(10), Dia.AddHours(11));

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
    }

    [Fact]
    public async Task MismoHorarioEnOtraSala_SePermite()
    {
        var salaA = await CrearSalaAsync();
        var salaB = await CrearSalaAsync();

        var primera = await ReservarAsync(salaA, Dia.AddHours(9), Dia.AddHours(10));
        var segunda = await ReservarAsync(salaB, Dia.AddHours(9), Dia.AddHours(10));

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
    }

    [Fact]
    public async Task ReservasSimultaneasDelMismoHorario_SoloSeConfirmaUna()
    {
        // El caso que rompe la solución "consulto y después inserto".
        const int intentos = 20;
        var salaId = await CrearSalaAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, intentos).Select(i =>
            ReservarAsync(salaId, Dia.AddHours(14), Dia.AddHours(15), $"persona{i}@empresa.com")));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(intentos - 1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task CrearReserva_EnSalaInexistente_Devuelve404()
    {
        var respuesta = await ReservarAsync(Guid.NewGuid(), Dia.AddHours(9), Dia.AddHours(10));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearReserva_QueSuperaLaCapacidad_Devuelve400ConElCampo()
    {
        var salaId = await CrearSalaAsync(capacidad: 4);

        var respuesta = await ReservarAsync(salaId, Dia.AddHours(9), Dia.AddHours(10), asistentes: 5);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("asistentes", cuerpo);
    }

    [Fact]
    public async Task Cancelar_PorOtraPersona_Devuelve403()
    {
        var reservaId = await ReservarOkAsync(await CrearSalaAsync());

        var respuesta = await CancelarAsync(reservaId, "otro@empresa.com");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cancelar_SinIdentificarAlUsuario_Devuelve401()
    {
        var reservaId = await ReservarOkAsync(await CrearSalaAsync());

        var respuesta = await _client.DeleteAsync($"/api/reservas/{reservaId}");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cancelar_PorElOrganizador_LiberaElHorario()
    {
        var salaId = await CrearSalaAsync();
        var reservaId = await ReservarOkAsync(salaId);

        var cancelacion = await CancelarAsync(reservaId, Organizador);
        var nuevaReserva = await ReservarAsync(salaId, Dia.AddHours(9), Dia.AddHours(10), "otro@empresa.com");

        Assert.Equal(HttpStatusCode.NoContent, cancelacion.StatusCode);
        Assert.Equal(HttpStatusCode.Created, nuevaReserva.StatusCode);
    }

    [Fact]
    public async Task Cancelar_ReservaInexistente_Devuelve404()
    {
        var respuesta = await CancelarAsync(Guid.NewGuid(), Organizador);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Disponibilidad_DevuelveSoloLasReservasActivasDelDia()
    {
        var salaId = await CrearSalaAsync();
        await ReservarAsync(salaId, Dia.AddHours(11), Dia.AddHours(12));
        await ReservarAsync(salaId, Dia.AddHours(9), Dia.AddHours(10));
        var cancelada = await ReservarOkAsync(salaId, Dia.AddHours(15));
        await CancelarAsync(cancelada, Organizador);
        await ReservarAsync(salaId, Dia.AddDays(1).AddHours(9), Dia.AddDays(1).AddHours(10));

        var reservas = await _client.GetFromJsonAsync<List<ReservaResponse>>(
            $"/api/salas/{salaId}/reservas?fecha={Dia:yyyy-MM-dd}");

        DateTimeOffset[] esperados = [Dia.AddHours(9), Dia.AddHours(11)];
        Assert.NotNull(reservas);
        Assert.Equal(esperados, reservas.Select(r => r.Inicio).ToArray());
    }

    [Fact]
    public async Task Disponibilidad_ConZonaHorariaInvalida_Devuelve400()
    {
        var salaId = await CrearSalaAsync();

        var respuesta = await _client.GetAsync($"/api/salas/{salaId}/reservas?fecha={Dia:yyyy-MM-dd}&zonaHoraria=Marte/Olympus");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearSala_ConNombreRepetido_Devuelve409()
    {
        var nombre = $"Sala {Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/salas", new CrearSalaRequest(nombre, 5));

        var respuesta = await _client.PostAsJsonAsync("/api/salas", new CrearSalaRequest(nombre.ToUpperInvariant(), 5));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_ConLaBaseDisponible_Devuelve200()
    {
        var respuesta = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private async Task<Guid> CrearSalaAsync(int capacidad = 10)
    {
        var respuesta = await _client.PostAsJsonAsync("/api/salas", new CrearSalaRequest($"Sala {Guid.NewGuid():N}", capacidad));
        respuesta.EnsureSuccessStatusCode();
        var sala = await respuesta.Content.ReadFromJsonAsync<SalaResponse>();
        return sala!.Id;
    }

    private Task<HttpResponseMessage> ReservarAsync(
        Guid salaId,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        string organizador = Organizador,
        int asistentes = 4) =>
        _client.PostAsJsonAsync("/api/reservas", new CrearReservaRequest(salaId, inicio, fin, organizador, asistentes));

    private async Task<Guid> ReservarOkAsync(Guid salaId, DateTimeOffset? inicio = null)
    {
        var desde = inicio ?? Dia.AddHours(9);
        var respuesta = await ReservarAsync(salaId, desde, desde.AddHours(1));
        respuesta.EnsureSuccessStatusCode();
        var reserva = await respuesta.Content.ReadFromJsonAsync<ReservaResponse>();
        return reserva!.Id;
    }

    private Task<HttpResponseMessage> CancelarAsync(Guid reservaId, string usuario)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Delete, $"/api/reservas/{reservaId}");
        solicitud.Headers.Add("X-User-Email", usuario);
        return _client.SendAsync(solicitud);
    }
}
