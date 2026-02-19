using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using M_A_G_A.Helpers;
using M_A_G_A.Models;

namespace M_A_G_A.Network
{
    public class TcpChatServer : IDisposable
    {
        private TcpListener _listener;
        private bool _running;

        public event Action<NetworkPacket, string> MessageReceived;
        public int Port { get; private set; }

        public void Start(int port = 0)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;

            var thread = new Thread(AcceptLoop);
            thread.IsBackground = true;
            thread.Start();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    var clientThread = new Thread(() => HandleClient(client));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch { }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    while (true)
                    {
                        var lenBytes = ReadExact(stream, 4);
                        if (lenBytes == null) break;
                        int length = BitConverter.ToInt32(lenBytes, 0);
                        var msgBytes = ReadExact(stream, length);
                        if (msgBytes == null) break;
                        var json = Encoding.UTF8.GetString(msgBytes);
                        var packet = JsonHelper.Deserialize<NetworkPacket>(json);
                        MessageReceived?.Invoke(packet, remoteIp);
                    }
                }
            }
            catch { }
        }

        private byte[] ReadExact(NetworkStream stream, int count)
        {
            var buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buf, read, count - read);
                if (n == 0) return null;
                read += n;
            }
            return buf;
        }

        public void Dispose()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }
    }
}
