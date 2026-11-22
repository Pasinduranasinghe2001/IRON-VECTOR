using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
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
    /// One-click project setup: generates URP assets, procedural PBR textures, 9 COD4 WeaponData assets,
    /// Player / ReconBuggy / MatchManager / CapturePoint / BombSite prefabs,
    /// MainMenu.unity (Boot Scene), AdminHall.unity (COD4 HQ Interior Map), and Arena.unity (Outpost Vector).
    /// </summary>
    public static class IronVectorProjectSetup
    {
        const string RootDir = "Assets/IronVector";
        const string PrefabsDir = RootDir + "/Prefabs";
        const string MaterialsDir = RootDir + "/Materials";
        const string TexturesDir = RootDir + "/Textures";
        const string WeaponsDir = RootDir + "/ScriptableObjects/Weapons";
        const string SettingsDir = RootDir + "/Settings";
        const string ScenesDir = RootDir + "/Scenes";
        const string MainMenuScenePath = ScenesDir + "/MainMenu.unity";
        const string AdminHallScenePath = ScenesDir + "/AdminHall.unity";
        const string ArenaScenePath = ScenesDir + "/Arena.unity";

        [InitializeOnLoadMethod]
        static void PromptFirstOpenSetup()
        {
            if (Application.isBatchMode) return;
            EnsureGICacheOnDDrive(silent: true);
            EditorApplication.delayCall += () =>
            {
                if ((!FileExists(MainMenuScenePath) || !FileExists(AdminHallScenePath)) && !SessionState.GetBool("iv_prompted_v2", false))
                {
                    SessionState.SetBool("iv_prompted_v2", true);
                    if (EditorUtility.DisplayDialog(
                        "IRON VECTOR — Update Available",
                        "Build or rebuild the updated project assets now?\n\nThis creates MainMenu, the new AdminHall HQ map, Outpost Vector, 9 COD4 guns, BombSite prefab, PBR textures, and Password/LAN networking.",
                        "Build All Maps & Assets",
                        "Later"))
                    {
                        BuildAll();
                    }
                }
            };
        }

        [MenuItem("Iron Vector/Redirect GI Cache to D Drive", priority = 20)]
        public static void MenuRedirectGICache() => EnsureGICacheOnDDrive(silent: false);

        static void EnsureGICacheOnDDrive(bool silent)
        {
            if (!System.IO.Directory.Exists("D:\\")) return;
            if (silent && EditorPrefs.GetBool("iv_gi_d_configured", false)) return;

            const string giPath = "D:/Unity/GICache";
            System.IO.Directory.CreateDirectory(giPath);
            EditorPrefs.SetBool("GICacheEnableCustomPath", true);
            EditorPrefs.SetString("GICacheFolder", giPath);
            EditorPrefs.SetBool("iv_gi_d_configured", true);
            if (!silent)
                Debug.Log($"[IRON VECTOR] Unity GI Cache redirected to {giPath}.");
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
            NetworkObject bombSitePrefab = CreateBombSitePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs");
            PrefabObjects spawnables = EnsureDefaultPrefabObjects(playerPrefab, buggyPrefab, matchManagerPrefab, capturePointPrefab, bombSitePrefab);

            BuildMainMenuScene(playerPrefab, buggyPrefab, matchManagerPrefab, capturePointPrefab, bombSitePrefab, spawnables);
            BuildAdminHallScene();
            BuildArenaScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(AdminHallScenePath, true),
                new EditorBuildSettingsScene(ArenaScenePath, true)
            };

            EditorSceneManager.OpenScene(MainMenuScenePath);
            Debug.Log("[IRON VECTOR] Complete! Press Play from MainMenu (or AdminHall / Arena) to play.");
        }

        static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "IronVector";
            PlayerSettings.productName = "IRON VECTOR";
            PlayerSettings.runInBackground = true;
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
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            pipeline.shadowDistance = 125f;
            pipeline.shadowCascadeCount = 2;
            pipeline.msaaSampleCount = 4;
            pipeline.mainLightShadowmapResolution = 2048;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static WeaponDatabase CreateWeaponAssets()
        {
            WeaponData[] defaults = WeaponDatabase.CreateDefaultRuntimeAssets();
            var saved = new WeaponData[defaults.Length];

            for (int i = 0; i < defaults.Length; i++)
            {
                string safeName = defaults[i].weaponName.Replace(" ", "_").Replace("/", "_");
                string path = $"{WeaponsDir}/{safeName}.asset";
                if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null) AssetDatabase.DeleteAsset(path);
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
            db.primaryCount = 7;
            db.firstSidearmIndex = 7;
            EditorUtility.SetDirty(db);
            return db;
        }

        static NetworkObject CreatePlayerPrefab(WeaponDatabase db)
        {
            Material matBody = GetOrCreatePbrMat("Mat_PlayerBody", new Color(0.26f, 0.30f, 0.34f), 0.28f);
            Material matHead = GetOrCreatePbrMat("Mat_PlayerHead", new Color(0.82f, 0.72f, 0.62f), 0.2f);
            Material matVisor = GetOrCreatePbrMat("Mat_Visor", new Color(0.08f, 0.10f, 0.12f), 0.8f, 0.5f);

            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();
            NetworkTransform rootNt = root.AddComponent<NetworkTransform>();
            rootNt.SetSynchronizeScale(false);

            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.04f;
            cc.stepOffset = 0.38f;

            root.AddComponent<PlayerIdentity>();
            root.AddComponent<PlayerHealth>();
            var motor = root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();
            var weapons = root.AddComponent<WeaponController>();
            root.AddComponent<GrenadeThrower>();
            root.AddComponent<PlayerVehicleInteraction>();
            root.AddComponent<PlayerBombInteraction>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = matBody;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = Vector3.one * 0.38f;
            head.GetComponent<Collider>().isTrigger = true;
            head.GetComponent<Renderer>().sharedMaterial = matHead;
            head.AddComponent<Hitbox>().isHead = true;

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.name = "Visor";
            visor.transform.SetParent(head.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.06f, 0.42f);
            visor.transform.localScale = new Vector3(0.68f, 0.24f, 0.25f);
            visor.GetComponent<Renderer>().sharedMaterial = matVisor;

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
            Material matHull = GetOrCreatePbrMat("Mat_BuggyHull", new Color(0.29f, 0.31f, 0.24f), 0.35f, 0.25f);
            Material matDark = GetOrCreatePbrMat("Mat_BuggyFrame", new Color(0.12f, 0.13f, 0.14f), 0.45f, 0.5f);
            Material matTire = GetOrCreatePbrMat("Mat_Tire", new Color(0.08f, 0.08f, 0.09f), 0.15f);
            Material matLight = GetOrCreatePbrMat("Mat_Headlight", new Color(1f, 0.95f, 0.78f), 0.8f, 0f, new Color(2f, 1.85f, 1.4f));

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
            Material matRing = GetOrCreatePbrMat("Mat_CaptureRing", new Color(0.85f, 0.85f, 0.85f), 0.1f);
            Material matPole = GetOrCreatePbrMat("Mat_CapturePole", new Color(0.20f, 0.22f, 0.25f), 0.5f, 0.7f);
            Material matBeacon = GetOrCreatePbrMat("Mat_CaptureBeacon", new Color(0.9f, 0.9f, 0.9f), 0.2f, 0f, Color.white);

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

        static NetworkObject CreateBombSitePrefab()
        {
            Material matRing = GetOrCreatePbrMat("Mat_BombSiteRing", new Color(0.95f, 0.72f, 0.22f), 0.15f);
            Material matCrate = GetOrCreatePbrMat("Mat_BombCrate", new Color(0.75f, 0.56f, 0.18f), 0.3f);

            var root = new GameObject("BombSite");
            root.AddComponent<NetworkObject>();
            var bs = root.AddComponent<BombSite>();

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.name = "SiteZone";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(10.5f, 0.025f, 10.5f);
            var ringR = ring.GetComponent<Renderer>();
            ringR.sharedMaterial = matRing;
            ringR.shadowCastingMode = ShadowCastingMode.Off;

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "ObjectiveCrate";
            crate.transform.SetParent(root.transform, false);
            crate.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            crate.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f);
            var crateR = crate.GetComponent<Renderer>();
            crateR.sharedMaterial = matCrate;

            bs.ringRenderer = ringR;
            bs.crateRenderer = crateR;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabsDir + "/BombSite.prefab");
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

        static void BuildMainMenuScene(
            NetworkObject playerPrefab,
            NetworkObject buggyPrefab,
            NetworkObject matchManagerPrefab,
            NetworkObject capturePointPrefab,
            NetworkObject bombSitePrefab,
            PrefabObjects spawnables)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment(new Color(0.45f, 0.54f, 0.66f), new Color(0.22f, 0.25f, 0.30f), 42f, -35f, 1.6f, "MainMenuVolumeProfile");

            // Subtle architectural backdrop for the Lobby Camera
            Material matFloor = GetOrCreateTileMat("Mat_MarbleTile", new Color(0.55f, 0.56f, 0.58f), new Color(0.45f, 0.46f, 0.48f), 0.55f, 18f);
            Material matAccent = GetOrCreatePbrMat("Mat_Steel", new Color(0.24f, 0.27f, 0.31f), 0.55f, 0.5f);
            Block(null, "MenuStage", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), matFloor);
            Block(null, "MonumentA", new Vector3(-6f, 2.5f, 8f), new Vector3(2f, 5f, 2f), matAccent);
            Block(null, "MonumentB", new Vector3(6f, 3.2f, 10f), new Vector3(2.5f, 6.4f, 2.5f), matAccent);

            var menuCamGo = new GameObject("MenuCamera");
            menuCamGo.tag = "MainCamera";
            menuCamGo.transform.position = new Vector3(0f, 4.5f, -8f);
            menuCamGo.transform.LookAt(new Vector3(0f, 2.2f, 8f));
            Camera menuCam = menuCamGo.AddComponent<Camera>();
            menuCam.farClipPlane = 400f;
            var menuCamData = menuCamGo.AddComponent<UniversalAdditionalCameraData>();
            menuCamData.renderPostProcessing = true;
            menuCamGo.AddComponent<AudioListener>();

            var netGo = new GameObject("NetworkManager");
            NetworkManager nm = netGo.AddComponent<NetworkManager>();
            TransportManager tm = netGo.GetComponent<TransportManager>() ?? netGo.AddComponent<TransportManager>();
            Tugboat tugboat = netGo.GetComponent<Tugboat>() ?? netGo.AddComponent<Tugboat>();
            tm.Transport = tugboat;
            if (spawnables != null) nm.SpawnablePrefabs = spawnables;

            var auth = netGo.AddComponent<RoomPasswordAuthenticator>();
            var smComp = netGo.GetComponent<FishNet.Managing.Server.ServerManager>() ?? netGo.AddComponent<FishNet.Managing.Server.ServerManager>();
            smComp.SetAuthenticator(auth);

            SerializedObject nmSo = new SerializedObject(nm);
            SerializedProperty refreshProp = nmSo.FindProperty("_refreshDefaultPrefabs");
            if (refreshProp != null)
            {
                refreshProp.boolValue = true;
                nmSo.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(tm);
            EditorUtility.SetDirty(smComp);
            EditorUtility.SetDirty(nm);

            var server = netGo.AddComponent<NetworkGameServer>();
            server.networkManager = nm;
            server.playerPrefab = playerPrefab;
            server.matchManagerPrefab = matchManagerPrefab;
            server.vehiclePrefab = buggyPrefab;
            server.capturePointPrefab = capturePointPrefab;
            server.bombSitePrefab = bombSitePrefab;

            var discovery = netGo.AddComponent<LanDiscovery>();
            var menuUi = netGo.AddComponent<MainMenuUI>();
            menuUi.networkManager = nm;
            menuUi.passwordAuth = auth;
            menuUi.lanDiscovery = discovery;
            menuUi.menuCamera = menuCam;
            netGo.AddComponent<GameHUD>();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        /// <summary>Builds Map 1: AdminHall — COD4-style HQ Atrium, Two-Floor Balconies, Offices, Server Room &amp; Plaza.</summary>
        static void BuildAdminHallScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment(new Color(0.68f, 0.77f, 0.88f), new Color(0.45f, 0.44f, 0.42f), 50f, -28f, 2.0f, "AdminHallVolumeProfile");

            Material matMarble = GetOrCreateTileMat("Mat_AdminMarble", new Color(0.72f, 0.71f, 0.68f), new Color(0.58f, 0.57f, 0.54f), 0.58f, 26f);
            Material matConcrete = GetOrCreateTileMat("Mat_ConcreteGrid", new Color(0.56f, 0.55f, 0.52f), new Color(0.47f, 0.46f, 0.44f), 0.14f, 18f);
            Material matPlaster = GetOrCreatePbrMat("Mat_AdminWall", new Color(0.76f, 0.74f, 0.70f), 0.18f);
            Material matWood = GetOrCreateTileMat("Mat_AdminWood", new Color(0.42f, 0.25f, 0.14f), new Color(0.32f, 0.18f, 0.10f), 0.42f, 8f);
            Material matSteel = GetOrCreatePbrMat("Mat_Steel", new Color(0.28f, 0.31f, 0.35f), 0.55f, 0.45f);
            Material matServerGlow = GetOrCreatePbrMat("Mat_ServerRack", new Color(0.10f, 0.13f, 0.18f), 0.5f, 0.4f, new Color(0.10f, 0.45f, 0.90f));
            Material matCeilingLight = GetOrCreatePbrMat("Mat_CeilingLight", Color.white, 0.9f, 0f, new Color(1.6f, 1.55f, 1.45f));
            Material matAlphaTrim = GetOrCreatePbrMat("Mat_AlphaTrim", Teams.TeamColor(Teams.Alpha), 0.35f);
            Material matBravoTrim = GetOrCreatePbrMat("Mat_BravoTrim", Teams.TeamColor(Teams.Bravo), 0.35f);

            var root = new GameObject("Map_AdminHall");

            // Main Building Slab (96 x 76) & Exterior Courtyard
            Block(root.transform, "ExteriorGround", new Vector3(0f, -0.55f, 0f), new Vector3(110f, 1f, 92f), matConcrete);
            Block(root.transform, "HallFloor", new Vector3(0f, 0f, 0f), new Vector3(78f, 0.15f, 52f), matMarble);

            // Outer Perimeter Bounds (Courtyard Fence Walls)
            Block(root.transform, "Bound_N", new Vector3(0f, 4f, 44f), new Vector3(110f, 8f, 2f), matConcrete);
            Block(root.transform, "Bound_S", new Vector3(0f, 4f, -44f), new Vector3(110f, 8f, 2f), matConcrete);
            Block(root.transform, "Bound_W", new Vector3(-54f, 4f, 0f), new Vector3(2f, 8f, 90f), matConcrete);
            Block(root.transform, "Bound_E", new Vector3(54f, 4f, 0f), new Vector3(2f, 8f, 90f), matConcrete);

            // Building Exterior Walls with Wide Entryways (South Courtyard Entrance + Side Corridors)
            Block(root.transform, "Bldg_BackWall", new Vector3(0f, 4.5f, 26f), new Vector3(78f, 9f, 1.2f), matPlaster);
            Block(root.transform, "Bldg_FrontWall_W", new Vector3(-24f, 4.5f, -26f), new Vector3(30f, 9f, 1.2f), matPlaster);
            Block(root.transform, "Bldg_FrontWall_E", new Vector3(24f, 4.5f, -26f), new Vector3(30f, 9f, 1.2f), matPlaster);
            Block(root.transform, "Bldg_FrontLintel", new Vector3(0f, 7.2f, -26f), new Vector3(18f, 3.6f, 1.2f), matPlaster);
            Block(root.transform, "Bldg_SideWall_W_N", new Vector3(-39f, 4.5f, 14f), new Vector3(1.2f, 9f, 24f), matPlaster);
            Block(root.transform, "Bldg_SideWall_W_S", new Vector3(-39f, 4.5f, -14f), new Vector3(1.2f, 9f, 24f), matPlaster);
            Block(root.transform, "Bldg_SideWall_E_N", new Vector3(39f, 4.5f, 14f), new Vector3(1.2f, 9f, 24f), matPlaster);
            Block(root.transform, "Bldg_SideWall_E_S", new Vector3(39f, 4.5f, -14f), new Vector3(1.2f, 9f, 24f), matPlaster);

            // Skylight Roof Beams (Leaves central sun atrium open for dramatic directional shadows)
            Block(root.transform, "Roof_W", new Vector3(-24f, 9.2f, 0f), new Vector3(30f, 0.5f, 54f), matSteel);
            Block(root.transform, "Roof_E", new Vector3(24f, 9.2f, 0f), new Vector3(30f, 0.5f, 54f), matSteel);
            for (int z = -20; z <= 20; z += 10)
            {
                Block(root.transform, "SkylightBeam_" + z, new Vector3(0f, 9.1f, z), new Vector3(18f, 0.5f, 0.8f), matSteel);
                BuildPart(root.transform, "AtriumLamp_" + z, PrimitiveType.Cube, new Vector3(-20f, 8.9f, z), new Vector3(3.2f, 0.12f, 0.7f), matCeilingLight);
                BuildPart(root.transform, "AtriumLampR_" + z, PrimitiveType.Cube, new Vector3(20f, 8.9f, z), new Vector3(3.2f, 0.12f, 0.7f), matCeilingLight);
            }

            // Grand Atrium Reception Desk + 4 Marble Pillars
            Block(root.transform, "ReceptionDesk", new Vector3(0f, 0.65f, 4f), new Vector3(9f, 1.3f, 2.2f), matWood);
            Block(root.transform, "AtriumPillar_NW", new Vector3(-10f, 4.5f, 12f), new Vector3(1.8f, 9f, 1.8f), matMarble);
            Block(root.transform, "AtriumPillar_NE", new Vector3(10f, 4.5f, 12f), new Vector3(1.8f, 9f, 1.8f), matMarble);
            Block(root.transform, "AtriumPillar_SW", new Vector3(-10f, 4.5f, -12f), new Vector3(1.8f, 9f, 1.8f), matMarble);
            Block(root.transform, "AtriumPillar_SE", new Vector3(10f, 4.5f, -12f), new Vector3(1.8f, 9f, 1.8f), matMarble);

            // Second-Floor Balconies + Crosswalk Bridge + Sloped Stairs
            Block(root.transform, "Balcony_W", new Vector3(-25f, 4.0f, 0f), new Vector3(26f, 0.5f, 20f), matMarble);
            Block(root.transform, "Balcony_E", new Vector3(25f, 4.0f, 0f), new Vector3(26f, 0.5f, 20f), matMarble);
            Block(root.transform, "BalconyRail_W", new Vector3(-12.2f, 4.75f, 0f), new Vector3(0.35f, 1.0f, 20f), matWood);
            Block(root.transform, "BalconyRail_E", new Vector3(12.2f, 4.75f, 0f), new Vector3(0.35f, 1.0f, 20f), matWood);
            Block(root.transform, "SkyBridge", new Vector3(0f, 4.0f, 16f), new Vector3(26f, 0.5f, 4.5f), matSteel);
            Block(root.transform, "BridgeRail_N", new Vector3(0f, 4.75f, 18.1f), new Vector3(24f, 1.0f, 0.3f), matSteel);
            Block(root.transform, "BridgeRail_S", new Vector3(0f, 4.75f, 13.9f), new Vector3(24f, 1.0f, 0.3f), matSteel);

            // Ramps up to Balconies (Left, Right, Rear)
            Ramp(root.transform, "StairRamp_W", new Vector3(-19f, 2.0f, 16f), new Vector3(4.2f, 0.45f, 13f), new Vector3(18f, 0f, 0f), matSteel);
            Ramp(root.transform, "StairRamp_E", new Vector3(19f, 2.0f, 16f), new Vector3(4.2f, 0.45f, 13f), new Vector3(18f, 0f, 0f), matSteel);

            // West Wing: Executive Offices & Glowing Server Room (Bomb Site A)
            Block(root.transform, "OfficeDiv_W1", new Vector3(-24f, 1.8f, 4f), new Vector3(12f, 3.6f, 0.6f), matPlaster);
            Block(root.transform, "OfficeDesk_W", new Vector3(-28f, 0.5f, -4f), new Vector3(4.5f, 1.0f, 1.6f), matWood);
            Block(root.transform, "ServerRack_1", new Vector3(-31f, 1.4f, 16f), new Vector3(1.6f, 2.8f, 3.5f), matServerGlow);
            Block(root.transform, "ServerRack_2", new Vector3(-26f, 1.4f, 16f), new Vector3(1.6f, 2.8f, 3.5f), matServerGlow);
            Block(root.transform, "AlphaSign", new Vector3(-37.8f, 3.2f, 0f), new Vector3(0.4f, 0.6f, 12f), matAlphaTrim);

            // East Wing: Command Conference Hall (Bomb Site B)
            Block(root.transform, "ConfTable", new Vector3(26f, 0.55f, 0f), new Vector3(7.5f, 1.1f, 2.6f), matWood);
            Block(root.transform, "ConfWall_N", new Vector3(24f, 1.8f, -11f), new Vector3(12f, 3.6f, 0.6f), matPlaster);
            Block(root.transform, "CommandConsole", new Vector3(31f, 1.2f, -18f), new Vector3(4f, 2.4f, 1.4f), matServerGlow);
            Block(root.transform, "BravoSign", new Vector3(37.8f, 3.2f, 0f), new Vector3(0.4f, 0.6f, 12f), matBravoTrim);

            // South Courtyard Plaza Cover (AR / Sniper Flank Route)
            Block(root.transform, "PlazaPlanter_W", new Vector3(-16f, 0.7f, -34f), new Vector3(5f, 1.4f, 2.2f), matConcrete);
            Block(root.transform, "PlazaPlanter_E", new Vector3(16f, 0.7f, -34f), new Vector3(5f, 1.4f, 2.2f), matConcrete);
            Block(root.transform, "PlazaFountain", new Vector3(0f, 0.65f, -35f), new Vector3(6f, 1.3f, 4f), matMarble);

            // Interior Warm Architectural Spotlights
            AddPointLight(root.transform, "Light_Atrium", new Vector3(0f, 7.5f, 0f), new Color(1f, 0.95f, 0.86f), 28f, 3.2f);
            AddPointLight(root.transform, "Light_WestWing", new Vector3(-26f, 3.2f, 0f), new Color(0.82f, 0.90f, 1.0f), 22f, 2.4f);
            AddPointLight(root.transform, "Light_EastWing", new Vector3(26f, 3.2f, 0f), new Color(1.0f, 0.92f, 0.80f), 22f, 2.4f);

            // Map Config & Spawns
            var markers = new GameObject("MapMarkers");
            Transform[] alphaSpawns =
            {
                Marker(markers.transform, "AlphaSpawn_1", new Vector3(-46f, 0.1f, -4f), Quaternion.Euler(0, 90, 0)),
                Marker(markers.transform, "AlphaSpawn_2", new Vector3(-46f, 0.1f, 4f), Quaternion.Euler(0, 90, 0)),
                Marker(markers.transform, "AlphaSpawn_3", new Vector3(-45f, 0.1f, -18f), Quaternion.Euler(0, 75, 0)),
                Marker(markers.transform, "AlphaSpawn_4", new Vector3(-45f, 0.1f, 18f), Quaternion.Euler(0, 105, 0)),
                Marker(markers.transform, "AlphaSpawn_5", new Vector3(-32f, 0.2f, -18f), Quaternion.Euler(0, 60, 0))
            };
            Transform[] bravoSpawns =
            {
                Marker(markers.transform, "BravoSpawn_1", new Vector3(46f, 0.1f, -4f), Quaternion.Euler(0, -90, 0)),
                Marker(markers.transform, "BravoSpawn_2", new Vector3(46f, 0.1f, 4f), Quaternion.Euler(0, -90, 0)),
                Marker(markers.transform, "BravoSpawn_3", new Vector3(45f, 0.1f, -18f), Quaternion.Euler(0, -75, 0)),
                Marker(markers.transform, "BravoSpawn_4", new Vector3(45f, 0.1f, 18f), Quaternion.Euler(0, -105, 0)),
                Marker(markers.transform, "BravoSpawn_5", new Vector3(32f, 0.2f, -18f), Quaternion.Euler(0, -60, 0))
            };
            Transform[] vehicleSpawns =
            {
                Marker(markers.transform, "BuggySpawn_PlazaW", new Vector3(-36f, 0.1f, -35f), Quaternion.Euler(0, 90, 0)),
                Marker(markers.transform, "BuggySpawn_PlazaE", new Vector3(36f, 0.1f, -35f), Quaternion.Euler(0, -90, 0))
            };
            Transform[] captureSpawns =
            {
                Marker(markers.transform, "CapturePoint_A", new Vector3(-26f, 0.1f, -4f), Quaternion.identity),
                Marker(markers.transform, "CapturePoint_B", new Vector3(0f, 0.1f, -6f), Quaternion.identity),
                Marker(markers.transform, "CapturePoint_C", new Vector3(26f, 0.1f, -4f), Quaternion.identity)
            };
            Transform[] bombSpawns =
            {
                Marker(markers.transform, "BombSite_A", new Vector3(-26f, 0.1f, 11f), Quaternion.identity),
                Marker(markers.transform, "BombSite_B", new Vector3(26f, 0.1f, 9f), Quaternion.identity)
            };

            var spawnMgr = markers.AddComponent<SpawnManager>();
            spawnMgr.alphaSpawns = alphaSpawns;
            spawnMgr.bravoSpawns = bravoSpawns;

            var cfg = markers.AddComponent<MapConfig>();
            cfg.mapDisplayName = "AdminHall";
            cfg.vehicleSpawns = vehicleSpawns;
            cfg.capturePoints = captureSpawns;
            cfg.bombSites = bombSpawns;

            EditorSceneManager.SaveScene(scene, AdminHallScenePath);
        }

        /// <summary>Builds Map 2: Outpost Vector (Arena.unity) — Open 3-Lane Combined-Arms Compound.</summary>
        static void BuildArenaScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment(new Color(0.65f, 0.74f, 0.86f), new Color(0.48f, 0.47f, 0.44f), 46f, -32f, 1.9f, "ArenaVolumeProfile");

            Material matFloor = GetOrCreateTileMat("Mat_ArenaFloor", new Color(0.46f, 0.44f, 0.40f), new Color(0.40f, 0.38f, 0.34f), 0.14f, 28f);
            Material matRoad = GetOrCreateTileMat("Mat_Asphalt", new Color(0.20f, 0.21f, 0.23f), new Color(0.16f, 0.17f, 0.19f), 0.20f, 20f);
            Material matWall = GetOrCreateTileMat("Mat_ConcreteGrid", new Color(0.56f, 0.55f, 0.52f), new Color(0.47f, 0.46f, 0.44f), 0.12f, 14f);
            Material matSteel = GetOrCreatePbrMat("Mat_Steel", new Color(0.32f, 0.35f, 0.38f), 0.55f, 0.4f);
            Material matContainerRed = GetOrCreateTileMat("Mat_ContainerRust", new Color(0.52f, 0.24f, 0.18f), new Color(0.42f, 0.18f, 0.14f), 0.25f, 6f);
            Material matContainerBlue = GetOrCreateTileMat("Mat_ContainerNavy", new Color(0.18f, 0.29f, 0.40f), new Color(0.14f, 0.22f, 0.32f), 0.25f, 6f);
            Material matAlphaTint = GetOrCreatePbrMat("Mat_AlphaTrim", Teams.TeamColor(Teams.Alpha), 0.35f);
            Material matBravoTint = GetOrCreatePbrMat("Mat_BravoTrim", Teams.TeamColor(Teams.Bravo), 0.35f);

            var map = new GameObject("Map_OutpostVector");

            Block(map.transform, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 100f), matFloor);
            Block(map.transform, "NorthRoad", new Vector3(0f, 0.02f, 28f), new Vector3(114f, 0.06f, 14f), matRoad);

            Block(map.transform, "Wall_North", new Vector3(0f, 4f, 49f), new Vector3(130f, 8f, 2f), matWall);
            Block(map.transform, "Wall_South", new Vector3(0f, 4f, -49f), new Vector3(130f, 8f, 2f), matWall);
            Block(map.transform, "Wall_West", new Vector3(-64f, 4f, 0f), new Vector3(2f, 8f, 100f), matWall);
            Block(map.transform, "Wall_East", new Vector3(64f, 4f, 0f), new Vector3(2f, 8f, 100f), matWall);

            Block(map.transform, "AlphaBunkerBack", new Vector3(-56f, 2.2f, 0f), new Vector3(2f, 4.4f, 26f), matWall);
            Block(map.transform, "AlphaTrim", new Vector3(-54.8f, 3.8f, 0f), new Vector3(0.4f, 0.5f, 24f), matAlphaTint);
            Block(map.transform, "AlphaBarrierN", new Vector3(-43f, 1.1f, 10f), new Vector3(1.5f, 2.2f, 8f), matWall);
            Block(map.transform, "AlphaBarrierS", new Vector3(-43f, 1.1f, -10f), new Vector3(1.5f, 2.2f, 8f), matWall);

            Block(map.transform, "BravoBunkerBack", new Vector3(56f, 2.2f, 0f), new Vector3(2f, 4.4f, 26f), matWall);
            Block(map.transform, "BravoTrim", new Vector3(54.8f, 3.8f, 0f), new Vector3(0.4f, 0.5f, 24f), matBravoTint);
            Block(map.transform, "BravoBarrierN", new Vector3(43f, 1.1f, 10f), new Vector3(1.5f, 2.2f, 8f), matWall);
            Block(map.transform, "BravoBarrierS", new Vector3(43f, 1.1f, -10f), new Vector3(1.5f, 2.2f, 8f), matWall);

            Block(map.transform, "CenterDeck", new Vector3(0f, 3.6f, 0f), new Vector3(18f, 0.6f, 12f), matSteel);
            Block(map.transform, "CenterPillarNW", new Vector3(-7.5f, 1.8f, 4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterPillarNE", new Vector3(7.5f, 1.8f, 4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterPillarSW", new Vector3(-7.5f, 1.8f, -4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterPillarSE", new Vector3(7.5f, 1.8f, -4.8f), new Vector3(1.4f, 3.6f, 1.4f), matWall);
            Block(map.transform, "CenterCoverW", new Vector3(-16f, 0.85f, 0f), new Vector3(2.2f, 1.7f, 6f), matWall);
            Block(map.transform, "CenterCoverE", new Vector3(16f, 0.85f, 0f), new Vector3(2.2f, 1.7f, 6f), matWall);

            Ramp(map.transform, "RampWest", new Vector3(-14.5f, 1.8f, 0f), new Vector3(12f, 0.5f, 4.2f), new Vector3(0f, 0f, 17f), matSteel);
            Ramp(map.transform, "RampEast", new Vector3(14.5f, 1.8f, 0f), new Vector3(12f, 0.5f, 4.2f), new Vector3(0f, 0f, -17f), matSteel);
            Block(map.transform, "DeckParapetN", new Vector3(0f, 4.45f, 5.6f), new Vector3(16f, 1.1f, 0.4f), matSteel);
            Block(map.transform, "DeckParapetS", new Vector3(0f, 4.45f, -5.6f), new Vector3(16f, 1.1f, 0.4f), matSteel);

            Ramp(map.transform, "NorthJumpW", new Vector3(-12f, 0.5f, 28f), new Vector3(6f, 0.4f, 6f), new Vector3(0f, 0f, 11f), matSteel);
            Ramp(map.transform, "NorthJumpE", new Vector3(12f, 0.5f, 28f), new Vector3(6f, 0.4f, 6f), new Vector3(0f, 0f, -11f), matSteel);
            Block(map.transform, "NorthCoverA", new Vector3(-25f, 0.9f, 20f), new Vector3(7f, 1.8f, 1.6f), matWall);
            Block(map.transform, "NorthCoverB", new Vector3(25f, 0.9f, 20f), new Vector3(7f, 1.8f, 1.6f), matWall);

            Block(map.transform, "Container1", new Vector3(-14f, 1.5f, -26f), new Vector3(7.5f, 3.0f, 2.8f), matContainerRed);
            Block(map.transform, "Container2", new Vector3(14f, 1.5f, -26f), new Vector3(7.5f, 3.0f, 2.8f), matContainerBlue);
            Block(map.transform, "Container3", new Vector3(0f, 1.5f, -35f), new Vector3(8.5f, 3.0f, 2.8f), matContainerRed);
            Block(map.transform, "Container4", new Vector3(-26f, 1.5f, -20f), new Vector3(2.8f, 3.0f, 7.5f), matContainerBlue);
            Block(map.transform, "Container5", new Vector3(26f, 1.5f, -20f), new Vector3(2.8f, 3.0f, 7.5f), matContainerRed);
            Block(map.transform, "CqbCrates", new Vector3(0f, 0.75f, -20f), new Vector3(3.2f, 1.5f, 3.2f), matWall);

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
            Transform[] bombSpawns =
            {
                Marker(markers.transform, "BombSite_A", new Vector3(0f, 0f, 18f), Quaternion.identity),
                Marker(markers.transform, "BombSite_B", new Vector3(0f, 0f, -28f), Quaternion.identity)
            };

            var spawnMgr = markers.AddComponent<SpawnManager>();
            spawnMgr.alphaSpawns = alphaSpawns;
            spawnMgr.bravoSpawns = bravoSpawns;

            var cfg = markers.AddComponent<MapConfig>();
            cfg.mapDisplayName = "Outpost Vector";
            cfg.vehicleSpawns = vehicleSpawns;
            cfg.capturePoints = captureSpawns;
            cfg.bombSites = bombSpawns;

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
        }

        static void SetupEnvironment(Color sky, Color eq, float pitch, float yaw, float intensity, string profileName)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = eq;
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.20f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(sky, eq, 0.55f);
            RenderSettings.fogDensity = 0.0022f;

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = intensity;
            sun.color = new Color(1.0f, 0.96f, 0.89f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;

            string volPath = $"{SettingsDir}/{profileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(volPath) != null) AssetDatabase.DeleteAsset(volPath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, volPath);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.0f);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(10f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.18f);

            vol.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        static void AddPointLight(Transform parent, string name, Vector3 pos, Color col, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
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

        static Material GetOrCreatePbrMat(string name, Color color, float smoothness = 0.25f, float metallic = 0f, Color? emission = null)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name, enableInstancing = true };
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

        static Material GetOrCreateTileMat(string name, Color baseCol, Color lineCol, float smoothness, float tiling)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Texture2D tex = GetOrCreateGridTexture(name + "_Tex", baseCol, lineCol);
            Material mat = GetOrCreatePbrMat(name, Color.white, smoothness);
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            else
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = new Vector2(tiling, tiling);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D GetOrCreateGridTexture(string name, Color baseCol, Color lineCol)
        {
            string texPath = $"{TexturesDir}/{name}.asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var rng = new System.Random(name.GetHashCode());
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x < 3 || y < 3 || (x >= 63 && x <= 64) || (y >= 63 && y <= 64);
                    float grain = ((float)rng.NextDouble() - 0.5f) * 0.06f;
                    Color c = border ? lineCol : baseCol;
                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(c.r + grain),
                        Mathf.Clamp01(c.g + grain),
                        Mathf.Clamp01(c.b + grain), 1f));
                }
            }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, texPath);
            return tex;
        }

        static void EnsureFolders()
        {
            CreateFolderIfNeeded("Assets", "IronVector");
            CreateFolderIfNeeded(RootDir, "Prefabs");
            CreateFolderIfNeeded(RootDir, "Materials");
            CreateFolderIfNeeded(RootDir, "Textures");
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
