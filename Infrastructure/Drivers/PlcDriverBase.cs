using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Constantes;

namespace Infrastructure.Drivers
{
    public abstract class PlcDriverBase : IPlcDriver, IDisposable
    {
        protected readonly string _ip;
        protected readonly int _port;
        private bool _disposed;

        protected PlcDriverBase(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public abstract bool IsConnected { get; }

        public abstract Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        public abstract Task DisconnectAsync();

        public abstract Task<byte[]> ReadAsync(TipoDireccionPlc type, string address, int length, CancellationToken cancellationToken = default);

        public abstract Task WriteAsync(TipoDireccionPlc type, string address, byte[] data);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Liberar recursos administrados
                    DisconnectAsync().GetAwaiter().GetResult();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
