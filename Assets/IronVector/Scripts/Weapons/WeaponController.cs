using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// Handles weapon switching (1/2/scroll), ADS, hip/ADS spread, recoil, reload,
    /// raycast hit detection (with head Hitbox check) and procedural 3D weapon viewmodels.
    /// </summary>
    public class WeaponController : NetworkBehaviour
    {
        [Header("References")]
        public WeaponDatabase database;
        public Transform weaponHolder;

        public readonly SyncVar<int> PrimaryIndex = new SyncVar<int>(0);
        public readonly SyncVar<int> ActiveSlot = new SyncVar<int>(0);

        PlayerIdentity _id;
        readonly int[] _magAmmo = new int[2];
        readonly int[] _reserveAmmo = new int[2];
        readonly float[] _serverNextShotTime = new float[2];

        float _nextFireTime;
        bool _reloading;
        float _reloadStart;
        float _reloadDuration;
        int _reloadSlot;
        float _ads;
        bool _modelsVisible = true;
        GameObject _currentModel;
        Transform _muzzlePoint;
        Vector3 _hipLocalPos = new Vector3(0.24f, -0.22f, 0.45f);
        Vector3 _adsLocalPos = new Vector3(0f, -0.145f, 0.36f);
        float _recoilKickZ;

        readonly RaycastHit[] _rayBuffer = new RaycastHit[32];

        public float AdsAmount => _ads;
        public float LastShotTime { get; private set; } = -100f;
        public float CurrentSpreadDegrees { get; private set; } = 1.5f;
        public bool IsReloading => _reloading;
        public float ReloadProgress01 => _reloading ? Mathf.Clamp01((Time.time - _reloadStart) / Mathf.Max(0.01f, _reloadDuration)) : 0f;
        public int CurrentAmmo => _magAmmo[Mathf.Clamp(ActiveSlot.Value, 0, 1)];
        public int CurrentReserve => _reserveAmmo[Mathf.Clamp(ActiveSlot.Value, 0, 1)];
        public bool IsSniperScope => CurrentWeapon != null && CurrentWeapon.category == WeaponCategory.Sniper && _ads > 0.72f;

        public WeaponData CurrentWeapon
        {
            get
            {
                WeaponDatabase db = ActiveDb;
                if (db == null || db.Count == 0) return null;
                int idx = ActiveSlot.Value == 1 ? db.sidearmIndex : db.ClampPrimaryIndex(PrimaryIndex.Value);
                return db.Get(idx);
            }
        }

        WeaponDatabase ActiveDb => database != null && database.Count > 0 ? database : WeaponDatabase.RuntimeFallback();

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
            if (weaponHolder != null) _hipLocalPos = weaponHolder.localPosition;
            PrimaryIndex.OnChange += OnWeaponChanged;
            ActiveSlot.OnChange += OnWeaponChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerResetAmmo();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                ServerSetLoadout(LocalProfile.PrimaryWeapon);
                ResetLocalAmmo();
            }
            RebuildWeaponModel();
        }

        void OnWeaponChanged(int prev, int next, bool asServer)
        {
            if (IsOwner) _reloading = false;
            RebuildWeaponModel();
        }

        [ServerRpc]
        void ServerSetLoadout(int primaryIdx)
        {
            PrimaryIndex.Value = ActiveDb.ClampPrimaryIndex(primaryIdx);
            ServerResetAmmo();
        }

        [ServerRpc]
        void ServerSelectSlot(int slot)
        {
            int clamped = Mathf.Clamp(slot, 0, 1);
            if (ActiveSlot.Value != clamped) ActiveSlot.Value = clamped;
        }

        public void ServerResetAmmo()
        {
            // Light server tracking alongside rate limiting.
            _serverNextShotTime[0] = 0f;
            _serverNextShotTime[1] = 0f;
        }

        public void OnLocalRespawn()
        {
            _reloading = false;
            _ads = 0f;
            ResetLocalAmmo();
            if (_id != null)
            {
                if (_id.Motor != null) _id.Motor.AimingDown = false;
                if (_id.Look != null) _id.Look.FovMultiplier = 1f;
            }
        }

        void ResetLocalAmmo()
        {
            WeaponData primary = GetWeaponForSlot(0);
            WeaponData sidearm = GetWeaponForSlot(1);
            if (primary != null) { _magAmmo[0] = primary.magazineSize; _reserveAmmo[0] = primary.reserveAmmo; }
            if (sidearm != null) { _magAmmo[1] = sidearm.magazineSize; _reserveAmmo[1] = sidearm.reserveAmmo; }
        }

        WeaponData GetWeaponForSlot(int slot)
        {
            WeaponDatabase db = ActiveDb;
            int idx = slot == 1 ? db.sidearmIndex : db.ClampPrimaryIndex(PrimaryIndex.Value);
            return db.Get(idx);
        }

        public void SetModelsVisible(bool visible)
        {
            _modelsVisible = visible;
            if (weaponHolder != null) weaponHolder.gameObject.SetActive(visible);
        }

        void Update()
        {
            if (!IsOwner) return;

            bool blocked = _id == null || !_id.Health.IsAlive || _id.VehicleUser.InVehicle;
            if (blocked)
            {
                _ads = 0f;
                _reloading = false;
                if (_id != null)
                {
                    if (_id.Motor != null) _id.Motor.AimingDown = false;
                    if (_id.Look != null) _id.Look.FovMultiplier = 1f;
                }
                return;
            }

            WeaponData w = CurrentWeapon;
            if (w == null) return;

            HandleSwitching();

            // Reload progression.
            if (_reloading)
            {
                if (ActiveSlot.Value != _reloadSlot)
                {
                    _reloading = false;
                }
                else if (Time.time - _reloadStart >= _reloadDuration)
                {
                    FinishReload(_reloadSlot, GetWeaponForSlot(_reloadSlot));
                }
            }
            else if (IVInput.Down(IVKey.Reload))
            {
                TryStartReload(w);
            }

            // ADS smoothing.
            bool wantsAds = IVInput.Held(IVKey.Aim) && !_reloading;
            _ads = Mathf.MoveTowards(_ads, wantsAds ? 1f : 0f, Time.deltaTime * w.adsSpeed);
            if (_id.Motor != null) _id.Motor.AimingDown = _ads > 0.25f;
            if (_id.Look != null) _id.Look.FovMultiplier = Mathf.Lerp(1f, w.adsFovMultiplier, _ads);

            float movePenalty = _id.Motor != null ? _id.Motor.Speed01 * 1.4f : 0f;
            float crouchBonus = _id.Motor != null && _id.Motor.IsCrouching ? 0.72f : 1f;
            CurrentSpreadDegrees = Mathf.Lerp(w.hipSpread, w.adsSpread, _ads) * crouchBonus + movePenalty * (1f - _ads * 0.7f);

            UpdateHolderPose(w);

            // Firing.
            bool trigger = w.automatic ? IVInput.Held(IVKey.Fire) : IVInput.Down(IVKey.Fire);
            if (trigger && !_reloading && Time.time >= _nextFireTime)
            {
                int slot = ActiveSlot.Value;
                if (_magAmmo[slot] <= 0)
                {
                    if (IVInput.Down(IVKey.Fire)) FxUtil.PlaySound2D(FxUtil.ClickClip, 0.5f, 1.1f);
                    TryStartReload(w);
                }
                else
                {
                    FireOwner(w, slot);
                }
            }
        }

        void HandleSwitching()
        {
            if (IVInput.Down(IVKey.Weapon1) && ActiveSlot.Value != 0) ServerSelectSlot(0);
            else if (IVInput.Down(IVKey.Weapon2) && ActiveSlot.Value != 1) ServerSelectSlot(1);
            else if (Mathf.Abs(IVInput.Scroll) > 0.01f) ServerSelectSlot(ActiveSlot.Value == 0 ? 1 : 0);
        }

        void TryStartReload(WeaponData w)
        {
            int slot = ActiveSlot.Value;
            if (_reloading || _magAmmo[slot] >= w.magazineSize || _reserveAmmo[slot] <= 0) return;
            _reloading = true;
            _reloadSlot = slot;
            _reloadStart = Time.time;
            _reloadDuration = w.reloadTime;
            FxUtil.PlaySound2D(FxUtil.ReloadClip, 0.7f, 1.0f);
        }

        void FinishReload(int slot, WeaponData w)
        {
            _reloading = false;
            if (w == null) return;
            int needed = w.magazineSize - _magAmmo[slot];
            int take = Mathf.Min(needed, _reserveAmmo[slot]);
            _magAmmo[slot] += take;
            _reserveAmmo[slot] -= take;
        }

        void UpdateHolderPose(WeaponData w)
        {
            if (weaponHolder == null) return;
            _recoilKickZ = Mathf.Lerp(_recoilKickZ, 0f, Time.deltaTime * 14f);
            float moveBob = (_id.Motor != null && _id.Motor.IsGrounded) ? _id.Motor.Speed01 * 0.015f : 0f;
            float bobX = Mathf.Sin(Time.time * 9f) * moveBob * (1f - _ads * 0.8f);
            float bobY = Mathf.Abs(Mathf.Cos(Time.time * 9f)) * moveBob * (1f - _ads * 0.8f);
            Vector3 basePos = Vector3.Lerp(_hipLocalPos, _adsLocalPos, _ads);
            Vector3 reloadOffset = _reloading ? new Vector3(0f, -0.14f, 0f) : Vector3.zero;
            weaponHolder.localPosition = basePos + reloadOffset + new Vector3(bobX, -bobY, -_recoilKickZ);

            // Hide own gun geometry cleanly while looking through a sniper scope.
            if (w.category == WeaponCategory.Sniper && _currentModel != null)
            {
                bool showGun = _modelsVisible && !IsSniperScope;
                if (_currentModel.activeSelf != showGun) _currentModel.SetActive(showGun);
            }
        }

        void FireOwner(WeaponData w, int slot)
        {
            _magAmmo[slot]--;
            _nextFireTime = Time.time + 60f / Mathf.Max(1f, w.fireRateRPM);
            LastShotTime = Time.time;
            _recoilKickZ = Mathf.Min(0.08f, _recoilKickZ + 0.035f);

            Camera cam = _id.Look != null ? _id.Look.playerCamera : null;
            Transform aimTf = cam != null ? cam.transform : (_id.Motor != null && _id.Motor.cameraPivot != null ? _id.Motor.cameraPivot : transform);
            Vector3 origin = aimTf.position;
            Vector3 muzzle = _muzzlePoint != null ? _muzzlePoint.position : origin + aimTf.forward * 0.5f;

            int pellets = Mathf.Max(1, w.pellets);
            var endPoints = new Vector3[pellets];
            var hitFlesh = new bool[pellets];
            var reports = new List<HitReport>();

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = ApplySpread(aimTf.forward, CurrentSpreadDegrees);
                CastBullet(origin, dir, w.range, out Vector3 endPt, out Vector3 normal, out bool flesh, out bool headshot, out NetworkObject nob);
                endPoints[i] = endPt;
                hitFlesh[i] = flesh;

                FxUtil.Tracer(muzzle, endPt, w.tracerColor);
                if (nob != null || (endPt - origin).sqrMagnitude < (w.range * w.range * 0.98f))
                    FxUtil.Impact(endPt, normal, flesh);

                if (nob != null)
                {
                    reports.Add(new HitReport
                    {
                        Target = nob,
                        Point = endPt,
                        Headshot = headshot
                    });
                }
            }

            FxUtil.MuzzleFlash(muzzle, aimTf.forward);
            FxUtil.PlaySound2D(FxUtil.ShotClip, 0.85f, w.shotPitch * UnityEngine.Random.Range(0.96f, 1.04f));

            if (_id.Look != null)
            {
                float hRecoil = UnityEngine.Random.Range(-w.recoilHorizontal, w.recoilHorizontal);
                float adsReduction = Mathf.Lerp(1f, 0.72f, _ads);
                _id.Look.AddRecoil(w.recoilVertical * adsReduction, hRecoil * adsReduction);
            }

            ServerFire(slot, origin, muzzle, endPoints, hitFlesh, reports.ToArray());

            if (_magAmmo[slot] <= 0 && _reserveAmmo[slot] > 0)
                TryStartReload(w);
        }

        void CastBullet(Vector3 origin, Vector3 dir, float range, out Vector3 hitPoint, out Vector3 hitNormal, out bool hitFlesh, out bool headshot, out NetworkObject targetNob)
        {
            hitPoint = origin + dir * range;
            hitNormal = -dir;
            hitFlesh = false;
            headshot = false;
            targetNob = null;

            int count = Physics.RaycastNonAlloc(origin, dir, _rayBuffer, range, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0) return;

            Array.Sort(_rayBuffer, 0, count, DistanceComparer.Instance);

            IDamageable firstDamageable = null;
            float firstDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _rayBuffer[i];
                Collider col = h.collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;

                // Stop searching deeper if we already entered a player capsule and moved past head depth.
                if (firstDamageable != null && h.distance > firstDist + 0.65f) break;

                Hitbox hb = col.GetComponent<Hitbox>();
                if (hb != null && hb.isHead)
                {
                    IDamageable hbDmg = col.GetComponentInParent<IDamageable>();
                    if (hbDmg != null && hbDmg.IsAlive && (firstDamageable == null || firstDamageable == hbDmg))
                    {
                        targetNob = col.GetComponentInParent<NetworkObject>();
                        hitPoint = h.point;
                        hitNormal = h.normal;
                        hitFlesh = true;
                        headshot = true;
                        return;
                    }
                    continue;
                }

                if (col.isTrigger) continue;

                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    if (firstDamageable == null)
                    {
                        firstDamageable = dmg;
                        firstDist = h.distance;
                        targetNob = col.GetComponentInParent<NetworkObject>();
                        hitPoint = h.point;
                        hitNormal = h.normal;
                        hitFlesh = col.GetComponentInParent<PlayerHealth>() != null;
                    }
                    if (col.GetComponentInParent<PlayerHealth>() == null) return; // Vehicle / non-player collider
                    continue;
                }

                // World obstacle stops the bullet.
                if (firstDamageable == null)
                {
                    hitPoint = h.point;
                    hitNormal = h.normal;
                }
                break;
            }
        }

        [ServerRpc]
        void ServerFire(int slot, Vector3 origin, Vector3 muzzle, Vector3[] endPoints, bool[] hitFlesh, HitReport[] reports)
        {
            if (_id == null || !_id.Health.IsAlive || _id.VehicleUser.InVehicle) return;
            if (slot < 0 || slot > 1) return;

            WeaponData w = GetWeaponForSlot(slot);
            if (w == null) return;

            float minInterval = (60f / Mathf.Max(1f, w.fireRateRPM)) * 0.78f;
            if (Time.time < _serverNextShotTime[slot]) return;
            _serverNextShotTime[slot] = Time.time + minInterval;

            // Validate origin near player eyes.
            if (Vector3.Distance(origin, transform.position + Vector3.up * 1.4f) > 3.5f) return;

            if (reports != null)
            {
                int maxReports = Mathf.Max(1, w.pellets);
                int applied = 0;
                foreach (HitReport hr in reports)
                {
                    if (applied >= maxReports) break;
                    if (hr.Target == null) continue;

                    float distToTarget = Vector3.Distance(origin, hr.Target.transform.position);
                    if (distToTarget > w.range + 6f) continue;

                    // Generous anti-cheat sanity check allowing movement latency.
                    if (Vector3.Distance(hr.Point, hr.Target.transform.position + Vector3.up * 0.9f) > 5.0f) continue;

                    IDamageable dmg = hr.Target.GetComponent<IDamageable>();
                    if (dmg == null || !dmg.IsAlive) continue;

                    float amount = w.damage * (hr.Headshot ? w.headshotMultiplier : 1f);
                    dmg.ServerTakeDamage(new DamageInfo
                    {
                        Amount = amount,
                        Attacker = _id,
                        Source = w.weaponName,
                        Headshot = hr.Headshot,
                        Explosive = false,
                        Point = hr.Point
                    });
                    applied++;
                }
            }

            ObserversFireEffects(muzzle, endPoints, hitFlesh, w.tracerColor, w.shotPitch);
        }

        [ObserversRpc(ExcludeOwner = true)]
        void ObserversFireEffects(Vector3 muzzle, Vector3[] endPoints, bool[] hitFlesh, Color tracerColor, float pitch)
        {
            if (!FxUtil.Enabled) return;
            FxUtil.MuzzleFlash(muzzle, endPoints != null && endPoints.Length > 0 ? (endPoints[0] - muzzle).normalized : transform.forward);
            FxUtil.PlaySound(FxUtil.ShotClip, muzzle, 0.75f, 95f, pitch);

            if (endPoints == null) return;
            for (int i = 0; i < endPoints.Length; i++)
            {
                Vector3 to = endPoints[i];
                FxUtil.Tracer(muzzle, to, tracerColor);
                bool flesh = hitFlesh != null && i < hitFlesh.Length && hitFlesh[i];
                FxUtil.Impact(to, (muzzle - to).normalized, flesh);
            }
        }

        void RebuildWeaponModel()
        {
            if (weaponHolder == null || Application.isBatchMode) return;
            if (_currentModel != null) Destroy(_currentModel);

            WeaponData w = CurrentWeapon;
            if (w == null) return;

            _currentModel = new GameObject(w.weaponName + "_Model");
            _currentModel.transform.SetParent(weaponHolder, false);

            // Receiver
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_currentModel.transform, false);
            body.transform.localScale = w.modelScale;
            var br = body.GetComponent<Renderer>();
            br.sharedMaterial = FxUtil.ColorMaterial(w.bodyColor);
            br.shadowCastingMode = ShadowCastingMode.Off;

            // Barrel
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(_currentModel.transform, false);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.025f, w.barrelLength * 0.5f, 0.025f);
            barrel.transform.localPosition = new Vector3(0f, 0.015f, w.modelScale.z * 0.45f + w.barrelLength * 0.4f);
            var barR = barrel.GetComponent<Renderer>();
            barR.sharedMaterial = FxUtil.ColorMaterial(w.accentColor);
            barR.shadowCastingMode = ShadowCastingMode.Off;

            // Optic / Scope
            var optic = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(optic.GetComponent<Collider>());
            optic.transform.SetParent(_currentModel.transform, false);
            optic.transform.localScale = w.category == WeaponCategory.Sniper
                ? new Vector3(0.038f, 0.038f, 0.24f)
                : new Vector3(0.03f, 0.025f, 0.09f);
            optic.transform.localPosition = new Vector3(0f, w.modelScale.y * 0.65f, 0.04f);
            var or = optic.GetComponent<Renderer>();
            or.sharedMaterial = FxUtil.ColorMaterial(w.accentColor);
            or.shadowCastingMode = ShadowCastingMode.Off;

            // Muzzle marker
            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(_currentModel.transform, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0.015f, w.modelScale.z * 0.45f + w.barrelLength);
            _muzzlePoint = muzzleGo.transform;

            weaponHolder.gameObject.SetActive(_modelsVisible);
        }

        static Vector3 ApplySpread(Vector3 forward, float spreadDegrees)
        {
            if (spreadDegrees <= 0.001f) return forward;
            Vector2 rnd = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            Quaternion q = Quaternion.LookRotation(forward);
            return (q * new Vector3(rnd.x, rnd.y, 1f)).normalized;
        }

        sealed class DistanceComparer : IComparer<RaycastHit>
        {
            public static readonly DistanceComparer Instance = new DistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
