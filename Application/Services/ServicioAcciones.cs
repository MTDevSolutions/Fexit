using Application.Constantes;
using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;

namespace Application.Services;

/// <summary>
/// El negocio de ejecutar, en tres pasos: resolver el código, verificar el modo, delegar en el
/// ejecutor del tipo de equipo. No sabe de protocolos ni de drivers, y no debe: eso vive en
/// Infrastructure.
/// </summary>
public class ServicioAcciones(IAccionRepository repositorio, IEnumerable<IEjecutorAccion> ejecutores)
    : IServicioAcciones
{
    public async Task<ResultadoAccion> EjecutarAsync(string codigo, string modoEsperado, CancellationToken ct)
    {
        // Trim y no ToLower: los códigos son identificadores, no texto libre. Un espacio pegado en la
        // carga del catálogo de Dixit daría un 404 incomprensible, y esto es barato.
        var accion = await repositorio.BuscarPorCodigoAsync(codigo.Trim(), ct)
            ?? throw new AccionNoEncontradaException();

        // La defensa de borde de §5. Va ANTES de tocar el equipo, obviamente, pero también antes de
        // resolver el ejecutor: si el modo no coincide no hay nada que ejecutar, y cuanto más temprano
        // se corta, menos código hay que auditar para creerle.
        if (accion.Accion.Modo != modoEsperado || !CteFexit.EsModoValido(modoEsperado))
            throw new ModoNoCoincideException();

        var ejecutor = ejecutores.FirstOrDefault(e => e.TipoEquipo == accion.Equipo.TipoEquipo)
            // El CHECK de la base limita los tipos, así que esto es una fila cargada contra una BD
            // vieja. Config, no red: definitivo, sin reintento.
            ?? throw new ConfigInvalidaException("No hay ejecutor para el tipo de equipo configurado.");

        return await ejecutor.EjecutarAsync(accion, ct);
    }
}
