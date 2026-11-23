using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// COD4-style gunplay controller: primary/sidearm switching, ADS snap, sprint tilt,
    /// strategy perk modifiers (Stopping Power / Rusher / Ghost), multi-part 3D viewmodels and server validation.
    /// </summary>
    public class WeaponController : NetworkBehaviour
    {
        [Header("References")]
        public WeaponDatabase database;
        public Transform weaponHolder;

        public readonly SyncVar<int> PrimaryIndex = new SyncVar<int>(0);
        public readonly SyncVar<int> SecondaryIndex = new SyncVar<int>(0);
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
        WeaponModelBuilder.WeaponRig _rig;
        Transform _muzzlePoint;
        Vector3 _hipLocalPos = new Vector3(0.21f, -0.19f, 0.44f);
        Vector3 _hipLocalEuler = new Vector3(1.5f, -4.2f, -2.0f);
        Vector3 _adsLocalPos = new Vector3(0f, -0.072f, 0.27f);
        float _recoilKickZ;
        float _recoilKickPitch;
        float _boltAnim;
        float _pumpAnim;
        float _sprintTilt;
        float _inspectUntil;
        float _inspectBlend;
        Vector2 _swayOffset;

        readonly RaycastHit[] _rayBuffer = new RaycastHit[32];

        public float AdsAmount => _ads;
        public bool IsSteadyingBreath => IsSniperScope && IVInput.Held(IVKey.Sprint);
        public float LastShotTime { get; private set; } = -100f;
        public float CurrentSpreadDegrees { get; private set; } = 1.5f;
        public bool IsReloading => _reloading;
        public float ReloadProgress01 => _reloading ? Mathf.Clamp01((Time.time - _reloadStart) / Mathf.Max(0.01f, _reloadDuration)) : 0f;
        public int CurrentAmmo => _magAmmo[Mathf.Clamp(ActiveSlot.Value, 0, 1)];
        public int CurrentReserve => _reserveAmmo[Mathf.Clamp(ActiveSlot.Value, 0, 1)];
        public bool IsSniperScope => CurrentWeapon != null && CurrentWeapon.category == WeaponCategory.Sniper && _ads > 0.72f;
        public float MoveSpeedMultiplier => CurrentWeapon != null ? CurrentWeapon.moveSpeedMultiplier : 1f;

        public WeaponData CurrentWeapon
        {
            get
            {
                WeaponDatabase db = ActiveDb;
                if (db == null || db.Count == 0) return null;
                int idx = ActiveSlot.Value == 1
                    ? db.ResolveSidearmIndex(SecondaryIndex.Value)
                    : db.ClampPrimaryIndex(PrimaryIndex.Value);
                return db.Get(idx);
            }
        }

        WeaponDatabase ActiveDb => database != null && database.Count > 0 ? database : WeaponDatabase.RuntimeFallback();

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
            if (weaponHolder != null) _hipLocalPos = weaponHolder.localPosition;
            PrimaryIndex.OnChange += OnWeaponChanged;
            SecondaryIndex.OnChange += OnWeaponChanged;
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
                ServerSetLoadout(LocalProfile.PrimaryWeapon, LocalProfile.SecondaryWeapon);
                ResetLocalAmmo();
            }
            RebuildWeaponModel();
        }

        void OnWeaponChanged(int prev, int next, bool asServer)
        {
            if (IsOwner) _reloading = false;
            RebuildWeaponModel();
        }

        public void ClientApplyNewLoadout(int primaryIdx, int secondaryIdx)
        {
            if (!IsOwner) return;
            ServerSetLoadout(primaryIdx, secondaryIdx);
        }

        [ServerRpc]
        void ServerSetLoadout(int primaryIdx, int secondaryIdx)
        {
            PrimaryIndex.Value = ActiveDb.ClampPrimaryIndex(primaryIdx);
            SecondaryIndex.Value = Mathf.Clamp(secondaryIdx, 0, 1);
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
            _serverNextShotTime[0] = 0f;
            _serverNextShotTime[1] = 0f;
        }

        public void OnLocalRespawn()
        {
            _reloading = false;
            _ads = 0f;
            ServerSetLoadout(LocalProfile.PrimaryWeapon, LocalProfile.SecondaryWeapon);
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
            int idx = slot == 1
                ? db.ResolveSidearmIndex(SecondaryIndex.Value)
                : db.ClampPrimaryIndex(PrimaryIndex.Value);
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

            bool isRusher = _id.Strategy.Value == (int)TacticalStrategy.Rusher;
            bool isGhost = _id.Strategy.Value == (int)TacticalStrategy.Ghost;
            float adsSpeedMult = isRusher ? 1.25f : 1f;

            bool wantsAds = IVInput.Held(IVKey.Aim) && !_reloading;
            _ads = Mathf.MoveTowards(_ads, wantsAds ? 1f : 0f, Time.deltaTime * w.adsSpeed * adsSpeedMult);
            if (_id.Motor != null) _id.Motor.AimingDown = _ads > 0.25f;
            if (_id.Look != null) _id.Look.FovMultiplier = Mathf.Lerp(1f, w.adsFovMultiplier, _ads);

            if (IVInput.Down(IVKey.Inspect) && !_reloading && _ads < 0.1f)
                _inspectUntil = Time.time + 2.6f;
            if (wantsAds || IVInput.Held(IVKey.Fire) || _reloading || (_id.Motor != null && _id.Motor.IsSprinting))
                _inspectUntil = 0f;

            float movePenalty = _id.Motor != null ? _id.Motor.Speed01 * (isGhost ? 0.95f : 1.35f) : 0f;
            float crouchBonus = _id.Motor != null && _id.Motor.IsCrouching ? 0.70f : 1f;
            CurrentSpreadDegrees = Mathf.Lerp(w.hipSpread, w.adsSpread, _ads) * crouchBonus + movePenalty * (1f - _ads * 0.72f);

            UpdateHolderPose(w);

            bool trigger = w.automatic ? IVInput.Held(IVKey.Fire) : IVInput.Down(IVKey.Fire);
            if (trigger && Time.time >= _nextFireTime)
            {
                int slot = ActiveSlot.Value;
                // COD-style reload cancel when clicking fire if rounds remain in mag.
                if (_reloading && _magAmmo[slot] > 0 && IVInput.Down(IVKey.Fire))
                    _reloading = false;

                if (!_reloading)
                {
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
            float reloadMult = _id.Strategy.Value == (int)TacticalStrategy.Rusher ? 0.65f : 1.0f; // Sleight of Hand
            _reloadDuration = w.reloadTime * reloadMult;
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
            _recoilKickZ = Mathf.Lerp(_recoilKickZ, 0f, Time.deltaTime * 16f);
            _recoilKickPitch = Mathf.Lerp(_recoilKickPitch, 0f, Time.deltaTime * 18f);
            _boltAnim = Mathf.MoveTowards(_boltAnim, 0f, Time.deltaTime * (w.category == WeaponCategory.Sniper ? 3.5f : 14f));
            _pumpAnim = Mathf.MoveTowards(_pumpAnim, 0f, Time.deltaTime * 3.8f);

            if (_rig != null && _rig.BoltCarrier != null)
                _rig.BoltCarrier.localPosition = _rig.BoltRestPos + new Vector3(0f, 0f, _rig.BoltTravelZ * _boltAnim);
            if (_rig != null && _rig.PumpForend != null)
            {
                float pumpPull = Mathf.Sin(_pumpAnim * Mathf.PI);
                _rig.PumpForend.localPosition = _rig.PumpRestPos + new Vector3(0f, 0f, -0.068f * pumpPull);
            }

            bool sprinting = _id.Motor != null && _id.Motor.IsSprinting && _ads < 0.1f && !_reloading;
            _sprintTilt = Mathf.Lerp(_sprintTilt, sprinting ? 1f : 0f, Time.deltaTime * 11f);

            Vector2 lookInput = IVInput.Look;
            Vector2 targetSway = new Vector2(
                Mathf.Clamp(-lookInput.x * 0.0022f, -0.014f, 0.014f),
                Mathf.Clamp(-lookInput.y * 0.0022f, -0.012f, 0.012f)) * (1f - _ads * 0.78f);
            _swayOffset = Vector2.Lerp(_swayOffset, targetSway, Time.deltaTime * 10f);

            float moveBob = (_id.Motor != null && _id.Motor.IsGrounded) ? _id.Motor.Speed01 * 0.013f : 0f;
            float bobSpeed = sprinting ? 12f : 9f;
            float bobX = Mathf.Sin(Time.time * bobSpeed) * moveBob * (1f - _ads * 0.85f);
            float bobY = Mathf.Abs(Mathf.Cos(Time.time * bobSpeed)) * moveBob * (1f - _ads * 0.85f);

            bool inspecting = Time.time < _inspectUntil && !_reloading && _ads < 0.1f && !sprinting;
            _inspectBlend = Mathf.MoveTowards(_inspectBlend, inspecting ? 1f : 0f, Time.deltaTime * 4.5f);

            Vector3 basePos = Vector3.Lerp(_hipLocalPos, _adsLocalPos, _ads);
            Vector3 inspectOffset = new Vector3(-0.11f, 0.04f, -0.05f) * _inspectBlend;
            Vector3 sprintOffset = new Vector3(-0.07f, -0.04f, -0.05f) * _sprintTilt;
            Vector3 reloadOffset = _reloading ? new Vector3(0f, -0.14f, -0.04f) : Vector3.zero;
            weaponHolder.localPosition = basePos + inspectOffset + sprintOffset + reloadOffset
                + new Vector3(bobX + _swayOffset.x, -bobY + _swayOffset.y, -_recoilKickZ);

            Quaternion hipRot = Quaternion.Slerp(Quaternion.Euler(_hipLocalEuler), Quaternion.identity, _ads);
            float inspWave = Mathf.Sin((_inspectUntil - Time.time) * 2.2f) * 14f;
            Quaternion inspectRot = Quaternion.Euler(14f * _inspectBlend, (54f + inspWave) * _inspectBlend, -22f * _inspectBlend);
            Quaternion swayRot = Quaternion.Euler(-_swayOffset.y * 160f - _recoilKickPitch, _swayOffset.x * 160f, -_swayOffset.x * 220f);
            Quaternion sprintRot = Quaternion.Euler(14f * _sprintTilt, -24f * _sprintTilt, 8f * _sprintTilt);
            Quaternion reloadRot = _reloading ? Quaternion.Euler(28f, -15f, 18f) : Quaternion.identity;
            weaponHolder.localRotation = hipRot * inspectRot * swayRot * sprintRot * reloadRot;

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
            _recoilKickZ = Mathf.Min(0.070f, _recoilKickZ + 0.036f);
            _recoilKickPitch = Mathf.Min(6.5f, _recoilKickPitch + (w.category == WeaponCategory.Pistol ? 3.8f : 2.2f));
            _boltAnim = 1f;
            if (w.category == WeaponCategory.Shotgun) _pumpAnim = 1f;

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
                    reports.Add(new HitReport { Target = nob, Point = endPt, Headshot = headshot });
            }

            FxUtil.MuzzleFlash(muzzle, aimTf.forward);
            Vector3 ejectOrigin = _rig != null && _rig.EjectPort != null
                ? _rig.EjectPort.position
                : muzzle - aimTf.forward * 0.28f + aimTf.right * 0.04f;
            SpawnShellEject(ejectOrigin, aimTf.right, aimTf.up);
            FxUtil.PlaySound2D(FxUtil.ShotClip, 0.85f, w.shotPitch * UnityEngine.Random.Range(0.96f, 1.04f));

            if (_id.Look != null)
            {
                float hRecoil = UnityEngine.Random.Range(-w.recoilHorizontal, w.recoilHorizontal);
                float adsReduction = Mathf.Lerp(1f, 0.70f, _ads);
                _id.Look.AddRecoil(w.recoilVertical * adsReduction, hRecoil * adsReduction);
                _id.Look.AddScreenShake(w.screenShake * adsReduction);
            }

            ServerFire(slot, origin, muzzle, endPoints, hitFlesh, reports.ToArray());

            if (_magAmmo[slot] <= 0 && _reserveAmmo[slot] > 0)
                TryStartReload(w);
        }

        static void SpawnShellEject(Vector3 pos, Vector3 right, Vector3 up)
        {
            if (!FxUtil.Enabled) return;
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(shell.GetComponent<Collider>());
            shell.name = "BrassShell";
            shell.transform.position = pos;
            shell.transform.rotation = Quaternion.LookRotation(right, up) * Quaternion.Euler(90f, 0f, 0f);
            shell.transform.localScale = new Vector3(0.011f, 0.018f, 0.011f);
            var rend = shell.GetComponent<Renderer>();
            rend.sharedMaterial = FxUtil.MetalMaterial(new Color(0.88f, 0.72f, 0.26f));
            rend.shadowCastingMode = ShadowCastingMode.Off;
            var rb = shell.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.linearVelocity = right * UnityEngine.Random.Range(1.5f, 2.6f) + up * UnityEngine.Random.Range(1.1f, 1.9f);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 28f;
            Destroy(shell, 0.45f);
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
                    if (col.GetComponentInParent<PlayerHealth>() == null) return;
                    continue;
                }

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

            if (Vector3.Distance(origin, transform.position + Vector3.up * 1.4f) > 3.5f) return;

            float perkDamageMult = _id.Strategy.Value == (int)TacticalStrategy.Breacher ? 1.15f : 1.0f; // Stopping Power

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
                    if (Vector3.Distance(hr.Point, hr.Target.transform.position + Vector3.up * 0.9f) > 5.0f) continue;

                    IDamageable dmg = hr.Target.GetComponent<IDamageable>();
                    if (dmg == null || !dmg.IsAlive) continue;

                    float amount = w.damage * perkDamageMult * (hr.Headshot ? w.headshotMultiplier : 1f);
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

            _rig = WeaponModelBuilder.Build(w, weaponHolder, IsOwner);
            _currentModel = _rig.Root;
            _muzzlePoint = _rig.Muzzle;
            _hipLocalPos = _rig.HipLocalPos;
            _hipLocalEuler = _rig.HipLocalEuler;
            _adsLocalPos = new Vector3(-_rig.AimAnchorLocalPos.x, -_rig.AimAnchorLocalPos.y, _rig.AdsDistanceZ);
            _boltAnim = 0f;
            _pumpAnim = 0f;

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
