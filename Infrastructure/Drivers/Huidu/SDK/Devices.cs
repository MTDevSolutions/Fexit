// Copiado de axControlBE (SDK de Huidu). Fuente de terceros: no se reformatea.
#nullable disable
using Infrastructure.Drivers.Huidu.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Infrastructure.Drivers.Huidu
{
    // Asegúrate de que todas las declaraciones parciales de la clase Device utilizan el mismo modificador de accesibilidad.
    public partial class Devices
    {
        /// <summary>
        /// Inicializa el dispositivo cuando el programa actúa como cliente.
        /// </summary>
        /// <param name="comm">Objeto de gestión de comunicaciones.</param>
        /// <param name="client">Objeto de conexión TCP.</param>
        internal Devices(HDCommunicationManager comm, TcpClient client) : this(comm, new TcpClientState(client))
        {
        }

        /// <summary>
        /// Inicializa el dispositivo cuando el programa actúa como servidor.
        /// </summary>
        /// <param name="comm">Objeto de gestión de comunicaciones.</param>
        /// <param name="client">Objeto de conexión TCP encapsulado en TcpClientState.</param>
        internal Devices(HDCommunicationManager comm, TcpClientState client)
        {
            CommunicationManager = comm;
            Client = client;

            // Inicialización de la cola de envío
            _sendQueue = new Queue<byte[]>();
            TransportProtocolVersion = DataProtocol._LOCAL_TCP_VERSION;
            SdkProtocolVersion = DataProtocol._SDK_VERSION;
            EnsureProtocolVersion = EnsureSdkProtocolVersion = false;
            UploadItems = new List<UploadFileInfo>();
            _deviceInfo = new DeviceInfo();
            UploadingItem = new UploadFileInfo();
        }

        /// <summary>
        /// Clase interna que indica los datos que se están enviando.
        /// </summary>
        private class SendingInfo
        {
            public byte[] sendingData;
            // public int startSendTick;
            // public int lastSendTime = System.Environment.TickCount;
        }

        /// <summary>
        /// Proporciona la conexión del cliente para servicios de red TCP.
        /// </summary>
        public TcpClientState Client { get; private set; }

        /// <summary>
        /// Objeto de gestión de comunicaciones.
        /// </summary>
        internal HDCommunicationManager CommunicationManager { get; private set; }

        /// <summary>
        /// Versión del protocolo de transporte.
        /// </summary>
        internal int TransportProtocolVersion { get; private set; }

        /// <summary>
        /// Versión del protocolo SDK.
        /// </summary>
        internal int SdkProtocolVersion { get; private set; }

        /// <summary>
        /// Indica si ya se ha confirmado la versión del protocolo de transporte.
        /// </summary>
        internal bool EnsureProtocolVersion { get; private set; }

        /// <summary>
        /// Indica si ya se ha confirmado la versión del protocolo SDK.
        /// </summary>
        internal bool EnsureSdkProtocolVersion { get; private set; }

        /// <summary>
        /// Identificador único de la conexión actual.
        /// </summary>
        internal string SdkGuid { get; set; }

        /// <summary>
        /// Versión del SDK.
        /// </summary>
        internal string SdkVersion { get; private set; }

        /// <summary>
        /// Datos XML recibidos.
        /// </summary>
        internal byte[] SDKCmdAnswerXmlData { get; private set; }

        // internal bool IsSending { get; private set; }  // Indicador de que se está enviando datos

        /// <summary>
        /// Lista actual de archivos a subir.
        /// </summary>
        public List<UploadFileInfo> UploadItems { get; private set; }

        /// <summary>
        /// Cola actual de datos a enviar.
        /// </summary>
        private Queue<byte[]> _sendQueue;

        /// <summary>
        /// Objeto para sincronización.
        /// </summary>
        private Object _dataLock = new Object();

        private SendingInfo _sendingInfo = new SendingInfo();
        internal int LastSendRecvDataTime = System.Environment.TickCount; // Última vez que se enviaron o recibieron datos
        internal int LastSendTime = System.Environment.TickCount;         // Última vez que se enviaron datos

        private UploadFileInfo UploadingItem;
        private DeviceInfo _deviceInfo;

        /// <summary>
        /// Indica si se está enviando un archivo.
        /// </summary>
        public bool SendingFile { get; set; }

        /// <summary>
        /// Indica si ya se ha comenzado a enviar el contenido del archivo
        /// (cuando no se recibe respuesta del dispositivo, se envía el siguiente paquete según el estado de envío).
        /// </summary>
        internal bool HasStartSendFileContext { get; set; }

        /// <summary>
        /// Indica si se está descargando un archivo.
        /// </summary>
        internal bool DowningFile { get; set; }

        /// <summary>
        /// Inicializa la versión de comunicación e información del dispositivo.
        /// </summary>
        internal void InitVersionAndDeviceInfo()
        {
            // Envía el comando de negociación de versión del protocolo
            SendEnsureProtocolVersionCmd();

            // Envía el comando de negociación de versión del SDK
            SendEnsureSdkVersionCmd();
        }

        /// <summary>
        /// Finaliza el envío.
        /// </summary>
        /// <param name="strError">mensaje de error (opcional).</param>
        internal void EndToSend(string strError = "")
        {
            _sendingInfo = new SendingInfo();
            // IsSending = false;

            if (SendingFile)
            {
                UploadingItem.Dispose();
                SendingFile = false;
                UploadingItem = new UploadFileInfo();
            }

            lock (_dataLock)
            {
                _sendQueue.Clear();
            }

            if (strError != null && strError.Length > 0)
            {
                CommunicationManager.ReportMsg(this, _deviceInfo.deviceID + " : " + strError);
            }
        }

        /// <summary>
        /// Envía el siguiente paquete de datos.
        /// </summary>
        internal void SendNext()
        {
            // IsSending = false;
            _sendingInfo = new SendingInfo();
            TryToSend();
        }

        /// <summary>
        /// Intenta enviar datos.
        /// </summary>
        private void TryToSend()
        {
            // Si la conexión ha finalizado, se trata como desconexión
            if (!Client.TcpClient.Connected)
            {
                EndToSend();
                return;
            }

            if (_sendingInfo.sendingData == null)
            {
                lock (_dataLock)
                {
                    if (_sendQueue.Count > 0)
                    {
                        _sendingInfo.sendingData = _sendQueue.Dequeue();

                        try
                        {
                            CommunicationManager.Send(this, _sendingInfo.sendingData);
                            // IsSending = true;
                        }
                        catch (Exception exp)
                        {
                            CommunicationManager.ReportMsg(this, HDCommunicationManager.GetLogMsgString(this, exp.Message));
                            EndToSend();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Cierra la conexión actual del dispositivo.
        /// </summary>
        public void Close()
        {
            if (Client != null)
            {
                Client.Close();
                Client.RecvedLength = 0;
            }
        }

        /// <summary>
        /// Verifica tiempos de espera y envía automáticamente los datos en la cola de envío.
        /// </summary>
        internal bool CheckTimeOutAndAutoSend()
        {
            bool hasOffline = false;
            AutoSend();

            // Si ha transcurrido demasiado tiempo o la conexión se ha perdido, se considera como desconexión
            if (System.Environment.TickCount - LastSendRecvDataTime > 90000 || !Client.TcpClient.Connected)
            {
                EndToSend();
                hasOffline = true;
            }

            return hasOffline;
        }

        /// <summary>
        /// Envía datos automáticamente.
        /// </summary>
        private void AutoSend()
        {
            try
            {
                // Si hay paquetes pendientes, intenta enviarlos
                TryToSend();

                // En estado inactivo, cada 30 segundos envía un paquete de latido para mantener la conexión TCP
                if (_sendQueue.Count == 0 && _sendingInfo.sendingData == null)
                {
                    if (System.Environment.TickCount - LastSendTime > 30000)
                    {
                        LastSendTime = System.Environment.TickCount;
                        SendHeartMsn();
                    }
                }
            }
            catch (Exception exp)
            {
                CommunicationManager.ReportMsg(this, HDCommunicationManager.GetLogMsgString(this, exp.Message));
            }
        }

        /// <summary>
        /// Envía el comando para confirmar la versión del protocolo de transporte.
        /// </summary>
        internal void SendEnsureProtocolVersionCmd()
        {
            if (!EnsureProtocolVersion)
            {
                byte[] tpv = DataProtocol.GetTransportProtocolVersionCmd(TransportProtocolVersion);
                lock (_dataLock)
                {
                    _sendQueue.Enqueue(tpv);
                }
                TryToSend();
            }
        }

        /// <summary>
        /// Envía el comando para confirmar la versión del protocolo SDK.
        /// </summary>
        internal void SendEnsureSdkVersionCmd()
        {
            if (!EnsureSdkProtocolVersion)
            {
                byte[] sdkv = DataProtocol.GetSdkVersionCmd(SdkProtocolVersion);
                lock (_dataLock)
                {
                    _sendQueue.Enqueue(sdkv);
                }
                TryToSend();
            }
        }

        /// <summary>
        /// Envía datos a partir de un XML.
        /// </summary>
        /// <param name="xml">Cadena de datos XML.</param>
        /// <param name="useCurrentSdkGuid">
        /// Indica si se debe usar el GUID actual del SDK (por defecto true), ya que cada conexión establece un GUID distinto;
        /// en ese caso se reemplaza el sdkGUID del documento por el actual.
        /// </param>
        public void SendFromXml(string xml, bool useCurrentSdkGuid = true)
        {
            string newXml = xml;

            if (useCurrentSdkGuid)
            {
                SdkXmlDocument doc = new SdkXmlDocument();
                doc.LoadXml(xml);
                if (doc.IsSdkXmlData)
                {
                    foreach (XmlNode node in doc)
                    {
                        if (node.Name == "sdk")
                        {
                            node.Attributes["guid"].Value = SdkGuid;
                            break;
                        }
                    }

                    MemoryStream stream = new MemoryStream();
                    XmlWriterSettings setting = new XmlWriterSettings
                    {
                        Encoding = Encoding.UTF8,
                        Indent = true
                    };
                    XmlWriter writer = XmlWriter.Create(stream, setting);
                    doc.Save(writer);
                    newXml = Encoding.UTF8.GetString(stream.ToArray());
                }
            }

            SendXmlData(newXml);
        }

        /// <summary>
        /// Envía directamente datos XML.
        /// </summary>
        /// <param name="xml">Cadena de datos XML.</param>
        private void SendXmlData(string xml)
        {
            var list = DataProtocol.GetSDKCmdAsk(xml);
            foreach (var data in list)
            {
                lock (_dataLock)
                {
                    _sendQueue.Enqueue(data);
                }
            }

            TryToSend();
        }

        /// <summary>
        /// Finaliza el envío de datos.
        /// </summary>
        internal void CompletedSend()
        {
            LastSendRecvDataTime = System.Environment.TickCount;
            LastSendTime = System.Environment.TickCount;
            _sendingInfo.sendingData = null;
            // IsSending = false;

            // Si ya se inició el envío del contenido de un archivo, envía el siguiente paquete
            if (HasStartSendFileContext)
            {
                SendNextFilePacket();
            }
        }

        /// <summary>
        /// Continúa con la carga del siguiente archivo.
        /// </summary>
        private void ContinueUploadFile()
        {
            if (UploadItems.Count > 0 && !SendingFile)
            {
                var item = UploadItems[0];
                UploadingItem = item;
                UploadItems.Remove(item);

                SendingFile = true;
                UploadingItem.isSending = true;
                HasStartSendFileContext = false;

                byte[] startUpload = DataProtocol.GetUploadFileStartAsk(Path.GetFileName(item.path), item.md5, (int)item.fs.Length, (int)item.type);
                lock (_dataLock)
                {
                    _sendQueue.Enqueue(startUpload);
                }
                TryToSend();
            }
        }

        /// <summary>
        /// Obtiene el tipo de archivo según la extensión del archivo.
        /// </summary>
        /// <param name="filePath">Ruta del archivo.</param>
        /// <returns>Tipo de archivo.</returns>
        public static HFileType GetHFileType(string filePath)
        {
            HFileType ftype = HFileType.kImageFile;
            string ext = Path.GetExtension(filePath).ToLower();
            List<string> imageExt = new List<string> { ".bmp", ".jpg", ".jpeg", ".png", ".ico", ".gif", ".tif", ".tif" };
            List<string> videoExt = new List<string> { ".mp3", ".swf", ".f4v", ".trp", ".wmv", ".asf", ".mpeg", ".webm", ".asx", ".rm", ".rmvb", ".mp4", ".3gp", ".mov", ".m4v", ".avi", ".dat", ".mkv", ".flv", ".vob", ".ts" };
            List<string> fontExt = new List<string> { ".ttc", ".ttf", ".bdf" };
            List<string> firewareExt = new List<string> { ".bin" };
            List<string> programTemplateExt = new List<string> { ".xml" };
            if (imageExt.Find(s => s == ext) != null)
            {
                ftype = HFileType.kImageFile;
            }
            else if (videoExt.Find(s => s == ext) != null)
            {
                ftype = HFileType.kVideoFile;
            }
            else if (fontExt.Find(s => s == ext) != null)
            {
                ftype = HFileType.kFont;
            }
            else if (firewareExt.Find(s => s == ext) != null)
            {
                ftype = HFileType.kImageFile;
            }
            else if (programTemplateExt.Find(s => s == ext) != null)
            {
                if (Path.GetFileName(filePath).ToLower() == "fpga.xml")
                {
                    ftype = HFileType.kFPGAConfig;
                }
                else if (Path.GetFileName(filePath).ToLower() == "config.xml")
                {
                    ftype = HFileType.kSettingCofnig;
                }
                else
                {
                    ftype = HFileType.kProgramTemplate;
                }
            }

            return ftype;
        }

        /// <summary>
        /// Agrega un archivo para subir.
        /// </summary>
        /// <param name="filePath">Ruta del archivo.</param>
        /// <param name="type">Tipo de archivo.</param>
        /// <returns>Objeto que contiene la información del archivo a subir.</returns>
        public UploadFileInfo AddUploadFile(string filePath, HFileType type = HFileType.kauto)
        {
            bool tempFile = false;
            if (type == HFileType.kTempImageFile || type == HFileType.kTempVideoFile)
            {
                tempFile = true;
            }
            return AddUploadFile(filePath, tempFile, type);
        }

        /// <summary>
        /// Agrega un archivo para subir.
        /// </summary>
        /// <param name="filePath">Ruta del archivo.</param>
        /// <param name="tempFile">Indica si es un archivo temporal.</param>
        /// <param name="type">Tipo de archivo.</param>
        /// <returns>Objeto que contiene la información del archivo a subir.</returns>
        public UploadFileInfo AddUploadFile(string filePath, bool tempFile = false, HFileType type = HFileType.kauto)
        {
            if (UploadingItem.path == filePath)
            {
                return UploadingItem;
            }

            foreach (var item in UploadItems)
            {
                if (item.path == filePath)
                {
                    return item;
                }
            }

            FileStream fs = File.Open(filePath, FileMode.Open);
            MD5CryptoServiceProvider md5Hasher = new MD5CryptoServiceProvider();
            byte[] data = md5Hasher.ComputeHash(fs);
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            UploadFileInfo sendFileInfo = new UploadFileInfo();
            sendFileInfo.md5 = sBuilder.ToString().ToLower();
            sendFileInfo.path = filePath;
            sendFileInfo.fs = fs;
            HFileType t = GetHFileType(filePath);
            if (tempFile && (t == HFileType.kVideoFile || t == HFileType.kImageFile))
            {
                if (t == HFileType.kVideoFile)
                {
                    type = HFileType.kTempVideoFile;
                }
                else
                {
                    type = HFileType.kTempImageFile;
                }
            }

            sendFileInfo.type = type != HFileType.kauto ? type : t;
            fs.Position = 0;

            UploadItems.Add(sendFileInfo);

            return sendFileInfo;
        }

        /// <summary>
        /// Inicia la carga de archivos.
        /// </summary>
        public void StartUploadFile()
        {
            ContinueUploadFile();
        }

        /// <summary>
        /// Elimina un archivo de la lista de cargas.
        /// </summary>
        /// <param name="fileinfo">Información del archivo a eliminar.</param>
        public void RemoveUploadFile(UploadFileInfo fileinfo)
        {
            if (UploadingItem == fileinfo)
            {
                SendingFile = false;
                UploadingItem = new UploadFileInfo();
            }

            if (fileinfo.fs != null)
            {
                fileinfo.fs.Close();
            }
            UploadItems.Remove(fileinfo);

            ContinueUploadFile();
        }

        /// <summary>
        /// Inicia la transferencia de archivo directamente a partir de la ruta del archivo.
        /// </summary>
        /// <param name="filePath">Ruta del archivo.</param>
        /// <param name="tempFile">Indica si es un archivo temporal.</param>
        /// <param name="type">Tipo de archivo.</param>
        /// <returns>Objeto que contiene la información del archivo a subir.</returns>
        public UploadFileInfo StartUploadFile(string filePath, bool tempFile, HFileType type = HFileType.kauto)
        {
            foreach (UploadFileInfo obj in UploadItems)
            {
                if (filePath == obj.path)
                {
                    return obj;
                }
            }

            UploadFileInfo sendFileInfo = AddUploadFile(filePath, tempFile, type);

            StartUploadFile();

            return sendFileInfo;
        }

        /// <summary>
        /// Envía un paquete de latido para mantener la conexión.
        /// </summary>
        public void SendHeartMsn()
        {
            if (_sendQueue.Count == 0)
            {
                byte[] data = DataProtocol.GetkTcpHeartbeatAsk();
                lock (_dataLock)
                {
                    _sendQueue.Enqueue(data);
                }
                TryToSend();

                // Imprime la información del envío del paquete de latido
                string strTip = DateTime.Now.ToString() + "  " + GetDeviceInfo().deviceID + " (Enviando kTcpHeartbeatAsk)";
                CommunicationManager.ReportMsg(this, strTip);
            }
        }

        /// <summary>
        /// Actualiza la información del dispositivo.
        /// </summary>
        public void RefreshDeviceInfo()
        {
            // Obtiene la información del dispositivo
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.GetDeviceInfo.ToString());
            SendXmlData(xml);

            // Obtiene la información de la red Ethernet
            GetEthernetInfo();

            // La obtención de información 3G/4G o Wifi se puede activar según sea necesario
        }

        /// <summary>
        /// Obtiene la información del dispositivo.
        /// </summary>
        /// <returns>Información del dispositivo.</returns>
        public DeviceInfo GetDeviceInfo()
        {
            return _deviceInfo;
        }

        /// <summary>
        /// Obtiene la información de la dirección de red Ethernet.
        /// </summary>
        public void GetEthernetInfo()
        {
            string xml = new EthernetInfo().GetCmdToXml(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura la información de la dirección de red Ethernet.
        /// </summary>
        public void SetEthernetInfo(EthernetInfo info)
        {
            string xml = info.SetCmdToXml(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Obtiene la información de Wifi.
        /// </summary>
        public void GetWifiInfo()
        {
            string xml = new WifiInfo().GetCmdToXml(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura la información de Wifi.
        /// </summary>
        public void SetWifiInfo(WifiInfo info)
        {
            string xml = info.SetCmdToXml(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Obtiene la información de la luminosidad.
        /// </summary>
        public void GetLuminanceInfo()
        {
            string xml = new LuminanceInfo().GetXml_GetLuminanceInfo(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura la información de la luminosidad.
        /// </summary>
        /// <param name="luminanceInfo">Datos de luminosidad a configurar.</param>
        public void SetLuminanceInfo(LuminanceInfo luminanceInfo)
        {
            string xml = luminanceInfo.SetLuminanceInfoToXml(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Obtiene la información de la hora.
        /// </summary>
        public void GetTimeInfo()
        {
            string xml = new TimeInfo().GetXml_GetTimeInfo(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura la información de la hora.
        /// </summary>
        /// <param name="timeInfo">Datos de tiempo a configurar.</param>
        public void SetTimeInfo(TimeInfo timeInfo)
        {
            string xml = timeInfo.SetTimeInfoToXml(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Enciende la pantalla.
        /// </summary>
        public void OpenScreen()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.OpenScreen.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Apaga la pantalla.
        /// </summary>
        public void CloseScreen()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.CloseScreen.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Obtiene la información de encendido y apagado.
        /// </summary>
        public void GetSwitchTimeInfo()
        {
            string xml = new SwitchTimeInfo().GetXml_GetSwitchTimeInfo(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura la información de encendido y apagado.
        /// </summary>
        /// <param name="switchTimeInfo">Datos de encendido/apagado a configurar.</param>
        public void SetSwitchTimeInfo(SwitchTimeInfo switchTimeInfo)
        {
            string xml = switchTimeInfo.SetSwitchTimeInfoToXml(SdkGuid);
            SendXmlData(xml);
        }

        /// <summary>
        /// Obtiene la información de la pantalla de inicio.
        /// </summary>
        public void GetBootLogoInfo()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.GetBootLogo.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura la información de la pantalla de inicio.
        /// </summary>
        /// <param name="bootLogo">Datos de la pantalla de inicio.</param>
        public void SetBootLogoInfo(BootLogoInfo bootLogo)
        {
            var list = bootLogo.GetXmlElements(null);
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.SetBootLogoName.ToString(), list);
            SendXmlData(xml);
        }

        /// <summary>
        /// Envía la pantalla (refresca todos los programas).
        /// </summary>
        /// <param name="screen">Objeto que contiene la información de la pantalla.</param>
        /// <returns>Cadena XML enviada.</returns>
        public string SendScreen(HdScreen screen)
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.AddProgram.ToString(), screen.GetXmlElement(new XmlDocument()));
            SendXmlData(xml);
            return xml;
        }

        /// <summary>
        /// Actualiza el programa especificado.
        /// </summary>
        /// <param name="program">Objeto que contiene la información del programa.</param>
        /// <returns>Cadena XML enviada.</returns>
        public string UpdateDeviceProgram(HdProgram program)
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.UpdateProgram.ToString(), program.GetXmlElement(new XmlDocument()));
            SendXmlData(xml);
            return xml;
        }

        /// <summary>
        /// Elimina el programa especificado.
        /// </summary>
        /// <param name="program">Objeto que contiene la información del programa.</param>
        /// <returns>Cadena XML enviada.</returns>
        public string DeleteDeviceProgram(HdProgram program)
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.DeleteProgram.ToString(), program.GetXmlElement(new XmlDocument()));
            SendXmlData(xml);
            return xml;
        }

        /// <summary>
        /// Obtiene la información de fuentes.
        /// </summary>
        public void GetDeviceFontInfo()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.GetAllFontInfo.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Obtiene la información del servidor.
        /// </summary>
        public void GetTcpServerInfo()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.GetSDKTcpServer.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Obtiene el indicador de sincronización de múltiples pantallas.
        /// </summary>
        public void GetMulScreenSync()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.GetMulScreenSync.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura el indicador de sincronización de múltiples pantallas.
        /// </summary>
        public void SetMulScreenSync()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.SetMulScreenSync.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Configura la información del servidor.
        /// </summary>
        /// <param name="info">Objeto con la información del servidor.</param>
        public void SetTcpServerInfo(ServerInfo info)
        {
            var list = info.GetXmlElements(null);
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.SetSDKTcpServer.ToString(), list);
            SendXmlData(xml);
        }

        /// <summary>
        /// Elimina los archivos subidos al dispositivo.
        /// </summary>
        /// <param name="fileNames">Lista de nombres de archivos.</param>
        public void DeleteFile(List<string> fileNames)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement filesElem = doc.CreateElement("files");
            foreach (string name in fileNames)
            {
                XmlElement itemElem = doc.CreateElement("file");
                itemElem.SetAttribute("name", name);
                filesElem.AppendChild(itemElem);
            }
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.DeleteFiles.ToString(), filesElem);
            SendXmlData(xml);
        }

        /// <summary>
        /// Elimina un archivo.
        /// </summary>
        /// <param name="fileName">Nombre del archivo.</param>
        public void DeleteFile(string fileName)
        {
            DeleteFile(new List<string> { fileName });
        }

        /// <summary>
        /// Realiza la lectura inversa (readback) del archivo.
        /// </summary>
        public void ReadbackFile()
        {
            // Implementación pendiente
        }

        /// <summary>
        /// Realiza la lectura inversa de la lista de archivos subidos al dispositivo.
        /// </summary>
        public void ReadbackFileList()
        {
            string xml = DataProtocol.GetSdkCmdXml(SdkGuid, SdkMethod.GetFiles.ToString());
            SendXmlData(xml);
        }

        /// <summary>
        /// Descarga un archivo desde el dispositivo.
        /// </summary>
        /// <param name="srcName">Nombre del archivo de origen.</param>
        /// <param name="savePath">Ruta para guardar el archivo.</param>
        public void DownloadFileFromDevice(string srcName, string savePath)
        {
            byte[] startUpload = DataProtocol.GetDownloadFileStartAsk(srcName);
            lock (_dataLock)
            {
                _sendQueue.Enqueue(startUpload);
            }
            TryToSend();
        }
    }
}