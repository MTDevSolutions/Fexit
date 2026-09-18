using Application.Dtos;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("estados")]
public class EstadosController(IEstadoRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EstadoPublicadoDto>>> Listar(CancellationToken ct) =>
        Ok(await repository.ListarPublicadosAsync(ct));
}
