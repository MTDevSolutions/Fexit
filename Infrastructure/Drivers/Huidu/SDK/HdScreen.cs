// Copiado de axControlBE (SDK de Huidu). Fuente de terceros: no se reformatea.
#nullable disable
using System.Collections.Generic;
using System.Xml;

namespace Infrastructure.Drivers.Huidu.SDK
{
    public class HdScreen
    {
        private ScreenParam _screenParam;

        public List<HdProgram> Programs { get; set; }

        public HdScreen(ScreenParam screenParam)
        {
            _screenParam = screenParam;
            Programs = new List<HdProgram>();
        }

        public XmlElement GetXmlElement(XmlDocument doc)
        {
            XmlElement screenElem = _screenParam.GetXmlElement(doc);
            foreach (HdProgram program in Programs)
            {
                screenElem.AppendChild(program.GetXmlElement(doc));
            }
            return screenElem;
        }
    }
}