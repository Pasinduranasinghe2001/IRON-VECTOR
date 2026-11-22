using FishNet.Object;
using UnityEngine;

namespace IronVector
{
    /// <summary>Lets players call or ride the Middle &amp; Corner elevators in AdminHall using [F].</summary>
    public class PlayerElevatorInteraction : NetworkBehaviour
    {
        PlayerIdentity _id;

        public string PromptLabel { get; private set; }

        void Awake() => _id = GetComponent<PlayerIdentity>();

        void Update()
        {
            if (!IsOwner) return;
            PromptLabel = null;

            if (_id == null || !_id.Health.IsAlive || (_id.VehicleUser != null && _id.VehicleUser.InVehicle))
                return;

            // Let Bomb plant/defuse or Vehicle entry take precedence if currently showing a prompt.
            if (_id.BombUser != null && !string.IsNullOrEmpty(_id.BombUser.PromptLabel)) return;
            if (_id.VehicleUser != null && _id.VehicleUser.NearbyVehicle != null) return;

            foreach (NetworkElevator el in NetworkElevator.All)
            {
                if (el == null) continue;

                if (el.IsPlayerInsideCab(transform.position))
                {
                    int cur = Mathf.Clamp(el.CurrentFloor.Value, 0, NetworkElevator.FloorNames.Length - 1);
                    int nxt = (el.TargetFloor.Value + 1) % NetworkElevator.FloorNames.Length;
                    PromptLabel = $"[F] {el.Label} (Floor: {NetworkElevator.FloorNames[cur]} -> {NetworkElevator.FloorNames[nxt]})";
                    if (IVInput.Down(IVKey.Interact)) el.ServerCycleNextFloor();
                    return;
                }

                if (el.IsPlayerAtLanding(transform.position, out int myFloor))
                {
                    string fName = NetworkElevator.FloorNames[myFloor];
                    if (el.TargetFloor.Value != myFloor)
                        PromptLabel = $"[F] Call {el.Label} to Floor {fName} (Now at {NetworkElevator.FloorNames[el.CurrentFloor.Value]})";
                    else
                        PromptLabel = $"{el.Label} arriving at Floor {fName}...";

                    if (IVInput.Down(IVKey.Interact)) el.ServerCallToFloor(myFloor);
                    return;
                }
            }
        }
    }
}
