using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace hvTools
{
    class EthernetAdapter
    {
        bool ServoConnectedViaTcp;

        internal bool ConnectToServo(string slaveIp, int masterPort, out TcpClient master, out NetworkStream netStream)
        {
            //Connect to the Slave Controller
            try
            {
                TcpClient masterSocket = new TcpClient();
                masterSocket.Connect(slaveIp, masterPort);
                master = masterSocket;
                NetworkStream masterStream = masterSocket.GetStream();
                netStream = masterStream;

                // Ping's the slave.
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(slaveIp);

                if (reply.Status == IPStatus.Success)
                {
                    this.ServoConnectedViaTcp = true;
                    //MessageBox.Show("מחובר בהצלחה לסרבו");

                    return true;
                }
                else
                {
                    //MessageBox.Show("Cannot connecto to printer!\n\n" + reply.Status);
                    this.ServoConnectedViaTcp = false;

                    return false;
                }
            }
            catch (Exception ee)
            {
                //MessageBox.Show("החיבור לסרבו נכשל: Exception: " + ee.Message);
                this.ServoConnectedViaTcp = false;
                master = null;
                netStream = null;
                return false;
            }
        }


        internal bool DisconnectFromServo(TcpClient tcpClient)
        {
            try
            {
                tcpClient.Client.Shutdown(SocketShutdown.Both);
                tcpClient.Client.Disconnect(false);
                tcpClient.GetStream().Close();
                tcpClient.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
