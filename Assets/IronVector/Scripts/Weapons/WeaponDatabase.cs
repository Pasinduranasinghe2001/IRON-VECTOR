using System.Collections.Generic;
using UnityEngine;

namespace IronVector
{
    /// <summary>COD4 Modern Warfare-inspired weapon catalog (7 primaries + 2 sidearms).</summary>
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Iron Vector/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        public static readonly string[] PrimaryLabels =
        {
            "M4A1 Carbine (AR)",
            "AK-47 (Heavy AR)",
            "MP5 (SMG)",
            "P90 (High-Cap SMG)",
            "M40A3 (Bolt Sniper)",
            "W1200 (Pump Shotgun)",
            "M249 SAW (LMG)"
        };

        public static readonly string[] SecondaryLabels =
        {
            "M9 (9mm Pistol)",
            "Desert Eagle (.50)"
        };

        public WeaponData[] weapons = new WeaponData[0];
        public int primaryCount = 7;
        public int firstSidearmIndex = 7;

        static WeaponDatabase _runtimeFallback;

        public int Count => weapons != null ? weapons.Length : 0;

        public WeaponData Get(int index)
        {
            if (weapons == null || weapons.Length == 0) return null;
            return weapons[Mathf.Clamp(index, 0, weapons.Length - 1)];
        }

        public int ClampPrimaryIndex(int index) =>
            Count == 0 ? 0 : Mathf.Clamp(index, 0, Mathf.Min(primaryCount - 1, Count - 1));

        public int ResolveSidearmIndex(int secondaryChoice)
        {
            if (Count == 0) return 0;
            int idx = firstSidearmIndex + Mathf.Clamp(secondaryChoice, 0, 1);
            return Mathf.Clamp(idx, 0, Count - 1);
        }

        public static WeaponDatabase RuntimeFallback()
        {
            if (_runtimeFallback != null) return _runtimeFallback;
            _runtimeFallback = CreateInstance<WeaponDatabase>();
            _runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
            _runtimeFallback.weapons = CreateDefaultRuntimeAssets();
            _runtimeFallback.primaryCount = 7;
            _runtimeFallback.firstSidearmIndex = 7;
            return _runtimeFallback;
        }

        public static WeaponData[] CreateDefaultRuntimeAssets()
        {
            var list = new List<WeaponData>
            {
                // 0: M4A1 Carbine
                Make("M4A1 Carbine", WeaponCategory.AssaultRifle, WeaponVisualStyle.ModernCarbine, true,
                    780f, 25f, 1.55f, 150f, 1, 0.96f, 1.75f, 0.16f, 1.00f, 0.38f, 0.22f, 0.78f, 13f,
                    30, 150, 1.65f,
                    new Color(0.16f, 0.17f, 0.19f), new Color(0.30f, 0.28f, 0.24f), new Color(1f, 0.85f, 0.35f),
                    1.02f, new Vector3(0.056f, 0.072f, 0.52f), 0.40f),

                // 1: AK-47
                Make("AK-47", WeaponCategory.AssaultRifle, WeaponVisualStyle.KalashnikovWood, true,
                    650f, 34f, 1.60f, 155f, 1, 0.94f, 2.25f, 0.24f, 1.55f, 0.62f, 0.34f, 0.80f, 12f,
                    30, 120, 1.85f,
                    new Color(0.15f, 0.15f, 0.16f), new Color(0.46f, 0.25f, 0.12f), new Color(1f, 0.78f, 0.25f),
                    0.86f, new Vector3(0.058f, 0.076f, 0.55f), 0.44f),

                // 2: MP5
                Make("MP5", WeaponCategory.SMG, WeaponVisualStyle.SubmachineSuppressed, true,
                    820f, 24f, 1.50f, 95f, 1, 1.04f, 1.10f, 0.26f, 0.82f, 0.42f, 0.18f, 0.84f, 16f,
                    30, 150, 1.45f,
                    new Color(0.13f, 0.14f, 0.16f), new Color(0.22f, 0.24f, 0.27f), new Color(1f, 0.90f, 0.45f),
                    1.24f, new Vector3(0.050f, 0.068f, 0.38f), 0.28f),

                // 3: P90
                Make("P90", WeaponCategory.SMG, WeaponVisualStyle.P90TopMag, true,
                    920f, 20f, 1.50f, 100f, 1, 1.04f, 1.00f, 0.28f, 0.72f, 0.46f, 0.16f, 0.85f, 16f,
                    50, 150, 1.85f,
                    new Color(0.18f, 0.19f, 0.20f), new Color(0.32f, 0.31f, 0.28f), new Color(1f, 0.92f, 0.50f),
                    1.34f, new Vector3(0.058f, 0.088f, 0.42f), 0.22f),

                // 4: M40A3 Bolt Sniper
                Make("M40A3", WeaponCategory.Sniper, WeaponVisualStyle.SniperBolt, false,
                    50f, 88f, 2.20f, 280f, 1, 0.90f, 5.2f, 0.01f, 3.4f, 0.55f, 0.55f, 0.30f, 11f,
                    5, 25, 2.25f,
                    new Color(0.22f, 0.26f, 0.18f), new Color(0.12f, 0.13f, 0.14f), new Color(0.95f, 0.96f, 0.72f),
                    0.66f, new Vector3(0.056f, 0.072f, 0.72f), 0.64f),

                // 5: W1200 Pump Shotgun
                Make("W1200", WeaponCategory.Shotgun, WeaponVisualStyle.PumpShotgun, false,
                    76f, 20f, 1.35f, 44f, 8, 0.98f, 4.4f, 2.7f, 2.9f, 1.0f, 0.50f, 0.86f, 12f,
                    7, 35, 1.95f,
                    new Color(0.20f, 0.18f, 0.17f), new Color(0.42f, 0.25f, 0.14f), new Color(1f, 0.72f, 0.25f),
                    0.76f, new Vector3(0.060f, 0.074f, 0.58f), 0.46f),

                // 6: M249 SAW LMG
                Make("M249 SAW", WeaponCategory.LMG, WeaponVisualStyle.LightMachineGun, true,
                    750f, 28f, 1.50f, 165f, 1, 0.86f, 2.8f, 0.35f, 1.35f, 0.75f, 0.32f, 0.82f, 8.5f,
                    100, 200, 3.10f,
                    new Color(0.19f, 0.21f, 0.18f), new Color(0.27f, 0.29f, 0.22f), new Color(1f, 0.82f, 0.30f),
                    0.82f, new Vector3(0.078f, 0.095f, 0.64f), 0.50f),

                // 7: M9 Tactical Pistol
                Make("M9", WeaponCategory.Pistol, WeaponVisualStyle.TacticalPistol, false,
                    480f, 28f, 1.75f, 90f, 1, 1.06f, 1.25f, 0.25f, 1.10f, 0.30f, 0.18f, 0.88f, 17f,
                    15, 60, 1.15f,
                    new Color(0.14f, 0.15f, 0.16f), new Color(0.24f, 0.24f, 0.26f), new Color(1f, 0.88f, 0.45f),
                    1.18f, new Vector3(0.038f, 0.056f, 0.22f), 0.14f),

                // 8: Desert Eagle .50
                Make("Desert Eagle", WeaponCategory.Pistol, WeaponVisualStyle.ChromeHandCannon, false,
                    290f, 52f, 2.00f, 115f, 1, 1.03f, 1.65f, 0.32f, 2.60f, 0.55f, 0.45f, 0.86f, 15f,
                    7, 35, 1.45f,
                    new Color(0.76f, 0.78f, 0.82f), new Color(0.16f, 0.16f, 0.18f), new Color(1f, 0.78f, 0.35f),
                    0.74f, new Vector3(0.044f, 0.068f, 0.27f), 0.17f)
            };
            return list.ToArray();
        }

        static WeaponData Make(
            string wName, WeaponCategory cat, WeaponVisualStyle style, bool auto,
            float rpm, float dmg, float hs, float range, int pellets, float moveMult,
            float hipSpread, float adsSpread, float recV, float recH, float shake, float adsFov, float adsSpd,
            int mag, int reserve, float reload, Color body, Color accent, Color tracer, float pitch, Vector3 scale, float barrel)
        {
            var d = CreateInstance<WeaponData>();
            d.name = wName;
            d.weaponName = wName;
            d.category = cat;
            d.visualStyle = style;
            d.automatic = auto;
            d.fireRateRPM = rpm;
            d.damage = dmg;
            d.headshotMultiplier = hs;
            d.range = range;
            d.pellets = pellets;
            d.moveSpeedMultiplier = moveMult;
            d.hipSpread = hipSpread;
            d.adsSpread = adsSpread;
            d.recoilVertical = recV;
            d.recoilHorizontal = recH;
            d.screenShake = shake;
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
