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

    [HttpGet("controladores/{controladorId:long}/enclavamientos")]
    public async Task<ActionResult<IReadOnlyList<EnclavamientoDto>>> ListarEnclavamientos(
        long controladorId, CancellationToken ct) =>
        Ok(await repository.ListarEnclavamientosAsync(controladorId, ct));

    [HttpPost("controladores/{controladorId:long}/enclavamientos")]
    public async Task<ActionResult<long>> CrearEnclavamiento(
        long controladorId, [FromBody] EnclavamientoRequest req, CancellationToken ct) =>
        Ok(await repository.CrearEnclavamientoAsync(controladorId, req, ct));

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
