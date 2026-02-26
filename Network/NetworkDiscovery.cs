using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using M_A_G_A.Helpers;
using M_A_G_A.Models;

namespace M_A_G_A.Network
{
    /// <summary>
    /// Discovers peers on the LAN using:
    ///   1) IPv4 UDP broadcast to 255.255.255.255
    ///   2) Directed subnet broadcasts per active adapter
    ///   3) IPv4 multicast group 239.255.255.250
    /// Peers are identified by MAC + hostname → stable GUID.
    /// </summary>
    public class NetworkDiscovery : IDisposable
    {
        private const int    DiscoveryPort      = 45678;
        private const int    BroadcastIntervalMs = 3000;
        private const string MulticastGroup      = "239.255.255.250";

        private UdpClient     _broadcastSender;
        private UdpClient     _multicastSender;
        private UdpClient     _listener;
        private Timer         _broadcastTimer;
        private volatile bool _running;

        public event Action<NetworkPacket, string> PeerDiscovered;
        public event Action<string>                PeerDisconnected;

        private string _userId;
        private string _userName;
        private string _avatarBase64;
        private string _macAddress;
        private string _hostname;
        private string _ipv4;
        private string _ipv6;
        private int    _tcpPort;

        public void Start(string userId, string userName, string avatarBase64, int tcpPort)
        {
            _userId       = userId;
            _userName     = userName;
            _avatarBase64 = avatarBase64 ?? "";
            _macAddress   = NetworkHelper.GetMacAddress();
            _hostname     = NetworkHelper.GetHostname();
            _ipv4         = NetworkHelper.GetIPv4();
            _ipv6         = NetworkHelper.GetIPv6();
            _tcpPort      = tcpPort;
            _running      = true;

            // Broadcast sender
            try
            {
                _broadcastSender = new UdpClient();
                _broadcastSender.EnableBroadcast = true;
                _broadcastSender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            catch { }

            // Multicast sender
            try
            {
                _multicastSender = new UdpClient();
                _multicastSender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _multicastSender.Ttl = 8;
            }
            catch { }

            // Listener — receives both broadcast and multicast
            try
            {
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.ExclusiveAddressUse = false;
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _listener.JoinMulticastGroup(IPAddress.Parse(MulticastGroup));
            }
            catch { }

            StartListening();
            _broadcastTimer = new Timer(_ => BroadcastAll(), null, 0, BroadcastIntervalMs);
        }

        // ─── Broadcast helpers ───────────────────────────────────────────

        private void BroadcastAll()
        {
            if (!_running) return;
            var data = Encoding.UTF8.GetBytes(BuildDiscoverJson());
            SendUdp(_broadcastSender, data, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            foreach (var subnet in NetworkHelper.GetSubnetBroadcasts())
                SendUdp(_broadcastSender, data, new IPEndPoint(subnet, DiscoveryPort));
            SendUdp(_multicastSender,  data, new IPEndPoint(IPAddress.Parse(MulticastGroup), DiscoveryPort));
        }

        private void SendUdp(UdpClient client, byte[] data, IPEndPoint ep)
        {
            try { client?.Send(data, data.Length, ep); } catch { }
        }

        public void SendBye()
        {
            var packet = new NetworkPacket
            {
                PacketType = "BYE",
                SenderId   = _userId,
                SenderName = _userName,
                MacAddress = _macAddress,
                Hostname   = _hostname,
                TcpPort    = _tcpPort,
                Timestamp  = DateTime.Now.ToString("o")
            };
            var data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(packet));
            SendUdp(_broadcastSender, data, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
        }

        public void UpdateAvatar(string avatarBase64)
        {
            _avatarBase64 = avatarBase64 ?? "";
        }

        // ─── Listener thread ─────────────────────────────────────────────

        private void StartListening()
        {
            var t = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        var remote = new IPEndPoint(IPAddress.Any, 0);
                        var data   = _listener?.Receive(ref remote);
                        if (data == null) continue;
                        var json   = Encoding.UTF8.GetString(data);
                        var packet = JsonHelper.Deserialize<NetworkPacket>(json);

                        // Ignore own packets
                        if (packet.SenderId == _userId) continue;
                        if (!string.IsNullOrEmpty(packet.MacAddress) && packet.MacAddress == _macAddress) continue;

                        if (packet.PacketType == "BYE")
                            PeerDisconnected?.Invoke(packet.SenderId);
                        else
                            PeerDiscovered?.Invoke(packet, remote.Address.ToString());
                    }
                    catch (ObjectDisposedException) { break; }
                    catch { }
                }
            }) { IsBackground = true };
            t.Start();
        }

        private string BuildDiscoverJson() => JsonHelper.Serialize(new NetworkPacket
        {
            PacketType   = "DISCOVER",
            SenderId     = _userId,
            SenderName   = _userName,
            SenderAvatar = _avatarBase64,
            MacAddress   = _macAddress,
            Hostname     = _hostname,
            IPv4         = _ipv4,
            IPv6         = _ipv6,
            TcpPort      = _tcpPort,
            Timestamp    = DateTime.Now.ToString("o")
        });

        public void Dispose()
        {
            _running = false;
            _broadcastTimer?.Dispose();
            try { _broadcastSender?.Close(); } catch { }
            try { _multicastSender?.Close(); } catch { }
            try { _listener?.DropMulticastGroup(IPAddress.Parse(MulticastGroup)); } catch { }
            try { _listener?.Close(); } catch { }
        }
    }
}
