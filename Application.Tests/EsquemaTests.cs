using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests;

public class EsquemaTests
{
    private static Controlador NuevoControlador(string nombre = "bomba3") => new()
    {
        Nombre = nombre,
        TipoEquipo = "plc",
        Ip = "10.0.0.20",
        Puerto = 102,
        Protocolo = "SiemensS7",
        Modelo = "S7300",
        Rack = 0,
        Slot = 1,
    };

    /// <summary>
    /// Un sector, un controlador y su equipo: el mínimo para poder colgar un enclavamiento o una
    /// acción, que desde el 2026-09-17 cuelgan del EQUIPO y ya no del controlador.
    /// </summary>
    private static Equipo SembrarEquipo(FexitDbContext ctx, string nombre = "bomba3")
    {
        var sector = new Sector { Nombre = "Sector de " + nombre, Orden = 1 };
        var controlador = NuevoControlador(nombre);
        ctx.AddRange(sector, controlador);
        ctx.SaveChanges();

        var equipo = new Equipo
        {
            Nombre = "Equipo " + nombre, Descripcion = "d",
            SectorId = sector.Id, ControladorId = controlador.Id,
        };
        ctx.Equipos.Add(equipo);
        ctx.SaveChanges();
        return equipo;
    }

    [Fact]
    public void UnEquipoConSusEnclavamientosYSuAccionSeGuarda()
    {
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();

        var equipo = SembrarEquipo(ctx);

        ctx.Enclavamientos.Add(new Enclavamiento
        {
            EquipoId = equipo.Id,
            Direccion = "DB1.DBX1.0",
            TipoDireccion = "S7Bit",
            Nombre = "Portón de playa",
            ValoresOk = [1],
            Orden = 1,
        });
        ctx.Acciones.Add(new Accion
        {
            Codigo = "arrancar_bomba3",
            Descripcion = "Arranca la bomba del silo 3",
            Modo = "escritura",
            EquipoId = equipo.Id,
            Direccion = "DB1.DBX0.0",
            TipoDireccion = "S7Bit",
            Valor = 1,
            UsaEnclavamientos = true,
            Habilitada = true,
        });
        ctx.SaveChanges();

        Assert.Single(ctx.Enclavamientos);
        Assert.Single(ctx.Acciones);
    }

    [Fact]
    public void ValoresOkVuelveComoLista()
    {
        // Es lista y no valor único (§10.5): un selector con varias posiciones válidas está en
        // condición con más de un valor. Si el converter se rompiera, el evaluador compararía contra
        // una lista vacía y TODO daría "fuera de condición" sin ningún error visible.
        using var prueba = new DbDePrueba();

        using (var ctx = prueba.CrearContext())
        {
            var equipo = SembrarEquipo(ctx);
            ctx.Enclavamientos.Add(new Enclavamiento
            {
                EquipoId = equipo.Id, Direccion = "40001", TipoDireccion = "HoldingRegister",
                Nombre = "Selector de modo", ValoresOk = [2, 3, 5], Orden = 1,
            });
            ctx.SaveChanges();
        }

        using var otro = prueba.CrearContext();
        Assert.Equal([2, 3, 5], otro.Enclavamientos.Single().ValoresOk);
    }

    [Fact]
    public void DosAccionesConElMismoCodigo_NoSePueden()
    {
        // El codigo es lo que manda Dixit: único, o la resolución sería no determinista y "abrir
        // barrera" podría ejecutar cualquiera de las dos filas.
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();
        var equipo = SembrarEquipo(ctx);

        ctx.Acciones.Add(Accion("abrir_barrera", equipo.Id));
        ctx.Acciones.Add(Accion("abrir_barrera", equipo.Id));

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void UnaAccionSinEquipoQueExista_NoSePuede()
    {
        // FK real, con Foreign Keys=True en la connection string: una acción que apunta a un equipo
        // inexistente no se puede ejecutar, así que no tiene por qué poder guardarse.
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();

        ctx.Acciones.Add(Accion("abrir_barrera", equipoId: 999));

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void BorrarElEquipoSeLlevaSusEnclavamientos()
    {
        // Cascade: un enclavamiento sin equipo no significa nada. Y es lo que hace imposible que la
        // lista de la lectura y la de la escritura divergan (§4.2) — hay una sola.
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();
        var equipo = SembrarEquipo(ctx);
        ctx.Enclavamientos.Add(new Enclavamiento
        {
            EquipoId = equipo.Id, Direccion = "DB1.DBX1.0", TipoDireccion = "S7Bit",
            Nombre = "Portón", ValoresOk = [1], Orden = 1,
        });
        ctx.SaveChanges();

        ctx.Equipos.Remove(equipo);
        ctx.SaveChanges();

        Assert.Empty(ctx.Enclavamientos);
    }

    [Fact]
    public void BorrarElEquipoConAccionesNoSePuede()
    {
        // Restrict y no Cascade, al revés que el enclavamiento: un enclavamiento se vuelve a cargar,
        // pero una acción borrada en silencio se nota recién cuando Dixit la pide y ya no está.
        using var prueba = new DbDePrueba();
        long equipoId;
        using (var ctx = prueba.CrearContext())
        {
            var equipo = SembrarEquipo(ctx);
            equipoId = equipo.Id;
            ctx.Acciones.Add(Accion("abrir_barrera", equipo.Id));
            ctx.SaveChanges();
        }

        // Contexto nuevo a propósito: con la acción trackeada, EF corta antes de llegar a la base y
        // el test no probaría la FK, que es lo que protege al servidor de producción.
        using var otro = prueba.CrearContext();
        otro.Equipos.Remove(otro.Equipos.Single(e => e.Id == equipoId));

        Assert.Throws<DbUpdateException>(() => otro.SaveChanges());
    }

    [Theory]
    [InlineData("lectura")]
    [InlineData("escritura")]
    public void ElCheckDeModoAceptaLosDosValores(string modo)
    {
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();
        var equipo = SembrarEquipo(ctx);

        var accion = Accion("una_accion", equipo.Id);
        accion.Modo = modo;
        ctx.Acciones.Add(accion);
        ctx.SaveChanges();

        Assert.Single(ctx.Acciones);
    }

    [Fact]
    public void ElCheckDeModoRechazaCualquierOtroValor()
    {
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();
        var equipo = SembrarEquipo(ctx);

        var accion = Accion("una_accion", equipo.Id);
        accion.Modo = "borrar_todo";
        ctx.Acciones.Add(accion);

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void ElCheckDeProtocoloRechazaUnProtocoloDesconocido()
    {
        // Un protocolo que ninguna factory atiende sólo falla el día que alguien ejecuta una acción
        // de ese controlador. La base lo rechaza antes, igual que el modo.
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();
        var controlador = NuevoControlador();
        controlador.Protocolo = "Profibus";
        ctx.Controladores.Add(controlador);

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void ElCheckDeModeloRechazaUnModeloDesconocido()
    {
        // El modelo de CPU decide cómo S7netplus negocia el tamaño de PDU y cómo resuelve el
        // direccionamiento de DB. Uno equivocado no da un error que diga "modelo equivocado": da una
        // PlcException genérica en la primera lectura, y quien la ve va a revisar el cableado. Que la
        // base sólo acepte los cinco que S7netplus conoce corta esa clase entera de diagnóstico
        // perdido.
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();
        var controlador = NuevoControlador();
        controlador.Modelo = "LOGO8";
        ctx.Controladores.Add(controlador);

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void LaMigracionInicialDejaLasTresTablasVacias()
    {
        // Migrate() y no EnsureCreated: es el camino que corre en la planta, y lo único que prueba
        // que la migración generada realmente crea el esquema que el modelo describe.
        using var cn = new SqliteConnection("Data Source=:memory:");
        cn.Open();
        using var ctx = new FexitDbContext(
            new DbContextOptionsBuilder<FexitDbContext>().UseSqlite(cn).Options);

        ctx.Database.Migrate();

        Assert.Empty(ctx.Controladores);
        Assert.Empty(ctx.Enclavamientos);
        Assert.Empty(ctx.Acciones);
    }

    [Fact]
    public void UnaAccionDeshabilitadaSeGuardaDeshabilitada()
    {
        // Parece trivial y no lo es. Con HasDefaultValue(true) sobre esta columna, EF la marca
        // ValueGeneratedOnAdd y omite del INSERT toda propiedad cuyo valor sea el default del CLR:
        // como el default del CLR de un bool es false — el OPUESTO del default de la base — una
        // acción creada como deshabilitada se guardaba habilitada, en silencio. Y una acción
        // deshabilitada tiene que dar 404 al ejecutarse, así que el mapeo se comía la regla de
        // negocio entera. El default de esta columna lo dueña C#, no la base.
        using var prueba = new DbDePrueba();
        using var ctx = prueba.CrearContext();
        var equipo = SembrarEquipo(ctx);

        var accion = Accion("una_accion", equipo.Id);
        accion.Habilitada = false;
        ctx.Acciones.Add(accion);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        Assert.False(ctx.Acciones.Single().Habilitada);
    }

    private static Accion Accion(string codigo, long equipoId) => new()
    {
        Codigo = codigo,
        Descripcion = "d",
        Modo = "escritura",
        EquipoId = equipoId,
        Direccion = "DB1.DBX0.0",
        TipoDireccion = "S7Bit",
        Valor = 1,
        UsaEnclavamientos = false,
        Habilitada = true,
    };
}
