using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronVector
{
    /// <summary>
    /// Call of Duty–inspired edge-anchored pre-match tactical lobby UI over a live 3D helipad showcase:
    /// Top Nav Bar (Play / Weapons / Strategies / Challenges / Settings), Left Challenges & Map Select cards,
    /// Center 3D Operator + Weapon Inspector, Bottom Deploy Bar, and Right Lobby / LAN / Invite Connect column.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        public NetworkManager networkManager;
        public RoomPasswordAuthenticator passwordAuth;
        public LanDiscovery lanDiscovery;
        public Camera menuCamera;

        static readonly string[] NavTabs = { "PLAY", "WEAPONS", "STRATEGIES", "CHALLENGES", "SETTINGS" };

        int _tab;
        int _selectedMap;
        int _selectedMode;
        int _shareIpIndex;

        string _hostPassword = "";
        string _joinInput = "127.0.0.1:7770";
        string _joinPassword = "";
        string _portText = "7770";
        string _status = "Ready to deploy.";
        string _copiedFeedback = "";
        float _copiedUntil;
        bool _showInviteDrawer = true;
        Vector2 _rightScroll;
        Vector2 _weaponsScroll;

        List<NetworkAddressHelper.EndpointInfo> _endpoints = new List<NetworkAddressHelper.EndpointInfo>();
        AudioListener _menuListener;
        LobbyStageController _stage;

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
            if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
                _stage = LobbyStageController.Ensure(menuCamera);
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
            if (_stage != null) _stage.SetInspectMode(_tab == 1);
        }

        void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                _status = "Connected · Loading level...";
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
            _status = $"Deploying to {NetworkGameServer.SelectedMapScene} ({NetworkGameServer.SelectedMode})...";
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

        // =====================================================================
        // COD-STYLE TACTICAL EDGE-ANCHORED ONGUI LOBBY
        // =====================================================================

        void OnGUI()
        {
            if (Application.isBatchMode || PlayerIdentity.Local != null) return;
            TacticalGuiTheme.EnsureInitialized();

            if (networkManager != null && networkManager.IsServerStarted && !networkManager.IsClientStarted)
            {
                DrawDedicatedServerPanel();
                return;
            }

            float sw = Screen.width;
            float sh = Screen.height;

            // Atmospheric top/bottom edge vignettes preserving center 3D scene clarity
            GUI.DrawTexture(new Rect(0, 0, sw, 82f), TacticalGuiTheme.TopVignette);
            GUI.DrawTexture(new Rect(0, sh - 100f, sw, 100f), TacticalGuiTheme.BottomVignette);

            DrawTopNavigationBar(sw);

            if (_tab == 0) DrawPlayLobbyOverlay(sw, sh);
            else if (_tab == 1) DrawWeaponsShowcaseOverlay(sw, sh);
            else if (_tab == 2) DrawStrategiesOverlay(sw, sh);
            else if (_tab == 3) DrawChallengesOverlay(sw, sh);
            else DrawSettingsOverlay(sw, sh);

            HandleShowcaseMouseDrag(sw, sh);
        }

        void DrawTopNavigationBar(float sw)
        {
            TacticalGuiTheme.DrawGlassPanel(new Rect(0, 0, sw, 52f), new Color(0.04f, 0.06f, 0.09f, 0.86f), null, new Color(1f, 1f, 1f, 0.10f));
            GUI.Label(new Rect(18f, 8f, 175f, 36f), "IRON VECTOR", TacticalGuiTheme.TitleStyle);

            float tabX = 195f;
            float tabW = Mathf.Clamp((sw - 510f) / NavTabs.Length, 86f, 128f);
            for (int i = 0; i < NavTabs.Length; i++)
            {
                Rect r = new Rect(tabX + i * (tabW + 4f), 9f, tabW, 34f);
                bool active = _tab == i;
                if (GUI.Button(r, NavTabs[i], active ? TacticalGuiTheme.TabActiveStyle : TacticalGuiTheme.TabBtnStyle))
                {
                    _tab = i;
                    if (_stage != null && i != 1) _stage.InspectSecondary = false;
                }
                if (active)
                    TacticalGuiTheme.FillRect(new Rect(r.x, r.yMax - 2.5f, r.width, 2.5f), TacticalGuiTheme.AccentAmber);
            }

            // Operator Callsign & Rank Tag (Top-Right)
            float cardW = Mathf.Min(280f, sw - (tabX + NavTabs.Length * (tabW + 4f) + 20f));
            if (cardW > 150f)
            {
                Rect opCard = new Rect(sw - cardW - 14f, 6f, cardW, 40f);
                TacticalGuiTheme.DrawGlassPanel(opCard, TacticalGuiTheme.GlassCard, TacticalGuiTheme.AccentAmber);
                TacticalGuiTheme.FillRect(new Rect(opCard.x + 8f, opCard.y + 7f, 26f, 26f), new Color(0.24f, 0.34f, 0.44f));
                GUI.Label(new Rect(opCard.x + 8f, opCard.y + 7f, 26f, 26f), "55", TacticalGuiTheme.BadgeStyle);

                WeaponDatabase db = WeaponDatabase.RuntimeFallback();
                WeaponData prim = db.Get(db.ClampPrimaryIndex(LocalProfile.PrimaryWeapon));
                string strat = StrategyInfo.ShortNames[Mathf.Clamp(LocalProfile.Strategy, 0, StrategyInfo.ShortNames.Length - 1)];
                GUI.Label(new Rect(opCard.x + 42f, opCard.y + 3f, opCard.width - 50f, 18f), $"[IV] {LocalProfile.PlayerName}", TacticalGuiTheme.HeaderStyle);
                GUI.Label(new Rect(opCard.x + 42f, opCard.y + 20f, opCard.width - 50f, 16f), $"{(prim != null ? prim.weaponName : "M4A1")} · {strat}", TacticalGuiTheme.MutedStyle);
            }
        }

        void DrawPlayLobbyOverlay(float sw, float sh)
        {
            float leftW = Mathf.Clamp(sw * 0.25f, 265f, 345f);
            float rightW = Mathf.Clamp(sw * 0.27f, 285f, 365f);

            // LEFT TOP: DAILY CHALLENGES CARD (matching screenshot's "STOCKS / DAILY CHALLENGES")
            Rect chalRect = new Rect(16f, 64f, leftW, Mathf.Min(230f, sh * 0.36f));
            TacticalGuiTheme.DrawGlassPanel(chalRect, TacticalGuiTheme.GlassDark, TacticalGuiTheme.AccentAmber);
            GUI.Label(new Rect(chalRect.x + 12f, chalRect.y + 7f, leftW - 24f, 18f), "DAILY TACTICAL CHALLENGES", TacticalGuiTheme.SubGoldStyle);

            float rowY = chalRect.y + 30f;
            float rowH = (chalRect.height - 72f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var ch = TacticalGuiTheme.Challenges[i];
                Rect rowR = new Rect(chalRect.x + 10f, rowY + i * (rowH + 3f), leftW - 20f, rowH - 2f);
                TacticalGuiTheme.DrawGlassPanel(rowR, new Color(0.09f, 0.12f, 0.17f, 0.72f), ch.Done ? TacticalGuiTheme.AccentGreen : TacticalGuiTheme.AccentCyan);
                GUI.Label(new Rect(rowR.x + 8f, rowR.y + 2f, rowR.width - 62f, rowR.height - 14f), ch.Title, TacticalGuiTheme.MutedStyle);
                GUI.Label(new Rect(rowR.xMax - 56f, rowR.y + 4f, 50f, 16f), ch.Done ? "✓ DONE" : $"{ch.Current}/{ch.Target}",
                    ch.Done ? TacticalGuiTheme.SubGoldStyle : TacticalGuiTheme.RightStyle);
                TacticalGuiTheme.DrawProgressBar(new Rect(rowR.x + 8f, rowR.yMax - 8f, rowR.width - 66f, 4f), ch.Current / (float)ch.Target,
                    ch.Done ? TacticalGuiTheme.AccentGreen : TacticalGuiTheme.AccentAmber);
                GUI.Label(new Rect(rowR.xMax - 56f, rowR.y + 20f, 50f, 14f), $"{ch.Xp} XP", TacticalGuiTheme.MutedStyle);
            }
            if (GUI.Button(new Rect(chalRect.x + 10f, chalRect.yMax - 32f, leftW - 20f, 24f), "ALL CHALLENGES & CONTROLS", TacticalGuiTheme.ActionBtnStyle))
                _tab = 3;

            // LEFT BOTTOM: GRAPHICAL MAP SELECT + GAME MODE (matching screenshot's bottom-left map cards)
            float mapBoxH = Mathf.Clamp(sh * 0.36f, 215f, 260f);
            Rect mapRect = new Rect(16f, sh - mapBoxH - 16f, leftW, mapBoxH);
            TacticalGuiTheme.DrawGlassPanel(mapRect, TacticalGuiTheme.GlassDark, TacticalGuiTheme.AccentCyan);
            GUI.Label(new Rect(mapRect.x + 12f, mapRect.y + 7f, leftW - 24f, 18f), "MAP SELECTION · OPERATIONS", TacticalGuiTheme.SubGoldStyle);

            float cardH = (mapBoxH - 86f) * 0.5f;
            DrawMapVoteCard(new Rect(mapRect.x + 10f, mapRect.y + 28f, leftW - 20f, cardH), 0, "ADMIN HALL · KILINOCHCHI", "3 FLOORS · 2 ELEVATORS", TacticalGuiTheme.AdminThumb);
            DrawMapVoteCard(new Rect(mapRect.x + 10f, mapRect.y + 32f + cardH, leftW - 20f, cardH), 1, "OUTPOST VECTOR · ARENA", "COMBINED ARMS · BUGGIES", TacticalGuiTheme.ArenaThumb);

            GUI.Label(new Rect(mapRect.x + 12f, mapRect.yMax - 50f, leftW - 24f, 16f), "GAME MODE", TacticalGuiTheme.MutedStyle);
            string[] modeShort = { "TDM", "S&D", "DOM", "FFA" };
            float mW = (leftW - 26f) / 4f;
            for (int m = 0; m < 4; m++)
            {
                Rect mr = new Rect(mapRect.x + 10f + m * (mW + 2f), mapRect.yMax - 32f, mW, 24f);
                bool sel = _selectedMode == m;
                if (GUI.Button(mr, modeShort[m], sel ? TacticalGuiTheme.PrimaryBtnStyle : TacticalGuiTheme.ActionBtnStyle))
                    _selectedMode = m;
            }

            // CENTER BOTTOM: DEPLOY ACTION & MATCH STATUS (under Operator's boots)
            float centerAvail = sw - leftW - rightW - 64f;
            if (centerAvail >= 240f)
            {
                float barW = Mathf.Clamp(centerAvail, 260f, 410f);
                Rect bar = new Rect(leftW + 32f + (centerAvail - barW) * 0.5f, sh - 102f, barW, 86f);
                TacticalGuiTheme.DrawGlassPanel(bar, TacticalGuiTheme.GlassCard, TacticalGuiTheme.AccentAmber);

                string mapTitle = MapCatalog.DisplayNames[Mathf.Clamp(_selectedMap, 0, MapCatalog.DisplayNames.Length - 1)];
                string modeTitle = GameModeInfo.Names[Mathf.Clamp(_selectedMode, 0, GameModeInfo.Names.Length - 1)];
                GUI.Label(new Rect(bar.x + 12f, bar.y + 6f, bar.width - 24f, 18f), $"{mapTitle.ToUpperInvariant()}  ·  {modeTitle.ToUpperInvariant()}", TacticalGuiTheme.SmallCenterStyle);
                GUI.Label(new Rect(bar.x + 12f, bar.y + 24f, bar.width - 24f, 16f), _status, TacticalGuiTheme.MutedStyle);

                float btnW = (bar.width - 30f) * 0.65f;
                if (GUI.Button(new Rect(bar.x + 12f, bar.y + 44f, btnW, 34f), "DEPLOY / HOST MATCH", TacticalGuiTheme.PrimaryBtnStyle))
                    HostMatch();
                if (GUI.Button(new Rect(bar.x + 18f + btnW, bar.y + 44f, bar.width - 30f - btnW, 34f), "DEDICATED", TacticalGuiTheme.ActionBtnStyle))
                    StartDedicatedServer(ParsePort());
            }

            // RIGHT COLUMN: LOBBY ROSTER + LAN DISCOVERY + INVITE CODE / PASSWORD
            Rect lobbyRect = new Rect(sw - rightW - 16f, 64f, rightW, sh - 80f);
            TacticalGuiTheme.DrawGlassPanel(lobbyRect, TacticalGuiTheme.GlassDark, TacticalGuiTheme.AccentAmber);
            DrawRightLobbyColumn(lobbyRect);
        }

        void DrawMapVoteCard(Rect r, int mapIdx, string title, string subtitle, Texture2D thumb)
        {
            bool selected = _selectedMap == mapIdx;
            TacticalGuiTheme.DrawGlassPanel(r, new Color(0.06f, 0.08f, 0.12f, 0.85f),
                selected ? TacticalGuiTheme.AccentAmber : TacticalGuiTheme.BorderSubtle,
                selected ? TacticalGuiTheme.AccentAmber : TacticalGuiTheme.BorderSubtle);

            float thumbW = r.height * 1.65f;
            GUI.DrawTexture(new Rect(r.x + 5f, r.y + 5f, thumbW, r.height - 10f), thumb, ScaleMode.ScaleAndCrop);
            GUI.Label(new Rect(r.x + thumbW + 12f, r.y + 6f, r.width - thumbW - 44f, 18f), title, TacticalGuiTheme.HeaderStyle);
            GUI.Label(new Rect(r.x + thumbW + 12f, r.y + 24f, r.width - thumbW - 44f, 16f), subtitle, TacticalGuiTheme.MutedStyle);
            GUI.Label(new Rect(r.xMax - 36f, r.y + (r.height - 20f) * 0.5f, 30f, 20f), selected ? "★" : "○", TacticalGuiTheme.SubGoldStyle);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _selectedMap = mapIdx;
        }

        void DrawRightLobbyColumn(Rect r)
        {
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f));
            _rightScroll = GUILayout.BeginScrollView(_rightScroll);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"LOBBY ({Mathf.Max(1, PlayerIdentity.All.Count)}/12)", TacticalGuiTheme.SubGoldStyle);
            if (GUILayout.Button(_showInviteDrawer ? "HIDE CONNECT" : "NETWORK / CODE", TacticalGuiTheme.ActionBtnStyle, GUILayout.Width(122f), GUILayout.Height(20f)))
                _showInviteDrawer = !_showInviteDrawer;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Roster list (mimicking screenshot right lobby list)
            DrawRosterRow(1, "55", LocalProfile.PlayerName, "[HOST]", true);
            for (int slot = 2; slot <= (_showInviteDrawer ? 4 : 9); slot++)
                DrawRosterRow(slot, "--", "Open Tactical Slot", "WAIT", false);

            GUILayout.Space(8);
            GUILayout.Label("SAME NETWORK (AUTO WI-FI / LAN)", TacticalGuiTheme.SubGoldStyle);
            if (lanDiscovery == null || lanDiscovery.Servers.Count == 0)
            {
                GUILayout.Label("Scanning Wi-Fi / Hotspot / LAN (UDP 47777)...", TacticalGuiTheme.MutedStyle);
            }
            else
            {
                foreach (LanDiscovery.DiscoveredServer srv in lanDiscovery.Servers)
                {
                    GUILayout.BeginHorizontal();
                    string lockTag = srv.HasPassword ? "[LOCK] " : "";
                    GUILayout.Label($"{lockTag}{srv.HostName} · {srv.MapName} ({srv.Players}p)", TacticalGuiTheme.BodyStyle);
                    if (GUILayout.Button("JOIN", TacticalGuiTheme.PrimaryBtnStyle, GUILayout.Width(58), GUILayout.Height(22)))
                    {
                        _joinInput = $"{srv.Ip}:{srv.Port}";
                        JoinDirect(srv.Ip, srv.Port, _joinPassword);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            if (_showInviteDrawer)
            {
                GUILayout.Space(8);
                GUILayout.Label("HOST INVITE CODE & PASSWORD", TacticalGuiTheme.SubGoldStyle);

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label("Room Pass", TacticalGuiTheme.MutedStyle);
                _hostPassword = GUILayout.PasswordField(_hostPassword, '*', 24);
                GUILayout.EndVertical();
                GUILayout.Space(6);
                GUILayout.BeginVertical(GUILayout.Width(70));
                GUILayout.Label("Port", TacticalGuiTheme.MutedStyle);
                _portText = GUILayout.TextField(_portText, 6);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                var shareOptions = new List<NetworkAddressHelper.EndpointInfo>(_endpoints);
                if (!string.IsNullOrEmpty(NetworkAddressHelper.PublicIp))
                    shareOptions.Add(new NetworkAddressHelper.EndpointInfo { Label = "Internet", Ip = NetworkAddressHelper.PublicIp });
                if (shareOptions.Count > 0)
                {
                    _shareIpIndex = Mathf.Clamp(_shareIpIndex, 0, shareOptions.Count - 1);
                    string[] epLabels = new string[shareOptions.Count];
                    for (int i = 0; i < shareOptions.Count; i++) epLabels[i] = shareOptions[i].Label;
                    _shareIpIndex = GUILayout.Toolbar(_shareIpIndex, epLabels, GUILayout.Height(22));
                    string code = NetworkAddressHelper.EncodeInvite(shareOptions[_shareIpIndex].Ip, ParsePort());
                    GUILayout.BeginHorizontal();
                    GUILayout.TextField(code);
                    if (GUILayout.Button("COPY", TacticalGuiTheme.ActionBtnStyle, GUILayout.Width(56), GUILayout.Height(22)))
                    {
                        GUIUtility.systemCopyBuffer = code;
                        _copiedFeedback = "Join Code Copied!";
                        _copiedUntil = Time.unscaledTime + 3f;
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(8);
                GUILayout.Label("JOIN OTHER NETWORK (CODE / IP)", TacticalGuiTheme.SubGoldStyle);
                GUILayout.BeginHorizontal();
                _joinInput = GUILayout.TextField(_joinInput, 128);
                if (GUILayout.Button("PASTE", TacticalGuiTheme.ActionBtnStyle, GUILayout.Width(56), GUILayout.Height(22)))
                    if (!string.IsNullOrWhiteSpace(GUIUtility.systemCopyBuffer)) _joinInput = GUIUtility.systemCopyBuffer.Trim();
                GUILayout.EndHorizontal();

                GUILayout.Label("Join Password (if locked)", TacticalGuiTheme.MutedStyle);
                _joinPassword = GUILayout.PasswordField(_joinPassword, '*', 24);
                GUILayout.Space(4);
                if (GUILayout.Button("CONNECT WITH CODE / IP", TacticalGuiTheme.PrimaryBtnStyle, GUILayout.Height(30)))
                    JoinFromInput(_joinInput, _joinPassword);
            }

            GUILayout.FlexibleSpace();
            if (Time.unscaledTime < _copiedUntil && !string.IsNullOrEmpty(_copiedFeedback))
                GUILayout.Label(_copiedFeedback, TacticalGuiTheme.SubGoldStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void DrawRosterRow(int idx, string lvl, string name, string tag, bool filled)
        {
            Rect rr = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
            Color bg = filled ? new Color(0.14f, 0.19f, 0.27f, 0.85f) : new Color(0.08f, 0.10f, 0.14f, 0.45f);
            TacticalGuiTheme.DrawGlassPanel(rr, bg, filled ? TacticalGuiTheme.AccentAmber : new Color(1f, 1f, 1f, 0.08f));
            GUI.Label(new Rect(rr.x + 6f, rr.y + 2f, 24f, 20f), lvl, TacticalGuiTheme.BadgeStyle);
            GUI.Label(new Rect(rr.x + 34f, rr.y + 2f, rr.width - 90f, 20f), name, filled ? TacticalGuiTheme.HeaderStyle : TacticalGuiTheme.MutedStyle);
            GUI.Label(new Rect(rr.xMax - 54f, rr.y + 2f, 48f, 20f), tag, filled ? TacticalGuiTheme.SubGoldStyle : TacticalGuiTheme.MutedStyle);
        }

        // =====================================================================
        // TAB 1: LIVE 3D WEAPON GUNSMITH INSPECTOR
        // =====================================================================

        void DrawWeaponsShowcaseOverlay(float sw, float sh)
        {
            float leftW = Mathf.Clamp(sw * 0.27f, 275f, 340f);
            float rightW = Mathf.Clamp(sw * 0.25f, 255f, 320f);
            WeaponDatabase db = WeaponDatabase.RuntimeFallback();

            Rect leftPanel = new Rect(16f, 64f, leftW, sh - 84f);
            TacticalGuiTheme.DrawGlassPanel(leftPanel, TacticalGuiTheme.GlassDark, TacticalGuiTheme.AccentAmber);
            GUILayout.BeginArea(new Rect(leftPanel.x + 12f, leftPanel.y + 10f, leftW - 24f, leftPanel.height - 20f));
            _weaponsScroll = GUILayout.BeginScrollView(_weaponsScroll);
            GUILayout.Label("PRIMARY ARSENAL (COD4)", TacticalGuiTheme.SubGoldStyle);
            GUILayout.Space(4);

            for (int i = 0; i < WeaponDatabase.PrimaryLabels.Length; i++)
            {
                bool active = LocalProfile.PrimaryWeapon == i && (_stage == null || !_stage.InspectSecondary);
                if (GUILayout.Button((active ? "▶ " : "   ") + WeaponDatabase.PrimaryLabels[i],
                    active ? TacticalGuiTheme.PrimaryBtnStyle : TacticalGuiTheme.CardBtnStyle, GUILayout.Height(34f)))
                {
                    LocalProfile.PrimaryWeapon = i;
                    if (_stage != null) _stage.InspectSecondary = false;
                }
                GUILayout.Space(3);
            }

            GUILayout.Space(10);
            GUILayout.Label("SIDEARM SLOT 2", TacticalGuiTheme.SubGoldStyle);
            GUILayout.Space(4);
            for (int i = 0; i < WeaponDatabase.SecondaryLabels.Length; i++)
            {
                bool active = LocalProfile.SecondaryWeapon == i && _stage != null && _stage.InspectSecondary;
                if (GUILayout.Button((active ? "▶ " : "   ") + WeaponDatabase.SecondaryLabels[i],
                    active ? TacticalGuiTheme.PrimaryBtnStyle : TacticalGuiTheme.CardBtnStyle, GUILayout.Height(32f)))
                {
                    LocalProfile.SecondaryWeapon = i;
                    if (_stage != null) _stage.InspectSecondary = true;
                }
                GUILayout.Space(3);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Center drag prompt
            GUI.Label(new Rect(leftW + 24f, sh - 56f, sw - leftW - rightW - 48f, 28f),
                "DRAG CENTER VIEW TO ROTATE 3D CAD WEAPON", TacticalGuiTheme.SmallCenterStyle);

            // Right Ballistics Telemetry
            WeaponData inspected = (_stage != null && _stage.InspectSecondary)
                ? db.Get(db.ResolveSidearmIndex(LocalProfile.SecondaryWeapon))
                : db.Get(db.ClampPrimaryIndex(LocalProfile.PrimaryWeapon));

            Rect rightPanel = new Rect(sw - rightW - 16f, 64f, rightW, sh - 84f);
            TacticalGuiTheme.DrawGlassPanel(rightPanel, TacticalGuiTheme.GlassDark, TacticalGuiTheme.AccentCyan);
            if (inspected != null)
            {
                GUILayout.BeginArea(new Rect(rightPanel.x + 14f, rightPanel.y + 12f, rightW - 28f, rightPanel.height - 24f));
                GUILayout.Label(inspected.weaponName.ToUpperInvariant(), TacticalGuiTheme.TitleStyle);
                GUILayout.Label($"{inspected.category} · {(inspected.automatic ? "AUTOMATIC" : "SEMI / BOLT")}", TacticalGuiTheme.SubGoldStyle);
                GUILayout.Space(12);

                DrawStatLine("DAMAGE", Mathf.Clamp01((inspected.damage * inspected.pellets) / 140f), $"{inspected.damage * inspected.pellets:0}");
                DrawStatLine("FIRE RATE", Mathf.Clamp01(inspected.fireRateRPM / 950f), $"{inspected.fireRateRPM:0} RPM");
                DrawStatLine("RANGE", Mathf.Clamp01(inspected.range / 280f), $"{inspected.range:0}m");
                DrawStatLine("PRECISION", Mathf.Clamp01(1f - inspected.adsSpread / 3.0f), $"{inspected.adsSpread:0.00}°");
                DrawStatLine("MOBILITY", Mathf.Clamp01(inspected.moveSpeedMultiplier / 1.1f), $"{inspected.moveSpeedMultiplier * 100f:0}%");

                GUILayout.Space(12);
                GUILayout.Label($"Magazine: {inspected.magazineSize} / {inspected.reserveAmmo} Reserve", TacticalGuiTheme.BodyStyle);
                GUILayout.Label($"Reload Time: {inspected.reloadTime:0.2f}s   ·   HS Multiplier: {inspected.headshotMultiplier:0.00}x", TacticalGuiTheme.MutedStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("BACK TO PLAY LOBBY", TacticalGuiTheme.PrimaryBtnStyle, GUILayout.Height(36f))) _tab = 0;
                GUILayout.EndArea();
            }
        }

        static void DrawStatLine(string label, float frac, string valueText)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, TacticalGuiTheme.MutedStyle);
            GUILayout.Label(valueText, TacticalGuiTheme.RightStyle);
            GUILayout.EndHorizontal();
            Rect r = GUILayoutUtility.GetRect(10f, 8f, GUILayout.ExpandWidth(true));
            TacticalGuiTheme.DrawProgressBar(r, frac, TacticalGuiTheme.AccentAmber);
            GUILayout.Space(8);
        }

        // =====================================================================
        // TAB 2: STRATEGIES & OPERATOR
        // =====================================================================

        void DrawStrategiesOverlay(float sw, float sh)
        {
            float sideW = Mathf.Clamp(sw * 0.30f, 300f, 390f);
            Rect left = new Rect(16f, 64f, sideW, sh - 84f);
            TacticalGuiTheme.DrawGlassPanel(left, TacticalGuiTheme.GlassDark, TacticalGuiTheme.AccentAmber);

            GUILayout.BeginArea(new Rect(left.x + 14f, left.y + 12f, sideW - 28f, left.height - 24f));
            GUILayout.Label("OPERATOR PROFILE", TacticalGuiTheme.SubGoldStyle);
            GUILayout.Space(4);
            GUILayout.Label("Callsign", TacticalGuiTheme.MutedStyle);
            LocalProfile.PlayerName = GUILayout.TextField(LocalProfile.PlayerName, 16);
            GUILayout.Space(12);

            GUILayout.Label("4 TACTICAL STRATEGIES (PERKS)", TacticalGuiTheme.SubGoldStyle);
            GUILayout.Space(6);
            for (int i = 0; i < StrategyInfo.Names.Length; i++)
            {
                bool active = LocalProfile.Strategy == i;
                if (GUILayout.Button((active ? "★ " : "   ") + StrategyInfo.Names[i],
                    active ? TacticalGuiTheme.PrimaryBtnStyle : TacticalGuiTheme.CardBtnStyle, GUILayout.Height(46f)))
                    LocalProfile.Strategy = i;
                GUILayout.Space(6);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("READY FOR DEPLOYMENT", TacticalGuiTheme.PrimaryBtnStyle, GUILayout.Height(36f))) _tab = 0;
            GUILayout.EndArea();
        }

        // =====================================================================
        // TAB 3: CHALLENGES & CONTROLS
        // =====================================================================

        void DrawChallengesOverlay(float sw, float sh)
        {
            float w = Mathf.Min(760f, sw - 40f);
            float h = Mathf.Min(490f, sh - 90f);
            Rect panel = new Rect((sw - w) * 0.5f, 66f, w, h);
            TacticalGuiTheme.DrawGlassPanel(panel, new Color(0.05f, 0.07f, 0.10f, 0.90f), TacticalGuiTheme.AccentAmber);

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, w - 40f, h - 32f));
            GUILayout.Label("DAILY TACTICAL CHALLENGES & FIELD BRIEFING", TacticalGuiTheme.TitleStyle);
            GUILayout.Space(10);

            foreach (var ch in TacticalGuiTheme.Challenges)
            {
                Rect row = GUILayoutUtility.GetRect(10f, 44f, GUILayout.ExpandWidth(true));
                TacticalGuiTheme.DrawGlassPanel(row, TacticalGuiTheme.GlassCard, ch.Done ? TacticalGuiTheme.AccentGreen : TacticalGuiTheme.AccentCyan);
                GUI.Label(new Rect(row.x + 12f, row.y + 5f, row.width - 170f, 20f), ch.Title, TacticalGuiTheme.HeaderStyle);
                GUI.Label(new Rect(row.xMax - 150f, row.y + 6f, 138f, 18f), $"{ch.Current} / {ch.Target}  ·  +{ch.Xp} XP", TacticalGuiTheme.RightStyle);
                TacticalGuiTheme.DrawProgressBar(new Rect(row.x + 12f, row.yMax - 11f, row.width - 24f, 5f), ch.Current / (float)ch.Target,
                    ch.Done ? TacticalGuiTheme.AccentGreen : TacticalGuiTheme.AccentAmber);
                GUILayout.Space(6);
            }

            GUILayout.Space(10);
            GUILayout.Label("TACTICAL KEYBINDS", TacticalGuiTheme.SubGoldStyle);
            GUILayout.Label(
                "WASD: Move / Drive   ·   Shift: Sprint   ·   C / Ctrl: Crouch & Slide   ·   Space: Jump / Handbrake\n" +
                "LMB / RMB: Fire / ADS   ·   R: Reload   ·   1 / 2 / Scroll: Switch Gun   ·   G: Frag Grenade\n" +
                "F: Call/Ride AdminHall Elevators (G / 1F / 2F), Plant/Defuse Bomb (S&D), or Enter/Exit Vehicle\n" +
                "H: Toggle Combat HUD On/Off   ·   Tab: Scoreboard   ·   Esc: Pause & Live Settings",
                TacticalGuiTheme.BodyStyle);
            GUILayout.EndArea();
        }

        // =====================================================================
        // TAB 4: GRAPHICS & SETTINGS
        // =====================================================================

        void DrawSettingsOverlay(float sw, float sh)
        {
            float w = Mathf.Min(780f, sw - 40f);
            float h = Mathf.Min(480f, sh - 90f);
            Rect panel = new Rect((sw - w) * 0.5f, 66f, w, h);
            TacticalGuiTheme.DrawGlassPanel(panel, new Color(0.05f, 0.07f, 0.10f, 0.90f), TacticalGuiTheme.AccentCyan);

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, w - 40f, h - 32f));
            GUILayout.Label("GRAPHICS, DISPLAY & INPUT SETTINGS", TacticalGuiTheme.TitleStyle);
            GUILayout.Space(12);

            GUILayout.Label("URP Render Quality Preset", TacticalGuiTheme.SubGoldStyle);
            LocalProfile.GraphicsQuality = GUILayout.Toolbar(LocalProfile.GraphicsQuality, GraphicsSettingsManager.PresetNames, GUILayout.Height(30));
            GUILayout.Space(10);

            GUILayout.Label("Target Frame Rate Cap", TacticalGuiTheme.SubGoldStyle);
            int[] fpsOpts = { 60, 120, 144, 0 };
            string[] fpsLabels = { "60 FPS", "120 FPS", "144 FPS", "Unlimited" };
            int curFps = Array.IndexOf(fpsOpts, LocalProfile.TargetFps);
            if (curFps < 0) curFps = 2;
            int newFps = GUILayout.Toolbar(curFps, fpsLabels, GUILayout.Height(28));
            if (newFps != curFps) LocalProfile.TargetFps = fpsOpts[newFps];
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            LocalProfile.VSync = GUILayout.Toggle(LocalProfile.VSync, "  VSync (Tear-Free)");
            LocalProfile.ShowHud = GUILayout.Toggle(LocalProfile.ShowHud, "  Show Combat HUD (Key: H)");
            LocalProfile.ShowPerformance = GUILayout.Toggle(LocalProfile.ShowPerformance, "  Show FPS Counter");
            GUILayout.EndHorizontal();
            GUILayout.Space(14);

            GUILayout.Label($"Mouse Sensitivity: {LocalProfile.Sensitivity:0.0}", TacticalGuiTheme.BodyStyle);
            LocalProfile.Sensitivity = GUILayout.HorizontalSlider(LocalProfile.Sensitivity, 0.4f, 8f);
            GUILayout.Space(8);

            GUILayout.Label($"Camera Field of View (FOV): {LocalProfile.FieldOfView:0}°", TacticalGuiTheme.BodyStyle);
            LocalProfile.FieldOfView = GUILayout.HorizontalSlider(LocalProfile.FieldOfView, 65f, 110f);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("APPLY & RETURN TO LOBBY", TacticalGuiTheme.PrimaryBtnStyle, GUILayout.Height(36f))) _tab = 0;
            GUILayout.EndArea();
        }

        void DrawDedicatedServerPanel()
        {
            Rect box = new Rect(Screen.width * 0.5f - 230f, Screen.height * 0.5f - 115f, 460f, 230f);
            TacticalGuiTheme.DrawGlassPanel(box, new Color(0.05f, 0.07f, 0.10f, 0.94f), TacticalGuiTheme.AccentAmber);
            GUILayout.BeginArea(new Rect(box.x + 20, box.y + 18, box.width - 40, box.height - 36));
            GUILayout.Label("IRON VECTOR — DEDICATED SERVER", TacticalGuiTheme.SubGoldStyle);
            GUILayout.Space(10);
            GUILayout.Label($"Map: {NetworkGameServer.SelectedMapScene}   |   Mode: {NetworkGameServer.SelectedMode}", TacticalGuiTheme.BodyStyle);
            GUILayout.Label($"Players Connected: {PlayerIdentity.All.Count}   |   Port: {ParsePort()}", TacticalGuiTheme.BodyStyle);
            GUILayout.Label($"Invite Code (LAN): {NetworkAddressHelper.EncodeInvite(NetworkAddressHelper.GetPrimaryLanIp(), ParsePort())}", TacticalGuiTheme.MutedStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("STOP SERVER", TacticalGuiTheme.PrimaryBtnStyle, GUILayout.Height(38)))
                DisconnectToMenu();
            GUILayout.EndArea();
        }

        void HandleShowcaseMouseDrag(float sw, float sh)
        {
            if (_tab != 1 || _stage == null) return;
            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDrag && e.button == 0)
            {
                Rect centerZone = new Rect(sw * 0.28f, 60f, sw * 0.44f, sh - 100f);
                if (centerZone.Contains(e.mousePosition))
                    _stage.AddDragRotation(-e.delta.x);
            }
        }
    }
}
