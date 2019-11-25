using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace LaserSurvey
{
    class NewScannerBt
    {
        const int KEEPALIVE_INTERVAL_MS = 5000;
        const int RESPONSE_BUFFER_LENGTH = 20;

        public string PortName;
        SerialPort serialPort;
        public List<Action> commandList;
        
        Thread thread;
        int connectionTries;

        public Action<bool> actBtConnectionChanged;
        public Action<string> actBtDataRead;
        public bool isTransfering = false;

        public bool IsRunning { get; private set; }
        public bool IsConnected
        {
            get
            {
                try { return serialPort != null && serialPort.IsOpen; }
                catch { return false; }
            }
        }

        public NewScannerBt()
        {
           
        }

        ~NewScannerBt()
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (serialPort != null)
            {
                try
                {
                    serialPort.Close();
                    serialPort.Dispose();
                }
                catch { }
            }

            actBtConnectionChanged?.Invoke(false);
        }

        internal void Stop()
        {
            Disconnect();
            IsRunning = false;
        }

        internal void Run(string portName)
        {
            PortName = "COM"+portName;
            IsRunning = true;
            commandList = new List<Action>();
            connectionTries = 0;
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

                        serialPort = new SerialPort
                        {
                            PortName = PortName,
                            BaudRate = 115200,
                            Parity = Parity.None,
                            StopBits = StopBits.One,
                            ReadTimeout = 500,
                            WriteTimeout = 500
                        };

                        connectionTries++;
                        serialPort.Open();
                        serialPort.DataReceived += SerialPort_DataReceived;

                        commandList.Clear();
                        actBtConnectionChanged?.Invoke(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Cannot connect bt comport #" + PortName + ": " + ex.Message);
                        serialPort = null;
                        actBtConnectionChanged?.Invoke(false);
                        if (connectionTries > 20) Stop();
                    }
                    swMonitor.Restart();
                    swKeepAlive.Restart();
                }
                else
                {
                    //actBtConnectionChanged?.Invoke(true);

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
                        try
                        {
                            if (!isTransfering)
                                KeepAlive();
                        }
                        catch
                        {
                            //Todo...
                        }
                        swKeepAlive.Restart();
                    }
                }
                Thread.Sleep(1);
            }
            Disconnect();
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            actBtDataRead?.Invoke(indata);
        }

        private void KeepAlive()
        {
            Debug.WriteLine("[Keep Alive]");
            string response = SendCommand("hv:STT,1,");
            //response = SendCommand("hv:STT,1,");
        }

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

        private string SendCommand(string c, bool read = false)
        {

            string trd = "[" + Thread.CurrentThread.ManagedThreadId + "]";

            try
            {
                //string[] parts = c.Split(',');
                //byte[] buffer = new byte[parts.Length];
                byte[] response_buffer = new byte[RESPONSE_BUFFER_LENGTH];

                //for (int i = 0; i < parts.Length; i++)
                //{
                //    buffer[i] = Convert.ToByte(parts[i]);
                //}



                serialPort.ReadExisting(); //Clear content..
                //Debug.WriteLine(trd + "[WRITING TO BT] | " + c);
                serialPort.WriteLine(c);
                //Debug.WriteLine(trd + "sent! | " + c);

                string res = "";
                if (read)
                {
                    //Debug.WriteLine(trd + "[READING BT] | " + c);
                    serialPort.Read(response_buffer, 0, RESPONSE_BUFFER_LENGTH);
                    res = ParseResponse(response_buffer);
                    //actPortError?.Invoke("OK");
                    //Debug.WriteLine(trd + "read! | " + c);
                }

                return res;
            }
            catch (Exception e)
            {
                Debug.WriteLine(trd + "[ERR READING bt] " + e.Message + " | " + c);
                Disconnect();
                //if (e.Message.Contains("The semaphore timeout period has expired."))
                //actPortError?.Invoke(e.Message);

                return "error";
            }
        }

        private string ParseResponse(byte[] response_buffer)
        {
            return null;
        }
    }
}
