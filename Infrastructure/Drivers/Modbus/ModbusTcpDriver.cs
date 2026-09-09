#nullable enable
using Infrastructure.Drivers;
using NModbus;
using Application.Constantes;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Drivers.Modbus
{
    /// <summary>
    /// Driver para comunicación Modbus TCP con PLCs.
    /// </summary>
    public class ModbusTcpDriver : PlcDriverBase
    {
        private TcpClient? _tcpClient;
        private IModbusMaster? _modbusMaster;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly byte _slaveId;
        private readonly int _timeout;

        /// <summary>
        /// Crea una instancia del driver Modbus TCP.
        /// </summary>
        /// <param name="ip">Dirección IP del PLC</param>
        /// <param name="port">Puerto (típicamente 502)</param>
        /// <param name="slaveId">ID del esclavo Modbus (default: 1)</param>
        /// <param name="timeout">Timeout en milisegundos (default: 3000)</param>
        public ModbusTcpDriver(
            string ip,
            int port,
            byte slaveId = 1,
            int timeout = 3000) : base(ip, port)
        {
            if (slaveId == 0)
                throw new ArgumentException("Slave ID no puede ser 0", nameof(slaveId));

            if (timeout <= 0)
                throw new ArgumentException("Timeout debe ser mayor a 0", nameof(timeout));

            _slaveId = slaveId;
            _timeout = timeout;
        }

        public override bool IsConnected => _tcpClient?.Connected ?? false;

        public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            // Liberar conexión anterior para evitar fuga de sockets en reconexión
            await DisconnectAsync();

            try
            {
                _tcpClient = new TcpClient
                {
                    SendTimeout = _timeout,
                    ReceiveTimeout = _timeout
                };

                await _tcpClient.ConnectAsync(_ip, _port, cancellationToken);

                var factory = new ModbusFactory();
                _modbusMaster = factory.CreateMaster(_tcpClient);

                return IsConnected;
            }
            catch
            {
                // Limpiar recursos si falla la conexión
                await DisconnectAsync();
                throw;
            }
        }

        public override async Task DisconnectAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _modbusMaster?.Dispose();
                _modbusMaster = null;

                if (_tcpClient != null)
                {
                    try
                    {
                        _tcpClient.Close();
                        _tcpClient.Dispose();
                    }
                    catch
                    {
                        // Ignorar errores al cerrar
                    }
                    finally
                    {
                        _tcpClient = null;
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
            // Validaciones
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("La dirección no puede estar vacía", nameof(address));

            if (length <= 0)
                throw new ArgumentException("La longitud debe ser mayor a 0", nameof(length));

            if (length > 125)
                throw new ArgumentException("La longitud máxima para Modbus es 125", nameof(length));

            if (!ushort.TryParse(address, out ushort startAddress))
                throw new ArgumentException($"Dirección inválida: '{address}'", nameof(address));

            // Thread-safety: solo una operación Modbus a la vez
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_modbusMaster == null)
                    throw new InvalidOperationException("No hay conexión Modbus establecida");

                // Ejecutar en thread pool para no bloquear (NModbus es síncrono)
                return await Task.Run(() => ReadInternal(type, startAddress, length), cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        private byte[] ReadInternal(TipoDireccionPlc type, ushort startAddress, int length)
        {
            return type switch
            {
                TipoDireccionPlc.Coil =>
                    BoolArrayToBytes(_modbusMaster!.ReadCoils(_slaveId, startAddress, (ushort)length)),

                TipoDireccionPlc.DiscreteInput =>
                    BoolArrayToBytes(_modbusMaster!.ReadInputs(_slaveId, startAddress, (ushort)length)),

                TipoDireccionPlc.HoldingRegister =>
                    UShortArrayToBytes(_modbusMaster!.ReadHoldingRegisters(_slaveId, startAddress, (ushort)length)),

                TipoDireccionPlc.InputRegister =>
                    UShortArrayToBytes(_modbusMaster!.ReadInputRegisters(_slaveId, startAddress, (ushort)length)),

                _ => throw new NotSupportedException($"Tipo de dirección no soportado: {type}")
            };
        }

        public override async Task WriteAsync(TipoDireccionPlc type, string address, byte[] data)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("La dirección no puede estar vacía", nameof(address));

            if (data == null || data.Length == 0)
                throw new ArgumentException("Los datos no pueden estar vacíos", nameof(data));

            if (!ushort.TryParse(address, out ushort startAddress))
                throw new ArgumentException($"Dirección inválida: '{address}'", nameof(address));

            // Thread-safety
            await _lock.WaitAsync();
            try
            {
                if (_modbusMaster == null)
                    throw new InvalidOperationException("No hay conexión Modbus establecida");

                // Ejecutar en thread pool
                await Task.Run(() => WriteInternal(type, startAddress, data), CancellationToken.None);
            }
            finally
            {
                _lock.Release();
            }
        }

        private void WriteInternal(TipoDireccionPlc type, ushort startAddress, byte[] data)
        {
            switch (type)
            {
                case TipoDireccionPlc.Coil:
                    WriteCoils(startAddress, data);
                    break;

                case TipoDireccionPlc.HoldingRegister:
                    WriteRegisters(startAddress, data);
                    break;

                default:
                    throw new NotSupportedException($"Escritura no soportada para tipo: {type}");
            }
        }

        private void WriteCoils(ushort startAddress, byte[] data)
        {
            if (data.Length == 1)
            {
                // Escribir un solo coil
                bool value = data[0] != 0;
                _modbusMaster!.WriteSingleCoil(_slaveId, startAddress, value);
            }
            else
            {
                // Escribir múltiples coils
                bool[] values = data.Select(b => b != 0).ToArray();
                _modbusMaster!.WriteMultipleCoils(_slaveId, startAddress, values);
            }
        }

        private void WriteRegisters(ushort startAddress, byte[] data)
        {
            if (data.Length % 2 != 0)
                throw new ArgumentException("Los datos para registros deben tener longitud par", nameof(data));

            ushort[] registers = BytesAUShortArray(data);

            if (registers.Length == 1)
            {
                // Escribir un solo registro (más eficiente)
                _modbusMaster!.WriteSingleRegister(_slaveId, startAddress, registers[0]);
            }
            else
            {
                // Escribir múltiples registros
                _modbusMaster!.WriteMultipleRegisters(_slaveId, startAddress, registers);
            }
        }

        /// <summary>
        /// Big-endian explícito, NO Buffer.BlockCopy. BlockCopy copia la representación en memoria del
        /// host —little-endian en x86— y del otro lado ConversorValores.AEntero lee big-endian, así que
        /// un registro que vale 1 se leía como 256 y escribir 1 mandaba 256 al PLC. Los dos lados eran
        /// correctos por separado; el error estaba en el par. Modbus es el protocolo de "Schneider y
        /// casi todos", así que esto afectaba a la mayoría de las instalaciones.
        /// </summary>
        internal static byte[] UShortArrayToBytes(ushort[] values)
        {
            byte[] result = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(
                    result.AsSpan(i * 2, 2), values[i]);
            return result;
        }

        /// <summary>
        /// Simétrico de <see cref="UShortArrayToBytes"/>: mismo motivo, mismo arreglo. Sin esto,
        /// WriteRegisters armaba el ushort[] con Buffer.BlockCopy (orden del host) a partir de bytes
        /// big-endian, y escribir 1 mandaba 256 al PLC.
        /// </summary>
        internal static ushort[] BytesAUShortArray(byte[] datos)
        {
            ushort[] registers = new ushort[datos.Length / 2];
            for (int i = 0; i < registers.Length; i++)
                registers[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(
                    datos.AsSpan(i * 2, 2));
            return registers;
        }

        private static byte[] BoolArrayToBytes(bool[] values)
            => values.Select(b => b ? (byte)1 : (byte)0).ToArray();

        protected override void Dispose(bool disposing)
        {
            // FEXIT: reordenado respecto del original. base.Dispose(disposing) ya llama a
            // DisconnectAsync() (despacho virtual), así que llamarla acá primero y disponer el
            // _lock antes de ese base.Dispose() hacía que la segunda llamada esperara sobre un
            // SemaphoreSlim ya disposed (ObjectDisposedException en todo Dispose()). Primero el
            // base (con el lock todavía vivo), después el lock.
            base.Dispose(disposing);

            if (disposing)
            {
                _lock?.Dispose();
            }
        }
    }
}
