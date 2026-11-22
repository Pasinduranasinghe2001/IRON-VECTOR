using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>Server-authoritative health, Juggernaut armor scaling, COD regen, FFA/S&amp;D rules and respawn.</summary>
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        public const float BaseMaxHealth = 100f;

        public float respawnDelay = 3.5f;
        public float regenDelay = 4.2f;
        public float regenPerSecond = 32f;
        public float killHeight = -25f;

        public readonly SyncVar<float> Health = new SyncVar<float>(BaseMaxHealth);
        public readonly SyncVar<bool> Dead = new SyncVar<bool>(false);

        PlayerIdentity _id;
        float _lastDamageTime = -100f;
        float _respawnAt;

        public float LocalRespawnAt { get; private set; }
        public string LastKillerName { get; private set; } = "";

        public bool IsAlive => !Dead.Value;
        public int DamageTeam => _id != null ? _id.Team.Value : Teams.None;

        public float EffectiveMaxHealth =>
            (_id != null && _id.Strategy.Value == (int)TacticalStrategy.Juggernaut) ? 125f : BaseMaxHealth;

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

        public void ServerSyncMaxHealth()
        {
            if (!IsServerInitialized || Dead.Value) return;
            float maxHp = EffectiveMaxHealth;
            if (Health.Value >= BaseMaxHealth && Health.Value < maxHp)
                Health.Value = maxHp;
            else if (Health.Value > maxHp)
                Health.Value = maxHp;
        }

        public void ServerTakeDamage(DamageInfo info)
        {
            if (!IsServerInitialized || Dead.Value) return;

            MatchManager match = MatchManager.Instance;
            if (match != null && !match.DamageAllowed) return;

            bool isFfa = match != null && match.IsFreeForAll;
            if (!isFfa && info.Attacker != null && info.Attacker != _id &&
                info.Attacker.Team.Value != Teams.None && info.Attacker.Team.Value == _id.Team.Value)
                return;

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

            float maxHp = EffectiveMaxHealth;
            float delay = (_id != null && _id.Strategy.Value == (int)TacticalStrategy.Juggernaut) ? regenDelay * 0.75f : regenDelay;
            if (Health.Value < maxHp && Time.time - _lastDamageTime > delay)
                Health.Value = Mathf.Min(maxHp, Health.Value + regenPerSecond * Time.deltaTime);
        }

        public void ServerRespawn()
        {
            if (!IsServerInitialized) return;
            bool isFfa = MatchManager.Instance != null && MatchManager.Instance.IsFreeForAll;
            Pose spawn = SpawnManager.Instance != null
                ? SpawnManager.Instance.GetSpawnPoint(_id.Team.Value, isFfa)
                : new Pose(Vector3.up * 2f, Quaternion.identity);

            _id.VehicleUser.ServerForceExit();
            transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            Health.Value = EffectiveMaxHealth;
            Dead.Value = false;
            _lastDamageTime = -100f;

            _id.Weapons.ServerResetAmmo();
            _id.Grenades.ServerRefill();
            TargetOnRespawn(Owner, spawn.position, spawn.rotation);
        }

        [TargetRpc]
        void TargetOnDamaged(NetworkConnection conn, Vector3 from)
        {
            GameHUD.NotifyDamaged(from);
            if (_id != null && _id.Look != null) _id.Look.AddScreenShake(0.45f);
        }

        [TargetRpc]
        public void TargetHitConfirm(NetworkConnection conn, bool kill, bool headshot) => GameHUD.NotifyHit(kill, headshot);

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
