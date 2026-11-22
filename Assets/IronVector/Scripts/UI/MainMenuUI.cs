using System;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Lobby / Connection overlay and headless CLI server bootstrap (-server -port 7770 -mode dom|tdm).
    /// Disables its menu camera once the local player spawns and restores it on disconnect.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public NetworkManager networkManager;
        public Camera menuCamera;

        string _address = "127.0.0.1";
        string _portText = "7770";
        string _status = "Ready to deploy.";
        int _selectedMode;

        GUIStyle _titleStyle, _subStyle, _labelStyle, _boxStyle, _btnStyle, _accentBtnStyle;
        Texture2D _darkTex, _panelTex, _accentTex;

        AudioListener _menuListener;

        void Awake()
        {
            if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();
            if (menuCamera == null) menuCamera = Camera.main;
            if (menuCamera != null) _menuListener = menuCamera.GetComponent<AudioListener>();
            _selectedMode = (int)NetworkGameServer.SelectedMode;
        }

        void Start()
        {
            if (networkManager != null)
            {
                networkManager.ClientManager.OnClientConnectionState += OnClientState;
                networkManager.ServerManager.OnServerConnectionState += OnServerState;
            }

            ProcessCommandLine();
        }

        void OnDestroy()
        {
            if (networkManager == null) return;
            if (networkManager.ClientManager != null) networkManager.ClientManager.OnClientConnectionState -= OnClientState;
            if (networkManager.ServerManager != null) networkManager.ServerManager.OnServerConnectionState -= OnServerState;
        }

        void ProcessCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool autoServer = Application.isBatchMode;
            string autoConnectIp = null;
            ushort port = 7770;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "-server") autoServer = true;
                else if (a == "-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort p)) port = p;
                else if (a == "-mode" && i + 1 < args.Length)
                {
                    string m = args[i + 1].ToLowerInvariant();
                    NetworkGameServer.SelectedMode = m.StartsWith("dom") ? GameMode.Domination : GameMode.TeamDeathmatch;
                    _selectedMode = (int)NetworkGameServer.SelectedMode;
                }
                else if (a == "-connect" && i + 1 < args.Length) autoConnectIp = args[i + 1];
            }

            _portText = port.ToString();

            if (autoServer)
            {
                StartDedicatedServer(port);
            }
            else if (!string.IsNullOrEmpty(autoConnectIp))
            {
                _address = autoConnectIp;
                JoinMatch(autoConnectIp, port);
            }
        }

        void Update()
        {
            bool localPlayerActive = PlayerIdentity.Local != null;
            if (menuCamera != null && menuCamera.enabled == localPlayerActive)
                menuCamera.enabled = !localPlayerActive;
            if (_menuListener != null && _menuListener.enabled == localPlayerActive)
                _menuListener.enabled = !localPlayerActive;
        }

        void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _status = "Connected.";
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _status = "Disconnected.";
                PlayerLook.ResetInputState();
            }
        }

        void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                _status = $"Server online (port {ParsePort()}).";
            else if (args.ConnectionState == LocalConnectionState.Stopped)
                _status = "Server stopped.";
        }

        ushort ParsePort() => ushort.TryParse(_portText, out ushort p) && p > 0 ? p : (ushort)7770;

        void ApplyTransport(string ip, ushort port)
        {
            if (networkManager == null || networkManager.TransportManager == null || networkManager.TransportManager.Transport == null)
                return;
            networkManager.TransportManager.Transport.SetPort(port);
            if (!string.IsNullOrWhiteSpace(ip))
                networkManager.TransportManager.Transport.SetClientAddress(ip.Trim());
        }

        public void HostMatch()
        {
            if (networkManager == null) return;
            ushort port = ParsePort();
            NetworkGameServer.SelectedMode = (GameMode)_selectedMode;
            ApplyTransport("127.0.0.1", port);
            networkManager.ServerManager.StartConnection(port);
            networkManager.ClientManager.StartConnection("127.0.0.1", port);
            _status = "Starting host...";
        }

        public void JoinMatch(string ip, ushort port)
        {
            if (networkManager == null) return;
            ApplyTransport(ip, port);
            networkManager.ClientManager.StartConnection(ip.Trim(), port);
            _status = $"Connecting to {ip}:{port}...";
        }

        public void StartDedicatedServer(ushort port)
        {
            if (networkManager == null) return;
            NetworkGameServer.SelectedMode = (GameMode)_selectedMode;
            ApplyTransport(null, port);
            networkManager.ServerManager.StartConnection(port);
            _status = $"Dedicated server listening on {port}...";
        }

        public void DisconnectAll()
        {
            if (networkManager == null) return;
            if (networkManager.IsClientStarted) networkManager.ClientManager.StopConnection();
            if (networkManager.IsServerStarted) networkManager.ServerManager.StopConnection(true);
            PlayerLook.ResetInputState();
        }

        void OnGUI()
        {
            if (Application.isBatchMode || PlayerIdentity.Local != null) return;
            EnsureStyles();

            // Dedicated server running without a local client: show minimal status card.
            if (networkManager != null && networkManager.IsServerStarted && !networkManager.IsClientStarted)
            {
                Rect box = new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.5f - 100f, 420f, 200f);
                GUI.Box(box, GUIContent.none, _boxStyle);
                GUILayout.BeginArea(new Rect(box.x + 20, box.y + 18, box.width - 40, box.height - 36));
                GUILayout.Label("IRON VECTOR — DEDICATED SERVER", _subStyle);
                GUILayout.Space(8);
                GUILayout.Label($"Status: {_status}", _labelStyle);
                GUILayout.Label($"Mode: {NetworkGameServer.SelectedMode}   |   Players Connected: {PlayerIdentity.All.Count}", _labelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("STOP SERVER", _accentBtnStyle, GUILayout.Height(36)))
                    DisconnectAll();
                GUILayout.EndArea();
                return;
            }

            float w = 480f;
            float h = 540f;
            Rect rect = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _darkTex);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            GUILayout.BeginArea(new Rect(rect.x + 24, rect.y + 20, w - 48, h - 40));

            GUILayout.Label("IRON VECTOR", _titleStyle);
            GUILayout.Label("COMBINED-ARMS TACTICAL MULTIPLAYER", _subStyle);
            GUILayout.Space(14);

            GUILayout.Label("Callsign", _labelStyle);
            LocalProfile.PlayerName = GUILayout.TextField(LocalProfile.PlayerName, 16);
            GUILayout.Space(8);

            GUILayout.Label("Primary Loadout", _labelStyle);
            LocalProfile.PrimaryWeapon = GUILayout.SelectionGrid(
                Mathf.Clamp(LocalProfile.PrimaryWeapon, 0, WeaponDatabase.DefaultPrimaryLabels.Length - 1),
                WeaponDatabase.DefaultPrimaryLabels, 2);
            GUILayout.Space(8);

            GUILayout.Label("Game Mode (Host / Server)", _labelStyle);
            _selectedMode = GUILayout.Toolbar(_selectedMode, new[] { "Team Deathmatch", "Domination" });
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Server IP", _labelStyle);
            _address = GUILayout.TextField(_address, 64);
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.BeginVertical(GUILayout.Width(100));
            GUILayout.Label("Port", _labelStyle);
            _portText = GUILayout.TextField(_portText, 6);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label($"Mouse Sensitivity: {LocalProfile.Sensitivity:0.0}   |   FOV: {LocalProfile.FieldOfView:0}", _labelStyle);
            GUILayout.BeginHorizontal();
            LocalProfile.Sensitivity = GUILayout.HorizontalSlider(LocalProfile.Sensitivity, 0.4f, 8f);
            GUILayout.Space(12);
            LocalProfile.FieldOfView = GUILayout.HorizontalSlider(LocalProfile.FieldOfView, 65f, 105f);
            GUILayout.EndHorizontal();
            GUILayout.Space(14);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HOST MATCH", _accentBtnStyle, GUILayout.Height(40)))
                HostMatch();
            GUILayout.Space(10);
            if (GUILayout.Button("JOIN MATCH", _btnStyle, GUILayout.Height(40)))
                JoinMatch(_address, ParsePort());
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            if (GUILayout.Button("START DEDICATED SERVER", _btnStyle, GUILayout.Height(32)))
                StartDedicatedServer(ParsePort());

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Status: {_status}", _labelStyle);
            GUILayout.Label("Controls: WASD Move · Shift Sprint · C/Ctrl Slide · LMB/RMB Fire/ADS · R Reload · G Frag · F Buggy · 1/2 Swap · Tab Score · Esc Pause", GUI.skin.label);

            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _darkTex = MakeSolidTex(new Color(0.05f, 0.06f, 0.08f, 0.58f));
            _panelTex = MakeSolidTex(new Color(0.08f, 0.10f, 0.13f, 0.92f));
            _accentTex = MakeSolidTex(new Color(0.84f, 0.43f, 0.12f, 1f));

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _panelTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f);

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subStyle.normal.textColor = new Color(0.92f, 0.56f, 0.22f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _labelStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);

            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            _accentBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            _accentBtnStyle.normal.background = _accentTex;
            _accentBtnStyle.normal.textColor = Color.white;
        }

        static Texture2D MakeSolidTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
