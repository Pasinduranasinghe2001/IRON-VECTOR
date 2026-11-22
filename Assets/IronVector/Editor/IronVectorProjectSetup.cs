using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IronVector.Editor
{
    /// <summary>
    /// One-click project setup: generates URP assets, WeaponData ScriptableObjects,
    /// Player / ReconBuggy / MatchManager / CapturePoint prefabs, and the 3-lane Arena scene.
    /// </summary>
    public static class IronVectorProjectSetup
    {
        const string RootDir = "Assets/IronVector";
        const string PrefabsDir = RootDir + "/Prefabs";
        const string MaterialsDir = RootDir + "/Materials";
        const string WeaponsDir = RootDir + "/ScriptableObjects/Weapons";
        const string SettingsDir = RootDir + "/Settings";
        const string ScenesDir = RootDir + "/Scenes";
        const string ArenaScenePath = ScenesDir + "/Arena.unity";

        [InitializeOnLoadMethod]
        static void PromptFirstOpenSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (!FileExists(ArenaScenePath) && !SessionState.GetBool("iv_prompted", false))
                {
                    SessionState.SetBool("iv_prompted", true);
                    if (EditorUtility.DisplayDialog(
                        "IRON VECTOR Setup",
                        "Welcome to IRON VECTOR!\n\nClick 'Build Arena & Prefabs' to generate the URP pipeline, weapons, networked prefabs, and the 3-lane Arena scene now.",
                        "Build Arena & Prefabs",
                        "Later"))
                    {
                        BuildAll();
                    }
                }
            };
        }

        [MenuItem("Iron Vector/Build Arena & Prefabs (One-Click Setup)", priority = 1)]
        public static void BuildAll()
        {
            EnsureFolders();
            ConfigureProjectSettings();
            SetupURP();

            WeaponDatabase db = CreateWeaponAssets();

            NetworkObject playerPrefab = CreatePlayerPrefab(db);
            NetworkObject buggyPrefab = CreateBuggyPrefab();
            NetworkObject matchManagerPrefab = CreateMatchManagerPrefab();
            NetworkObject capturePointPrefab = CreateCapturePointPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Populate FishNet's DefaultPrefabObjects cleanly.
            EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs");
            PrefabObjects spawnables = EnsureDefaultPrefabObjects(playerPrefab, buggyPrefab, matchManagerPrefab, capturePointPrefab);

            BuildArenaScene(playerPrefab, buggyPrefab, matchManagerPrefab, capturePointPrefab, spawnables);

            Debug.Log("[IRON VECTOR] Setup complete! Open Assets/IronVector/Scenes/Arena.unity and press Play.");
        }

        static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "IronVector";
            PlayerSettings.productName = "IRON VECTOR";
            PlayerSettings.runInBackground = true; // Required for testing Host + Client locally.
        }

        static void SetupURP()
        {
            string rendererPath = SettingsDir + "/IronVectorRenderer.asset";
            string pipelinePath = SettingsDir + "/IronVectorURP.asset";

            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "IronVectorRenderer";
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }

            // Wire default PostProcessData if missing so Bloom / Tonemapping render cleanly.
            SerializedObject rSo = new SerializedObject(rendererData);
            SerializedProperty ppProp = rSo.FindProperty("m_PostProcessData") ?? rSo.FindProperty("postProcessData");
            if (ppProp != null && ppProp.objectReferenceValue == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:PostProcessData");
                if (guids.Length > 0)
                {
                    ppProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    rSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rendererData);
                }
            }

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "IronVectorURP";
                pipeline.shadowDistance = 120f;
                pipeline.shadowCascadeCount = 2;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static WeaponDatabase CreateWeaponAssets()
        {
            WeaponData[] defaults = WeaponDatabase.CreateDefaultRuntimeAssets();
            var saved = new WeaponData[defaults.Length];

            for (int i = 0; i < defaults.Length; i++)
            {
                string safeName = defaults[i].weaponName.Replace(" ", "_");
                string path = $"{WeaponsDir}/{safeName}.asset";
                WeaponData existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (existing != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(defaults[i], path);
                saved[i] = defaults[i];
            }

            string dbPath = WeaponsDir + "/WeaponDatabase.asset";
            WeaponDatabase db = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<WeaponDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }
            db.weapons = saved;
            db.sidearmIndex = 4;
            EditorUtility.SetDirty(db);
            return db;
        }

        static NetworkObject CreatePlayerPrefab(WeaponDatabase db)
        {
            Material matBody = GetOrCreateMat("Mat_PlayerBody", new Color(0.28f, 0.32f, 0.36f), 0.25f);
            Material matHead = GetOrCreateMat("Mat_PlayerHead", new Color(0.82f, 0.72f, 0.62f), 0.2f);

            var root = new GameObject("Player");
            NetworkObject nob = root.AddComponent<NetworkObject>();
            NetworkTransform rootNt = root.AddComponent<NetworkTransform>();
            rootNt.SetSynchronizeScale(false);

            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.04f;
            cc.stepOffset = 0.35f;

            var id = root.AddComponent<PlayerIdentity>();
            var health = root.AddComponent<PlayerHealth>();
            var motor = root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();
            var weapons = root.AddComponent<WeaponController>();
            root.AddComponent<GrenadeThrower>();
            root.AddComponent<PlayerVehicleInteraction>();

            // Body visual (no collider; CharacterController owns body collisions)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = matBody;

            // Head + Trigger Hitbox
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = Vector3.one * 0.38f;
            head.GetComponent<Collider>().isTrigger = true;
            head.GetComponent<Renderer>().sharedMaterial = matHead;
            var hb = head.AddComponent<Hitbox>();
            hb.isHead = true;

            // Visor accent so facing direction is readable on teammates & enemies
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.name = "Visor";
            visor.transform.SetParent(head.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.06f, 0.42f);
            visor.transform.localScale = new Vector3(0.68f, 0.24f, 0.25f);
            visor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMat("Mat_Visor", new Color(0.10f, 0.12f, 0.14f), 0.75f, 0.4f);

            // Camera Pivot (synced child NetworkTransform shares look pitch & crouch height)
            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            NetworkTransform pivotNt = pivot.AddComponent<NetworkTransform>();
            pivotNt.SetSynchronizeScale(false);

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 450f;
            cam.enabled = false;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var listener = camGo.AddComponent<AudioListener>();
            listener.enabled = false;

            var weaponHolder = new GameObject("WeaponHolder");
            weaponHolder.transform.SetParent(pivot.transform, false);
            weaponHolder.transform.localPosition = new Vector3(0.24f, -0.22f, 0.45f);

            motor.cameraPivot = pivot.transform;
            motor.bodyVisual = body.transform;
            motor.head = head.transform;
            look.cameraPivot = pivot.transform;
            look.playerCamera = cam;
            weapons.database = db;
            weapons.weaponHolder = weaponHolder.transform;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabsDir + "/Player.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        static NetworkObject CreateBuggyPrefab()
        {
            Material matHull = GetOrCreateMat("Mat_BuggyHull", new Color(0.29f, 0.31f, 0.24f), 0.35f, 0.2f);
            Material matDark = GetOrCreateMat("Mat_BuggyFrame", new Color(0.12f, 0.13f, 0.14f), 0.45f, 0.5f);
            Material matTire = GetOrCreateMat("Mat_Tire", new Color(0.08f, 0.08f, 0.09f), 0.15f);
            Material matLight = GetOrCreateMat("Mat_Headlight", new Color(1f, 0.95f, 0.78f), 0.8f, 0f, new Color(2f, 1.85f, 1.4f));

            var root = new GameObject("ReconBuggy");
            root.AddComponent<NetworkObject>();
            NetworkTransform nt = root.AddComponent<NetworkTransform>();
            nt.SetSynchronizeScale(false);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 950f;
            rb.linearDamping = 0.08f;
            rb.angularDamping = 0.65f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.72f, 0f);
            col.size = new Vector3(1.85f, 0.72f, 3.65f);

            var vc = root.AddComponent<VehicleController>();
            root.AddComponent<VehicleHealth>();

            BuildPart(root.transform, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(1.75f, 0.48f, 3.55f), matHull);
            BuildPart(root.transform, "Hood", PrimitiveType.Cube, new Vector3(0f, 0.92f, 1.05f), new Vector3(1.45f, 0.26f, 1.35f), matHull);
            BuildPart(root.transform, "RearBed", PrimitiveType.Cube, new Vector3(0f, 0.95f, -1.15f), new Vector3(1.62f, 0.30f, 1.05f), matDark);
            BuildPart(root.transform, "RollCageTop", PrimitiveType.Cube, new Vector3(0f, 1.72f, -0.15f), new Vector3(1.42f, 0.08f, 1.55f), matDark);
            BuildPart(root.transform, "BumperFront", PrimitiveType.Cube, new Vector3(0f, 0.56f, 1.84f), new Vector3(1.78f, 0.22f, 0.20f), matDark);
            BuildPart(root.transform, "HeadlightL", PrimitiveType.Cube, new Vector3(-0.56f, 0.82f, 1.78f), new Vector3(0.24f, 0.12f, 0.08f), matLight);
            BuildPart(root.transform, "HeadlightR", PrimitiveType.Cube, new Vector3(0.56f, 0.82f, 1.78f), new Vector3(0.24f, 0.12f, 0.08f), matLight);

            var seat = new GameObject("Seat");
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.72f, -0.05f);
            vc.seat = seat.transform;

            vc.wheelMeshes = new Transform[4];
            Vector3[] visualWheelPos =
            {
                new Vector3(-0.95f, 0.38f, 1.32f),
                new Vector3(0.95f, 0.38f, 1.32f),
                new Vector3(-0.95f, 0.38f, -1.32f),
                new Vector3(0.95f, 0.38f, -1.32f)
            };
            for (int i = 0; i < 4; i++)
            {
                var wheelPivot = new GameObject("Wheel_" + i);
                wheelPivot.transform.SetParent(root.transform, false);
                wheelPivot.transform.localPosition = visualWheelPos[i];

                var wheelCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(wheelCyl.GetComponent<Collider>());
                wheelCyl.name = "Tire";
                wheelCyl.transform.SetParent(wheelPivot.transform, false);
                wheelCyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelCyl.transform.localScale = new Vector3(0.76f, 0.16f, 0.76f);
                wheelCyl.GetComponent<Renderer>().sharedMaterial = matTire;

                vc.wheelMeshes[i] = wheelPivot.transform;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabsDir + "/ReconBuggy.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        static NetworkObject CreateMatchManagerPrefab()
        {
            var root = new GameObject("MatchManager");
            root.AddComponent<NetworkObject>();
            root.AddComponent<MatchManager>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabsDir + "/MatchManager.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        static NetworkObject CreateCapturePointPrefab()
        {
            Material matRing = GetOrCreateMat("Mat_CaptureRing", new Color(0.85f, 0.85f, 0.85f), 0.1f);
            Material matPole = GetOrCreateMat("Mat_CapturePole", new Color(0.20f, 0.22f, 0.25f), 0.5f, 0.7f);
            Material matBeacon = GetOrCreateMat("Mat_CaptureBeacon", new Color(0.9f, 0.9f, 0.9f), 0.2f, 0f, Color.white);

            var root = new GameObject("CapturePoint");
            root.AddComponent<NetworkObject>();
            var cp = root.AddComponent<CapturePoint>();

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.name = "CaptureRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(14.5f, 0.025f, 14.5f);
            var ringR = ring.GetComponent<Renderer>();
            ringR.sharedMaterial = matRing;
            ringR.shadowCastingMode = ShadowCastingMode.Off;

            BuildPart(root.transform, "FlagPole", PrimitiveType.Cylinder, new Vector3(0f, 3.0f, 0f), new Vector3(0.14f, 3.0f, 0.14f), matPole);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.name = "Beacon";
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localPosition = new Vector3(0f, 6.2f, 0f);
            beacon.transform.localScale = Vector3.one * 0.75f;
            var beaconR = beacon.GetComponent<Renderer>();
            beaconR.sharedMaterial = matBeacon;

            cp.ringRenderer = ringR;
            cp.beaconRenderer = beaconR;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabsDir + "/CapturePoint.prefab");
            Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkObject>();
        }

        static PrefabObjects EnsureDefaultPrefabObjects(params NetworkObject[] required)
        {
            string[] guids = AssetDatabase.FindAssets("t:DefaultPrefabObjects");
            DefaultPrefabObjects dpo = guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;

            if (dpo == null) return null;
            foreach (NetworkObject nob in required)
                if (nob != null) dpo.AddObject(nob, checkForDuplicates: true);

            EditorUtility.SetDirty(dpo);
            AssetDatabase.SaveAssets();
            return dpo;
        }

        static void BuildArenaScene(
            NetworkObject playerPrefab,
            NetworkObject buggyPrefab,
            NetworkObject matchManagerPrefab,
            NetworkObject capturePointPrefab,
            PrefabObjects spawnables)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Materials
            Material matFloor = GetOrCreateMat("Mat_ArenaFloor", new Color(0.46f, 0.44f, 0.40f), 0.12f);
            Material matRoad = GetOrCreateMat("Mat_Asphalt", new Color(0.20f, 0.21f, 0.23f), 0.18f);
            Material matWall = GetOrCreateMat("Mat_Concrete", new Color(0.56f, 0.55f, 0.52f), 0.10f);
            Material matSteel = GetOrCreateMat("Mat_Steel", new Color(0.32f, 0.35f, 0.38f), 0.55f, 0.4f);
            Material matContainerRed = GetOrCreateMat("Mat_ContainerRust", new Color(0.52f, 0.24f, 0.18f), 0.25f);
            Material matContainerBlue = GetOrCreateMat("Mat_ContainerNavy", new Color(0.18f, 0.29f, 0.40f), 0.25f);
            Material matAlphaTint = GetOrCreateMat("Mat_AlphaTrim", Teams.TeamColor(Teams.Alpha), 0.35f);
            Material matBravoTint = GetOrCreateMat("Mat_BravoTrim", Teams.TeamColor(Teams.Bravo), 0.35f);

            // Lighting
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.74f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.47f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.9f;
            sun.color = new Color(1.0f, 0.96f, 0.89f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

            // Post-Processing Volume
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, SettingsDir + "/ArenaVolumeProfile.asset");

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.42f);
            bloom.threshold.Override(1.05f);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.Override(10f);
            colorAdj.saturation.Override(8f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.18f);

            vol.sharedProfile = profile;
            EditorUtility.SetDirty(profile);

            // Arena Geometry
            var map = new GameObject("Map_OutpostVector");

            // Ground & North Vehicle Road
            Block(map.transform, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 100f), matFloor);
            Block(map.transform, "NorthRoad", new Vector3(0f, 0.02f, 28f), new Vector3(114f, 0.06f, 14f), matRoad);

            // Perimeter Walls
            Block(map.transform, "Wall_North", new Vector3(0f, 4f, 49f), new Vector3(130f, 8f, 2f), matWall);
            Block(map.transform, "Wall_South", new Vector3(0f, 4f, -49f), new Vector3(130f, 8f, 2f), matWall);
            Block(map.transform, "Wall_West", new Vector3(-64f, 4f, 0f), new Vector3(2f, 8f, 100f), matWall);
            Block(map.transform, "Wall_East", new Vector3(64f, 4f, 0f), new Vector3(2f, 8f, 100f), matWall);

            // Alpha Base (West, X = -50) & Bravo Base (East, X = +50)
            Block(map.transform, "AlphaBunkerBack", new Vector3(-56f, 2.2f, 0f), new Vector3(2f, 4.4f, 26f), matWall);
            Block(map.transform, "AlphaTrim", new Vector3(-54.8f, 3.8f, 0f), new Vector3(0.4f, 0.5f, 24f), matAlphaTint);
            Block(map.transform, "AlphaBarrierN", new Vector3(-43f, 1.1f, 10f), new Vector3(1.5f, 2.2f, 8f), matWall);
            Block(map.transform, "AlphaBarrierS", new Vector3(-43f, 1.1f, -10f), new Vector3(1.5f, 2.2f, 8f), matWall);

            Block(map.transform, "BravoBunkerBack", new Vector3(56f, 2.2f, 0f), new Vector3(2f, 4.4f, 26f), matWall);
            Block(map.transform, "BravoTrim", new Vector3(54.8f, 3.8f, 0f), new Vector3(0.4f, 0.5f, 24f), matBravoTint);
            Block(map.transform, "BravoBarrierN", new Vector3(43f, 1.1f, 10f), new Vector3(1.5f, 2.2f, 8f), matWall);
            Block(map.transform, "BravoBarrierS", new Vector3(43f, 1.1f, -10f), new Vector3(1.5f, 2.2f, 8f), matWall);

            // Center Lane: Two-Tier Bridge / Warehouse Hub
            Block(map.transform, "CenterDeck", new Vector3(0f, 3.6f, 0f), new Vector3(18f, 0.6f, 12f), matSteel);
            Block(map.transform, "CenterPillarNW", new Vector3(-7.5f, 1.8f, 4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterPillarNE", new Vector3(7.5f, 1.8f, 4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterPillarSW", new Vector3(-7.5f, 1.8f, -4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterPillarSE", new Vector3(7.5f, 1.8f, -4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterCoverW", new Vector3(-16f, 0.85f, 0f), new Vector3(2.2f, 1.7f, 6f), matWall);
            Block(map.transform, "CenterCoverE", new Vector3(16f, 0.85f, 0f), new Vector3(2.2f, 1.7f, 6f), matWall);

            // Ramps up to Center Deck
            Ramp(map.transform, "RampWest", new Vector3(-14.5f, 1.8f, 0f), new Vector3(12f, 0.5f, 4.2f), new Vector3(0f, 0f, 17f), matSteel);
            Ramp(map.transform, "RampEast", new Vector3(14.5f, 1.8f, 0f), new Vector3(12f, 0.5f, 4.2f), new Vector3(0f, 0f, -17f), matSteel);
            Block(map.transform, "DeckParapetN", new Vector3(0f, 4.45f, 5.6f), new Vector3(16f, 1.1f, 0.4f), matSteel);
            Block(map.transform, "DeckParapetS", new Vector3(0f, 4.45f, -5.6f), new Vector3(16f, 1.1f, 0.4f), matSteel);

            // North Vehicle Lane: Ramps + Low Barricades
            Ramp(map.transform, "NorthJumpW", new Vector3(-12f, 0.5f, 28f), new Vector3(6f, 0.4f, 6f), new Vector3(0f, 0f, 11f), matSteel);
            Ramp(map.transform, "NorthJumpE", new Vector3(12f, 0.5f, 28f), new Vector3(6f, 0.4f, 6f), new Vector3(0f, 0f, -11f), matSteel);
            Block(map.transform, "NorthCoverA", new Vector3(-25f, 0.9f, 20f), new Vector3(7f, 1.8f, 1.6f), matWall);
            Block(map.transform, "NorthCoverB", new Vector3(25f, 0.9f, 20f), new Vector3(7f, 1.8f, 1.6f), matWall);

            // South CQB Cargo Yard (Containers + Flank Corridors)
            Block(map.transform, "Container1", new Vector3(-14f, 1.5f, -26f), new Vector3(7.5f, 3.0f, 2.8f), matContainerRed);
            Block(map.transform, "Container2", new Vector3(14f, 1.5f, -26f), new Vector3(7.5f, 3.0f, 2.8f), matContainerBlue);
            Block(map.transform, "Container3", new Vector3(0f, 1.5f, -35f), new Vector3(8.5f, 3.0f, 2.8f), matContainerRed);
            Block(map.transform, "Container4", new Vector3(-26f, 1.5f, -20f), new Vector3(2.8f, 3.0f, 7.5f), matContainerBlue);
            Block(map.transform, "Container5", new Vector3(26f, 1.5f, -20f), new Vector3(2.8f, 3.0f, 7.5f), matContainerRed);
            Block(map.transform, "CqbCrates", new Vector3(0f, 0.75f, -20f), new Vector3(3.2f, 1.5f, 3.2f), matWall);

            // Spawn Points & Map Markers
            var markers = new GameObject("MapMarkers");
            Transform[] alphaSpawns =
            {
                Marker(markers.transform, "AlphaSpawn_1", new Vector3(-50f, 0.1f, -6f), Quaternion.Euler(0, 90, 0)),
                Marker(markers.transform, "AlphaSpawn_2", new Vector3(-50f, 0.1f, 0f), Quaternion.Euler(0, 90, 0)),
                Marker(markers.transform, "AlphaSpawn_3", new Vector3(-50f, 0.1f, 6f), Quaternion.Euler(0, 90, 0)),
                Marker(markers.transform, "AlphaSpawn_4", new Vector3(-47f, 0.1f, -15f), Quaternion.Euler(0, 75, 0))
            };
            Transform[] bravoSpawns =
            {
                Marker(markers.transform, "BravoSpawn_1", new Vector3(50f, 0.1f, -6f), Quaternion.Euler(0, -90, 0)),
                Marker(markers.transform, "BravoSpawn_2", new Vector3(50f, 0.1f, 0f), Quaternion.Euler(0, -90, 0)),
                Marker(markers.transform, "BravoSpawn_3", new Vector3(50f, 0.1f, 6f), Quaternion.Euler(0, -90, 0)),
                Marker(markers.transform, "BravoSpawn_4", new Vector3(47f, 0.1f, 15f), Quaternion.Euler(0, -105, 0))
            };
            Transform[] vehicleSpawns =
            {
                Marker(markers.transform, "BuggySpawn_Alpha", new Vector3(-42f, 0.1f, 28f), Quaternion.Euler(0, 90, 0)),
                Marker(markers.transform, "BuggySpawn_Bravo", new Vector3(42f, 0.1f, 28f), Quaternion.Euler(0, -90, 0))
            };
            Transform[] captureSpawns =
            {
                Marker(markers.transform, "CapturePoint_A", new Vector3(0f, 0f, 28f), Quaternion.identity),
                Marker(markers.transform, "CapturePoint_B", new Vector3(0f, 0f, 0f), Quaternion.identity),
                Marker(markers.transform, "CapturePoint_C", new Vector3(0f, 0f, -26f), Quaternion.identity)
            };

            // Menu Overview Camera
            var menuCamGo = new GameObject("MenuCamera");
            menuCamGo.transform.position = new Vector3(-28f, 26f, -44f);
            menuCamGo.transform.LookAt(new Vector3(0f, 2f, 4f));
            Camera menuCam = menuCamGo.AddComponent<Camera>();
            menuCam.farClipPlane = 450f;
            var menuCamData = menuCamGo.AddComponent<UniversalAdditionalCameraData>();
            menuCamData.renderPostProcessing = true;

            // NetworkManager + Bootstrap
            var netGo = new GameObject("NetworkManager");
            NetworkManager nm = netGo.AddComponent<NetworkManager>();
            Tugboat tugboat = netGo.AddComponent<Tugboat>();
            nm.TransportManager.Transport = tugboat;
            if (spawnables != null) nm.SpawnablePrefabs = spawnables;

            SerializedObject nmSo = new SerializedObject(nm);
            SerializedProperty refreshProp = nmSo.FindProperty("_refreshDefaultPrefabs");
            if (refreshProp != null)
            {
                refreshProp.boolValue = true;
                nmSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var server = netGo.AddComponent<NetworkGameServer>();
            server.networkManager = nm;
            server.playerPrefab = playerPrefab;
            server.matchManagerPrefab = matchManagerPrefab;
            server.vehiclePrefab = buggyPrefab;
            server.capturePointPrefab = capturePointPrefab;
            server.vehicleSpawns = vehicleSpawns;
            server.capturePoints = captureSpawns;

            var spawnMgr = netGo.AddComponent<SpawnManager>();
            spawnMgr.alphaSpawns = alphaSpawns;
            spawnMgr.bravoSpawns = bravoSpawns;

            var menuUi = netGo.AddComponent<MainMenuUI>();
            menuUi.networkManager = nm;
            menuUi.menuCamera = menuCam;
            netGo.AddComponent<GameHUD>();

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ArenaScenePath, true) };
        }

        static void BuildPart(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void Ramp(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            var go = Block(parent, name, pos, scale, mat);
            go.transform.localRotation = Quaternion.Euler(euler);
        }

        static Transform Marker(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go.transform;
        }

        static Material GetOrCreateMat(string name, Color color, float smoothness = 0.2f, float metallic = 0f, Color? emission = null)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolders()
        {
            CreateFolderIfNeeded("Assets", "IronVector");
            CreateFolderIfNeeded(RootDir, "Prefabs");
            CreateFolderIfNeeded(RootDir, "Materials");
            CreateFolderIfNeeded(RootDir, "ScriptableObjects");
            CreateFolderIfNeeded(RootDir + "/ScriptableObjects", "Weapons");
            CreateFolderIfNeeded(RootDir, "Settings");
            CreateFolderIfNeeded(RootDir, "Scenes");
        }

        static void CreateFolderIfNeeded(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        static bool FileExists(string assetPath) => System.IO.File.Exists(assetPath);
    }
}
