using System.Collections;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Server bootstrap: when the server starts it spawns the MatchManager, vehicles and
    /// capture points; when a client finishes loading it spawns that client's player.
    /// </summary>
    public class NetworkGameServer : MonoBehaviour
    {
        public static NetworkGameServer Instance { get; private set; }

        /// <summary>Set by the menu (or -mode command line arg) before the server starts.</summary>
        public static GameMode SelectedMode = GameMode.TeamDeathmatch;

        public NetworkManager networkManager;

        [Header("Networked Prefabs")]
        public NetworkObject playerPrefab;
        public NetworkObject matchManagerPrefab;
        public NetworkObject vehiclePrefab;
        public NetworkObject capturePointPrefab;

        [Header("Map Markers")]
        public Transform[] vehicleSpawns = new Transform[0];
        public Transform[] capturePoints = new Transform[0];

        public float vehicleRespawnDelay = 20f;

        bool _worldSpawned;

        void Awake()
        {
            Instance = this;
            if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();
        }

        void Start()
        {
            if (networkManager == null)
            {
                Debug.LogError("[IRON VECTOR] No NetworkManager found in scene.");
                return;
            }
            networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (networkManager == null) return;
            if (networkManager.ServerManager != null) networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            if (networkManager.SceneManager != null) networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && !_worldSpawned)
            {
                _worldSpawned = true;
                StartCoroutine(SpawnWorldRoutine());
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _worldSpawned = false;
                StopAllCoroutines();
            }
        }

        IEnumerator SpawnWorldRoutine()
        {
            yield return null; // let the server finish initializing
            if (!networkManager.IsServerStarted) yield break;

            if (matchManagerPrefab != null)
                networkManager.ServerManager.Spawn(Instantiate(matchManagerPrefab));

            for (int i = 0; i < vehicleSpawns.Length; i++)
                SpawnVehicle(i);

            if (SelectedMode == GameMode.Domination && capturePointPrefab != null)
            {
                for (int i = 0; i < capturePoints.Length; i++)
                {
                    if (capturePoints[i] == null) continue;
                    NetworkObject cp = Instantiate(capturePointPrefab, capturePoints[i].position, capturePoints[i].rotation);
                    cp.GetComponent<CapturePoint>().ServerLabel = i;
                    networkManager.ServerManager.Spawn(cp);
                }
            }

            Debug.Log($"[IRON VECTOR] World spawned. Mode: {SelectedMode}");
        }

        void SpawnVehicle(int index)
        {
            if (vehiclePrefab == null || index < 0 || index >= vehicleSpawns.Length || vehicleSpawns[index] == null) return;
            Transform t = vehicleSpawns[index];
            NetworkObject nob = Instantiate(vehiclePrefab, t.position + Vector3.up * 0.6f, t.rotation);
            nob.GetComponent<VehicleController>().SpawnIndex = index;
            networkManager.ServerManager.Spawn(nob);
        }

        /// <summary>Called by vehicles when they are destroyed/despawned on the server.</summary>
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

            int team = ChooseTeam();
            Pose pose = SpawnManager.Instance != null
                ? SpawnManager.Instance.GetSpawnPoint(team)
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
