using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// Live 3D tactical lobby showcase for MainMenu: renders a night wet-asphalt helipad,
    /// neon skyline towers, idling Blackhawk-style transport helicopter, and a 3D Tactical Operator
    /// holding the player's actual live-equipped CAD weapon from WeaponModelBuilder.
    /// </summary>
    public class LobbyStageController : MonoBehaviour
    {
        Camera _cam;
        Transform _operatorRoot;
        Transform _weaponMount;
        Transform _mainRotor;
        Transform _tailRotor;
        GameObject _currentWeaponGo;

        int _shownWeaponIdx = -1;
        bool _shownSecondary;
        public bool InspectSecondary;
        float _yawOffset = 18f;
        bool _inspectMode;

        readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        public static LobbyStageController Ensure(Camera menuCam)
        {
            LobbyStageController existing = FindAnyObjectByType<LobbyStageController>();
            if (existing != null)
            {
                existing._cam = menuCam;
                return existing;
            }

            // Clear legacy prototype backdrop objects if present in older baked MainMenu.unity
            foreach (string legacy in new[] { "MenuStage", "MonumentA", "MonumentB" })
            {
                GameObject go = GameObject.Find(legacy);
                if (go != null) Destroy(go);
            }

            var stageGo = new GameObject("LobbyStage3D");
            var ctrl = stageGo.AddComponent<LobbyStageController>();
            ctrl._cam = menuCam;
            ctrl.BuildStage();
            return ctrl;
        }

        public void SetInspectMode(bool inspect) => _inspectMode = inspect;

        public void AddDragRotation(float deltaX) => _yawOffset += deltaX * 0.48f;

        void BuildStage()
        {
            ApplyNightHelipadEnvironment();

            Material matAsphalt = GetMat("Asphalt", new Color(0.08f, 0.09f, 0.11f), 0.72f, 0.10f);
            Material matSteel = GetMat("Steel", new Color(0.18f, 0.21f, 0.25f), 0.56f, 0.68f);
            Material matChopper = GetMat("ChopperBody", new Color(0.11f, 0.13f, 0.16f), 0.44f, 0.52f);
            Material matGlass = GetMat("CockpitGlass", new Color(0.10f, 0.22f, 0.30f), 0.90f, 0.25f);
            Material matPadMark = GetMat("HeliLine", new Color(0.86f, 0.62f, 0.16f), 0.25f, 0f, new Color(0.35f, 0.20f, 0.04f));
            Material matCyanNeon = GetMat("CyanNeon", new Color(0.15f, 0.85f, 1.0f), 0.2f, 0f, new Color(0.25f, 1.45f, 2.1f));
            Material matRedNeon = GetMat("RedNeon", new Color(1.0f, 0.22f, 0.28f), 0.2f, 0f, new Color(1.9f, 0.26f, 0.35f));

            // 1. Wet Helipad Tarmac & Painted Markings
            Part("HelipadTarmac", PrimitiveType.Cube, new Vector3(0f, -0.5f, 2f), new Vector3(48f, 1f, 48f), matAsphalt);
            Part("PadCircleOuter", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, 0.2f), new Vector3(4.2f, 0.005f, 4.2f), GetMat("PadInner", new Color(0.06f, 0.07f, 0.09f), 0.80f, 0.05f));
            Part("PadRingN", PrimitiveType.Cube, new Vector3(0f, 0.012f, 2.3f), new Vector3(2.4f, 0.006f, 0.12f), matPadMark);
            Part("PadRingS", PrimitiveType.Cube, new Vector3(0f, 0.012f, -1.9f), new Vector3(2.4f, 0.006f, 0.12f), matPadMark);
            Part("PadHBarL", PrimitiveType.Cube, new Vector3(-0.55f, 0.012f, 0.2f), new Vector3(0.14f, 0.006f, 1.6f), matPadMark);
            Part("PadHBarR", PrimitiveType.Cube, new Vector3(0.55f, 0.012f, 0.2f), new Vector3(0.14f, 0.006f, 1.6f), matPadMark);
            Part("PadHBarMid", PrimitiveType.Cube, new Vector3(0f, 0.012f, 0.2f), new Vector3(1.1f, 0.006f, 0.14f), matPadMark);

            // Perimeter Runway Amber LEDs
            for (int i = -3; i <= 3; i++)
            {
                Part("EdgeLed_" + i, PrimitiveType.Cube, new Vector3(i * 2.2f, 0.03f, -2.5f), new Vector3(0.16f, 0.04f, 0.08f), matPadMark);
            }

            // 2. Neon Skyline Towers Backdrop
            Material matTower = GetMat("CityTower", new Color(0.05f, 0.07f, 0.10f), 0.3f, 0.1f);
            Part("SkylineTowerL", PrimitiveType.Cube, new Vector3(-7.2f, 7.5f, 16f), new Vector3(4.2f, 16f, 3.6f), matTower);
            Part("SkylineBannerCyan", PrimitiveType.Cube, new Vector3(-6.4f, 7.5f, 14.1f), new Vector3(2.1f, 11.5f, 0.12f), matCyanNeon);
            Part("SkylineTowerR", PrimitiveType.Cube, new Vector3(6.8f, 6.2f, 18f), new Vector3(5.0f, 13f, 4.0f), matTower);
            Part("SkylineBannerRed", PrimitiveType.Cube, new Vector3(5.8f, 4.5f, 15.9f), new Vector3(3.4f, 4.2f, 0.12f), matRedNeon);
            Part("SkylineTowerFar", PrimitiveType.Cube, new Vector3(0.5f, 8.5f, 22f), new Vector3(4.8f, 18f, 4.2f), matTower);

            // 3. Tactical Blackhawk-Style Helicopter
            BuildHelicopter(matChopper, matSteel, matGlass, matRedNeon);

            // 4. Center Tactical 3D Operator
            BuildOperator();

            // 5. Dramatic Rim, Key & Uplights
            AddLight("KeySpot", LightType.Spot, new Vector3(-2.6f, 4.2f, -3.0f), Quaternion.LookRotation(new Vector3(2.6f, -3.0f, 3.0f)),
                new Color(0.82f, 0.90f, 1.0f), 16f, 6.5f, 52f);
            AddLight("WarmUplight", LightType.Point, new Vector3(0f, 0.25f, -0.55f), Quaternion.identity,
                new Color(1.0f, 0.62f, 0.22f), 4.5f, 2.8f, 0f);
            AddLight("CyanRimLight", LightType.Point, new Vector3(-2.5f, 2.1f, 2.0f), Quaternion.identity,
                new Color(0.24f, 0.80f, 1.0f), 8f, 3.2f, 0f);
            AddLight("RedRimLight", LightType.Point, new Vector3(2.8f, 2.2f, 2.8f), Quaternion.identity,
                new Color(1.0f, 0.28f, 0.30f), 8f, 2.8f, 0f);
        }

        void BuildHelicopter(Material body, Material steel, Material glass, Material beacon)
        {
            var heli = new GameObject("LobbyHelicopter");
            heli.transform.SetParent(transform, false);
            heli.transform.localPosition = new Vector3(0.45f, 0.0f, 4.8f);
            heli.transform.localRotation = Quaternion.Euler(0f, 108f, 0f);

            // Fuselage, Sloped Nose & Cockpit Canopy
            Part("Fuselage", PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(1.65f, 1.35f, 4.4f), body, parent: heli.transform);
            Part("NoseCone", PrimitiveType.Cube, new Vector3(0f, 1.15f, 2.65f), new Vector3(1.38f, 0.95f, 1.35f), body, new Vector3(14f, 0f, 0f), heli.transform);
            Part("Windshield", PrimitiveType.Cube, new Vector3(0f, 1.56f, 2.25f), new Vector3(1.34f, 0.70f, 1.15f), glass, new Vector3(-34f, 0f, 0f), heli.transform);
            Part("TurbineLeft", PrimitiveType.Cylinder, new Vector3(-0.55f, 2.18f, 0.35f), new Vector3(0.55f, 1.15f, 0.55f), steel, new Vector3(90f, 0f, 0f), heli.transform);
            Part("TurbineRight", PrimitiveType.Cylinder, new Vector3(0.55f, 2.18f, 0.35f), new Vector3(0.55f, 1.15f, 0.55f), steel, new Vector3(90f, 0f, 0f), heli.transform);

            // Tail Boom, Stabilizers & Landing Gear
            Part("TailBoom", PrimitiveType.Cube, new Vector3(0f, 1.60f, -4.1f), new Vector3(0.48f, 0.54f, 4.2f), body, new Vector3(4f, 0f, 0f), heli.transform);
            Part("TailFin", PrimitiveType.Cube, new Vector3(0f, 2.25f, -6.0f), new Vector3(0.22f, 1.55f, 0.85f), body, new Vector3(-22f, 0f, 0f), heli.transform);
            Part("HorizStab", PrimitiveType.Cube, new Vector3(0f, 1.48f, -5.85f), new Vector3(2.2f, 0.10f, 0.55f), steel, parent: heli.transform);
            Part("BeaconTop", PrimitiveType.Sphere, new Vector3(0f, 3.05f, -6.1f), Vector3.one * 0.14f, beacon, parent: heli.transform);

            Part("GearStrutL", PrimitiveType.Cylinder, new Vector3(-0.98f, 0.45f, 1.3f), new Vector3(0.10f, 0.48f, 0.10f), steel, new Vector3(0f, 0f, 22f), heli.transform);
            Part("GearStrutR", PrimitiveType.Cylinder, new Vector3(0.98f, 0.45f, 1.3f), new Vector3(0.10f, 0.48f, 0.10f), steel, new Vector3(0f, 0f, -22f), heli.transform);
            Part("WheelL", PrimitiveType.Cylinder, new Vector3(-1.12f, 0.24f, 1.3f), new Vector3(0.48f, 0.16f, 0.48f), body, new Vector3(0f, 0f, 90f), heli.transform);
            Part("WheelR", PrimitiveType.Cylinder, new Vector3(1.12f, 0.24f, 1.3f), new Vector3(0.48f, 0.16f, 0.48f), body, new Vector3(0f, 0f, 90f), heli.transform);
            Part("TailWheel", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, -5.7f), new Vector3(0.32f, 0.12f, 0.32f), body, new Vector3(0f, 0f, 90f), heli.transform);

            // Main Rotor Hub & 4 Blades
            var rotorGo = new GameObject("MainRotor");
            rotorGo.transform.SetParent(heli.transform, false);
            rotorGo.transform.localPosition = new Vector3(0f, 2.55f, 0.15f);
            Part("Hub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.42f, 0.16f, 0.42f), steel, parent: rotorGo.transform);
            for (int i = 0; i < 4; i++)
            {
                var blade = Part("Blade_" + i, PrimitiveType.Cube, Vector3.zero, new Vector3(0.24f, 0.03f, 5.2f), steel, parent: rotorGo.transform);
                blade.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                blade.transform.localPosition = blade.transform.forward * 2.55f;
            }
            _mainRotor = rotorGo.transform;

            var tailRotorGo = new GameObject("TailRotor");
            tailRotorGo.transform.SetParent(heli.transform, false);
            tailRotorGo.transform.localPosition = new Vector3(0.22f, 2.55f, -6.15f);
            for (int i = 0; i < 2; i++)
            {
                var b = Part("TailBlade_" + i, PrimitiveType.Cube, Vector3.zero, new Vector3(0.03f, 1.35f, 0.14f), steel, new Vector3(i * 90f, 0f, 0f), tailRotorGo.transform);
                _ = b;
            }
            _tailRotor = tailRotorGo.transform;
        }

        void BuildOperator()
        {
            Material matSuit = GetMat("OpSuit", new Color(0.16f, 0.19f, 0.22f), 0.24f, 0.05f);
            Material matVest = GetMat("OpVest", new Color(0.12f, 0.14f, 0.15f), 0.30f, 0.12f);
            Material matPants = GetMat("OpPants", new Color(0.19f, 0.21f, 0.20f), 0.18f, 0f);
            Material matBoot = GetMat("OpBoot", new Color(0.08f, 0.08f, 0.09f), 0.35f, 0.10f);
            Material matSkin = GetMat("OpSkin", new Color(0.76f, 0.62f, 0.52f), 0.22f, 0f);
            Material matHelmet = GetMat("OpHelmet", new Color(0.13f, 0.15f, 0.16f), 0.38f, 0.25f);
            Material matGoggles = GetMat("OpGoggles", new Color(0.07f, 0.16f, 0.22f), 0.88f, 0.45f);
            Material matSteel = GetMat("OpGear", new Color(0.26f, 0.29f, 0.32f), 0.55f, 0.65f);

            var op = new GameObject("LobbyOperator");
            op.transform.SetParent(transform, false);
            op.transform.localPosition = new Vector3(0f, 0f, 0f);
            _operatorRoot = op.transform;

            // Boots, Legs & Knee Pads
            Part("BootL", PrimitiveType.Cube, new Vector3(-0.14f, 0.08f, 0.04f), new Vector3(0.13f, 0.16f, 0.28f), matBoot, parent: op.transform);
            Part("BootR", PrimitiveType.Cube, new Vector3(0.14f, 0.08f, -0.02f), new Vector3(0.13f, 0.16f, 0.28f), matBoot, new Vector3(0f, -10f, 0f), op.transform);
            Part("LegL", PrimitiveType.Capsule, new Vector3(-0.14f, 0.50f, 0.01f), new Vector3(0.16f, 0.42f, 0.17f), matPants, parent: op.transform);
            Part("LegR", PrimitiveType.Capsule, new Vector3(0.14f, 0.50f, -0.02f), new Vector3(0.16f, 0.42f, 0.17f), matPants, parent: op.transform);
            Part("KneeL", PrimitiveType.Cube, new Vector3(-0.14f, 0.50f, 0.09f), new Vector3(0.12f, 0.14f, 0.05f), matVest, parent: op.transform);
            Part("KneeR", PrimitiveType.Cube, new Vector3(0.14f, 0.50f, 0.06f), new Vector3(0.12f, 0.14f, 0.05f), matVest, parent: op.transform);

            // Torso, Plate Carrier & Pouches
            Part("Torso", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 0.56f, 0.24f), matSuit, parent: op.transform);
            Part("PlateCarrier", PrimitiveType.Cube, new Vector3(0f, 1.18f, 0.01f), new Vector3(0.36f, 0.44f, 0.28f), matVest, parent: op.transform);
            Part("MagPouch1", PrimitiveType.Cube, new Vector3(-0.09f, 1.04f, 0.16f), new Vector3(0.08f, 0.13f, 0.06f), matSteel, parent: op.transform);
            Part("MagPouch2", PrimitiveType.Cube, new Vector3(0f, 1.04f, 0.16f), new Vector3(0.08f, 0.13f, 0.06f), matSteel, parent: op.transform);
            Part("MagPouch3", PrimitiveType.Cube, new Vector3(0.09f, 1.04f, 0.16f), new Vector3(0.08f, 0.13f, 0.06f), matSteel, parent: op.transform);
            Part("ShoulderPadL", PrimitiveType.Cube, new Vector3(-0.24f, 1.36f, 0f), new Vector3(0.10f, 0.16f, 0.20f), matVest, new Vector3(0f, 0f, -10f), op.transform);
            Part("ShoulderPadR", PrimitiveType.Cube, new Vector3(0.24f, 1.36f, 0f), new Vector3(0.10f, 0.16f, 0.20f), matVest, new Vector3(0f, 0f, 10f), op.transform);

            // Articulated Arms & Tactical Gloves Cradling Weapon Across Chest
            Part("ArmR", PrimitiveType.Capsule, new Vector3(0.23f, 1.14f, 0.11f), new Vector3(0.11f, 0.24f, 0.11f), matSuit, new Vector3(44f, -22f, 12f), op.transform);
            Part("ArmL", PrimitiveType.Capsule, new Vector3(-0.20f, 1.16f, 0.16f), new Vector3(0.11f, 0.25f, 0.11f), matSuit, new Vector3(58f, 34f, -15f), op.transform);
            Part("GloveR", PrimitiveType.Cube, new Vector3(0.14f, 1.04f, 0.22f), new Vector3(0.09f, 0.08f, 0.10f), matBoot, parent: op.transform);
            Part("GloveL", PrimitiveType.Cube, new Vector3(-0.04f, 1.16f, 0.24f), new Vector3(0.09f, 0.08f, 0.10f), matBoot, parent: op.transform);

            // Head, Tactical Helmet, NVG Mount & Goggles
            Part("Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.46f, 0f), new Vector3(0.13f, 0.08f, 0.13f), matSkin, parent: op.transform);
            Part("Head", PrimitiveType.Sphere, new Vector3(0f, 1.59f, 0.01f), new Vector3(0.22f, 0.25f, 0.24f), matSkin, parent: op.transform);
            Part("HelmetShell", PrimitiveType.Sphere, new Vector3(0f, 1.63f, -0.01f), new Vector3(0.26f, 0.22f, 0.27f), matHelmet, parent: op.transform);
            Part("HelmetRim", PrimitiveType.Cube, new Vector3(0f, 1.60f, -0.02f), new Vector3(0.27f, 0.08f, 0.27f), matHelmet, parent: op.transform);
            Part("NvgMount", PrimitiveType.Cube, new Vector3(0f, 1.68f, 0.14f), new Vector3(0.07f, 0.06f, 0.05f), matSteel, parent: op.transform);
            Part("TacticalGoggles", PrimitiveType.Cube, new Vector3(0f, 1.61f, 0.11f), new Vector3(0.19f, 0.06f, 0.06f), matGoggles, parent: op.transform);

            var mountGo = new GameObject("WeaponDisplayMount");
            mountGo.transform.SetParent(op.transform, false);
            mountGo.transform.localPosition = new Vector3(0.04f, 1.13f, 0.23f);
            mountGo.transform.localRotation = Quaternion.Euler(28f, -52f, -12f);
            mountGo.transform.localScale = Vector3.one * 1.12f;
            _weaponMount = mountGo.transform;

            RefreshWeapon();
        }

        void Update()
        {
            if (_mainRotor != null) _mainRotor.Rotate(0f, 52f * Time.deltaTime, 0f, Space.Self);
            if (_tailRotor != null) _tailRotor.Rotate(95f * Time.deltaTime, 0f, 0f, Space.Self);

            WeaponDatabase db = WeaponDatabase.RuntimeFallback();
            int wantedIdx = InspectSecondary
                ? db.ResolveSidearmIndex(LocalProfile.SecondaryWeapon)
                : db.ClampPrimaryIndex(LocalProfile.PrimaryWeapon);
            if (wantedIdx != _shownWeaponIdx || InspectSecondary != _shownSecondary) RefreshWeapon();

            if (_operatorRoot != null)
            {
                float targetRot = _inspectMode ? _yawOffset : Mathf.LerpAngle(_yawOffset, 18f, Time.deltaTime * 3.5f);
                if (!_inspectMode) _yawOffset = targetRot;
                _operatorRoot.localRotation = Quaternion.Euler(0f, _yawOffset, 0f);
                _operatorRoot.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 1.3f) * 0.008f, 0f);
            }

            if (_cam != null && _cam.enabled)
            {
                Vector3 desiredPos = _inspectMode ? new Vector3(-0.08f, 1.24f, -1.65f) : new Vector3(0.02f, 1.34f, -2.85f);
                Vector3 lookTarget = _inspectMode ? new Vector3(0.02f, 1.12f, 0.20f) : new Vector3(0.0f, 1.12f, 0.45f);
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, desiredPos, Time.deltaTime * 5.5f);
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation(lookTarget - _cam.transform.position), Time.deltaTime * 5.5f);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _inspectMode ? 42f : 48f, Time.deltaTime * 5.5f);
            }
        }

        void RefreshWeapon()
        {
            if (_weaponMount == null) return;
            if (_currentWeaponGo != null) Destroy(_currentWeaponGo);

            WeaponDatabase db = WeaponDatabase.RuntimeFallback();
            _shownSecondary = InspectSecondary;
            _shownWeaponIdx = InspectSecondary
                ? db.ResolveSidearmIndex(LocalProfile.SecondaryWeapon)
                : db.ClampPrimaryIndex(LocalProfile.PrimaryWeapon);
            WeaponData w = db.Get(_shownWeaponIdx);
            if (w == null) return;

            var rig = WeaponModelBuilder.Build(w, _weaponMount, isOwnerViewmodel: false);
            _currentWeaponGo = rig.Root;
        }

        void ApplyNightHelipadEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.26f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.16f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.10f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.09f, 0.15f);
            RenderSettings.fogDensity = 0.028f;

            Light sun = FindAnyObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.intensity = 0.55f;
                sun.color = new Color(0.65f, 0.80f, 1.0f);
            }
        }

        GameObject Part(string partName, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = partName;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        void AddLight(string lightName, LightType type, Vector3 pos, Quaternion rot, Color col, float range, float intensity, float spotAngle)
        {
            var go = new GameObject(lightName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = col;
            l.range = range;
            l.intensity = intensity;
            if (type == LightType.Spot) l.spotAngle = spotAngle;
            l.shadows = LightShadows.None;
        }

        Material GetMat(string key, Color col, float smoothness, float metallic, Color? emission = null)
        {
            if (_mats.TryGetValue(key, out Material cached) && cached != null) return cached;
            Shader sh = (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultShader : null)
                        ?? Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = "Lobby_" + key, enableInstancing = true };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            else mat.color = col;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            _mats[key] = mat;
            return mat;
        }
    }
}
