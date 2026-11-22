using System.Collections;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronVector
{
    /// <summary>
    /// Persistent server bootstrap: loads the selected map scene across all clients,
    /// spawns MatchManager, capture/bomb objectives and vehicles, and spawns joining players.
    /// </summary>
    public class NetworkGameServer : MonoBehaviour
    {
        public static NetworkGameServer Instance { get; private set; }

        public static GameMode SelectedMode = GameMode.TeamDeathmatch;
        public static string SelectedMapScene = "AdminHall";

        public NetworkManager networkManager;

        [Header("Networked Prefabs")]
        public NetworkObject playerPrefab;
        public NetworkObject matchManagerPrefab;
        public NetworkObject vehiclePrefab;
        public NetworkObject capturePointPrefab;
        public NetworkObject bombSitePrefab;
        public NetworkObject elevatorPrefab;

        public float vehicleRespawnDelay = 20f;

        bool _worldSpawned;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();
        }

        void Start()
        {
            if (networkManager == null)
            {
                Debug.LogError("[IRON VECTOR] No NetworkManager found.");
                return;
            }
            networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            networkManager.SceneManager.OnLoadEnd += OnSceneLoadEnd;
            networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (networkManager == null) return;
            if (networkManager.ServerManager != null) networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            if (networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
                networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            }
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _worldSpawned = false;
                string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (activeScene != SelectedMapScene)
                {
                    var sld = new SceneLoadData(SelectedMapScene)
                    {
                        ReplaceScenes = ReplaceOption.All
                    };
                    networkManager.SceneManager.LoadGlobalScenes(sld);
                }
                else
                {
                    StartCoroutine(SpawnWorldRoutine());
                }
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _worldSpawned = false;
                StopAllCoroutines();
            }
        }

        void OnSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!networkManager.IsServerStarted || _worldSpawned) return;
            if (args.QueueData.AsServer && MapConfig.Instance != null)
                StartCoroutine(SpawnWorldRoutine());
        }

        IEnumerator SpawnWorldRoutine()
        {
            yield return null;
            if (!networkManager.IsServerStarted || _worldSpawned) yield break;
            _worldSpawned = true;

            MapConfig map = MapConfig.Instance;

            if (matchManagerPrefab != null && MatchManager.Instance == null)
                networkManager.ServerManager.Spawn(Instantiate(matchManagerPrefab));

            if (map != null)
            {
                if (SelectedMode != GameMode.SearchAndDestroy)
                {
                    for (int i = 0; i < map.vehicleSpawns.Length; i++)
                        SpawnVehicle(i);
                }

                if (SelectedMode == GameMode.Domination && capturePointPrefab != null)
                {
                    for (int i = 0; i < map.capturePoints.Length; i++)
                    {
                        Transform t = map.capturePoints[i];
                        if (t == null) continue;
                        NetworkObject cp = Instantiate(capturePointPrefab, t.position, t.rotation);
                        cp.GetComponent<CapturePoint>().ServerLabel = i;
                        networkManager.ServerManager.Spawn(cp);
                    }
                }

                if (SelectedMode == GameMode.SearchAndDestroy && bombSitePrefab != null)
                {
                    for (int i = 0; i < map.bombSites.Length; i++)
                    {
                        Transform t = map.bombSites[i];
                        if (t == null) continue;
                        NetworkObject bs = Instantiate(bombSitePrefab, t.position, t.rotation);
                        bs.GetComponent<BombSite>().ServerSiteIndex = i;
                        networkManager.ServerManager.Spawn(bs);
                    }
                }

                if (elevatorPrefab != null)
                {
                    for (int i = 0; i < map.elevatorSpawns.Length; i++)
                    {
                        Transform t = map.elevatorSpawns[i];
                        if (t == null) continue;
                        NetworkObject el = Instantiate(elevatorPrefab, t.position, t.rotation);
                        el.GetComponent<NetworkElevator>().ServerShaftIndex = i;
                        networkManager.ServerManager.Spawn(el);
                    }
                }
            }

            Debug.Log($"[IRON VECTOR] Map '{SelectedMapScene}' started. Mode: {SelectedMode}");
        }

        void SpawnVehicle(int index)
        {
            MapConfig map = MapConfig.Instance;
            if (vehiclePrefab == null || map == null || index < 0 || index >= map.vehicleSpawns.Length || map.vehicleSpawns[index] == null) return;
            Transform t = map.vehicleSpawns[index];
            NetworkObject nob = Instantiate(vehiclePrefab, t.position + Vector3.up * 0.6f, t.rotation);
            nob.GetComponent<VehicleController>().SpawnIndex = index;
            networkManager.ServerManager.Spawn(nob);
        }

        public void OnVehicleRemoved(int index)
        {
            if (index < 0 || networkManager == null || !networkManager.IsServerStarted || !isActiveAndEnabled) return;
            StartCoroutine(RespawnVehicleRoutine(index));
        }

        IEnumerator RespawnVehicleRoutine(int index)
        {
            yield return new WaitForSeconds(vehicleRespawnDelay);
            if (networkManager.IsServerStarted) SpawnVehicle(index);
        }

        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer || playerPrefab == null) return;
            // Only spawn players once a gameplay map scene is active.
            if (MapConfig.Instance == null) return;

            bool ffa = SelectedMode == GameMode.FreeForAll;
            int team = ffa ? Teams.None : ChooseTeam();
            Pose pose = SpawnManager.Instance != null
                ? SpawnManager.Instance.GetSpawnPoint(team, ffa)
                : new Pose(Vector3.up * 2f, Quaternion.identity);

            NetworkObject nob = Instantiate(playerPrefab, pose.position, pose.rotation);
            nob.GetComponent<PlayerIdentity>().PendingTeam = team;
            networkManager.ServerManager.Spawn(nob, conn);
            networkManager.SceneManager.AddOwnerToDefaultScene(nob);
        }

        static int ChooseTeam()
        {
            int alpha = 0, bravo = 0;
            foreach (PlayerIdentity p in PlayerIdentity.All)
            {
                if (p == null) continue;
                if (p.Team.Value == Teams.Alpha) alpha++;
                else if (p.Team.Value == Teams.Bravo) bravo++;
            }
            return alpha <= bravo ? Teams.Alpha : Teams.Bravo;
        }
    }
}
