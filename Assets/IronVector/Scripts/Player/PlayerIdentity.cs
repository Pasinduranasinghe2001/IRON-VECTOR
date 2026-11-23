using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// Hub component on Player: name, team, kills/deaths/score, active TacticalStrategy (4 COD4 perks),
    /// procedural multi-part SoldierRig team uniform styling, and cached references to sibling systems.
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

        static readonly HashSet<string> TrimNames = new HashSet<string>
        {
            "ChestTeamPatch", "TeamArmband_L", "TeamArmband_R", "HelmetTeamStrip"
        };

        static readonly HashSet<string> ClothNames = new HashSet<string>
        {
            "Pelvis", "CombatShirt", "ShoulderPad_L", "ShoulderPad_R",
            "Arm_R", "Arm_L", "UpperArm_R", "Forearm_R", "UpperArm_L", "Forearm_L",
            "Thigh", "CargoPocket", "Shin"
        };

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

            EnsureMultiPartSoldier();

            var bodyRendList = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (Weapons != null && Weapons.weaponHolder != null && r.transform.IsChildOf(Weapons.weaponHolder))
                    continue;
                bodyRendList.Add(r);
            }
            _bodyRenderers = bodyRendList.ToArray();

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

        void EnsureMultiPartSoldier()
        {
            if (Application.isBatchMode) return;
            if (transform.Find("SoldierRig") != null) return;

            Transform oldBody = transform.Find("Body");
            if (oldBody != null)
            {
                var r = oldBody.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }

            Transform headTf = Motor != null && Motor.head != null ? Motor.head : transform.Find("Head");
            if (headTf != null)
            {
                var hr = headTf.GetComponent<Renderer>();
                if (hr != null) hr.enabled = false;
                Transform oldVisor = headTf.Find("Visor");
                if (oldVisor != null)
                {
                    var vr = oldVisor.GetComponent<Renderer>();
                    if (vr != null) vr.enabled = false;
                }
            }

            var refs = SoldierVisualBuilder.Build(transform, headTf, false,
                new Color(0.18f, 0.24f, 0.34f), Teams.TeamColor(Teams.Alpha));
            if (Motor != null && refs.TorsoPivot != null)
                Motor.BindSoldierRig(refs.LeftLegPivot, refs.RightLegPivot, refs.TorsoPivot);
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
            Color trimCol = Teams.TeamColor(Team.Value);
            Color clothCol = Team.Value == Teams.Bravo
                ? new Color(0.46f, 0.36f, 0.25f) // Desert Coyote Tan (Bravo)
                : (Team.Value == Teams.Alpha
                    ? new Color(0.17f, 0.24f, 0.35f) // Urban Spec-Ops Slate Blue (Alpha)
                    : new Color(0.28f, 0.30f, 0.24f)); // Solo Olive Tactical

            foreach (Renderer r in _bodyRenderers)
            {
                if (r == null) continue;
                string gName = r.gameObject.name;
                if (TrimNames.Contains(gName) || gName == "Body")
                    SetRendererColor(r, trimCol);
                else if (ClothNames.Contains(gName))
                    SetRendererColor(r, clothCol);
            }
        }

        static void SetRendererColor(Renderer r, Color c)
        {
            Material m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
        }

        public void RefreshState()
        {
            bool alive = Health == null || Health.IsAlive;
            bool inVehicle = VehicleUser != null && VehicleUser.InVehicle;
            bool active = alive && !inVehicle;

            if (_bodyRenderers != null)
                foreach (Renderer r in _bodyRenderers)
                {
                    if (r == null) continue;
                    string n = r.gameObject.name;
                    if ((n == "Body" || n == "Head" || n == "Visor") && transform.Find("SoldierRig") != null)
                    {
                        r.enabled = false;
                        continue;
                    }
                    r.enabled = active;
                }

            if (_hitboxColliders != null)
                foreach (Collider c in _hitboxColliders)
                    if (c != null) c.enabled = active;

            if (_cc != null) _cc.enabled = active;
            if (Weapons != null) Weapons.SetModelsVisible(active);
        }
    }
}
