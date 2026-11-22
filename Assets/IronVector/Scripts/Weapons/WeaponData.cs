using UnityEngine;

namespace IronVector
{
    public enum WeaponCategory
    {
        AssaultRifle,
        SMG,
        Sniper,
        Shotgun,
        Pistol
    }

    /// <summary>Data asset driving ballistics, handling, ammo and procedural gun proportions.</summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Iron Vector/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName = "M4A1 Carbine";
        public WeaponCategory category = WeaponCategory.AssaultRifle;
        public bool automatic = true;

        [Header("Ballistics")]
        public float fireRateRPM = 720f;
        public float damage = 24f;
        public float headshotMultiplier = 1.6f;
        public float range = 140f;
        public int pellets = 1;

        [Header("Handling & Recoil")]
        public float hipSpread = 1.8f;
        public float adsSpread = 0.25f;
        public float recoilVertical = 1.15f;
        public float recoilHorizontal = 0.45f;
        public float adsFovMultiplier = 0.78f;
        public float adsSpeed = 12f;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int reserveAmmo = 120;
        public float reloadTime = 1.75f;

        [Header("Visuals & Audio")]
        public Color bodyColor = new Color(0.18f, 0.19f, 0.21f);
        public Color accentColor = new Color(0.32f, 0.30f, 0.26f);
        public Color tracerColor = new Color(1f, 0.85f, 0.35f);
        public float shotPitch = 1.0f;
        public Vector3 modelScale = new Vector3(0.06f, 0.08f, 0.55f);
        public float barrelLength = 0.42f;
    }
}
