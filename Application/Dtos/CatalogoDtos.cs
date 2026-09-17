using Application.Constantes;

namespace Application.Dtos;

/// <summary>
/// Alta y edición de un controlador. A diferencia de AccionRemotaDto, acá SÍ viajan IP y puerto: es
/// el endpoint de configuración, y quien llega ya tiene la clave de la instalación. La promesa de §6
/// es sobre lo que Fexit le contesta a Dixit al ejecutar, no sobre su propio ABM.
/// </summary>
public record ControladorRequest(
    string Nombre, string TipoEquipo, string Ip, int Puerto, string Protocolo, int Rack, int Slot,
    // Último y con default para que sea opcional en el JSON: en Modbus y en Simulado se ignora, y
    // obligar a mandarlo ahí sería pedir un dato que no significa nada. El default es el mismo que la
    // columna.
    string Modelo = CteFexit.ModeloS7300);

public record ControladorDto(
    long Id, string Nombre, string TipoEquipo, string Ip, int Puerto, string Protocolo,
    string Modelo, int Rack, int Slot);

public record EnclavamientoRequest(
    string Direccion, string TipoDireccion, string Nombre, List<int> ValoresOk, int Orden);

public record EnclavamientoDto(
    long Id, long ControladorId, string Direccion, string TipoDireccion, string Nombre, List<int> ValoresOk, int Orden);

public record AccionRequest(
    string Codigo, string Descripcion, string Modo, long ControladorId,
    string? Direccion, string? TipoDireccion, int? Valor, bool UsaEnclavamientos, bool Habilitada,
    string? DefinicionParametrosJson = null, string? ConfigJson = null);

public record AccionCatalogoDto(
    long Id, string Codigo, string Descripcion, string Modo, long ControladorId,
    string? Direccion, string? TipoDireccion, int? Valor, bool UsaEnclavamientos, bool Habilitada,
    string? DefinicionParametrosJson = null, string? ConfigJson = null);
