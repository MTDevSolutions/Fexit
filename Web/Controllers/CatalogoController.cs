using Application.Dtos;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>
/// Carga del catálogo, sin pantalla (decisión del 2026-09-09). Detrás de la misma X-Api-Key que el
/// resto: Fexit no autoriza a nadie (§7), y quien tiene la clave ya podía ejecutar acciones, así que
/// también puede cargarlas.
///
/// No hay PUT de controlador ni de enclavamiento a propósito: son pocas filas, se cargan una vez en
/// la puesta en marcha, y borrar y volver a crear es un camino menos que mantener y probar. La acción
/// sí tiene PUT, que es la que se toca seguido (habilitar, deshabilitar, corregir la descripción).
///
/// El equipo también tiene PUT, y por un motivo distinto: su nombre es el que el usuario pronuncia
/// ("¿cómo está la barrera 1?"), así que se corrige; y "borrar y crear de nuevo" no es un camino
/// equivalente, porque el borrado se lleva sus estados por cascada.
/// </summary>
[ApiController]
[Route("catalogo")]
public class CatalogoController(ICatalogoRepository repository) : ControllerBase
{
    [HttpGet("controladores")]
    public async Task<ActionResult<IReadOnlyList<ControladorDto>>> ListarControladores(CancellationToken ct) =>
        Ok(await repository.ListarControladoresAsync(ct));

    [HttpPost("controladores")]
    public async Task<ActionResult<long>> CrearControlador([FromBody] ControladorRequest req, CancellationToken ct) =>
        Ok(await repository.CrearControladorAsync(req, ct));

    [HttpDelete("controladores/{id:long}")]
    public async Task<ActionResult> BorrarControlador(long id, CancellationToken ct)
    {
        await repository.BorrarControladorAsync(id, ct);
        return NoContent();
    }

    [HttpPost("sectores")]
    public async Task<ActionResult<long>> CrearSector([FromBody] SectorRequest req, CancellationToken ct) =>
        Ok(await repository.CrearSectorAsync(req, ct));

    [HttpPost("equipos")]
    public async Task<ActionResult<long>> CrearEquipo([FromBody] EquipoRequest req, CancellationToken ct) =>
        Ok(await repository.CrearEquipoAsync(req, ct));

    [HttpGet("equipos")]
    public async Task<ActionResult<IReadOnlyList<EquipoDto>>> ListarEquipos(CancellationToken ct) =>
        Ok(await repository.ListarEquiposAsync(ct));

    [HttpPut("equipos/{id:long}")]
    public async Task<ActionResult> EditarEquipo(long id, [FromBody] EquipoEdicionRequest req, CancellationToken ct)
    {
        await repository.EditarEquipoAsync(id, req, ct);
        return NoContent();
    }

    // Lote: un importador con 200 señales no puede hacer 200 llamadas (§3.1).
    [HttpPost("equipos/{equipoId:long}/estados")]
    public async Task<ActionResult<ResultadoAltaEstados>> GuardarEstados(
        long equipoId, [FromBody] List<EstadoRequest> estados, CancellationToken ct) =>
        Ok(await repository.GuardarEstadosAsync(equipoId, estados, ct));

    [HttpGet("equipos/{equipoId:long}/estados")]
    public async Task<ActionResult<IReadOnlyList<EstadoDto>>> ListarEstados(
        long equipoId, CancellationToken ct) =>
        Ok(await repository.ListarEstadosAsync(equipoId, ct));

    [HttpDelete("estados/{id:long}")]
    public async Task<ActionResult> BorrarEstado(long id, CancellationToken ct)
    {
        await repository.BorrarEstadoAsync(id, ct);
        return NoContent();
    }

    // Cuelgan del EQUIPO y ya no del controlador (spec 2026-09-17 §2.2): "el portón de playa está
    // abierto" es una precondición de la barrera, no del PLC que además atiende al radar.
    [HttpGet("equipos/{equipoId:long}/enclavamientos")]
    public async Task<ActionResult<IReadOnlyList<EnclavamientoDto>>> ListarEnclavamientos(
        long equipoId, CancellationToken ct) =>
        Ok(await repository.ListarEnclavamientosAsync(equipoId, ct));

    [HttpPost("equipos/{equipoId:long}/enclavamientos")]
    public async Task<ActionResult<long>> CrearEnclavamiento(
        long equipoId, [FromBody] EnclavamientoRequest req, CancellationToken ct) =>
        Ok(await repository.CrearEnclavamientoAsync(equipoId, req, ct));

    [HttpDelete("enclavamientos/{id:long}")]
    public async Task<ActionResult> BorrarEnclavamiento(long id, CancellationToken ct)
    {
        await repository.BorrarEnclavamientoAsync(id, ct);
        return NoContent();
    }

    [HttpGet("acciones")]
    public async Task<ActionResult<IReadOnlyList<AccionCatalogoDto>>> ListarAcciones(CancellationToken ct) =>
        Ok(await repository.ListarAccionesAsync(ct));

    [HttpPost("acciones")]
    public async Task<ActionResult<long>> CrearAccion([FromBody] AccionRequest req, CancellationToken ct) =>
        Ok(await repository.CrearAccionAsync(req, ct));

    [HttpPut("acciones/{id:long}")]
    public async Task<ActionResult> EditarAccion(long id, [FromBody] AccionRequest req, CancellationToken ct)
    {
        await repository.EditarAccionAsync(id, req, ct);
        return NoContent();
    }

    [HttpDelete("acciones/{id:long}")]
    public async Task<ActionResult> BorrarAccion(long id, CancellationToken ct)
    {
        await repository.BorrarAccionAsync(id, ct);
        return NoContent();
    }
}
