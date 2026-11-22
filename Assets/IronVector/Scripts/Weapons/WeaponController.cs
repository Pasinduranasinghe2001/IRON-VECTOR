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
        Transform _muzzlePoint;
        Vector3 _hipLocalPos = new Vector3(0.24f, -0.22f, 0.45f);
        Vector3 _adsLocalPos = new Vector3(0f, -0.142f, 0.35f);
        float _recoilKickZ;
        float _sprintTilt;

        readonly RaycastHit[] _rayBuffer = new RaycastHit[32];

        public float AdsAmount => _ads;
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
            bool sprinting = _id.Motor != null && _id.Motor.IsSprinting && _ads < 0.1f && !_reloading;
            _sprintTilt = Mathf.Lerp(_sprintTilt, sprinting ? 1f : 0f, Time.deltaTime * 11f);

            float moveBob = (_id.Motor != null && _id.Motor.IsGrounded) ? _id.Motor.Speed01 * 0.016f : 0f;
            float bobSpeed = sprinting ? 12f : 9f;
            float bobX = Mathf.Sin(Time.time * bobSpeed) * moveBob * (1f - _ads * 0.82f);
            float bobY = Mathf.Abs(Mathf.Cos(Time.time * bobSpeed)) * moveBob * (1f - _ads * 0.82f);

            Vector3 basePos = Vector3.Lerp(_hipLocalPos, _adsLocalPos, _ads);
            Vector3 sprintOffset = new Vector3(-0.07f, -0.04f, -0.05f) * _sprintTilt;
            Vector3 reloadOffset = _reloading ? new Vector3(0f, -0.15f, -0.04f) : Vector3.zero;
            weaponHolder.localPosition = basePos + sprintOffset + reloadOffset + new Vector3(bobX, -bobY, -_recoilKickZ);

            Quaternion sprintRot = Quaternion.Euler(14f * _sprintTilt, -24f * _sprintTilt, 8f * _sprintTilt);
            Quaternion reloadRot = _reloading ? Quaternion.Euler(28f, -15f, 18f) : Quaternion.identity;
            weaponHolder.localRotation = sprintRot * reloadRot;

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
            _recoilKickZ = Mathf.Min(0.085f, _recoilKickZ + 0.04f);

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
            SpawnShellEject(muzzle - aimTf.forward * 0.25f + aimTf.right * 0.06f, aimTf.right, aimTf.up);
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
            GameObject shell = FxUtil.Blob(PrimitiveType.Cube, pos, 0.022f, new Color(0.88f, 0.72f, 0.26f), 0.28f);
            if (shell == null) return;
            shell.transform.localScale = new Vector3(0.012f, 0.012f, 0.032f);
            var rb = shell.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.linearVelocity = right * UnityEngine.Random.Range(1.8f, 3.0f) + up * UnityEngine.Random.Range(1.2f, 2.2f);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 25f;
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

            _currentModel = new GameObject(w.weaponName + "_Model");
            _currentModel.transform.SetParent(weaponHolder, false);

            // Receiver
            AddWeaponPart("Receiver", PrimitiveType.Cube, Vector3.zero, w.modelScale, w.bodyColor);

            // Pistol Grip
            AddWeaponPart("Grip", PrimitiveType.Cube, new Vector3(0f, -w.modelScale.y * 0.85f, -w.modelScale.z * 0.16f),
                new Vector3(0.032f, 0.085f, 0.042f), w.bodyColor, new Vector3(16f, 0f, 0f));

            // Handguard & Stock (if long gun)
            if (w.category != WeaponCategory.Pistol)
            {
                AddWeaponPart("Stock", PrimitiveType.Cube, new Vector3(0f, -0.01f, -w.modelScale.z * 0.62f),
                    new Vector3(0.040f, 0.070f, 0.22f), w.accentColor);
                AddWeaponPart("Handguard", PrimitiveType.Cube, new Vector3(0f, -0.005f, w.modelScale.z * 0.36f),
                    new Vector3(w.modelScale.x * 1.05f, w.modelScale.y * 0.80f, w.modelScale.z * 0.38f), w.accentColor);
            }

            // Magazine style
            if (w.visualStyle == WeaponVisualStyle.P90TopMag)
            {
                AddWeaponPart("TopMag", PrimitiveType.Cube, new Vector3(0f, w.modelScale.y * 0.55f, 0.05f),
                    new Vector3(0.044f, 0.025f, 0.26f), new Color(0.25f, 0.22f, 0.16f));
            }
            else if (w.visualStyle == WeaponVisualStyle.LightMachineGun)
            {
                AddWeaponPart("BoxMag", PrimitiveType.Cube, new Vector3(0f, -w.modelScale.y * 1.15f, 0.05f),
                    new Vector3(0.11f, 0.12f, 0.14f), w.accentColor);
            }
            else if (w.category != WeaponCategory.Shotgun && w.category != WeaponCategory.Sniper)
            {
                float magTilt = w.visualStyle == WeaponVisualStyle.KalashnikovWood ? -22f : -8f;
                float magLen = w.category == WeaponCategory.Pistol ? 0.06f : 0.14f;
                AddWeaponPart("Magazine", PrimitiveType.Cube, new Vector3(0f, -w.modelScale.y * 1.05f, 0.07f),
                    new Vector3(0.028f, magLen, 0.048f), w.accentColor, new Vector3(magTilt, 0f, 0f));
            }

            // Barrel + Optional Suppressor
            bool hasSuppressor = w.visualStyle == WeaponVisualStyle.SubmachineSuppressed;
            AddWeaponPart("Barrel", PrimitiveType.Cylinder,
                new Vector3(0f, 0.012f, w.modelScale.z * 0.45f + w.barrelLength * 0.4f),
                new Vector3(hasSuppressor ? 0.040f : 0.024f, w.barrelLength * 0.5f, hasSuppressor ? 0.040f : 0.024f),
                w.visualStyle == WeaponVisualStyle.ChromeHandCannon ? w.bodyColor : new Color(0.12f, 0.12f, 0.13f),
                new Vector3(90f, 0f, 0f));

            // Optic / Iron Sight
            Vector3 opticScale = w.category == WeaponCategory.Sniper
                ? new Vector3(0.040f, 0.040f, 0.26f)
                : (w.category == WeaponCategory.AssaultRifle || w.visualStyle == WeaponVisualStyle.P90TopMag
                    ? new Vector3(0.032f, 0.028f, 0.10f)
                    : new Vector3(0.018f, 0.016f, 0.03f));
            AddWeaponPart("Optic", PrimitiveType.Cube,
                new Vector3(0f, w.modelScale.y * 0.65f, w.category == WeaponCategory.Pistol ? w.modelScale.z * 0.4f : 0.02f),
                opticScale, new Color(0.12f, 0.12f, 0.14f));

            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(_currentModel.transform, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0.012f, w.modelScale.z * 0.45f + w.barrelLength);
            _muzzlePoint = muzzleGo.transform;

            weaponHolder.gameObject.SetActive(_modelsVisible);
        }

        void AddWeaponPart(string partName, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = partName;
            go.transform.SetParent(_currentModel.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = FxUtil.ColorMaterial(color);
            r.shadowCastingMode = ShadowCastingMode.Off;
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
