using FishNet.Object;
using UnityEngine;

namespace IronVector
{
    /// <summary>Handles holding F to Plant (Alpha) or Defuse (Bravo) in Search &amp; Destroy.</summary>
    public class PlayerBombInteraction : NetworkBehaviour
    {
        public float holdDuration = 3.2f;

        PlayerIdentity _id;
        float _holdTimer;
        BombSite _activeSite;

        public BombSite NearbySite { get; private set; }
        public string PromptLabel { get; private set; }
        public float HoldProgress01 => Mathf.Clamp01(_holdTimer / Mathf.Max(0.1f, holdDuration));

        void Awake() => _id = GetComponent<PlayerIdentity>();

        void Update()
        {
            if (!IsOwner) return;

            NearbySite = null;
            PromptLabel = null;

            MatchManager mm = MatchManager.Instance;
            if (mm == null || !mm.IsSearchAndDestroy || mm.State.Value != MatchState.Playing || _id == null || !_id.Health.IsAlive || _id.VehicleUser.InVehicle)
            {
                _holdTimer = 0f;
                return;
            }

            bool isAttacker = _id.Team.Value == Teams.Alpha;
            bool isDefender = _id.Team.Value == Teams.Bravo;

            foreach (BombSite bs in BombSite.All)
            {
                if (bs == null) continue;
                Vector3 delta = transform.position - bs.transform.position;
                if (Mathf.Abs(delta.y) > 2.8f) continue;
                delta.y = 0f;
                if (delta.magnitude > bs.siteRadius) continue;

                if (isAttacker && mm.BombStatus.Value == BombState.Unplanted)
                {
                    NearbySite = bs;
                    PromptLabel = $"Hold [F] to PLANT BOMB at Site {bs.Letter}";
                    break;
                }
                if (isDefender && mm.BombStatus.Value == BombState.Planted && bs.IsPlantedHere.Value)
                {
                    NearbySite = bs;
                    PromptLabel = $"Hold [F] to DEFUSE BOMB at Site {bs.Letter}";
                    break;
                }
            }

            if (NearbySite != null && IVInput.Held(IVKey.Interact))
            {
                if (_activeSite != NearbySite)
                {
                    _activeSite = NearbySite;
                    _holdTimer = 0f;
                }
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= holdDuration)
                {
                    _holdTimer = 0f;
                    if (isAttacker) ServerPlant(NearbySite.SiteIndex.Value);
                    else if (isDefender) ServerDefuse(NearbySite.SiteIndex.Value);
                }
            }
            else
            {
                _holdTimer = 0f;
                _activeSite = null;
            }
        }

        [ServerRpc]
        void ServerPlant(int siteIndex)
        {
            if (_id == null || !_id.Health.IsAlive || _id.Team.Value != Teams.Alpha) return;
            foreach (BombSite bs in BombSite.All)
            {
                if (bs != null && bs.SiteIndex.Value == siteIndex && Mathf.Abs(transform.position.y - bs.transform.position.y) <= 3.5f && Vector3.Distance(transform.position, bs.transform.position) <= bs.siteRadius + 2.0f)
                {
                    MatchManager.Instance?.ServerBombPlanted(siteIndex, _id);
                    return;
                }
            }
        }

        [ServerRpc]
        void ServerDefuse(int siteIndex)
        {
            if (_id == null || !_id.Health.IsAlive || _id.Team.Value != Teams.Bravo) return;
            foreach (BombSite bs in BombSite.All)
            {
                if (bs != null && bs.SiteIndex.Value == siteIndex && bs.IsPlantedHere.Value && Mathf.Abs(transform.position.y - bs.transform.position.y) <= 3.5f && Vector3.Distance(transform.position, bs.transform.position) <= bs.siteRadius + 2.0f)
                {
                    MatchManager.Instance?.ServerBombDefused(_id);
                    return;
                }
            }
        }
    }
}
