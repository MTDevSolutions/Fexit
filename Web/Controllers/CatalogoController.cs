using Application.Dtos;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>
/// Carga del catálogo, sin pantalla (decisión del 2026-09-09). Detrás de la misma X-Api-Key que el
/// resto: Fexit no autoriza a nadie (§7), y quien tiene la clave ya podía ejecutar acciones, así que
/// también puede cargarlas.
///
/// No hay PUT de equipo ni de enclavamiento a propósito: son pocas filas, se cargan una vez en la
/// puesta en marcha, y borrar y volver a crear es un camino menos que mantener y probar. La acción sí
/// tiene PUT, que es la que se toca seguido (habilitar, deshabilitar, corregir la descripción).
/// </summary>
[ApiController]
[Route("catalogo")]
public class CatalogoController(ICatalogoRepository repository) : ControllerBase
{
    [HttpGet("equipos")]
    public async Task<ActionResult<IReadOnlyList<EquipoDto>>> ListarEquipos(CancellationToken ct) =>
        Ok(await repository.ListarEquiposAsync(ct));

    [HttpPost("equipos")]
    public async Task<ActionResult<long>> CrearEquipo([FromBody] EquipoRequest req, CancellationToken ct) =>
        Ok(await repository.CrearEquipoAsync(req, ct));

    [HttpDelete("equipos/{id:long}")]
    public async Task<ActionResult> BorrarEquipo(long id, CancellationToken ct)
    {
        await repository.BorrarEquipoAsync(id, ct);
        return NoContent();
    }

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
