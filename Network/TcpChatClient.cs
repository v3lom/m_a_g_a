using System;
using System.Net.Sockets;
using System.Text;
using M_A_G_A.Helpers;
using M_A_G_A.Models;

namespace M_A_G_A.Network
{
    public static class TcpChatClient
    {
        public static bool Send(string ip, int port, NetworkPacket packet)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect(ip, port);
                    using (var stream = client.GetStream())
                    {
                        var json = JsonHelper.Serialize(packet);
                        var msgBytes = Encoding.UTF8.GetBytes(json);
                        var lenBytes = BitConverter.GetBytes(msgBytes.Length);
                        stream.Write(lenBytes, 0, 4);
                        stream.Write(msgBytes, 0, msgBytes.Length);
                        stream.Flush();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
