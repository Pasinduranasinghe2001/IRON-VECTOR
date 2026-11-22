using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Zero-config UDP discovery on port 47777 so players on the same Wi-Fi, hotspot, or Ethernet LAN
    /// automatically see active matches and join without typing IP addresses.
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        public const int DiscoveryPort = 47777;

        public struct DiscoveredServer
        {
            public string HostName;
            public string Ip;
            public ushort Port;
            public string MapName;
            public string ModeName;
            public int Players;
            public bool HasPassword;
            public float LastSeen;
        }

        public readonly List<DiscoveredServer> Servers = new List<DiscoveredServer>();

        UdpClient _listener;
        UdpClient _broadcaster;
        bool _advertising;
        float _nextBroadcast;
        Func<string> _payloadBuilder;

        public void StartAdvertising(Func<string> buildPayload)
        {
            _payloadBuilder = buildPayload;
            _advertising = true;
            _nextBroadcast = 0f;
            try
            {
                if (_broadcaster == null)
                {
                    _broadcaster = new UdpClient();
                    _broadcaster.EnableBroadcast = true;
                }
            }
            catch { }
        }

        public void StopAdvertising()
        {
            _advertising = false;
            try { _broadcaster?.Close(); } catch { }
            _broadcaster = null;
        }

        public void StartListening()
        {
            if (_listener != null) return;
            try
            {
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _listener.EnableBroadcast = true;
            }
            catch
            {
                try { _listener?.Close(); } catch { }
                _listener = null;
            }
        }

        public void StopListening()
        {
            try { _listener?.Close(); } catch { }
            _listener = null;
            Servers.Clear();
        }

        void OnDestroy()
        {
            StopAdvertising();
            StopListening();
        }

        void Update()
        {
            if (_advertising && _broadcaster != null && _payloadBuilder != null && Time.unscaledTime >= _nextBroadcast)
            {
                _nextBroadcast = Time.unscaledTime + 1.0f;
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(_payloadBuilder());
                    _broadcaster.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                }
                catch { }
            }

            if (_listener != null)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    while (_listener.Available > 0)
                    {
                        byte[] data = _listener.Receive(ref remote);
                        ParsePacket(Encoding.UTF8.GetString(data), remote.Address.ToString());
                    }
                }
                catch { }

                for (int i = Servers.Count - 1; i >= 0; i--)
                    if (Time.unscaledTime - Servers[i].LastSeen > 4.0f) Servers.RemoveAt(i);
            }
        }

        void ParsePacket(string text, string senderIp)
        {
            // Format: IV4|HostName|Map|Mode|Players|GamePort|HasPassword
            string[] parts = text.Split('|');
            if (parts.Length < 7 || parts[0] != "IV4") return;
            if (!ushort.TryParse(parts[5], out ushort gamePort)) return;
            int.TryParse(parts[4], out int players);
            bool hasPass = parts[6] == "1";

            var entry = new DiscoveredServer
            {
                HostName = parts[1],
                Ip = senderIp,
                Port = gamePort,
                MapName = parts[2],
                ModeName = parts[3],
                Players = players,
                HasPassword = hasPass,
                LastSeen = Time.unscaledTime
            };

            for (int i = 0; i < Servers.Count; i++)
            {
                if (Servers[i].Ip == entry.Ip && Servers[i].Port == entry.Port)
                {
                    Servers[i] = entry;
                    return;
                }
            }
            Servers.Add(entry);
        }
    }
}
