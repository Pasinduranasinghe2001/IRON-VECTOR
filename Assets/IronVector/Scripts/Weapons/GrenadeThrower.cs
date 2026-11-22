using FishNet.Object;
using UnityEngine;

namespace IronVector
{
    /// <summary>Frag grenade throwing (G key). Server runs damage fuse; clients spawn lightweight visuals.</summary>
    public class GrenadeThrower : NetworkBehaviour
    {
        public int maxGrenades = 2;
        public float throwSpeed = 19f;
        public float upwardBoost = 4.2f;
        public float fuseSeconds = 2.2f;
        public float explosionRadius = 8.5f;
        public float explosionDamage = 130f;
        public float throwCooldown = 0.8f;

        PlayerIdentity _id;
        int _localRemaining;
        int _serverRemaining;
        float _nextThrowTime;

        public int Remaining => _localRemaining;

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerRefill();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) LocalRefill();
        }

        public void ServerRefill() => _serverRemaining = maxGrenades;
        public void LocalRefill() => _localRemaining = maxGrenades;

        void Update()
        {
            if (!IsOwner) return;
            if (_id == null || !_id.Health.IsAlive || _id.VehicleUser.InVehicle) return;

            if (IVInput.Down(IVKey.Grenade) && _localRemaining > 0 && Time.time >= _nextThrowTime)
            {
                _localRemaining--;
                _nextThrowTime = Time.time + throwCooldown;

                Camera cam = _id.Look != null ? _id.Look.playerCamera : null;
                Transform pivot = cam != null ? cam.transform : (_id.Motor != null && _id.Motor.cameraPivot != null ? _id.Motor.cameraPivot : transform);
                Vector3 origin = pivot.position + pivot.forward * 0.7f + Vector3.down * 0.1f;
                Vector3 velocity = pivot.forward * throwSpeed + Vector3.up * upwardBoost;
                ServerThrow(origin, velocity);
            }
        }

        [ServerRpc]
        void ServerThrow(Vector3 origin, Vector3 velocity)
        {
            if (_id == null || !_id.Health.IsAlive || _id.VehicleUser.InVehicle) return;
            if (_serverRemaining <= 0) return;
            if (Vector3.Distance(origin, transform.position + Vector3.up * 1.4f) > 4f) return;

            _serverRemaining--;
            Vector3 clampedVel = Vector3.ClampMagnitude(velocity, 30f);

            // Server simulates invisible physics body so Host doesn't render two grenades.
            var go = new GameObject("FragGrenade_Server");
            go.transform.position = origin;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.14f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = clampedVel;
            foreach (Collider own in GetComponentsInChildren<Collider>(true))
                if (own != null && own.enabled) Physics.IgnoreCollision(col, own, true);

            var sim = go.AddComponent<FragGrenadeServer>();
            sim.Init(_id, fuseSeconds, explosionRadius, explosionDamage, pos => ObserversExplode(pos, explosionRadius));

            ObserversSpawnVisual(origin, clampedVel);
        }

        [ObserversRpc]
        void ObserversSpawnVisual(Vector3 origin, Vector3 velocity)
        {
            if (!FxUtil.Enabled) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FragGrenade_Visual";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.22f;
            var col = go.GetComponent<Collider>();
            col.isTrigger = false;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = FxUtil.ColorMaterial(new Color(0.24f, 0.30f, 0.18f));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = velocity;
            foreach (Collider own in GetComponentsInChildren<Collider>(true))
                if (own != null && own.enabled) Physics.IgnoreCollision(col, own, true);
            Destroy(go, fuseSeconds + 0.05f);
        }

        [ObserversRpc]
        void ObserversExplode(Vector3 position, float radius)
        {
            FxUtil.Explosion(position, radius);
        }
    }

    internal class FragGrenadeServer : MonoBehaviour
    {
        PlayerIdentity _attacker;
        float _explodeAt;
        float _radius;
        float _maxDamage;
        System.Action<Vector3> _onExplode;
        bool _detonated;

        public void Init(PlayerIdentity attacker, float fuse, float radius, float maxDamage, System.Action<Vector3> onExplode)
        {
            _attacker = attacker;
            _explodeAt = Time.time + fuse;
            _radius = radius;
            _maxDamage = maxDamage;
            _onExplode = onExplode;
        }

        void Update()
        {
            if (_detonated || Time.time < _explodeAt) return;
            _detonated = true;
            Vector3 pos = transform.position;
            Explosions.ServerApply(pos, _radius, _maxDamage, _attacker, "Frag Grenade");
            _onExplode?.Invoke(pos);
            Destroy(gameObject);
        }
    }
}
