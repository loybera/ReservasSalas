using ReservasSalas.Api.Domain;

namespace ReservasSalas.UnitTests;

public class ReservaTests
{
    // "Ahora" fijo: los tests no dependen del reloj real.
    private static readonly DateTimeOffset Ahora = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Inicio = Ahora.AddDays(1);
    private static readonly Sala SalaDeSeis = Sala.Crear("Sala Andes", capacidad: 6).Valor!;

    private static Resultado<Reserva> Crear(
        DateTimeOffset? inicio = null,
        DateTimeOffset? fin = null,
        string? email = "ana@empresa.com",
        int asistentes = 4)
    {
        var i = inicio ?? Inicio;
        return Reserva.Crear(SalaDeSeis, i, fin ?? i.AddHours(1), email, asistentes, Ahora);
    }

    [Fact]
    public void Crear_ConDatosValidos_DevuelveLaReserva()
    {
        var resultado = Crear();

        Assert.True(resultado.EsExito);
        Assert.Equal(SalaDeSeis.Id, resultado.Valor.SalaId);
        Assert.Equal(Inicio, resultado.Valor.Inicio);
        Assert.Equal(Inicio.AddHours(1), resultado.Valor.Fin);
        Assert.Equal(Ahora, resultado.Valor.CreadaEn);
        Assert.False(resultado.Valor.EstaCancelada);
    }

    [Fact]
    public void Crear_ConvierteLasFechasAUtc()
    {
        var inicioEnSantiago = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.FromHours(-3));

        var resultado = Crear(inicio: inicioEnSantiago);

        Assert.True(resultado.EsExito);
        Assert.Equal(TimeSpan.Zero, resultado.Valor.Inicio.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero), resultado.Valor.Inicio);
    }

    [Fact]
    public void Crear_NormalizaElEmail()
    {
        var resultado = Crear(email: "  Ana@Empresa.COM ");

        Assert.True(resultado.EsExito);
        Assert.Equal("ana@empresa.com", resultado.Valor.OrganizadorEmail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Crear_ConFinIgualOAnteriorAlInicio_EsInvalido(int minutos)
    {
        var resultado = Crear(fin: Inicio.AddMinutes(minutos));

        AssertErrorEn(resultado, "fin");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(90)]
    [InlineData(240)]
    public void Crear_ConDuracionEntre15MinutosY4Horas_EsValida(int minutos)
    {
        var resultado = Crear(fin: Inicio.AddMinutes(minutos));

        Assert.True(resultado.EsExito);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(241)]
    public void Crear_ConDuracionFueraDeRango_EsInvalida(int minutos)
    {
        var resultado = Crear(fin: Inicio.AddMinutes(minutos));

        AssertErrorEn(resultado, "fin");
    }

    [Fact]
    public void Crear_EnElPasado_EsInvalida()
    {
        var resultado = Crear(inicio: Ahora.AddMinutes(-1));

        AssertErrorEn(resultado, "inicio");
    }

    [Fact]
    public void Crear_EmpezandoJustoAhora_EsValida()
    {
        var resultado = Crear(inicio: Ahora);

        Assert.True(resultado.EsExito);
    }

    [Fact]
    public void Crear_ConMasAsistentesQueLaCapacidad_EsInvalida()
    {
        var resultado = Crear(asistentes: 7);

        AssertErrorEn(resultado, "asistentes");
    }

    [Fact]
    public void Crear_ConLaCapacidadExacta_EsValida()
    {
        var resultado = Crear(asistentes: 6);

        Assert.True(resultado.EsExito);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_SinAsistentes_EsInvalida(int asistentes)
    {
        var resultado = Crear(asistentes: asistentes);

        AssertErrorEn(resultado, "asistentes");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-email")]
    [InlineData("Ana <ana@empresa.com>")]
    public void Crear_ConEmailInvalido_EsInvalida(string? email)
    {
        var resultado = Crear(email: email);

        AssertErrorEn(resultado, "organizadorEmail");
    }

    [Fact]
    public void Crear_ConVariosErrores_LosInformaTodosJuntos()
    {
        var resultado = Crear(inicio: Ahora.AddHours(-1), email: "x", asistentes: 50);

        string[] esperados = ["asistentes", "inicio", "organizadorEmail"];
        Assert.False(resultado.EsExito);
        Assert.Equal(esperados, resultado.Error.Campos!.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Cancelar_PorElOrganizador_LaMarcaComoCancelada()
    {
        var reserva = Crear().Valor!;
        var momento = Ahora.AddMinutes(5);

        var resultado = reserva.Cancelar("ANA@empresa.com", momento);

        Assert.Equal(ResultadoCancelacion.Cancelada, resultado);
        Assert.True(reserva.EstaCancelada);
        Assert.Equal(momento, reserva.CanceladaEn);
    }

    [Theory]
    [InlineData("otro@empresa.com")]
    [InlineData("")]
    [InlineData(null)]
    public void Cancelar_PorOtraPersona_NoSePermite(string? email)
    {
        var reserva = Crear().Valor!;

        var resultado = reserva.Cancelar(email, Ahora);

        Assert.Equal(ResultadoCancelacion.NoEsElOrganizador, resultado);
        Assert.False(reserva.EstaCancelada);
    }

    [Fact]
    public void Cancelar_DosVeces_EsIdempotente()
    {
        var reserva = Crear().Valor!;
        reserva.Cancelar("ana@empresa.com", Ahora);
        var primeraCancelacion = reserva.CanceladaEn;

        var resultado = reserva.Cancelar("ana@empresa.com", Ahora.AddHours(1));

        Assert.Equal(ResultadoCancelacion.YaEstabaCancelada, resultado);
        Assert.Equal(primeraCancelacion, reserva.CanceladaEn);
    }

    private static void AssertErrorEn(Resultado<Reserva> resultado, string campo)
    {
        Assert.False(resultado.EsExito);
        Assert.Equal(TipoError.Validacion, resultado.Error.Tipo);
        Assert.Contains(campo, resultado.Error.Campos!.Keys);
    }
}
