using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronVector
{
    /// <summary>
    /// Full tactical lobby UI: Map selection (AdminHall / Outpost Vector), 4 Game Modes (TDM / S&amp;D / Dom / FFA),
    /// COD4 Loadout &amp; 4 Strategies, LAN auto-discovery, Invite Join Codes + Passwords, and Graphics/HUD Settings.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        public NetworkManager networkManager;
        public RoomPasswordAuthenticator passwordAuth;
        public LanDiscovery lanDiscovery;
        public Camera menuCamera;

        int _tab; // 0 = Play/Connect, 1 = Loadout & Strategies, 2 = Graphics & Controls
        int _selectedMap;
        int _selectedMode;

        string _hostPassword = "";
        string _joinInput = "127.0.0.1:7770";
        string _joinPassword = "";
        string _portText = "7770";
        string _status = "Ready to deploy.";
        string _copiedFeedback = "";
        float _copiedUntil;
        int _shareIpIndex;

        List<NetworkAddressHelper.EndpointInfo> _endpoints = new List<NetworkAddressHelper.EndpointInfo>();
        AudioListener _menuListener;

        GUIStyle _titleStyle, _subStyle, _labelStyle, _smallStyle, _boxStyle, _cardStyle, _btnStyle, _accentBtnStyle;
        Texture2D _darkTex, _panelTex, _cardTex, _accentTex;

        void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;

            if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();
            if (passwordAuth == null && networkManager != null) passwordAuth = networkManager.GetComponent<RoomPasswordAuthenticator>();
            if (lanDiscovery == null && networkManager != null) lanDiscovery = networkManager.GetComponent<LanDiscovery>();

            RefreshMenuCamera();
            _selectedMode = (int)NetworkGameServer.SelectedMode;
            _selectedMap = Array.IndexOf(MapCatalog.SceneNames, NetworkGameServer.SelectedMapScene);
            if (_selectedMap < 0) _selectedMap = 0;

            RefreshEndpoints();
        }

        void RefreshMenuCamera()
        {
            if (menuCamera == null) menuCamera = Camera.main;
            _menuListener = menuCamera != null ? menuCamera.GetComponent<AudioListener>() : null;
        }

        void RefreshEndpoints()
        {
            _endpoints = NetworkAddressHelper.GetLocalEndpoints();
            if (_shareIpIndex >= _endpoints.Count) _shareIpIndex = 0;
        }

        void Start()
        {
            GraphicsSettingsManager.Apply();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnUnitySceneLoaded;

            if (networkManager != null)
            {
                networkManager.ClientManager.OnClientConnectionState += OnClientState;
                networkManager.ServerManager.OnServerConnectionState += OnServerState;
            }
            if (passwordAuth != null)
                passwordAuth.OnLocalAuthFeedback += OnAuthFeedback;

            if (!string.IsNullOrEmpty(MapConfig.PendingDirectMap))
            {
                int idx = Array.IndexOf(MapCatalog.SceneNames, MapConfig.PendingDirectMap);
                if (idx >= 0) _selectedMap = idx;
                MapConfig.PendingDirectMap = null;
            }

            if (lanDiscovery != null && !Application.isBatchMode)
                lanDiscovery.StartListening();

            if (!Application.isBatchMode)
                StartCoroutine(NetworkAddressHelper.FetchPublicIpRoutine(_ => { }));

            ProcessCommandLine();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            if (networkManager != null)
            {
                if (networkManager.ClientManager != null) networkManager.ClientManager.OnClientConnectionState -= OnClientState;
                if (networkManager.ServerManager != null) networkManager.ServerManager.OnServerConnectionState -= OnServerState;
            }
            if (passwordAuth != null)
                passwordAuth.OnLocalAuthFeedback -= OnAuthFeedback;
        }

        void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                RefreshMenuCamera();
                PlayerLook.ResetInputState();
                if (lanDiscovery != null && !Application.isBatchMode) lanDiscovery.StartListening();
            }
        }

        void OnAuthFeedback(bool passed, string message)
        {
            _status = message;
            if (!passed && networkManager != null && networkManager.IsClientStarted)
                networkManager.ClientManager.StopConnection();
        }

        void ProcessCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool autoServer = Application.isBatchMode;
            string autoConnect = null;
            ushort port = 7770;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "-server") autoServer = true;
                else if (a == "-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort p)) port = p;
                else if (a == "-pass" && i + 1 < args.Length) _hostPassword = args[i + 1];
                else if (a == "-map" && i + 1 < args.Length)
                {
                    int idx = Array.FindIndex(MapCatalog.SceneNames, s => s.Equals(args[i + 1], StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) _selectedMap = idx;
                }
                else if (a == "-mode" && i + 1 < args.Length)
                {
                    string m = args[i + 1].ToLowerInvariant();
                    if (m.StartsWith("snd") || m.StartsWith("search")) NetworkGameServer.SelectedMode = GameMode.SearchAndDestroy;
                    else if (m.StartsWith("dom")) NetworkGameServer.SelectedMode = GameMode.Domination;
                    else if (m.StartsWith("ffa") || m.StartsWith("free")) NetworkGameServer.SelectedMode = GameMode.FreeForAll;
                    else NetworkGameServer.SelectedMode = GameMode.TeamDeathmatch;
                    _selectedMode = (int)NetworkGameServer.SelectedMode;
                }
                else if (a == "-connect" && i + 1 < args.Length) autoConnect = args[i + 1];
            }

            _portText = port.ToString();

            if (autoServer) StartDedicatedServer(port);
            else if (!string.IsNullOrEmpty(autoConnect)) JoinFromInput(autoConnect, _joinPassword);
        }

        void Update()
        {
            bool localPlayerActive = PlayerIdentity.Local != null;
            if (menuCamera != null && menuCamera.enabled == localPlayerActive) menuCamera.enabled = !localPlayerActive;
            if (_menuListener != null && _menuListener.enabled == localPlayerActive) _menuListener.enabled = !localPlayerActive;
        }

        void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                _status = "Connected. Loading map...";
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (!_status.Contains("password")) _status = "Disconnected.";
                PlayerLook.ResetInputState();
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu" &&
                    (networkManager == null || !networkManager.IsServerStarted))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                }
            }
        }

        void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                ushort p = ParsePort();
                _status = $"Server online (port {p}).";
                if (lanDiscovery != null)
                {
                    lanDiscovery.StartAdvertising(() =>
                    {
                        string mapName = MapCatalog.DisplayNames[Mathf.Clamp(_selectedMap, 0, MapCatalog.DisplayNames.Length - 1)];
                        string modeName = GameModeInfo.Names[Mathf.Clamp(_selectedMode, 0, GameModeInfo.Names.Length - 1)];
                        string passFlag = string.IsNullOrEmpty(_hostPassword) ? "0" : "1";
                        return $"IV4|{LocalProfile.PlayerName}|{mapName}|{modeName}|{PlayerIdentity.All.Count}|{p}|{passFlag}";
                    });
                }
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _status = "Server stopped.";
                lanDiscovery?.StopAdvertising();
            }
        }

        ushort ParsePort() => ushort.TryParse(_portText, out ushort p) && p > 0 ? p : (ushort)7770;

        void ApplyServerSettings(ushort port)
        {
            NetworkGameServer.SelectedMapScene = MapCatalog.SceneNames[Mathf.Clamp(_selectedMap, 0, MapCatalog.SceneNames.Length - 1)];
            NetworkGameServer.SelectedMode = (GameMode)Mathf.Clamp(_selectedMode, 0, 3);
            if (passwordAuth != null)
            {
                passwordAuth.ServerPassword = _hostPassword ?? "";
                passwordAuth.ClientPassword = _hostPassword ?? "";
            }
            if (networkManager != null && networkManager.TransportManager.Transport != null)
                networkManager.TransportManager.Transport.SetPort(port);
        }

        public void HostMatch()
        {
            if (networkManager == null) return;
            ushort port = ParsePort();
            ApplyServerSettings(port);
            networkManager.TransportManager.Transport?.SetClientAddress("127.0.0.1");
            networkManager.ServerManager.StartConnection(port);
            networkManager.ClientManager.StartConnection("127.0.0.1", port);
            _status = $"Hosting {NetworkGameServer.SelectedMapScene} ({NetworkGameServer.SelectedMode})...";
        }

        public void StartDedicatedServer(ushort port)
        {
            if (networkManager == null) return;
            ApplyServerSettings(port);
            networkManager.ServerManager.StartConnection(port);
            _status = $"Dedicated server listening on {port}...";
        }

        public void JoinFromInput(string rawInput, string password)
        {
            if (!NetworkAddressHelper.TryDecodeInvite(rawInput, out string ip, out ushort port))
            {
                _status = "Invalid IP or Join Code.";
                return;
            }
            JoinDirect(ip, port, password);
        }

        public void JoinDirect(string ip, ushort port, string password)
        {
            if (networkManager == null) return;
            if (passwordAuth != null) passwordAuth.ClientPassword = password ?? "";
            if (networkManager.TransportManager.Transport != null)
            {
                networkManager.TransportManager.Transport.SetClientAddress(ip);
                networkManager.TransportManager.Transport.SetPort(port);
            }
            networkManager.ClientManager.StartConnection(ip, port);
            _status = $"Connecting to {ip}:{port}...";
        }

        public void DisconnectToMenu()
        {
            if (networkManager != null)
            {
                if (networkManager.IsClientStarted) networkManager.ClientManager.StopConnection();
                if (networkManager.IsServerStarted) networkManager.ServerManager.StopConnection(true);
            }
            PlayerLook.ResetInputState();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        void OnGUI()
        {
            if (Application.isBatchMode || PlayerIdentity.Local != null) return;
            EnsureStyles();

            if (networkManager != null && networkManager.IsServerStarted && !networkManager.IsClientStarted)
            {
                DrawDedicatedServerPanel();
                return;
            }

            float w = 780f;
            float h = 585f;
            Rect rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _darkTex);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            GUILayout.BeginArea(new Rect(rect.x + 22, rect.y + 16, w - 44, h - 32));
            GUILayout.Label("IRON VECTOR", _titleStyle);
            GUILayout.Label("MODERN WARFARE TACTICAL MULTIPLAYER", _subStyle);
            GUILayout.Space(10);

            _tab = GUILayout.Toolbar(_tab, new[] { "MULTIPLAYER (HOST / JOIN)", "LOADOUT & 4 STRATEGIES", "GRAPHICS & SETTINGS" }, GUILayout.Height(30));
            GUILayout.Space(10);

            if (_tab == 0) DrawMultiplayerTab();
            else if (_tab == 1) DrawLoadoutTab();
            else DrawSettingsTab();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Status: {_status}", _labelStyle);
            if (Time.unscaledTime < _copiedUntil && !string.IsNullOrEmpty(_copiedFeedback))
                GUILayout.Label(_copiedFeedback, _subStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void DrawDedicatedServerPanel()
        {
            Rect box = new Rect(Screen.width * 0.5f - 230f, Screen.height * 0.5f - 115f, 460f, 230f);
            GUI.Box(box, GUIContent.none, _boxStyle);
            GUILayout.BeginArea(new Rect(box.x + 20, box.y + 18, box.width - 40, box.height - 36));
            GUILayout.Label("IRON VECTOR — DEDICATED SERVER", _subStyle);
            GUILayout.Space(10);
            GUILayout.Label($"Map: {NetworkGameServer.SelectedMapScene}   |   Mode: {NetworkGameServer.SelectedMode}", _labelStyle);
            GUILayout.Label($"Players Connected: {PlayerIdentity.All.Count}   |   Port: {ParsePort()}", _labelStyle);
            GUILayout.Label($"Invite Code (LAN): {NetworkAddressHelper.EncodeInvite(NetworkAddressHelper.GetPrimaryLanIp(), ParsePort())}", _smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("STOP SERVER", _accentBtnStyle, GUILayout.Height(38)))
                DisconnectToMenu();
            GUILayout.EndArea();
        }

        void DrawMultiplayerTab()
        {
            GUILayout.BeginHorizontal();

            // LEFT COLUMN: HOST MATCH
            GUILayout.BeginVertical(_cardStyle, GUILayout.Width(355), GUILayout.Height(410));
            GUILayout.Label("HOST MATCH", _subStyle);
            GUILayout.Space(6);

            GUILayout.Label("Select Map", _labelStyle);
            _selectedMap = GUILayout.SelectionGrid(_selectedMap, MapCatalog.DisplayNames, 1);
            GUILayout.Space(6);

            GUILayout.Label("Game Mode", _labelStyle);
            _selectedMode = GUILayout.SelectionGrid(_selectedMode, GameModeInfo.Names, 2);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Room Password (Optional)", _labelStyle);
            _hostPassword = GUILayout.PasswordField(_hostPassword, '*', 24);
            GUILayout.EndVertical();
            GUILayout.Space(8);
            GUILayout.BeginVertical(GUILayout.Width(80));
            GUILayout.Label("Port", _labelStyle);
            _portText = GUILayout.TextField(_portText, 6);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // Shareable Invite Code / Link builder
            GUILayout.Label("Share Link / Invite Code for Friends:", _labelStyle);
            var shareOptions = new List<NetworkAddressHelper.EndpointInfo>(_endpoints);
            if (!string.IsNullOrEmpty(NetworkAddressHelper.PublicIp))
                shareOptions.Add(new NetworkAddressHelper.EndpointInfo { Label = "Internet", Ip = NetworkAddressHelper.PublicIp });
            if (shareOptions.Count > 0)
            {
                _shareIpIndex = Mathf.Clamp(_shareIpIndex, 0, shareOptions.Count - 1);
                string[] epLabels = new string[shareOptions.Count];
                for (int i = 0; i < shareOptions.Count; i++) epLabels[i] = shareOptions[i].Label;
                _shareIpIndex = GUILayout.Toolbar(_shareIpIndex, epLabels);
                string chosenIp = shareOptions[_shareIpIndex].Ip;
                string code = NetworkAddressHelper.EncodeInvite(chosenIp, ParsePort());
                GUILayout.BeginHorizontal();
                GUILayout.TextField(code, GUILayout.Width(245));
                if (GUILayout.Button("COPY", GUILayout.Width(70)))
                {
                    GUIUtility.systemCopyBuffer = code;
                    _copiedFeedback = "Copied Join Code!";
                    _copiedUntil = Time.unscaledTime + 3f;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("HOST MATCH", _accentBtnStyle, GUILayout.Height(38))) HostMatch();
            GUILayout.Space(4);
            if (GUILayout.Button("DEDICATED SERVER", _btnStyle, GUILayout.Height(28))) StartDedicatedServer(ParsePort());
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // RIGHT COLUMN: JOIN MATCH (SAME NETWORK + DIFFERENT NETWORKS)
            GUILayout.BeginVertical(_cardStyle, GUILayout.Width(365), GUILayout.Height(410));
            GUILayout.Label("JOIN MATCH (SAME WI-FI / LAN OR INTERNET)", _subStyle);
            GUILayout.Space(6);

            GUILayout.Label("Same Network Matches (Auto-Detected):", _labelStyle);
            if (lanDiscovery == null || lanDiscovery.Servers.Count == 0)
            {
                GUILayout.Label("Searching Wi-Fi / Hotspot / Wired LAN (UDP 47777)...", _smallStyle);
            }
            else
            {
                foreach (LanDiscovery.DiscoveredServer srv in lanDiscovery.Servers)
                {
                    GUILayout.BeginHorizontal(_boxStyle);
                    string lockTag = srv.HasPassword ? "[LOCK] " : "";
                    GUILayout.Label($"{lockTag}{srv.HostName} · {srv.MapName} · {srv.ModeName} ({srv.Players}p)", _smallStyle);
                    if (GUILayout.Button("JOIN", _accentBtnStyle, GUILayout.Width(64), GUILayout.Height(24)))
                    {
                        _joinInput = $"{srv.Ip}:{srv.Port}";
                        JoinDirect(srv.Ip, srv.Port, _joinPassword);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Different Networks / Direct Join Code or IP:", _labelStyle);
            GUILayout.BeginHorizontal();
            _joinInput = GUILayout.TextField(_joinInput, 128);
            if (GUILayout.Button("PASTE", GUILayout.Width(64)))
                if (!string.IsNullOrWhiteSpace(GUIUtility.systemCopyBuffer)) _joinInput = GUIUtility.systemCopyBuffer.Trim();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Room Password (if locked)", _labelStyle);
            _joinPassword = GUILayout.PasswordField(_joinPassword, '*', 24);

            GUILayout.Space(8);
            if (GUILayout.Button("JOIN WITH CODE / IP", _accentBtnStyle, GUILayout.Height(36)))
                JoinFromInput(_joinInput, _joinPassword);

            GUILayout.Space(8);
            GUILayout.Label(
                "Connection Guide:\n" +
                "• Same Wi-Fi / Hotspot / LAN: Click JOIN above (zero IP needed).\n" +
                "• Different Networks: Use Tailscale / ZeroTier (pick VPN/Tunnel code) OR forward UDP port 7770 (Internet code), then share the Join Code + Password.",
                _smallStyle);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void DrawLoadoutTab()
        {
            GUILayout.BeginVertical(_cardStyle, GUILayout.Height(410));

            GUILayout.Label("OPERATOR CALLSIGN", _subStyle);
            LocalProfile.PlayerName = GUILayout.TextField(LocalProfile.PlayerName, 16, GUILayout.Width(280));
            GUILayout.Space(10);

            GUILayout.Label("PRIMARY WEAPON (CALL OF DUTY 4: MODERN WARFARE ARSENAL)", _subStyle);
            LocalProfile.PrimaryWeapon = GUILayout.SelectionGrid(
                Mathf.Clamp(LocalProfile.PrimaryWeapon, 0, WeaponDatabase.PrimaryLabels.Length - 1),
                WeaponDatabase.PrimaryLabels, 2, GUILayout.Height(110));
            GUILayout.Space(10);

            GUILayout.Label("SIDEARM (SLOT 2)", _subStyle);
            LocalProfile.SecondaryWeapon = GUILayout.SelectionGrid(
                Mathf.Clamp(LocalProfile.SecondaryWeapon, 0, WeaponDatabase.SecondaryLabels.Length - 1),
                WeaponDatabase.SecondaryLabels, 2, GUILayout.Height(32));
            GUILayout.Space(10);

            GUILayout.Label("TACTICAL STRATEGY (SELECT 1 OF 4)", _subStyle);
            LocalProfile.Strategy = GUILayout.SelectionGrid(
                Mathf.Clamp(LocalProfile.Strategy, 0, StrategyInfo.Names.Length - 1),
                StrategyInfo.Names, 2, GUILayout.Height(64));

            GUILayout.FlexibleSpace();
            GUILayout.Label("Controls: WASD Move · Shift Sprint · C/Ctrl Slide · LMB/RMB Fire/ADS · R Reload · G Frag · F Bomb/Vehicle · 1/2 Swap · H HUD Off · Esc Pause", _smallStyle);
            GUILayout.EndVertical();
        }

        void DrawSettingsTab()
        {
            GUILayout.BeginVertical(_cardStyle, GUILayout.Height(410));

            GUILayout.Label("GRAPHICS & PERFORMANCE", _subStyle);
            GUILayout.Space(6);

            GUILayout.Label("Graphics Quality Preset", _labelStyle);
            LocalProfile.GraphicsQuality = GUILayout.Toolbar(LocalProfile.GraphicsQuality, GraphicsSettingsManager.PresetNames, GUILayout.Height(30));
            GUILayout.Space(10);

            GUILayout.Label("Target Frame Rate", _labelStyle);
            int[] fpsOpts = { 60, 120, 144, 0 };
            string[] fpsLabels = { "60 FPS", "120 FPS", "144 FPS", "Unlimited" };
            int curFpsIdx = Array.IndexOf(fpsOpts, LocalProfile.TargetFps);
            if (curFpsIdx < 0) curFpsIdx = 2;
            int newFpsIdx = GUILayout.Toolbar(curFpsIdx, fpsLabels, GUILayout.Height(28));
            if (newFpsIdx != curFpsIdx) LocalProfile.TargetFps = fpsOpts[newFpsIdx];
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            LocalProfile.VSync = GUILayout.Toggle(LocalProfile.VSync, "  VSync (Reduce Screen Tearing)");
            LocalProfile.ShowHud = GUILayout.Toggle(LocalProfile.ShowHud, "  Show Combat HUD (Toggle anytime with H)");
            LocalProfile.ShowPerformance = GUILayout.Toggle(LocalProfile.ShowPerformance, "  Show FPS Counter");
            GUILayout.EndHorizontal();

            GUILayout.Space(14);
            GUILayout.Label($"Mouse Sensitivity: {LocalProfile.Sensitivity:0.0}", _labelStyle);
            LocalProfile.Sensitivity = GUILayout.HorizontalSlider(LocalProfile.Sensitivity, 0.4f, 8f);
            GUILayout.Space(8);

            GUILayout.Label($"Field of View (FOV): {LocalProfile.FieldOfView:0}", _labelStyle);
            LocalProfile.FieldOfView = GUILayout.HorizontalSlider(LocalProfile.FieldOfView, 65f, 110f);

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _darkTex = MakeSolidTex(new Color(0.04f, 0.05f, 0.07f, 0.62f));
            _panelTex = MakeSolidTex(new Color(0.07f, 0.09f, 0.12f, 0.94f));
            _cardTex = MakeSolidTex(new Color(0.11f, 0.14f, 0.18f, 0.88f));
            _accentTex = MakeSolidTex(new Color(0.84f, 0.43f, 0.12f, 1f));

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _panelTex;

            _cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
            _cardStyle.normal.background = _cardTex;

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _titleStyle.normal.textColor = Color.white;

            _subStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _subStyle.normal.textColor = new Color(0.95f, 0.58f, 0.22f);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _labelStyle.normal.textColor = new Color(0.88f, 0.90f, 0.94f);

            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _smallStyle.normal.textColor = new Color(0.75f, 0.79f, 0.85f);

            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            _accentBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
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
