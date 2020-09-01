using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace LaserSurvey
{
    class port_finder
    {
        SerialPort sp;
        Thread thread;
        string _p;
        bool found;
        public Action<int, string> actEnd;

        public void Run()
        {
            thread = new Thread(Find);
            thread.Start();
        }

        void Find()
        {
            found = false;
            string[] ports = SerialPort.GetPortNames();
            Console.WriteLine("List of " + ports.Length + " ports found...");
            foreach (string port in ports)
            {
                _p = port;
                try
                {
                    Console.WriteLine("Checking port: " + port);
                    sp = new SerialPort(port);
                    sp.ReadTimeout = 500;
                    sp.WriteTimeout = 500;
                    sp.DataReceived += Sp_DataReceived;
                    sp.Open();
                    Thread.Sleep(500);
                    sp.WriteLine("hv:STT,1,");
                    Thread.Sleep(1000);
                    if (found)
                    {
                        actEnd?.Invoke(2, port);
                        return;
                    }
                    else
                    {
                        actEnd?.Invoke(0, port);
                    }
                    sp.DataReceived -= Sp_DataReceived;
                    sp.Close();
                    sp.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Port Finding Error: " + e.Message);
                }
            }
            actEnd?.Invoke(1, "not found");
            
        }

        private void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            if (indata.Contains("<stt>"))
            {
                Console.WriteLine("FOUND! - " + _p);
                found = true;
            }
        }
    }
}
