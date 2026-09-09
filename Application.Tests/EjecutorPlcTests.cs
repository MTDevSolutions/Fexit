using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Domain.Entities;
using Infrastructure.Ejecutores;

namespace Application.Tests;

public class EjecutorPlcTests
{
    private static Equipo Equipo() => new()
    {
        Id = 1, Nombre = "bomba3", TipoEquipo = CteFexit.TipoEquipoPlc, Ip = "10.0.0.20",
        Puerto = 102, Protocolo = CteFexit.ProtocoloSiemensS7, Rack = 0, Slot = 1,
    };

    private static Enclavamiento Enc(string nombre, string direccion, params int[] valoresOk) => new()
    {
        EquipoId = 1, Direccion = direccion, TipoDireccion = CteFexit.S7Bit,
        Nombre = nombre, ValoresOk = [.. valoresOk], Orden = 0,
    };

    private static Accion Escritura(bool usaEnclavamientos = true) => new()
    {
        Codigo = "arrancar_bomba3", Descripcion = "d", Modo = CteFexit.ModoEscritura, EquipoId = 1,
        Direccion = "DB1.DBX0.0", TipoDireccion = CteFexit.S7Bit, Valor = 1,
        UsaEnclavamientos = usaEnclavamientos, Habilitada = true,
    };

    private static Accion Lectura() => new()
    {
        Codigo = "estado_bomba3", Descripcion = "d", Modo = CteFexit.ModoLectura, EquipoId = 1,
        Direccion = null, TipoDireccion = null, Valor = null,
        UsaEnclavamientos = true, Habilitada = true,
    };

    private static (EjecutorPlc Ejecutor, DriverFalso Driver) Armar(DriverFalso? driver = null)
    {
        var d = driver ?? new DriverFalso();
        return (new EjecutorPlc(new FactoriaFalsa(d)), d);
    }

    [Fact]
    public void AtiendeElTipoDeEquipoPlc()
    {
        Assert.Equal(CteFexit.TipoEquipoPlc, Armar().Ejecutor.TipoEquipo);
    }

    [Fact]
    public async Task LaEscrituraConTodoEnCondicionEscribe()
    {
        var driver = new DriverFalso().Con("DB1.DBX1.0", 1);
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Escritura(), Equipo(), [Enc("Portón", "DB1.DBX1.0", 1)]);

        var resultado = await ejecutor.EjecutarAsync(accion, default);

        Assert.True(resultado.Exito);
        var escrita = Assert.Single(driver.Escrituras);
        Assert.Equal("DB1.DBX0.0", escrita.Direccion);
        Assert.Equal([1], escrita.Datos);
    }

    [Fact]
    public async Task LaPrecondicionQueNoDaAbortaANTESDeEscribir()
    {
        // LA prueba de §4.5, y la razón por la que este ejecutor existe. Nunca hay escritura parcial:
        // si la condición no da, el actuador no se mueve. Assert.Empty sobre las escrituras es lo
        // único que lo prueba de verdad — comprobar Exito=false no distingue "abortó" de "escribió y
        // después falló".
        var driver = new DriverFalso().Con("DB1.DBX1.0", 0);
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Escritura(), Equipo(), [Enc("Portón de playa", "DB1.DBX1.0", 1)]);

        var resultado = await ejecutor.EjecutarAsync(accion, default);

        Assert.Empty(driver.Escrituras);
        Assert.False(resultado.Exito);
        Assert.Equal("Precondición no cumplida: Portón de playa.", resultado.Detalle);
    }

    [Fact]
    public async Task ConUnoSoloDeVariosEnclavamientosFuera_Aborta()
    {
        var driver = new DriverFalso().Con("a", 1).Con("b", 0).Con("c", 1);
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Escritura(), Equipo(),
            [Enc("Portón", "a", 1), Enc("Nivel", "b", 1), Enc("Térmica", "c", 1)]);

        var resultado = await ejecutor.EjecutarAsync(accion, default);

        Assert.Empty(driver.Escrituras);
        Assert.Contains("Nivel", resultado.Detalle);
    }

    [Fact]
    public async Task UnaEscrituraQueNoUsaEnclavamientosNoLosLee()
    {
        // Prender un cartel no tiene condiciones. Si los leyera igual, un enclavamiento mal cargado
        // en el equipo bloquearía acciones que no dependen de él.
        var driver = new DriverFalso().Con("DB1.DBX1.0", 0);
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(
            Escritura(usaEnclavamientos: false), Equipo(), [Enc("Portón", "DB1.DBX1.0", 1)]);

        var resultado = await ejecutor.EjecutarAsync(accion, default);

        Assert.True(resultado.Exito);
        Assert.Single(driver.Escrituras);
    }

    [Fact]
    public async Task LaLecturaDevuelveLaTablaDeEnclavamientos()
    {
        var driver = new DriverFalso().Con("a", 0).Con("b", 1);
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Lectura(), Equipo(),
            [Enc("Portón de playa", "a", 1), Enc("Nivel de tanque", "b", 1)]);

        var resultado = await ejecutor.EjecutarAsync(accion, default);

        Assert.True(resultado.Exito);
        Assert.Equal(["enclavamiento", "en condicion"], resultado.Columnas);
        Assert.Equal(2, resultado.Filas.Count);
        Assert.Equal("no", resultado.Filas[0]["en condicion"]);
        Assert.Equal("sí", resultado.Filas[1]["en condicion"]);
    }

    [Fact]
    public async Task LaLecturaNuncaEscribe()
    {
        var driver = new DriverFalso().Con("a", 1);
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Lectura(), Equipo(), [Enc("Portón", "a", 1)]);

        await ejecutor.EjecutarAsync(accion, default);

        Assert.Empty(driver.Escrituras);
    }

    [Fact]
    public async Task ElEquipoInalcanzableTiraEquipoInalcanzable()
    {
        // Se traduce acá y no se deja subir la excepción del driver: el mensaje de una
        // SocketException trae host y puerto, y ese texto lo terminaría escribiendo el log de Dixit
        // y la columna Error del comando.
        var driver = new DriverFalso { TiraAlConectar = new IOException("No route to host 10.0.0.20:102") };
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Escritura(usaEnclavamientos: false), Equipo(), []);

        var ex = await Assert.ThrowsAsync<EquipoInalcanzableException>(
            () => ejecutor.EjecutarAsync(accion, default));

        Assert.DoesNotContain("10.0.0.20", ex.Message);
        Assert.DoesNotContain("102", ex.Message);
    }

    [Fact]
    public async Task UnFalloLeyendoLaPrecondicionAbortaSinEscribir()
    {
        // No se sabe en qué estado está el equipo, así que NO se escribe. Es el mismo criterio que la
        // precondición que no da: ante la duda, el actuador no se mueve.
        var driver = new DriverFalso { TiraAlLeer = new IOException("timeout") };
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Escritura(), Equipo(), [Enc("Portón", "a", 1)]);

        await Assert.ThrowsAsync<EquipoInalcanzableException>(() => ejecutor.EjecutarAsync(accion, default));

        Assert.Empty(driver.Escrituras);
    }

    [Fact]
    public async Task UnaEscrituraSinDireccionTiraConfigInvalida()
    {
        // El CHECK de la base lo impide, pero una fila migrada a mano no pasó por ahí. Es una falla
        // determinista: reintentarla da lo mismo, y Dixit tiene que cerrarla en fallido sin reintento.
        var (ejecutor, driver) = Armar();
        var accion = Escritura(usaEnclavamientos: false);
        accion.Direccion = null;

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => ejecutor.EjecutarAsync(new AccionAEjecutar(accion, Equipo(), []), default));

        Assert.Empty(driver.Escrituras);
    }

    [Fact]
    public async Task UnaEscrituraEnUnTipoDeSoloLecturaTiraConfigInvalida()
    {
        // InputRegister y DiscreteInput no se pueden escribir. Sin esta guarda la escritura llegaría
        // al driver, que tiraría NotSupportedException — y ésa sí la atrapa el `when` y sale como
        // EquipoInalcanzable, o sea "no se pudo comunicar" para un equipo que está perfecto. Dixit lo
        // reintentaría cinco veces en vez de cerrarlo para que alguien mire la fila.
        var (ejecutor, driver) = Armar();
        var accion = Escritura(usaEnclavamientos: false);
        accion.TipoDireccion = CteFexit.InputRegister;

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => ejecutor.EjecutarAsync(new AccionAEjecutar(accion, Equipo(), []), default));

        Assert.Empty(driver.Escrituras);
    }

    [Fact]
    public async Task UnTipoDeDireccionDesconocidoTiraConfigInvalida()
    {
        // El CHECK de la base lo impide, pero una fila migrada a mano no pasó por ahí. Es config, no
        // red: tiene que cerrar definitivo y no entrar al carril de reintentos.
        var (ejecutor, driver) = Armar();
        var accion = Escritura(usaEnclavamientos: false);
        accion.TipoDireccion = "Profibus";

        await Assert.ThrowsAsync<ConfigInvalidaException>(
            () => ejecutor.EjecutarAsync(new AccionAEjecutar(accion, Equipo(), []), default));

        Assert.Empty(driver.Escrituras);
    }

    [Fact]
    public async Task UnaEscrituraQueExigeEnclavamientosSinNingunoCargadoNoEscribe()
    {
        // La intención declarada (UsaEnclavamientos=true) no puede degradarse sola a "escribí igual".
        // Falla cerrado: el actuador no se mueve y alguien mira la fila.
        var (ejecutor, driver) = Armar();
        var accion = new AccionAEjecutar(Escritura(usaEnclavamientos: true), Equipo(), []);

        await Assert.ThrowsAsync<ConfigInvalidaException>(() => ejecutor.EjecutarAsync(accion, default));

        Assert.Empty(driver.Escrituras);
    }

    [Fact]
    public async Task UnaDireccionMalFormadaEsErrorDeConfigYNoDeRed()
    {
        // El driver la rechaza antes de tocar la red. Si saliera como EquipoInalcanzable, Dixit
        // reintentaría contra un equipo sano y el usuario buscaría el problema en el cableado.
        var driver = new DriverFalso { TiraAlEscribir = new FormatException("Formato de dirección S7 no válido") };
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Escritura(usaEnclavamientos: false), Equipo(), []);

        await Assert.ThrowsAsync<ConfigInvalidaException>(() => ejecutor.EjecutarAsync(accion, default));
    }

    [Fact]
    public async Task ElDriverSeCierraAunqueLaEjecucionFalle()
    {
        // Un socket que queda abierto por cada fallo agota los del proceso, y en una planta eso se
        // manifiesta como "el sistema dejó de responder" a las horas, sin ninguna pista.
        var driver = new DriverFalso { TiraAlEscribir = new IOException("se cayó") };
        var (ejecutor, _) = Armar(driver);
        var accion = new AccionAEjecutar(Escritura(usaEnclavamientos: false), Equipo(), []);

        await Assert.ThrowsAnyAsync<Exception>(() => ejecutor.EjecutarAsync(accion, default));

        Assert.True(driver.ConectoAlgunaVez);
        Assert.False(driver.IsConnected);
    }
}
