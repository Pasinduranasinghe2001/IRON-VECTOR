using UnityEngine;

namespace IronVector
{
    /// <summary>Holds team spawn points and picks the safest one (farthest from enemies).</summary>
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager Instance { get; private set; }

        public Transform[] alphaSpawns = new Transform[0];
        public Transform[] bravoSpawns = new Transform[0];

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Pose GetSpawnPoint(int team)
        {
            Transform[] list = team == Teams.Bravo ? bravoSpawns : alphaSpawns;
            if (list == null || list.Length == 0)
                return new Pose(Vector3.up * 2f, Quaternion.identity);

            Transform best = null;
            float bestScore = float.MinValue;

            foreach (Transform sp in list)
            {
                if (sp == null) continue;
                float nearestEnemy = 200f;
                float crowdPenalty = 0f;

                foreach (PlayerIdentity p in PlayerIdentity.All)
                {
                    if (p == null || p.Health == null || !p.Health.IsAlive) continue;
                    float d = Vector3.Distance(p.transform.position, sp.position);
                    if (p.Team.Value != team) nearestEnemy = Mathf.Min(nearestEnemy, d);
                    else if (d < 2f) crowdPenalty += 50f;
                }

                float score = nearestEnemy - crowdPenalty + Random.Range(0f, 10f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = sp;
                }
            }

            if (best == null) best = list[0];
            return new Pose(best.position + Vector3.up * 0.1f, best.rotation);
        }
    }
}
