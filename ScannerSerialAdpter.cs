using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace LaserSurvey
{
    class serial_adapter
    {
        SerialPort sp;
        int _portnum;
        Thread thread;
        public List<Action> commandList;
        public Action<string> actUpdateState;
        public Action<string> actDataRead;

        Timer connection_lost;

        const int KEEPALIVE_INTERVAL_MS = 2000;
        const int RESPONSE_BUFFER_LENGTH = 20;

        public bool IsRunning { get; private set; }
        public bool IsConnected
        {
            get
            {
                try { return sp != null && sp.IsOpen; }
                catch { return false; }
            }
        }


        ///========================================
        ///
        public Action<bool> actBtConnectionChanged;
        public bool isTransfering = false;

        public bool Send(string s, bool useCommandList = true)
        {
            if (isTransfering) return false;

            if (useCommandList)
            {
                commandList.Add(() =>
                {
                    SendCommand(s);
                });
            }
            else
            {
                SendCommand(s);
            }

            return true;
        }

        private string SendCommand(string c, bool read)
        {

            string trd = "[" + Thread.CurrentThread.ManagedThreadId + "]";

            try
            {
                byte[] response_buffer = new byte[RESPONSE_BUFFER_LENGTH];

                sp.ReadExisting(); //Clear content..
                sp.WriteLine(c);

                string res = "";
                if (read)
                {
                    sp.Read(response_buffer, 0, RESPONSE_BUFFER_LENGTH);
                    res = ParseResponse(response_buffer);
                }

                return res;
            }
            catch (Exception e)
            {
                //TOCHECK
                Debug.WriteLine(trd + "[ERR READING bt] " + e.Message + " | " + c);
                Disconnect();

                return "error";
            }
        }

        private string ParseResponse(byte[] response_buffer)
        {
            return null;
        }

        //=====================================================




        public serial_adapter(int portnum)
        {
            _portnum = portnum;
            connection_lost = new Timer(new TimerCallback(lost_connection), null, 5000, 5000);
        }

        ~serial_adapter()
        {
            Disconnect();
        }

        private void lost_connection(object state)
        {
            Stop();
        }

        internal void Stop()
        {
            Disconnect();
            sp.Dispose();
            //IsRunning = false;
            actUpdateState?.Invoke("lost");
        }

        internal void Die()
        {
            Disconnect();
            IsRunning = false;
            actUpdateState?.Invoke("lost");
        }

        private void SetSerialPort()
        {
            sp = new SerialPort("COM" + _portnum);
            sp.BaudRate = 115200;
            sp.DataBits = 8;
            sp.Parity = Parity.None;
            sp.ReadTimeout = 500;
            sp.WriteTimeout = 500;
            sp.ReadBufferSize = 100;
            sp.WriteBufferSize = 64;
        }

        private string SendCommand(string c)
        {
            try
            {
                sp.WriteLine(c);
                return "ok";
            }
            catch (Exception e)
            {
                return "error | " + e.Message + " | " + c;
            }
        }

        internal void Run()
        {
            IsRunning = true;
            commandList = new List<Action>();
            thread = new Thread(Loop);
            thread.Start();
        }

        public void Loop()
        {
            Stopwatch swMonitor = new Stopwatch();
            Stopwatch swKeepAlive = new Stopwatch();

            while (IsRunning)
            {
                if (!IsConnected)
                {
                    try
                    {
                        Disconnect();
                        ResetTO();
                        actUpdateState?.Invoke("trying");
                        SetSerialPort();
                        sp.Open();
                        sp.DataReceived += SerialPort_DataReceived;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Cannot connect bt port " + _portnum + " | " + ex.Message);
                        sp = null;
                        actUpdateState?.Invoke("failed");
                    }
                    swMonitor.Restart();
                    swKeepAlive.Restart();
                    ResetTO();
                }
                else
                {
                    actUpdateState?.Invoke("connected");
                    foreach (var command in commandList.ToList())
                    {
                        try
                        {
                            command.Invoke();
                            Thread.Sleep(250);
                        }
                        catch { }
                        commandList.Remove(command);
                    }

                    if (swKeepAlive.ElapsedMilliseconds > KEEPALIVE_INTERVAL_MS)
                    {
                        swKeepAlive.Restart();
                        KeepAlive();
                        swKeepAlive.Restart();
                    }
                }
                Thread.Sleep(1);
            }
            Disconnect();
        }

        private void KeepAlive()
        {
            Debug.WriteLine("[Keep Alive]");
            actUpdateState?.Invoke("keep");
            SendCommand("hv:STT,1,");
        }

        private void Disconnect()
        {
            if (sp != null)
            {
                try
                {
                    sp.Close();
                    sp.Dispose();
                }
                catch { }
            }

        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            ResetTO();
            actUpdateState?.Invoke("received");
            actDataRead?.Invoke(indata);
        }

        private void ResetTO()
        {
            connection_lost.Change(5000, 5000);
        }

        
    }
}
