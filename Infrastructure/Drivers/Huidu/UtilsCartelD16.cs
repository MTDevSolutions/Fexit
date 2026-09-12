using System.Security;

namespace Infrastructure.Drivers.Huidu;

/// <summary>
/// Copiado de axControlBE (UtilsCartelD16). La regla de largo — hasta 6 caracteres fijo con letra
/// chica, más largo con scroll — es la pedida en el spec 2026-09-12 §3.3.
/// </summary>
internal static class UtilsCartelD16
{
    public static string BuildDinamico(MensajeCartel dto)
    {
        // 0. Los modos que no usan texto devuelven su propio programa: no les
        // aplica nada de la lógica de largo, scroll y tamaño de fuente.
        switch (dto.Modo)
        {
            case ModoMensajeCartel.Logo:
                return Paso_logo;
            case ModoMensajeCartel.FullVerde:
                return BuildFullColor("#00ff00");
            case ModoMensajeCartel.FullRojo:
                return BuildFullColor("#ff0000");
        }

        // 1. Determinar el color según el modo del cliente
        string modo = dto.Modo switch
        {
            ModoMensajeCartel.Rojo => "#ff0000", // Rojo
            ModoMensajeCartel.Verde => "#00ff00", // Verde
            ModoMensajeCartel.Amarillo => "#ffff80", // Amarillo
            ModoMensajeCartel.Blanco => "#FFFFFF",// blanco
            _ => "#FFFFFF"  // Blanco por defecto
        };

        // 2. Lógica basada en el Length del texto
        string mensaje = SecurityElement.Escape(dto.Texto ?? string.Empty);
        int largo = mensaje.Length;
        bool esLargo = largo > 6;

        string speed = "10";
        if (esLargo)
        {
            mensaje += " - "; // Espacio extra para el scroll

            // Ejemplo de lógica:
            // Texto de 10 chars -> Speed 12
            // Texto de 50 chars -> Speed 3
            if (largo < 18) speed = "12";
            else if (largo < 30) speed = "16";
            else speed = "4"; // Muy largo, muy rápido
        }

        // 3. Configurar comportamiento (Fijo vs Variable)
        // Si es largo, usamos efecto de movimiento (in="26" es11oll en Huidu)
        // Si es corto, efecto estático (in="0")

        string effectIn = esLargo ? "26" : "0";
        string singleLine = esLargo ? "true" : "false";
        string fontSize = esLargo ? "25" : "19";

        // 4. Inyectar todo en el template único
        return Template_Texto
            .Replace("{msj}", mensaje)
            .Replace("{color}", modo)
            .Replace("{fontSize}", fontSize)
            .Replace("{isSingleLine}", singleLine)
            .Replace("{effectIn}", effectIn)
            .Replace("{speed}", speed);
    }

    /// <summary>
    /// Pinta el panel entero de un color, sin texto. El color de la fuente va igual
    /// al del fondo a propósito: si el panel llegara a dibujar algo en el string
    /// vacío, queda invisible igual.
    /// </summary>
    private static string BuildFullColor(string color)
        => Template_FullColor.Replace("{color}", color);

    private const string Template_FullColor = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <sdk guid=""948fc142b6cc60b0da376e9d0186fafc"">
                                          <in method=""AddProgram"">
                                            <screen timeStamps=""1694718748428"">
                                              <program type=""normal"" id=""0"" guid=""6f7e9086-8058-4cfa-8f7c-83328e2de690"" name="""">
                                                <backgroundMusic />
                                                <playControl count=""1"" disabled=""false"" />
                                                <area guid=""940efdfa-96dc-41ea-9741-4a4a99d1baca"" name="""" alpha=""255"">
                                                  <rectangle x=""0"" y=""0"" width=""80"" height=""40"" />
                                                  <resources>
                                                    <text guid=""def4fbe6-6901-411c-bf97-1c6b5bfe70aa"" name="""" singleLine=""false"" background=""{color}"">
                                                      <style align=""center"" valign=""middle"" />
                                                      <string></string>
                                                      <font name=""Arial"" size=""10"" color=""{color}"" bold=""true"" />
                                                      <effect in=""0"" inSpeed=""0"" out=""0"" outSpeed=""0"" duration=""30"" />
                                                    </text>
                                                  </resources>
                                                </area>
                                              </program>
                                            </screen>
                                          </in>
                                        </sdk>";

    private const string Template_Texto = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <sdk guid=""948fc142b6cc60b0da376e9d0186fafc"">
                                          <in method=""AddProgram"">
                                            <screen timeStamps=""1694718748428"">
                                              <program type=""normal"" id=""0"" guid=""6f7e9086-8058-4cfa-8f7c-83328e2de690"" name="""">
                                                <area guid=""940efdfa-96dc-41ea-9741-4a4a99d1baca"" name="""" alpha=""255"">
                                                  <rectangle x=""0"" y=""0"" width=""80"" height=""40"" />
                                                  <resources>
                                                    <text guid=""def4fbe6-6901-411c-bf97-1c6b5bfe70aa"" name="""" singleLine=""{isSingleLine}"">
                                                      <style align=""center"" valign=""middle"" />
                                                      <string>{msj}</string>
                                                      <font name=""Impact"" size=""{fontSize}"" color=""{color}"" bold=""true"" />
                                                      <effect in=""{effectIn}"" inSpeed=""4"" out=""0"" outSpeed=""{speed}"" duration=""10"" />
                                                    </text>
                                                  </resources>
                                                </area>
                                              </program>
                                            </screen>
                                          </in>
                                        </sdk>";

    private const string Paso_logo = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <sdk guid=""948fc142b6cc60b0da376e9d0186fafc"">
                                          <in method=""AddProgram"">
                                            <screen timeStamps=""1694719238136"">
                                              <program type=""normal"" id=""0"" guid=""6f7e9086-8058-4cfa-8f7c-83328e2de690"" name="""">
                                                <backgroundMusic />
                                                <playControl count=""1"" disabled=""false"" />
                                                <area guid=""940efdfa-96dc-41ea-9741-4a4a99d1baca"" name="""" alpha=""255"">
                                                  <rectangle x=""0"" y=""0"" width=""80"" height=""40"" />
                                                  <resources>
                                                    <image guid=""c9088fda-e143-481c-b782-3e97ca6ba95d"" name="""" fit=""stretch"">
                                                      <effect in=""0"" inSpeed=""0"" out=""0"" outSpeed=""0"" duration=""30"" />
                                                      <file name=""logo.png"" />
                                                    </image>
                                                  </resources>
                                                </area>
                                              </program>
                                            </screen>
                                          </in>
                                        </sdk>";
}
