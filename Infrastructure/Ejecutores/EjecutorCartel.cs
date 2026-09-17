using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Infrastructure.Drivers.Huidu;

namespace Infrastructure.Ejecutores;

/// <summary>
/// Escribe en un cartel Huidu D16 (spec 2026-09-12 §3.3). Sin estado: conecta, manda, espera la
/// confirmación y cierra, todo adentro del transporte. No vuelve al mensaje por defecto: el cartel
/// es exclusivo de Fexit y lo escrito queda hasta la próxima escritura.
/// </summary>
public class EjecutorCartel(ITransporteCartel transporte) : IEjecutorAccion
{
    // Se apoya en CteFexit.ColoresCartel, que es contra lo que valida el ABM: si mañana se agrega un
    // color a esa lista sin agregarlo acá, falta la entrada y ModoDeColor tira en vez de pintar de
    // blanco en silencio.
    private static readonly Dictionary<string, ModoMensajeCartel> _modoPorColor = new()
    {
        [CteFexit.ColorVerde] = ModoMensajeCartel.Verde,
        [CteFexit.ColorRojo] = ModoMensajeCartel.Rojo,
        [CteFexit.ColorAmarillo] = ModoMensajeCartel.Amarillo,
        [CteFexit.ColorBlanco] = ModoMensajeCartel.Blanco,
    };

    static EjecutorCartel()
    {
        var sinMapear = CteFexit.ColoresCartel.Where(c => !_modoPorColor.ContainsKey(c)).ToList();
        if (sinMapear.Count > 0)
            throw new InvalidOperationException(
                $"Faltan mapear a un modo del cartel los colores: {string.Join(", ", sinMapear)}.");
    }

    public string TipoEquipo => CteFexit.TipoEquipoCartel;

    public async Task<ResultadoAccion> EjecutarAsync(AccionAEjecutar accion, CancellationToken ct)
    {
        if (accion.Accion.Modo != CteFexit.ModoEscritura)
            throw new ConfigInvalidaException("Un cartel no admite acciones de lectura.");

        var (mensaje, detalle) = Armar(accion);
        var xml = UtilsCartelD16.BuildDinamico(mensaje);

        try
        {
            await transporte.EnviarAsync(accion.Controlador, xml, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // El mensaje del SDK o del socket trae la IP: va en InnerException, que se loguea de
            // este lado y no sale por la API. Un timeout de espera cuenta como "no confirmó".
            throw new EquipoInalcanzableException(ex);
        }

        return new ResultadoAccion(true, detalle, [], []);
    }

    private static (MensajeCartel Mensaje, string Detalle) Armar(AccionAEjecutar accion)
    {
        if (accion.Valores?.Texto is { } texto)
        {
            var color = accion.Valores.Opcion ?? CteFexit.ColorBlanco;
            return (new MensajeCartel(texto, ModoDeColor(color)), $"Se mostró «{texto}» en {color}.");
        }

        var cfg = ConfigCartel.Leer(accion.Accion.ConfigJson);
        return cfg.Modo switch
        {
            CteFexit.CartelModoLogo => (new MensajeCartel("", ModoMensajeCartel.Logo), "Se mostró el logo."),
            CteFexit.CartelModoPantallaVerde => (new MensajeCartel("", ModoMensajeCartel.FullVerde), "Se pintó el cartel de verde."),
            CteFexit.CartelModoPantallaRoja => (new MensajeCartel("", ModoMensajeCartel.FullRojo), "Se pintó el cartel de rojo."),
            _ => (new MensajeCartel(cfg.Texto!, ModoDeColor(cfg.Color!)), $"Se mostró «{cfg.Texto}» en {cfg.Color}."),
        };
    }

    private static ModoMensajeCartel ModoDeColor(string color) =>
        _modoPorColor.TryGetValue(color, out var modo)
            ? modo
            : throw new InvalidOperationException($"Color de cartel sin mapear: '{color}'.");
}
