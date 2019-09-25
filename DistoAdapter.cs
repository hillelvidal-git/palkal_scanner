using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;



namespace LaserSurvey
{
    class DistoAdapter
    {

        //properties:
        private SerialPort distoCOMport;
        public ASCIIEncoding myAsciiEncoding;


        //methods:

        public DistoAdapter(string COMportName)
        {
            this.distoCOMport = new SerialPort(COMportName);
            myAsciiEncoding = new ASCIIEncoding();

        }

        ~DistoAdapter()
        {
            this.distoCOMport.Close();
            this.distoCOMport.Dispose();
        }

        public string Connect()
        {
            try
            {
                distoCOMport.BaudRate = 9600;
                distoCOMport.WriteTimeout = 50;
                distoCOMport.ReadTimeout = 5000;
                distoCOMport.Open();
                System.Threading.Thread.Sleep(300);

                if (distoCOMport.IsOpen)
                {
                    string res = this.SwitchOn(false);
                    return res;
                }
                else return "Cannot open serial port.";
            }
            catch (Exception e)
            {
                return "Cannot open serial port.\n" + e.Message;
            }
        }

        public string Disconnect()
        {
            try
            {
                if (distoCOMport.IsOpen)
                {
                    this.SwitchOn(false);
                    distoCOMport.Close();
                    //Application.DoEvents();
                    System.Threading.Thread.Sleep(200);
                }
                return "OK";
            }
            catch (Exception e)
            {
                return "Cannot close serial port.\n" + e.Message;
            }
        }

        private string InitializeDisto()
        {
            //ToDo: initialize disto
            return "OK";
        }

        public string SwitchOn(bool ToOn)
        {
            string Response = InitializeDisto();
            if (Response.StartsWith("OK"))
            {
                if (ToOn) Response = SendCommand("o"); //Switch On
                else Response = SendCommand("p"); //Switch Off
            }
            return Response;
        }

        public string GetSerial()
        {
            string response = SendCommand("N02N");
            if (response.StartsWith("OK")) return response.Substring(9, 8);
            else return response;
        }

        public string LastResponse;
        public int MeasureOnce()
        {
            string response = SendCommand("g");
            LastResponse = response;

            if (response.StartsWith("OK")) return Convert.ToInt32(response.Substring(8, 9));
            else if (response.StartsWith("TimeOut")) throw new Exception("TimeOut");
            else return -1;
        }

        public int MeasureOnce(int tries)
        {
            string response = "";
            for (int t = 0; t < tries; tries++)
            {
                response = SendCommand("g");
                LastResponse = response;
                if (response != "Error @E220") break;
            }

            if (response.StartsWith("OK")) return Convert.ToInt32(response.Substring(8, 9));
            else if (response.StartsWith("TimeOut")) throw new Exception("TimeOut");
            else return -1;
        }


        public bool IsReady()
        {
            //ToDo: check if the disto is ready
            return true;
        }

        public string SendCommand(string inCommand)
        {
            string ASCIIConvertedString = "";
            string response = "";

            ASCIIConvertedString = myAsciiEncoding.GetString(myAsciiEncoding.GetBytes(inCommand + "\r\n"));
            try
            {
                //Clear buffer:
                distoCOMport.ReadExisting();
                //Write command:
                distoCOMport.WriteLine(ASCIIConvertedString);
                //Try to read response, till it arrives:

                int tries = 0;
                while (response == "" && tries < 20)
                {
                    response = myAsciiEncoding.GetString(myAsciiEncoding.GetBytes(distoCOMport.ReadLine()));
                    tries++;
                }

                //Use response:
                if (response.StartsWith("?")) return "OK" + response;
                else if ((response.StartsWith("@")) || (response.Length < 7)) return "Error " + response;
                else return "OK" + response;
            }
            catch (System.TimeoutException toe)
            {
                return "TimeOut Error!" + toe.Message;
            }
            catch (Exception e2)
            {
                return "Error!" + e2.Message;
            }

        }


        internal void ClearBuffer()
        {
            distoCOMport.DiscardInBuffer();
        }
    }
}
