using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>Enter / exit vehicles with F. Server validates and transfers vehicle ownership.</summary>
    public class PlayerVehicleInteraction : NetworkBehaviour
    {
        public float interactRange = 4f;

        public readonly SyncVar<NetworkObject> CurrentVehicle = new SyncVar<NetworkObject>();

        PlayerIdentity _id;

        public bool InVehicle => CurrentVehicle.Value != null;
        public VehicleController Vehicle => CurrentVehicle.Value != null ? CurrentVehicle.Value.GetComponent<VehicleController>() : null;

        /// <summary>Owner-side: closest free vehicle in range (for the HUD prompt).</summary>
        public VehicleController NearbyVehicle { get; private set; }

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
            CurrentVehicle.OnChange += OnVehicleChanged;
        }

        void OnVehicleChanged(NetworkObject prev, NetworkObject next, bool asServer)
        {
            if (_id != null) _id.RefreshState();
            if (!IsOwner) return;

            if (next != null)
            {
                _id.Look.EnterVehicleView(next.transform.eulerAngles.y);
            }
            else
            {
                VehicleController vc = prev != null ? prev.GetComponent<VehicleController>() : null;
                Vector3 exit = vc != null ? vc.FindExitPosition() : transform.position + Vector3.up;
                float yaw = prev != null ? prev.transform.eulerAngles.y : transform.eulerAngles.y;
                _id.Motor.Teleport(exit, Quaternion.Euler(0f, yaw, 0f));
                _id.Look.ResetView(yaw);
            }
        }

        void Update()
        {
            if (!IsOwner) return;
            NearbyVehicle = null;
            if (!_id.Health.IsAlive) return;

            VehicleController vehicle = Vehicle;
            if (vehicle != null)
            {
                // Ride along in the seat (body hidden, NetworkTransform keeps server in sync).
                Transform seat = vehicle.seat != null ? vehicle.seat : vehicle.transform;
                transform.SetPositionAndRotation(seat.position, Quaternion.Euler(0f, vehicle.transform.eulerAngles.y, 0f));
                if (IVInput.Down(IVKey.Interact)) ServerExit();
                return;
            }

            float best = interactRange;
            foreach (VehicleController v in VehicleController.All)
            {
                if (v == null || v.HasDriver || !v.Health.IsAlive) continue;
                float d = Vector3.Distance(transform.position, v.transform.position);
                if (d < best)
                {
                    best = d;
                    NearbyVehicle = v;
                }
            }

            if (NearbyVehicle != null && IVInput.Down(IVKey.Interact))
                ServerEnter(NearbyVehicle.NetworkObject);
        }

        [ServerRpc]
        void ServerEnter(NetworkObject vehicleObject)
        {
            if (vehicleObject == null || InVehicle || !_id.Health.IsAlive) return;
            VehicleController vc = vehicleObject.GetComponent<VehicleController>();
            if (vc == null || vc.HasDriver || !vc.Health.IsAlive) return;
            if (Vector3.Distance(transform.position, vc.transform.position) > interactRange + 3f) return;

            vc.ServerSetDriver(_id);
            CurrentVehicle.Value = vehicleObject;
        }

        [ServerRpc]
        void ServerExit() => ServerForceExit();

        /// <summary>Server: remove this player from their vehicle (death, exit, vehicle destroyed).</summary>
        public void ServerForceExit(bool releaseVehicle = true)
        {
            if (!IsServerInitialized || CurrentVehicle.Value == null) return;
            VehicleController vc = Vehicle;
            if (releaseVehicle && vc != null) vc.ServerClearDriver();
            CurrentVehicle.Value = null;
        }
    }
}
