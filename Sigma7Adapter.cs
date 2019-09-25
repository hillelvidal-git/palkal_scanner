using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;



namespace LaserSurvey
{
    class Sigma7Adapter
    {
        TcpClient _masterSocket;
        NetworkStream _masterStream;

        string _slaveIp;
        int _masterPort;
        hvTools.EthernetAdapter _et;

        #region Done
        public Sigma7Adapter(string slaveIp, int masterPort)
        {
            this._slaveIp = slaveIp;
            this._masterPort = masterPort;
            this._et = new hvTools.EthernetAdapter();
        }

        ~Sigma7Adapter()
        {
            this.Disconnect();
            //TODO?
        }

        public bool Connect()
        {
            bool c = _et.ConnectToServo(this._slaveIp, this._masterPort, out _masterSocket, out _masterStream);
            return c;
        }

        public bool Disconnect()
        {
            return this._et.DisconnectFromServo(this._masterSocket);
        }

        public void SwitchOn()
        {
            Write(Sigma7.Parameters.Control_System_ServoOn, 1);
        }

        public void SwitchOff()
        {
            Write(Sigma7.Parameters.Control_System_ServoOn, 0);
        }

        internal int GetPosition()
        {
            int pos;

            if (Read(Sigma7.Parameters.Information_Ax1_Position, out pos))
            {
                //pos = pos % 36000;
                return pos;
            }
            else return -1;
        }

        public bool IsOn()
        {
            int b;
            if (Read(Sigma7.Parameters.Monitor_Ax1_IsOn, out b))
            {
                return (b == 1);
            }
            else return false;
        }

        public bool GoHome()
        {
            bool b = Write(Sigma7.Parameters.Control_Ax1_ReturnToZero, 1);
            Write(Sigma7.Parameters.Control_Ax1_ReturnToZero, 0);
            return b;
        }

        public bool IsReady()
        {
            int b;
            if (Read(Sigma7.Parameters.Monitor_System_IsReady, out b))
            {
                return (b == 1);
            }
            else return false;
        }

        internal bool ResetAlarms()
        {
            Write(Sigma7.Parameters.Control_Program_Reset, 1);
            Write(Sigma7.Parameters.Control_Program_Reset, 0);
            return true;
        }

        internal void DoHoming()
        {
            this.GoHome();
        }

        internal void SetSpeed(int s)
        {
            Write(Sigma7.Parameters.Parameter_Ax1_JogSpeed, (int)(s)); //set jog speed
            Write(Sigma7.Parameters.Parameter_Ax1_StepSpeed, (int)(s)); //set step speed
        }

        public bool MoveAbsolute(int absPosition)
        {
            int relPosition = absPosition - this.GetPosition();
            return MoveRelative(relPosition);
        }

        public bool MoveRelative(int relPosition)
        {
            SetAmountOfStep(Math.Abs(relPosition));

            //TODO: פתרון אחר לגבולות הסיבוב - מה קורה כאר עובר את המקסימום או שלילי

            if (relPosition > 0)
            {
                DoPosStep();
            }
            else
            {
                DoNegStep();
            }

            return WaitForInPos();
        }

        public bool SetAmountOfStep(int Ticks)
        {
            Write(Sigma7.Parameters.Parameter_Ax1_AmountStepFW, Ticks);
            Write(Sigma7.Parameters.Parameter_Ax1_AmountStepRW, Ticks);
            return true;
        }

        public void DoPosStep()
        {
            Write(Sigma7.Parameters.Control_Ax1_JogOrStep, 1);
            Write(Sigma7.Parameters.Control_Ax1_StepFW, 1);
            Write(Sigma7.Parameters.Control_Ax1_StepFW, 0);
        }

        private void DoNegStep()
        {
            Write(Sigma7.Parameters.Control_Ax1_JogOrStep, 1);
            Write(Sigma7.Parameters.Control_Ax1_StepRW, 1);
            Write(Sigma7.Parameters.Control_Ax1_StepRW, 0);
        }

        public bool isInPosition;

        private bool WaitForInPos()
        {
            this.isInPosition = false;

            //for (int t = 0; t < 20; t++)
            //{
            //    if (this.IsInPosition()) return true;
            //    Thread.Sleep(20);
            //}
            //return this.IsInPosition();

            return true;
        }

        internal bool CheckInPosition()
        {
            int b;
            if (Read(Sigma7.Parameters.Monitor_Ax1_IsInPosition, out b))
            {
                this.isInPosition = (b == 1);
                return (b == 1);
            }
            else
            {
                this.isInPosition = false;
                return false;
            }
        }

        #endregion
        //****************************************** TO-DO! **********************************************

        public void InitializeServo()
        {
            //TODO more??
            ResetAlarms();
        }



        #region Send Commands
        private bool Write(string address, int decValue)
        {
            if (this._masterStream == null || !this._masterSocket.Connected)
            {
                //...
                //return false;
            }

            //Build command string
            byte[] outStream = hvTools.Modbus.Build.WriteCommand(address, decValue);

            //Send command
            SendCommand(outStream);

            //Read response
            List<byte> blist = new List<byte>();
            byte[] inStream = new byte[(int)_masterSocket.ReceiveBufferSize + 1000];
            int resl = _masterStream.Read(inStream, 0, (int)_masterSocket.ReceiveBufferSize);
            //TODO: manage response

            return true;
        }

        private bool Read(string address, out int readValue)
        {
            //Build command string
            byte[] outStream = hvTools.Modbus.Build.ReadCommand(address);

            //send command
            SendCommand(outStream);

            //read response
            List<byte> blist = new List<byte>();
            byte[] inStream = new byte[(int)_masterSocket.ReceiveBufferSize + 1000];
            int resl = _masterStream.Read(inStream, 0, (int)_masterSocket.ReceiveBufferSize);

            readValue = hvTools.Modbus.Parse.Response(address[1], inStream);

            return true;
        }

        private void SendCommand(byte[] outStream)
        {
            this._masterStream.Write(outStream, 0, outStream.Length);
            this._masterStream.Flush();
        }
        #endregion

    }
}
