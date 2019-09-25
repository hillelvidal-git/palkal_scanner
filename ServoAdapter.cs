using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;



namespace LaserSurvey
{
    class ServoAdapter
    {
        public struct ServoIOAction
        {
            public const string Read = "03";
            public const string Write = "06";
        }

        //properties:
        private SerialPort servoCOMport;
        private Int16 m_Register0407 = 0x00;

        //methods:

        public ServoAdapter(string COMportName)
        {
            this.servoCOMport = new SerialPort(COMportName);
        }

        ~ServoAdapter()
        {
            this.Disconnect();
            this.servoCOMport.Close();
            this.servoCOMport.Dispose();
        }

        public string Connect(int Rate, Parity ParityType, int DataBitsNum, StopBits StopBitsNum)
        {
            try
            {
                servoCOMport.BaudRate = Rate;
                servoCOMport.Parity = ParityType;
                servoCOMport.DataBits = DataBitsNum;
                servoCOMport.StopBits = StopBitsNum;
                servoCOMport.ReadTimeout = 500;
                servoCOMport.WriteTimeout = 50;
                //servoCOMport.w

                servoCOMport.Open();
                System.Threading.Thread.Sleep(300);

                string tryCommand = this.SendCommand(ServoIOAction.Write, "0306", 255); //Enable communication
                if (!tryCommand.StartsWith("OK"))
                {
                    return "Not connected to servo";
                }
                //this.SendCommand(ServoIOAction.Write, "021E", 5); //Prevent values saving

                //ישנה תופעה של יציאות מראה, שמחזירות כל מה שכותבים להן
                //וזה עלול לגרום למצב של התחברות ליציאת דמה כזאת במקום לבקר האמיתי
                //ולכן נבצע בדיקה:
                if (IsMirrorPort())
                {
                    return "Dummy Port";
                }
                //אם זו אינה יציאת מראה, והחיבור הצליח אז זהו הבקר המבוקש
                return tryCommand;
            }
            catch (Exception e)
            {
                return "Cannot open serial port.\n" + e.Message;
            }
        }

        private bool IsMirrorPort()
        {
            try
            {
                string command = "I am a dummy mirror port";
                servoCOMport.ReadExisting();
                servoCOMport.WriteLine(command);
                string response = servoCOMport.ReadLine();
                if (response == command)
                {
                    //זו יציאת מראה ולא בקר
                    return true;
                }
            }
            catch (Exception ec)
            {
                return false;
            }
            //זו אינה יציאת מראה
            return false;
        }

        public string FirstHome()
        {
            this.SwitchOn();
            //this.GoHome(); problems!!
            return this.SwitchOff();
        }

        public string Disconnect()
        {
            try
            {
                this.SwitchOff();
                servoCOMport.Close();
                //Application.DoEvents();
                System.Threading.Thread.Sleep(200);
                return "OK";
            }
            catch (Exception e)
            {
                return "Cannot close serial port.\n" + e.Message; ;
            }
        }

        public string InitializeServo(string[] ParametersNvalues)
        {
            ResetAlarms();
            this.SendCommand(ServoIOAction.Write, "021E", 0); //Enable values saving
            string line, function, value, mess; mess = "";
            int VAL;

            for (int i = 0; i < ParametersNvalues.Length; i++)
            {
                line = ParametersNvalues[i];
                if (line.StartsWith("//")) continue;
                if (line.IndexOf("//") > -1) line = line.Substring(0, line.IndexOf("//"));
                mess += "\n" + line;
                function = line.Substring(0, line.IndexOf(@"/"));
                value = line.Substring(line.IndexOf(@"/") + 1);
                VAL = Convert.ToInt32(value);
                SendCommand(ServoIOAction.Write, function, VAL);
            }

            //this.SendCommand(ServoIOAction.Write, "021E", 5); //Prevent values saving
            //MessageBox.Show(mess);

            return mess;
        }

        public string SwitchOn()
        {
            //ResetAlarms();
            return OperateDI(1, true);
        }

        public string SwitchOff()
        {
            return OperateDI(1, false);
        }

        internal string ResetAlarms()
        {
            //OperateDI(5, true);
            return OperateDI(5, false);
        }

        public string GetReady()
        {
            string message;
            if (!this.servoCOMport.IsOpen)//port is closed
            {
                message = "COM port is closed";
            }
            else    //port is opened
            {
                message = this.SwitchOn();
                if (message.StartsWith("OK")) //servo is on
                {
                    message = this.ResetAlarms();
                    if (message.StartsWith("OK"))  //alarms cleared
                    {
                        if (this.IsReady()) message = "OK";
                        else message = "servo is not ready";
                    }
                }
            }
            return message;
        }

        public string GoHome()
        {
            do
            { } while (MoveAbsolute(0) > 9999);
            //OperateDI(4, true);
            //OperateDI(4, false);
            return "OK" + GetPosition().ToString();
        }

        public int MoveRelative(int relPosition)
        {
            SetPositionCommand(GetPosition() + relPosition);
            JustMove();
            WaitForPosComplete();
            return GetPosition();
        }

        public int MoveAbsolute(int absPosition)
        {
            SetPositionCommand(absPosition);
            JustMove();
            WaitForPosComplete();
            return GetPosition();
        }

        public void JustMove()
        {
            OperateDI(2, true);
            OperateDI(2, false);
        }

        public string WaitForPosComplete()
        {
            int timeOut = 450;
            int time = 0;

            int resp = 0;
            string rs = "";

            while ((resp != 1) && (time < timeOut))
            {
                //Application.DoEvents(); //Allow stop
                rs = SendCommand(ServoIOAction.Read, "0409", 1);
                resp = Convert.ToInt32(FourHexaToDecimal(rs.Substring(9, 4)));
                time++;
            }
            if (resp != 1) throw new Exception("Positioning Not Succeeded");
            System.Threading.Thread.Sleep(50);
            return rs + "\n" + resp.ToString();
        }

        public string SetPositionCommand(int Ticks)
        {
            //get the current rotation:
            int rev = 0;

            if (Ticks > 9999)
            {
                //rev++;
                Ticks -= 10000;
            }

            //Set 1st position command for rotation
            SendCommand(ServoIOAction.Write, "010F", rev);
            //Set 1st position command for pulse
            return SendCommand(ServoIOAction.Write, "0110", Ticks);
        }

        public int GetPosition()
        {
            //TODO:
            //כרגע ישנה כאן התמודדות מאולתרת עם בעיה מוזרה
            //לפעמים מתקבל ערך 0 או 5630 במקום המיקום
            //הפתרון כרגע הוא לנסות עוד פעמיים
            //כדאי למצוא פתרון קבוע ואלגנטי

            int pos;
            string rs;
            rs = SendCommand(ServoIOAction.Read, "0004", 1);
            if (rs.StartsWith("OK:018"))
            {
                return -1;
            }
            else
            {
                pos = Convert.ToInt32(FourHexaToDecimal(rs.Substring(9, 4)));
                if (pos > 9999) pos -= 55536;
                if ((pos == 5630) || (pos == 0))
                {
                    rs = SendCommand(ServoIOAction.Read, "0004", 1);
                    if (rs.StartsWith("OK:018"))
                    {
                        return -1;
                    }
                    else
                    {
                        pos = Convert.ToInt32(FourHexaToDecimal(rs.Substring(9, 4)));
                        if (pos > 9999) pos -= 55536;
                        if ((pos == 5630) || (pos == 0))
                        {
                            rs = SendCommand(ServoIOAction.Read, "0004", 1);
                            if (rs.StartsWith("OK:018"))
                            {
                                return -1;
                            }
                            else
                            {
                                pos = Convert.ToInt32(FourHexaToDecimal(rs.Substring(9, 4)));
                                if (pos > 9999) pos -= 55536;
                            }
                        }
                    }
                }
                return pos;
            }
        }

        public bool IsReady()
        {
            //check if the DO1 contain "SRDY"
            //string son = SendCommand(ServoIOAction.Read, "0406", 4);
            return true;
        }

        public bool IsOn()
        {
            //check if the DO2 contain "SON"
            //string son = SendCommand(ServoIOAction.Read, "0213", 1);
            return true;
        }

        private string OperateDI(int DI, bool On)
        {
            string TempInput = "";
            Int16 Mask = 0;

            switch (DI)
            {
                case 1: Mask = 1; break;
                case 2: Mask = 2; break;
                case 3: Mask = 4; break;
                case 4: Mask = 8; break;
                case 5: Mask = 16; break;
                case 6: Mask = 32; break;
                case 7: Mask = 64; break;
                case 8: Mask = 128; break;
            }

            if (On)
            {
                m_Register0407 |= Mask; //Add the relevant ID to the ON list
            }
            else
            {
                Mask = (Int16)~Mask;
                m_Register0407 &= Mask; //Remove the relevant ID from the ON list
            }

            TempInput = m_Register0407.ToString("X");
            while (TempInput.Length < 4)
                TempInput = "0" + TempInput;

            return SendCommand(ServoIOAction.Write, "0407", Convert.ToInt16(TempInput));
        }

        public string SendCommand(string cAction, string cDataAdress, int decimalValue)
        {
            string servoResponse = "";
            string cStart = ":";
            string cServoAdress = "01";
            string cChekSum = "";
            string cEnd = "\r\n";
            string cHexaValue = DecimalToHexa(decimalValue);

            //Build command string:
            string command = cStart + cServoAdress + cAction + cDataAdress + cHexaValue;
            if (command.Length != 13) return "Command length error: length is  " + command.Length.ToString() + "  instead of 13.";

            cChekSum = GetChekSum(command);
            command += cChekSum + cEnd;


            try
            {
                //Clear buffer:
                servoCOMport.ReadExisting();

                //Write command:
                servoCOMport.WriteLine(command);

                try
                {
                    //Try to read response, till it arrives:
                    //while (servoResponse == "")
                    servoResponse = servoCOMport.ReadLine();

                    //Check response:
                    if (servoResponse.StartsWith(":010"))
                    {
                        return "OK" + servoResponse + "\nWas rhe response for command:" + command;
                    }
                    else
                    {
                        return "Response Error" + servoResponse;
                    }
                }
                catch (Exception er)
                {
                    return "Reading Error:\n" + er.Message;
                }
            }

            catch (Exception ew)
            {
                return "Writing Error:\n" + ew.Message;
            }

        }

        private string GetChekSum(string command)
        {
            string chk;
            int decSum = 0;
            for (int i = 1; i < 13; i += 2)
            {
                decSum += HexaToDecimal(command.Substring(i, 2));
            }
            decSum = ~decSum;
            byte hexaComp = (byte)(decSum + 1);
            chk = hexaComp.ToString("X");
            if (chk.Length == 0) chk = "00"; else if (chk.Length == 1) chk = "0" + chk;
            return chk;
        }

        private int HexaToDecimal(string Hexa)
        {
            int tChr = 0; int decSum = 0;
            for (int i = 0; i <= 1; i++)
            {
                switch (Hexa[i])
                {
                    case '0': tChr = 0; break;
                    case '1': tChr = 1; break;
                    case '2': tChr = 2; break;
                    case '3': tChr = 3; break;
                    case '4': tChr = 4; break;
                    case '5': tChr = 5; break;
                    case '6': tChr = 6; break;
                    case '7': tChr = 7; break;
                    case '8': tChr = 8; break;
                    case '9': tChr = 9; break;
                    case 'A': tChr = 10; break;
                    case 'B': tChr = 11; break;
                    case 'C': tChr = 12; break;
                    case 'D': tChr = 13; break;
                    case 'E': tChr = 14; break;
                    case 'F': tChr = 15; break;
                }
                if (i == 0) decSum = tChr * 16; else decSum += tChr;
            }
            return decSum;
        }

        private string DecimalToHexa(int decimalValue)
        {
            string temp = decimalValue.ToString("X");
            while (temp.Length < 4) temp = "0" + temp;
            if (temp.Length > 4) temp = temp.Substring(temp.Length - 4);
            return temp;
        }

        private string FourHexaToDecimal(string Hexa)
        {
            int tChr = 0; int decSum = 0;
            for (int i = 0; i <= 3; i++)
            {
                switch (Hexa[i])
                {
                    case '0': tChr = 0; break;
                    case '1': tChr = 1; break;
                    case '2': tChr = 2; break;
                    case '3': tChr = 3; break;
                    case '4': tChr = 4; break;
                    case '5': tChr = 5; break;
                    case '6': tChr = 6; break;
                    case '7': tChr = 7; break;
                    case '8': tChr = 8; break;
                    case '9': tChr = 9; break;
                    case 'A': tChr = 10; break;
                    case 'B': tChr = 11; break;
                    case 'C': tChr = 12; break;
                    case 'D': tChr = 13; break;
                    case 'E': tChr = 14; break;
                    case 'F': tChr = 15; break;
                }
                decSum += (int)(tChr * Math.Pow(16, (double)3 - i));
            }
            return decSum.ToString();
        }


        internal void ClearBuffer()
        {
            servoCOMport.DiscardInBuffer();
        }

        internal void ClearPulsesAndRotations()
        {
            this.OperateDI(3, true);
            this.OperateDI(3, false);
        }


        internal int GetPosition(bool p)
        {
            int pos = 0;
            if (!p)
                return GetPosition();
            else //סנן אפסים
            {
                for (int t = 0; t < 20; t++)
                {
                    pos = GetPosition();
                    if (pos != 0) return pos;
                }
                return pos;
            }
        }
    }
}
