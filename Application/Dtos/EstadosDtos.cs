namespace Application.Dtos;

/// <summary>
/// Un estado tal como lo publica Fexit. Es lo que el superadmin ve en Dixit al armar el catálogo del
/// proyecto. **No tiene dirección, ni tipo de dirección, ni controlador, ni etiquetas**: Dixit no las
/// necesita para armar el prompt, y lo que no sale no se filtra.
/// </summary>
public record EstadoPublicadoDto(string Codigo, string Nombre, string Descripcion, string Equipo, string Sector);

/// <summary>
/// Cuerpo del pedido de lectura. Sólo códigos: a diferencia de /acciones/{codigo}/ejecutar, acá no
/// hay nada que verificar antes de leer.
/// </summary>
public record LeerEstadosRequest(List<string> Codigos);
