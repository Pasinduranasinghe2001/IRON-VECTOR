using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// The "hub" component of a player: name, team, stats and cached references to every
    /// other player component. Keeps a static list of all players for HUD/scoreboard/spawns.
    /// </summary>
    public class PlayerIdentity : NetworkBehaviour
    {
        public static readonly List<PlayerIdentity> All = new List<PlayerIdentity>();
        public static PlayerIdentity Local { get; private set; }

        public readonly SyncVar<string> PlayerName = new SyncVar<string>("Operator");
        public readonly SyncVar<int> Team = new SyncVar<int>(Teams.None);
        public readonly SyncVar<int> Kills = new SyncVar<int>(0);
        public readonly SyncVar<int> Deaths = new SyncVar<int>(0);
        public readonly SyncVar<int> Score = new SyncVar<int>(0);

        /// <summary>Server-only: team chosen by NetworkGameServer before spawning.</summary>
        [System.NonSerialized] public int PendingTeam = Teams.Alpha;

        public PlayerHealth Health { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerLook Look { get; private set; }
        public WeaponController Weapons { get; private set; }
        public GrenadeThrower Grenades { get; private set; }
        public PlayerVehicleInteraction VehicleUser { get; private set; }

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
            _cc = GetComponent<CharacterController>();

            // Weapon models are created at runtime, so at Awake these are only body parts.
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
                ServerSetName(LocalProfile.PlayerName);
                // Don't render our own body to our camera, but keep its shadow.
                foreach (Renderer r in _bodyRenderers)
                    if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            ApplyTeamColor();
            RefreshState();
        }

        [ServerRpc]
        void ServerSetName(string requested)
        {
            string n = string.IsNullOrWhiteSpace(requested) ? "Operator" : requested.Trim();
            if (n.Length > 16) n = n.Substring(0, 16);
            PlayerName.Value = n;
        }

        void OnTeamChanged(int prev, int next, bool asServer) => ApplyTeamColor();

        void ApplyTeamColor()
        {
            if (Application.isBatchMode || _bodyRenderers == null) return;
            Color c = Teams.TeamColor(Team.Value);
            foreach (Renderer r in _bodyRenderers)
                if (r != null && r.gameObject.name == "Body") r.material.color = c;
        }

        /// <summary>Show/hide body, colliders and weapons based on alive + in-vehicle state.</summary>
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
