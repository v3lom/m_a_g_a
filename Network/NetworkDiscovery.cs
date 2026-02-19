using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using M_A_G_A.Helpers;
using M_A_G_A.Models;

namespace M_A_G_A.Network
{
    public class NetworkDiscovery : IDisposable
    {
        private const int DiscoveryPort = 45678;
        private const int BroadcastInterval = 3000;

        private UdpClient _sender;
        private UdpClient _listener;
        private Timer _broadcastTimer;
        private bool _running;

        public event Action<NetworkPacket, string> PeerDiscovered;
        public event Action<string> PeerDisconnected;

        private string _userId;
        private string _userName;
        private string _avatarBase64;
        private int _tcpPort;

        public void Start(string userId, string userName, string avatarBase64, int tcpPort)
        {
            _userId = userId;
            _userName = userName;
            _avatarBase64 = avatarBase64;
            _tcpPort = tcpPort;
            _running = true;

            _sender = new UdpClient();
            _sender.EnableBroadcast = true;

            _listener = new UdpClient();
            _listener.ExclusiveAddressUse = false;
            _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            StartListening();

            _broadcastTimer = new Timer(_ => Broadcast(), null, 0, BroadcastInterval);
        }

        private void Broadcast()
        {
            if (!_running) return;
            try
            {
                var packet = new NetworkPacket
                {
                    PacketType = "DISCOVER",
                    SenderId = _userId,
                    SenderName = _userName,
                    SenderAvatar = _avatarBase64 ?? "",
                    TcpPort = _tcpPort,
                    Timestamp = DateTime.Now.ToString("o")
                };
                var data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(packet));
                _sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch { }
        }

        public void SendBye()
        {
            try
            {
                var packet = new NetworkPacket
                {
                    PacketType = "BYE",
                    SenderId = _userId,
                    SenderName = _userName,
                    TcpPort = _tcpPort,
                    Timestamp = DateTime.Now.ToString("o")
                };
                var data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(packet));
                _sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch { }
        }

        private void StartListening()
        {
            var thread = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        var remote = new IPEndPoint(IPAddress.Any, 0);
                        var data = _listener.Receive(ref remote);
                        var json = Encoding.UTF8.GetString(data);
                        var packet = JsonHelper.Deserialize<NetworkPacket>(json);
                        if (packet.SenderId == _userId) continue;

                        if (packet.PacketType == "BYE")
                            PeerDisconnected?.Invoke(packet.SenderId);
                        else
                            PeerDiscovered?.Invoke(packet, remote.Address.ToString());
                    }
                    catch { }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public void Dispose()
        {
            _running = false;
            _broadcastTimer?.Dispose();
            try { _sender?.Close(); } catch { }
            try { _listener?.Close(); } catch { }
        }
    }
}
