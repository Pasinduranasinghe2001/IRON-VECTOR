using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>Search & Destroy Bomb Site (A or B). Attackers plant with F; Defenders defuse with F.</summary>
    public class BombSite : NetworkBehaviour
    {
        public static readonly List<BombSite> All = new List<BombSite>();

        public float siteRadius = 5.5f;
        public Renderer ringRenderer;
        public Renderer crateRenderer;

        [System.NonSerialized] public int ServerSiteIndex;

        public readonly SyncVar<int> SiteIndex = new SyncVar<int>(0);
        public readonly SyncVar<bool> IsPlantedHere = new SyncVar<bool>(false);

        public string Letter => SiteIndex.Value == 0 ? "A" : "B";

        void Awake()
        {
            IsPlantedHere.OnChange += OnPlantedChanged;
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
            SiteIndex.Value = ServerSiteIndex;
            IsPlantedHere.Value = false;
        }

        void OnPlantedChanged(bool prev, bool next, bool asServer) => RefreshVisuals();

        void RefreshVisuals()
        {
            if (Application.isBatchMode) return;
            Color c = IsPlantedHere.Value ? new Color(1f, 0.22f, 0.15f) : new Color(0.95f, 0.72f, 0.22f);
            if (ringRenderer != null) ringRenderer.material.color = new Color(c.r, c.g, c.b, 0.48f);
            if (crateRenderer != null) crateRenderer.material.color = c;
        }
    }
}
