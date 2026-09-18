using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Application.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Web.Controllers;

[ApiController]
[Route("estados")]
public class EstadosController(
    IEstadoRepository repository, LectorEstados lector, IOptions<FexitSettings> settings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EstadoPublicadoDto>>> Listar(CancellationToken ct) =>
        Ok(await repository.ListarPublicadosAsync(ct));

    /// <summary>
    /// No lleva `modoEsperado`: a diferencia de /acciones/{codigo}/ejecutar, acá no hay nada que
    /// verificar antes de leer. Leer es lo único que sabe hacer este endpoint.
    /// </summary>
    [HttpPost("leer")]
    public async Task<ActionResult<ResultadoAccion>> Leer([FromBody] LeerEstadosRequest req, CancellationToken ct)
    {
        // El tope existe para que un pedido no se convierta en un barrido del PLC. Falla como pedido
        // inválido, no como lectura parcial: quien pidió 500 códigos se equivocó, no el equipo.
        if (req.Codigos.Count > settings.Value.MaxEstadosPorPedido)
            throw new ParametrosInvalidosException(
                $"No se pueden leer más de {settings.Value.MaxEstadosPorPedido} estados por pedido.");

        var estados = await repository.BuscarParaLeerAsync(req.Codigos, ct);
        return Ok(await lector.LeerAsync(estados, req.Codigos, ct));
    }
}
