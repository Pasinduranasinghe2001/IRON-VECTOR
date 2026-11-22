using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Arcade tactical buggy physics: raycast spring suspension, steering, lateral grip,
    /// FishNet ownership transfer on entry, and cleanup if a driver disconnects.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : NetworkBehaviour
    {
        public static readonly List<VehicleController> All = new List<VehicleController>();

        [Header("Info & References")]
        public string vehicleName = "Recon Buggy";
        public Transform seat;
        public Transform[] wheelMeshes = new Transform[4];

        [Header("Suspension")]
        public Vector3[] wheelOffsets =
        {
            new Vector3(-0.95f, 0.45f, 1.35f),
            new Vector3(0.95f, 0.45f, 1.35f),
            new Vector3(-0.95f, 0.45f, -1.35f),
            new Vector3(0.95f, 0.45f, -1.35f)
        };
        public float restLength = 0.65f;
        public float springStrength = 26000f;
        public float damperStrength = 3400f;

        [Header("Driving")]
        public float motorForce = 16500f;
        public float maxSpeed = 28f;
        public float steerTorque = 4200f;
        public float lateralGrip = 7.5f;
        public float brakeDrag = 5f;

        public readonly SyncVar<NetworkObject> Driver = new SyncVar<NetworkObject>();

        [System.NonSerialized] public int SpawnIndex = -1;

        public VehicleHealth Health { get; private set; }
        public bool HasDriver => Driver.Value != null;
        public PlayerIdentity DriverIdentity => Driver.Value != null ? Driver.Value.GetComponent<PlayerIdentity>() : null;
        public float CurrentSpeedKmh => _rb != null ? _rb.linearVelocity.magnitude * 3.6f : 0f;

        Rigidbody _rb;
        Collider[] _ownColliders;
        float _wheelSpinAngle;
        float _steerAngle;
        float _nextRamCheck;
        readonly Collider[] _ramHits = new Collider[16];

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = new Vector3(0f, -0.35f, 0f);
            Health = GetComponent<VehicleHealth>();
            _ownColliders = GetComponentsInChildren<Collider>(true);
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
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (ServerManager != null && ServerManager.Objects != null)
                ServerManager.Objects.OnPreDestroyClientObjects += OnPreDestroyClientObjects;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (ServerManager != null && ServerManager.Objects != null)
                ServerManager.Objects.OnPreDestroyClientObjects -= OnPreDestroyClientObjects;
            if (NetworkGameServer.Instance != null)
                NetworkGameServer.Instance.OnVehicleRemoved(SpawnIndex);
        }

        void OnPreDestroyClientObjects(NetworkConnection conn)
        {
            // Keep the buggy alive in the world if the driver disconnects mid-ride.
            if (conn == Owner) ServerClearDriver();
        }

        public void ServerSetDriver(PlayerIdentity driver)
        {
            if (!IsServerInitialized || driver == null) return;
            Driver.Value = driver.NetworkObject;
            GiveOwnership(driver.Owner);
        }

        public void ServerClearDriver()
        {
            if (!IsServerInitialized) return;
            Driver.Value = null;
            if (Owner.IsValid) RemoveOwnership();
        }

        void Update()
        {
            AnimateWheels();
            if (IsOwner && HasDriver && Time.time >= _nextRamCheck)
            {
                _nextRamCheck = Time.time + 0.08f;
                CheckRoadkill();
            }
        }

        void FixedUpdate()
        {
            if (!IsSpawned || _rb == null) return;

            // Owner drives when occupied; server stabilizes when empty.
            bool simulate = IsController;
            if (_rb.isKinematic == simulate) _rb.isKinematic = !simulate;
            if (!simulate) return;

            float throttle = 0f;
            float steer = 0f;
            bool braking = true;

            if (IsOwner && HasDriver && (Health == null || Health.IsAlive))
            {
                Vector2 move = IVInput.Move;
                throttle = move.y;
                steer = move.x;
                braking = IVInput.Held(IVKey.Jump);
                _steerAngle = Mathf.Lerp(_steerAngle, steer * 28f, Time.fixedDeltaTime * 10f);
            }
            else
            {
                _steerAngle = Mathf.Lerp(_steerAngle, 0f, Time.fixedDeltaTime * 6f);
            }

            int groundedWheels = 0;
            for (int i = 0; i < wheelOffsets.Length; i++)
            {
                Vector3 origin = transform.TransformPoint(wheelOffsets[i]);
                if (TryWheelRaycast(origin, -transform.up, restLength, out RaycastHit hit))
                {
                    groundedWheels++;
                    float compression = 1f - hit.distance / restLength;
                    Vector3 pointVel = _rb.GetPointVelocity(origin);
                    float upVel = Vector3.Dot(pointVel, transform.up);
                    float force = compression * springStrength - upVel * damperStrength;
                    _rb.AddForceAtPosition(transform.up * Mathf.Max(0f, force), origin, ForceMode.Force);
                }
            }

            if (groundedWheels > 0)
            {
                float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);

                if (Mathf.Abs(throttle) > 0.01f && Mathf.Abs(forwardSpeed) < maxSpeed && !braking)
                    _rb.AddForce(transform.forward * (throttle * motorForce), ForceMode.Force);

                float turnScale = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 3f) * (forwardSpeed >= -0.5f ? 1f : -1f);
                _rb.AddTorque(transform.up * (steer * steerTorque * turnScale), ForceMode.Force);

                // Lateral tire grip to reduce ice-skating.
                float sideSpeed = Vector3.Dot(_rb.linearVelocity, transform.right);
                _rb.AddForce(-transform.right * (sideSpeed * lateralGrip * _rb.mass), ForceMode.Force);

                // Handbrake or coasting drag when empty.
                if (braking || Mathf.Abs(throttle) < 0.01f)
                {
                    float drag = braking ? brakeDrag * 2.5f : brakeDrag * 0.45f;
                    Vector3 horiz = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                    _rb.AddForce(-horiz * drag * _rb.mass, ForceMode.Force);
                }
            }

            // Gentle self-righting if flipped upside down while a driver tries to move.
            if (Vector3.Dot(transform.up, Vector3.up) < 0.25f && IsOwner && HasDriver && (Mathf.Abs(throttle) > 0.1f || Mathf.Abs(steer) > 0.1f))
                _rb.AddTorque(Vector3.Cross(transform.up, Vector3.up) * (_rb.mass * 22f), ForceMode.Force);
        }

        void AnimateWheels()
        {
            if (wheelMeshes == null) return;
            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            float sign = _rb != null && Vector3.Dot(_rb.linearVelocity, transform.forward) < -0.2f ? -1f : 1f;
            _wheelSpinAngle += speed * sign * 180f * Time.deltaTime;
            for (int i = 0; i < wheelMeshes.Length; i++)
            {
                if (wheelMeshes[i] == null) continue;
                float steerYaw = i < 2 ? _steerAngle : 0f;
                wheelMeshes[i].localRotation = Quaternion.Euler(0f, steerYaw, 0f) * Quaternion.Euler(_wheelSpinAngle, 0f, 0f);
            }
        }

        void CheckRoadkill()
        {
            float speed = _rb.linearVelocity.magnitude;
            if (speed < 8f) return;

            Vector3 probe = transform.position + _rb.linearVelocity.normalized * 1.9f + Vector3.up * 0.9f;
            int count = Physics.OverlapSphereNonAlloc(probe, 1.5f, _ramHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _ramHits[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                PlayerIdentity victim = c.GetComponentInParent<PlayerIdentity>();
                if (victim != null && victim.Health.IsAlive && victim != DriverIdentity)
                {
                    ServerReportImpact(victim.NetworkObject, speed);
                    return;
                }
            }
        }

        [ServerRpc]
        void ServerReportImpact(NetworkObject victimNob, float reportedSpeed)
        {
            if (victimNob == null || !HasDriver || Health == null || !Health.IsAlive) return;
            PlayerIdentity victim = victimNob.GetComponent<PlayerIdentity>();
            PlayerIdentity driver = DriverIdentity;
            if (victim == null || driver == null || !victim.Health.IsAlive) return;
            if (Vector3.Distance(transform.position, victim.transform.position) > 5.5f) return;

            float dmg = Mathf.Clamp(reportedSpeed * 7.5f, 35f, 150f);
            victim.Health.ServerTakeDamage(new DamageInfo
            {
                Amount = dmg,
                Attacker = driver,
                Source = vehicleName,
                Point = victim.transform.position + Vector3.up * 0.9f
            });
        }

        bool TryWheelRaycast(Vector3 origin, Vector3 dir, float distance, out RaycastHit bestHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, distance, ~0, QueryTriggerInteraction.Ignore);
            bestHit = default;
            float best = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                if (h.collider == null || IsOwnCollider(h.collider)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    bestHit = h;
                }
            }
            return best < float.MaxValue;
        }

        bool IsOwnCollider(Collider c)
        {
            if (_ownColliders == null) return c.transform.IsChildOf(transform);
            for (int i = 0; i < _ownColliders.Length; i++)
                if (_ownColliders[i] == c) return true;
            return false;
        }

        /// <summary>Finds an unobstructed exit position next to the buggy.</summary>
        public Vector3 FindExitPosition()
        {
            Vector3[] candidates =
            {
                transform.position - transform.right * 2.4f + Vector3.up * 0.3f,
                transform.position + transform.right * 2.4f + Vector3.up * 0.3f,
                transform.position - transform.forward * 3.1f + Vector3.up * 0.3f,
                transform.position + Vector3.up * 2.6f
            };
            foreach (Vector3 pos in candidates)
            {
                if (!Physics.CheckCapsule(pos + Vector3.up * 0.4f, pos + Vector3.up * 1.55f, 0.32f, ~0, QueryTriggerInteraction.Ignore))
                    return pos;
            }
            return transform.position + Vector3.up * 2.6f;
        }
    }
}
