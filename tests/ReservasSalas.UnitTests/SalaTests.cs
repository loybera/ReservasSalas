using ReservasSalas.Api.Domain;

namespace ReservasSalas.UnitTests;

public class SalaTests
{
    [Fact]
    public void Crear_ConDatosValidos_RecortaElNombre()
    {
        var resultado = Sala.Crear("  Sala Andes ", 8);

        Assert.True(resultado.EsExito);
        Assert.Equal("Sala Andes", resultado.Valor.Nombre);
        Assert.Equal(8, resultado.Valor.Capacidad);
        Assert.NotEqual(Guid.Empty, resultado.Valor.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_SinNombre_EsInvalida(string? nombre)
    {
        var resultado = Sala.Crear(nombre, 8);

        Assert.False(resultado.EsExito);
        Assert.Contains("nombre", resultado.Error.Campos!.Keys);
    }

    [Fact]
    public void Crear_ConNombreDemasiadoLargo_EsInvalida()
    {
        var resultado = Sala.Crear(new string('x', Sala.LargoMaximoNombre + 1), 8);

        Assert.False(resultado.EsExito);
        Assert.Contains("nombre", resultado.Error.Campos!.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Crear_ConCapacidadNoPositiva_EsInvalida(int capacidad)
    {
        var resultado = Sala.Crear("Sala Andes", capacidad);

        Assert.False(resultado.EsExito);
        Assert.Contains("capacidad", resultado.Error.Campos!.Keys);
    }
}
