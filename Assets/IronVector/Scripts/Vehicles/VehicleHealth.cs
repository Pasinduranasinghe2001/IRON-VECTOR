using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>Server-authoritative health and explosive destruction for vehicles.</summary>
    public class VehicleHealth : NetworkBehaviour, IDamageable
    {
        public float maxHealth = 450f;
        public float explosionRadius = 9f;
        public float explosionDamage = 135f;

        public readonly SyncVar<float> Health = new SyncVar<float>(450f);
        public readonly SyncVar<bool> Dead = new SyncVar<bool>(false);

        VehicleController _vc;
        float _nextSmokeTime;

        public bool IsAlive => !Dead.Value;
        public int DamageTeam => _vc != null && _vc.DriverIdentity != null ? _vc.DriverIdentity.Team.Value : Teams.None;

        void Awake()
        {
            _vc = GetComponent<VehicleController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Health.Value = maxHealth;
            Dead.Value = false;
        }

        void Update()
        {
            if (FxUtil.Enabled && !Dead.Value && Health.Value < maxHealth * 0.45f && Time.time >= _nextSmokeTime)
            {
                _nextSmokeTime = Time.time + 0.25f;
                FxUtil.Smoke(transform.position + transform.forward * 1.1f + Vector3.up * 0.8f, 1.3f, 1.1f);
            }
        }

        public void ServerTakeDamage(DamageInfo info)
        {
            if (!IsServerInitialized || Dead.Value) return;

            MatchManager match = MatchManager.Instance;
            if (match != null && !match.DamageAllowed) return;

            // Friendly fire off for driven vehicles.
            PlayerIdentity driver = _vc != null ? _vc.DriverIdentity : null;
            if (driver != null && info.Attacker != null && info.Attacker != driver && info.Attacker.Team.Value == driver.Team.Value)
                return;

            float hp = Mathf.Max(0f, Health.Value - info.Amount);
            Health.Value = hp;

            if (info.Attacker != null && info.Attacker != driver)
                info.Attacker.Health.TargetHitConfirm(info.Attacker.Owner, hp <= 0f, false);

            if (hp <= 0f) ServerDie(info);
        }

        void ServerDie(DamageInfo info)
        {
            Dead.Value = true;

            PlayerIdentity driver = _vc != null ? _vc.DriverIdentity : null;
            if (driver != null && driver.Health != null && driver.Health.IsAlive)
            {
                driver.VehicleUser.ServerForceExit(false);
                driver.Health.ServerTakeDamage(new DamageInfo
                {
                    Amount = 999f,
                    Attacker = info.Attacker,
                    Source = "Vehicle Explosion",
                    Explosive = true,
                    Point = transform.position
                });
            }

            if (_vc != null) _vc.ServerClearDriver();

            Explosions.ServerApply(transform.position, explosionRadius, explosionDamage, info.Attacker, "Vehicle Explosion", this);
            ObserversExplode(transform.position, explosionRadius);
            StartCoroutine(DespawnSoon());
        }

        IEnumerator DespawnSoon()
        {
            yield return new WaitForSeconds(0.25f);
            if (IsServerInitialized && NetworkObject != null && NetworkObject.IsSpawned)
                Despawn();
        }

        [ObserversRpc]
        void ObserversExplode(Vector3 position, float radius)
        {
            FxUtil.Explosion(position, radius);
        }
    }
}
