using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>Domination capture zone (A, B, C). Server computes presence; clients color visuals.</summary>
    public class CapturePoint : NetworkBehaviour
    {
        public static readonly List<CapturePoint> All = new List<CapturePoint>();

        public float captureRadius = 7.5f;
        public float captureSeconds = 8f;

        [Header("Visuals (optional)")]
        public Renderer ringRenderer;
        public Renderer beaconRenderer;

        /// <summary>Server-only: assigned by NetworkGameServer before Spawn.</summary>
        [System.NonSerialized] public int ServerLabel;

        public readonly SyncVar<int> LabelIndex = new SyncVar<int>(0);
        public readonly SyncVar<int> OwnerTeam = new SyncVar<int>(Teams.None);
        public readonly SyncVar<int> CapturingTeam = new SyncVar<int>(Teams.None);
        public readonly SyncVar<float> Progress = new SyncVar<float>(0f);

        // -1 = Bravo owns fully, 0 = Neutral, +1 = Alpha owns fully.
        float _control;

        public string Letter => LabelIndex.Value >= 0 && LabelIndex.Value < 26
            ? ((char)('A' + LabelIndex.Value)).ToString()
            : "?";

        void Awake()
        {
            OwnerTeam.OnChange += OnVisualStateChanged;
            CapturingTeam.OnChange += OnVisualStateChanged;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!All.Contains(this)) All.Add(this);
            RefreshVisuals();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            All.Remove(this);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            LabelIndex.Value = ServerLabel;
            ServerReset();
        }

        public void ServerReset()
        {
            if (!IsServerInitialized) return;
            _control = 0f;
            OwnerTeam.Value = Teams.None;
            CapturingTeam.Value = Teams.None;
            Progress.Value = 0f;
        }

        void Update()
        {
            if (!IsServerInitialized) return;
            if (MatchManager.Instance != null && !MatchManager.Instance.DamageAllowed) return;

            int alphaCount = 0;
            int bravoCount = 0;

            foreach (PlayerIdentity p in PlayerIdentity.All)
            {
                if (p == null || p.Health == null || !p.Health.IsAlive) continue;
                Vector3 delta = p.transform.position - transform.position;
                if (Mathf.Abs(delta.y) > 2.8f) continue;
                delta.y = 0f;
                if (delta.sqrMagnitude > captureRadius * captureRadius) continue;

                if (p.Team.Value == Teams.Alpha) alphaCount++;
                else if (p.Team.Value == Teams.Bravo) bravoCount++;
            }

            if (alphaCount == bravoCount)
            {
                CapturingTeam.Value = Teams.None;
                return;
            }

            int advantage = alphaCount > bravoCount ? Teams.Alpha : Teams.Bravo;
            int diff = Mathf.Min(3, Mathf.Abs(alphaCount - bravoCount));
            float rate = (1f / Mathf.Max(1f, captureSeconds)) * (1f + (diff - 1) * 0.35f);
            float dir = advantage == Teams.Alpha ? 1f : -1f;

            _control = Mathf.Clamp(_control + dir * rate * Time.deltaTime, -1f, 1f);

            int newOwner = OwnerTeam.Value;
            if (_control >= 0.999f)
            {
                _control = 1f;
                newOwner = Teams.Alpha;
            }
            else if (_control <= -0.999f)
            {
                _control = -1f;
                newOwner = Teams.Bravo;
            }
            else if ((OwnerTeam.Value == Teams.Alpha && _control <= 0f) ||
                     (OwnerTeam.Value == Teams.Bravo && _control >= 0f))
            {
                newOwner = Teams.None;
            }

            OwnerTeam.Value = newOwner;
            float prog = Mathf.Abs(_control);
            Progress.Value = prog;
            CapturingTeam.Value = (newOwner == advantage && prog >= 0.999f) ? Teams.None : advantage;
        }

        void OnVisualStateChanged(int prev, int next, bool asServer) => RefreshVisuals();

        void RefreshVisuals()
        {
            if (Application.isBatchMode) return;
            int colorTeam = OwnerTeam.Value != Teams.None ? OwnerTeam.Value : CapturingTeam.Value;
            Color c = Teams.TeamColor(colorTeam);
            if (ringRenderer != null) ringRenderer.material.color = new Color(c.r, c.g, c.b, 0.45f);
            if (beaconRenderer != null) beaconRenderer.material.color = c;
        }
    }
}
