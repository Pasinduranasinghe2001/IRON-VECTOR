using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace IronVector
{
    /// <summary>
    /// Discovers local LAN / VPN / Public IPv4 endpoints and encodes/decodes shareable
    /// Iron Vector Join Codes (e.g. "IV-MTkyLjE2OC4xLjU6Nzc3MA") so friends can join across networks.
    /// </summary>
    public static class NetworkAddressHelper
    {
        public struct EndpointInfo
        {
            public string Label;
            public string Ip;
        }

        public static string PublicIp { get; private set; } = "";

        public static List<EndpointInfo> GetLocalEndpoints()
        {
            var results = new List<EndpointInfo>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    IPInterfaceProperties props = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254.")) continue;

                        string desc = (ni.Name + " " + ni.Description).ToLowerInvariant();
                        string tag;
                        if (desc.Contains("tailscale") || desc.Contains("zerotier") || desc.Contains("radmin") || desc.Contains("hamachi") || desc.Contains("wireguard") || ip.StartsWith("100.") || ip.StartsWith("26."))
                            tag = "VPN/Tunnel";
                        else if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || desc.Contains("wi-fi") || desc.Contains("wireless"))
                            tag = "Wi-Fi";
                        else
                            tag = "LAN/Ethernet";

                        results.Add(new EndpointInfo { Label = tag, Ip = ip });
                    }
                }
            }
            catch { }

            if (results.Count == 0)
                results.Add(new EndpointInfo { Label = "Localhost", Ip = "127.0.0.1" });
            return results;
        }

        public static string GetPrimaryLanIp()
        {
            List<EndpointInfo> list = GetLocalEndpoints();
            foreach (EndpointInfo e in list)
                if (e.Label == "Wi-Fi" || e.Label == "LAN/Ethernet") return e.Ip;
            return list[0].Ip;
        }

        public static IEnumerator FetchPublicIpRoutine(Action<string> onDone)
        {
            using (UnityWebRequest req = UnityWebRequest.Get("https://api.ipify.org"))
            {
                req.timeout = 4;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    string ip = req.downloadHandler.text.Trim();
                    if (IPAddress.TryParse(ip, out _))
                    {
                        PublicIp = ip;
                        onDone?.Invoke(ip);
                    }
                }
            }
        }

        public static string EncodeInvite(string ip, ushort port)
        {
            string raw = $"{ip.Trim()}:{port}";
            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return "IV-" + b64;
        }

        public static bool TryDecodeInvite(string input, out string ip, out ushort port)
        {
            ip = "127.0.0.1";
            port = 7770;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string s = input.Trim();
            if (s.StartsWith("iv://", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(5);

            if (s.StartsWith("IV-", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string payload = s.Substring(3).Replace('-', '+').Replace('_', '/');
                    int mod = payload.Length % 4;
                    if (mod > 0) payload += new string('=', 4 - mod);
                    s = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                }
                catch { return false; }
            }

            int colon = s.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(s.Substring(colon + 1), out ushort parsedPort))
            {
                ip = s.Substring(0, colon).Trim();
                port = parsedPort;
                return !string.IsNullOrEmpty(ip);
            }

            ip = s;
            return !string.IsNullOrEmpty(ip);
        }
    }
}
