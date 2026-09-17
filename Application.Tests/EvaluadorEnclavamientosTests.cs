using Application.Services;
using Domain.Entities;

namespace Application.Tests;

public class EvaluadorEnclavamientosTests
{
    private static Enclavamiento Enc(string nombre, params int[] valoresOk) => new()
    {
        Id = 0, ControladorId = 1, Direccion = "DB1.DBX1.0", TipoDireccion = "S7Bit",
        Nombre = nombre, ValoresOk = [.. valoresOk], Orden = 0,
    };

    [Fact]
    public void UnValorDeLaListaEstaEnCondicion()
    {
        Assert.True(EvaluadorEnclavamientos.EstaEnCondicion(Enc("Portón", 1), 1));
    }

    [Fact]
    public void UnValorFueraDeLaListaNoEstaEnCondicion()
    {
        Assert.False(EvaluadorEnclavamientos.EstaEnCondicion(Enc("Portón", 1), 0));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(4, false)]
    public void ConVariosValoresOkCualquieraDeEllosSirve(int leido, bool esperado)
    {
        // §10.5: valoresOk es lista, no valor único. Un selector con varias posiciones válidas está
        // en condición con más de un valor, y modelarlo como igualdad obligaría a rehacer la config
        // el día que aparezca el primero.
        Assert.Equal(esperado, EvaluadorEnclavamientos.EstaEnCondicion(Enc("Selector", 2, 3, 5), leido));
    }

    [Fact]
    public void UnEnclavamientoSinValoresOkNuncaEstaEnCondicion()
    {
        // Config incompleta. Que dé "fuera de condición" es lo seguro: aborta la escritura y alguien
        // mira la fila. Lo peligroso sería lo contrario — una lista vacía interpretada como "todo
        // vale" dejaría escribir sin ninguna precondición y nadie se enteraría.
        Assert.False(EvaluadorEnclavamientos.EstaEnCondicion(Enc("Roto"), 1));
        Assert.False(EvaluadorEnclavamientos.EstaEnCondicion(Enc("Roto"), 0));
    }

    [Fact]
    public void TodosEnCondicion_ConLaListaVacia_DaTrue()
    {
        // Un equipo sin enclavamientos cargados no bloquea nada: la escritura procede. Es distinto
        // del caso de arriba — allá hay una condición mal cargada, acá no hay condiciones.
        Assert.True(EvaluadorEnclavamientos.TodosEnCondicion([]));
    }

    [Fact]
    public void TodosEnCondicion_ConUnoSoloFuera_DaFalse()
    {
        var lecturas = new List<LecturaEnclavamiento>
        {
            new(Enc("Portón", 1), 1),
            new(Enc("Nivel", 1), 0),
            new(Enc("Térmica", 1), 1),
        };

        Assert.False(EvaluadorEnclavamientos.TodosEnCondicion(lecturas));
    }

    [Fact]
    public void LaTablaTraeLaEvaluacionYNoElValorCrudo()
    {
        // LA prueba de §10.12. Si acá se colara el entero, el LLM tendría que adivinar qué significa
        // un 0 en ese equipo, y va a adivinar mal con total seguridad.
        var lecturas = new List<LecturaEnclavamiento>
        {
            new(Enc("Portón de playa", 1), 0),
            new(Enc("Nivel de tanque", 1), 1),
        };

        var resultado = EvaluadorEnclavamientos.ArmarTabla(lecturas);

        Assert.Equal(["enclavamiento", "en condicion"], resultado.Columnas);
        Assert.Equal("Portón de playa", resultado.Filas[0]["enclavamiento"]);
        Assert.Equal("no", resultado.Filas[0]["en condicion"]);
        Assert.Equal("Nivel de tanque", resultado.Filas[1]["enclavamiento"]);
        Assert.Equal("sí", resultado.Filas[1]["en condicion"]);

        var json = System.Text.Json.JsonSerializer.Serialize(resultado);
        Assert.DoesNotContain("DB1.DBX1.0", json);
    }

    [Fact]
    public void LaTablaDeUnaLecturaConTodoEnCondicionEsExitosa()
    {
        // Leer siempre "sale bien", esté o no todo en condición: la lectura se ejecutó. Exito=false
        // en una lectura significaría que no se pudo leer, no que hay algo fuera de condición.
        var resultado = EvaluadorEnclavamientos.ArmarTabla([new(Enc("Portón", 1), 0)]);

        Assert.True(resultado.Exito);
    }

    [Fact]
    public void ElDetalleDeLaLecturaDiceCuantosEstanFuera()
    {
        var lecturas = new List<LecturaEnclavamiento>
        {
            new(Enc("Portón", 1), 0),
            new(Enc("Nivel", 1), 0),
            new(Enc("Térmica", 1), 1),
        };

        Assert.Equal("2 fuera de condición.", EvaluadorEnclavamientos.ArmarTabla(lecturas).Detalle);
    }

    [Fact]
    public void ConTodoEnCondicion_ElDetalleLoDice()
    {
        Assert.Equal("Todo en condición.",
            EvaluadorEnclavamientos.ArmarTabla([new(Enc("Portón", 1), 1)]).Detalle);
    }

    [Fact]
    public void ElDetalleDelAbortoNombraLosQueFallan()
    {
        // Es el texto que ve el usuario en el chat cuando la escritura no procede. Tiene que decir
        // CUÁL, o "precondición no cumplida" a secas obliga a llamar por teléfono a la planta.
        var lecturas = new List<LecturaEnclavamiento>
        {
            new(Enc("Portón de playa", 1), 0),
            new(Enc("Nivel de tanque", 1), 1),
        };

        var detalle = EvaluadorEnclavamientos.DetalleDelAborto(lecturas);

        Assert.Equal("Precondición no cumplida: Portón de playa.", detalle);
    }

    [Fact]
    public void ElDetalleDelAbortoConVariosLosLista()
    {
        var lecturas = new List<LecturaEnclavamiento>
        {
            new(Enc("Portón de playa", 1), 0),
            new(Enc("Nivel de tanque", 1), 0),
        };

        Assert.Equal("Precondición no cumplida: Portón de playa, Nivel de tanque.",
            EvaluadorEnclavamientos.DetalleDelAborto(lecturas));
    }

    [Fact]
    public void ElDetalleDelAbortoNoLlevaDireccionesNiValores()
    {
        // §6: ningún mensaje que salga de Fexit lleva IP, dirección ni tag, ni siquiera para explicar
        // por qué abortó. El nombre del enclavamiento sí: es lo único que el usuario puede entender.
        var lecturas = new List<LecturaEnclavamiento> { new(Enc("Portón", 1), 7) };

        var detalle = EvaluadorEnclavamientos.DetalleDelAborto(lecturas);

        Assert.DoesNotContain("DB1.DBX1.0", detalle);
        Assert.DoesNotContain("7", detalle);
    }
}
