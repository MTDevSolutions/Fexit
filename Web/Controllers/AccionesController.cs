using Application.Dtos;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>
/// Los dos verbos del contrato de §3. Nada acá menciona PLC: una barrera y un sensor IoT tienen la
/// misma forma, que es lo que permite sumar un tipo de equipo sin tocar la API.
/// </summary>
[ApiController]
[Route("acciones")]
public class AccionesController(IAccionRepository repository, IServicioAcciones servicio) : ControllerBase
{
    /// <summary>
    /// El catálogo técnico que Fexit publica. Lo consume el ABM de Dixit, no la ejecución.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccionRemotaDto>>> Listar(CancellationToken ct) =>
        Ok(await repository.ListarAsync(ct));

    /// <summary>
    /// Ejecuta una acción por su código. El cuerpo no describe la acción: la verifica. Si en este
    /// catálogo ese código no es del modo que Dixit esperaba, devuelve 409 y no ejecuta nada — es la
    /// defensa que impide que una fila mal cargada en Dixit como lectura termine escribiendo (§5).
    ///
    /// Una precondición que no da NO es un error: es 200 con exito:false y el detalle de cuál falló.
    /// La acción se ejecutó y decidió no escribir.
    /// </summary>
    [HttpPost("{codigo}/ejecutar")]
    public async Task<ActionResult<ResultadoAccion>> Ejecutar(
        string codigo, [FromBody] EjecutarAccionRequest request, CancellationToken ct) =>
        Ok(await servicio.EjecutarAsync(codigo, request.ModoEsperado, ct));
}
