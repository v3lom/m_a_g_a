using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Provides stable, cross-session identification based on MAC + hostname.
    /// </summary>
    public static class NetworkHelper
    {
        private static string _cachedId;
        private static string _cachedMac;
        private static string _cachedHostname;
        private static string _cachedIPv4;
        private static string _cachedIPv6;

        public static string GetStableId()
        {
            if (_cachedId != null) return _cachedId;
            var raw = GetMacAddress() + "|" + GetHostname();
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));
                _cachedId = new Guid(hash).ToString();
            }
            return _cachedId;
        }

        public static string GetMacAddress()
        {
            if (_cachedMac != null) return _cachedMac;
            _cachedMac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                          && ni.OperationalStatus == OperationalStatus.Up
                          && ni.GetPhysicalAddress() != PhysicalAddress.None)
                .OrderByDescending(ni => ni.Speed)
                .Select(ni => string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))))
                .FirstOrDefault() ?? "00:00:00:00:00:00";
            return _cachedMac;
        }

        public static string GetHostname()
        {
            if (_cachedHostname != null) return _cachedHostname;
            _cachedHostname = Dns.GetHostName();
            return _cachedHostname;
        }

        public static string GetIPv4()
        {
            if (_cachedIPv4 != null) return _cachedIPv4;
            _cachedIPv4 = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(ua => ua.Address.ToString())
                .FirstOrDefault() ?? "0.0.0.0";
            return _cachedIPv4;
        }

        public static string GetIPv6()
        {
            if (_cachedIPv6 != null) return _cachedIPv6;
            _cachedIPv6 = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetworkV6
                          && !ua.Address.IsIPv6LinkLocal)
                .Select(ua => ua.Address.ToString())
                .FirstOrDefault() ?? "";
            return _cachedIPv6;
        }

        /// <summary>
        /// Returns all subnet broadcast addresses for active adapters.
        /// e.g., 192.168.1.5/24 → 192.168.1.255
        /// </summary>
        public static IPAddress[] GetSubnetBroadcasts()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(ua =>
                {
                    var ip = ua.Address.GetAddressBytes();
                    var mask = ua.IPv4Mask.GetAddressBytes();
                    var broadcast = new byte[4];
                    for (int i = 0; i < 4; i++)
                        broadcast[i] = (byte)(ip[i] | ~mask[i]);
                    return new IPAddress(broadcast);
                })
                .Distinct()
                .ToArray();
        }
    }
}
