using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Drivers;
using S7.Net;
using Application.Constantes;
using S7CpuType = S7.Net.CpuType;

namespace Infrastructure.Drivers.Siemens
{
    public sealed class SiemensS7Driver : PlcDriverBase
    {
        private readonly int _rack;
        private readonly int _slot;
        private readonly S7CpuType _tipoDeCpu;
        private readonly int _timeout;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private Plc? _plc;

        public SiemensS7Driver(
            string ip,
            int port,
            int rack,
            int slot,
            string modelo,
            int timeout = 3000) : base(ip, port)
        {
            if (rack < 0)
                throw new ArgumentException("Rack no puede ser negativo", nameof(rack));

            if (slot < 0)
                throw new ArgumentException("Slot no puede ser negativo", nameof(slot));

            if (timeout <= 0)
                throw new ArgumentException("Timeout debe ser mayor a 0", nameof(timeout));

            _rack = rack;
            _slot = slot;
            _tipoDeCpu = MapModeloToCpuType(modelo);
            _timeout = timeout;
        }

        public override bool IsConnected => _plc?.IsConnected ?? false;

        public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            // Liberar conexión anterior para evitar fuga de recursos en reconexión
            await DisconnectAsync();

            try
            {
                return await Task.Run(() =>
                {
                    _plc = new Plc(_tipoDeCpu, _ip, _port, (short)_rack, (short)_slot)
                    {
                        ReadTimeout = _timeout,
                        WriteTimeout = _timeout
                    };
                    _plc.Open();
                    return IsConnected;
                }, cancellationToken);
            }
            catch
            {
                await DisconnectAsync();
                throw;
            }
        }

        public override async Task DisconnectAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_plc != null)
                {
                    try
                    {
                        _plc.Close();
                    }
                    catch
                    {
                        // Ignorar errores al cerrar
                    }
                    finally
                    {
                        _plc = null;
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public override async Task<byte[]> ReadAsync(TipoDireccionPlc type, string address, int length, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("La dirección no puede estar vacía", nameof(address));

            if (length <= 0)
                throw new ArgumentException("La longitud debe ser mayor a 0", nameof(length));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_plc is null || !_plc.IsConnected)
                    throw new InvalidOperationException("No hay conexión S7 establecida");

                return await Task.Run(() =>
                {
                    var a = S7Address.Parse(address);

                    return type switch
                    {
                        TipoDireccionPlc.S7Bit => ReadBit(a),
                        TipoDireccionPlc.S7Byte => ReadBytes(a, Math.Max(1, length)),
                        TipoDireccionPlc.S7Word => ReadBytes(a, 2),
                        TipoDireccionPlc.S7DWord => ReadBytes(a, 4),
                        TipoDireccionPlc.S7Real => ReadBytes(a, 4),
                        _ => throw new NotSupportedException($"Tipo de dirección S7 no soportado: {type}")
                    };
                }, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public override async Task WriteAsync(TipoDireccionPlc type, string address, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("La dirección no puede estar vacía", nameof(address));

            if (data == null || data.Length == 0)
                throw new ArgumentException("Los datos no pueden estar vacíos", nameof(data));

            await _lock.WaitAsync();
            try
            {
                if (_plc is null || !_plc.IsConnected)
                    throw new InvalidOperationException("No hay conexión S7 establecida");

                await Task.Run(() =>
                {
                    var a = S7Address.Parse(address);

                    switch (type)
                    {
                        case TipoDireccionPlc.S7Bit:
                            WriteBit(a, data);
                            break;

                        case TipoDireccionPlc.S7Byte:
                            WriteBytes(a, data);
                            break;

                        case TipoDireccionPlc.S7Word:
                            ValidateDataLength(data, 2, type);
                            WriteBytes(a, data);
                            break;

                        case TipoDireccionPlc.S7DWord:
                        case TipoDireccionPlc.S7Real:
                            ValidateDataLength(data, 4, type);
                            WriteBytes(a, data);
                            break;

                        default:
                            throw new NotSupportedException($"Escritura no soportada para tipo S7: {type}");
                    }
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        private byte[] ReadBytes(S7Address a, int count)
        {
            if (_plc is null) throw new InvalidOperationException("PLC not initialized.");

            try
            {
                // S7.NetPlus: lee bytes “crudos”
                return _plc.ReadBytes(a.DataType, a.DbNumber, a.StartByte, count);
            }
            catch (PlcException)
            {
                // La primera lectura justo después de Open() puede chocar con la negociación
                // de PDU del PLC y responder con un Ack de error sin datos. Se reintenta una vez
                // antes de dar la conexión por rota.
                Thread.Sleep(100);

                try
                {
                    return _plc.ReadBytes(a.DataType, a.DbNumber, a.StartByte, count);
                }
                catch (PlcException)
                {
                    // PlcException es un rechazo de protocolo (dirección inválida, tipo no
                    // soportado, etc.), no un problema de conexión. Forzar el cierre acá tira
                    // abajo el resto de los tags de este PLC en el mismo ciclo de polling por un
                    // tag puntual que va a seguir fallando siempre, sin importar cuántas veces
                    // se reconecte.
                    throw;
                }
            }
            catch
            {
                // Un error que no es de protocolo S7 (socket, etc.) puede dejar el socket
                // abierto pero el stream desincronizado (Plc.IsConnected seguiría en true).
                // Se fuerza el cierre para que el próximo ciclo de polling reconecte.
                ForceCloseOnError();
                throw;
            }
        }

        private byte[] ReadBit(S7Address a)
        {
            var byteData = ReadBytes(a with { Bit = null }, 1);
            var bit = a.Bit ?? 0;

            if (bit < 0 || bit > 7)
                throw new ArgumentOutOfRangeException(nameof(a.Bit), $"El bit debe estar entre 0 y 7, recibido: {bit}");

            bool isSet = (byteData[0] & (1 << bit)) != 0;
            return new[] { (byte)(isSet ? 1 : 0) };
        }

        private void WriteBytes(S7Address a, byte[] data)
        {
            if (_plc is null)
                throw new InvalidOperationException("PLC no inicializado");

            try
            {
                _plc.WriteBytes(a.DataType, a.DbNumber, a.StartByte, data);
            }
            catch (PlcException)
            {
                Thread.Sleep(100);

                try
                {
                    _plc.WriteBytes(a.DataType, a.DbNumber, a.StartByte, data);
                }
                catch (PlcException)
                {
                    // Ídem ReadBytes: un rechazo de protocolo no es un problema de conexión.
                    throw;
                }
            }
            catch
            {
                ForceCloseOnError();
                throw;
            }
        }

        private void ForceCloseOnError()
        {
            try
            {
                _plc?.Close();
            }
            catch
            {
                // Ignorar errores al cerrar
            }
            finally
            {
                _plc = null;
            }
        }

        private void WriteBit(S7Address a, byte[] data)
        {
            if (data.Length < 1)
                throw new ArgumentException("Para escribir un bit se requiere al menos 1 byte (0/1)", nameof(data));

            var bit = a.Bit ?? throw new ArgumentException("La dirección de bit no especifica el índice de bit (0-7)", nameof(a.Bit));

            if (bit < 0 || bit > 7)
                throw new ArgumentOutOfRangeException(nameof(a.Bit), $"El bit debe estar entre 0 y 7, recibido: {bit}");

            var currentByte = ReadBytes(a with { Bit = null }, 1);
            byte mask = (byte)(1 << bit);

            bool setValue = data[0] != 0;

            currentByte[0] = setValue
                ? (byte)(currentByte[0] | mask)
                : (byte)(currentByte[0] & ~mask);

            WriteBytes(a with { Bit = null }, currentByte);
        }

        private static void ValidateDataLength(byte[] data, int expectedLength, TipoDireccionPlc type)
        {
            if (data.Length < expectedLength)
                throw new ArgumentException(
                    $"Para {type} se requieren {expectedLength} bytes, recibidos: {data.Length}",
                    nameof(data));
        }

        private readonly record struct S7Address(DataType DataType, int DbNumber, int StartByte, int? Bit)
        {
            private static readonly Regex DbxRegex = new(@"^DB(?<db>\d+)\.DBX(?<byte>\d+)\.(?<bit>[0-7])$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex DbbRegex = new(@"^DB(?<db>\d+)\.DBB(?<byte>\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex DbwRegex = new(@"^DB(?<db>\d+)\.DBW(?<byte>\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex DbdRegex = new(@"^DB(?<db>\d+)\.DBD(?<byte>\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex MemBitRegex = new(@"^(?<area>M|I|Q)(?<byte>\d+)\.(?<bit>[0-7])$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            private static readonly Regex MemByteRegex = new(@"^(?<area>MB|IB|QB)(?<byte>\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static S7Address Parse(string address)
            {
                if (string.IsNullOrWhiteSpace(address))
                    throw new ArgumentException("La dirección está vacía", nameof(address));

                address = address.Trim();

                var match = DbxRegex.Match(address);
                if (match.Success)
                {
                    return new S7Address(
                        DataType.DataBlock,
                        int.Parse(match.Groups["db"].Value),
                        int.Parse(match.Groups["byte"].Value),
                        int.Parse(match.Groups["bit"].Value));
                }

                match = DbbRegex.Match(address);
                if (match.Success)
                {
                    return new S7Address(
                        DataType.DataBlock,
                        int.Parse(match.Groups["db"].Value),
                        int.Parse(match.Groups["byte"].Value),
                        null);
                }

                match = DbwRegex.Match(address);
                if (match.Success)
                {
                    return new S7Address(
                        DataType.DataBlock,
                        int.Parse(match.Groups["db"].Value),
                        int.Parse(match.Groups["byte"].Value),
                        null);
                }

                match = DbdRegex.Match(address);
                if (match.Success)
                {
                    return new S7Address(
                        DataType.DataBlock,
                        int.Parse(match.Groups["db"].Value),
                        int.Parse(match.Groups["byte"].Value),
                        null);
                }

                match = MemBitRegex.Match(address);
                if (match.Success)
                {
                    var area = match.Groups["area"].Value.ToUpperInvariant();
                    var dataType = area switch
                    {
                        "M" => DataType.Memory,
                        "I" => DataType.Input,
                        "Q" => DataType.Output,
                        _ => throw new NotSupportedException($"Área de memoria no soportada: {area}")
                    };

                    return new S7Address(
                        dataType,
                        DbNumber: 0,
                        StartByte: int.Parse(match.Groups["byte"].Value),
                        Bit: int.Parse(match.Groups["bit"].Value));
                }

                match = MemByteRegex.Match(address);
                if (match.Success)
                {
                    var area = match.Groups["area"].Value.ToUpperInvariant();
                    var dataType = area switch
                    {
                        "MB" => DataType.Memory,
                        "IB" => DataType.Input,
                        "QB" => DataType.Output,
                        _ => throw new NotSupportedException($"Área de memoria no soportada: {area}")
                    };

                    return new S7Address(
                        dataType,
                        DbNumber: 0,
                        StartByte: int.Parse(match.Groups["byte"].Value),
                        Bit: null);
                }

                throw new FormatException($"Formato de dirección S7 no válido: '{address}'. " +
                    "Formatos soportados: DBx.DBXx.x, DBx.DBBx, DBx.DBWx, DBx.DBDx, Mx.x, MBx, Ix.x, IBx, Qx.x, QBx");
            }
        }

        /// <summary>
        /// Los cinco que conoce S7netplus. Si aparece uno que no está, no alcanza con agregar la
        /// constante: la librería no lo sabe manejar.
        /// </summary>
        private static S7CpuType MapModeloToCpuType(string modelo) => modelo switch
        {
            CteFexit.ModeloS7200 => S7CpuType.S7200,
            CteFexit.ModeloS7300 => S7CpuType.S7300,
            CteFexit.ModeloS7400 => S7CpuType.S7400,
            CteFexit.ModeloS71200 => S7CpuType.S71200,
            CteFexit.ModeloS71500 => S7CpuType.S71500,
            _ => throw new NotSupportedException($"Modelo de Siemens S7 no soportado: {modelo}")
        };

        protected override void Dispose(bool disposing)
        {
            // FEXIT: reordenado respecto del original. base.Dispose(disposing) ya llama a
            // DisconnectAsync() (despacho virtual, cierra _plc bajo el lock), así que el cierre
            // manual de acá y disponer el _lock antes del base.Dispose() hacía que esa segunda
            // llamada esperara sobre un SemaphoreSlim ya disposed (ObjectDisposedException en todo
            // Dispose()). Primero el base (con el lock todavía vivo), después el lock.
            base.Dispose(disposing);

            if (disposing)
            {
                _lock?.Dispose();
            }
        }
    }
}
