using UnityEngine;

namespace IronVector
{
    public enum WeaponCategory
    {
        AssaultRifle,
        SMG,
        Sniper,
        Shotgun,
        LMG,
        Pistol
    }

    public enum WeaponVisualStyle
    {
        ModernCarbine = 0,
        KalashnikovWood = 1,
        SubmachineSuppressed = 2,
        P90TopMag = 3,
        SniperBolt = 4,
        PumpShotgun = 5,
        LightMachineGun = 6,
        TacticalPistol = 7,
        ChromeHandCannon = 8
    }

    /// <summary>COD4-inspired weapon ballistics, mobility, handling and visual silhouette settings.</summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Iron Vector/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName = "M4A1 Carbine";
        public WeaponCategory category = WeaponCategory.AssaultRifle;
        public WeaponVisualStyle visualStyle = WeaponVisualStyle.ModernCarbine;
        public bool automatic = true;

        [Header("Ballistics")]
        public float fireRateRPM = 780f;
        public float damage = 25f;
        public float headshotMultiplier = 1.55f;
        public float range = 150f;
        public int pellets = 1;

        [Header("Handling, Mobility & Recoil")]
        public float moveSpeedMultiplier = 0.96f;
        public float hipSpread = 1.8f;
        public float adsSpread = 0.18f;
        public float recoilVertical = 1.05f;
        public float recoilHorizontal = 0.40f;
        public float screenShake = 0.22f;
        public float adsFovMultiplier = 0.78f;
        public float adsSpeed = 13f;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int reserveAmmo = 150;
        public float reloadTime = 1.65f;

        [Header("Visuals & Audio")]
        public Color bodyColor = new Color(0.18f, 0.19f, 0.21f);
        public Color accentColor = new Color(0.32f, 0.30f, 0.26f);
        public Color tracerColor = new Color(1f, 0.85f, 0.35f);
        public float shotPitch = 1.0f;
        public Vector3 modelScale = new Vector3(0.058f, 0.075f, 0.54f);
        public float barrelLength = 0.42f;
    }
}
