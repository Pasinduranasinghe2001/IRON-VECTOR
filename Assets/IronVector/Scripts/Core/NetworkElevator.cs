using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Multiplayer networked 3-floor elevator (Ground = 0m, 1st Floor = 4.2m, 2nd Floor = 8.4m).
    /// Supports calling from any landing or cycling floors from inside the cab using [F].
    /// </summary>
    public class NetworkElevator : NetworkBehaviour
    {
        public static readonly List<NetworkElevator> All = new List<NetworkElevator>();
        public static readonly float[] FloorHeights = { 0.0f, 4.2f, 8.4f };
        public static readonly string[] FloorNames = { "G", "1F", "2F" };

        public float speed = 3.8f;
        public string elevatorLabel = "Middle Elevator";

        [System.NonSerialized] public int ServerShaftIndex;

        public readonly SyncVar<int> ShaftIndex = new SyncVar<int>(0);
        public readonly SyncVar<int> TargetFloor = new SyncVar<int>(0);
        public readonly SyncVar<int> CurrentFloor = new SyncVar<int>(0);
        public readonly SyncVar<bool> IsMoving = new SyncVar<bool>(false);

        public Vector3 FrameDelta { get; private set; }
        public string Label => ShaftIndex.Value == 0 ? "Middle Elevator" : "Corner Elevator";

        Vector3 _basePos;
        Vector3 _lastPos;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!All.Contains(this)) All.Add(this);
            _basePos = new Vector3(transform.position.x, 0f, transform.position.z);
            _lastPos = transform.position;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            All.Remove(this);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ShaftIndex.Value = ServerShaftIndex;
            TargetFloor.Value = 0;
            CurrentFloor.Value = 0;
            IsMoving.Value = false;
            transform.position = _basePos;
            _lastPos = transform.position;
        }

        void Update()
        {
            if (IsServerInitialized)
            {
                float targetY = FloorHeights[Mathf.Clamp(TargetFloor.Value, 0, FloorHeights.Length - 1)];
                Vector3 pos = transform.position;
                float nextY = Mathf.MoveTowards(pos.y, targetY, speed * Time.deltaTime);
                bool moving = !Mathf.Approximately(nextY, targetY);
                transform.position = new Vector3(_basePos.x, nextY, _basePos.z);

                if (IsMoving.Value && !moving)
                    ObserversDing(transform.position);

                IsMoving.Value = moving;
                CurrentFloor.Value = NearestFloorIndex(nextY);
            }

            FrameDelta = transform.position - _lastPos;
            _lastPos = transform.position;
        }

        static int NearestFloorIndex(float y)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < FloorHeights.Length; i++)
            {
                float d = Mathf.Abs(y - FloorHeights[i]);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        [ObserversRpc]
        void ObserversDing(Vector3 pos)
        {
            if (FxUtil.Enabled) FxUtil.PlaySound(FxUtil.ClickClip, pos, 0.55f, 25f, 1.35f);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ServerCycleNextFloor()
        {
            int next = (TargetFloor.Value + 1) % FloorHeights.Length;
            TargetFloor.Value = next;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ServerCallToFloor(int floor)
        {
            TargetFloor.Value = Mathf.Clamp(floor, 0, FloorHeights.Length - 1);
        }

        public bool IsPlayerInsideCab(Vector3 playerPos)
        {
            Vector3 d = playerPos - transform.position;
            return Mathf.Abs(d.x) <= 1.75f && Mathf.Abs(d.z) <= 1.75f && d.y >= -0.4f && d.y <= 2.8f;
        }

        public bool IsPlayerAtLanding(Vector3 playerPos, out int playerFloor)
        {
            playerFloor = NearestFloorIndex(playerPos.y);
            float floorY = FloorHeights[playerFloor];
            if (Mathf.Abs(playerPos.y - floorY) > 1.6f) return false;
            Vector2 horiz = new Vector2(playerPos.x - _basePos.x, playerPos.z - _basePos.z);
            return horiz.magnitude <= 4.2f;
        }
    }
}
