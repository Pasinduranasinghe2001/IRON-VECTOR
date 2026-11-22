using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>Server-authoritative health, death, COD-style regen and respawn.</summary>
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        public const float MaxHealth = 100f;

        public float respawnDelay = 4f;
        public float regenDelay = 4.5f;
        public float regenPerSecond = 30f;
        public float killHeight = -25f;

        public readonly SyncVar<float> Health = new SyncVar<float>(MaxHealth);
        public readonly SyncVar<bool> Dead = new SyncVar<bool>(false);

        PlayerIdentity _id;
        float _lastDamageTime = -100f;
        float _respawnAt;

        /// <summary>Owner-side: when we will respawn (for the death screen).</summary>
        public float LocalRespawnAt { get; private set; }
        public string LastKillerName { get; private set; } = "";

        public bool IsAlive => !Dead.Value;
        public int DamageTeam => _id != null ? _id.Team.Value : Teams.None;

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
            Dead.OnChange += OnDeadChanged;
        }

        void OnDeadChanged(bool prev, bool next, bool asServer)
        {
            if (next) LocalRespawnAt = Time.time + respawnDelay;
            if (_id != null) _id.RefreshState();
        }

        // ---------------- Server ----------------

        public void ServerTakeDamage(DamageInfo info)
        {
            if (!IsServerInitialized || Dead.Value) return;

            MatchManager match = MatchManager.Instance;
            if (match != null && !match.DamageAllowed) return;

            // Friendly fire off (self-damage allowed, e.g. own grenade).
            if (info.Attacker != null && info.Attacker != _id && info.Attacker.Team.Value == _id.Team.Value) return;

            float hp = Mathf.Max(0f, Health.Value - info.Amount);
            Health.Value = hp;
            _lastDamageTime = Time.time;

            Vector3 from = info.Attacker != null ? info.Attacker.transform.position : info.Point;
            TargetOnDamaged(Owner, from);

            if (info.Attacker != null && info.Attacker != _id)
                info.Attacker.Health.TargetHitConfirm(info.Attacker.Owner, hp <= 0f, info.Headshot);

            if (hp <= 0f) ServerDie(info);
        }

        void ServerDie(DamageInfo info)
        {
            Dead.Value = true;
            _respawnAt = Time.time + respawnDelay;
            _id.Deaths.Value++;

            PlayerIdentity killer = info.Attacker;
            if (killer != null && killer != _id)
            {
                killer.Kills.Value++;
                killer.Score.Value += info.Headshot ? 125 : 100;
            }

            _id.VehicleUser.ServerForceExit();

            if (MatchManager.Instance != null)
                MatchManager.Instance.ServerOnKill(killer, _id, string.IsNullOrEmpty(info.Source) ? "?" : info.Source, info.Headshot);

            TargetOnKilled(Owner, killer != null && killer != _id ? killer.PlayerName.Value : "");
        }

        void Update()
        {
            if (!IsServerInitialized) return;

            if (Dead.Value)
            {
                bool allowed = MatchManager.Instance == null || MatchManager.Instance.RespawnAllowed;
                if (allowed && Time.time >= _respawnAt) ServerRespawn();
                return;
            }

            if (transform.position.y < killHeight)
            {
                ServerTakeDamage(new DamageInfo { Amount = 999f, Source = "Fell", Point = transform.position });
                return;
            }

            if (Health.Value < MaxHealth && Time.time - _lastDamageTime > regenDelay)
                Health.Value = Mathf.Min(MaxHealth, Health.Value + regenPerSecond * Time.deltaTime);
        }

        public void ServerRespawn()
        {
            if (!IsServerInitialized) return;
            Pose spawn = SpawnManager.Instance != null
                ? SpawnManager.Instance.GetSpawnPoint(_id.Team.Value)
                : new Pose(Vector3.up * 2f, Quaternion.identity);

            _id.VehicleUser.ServerForceExit();
            transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            Health.Value = MaxHealth;
            Dead.Value = false;
            _lastDamageTime = -100f;

            _id.Weapons.ServerResetAmmo();
            _id.Grenades.ServerRefill();
            TargetOnRespawn(Owner, spawn.position, spawn.rotation);
        }

        // ---------------- Owner-targeted RPCs ----------------

        [TargetRpc]
        void TargetOnDamaged(NetworkConnection conn, Vector3 from)
        {
            GameHUD.NotifyDamaged(from);
        }

        [TargetRpc]
        public void TargetHitConfirm(NetworkConnection conn, bool kill, bool headshot)
        {
            GameHUD.NotifyHit(kill, headshot);
        }

        [TargetRpc]
        void TargetOnKilled(NetworkConnection conn, string killerName)
        {
            LastKillerName = killerName;
            LocalRespawnAt = Time.time + respawnDelay;
        }

        [TargetRpc]
        void TargetOnRespawn(NetworkConnection conn, Vector3 position, Quaternion rotation)
        {
            _id.Motor.Teleport(position, rotation);
            _id.Look.ResetView(rotation.eulerAngles.y);
            _id.Weapons.OnLocalRespawn();
            _id.Grenades.LocalRefill();
        }
    }
}
