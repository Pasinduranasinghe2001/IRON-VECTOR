using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Managing;

namespace IronVector
{
    /// <summary>
    /// Placed inside each map scene (AdminHall, Arena) to supply spawn/objective markers.
    /// If a developer presses Play directly inside a map scene, redirects cleanly to MainMenu boot scene.
    /// </summary>
    public class MapConfig : MonoBehaviour
    {
        public static MapConfig Instance { get; private set; }
        public static string PendingDirectMap { get; set; }

        public string mapDisplayName = "AdminHall";
        public Transform[] vehicleSpawns = new Transform[0];
        public Transform[] capturePoints = new Transform[0];
        public Transform[] bombSites = new Transform[0];
        public Transform[] elevatorSpawns = new Transform[0];

        void Awake()
        {
            Instance = this;
            if (FindAnyObjectByType<NetworkManager>() == null && !Application.isBatchMode)
            {
                PendingDirectMap = gameObject.scene.name;
                SceneManager.LoadScene("MainMenu");
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
