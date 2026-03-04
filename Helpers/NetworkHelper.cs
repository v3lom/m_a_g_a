using System;
using System.Collections.Generic;
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
    /// Correctly handles machines with multiple network adapters (VMware, Hyper-V,
    /// VirtualBox, VPN, etc.) by preferring physical Ethernet/Wi-Fi interfaces.
    /// </summary>
    public static class NetworkHelper
    {
        private static string _cachedId;
        private static string _cachedMac;
        private static string _cachedHostname;
        private static string _cachedIPv4;
        private static string _cachedIPv6;

        // Keywords found in Description or Name of virtual / tunnel adapters.
        // Deliberately lowercased for case-insensitive matching.
        private static readonly string[] VirtualKeywords = {
            "vmware", "virtualbox", "vbox", "hyper-v", "vethernet",
            "tap-windows", "tap adapter", "npcap loopback",
            "teredo", "isatap", "bluetooth", "wi-fi direct",
            "pptp", "l2tp", "sstp", "wireguard", "openvpn",
            "nordvpn", "expressvpn", "cisco anyconnect", "pulse secure",
            "juniper", "checkpoint", "fortinet", "sonicwall"
        };

        private static bool IsVirtualAdapter(NetworkInterface ni)
        {
            var desc = (ni.Description ?? "").ToLowerInvariant();
            var name = (ni.Name       ?? "").ToLowerInvariant();
            return VirtualKeywords.Any(k => desc.Contains(k) || name.Contains(k));
        }

        /// <summary>
        /// Scores an interface: higher = more preferred.
        /// Physical Ethernet > Wi-Fi > other real adapters.
        /// </summary>
        private static int AdapterScore(NetworkInterface ni)
        {
            if (IsVirtualAdapter(ni)) return -1;
            switch (ni.NetworkInterfaceType)
            {
                case NetworkInterfaceType.Ethernet:    return 3;
                case NetworkInterfaceType.Wireless80211: return 2;
                case NetworkInterfaceType.GigabitEthernet: return 3;
                case NetworkInterfaceType.FastEthernetT:   return 3;
                default: return 1;
            }
        }

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
                          && ni.GetPhysicalAddress() != PhysicalAddress.None
                          && !IsVirtualAdapter(ni))
                .OrderByDescending(ni => ni.Speed)
                .Select(ni => string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))))
                .FirstOrDefault()
                // fallback: allow virtual adapters if nothing else found
                ?? NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
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

        /// <summary>
        /// Returns the best IPv4 address for this machine.
        /// Prefers physical Ethernet/Wi-Fi adapters and ignores VMware, Hyper-V,
        /// VirtualBox, VPN and other virtual adapters so that users on the same
        /// physical LAN can always reach each other.
        /// </summary>
        public static string GetIPv4()
        {
            if (_cachedIPv4 != null) return _cachedIPv4;
            _cachedIPv4 = SelectBestIPv4() ?? "0.0.0.0";
            return _cachedIPv4;
        }

        /// <summary>Forces re-detection of the best IPv4 (e.g. after network change).</summary>
        public static void RefreshIPv4Cache()
        {
            _cachedIPv4 = null;
        }

        private static string SelectBestIPv4()
        {
            var all = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            // 1st pass: physical adapters only, scored
            var candidate = all
                .Where(ni => !IsVirtualAdapter(ni))
                .OrderByDescending(ni => AdapterScore(ni))
                .ThenByDescending(ni => ni.Speed)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork
                          && !IPAddress.IsLoopback(ua.Address)
                          && !ua.Address.ToString().StartsWith("169.254")) // exclude APIPA
                .Select(ua => ua.Address.ToString())
                .FirstOrDefault();

            if (candidate != null) return candidate;

            // 2nd pass: accept any non-loopback, non-APIPA address (fallback)
            return all
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork
                          && !IPAddress.IsLoopback(ua.Address)
                          && !ua.Address.ToString().StartsWith("169.254"))
                .Select(ua => ua.Address.ToString())
                .FirstOrDefault();
        }

        public static string GetIPv6()
        {
            if (_cachedIPv6 != null) return _cachedIPv6;
            _cachedIPv6 = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                          && !IsVirtualAdapter(ni))
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetworkV6
                          && !ua.Address.IsIPv6LinkLocal)
                .Select(ua => ua.Address.ToString())
                .FirstOrDefault() ?? "";
            return _cachedIPv6;
        }

        /// <summary>
        /// Returns subnet broadcast addresses for all active adapters including virtual ones
        /// so that broadcasts reach all subnets on the machine.
        /// </summary>
        public static IPAddress[] GetSubnetBroadcasts()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork
                          && !ua.Address.ToString().StartsWith("169.254"))
                .Select(ua =>
                {
                    try
                    {
                        var ip   = ua.Address.GetAddressBytes();
                        var mask = ua.IPv4Mask?.GetAddressBytes();
                        if (mask == null || mask.Length != 4) return null;
                        var broadcast = new byte[4];
                        for (int i = 0; i < 4; i++)
                            broadcast[i] = (byte)(ip[i] | ~mask[i]);
                        return new IPAddress(broadcast);
                    }
                    catch { return null; }
                })
                .Where(a => a != null)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Returns all local IPv4 addresses (for display in settings).
        /// Includes virtual adapters so the user can see all interfaces.
        /// </summary>
        public static IEnumerable<string> GetAllIPv4Addresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ua => $"{ua.Address}  [{ni.Name}]"))
                .ToList();
        }
    }
}
