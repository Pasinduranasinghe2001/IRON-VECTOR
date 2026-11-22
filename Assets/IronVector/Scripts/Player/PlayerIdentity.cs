using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// Hub component on Player: name, team, kills/deaths/score, active TacticalStrategy (4 COD4 perks),
    /// and cached references to every sibling player system.
    /// </summary>
    public class PlayerIdentity : NetworkBehaviour
    {
        public static readonly List<PlayerIdentity> All = new List<PlayerIdentity>();
        public static PlayerIdentity Local { get; private set; }

        public readonly SyncVar<string> PlayerName = new SyncVar<string>("Operator");
        public readonly SyncVar<int> Team = new SyncVar<int>(Teams.None);
        public readonly SyncVar<int> Strategy = new SyncVar<int>(0);
        public readonly SyncVar<int> Kills = new SyncVar<int>(0);
        public readonly SyncVar<int> Deaths = new SyncVar<int>(0);
        public readonly SyncVar<int> Score = new SyncVar<int>(0);

        [System.NonSerialized] public int PendingTeam = Teams.Alpha;

        public PlayerHealth Health { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerLook Look { get; private set; }
        public WeaponController Weapons { get; private set; }
        public GrenadeThrower Grenades { get; private set; }
        public PlayerVehicleInteraction VehicleUser { get; private set; }
        public PlayerBombInteraction BombUser { get; private set; }
        public PlayerElevatorInteraction ElevatorUser { get; private set; }

        CharacterController _cc;
        Renderer[] _bodyRenderers;
        Collider[] _hitboxColliders;

        void Awake()
        {
            Health = GetComponent<PlayerHealth>();
            Motor = GetComponent<PlayerMotor>();
            Look = GetComponent<PlayerLook>();
            Weapons = GetComponent<WeaponController>();
            Grenades = GetComponent<GrenadeThrower>();
            VehicleUser = GetComponent<PlayerVehicleInteraction>();
            BombUser = GetComponent<PlayerBombInteraction>();
            ElevatorUser = GetComponent<PlayerElevatorInteraction>();
            _cc = GetComponent<CharacterController>();

            _bodyRenderers = GetComponentsInChildren<Renderer>(true);
            Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
            var cols = new List<Collider>();
            foreach (Hitbox hb in hitboxes)
            {
                Collider c = hb.GetComponent<Collider>();
                if (c != null) cols.Add(c);
            }
            _hitboxColliders = cols.ToArray();

            Team.OnChange += OnTeamChanged;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!All.Contains(this)) All.Add(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            All.Remove(this);
            if (Local == this) Local = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Team.Value = PendingTeam;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                Local = this;
                ServerSetProfile(LocalProfile.PlayerName, LocalProfile.Strategy);
                foreach (Renderer r in _bodyRenderers)
                    if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            ApplyTeamColor();
            RefreshState();
        }

        public void ClientApplyStrategy(int strategy)
        {
            if (IsOwner) ServerSetProfile(LocalProfile.PlayerName, strategy);
        }

        [ServerRpc]
        void ServerSetProfile(string requestedName, int strategy)
        {
            string n = string.IsNullOrWhiteSpace(requestedName) ? "Operator" : requestedName.Trim();
            if (n.Length > 16) n = n.Substring(0, 16);
            PlayerName.Value = n;
            Strategy.Value = Mathf.Clamp(strategy, 0, 3);
            if (Health != null && Health.IsAlive)
                Health.ServerSyncMaxHealth();
        }

        void OnTeamChanged(int prev, int next, bool asServer) => ApplyTeamColor();

        void ApplyTeamColor()
        {
            if (Application.isBatchMode || _bodyRenderers == null) return;
            Color c = Teams.TeamColor(Team.Value);
            foreach (Renderer r in _bodyRenderers)
                if (r != null && r.gameObject.name == "Body") r.material.color = c;
        }

        public void RefreshState()
        {
            bool alive = Health == null || Health.IsAlive;
            bool inVehicle = VehicleUser != null && VehicleUser.InVehicle;
            bool active = alive && !inVehicle;

            if (_bodyRenderers != null)
                foreach (Renderer r in _bodyRenderers)
                    if (r != null) r.enabled = active;

            if (_hitboxColliders != null)
                foreach (Collider c in _hitboxColliders)
                    if (c != null) c.enabled = active;

            if (_cc != null) _cc.enabled = active;
            if (Weapons != null) Weapons.SetModelsVisible(active);
        }
    }
}
