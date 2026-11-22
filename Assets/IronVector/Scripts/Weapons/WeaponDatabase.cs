using System.Collections.Generic;
using UnityEngine;

namespace IronVector
{
    /// <summary>Catalog of weapons. Provides a runtime fallback if no asset is wired yet.</summary>
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Iron Vector/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        public static readonly string[] DefaultPrimaryLabels =
        {
            "M4A1 Carbine (AR)",
            "MP5K Tactical (SMG)",
            "M24 Sniper (Bolt)",
            "M870 Breacher (Shotgun)"
        };

        public WeaponData[] weapons = new WeaponData[0];
        public int sidearmIndex = 4;

        static WeaponDatabase _runtimeFallback;

        public int Count => weapons != null ? weapons.Length : 0;

        public WeaponData Get(int index)
        {
            if (weapons == null || weapons.Length == 0) return null;
            int clamped = Mathf.Clamp(index, 0, weapons.Length - 1);
            return weapons[clamped];
        }

        public int ClampPrimaryIndex(int index)
        {
            int maxPrimary = Mathf.Max(0, Count - 2); // last slot is usually sidearm
            if (Count == 0) return 0;
            return Mathf.Clamp(index, 0, maxPrimary);
        }

        public static WeaponDatabase RuntimeFallback()
        {
            if (_runtimeFallback != null) return _runtimeFallback;
            _runtimeFallback = CreateInstance<WeaponDatabase>();
            _runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
            _runtimeFallback.weapons = CreateDefaultRuntimeAssets();
            _runtimeFallback.sidearmIndex = 4;
            return _runtimeFallback;
        }

        public static WeaponData[] CreateDefaultRuntimeAssets()
        {
            var list = new List<WeaponData>
            {
                Make("M4A1 Carbine", WeaponCategory.AssaultRifle, true, 720f, 24f, 1.6f, 145f, 1,
                    1.8f, 0.22f, 1.1f, 0.45f, 0.78f, 12f, 30, 120, 1.75f,
                    new Color(0.18f, 0.19f, 0.21f), new Color(0.32f, 0.30f, 0.25f), new Color(1f, 0.85f, 0.35f),
                    1.0f, new Vector3(0.06f, 0.08f, 0.56f), 0.42f),

                Make("MP5K Tactical", WeaponCategory.SMG, true, 880f, 19f, 1.5f, 95f, 1,
                    1.2f, 0.32f, 0.85f, 0.50f, 0.84f, 15f, 32, 160, 1.50f,
                    new Color(0.14f, 0.15f, 0.17f), new Color(0.24f, 0.25f, 0.28f), new Color(1f, 0.90f, 0.45f),
                    1.22f, new Vector3(0.055f, 0.075f, 0.40f), 0.26f),

                Make("M24 Sniper", WeaponCategory.Sniper, false, 52f, 75f, 2.2f, 260f, 1,
                    5.5f, 0.02f, 3.6f, 0.65f, 0.36f, 9f, 5, 25, 2.35f,
                    new Color(0.22f, 0.25f, 0.19f), new Color(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.95f, 0.70f),
                    0.68f, new Vector3(0.06f, 0.08f, 0.74f), 0.62f),

                Make("M870 Breacher", WeaponCategory.Shotgun, false, 78f, 16f, 1.35f, 45f, 8,
                    4.6f, 2.9f, 3.1f, 1.1f, 0.86f, 11f, 6, 36, 2.00f,
                    new Color(0.20f, 0.17f, 0.15f), new Color(0.38f, 0.26f, 0.16f), new Color(1f, 0.72f, 0.25f),
                    0.78f, new Vector3(0.065f, 0.08f, 0.58f), 0.46f),

                Make("P226 Sidearm", WeaponCategory.Pistol, false, 420f, 26f, 1.75f, 85f, 1,
                    1.35f, 0.30f, 1.25f, 0.35f, 0.86f, 16f, 15, 60, 1.25f,
                    new Color(0.15f, 0.15f, 0.16f), new Color(0.28f, 0.28f, 0.30f), new Color(1f, 0.88f, 0.45f),
                    1.15f, new Vector3(0.042f, 0.06f, 0.24f), 0.14f)
            };
            return list.ToArray();
        }

        static WeaponData Make(
            string wName, WeaponCategory cat, bool auto, float rpm, float dmg, float hs, float range, int pellets,
            float hipSpread, float adsSpread, float recV, float recH, float adsFov, float adsSpd,
            int mag, int reserve, float reload, Color body, Color accent, Color tracer, float pitch, Vector3 scale, float barrel)
        {
            var d = CreateInstance<WeaponData>();
            d.name = wName;
            d.weaponName = wName;
            d.category = cat;
            d.automatic = auto;
            d.fireRateRPM = rpm;
            d.damage = dmg;
            d.headshotMultiplier = hs;
            d.range = range;
            d.pellets = pellets;
            d.hipSpread = hipSpread;
            d.adsSpread = adsSpread;
            d.recoilVertical = recV;
            d.recoilHorizontal = recH;
            d.adsFovMultiplier = adsFov;
            d.adsSpeed = adsSpd;
            d.magazineSize = mag;
            d.reserveAmmo = reserve;
            d.reloadTime = reload;
            d.bodyColor = body;
            d.accentColor = accent;
            d.tracerColor = tracer;
            d.shotPitch = pitch;
            d.modelScale = scale;
            d.barrelLength = barrel;
            return d;
        }
    }
}
