// Copiado de axControlBE (SDK de Huidu). Fuente de terceros: no se reformatea.
#nullable disable
namespace Infrastructure.Drivers.Huidu.SDK
{
    public class AsyncEventArgs
    {
        /// <summary>
        /// 提示信息
        /// </summary>
        public string _msg;

        /// <summary>
        /// 客户端状态封装类
        /// </summary>
        public TcpClientState _state;

        public Devices _device;

        /// <summary>
        /// 是否已经处理过了
        /// </summary>
        public bool IsHandled { get; set; }

        public AsyncEventArgs(string msg)
        {
            _msg = msg;
            IsHandled = false;
        }

        public AsyncEventArgs(TcpClientState state)
        {
            _state = state;
            IsHandled = false;
        }

        public AsyncEventArgs(Devices device)
        {
            _device = device;
            IsHandled = false;
        }

        public AsyncEventArgs(string msg, TcpClientState state)
        {
            _msg = msg;
            _state = state;
            IsHandled = false;
        }
    }
}