using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace IronVector
{
    /// <summary>Server-side damage description.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public PlayerIdentity Attacker;
        public string Source;
        public bool Headshot;
        public bool Explosive;
        public Vector3 Point;
    }

    /// <summary>Anything that can take damage (players, vehicles, later: deployables).</summary>
    public interface IDamageable
    {
        int DamageTeam { get; }
        bool IsAlive { get; }
        void ServerTakeDamage(DamageInfo info);
    }

    /// <summary>Client -> server report of a pellet/bullet that hit a damageable target.</summary>
    public struct HitReport
    {
        public NetworkObject Target;
        public Vector3 Point;
        public bool Headshot;
    }

    public static class Explosions
    {
        static readonly Collider[] Buffer = new Collider[128];

        /// <summary>Applies radial damage with line-of-sight checks. Server only.</summary>
        public static void ServerApply(Vector3 center, float radius, float maxDamage, PlayerIdentity attacker, string source, IDamageable ignore = null)
        {
            Vector3 origin = center + Vector3.up * 0.3f;
            int count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, ~0, QueryTriggerInteraction.Ignore);
            var processed = new HashSet<IDamageable>();

            for (int i = 0; i < count; i++)
            {
                Collider col = Buffer[i];
                if (col == null) continue;

                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target == null || target == ignore || !target.IsAlive || !processed.Add(target))
                    continue;

                Vector3 point = col.ClosestPoint(origin);
                float distance = Vector3.Distance(origin, point);
                if (distance > radius) continue;

                // Line of sight: walls block explosions.
                Vector3 toTarget = point - origin;
                if (toTarget.sqrMagnitude > 0.01f &&
                    Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude - 0.05f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<IDamageable>() != target)
                        continue;
                }

                float falloff = 1f - Mathf.Clamp01(distance / radius);
                target.ServerTakeDamage(new DamageInfo
                {
                    Amount = maxDamage * falloff,
                    Attacker = attacker,
                    Source = source,
                    Explosive = true,
                    Point = point
                });
            }
        }
    }
}
