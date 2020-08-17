using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LaserSurvey
{
    class new_bt_adapter
    {
        SerialPort sp;
        int _portnum;
        Thread thread;
        public List<Action> commandList;
        public Action<string> actBtDataRead;

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

        public new_bt_adapter(int portnum)
        {
            _portnum = portnum;
            connection_lost = new Timer(new TimerCallback(lost_connection), null, 5000, 5000);

        }

        ~new_bt_adapter()
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
            //IsRunning = false;
            actBtDataRead?.Invoke("lost");
        }

        private void SetSerialPort()
        {
            sp = new SerialPort("COM" + _portnum);
            sp.BaudRate = 115200;
            sp.DataBits = 8;
            sp.Parity = Parity.None;
            sp.ReadTimeout = 500;
            sp.WriteTimeout = 500;
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
                        actBtDataRead?.Invoke("trying");
                        SetSerialPort();
                        sp.Open();
                        sp.DataReceived += SerialPort_DataReceived;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Cannot connect bt port " + _portnum + " | " + ex.Message);
                        sp = null;
                        actBtDataRead?.Invoke("failed");
                    }
                    swMonitor.Restart();
                    swKeepAlive.Restart();
                    ResetTO();
                }
                else
                {
                    actBtDataRead?.Invoke("connected");
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
            actBtDataRead?.Invoke("keep");
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
            actBtDataRead?.Invoke("received");
            actBtDataRead?.Invoke(indata);
        }

        private void ResetTO()
        {
            connection_lost.Change(5000, 5000);
        }
    }


}

/*
 private void button1_Click(object sender, EventArgs e)
        {
            s.actBtDataRead += serial_rec;
            s.Run();
        }

        private void serial_rec(string data)
        {
            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                textBox1.Text = data;
                //panel1.BackColor = data=="lost"? Color.Gray : Color.Green;
                switch (data)
                {
                    case "lost":
                        panel1.BackColor = Color.Gray;
                        panel2.BackColor = Color.Gray;
                        break;
                    case "trying":
                        panel2.BackColor = Color.Orange;
                        break;
                    case "failed":
                        panel2.BackColor = Color.Red;
                        break;
                    case "connected":
                        panel2.BackColor = Color.Yellow;
                        break;
                    case "keep":
                        panel2.BackColor = Color.Blue;
                        break;
                    case "received":
                        panel1.BackColor = Color.LawnGreen;
                        break;

                }
            });
        }

    */
