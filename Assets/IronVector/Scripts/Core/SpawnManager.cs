using System.Collections.Generic;
using UnityEngine;

namespace IronVector
{
    /// <summary>Holds team/FFA spawn points and picks the safest spawn farthest from enemies.</summary>
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

        public Pose GetSpawnPoint(int team, bool isFreeForAll = false)
        {
            var candidates = new List<Transform>();
            if (isFreeForAll || team == Teams.None)
            {
                if (alphaSpawns != null) candidates.AddRange(alphaSpawns);
                if (bravoSpawns != null) candidates.AddRange(bravoSpawns);
            }
            else
            {
                Transform[] list = team == Teams.Bravo ? bravoSpawns : alphaSpawns;
                if (list != null) candidates.AddRange(list);
            }

            if (candidates.Count == 0)
                return new Pose(Vector3.up * 2f, Quaternion.identity);

            Transform best = null;
            float bestScore = float.MinValue;

            foreach (Transform sp in candidates)
            {
                if (sp == null) continue;
                float nearestThreat = 200f;
                float crowdPenalty = 0f;

                foreach (PlayerIdentity p in PlayerIdentity.All)
                {
                    if (p == null || p.Health == null || !p.Health.IsAlive) continue;
                    float d = Vector3.Distance(p.transform.position, sp.position);
                    bool enemy = isFreeForAll || team == Teams.None || p.Team.Value != team;
                    if (enemy) nearestThreat = Mathf.Min(nearestThreat, d);
                    else if (d < 2.2f) crowdPenalty += 50f;
                }

                float score = nearestThreat - crowdPenalty + Random.Range(0f, 9f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = sp;
                }
            }

            if (best == null) best = candidates[0];
            return new Pose(best.position + Vector3.up * 0.1f, best.rotation);
        }
    }
}
