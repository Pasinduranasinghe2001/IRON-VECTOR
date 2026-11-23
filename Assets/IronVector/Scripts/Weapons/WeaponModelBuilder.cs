using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// Procedural CAD-style 3D firearm builder generating chamfered machined receivers, extruded grips/mags/stocks,
    /// Picatinny rail teeth, hollow-bore barrels, optical sights with illuminated reticles, and URP Lit PBR materials.
    /// </summary>
    public static class WeaponModelBuilder
    {
        public sealed class WeaponRig
        {
            public GameObject Root;
            public Transform Muzzle;
            public Transform EjectPort;
            public Transform BoltCarrier;
            public Vector3 BoltRestPos;
            public float BoltTravelZ = -0.042f;
            public Transform PumpForend;
            public Vector3 PumpRestPos;
            public Vector3 AimAnchorLocalPos;
            public float AdsDistanceZ = 0.27f;
            public Vector3 HipLocalPos = new Vector3(0.21f, -0.19f, 0.44f);
            public Vector3 HipLocalEuler = new Vector3(1.5f, -4.2f, -2.0f);
        }

        enum FinishType
        {
            AnodizedMetal,
            MachinedSteel,
            Chrome,
            MattePolymer,
            KnurledGrip,
            WalnutWood,
            Brass,
            Copper,
            TintedGlass,
            ReticleGlow,
            TritiumDot,
            RecessDark
        }

        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();
        static Texture2D _brushedTex, _knurlTex, _woodTex;
        static Shader _urpLit;

        static Shader UrpLitShader
        {
            get
            {
                if (_urpLit != null) return _urpLit;
                _urpLit = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultShader : null;
                if (_urpLit == null) _urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (_urpLit == null) _urpLit = Shader.Find("Standard");
                return _urpLit;
            }
        }

        public static WeaponRig Build(WeaponData w, Transform parent, bool isOwnerViewmodel)
        {
            var root = new GameObject(w.weaponName + "_CAD3D");
            root.transform.SetParent(parent, false);

            var rig = new WeaponRig { Root = root };
            var ctx = new BuildCtx(root.transform, rig, isOwnerViewmodel, w);

            switch (w.visualStyle)
            {
                case WeaponVisualStyle.ModernCarbine:        BuildM4A1(ctx); break;
                case WeaponVisualStyle.KalashnikovWood:      BuildAK47(ctx); break;
                case WeaponVisualStyle.SubmachineSuppressed: BuildMP5(ctx); break;
                case WeaponVisualStyle.P90TopMag:            BuildP90(ctx); break;
                case WeaponVisualStyle.SniperBolt:           BuildM40A3(ctx); break;
                case WeaponVisualStyle.PumpShotgun:          BuildW1200(ctx); break;
                case WeaponVisualStyle.LightMachineGun:      BuildM249(ctx); break;
                case WeaponVisualStyle.TacticalPistol:       BuildM9(ctx); break;
                case WeaponVisualStyle.ChromeHandCannon:     BuildDesertEagle(ctx); break;
                default:                                     BuildM4A1(ctx); break;
            }

            if (rig.Muzzle == null)
                rig.Muzzle = MakeMarker(root.transform, "Muzzle", new Vector3(0f, 0.015f, 0.45f));
            if (rig.EjectPort == null)
                rig.EjectPort = MakeMarker(root.transform, "EjectPort", new Vector3(0.028f, 0.022f, 0.02f));

            if (isOwnerViewmodel)
                AttachFirstPersonHands(ctx, w);

            return rig;
        }

        static void AttachFirstPersonHands(BuildCtx c, WeaponData w)
        {
            Material sleeveMat = GetMat(FinishType.MattePolymer, new Color(0.20f, 0.26f, 0.34f));
            Material cuffMat = GetMat(FinishType.KnurledGrip, new Color(0.15f, 0.17f, 0.19f));
            Material gloveMat = GetMat(FinishType.KnurledGrip, new Color(0.22f, 0.21f, 0.19f));
            Material armorMat = GetMat(FinishType.MachinedSteel, new Color(0.24f, 0.25f, 0.27f));

            bool isPistol = w.category == WeaponCategory.Pistol;
            bool isP90 = w.visualStyle == WeaponVisualStyle.P90TopMag;

            // Right Tactical Forearm & Mechanix Glove gripping pistol grip
            Vector3 rGripPos = isPistol ? new Vector3(0.008f, -0.052f, -0.032f)
                : (isP90 ? new Vector3(0.008f, -0.052f, 0.030f) : new Vector3(0.008f, -0.062f, -0.068f));
            AddChamfer("FP_GloveR", c, rGripPos, new Vector3(0.044f, 0.058f, 0.054f), 0.008f, gloveMat, new Vector3(16f, 0f, 0f));
            AddChamfer("FP_KnuckleR", c, rGripPos + new Vector3(0.022f, 0.012f, 0.004f), new Vector3(0.008f, 0.046f, 0.038f), 0.002f, armorMat, new Vector3(16f, 0f, 0f));
            AddChamfer("FP_WristR", c, rGripPos + new Vector3(0.026f, -0.042f, -0.058f), new Vector3(0.056f, 0.054f, 0.095f), 0.012f, cuffMat, new Vector3(-28f, -18f, 0f));
            AddChamfer("FP_SleeveR", c, rGripPos + new Vector3(0.088f, -0.105f, -0.165f), new Vector3(0.082f, 0.078f, 0.22f), 0.016f, sleeveMat, new Vector3(-24f, -22f, 0f));

            // Left Support Glove & Forearm (parents directly to PumpForend on W1200 so arm pumps!)
            Transform leftParent = (!isPistol && c.Rig.PumpForend != null) ? c.Rig.PumpForend : c.Root;
            Vector3 lGripPos = isPistol ? rGripPos + new Vector3(-0.022f, -0.012f, -0.005f)
                : (c.Rig.PumpForend != null ? new Vector3(-0.010f, -0.018f, 0.0f)
                    : (isP90 ? new Vector3(-0.014f, -0.032f, 0.140f) : new Vector3(-0.012f, -0.016f, 0.195f)));

            AddChamfer("FP_GloveL", c, lGripPos, new Vector3(0.046f, 0.042f, 0.068f), 0.009f, gloveMat, new Vector3(0f, 10f, 0f), leftParent);
            AddChamfer("FP_KnuckleL", c, lGripPos + new Vector3(-0.022f, -0.005f, 0.010f), new Vector3(0.008f, 0.032f, 0.048f), 0.002f, armorMat, new Vector3(0f, 10f, 0f), leftParent);
            AddChamfer("FP_WristL", c, lGripPos + new Vector3(-0.045f, -0.046f, -0.062f), new Vector3(0.054f, 0.052f, 0.095f), 0.012f, cuffMat, new Vector3(-26f, 28f, 0f), leftParent);
            AddChamfer("FP_SleeveL", c, lGripPos + new Vector3(-0.135f, -0.118f, -0.175f), new Vector3(0.080f, 0.076f, 0.23f), 0.016f, sleeveMat, new Vector3(-24f, 34f, 0f), leftParent);
        }

        sealed class BuildCtx
        {
            public readonly Transform Root;
            public readonly WeaponRig Rig;
            public readonly ShadowCastingMode CastMode;
            public readonly WeaponData Data;

            public BuildCtx(Transform root, WeaponRig rig, bool isOwner, WeaponData data)
            {
                Root = root;
                Rig = rig;
                CastMode = isOwner ? ShadowCastingMode.Off : ShadowCastingMode.On;
                Data = data;
            }
        }

        // =====================================================================
        // PER-WEAPON CAD ASSEMBLIES
        // =====================================================================

        static void BuildM4A1(BuildCtx c)
        {
            Material upper = GetMat(FinishType.AnodizedMetal, Lift(c.Data.bodyColor, 0.04f));
            Material lower = GetMat(FinishType.AnodizedMetal, Lift(c.Data.bodyColor, 0.01f));
            Material poly = GetMat(FinishType.MattePolymer, new Color(0.14f, 0.15f, 0.17f));
            Material gripMat = GetMat(FinishType.KnurledGrip, new Color(0.16f, 0.17f, 0.19f));
            Material steel = GetMat(FinishType.MachinedSteel, new Color(0.24f, 0.26f, 0.29f));
            Material chrome = GetMat(FinishType.Chrome, new Color(0.76f, 0.79f, 0.83f));
            Material tanRail = GetMat(FinishType.AnodizedMetal, new Color(0.28f, 0.25f, 0.21f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Lower & Upper Chamfered Receivers
            AddChamfer("LowerReceiver", c, new Vector3(0f, -0.008f, -0.01f), new Vector3(0.044f, 0.038f, 0.20f), 0.004f, lower);
            AddChamfer("UpperReceiver", c, new Vector3(0f, 0.022f, -0.005f), new Vector3(0.042f, 0.034f, 0.21f), 0.005f, upper);
            AddChamfer("MagWell", c, new Vector3(0f, -0.032f, 0.055f), new Vector3(0.038f, 0.032f, 0.068f), 0.003f, lower);
            AddChamfer("ShellDeflector", c, new Vector3(0.025f, 0.020f, -0.045f), new Vector3(0.012f, 0.018f, 0.024f), 0.002f, upper);

            // Ejection port recess + sliding chrome bolt carrier
            AddBox("EjectPortRecess", c, new Vector3(0.018f, 0.023f, 0.025f), new Vector3(0.010f, 0.016f, 0.072f), boreDark);
            Transform bolt = AddChamfer("BoltCarrier", c, new Vector3(0.019f, 0.023f, 0.022f), new Vector3(0.009f, 0.013f, 0.056f), 0.0015f, chrome).transform;
            c.Rig.BoltCarrier = bolt;
            c.Rig.BoltRestPos = bolt.localPosition;
            c.Rig.BoltTravelZ = -0.045f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.030f, 0.024f, 0.028f));

            // Charging Handle + Selector Switch
            AddChamfer("ChargingHandle", c, new Vector3(0f, 0.036f, -0.115f), new Vector3(0.030f, 0.010f, 0.022f), 0.002f, steel);
            AddCylinder("SelectorLever", c, new Vector3(-0.024f, -0.004f, -0.045f), 0.006f, 0.006f, steel, new Vector3(0f, 0f, 90f));
            AddChamfer("SelectorTip", c, new Vector3(-0.026f, 0.002f, -0.053f), new Vector3(0.005f, 0.007f, 0.016f), 0.001f, steel, new Vector3(-25f, 0f, 0f));

            // Pistol Grip & Trigger Assembly
            AddExtrudedProfile("PistolGrip", c, GripProfile(0.042f, 0.105f, 18f), 0.032f, new Vector3(0f, -0.022f, -0.068f), gripMat);
            AddTriggerAssembly(c, new Vector3(0f, -0.032f, -0.012f), steel);

            // Collapsible SOPMOD Buffer Tube + Stock
            AddCylinder("BufferTube", c, new Vector3(0f, 0.014f, -0.205f), 0.016f, 0.19f, steel, new Vector3(90f, 0f, 0f));
            AddChamfer("CastleNut", c, new Vector3(0f, 0.014f, -0.118f), new Vector3(0.036f, 0.036f, 0.016f), 0.004f, steel);
            AddExtrudedProfile("SOPMODStock", c, RifleStockProfile(0.185f, 0.095f), 0.040f, new Vector3(0f, 0.015f, -0.22f), poly);
            AddChamfer("ButtPad", c, new Vector3(0f, -0.015f, -0.322f), new Vector3(0.043f, 0.110f, 0.014f), 0.003f, gripMat);

            // Quad-Rail RAS Handguard + Barrel Assembly
            AddChamfer("HandguardCore", c, new Vector3(0f, 0.020f, 0.205f), new Vector3(0.046f, 0.046f, 0.19f), 0.007f, tanRail);
            AddPicatinnyRail("TopRail", c, new Vector3(0f, 0.042f, -0.095f), 0.39f, 0.021f, steel, 18);
            AddPicatinnyRail("SideRailR", c, new Vector3(0.024f, 0.020f, 0.12f), 0.16f, 0.014f, tanRail, 8, new Vector3(0f, 0f, -90f));
            AddPicatinnyRail("SideRailL", c, new Vector3(-0.024f, 0.020f, 0.12f), 0.16f, 0.014f, tanRail, 8, new Vector3(0f, 0f, 90f));
            AddPicatinnyRail("BotRail", c, new Vector3(0f, -0.004f, 0.12f), 0.16f, 0.016f, tanRail, 8, new Vector3(0f, 0f, 180f));

            // Vertical Foregrip
            AddCylinder("Foregrip", c, new Vector3(0f, -0.048f, 0.22f), 0.015f, 0.082f, gripMat);
            AddCylinder("ForegripCap", c, new Vector3(0f, -0.090f, 0.22f), 0.0165f, 0.008f, poly);

            // Barrel, Gas Block, A-Frame Front Sight & Birdcage Flash Hider
            AddCylinder("GasTube", c, new Vector3(0f, 0.036f, 0.21f), 0.0035f, 0.20f, chrome, new Vector3(90f, 0f, 0f));
            AddCylinder("Barrel", c, new Vector3(0f, 0.022f, 0.355f), 0.010f, 0.26f, steel, new Vector3(90f, 0f, 0f));
            AddExtrudedProfile("FrontSightAFrame", c, AFrameProfile(), 0.018f, new Vector3(0f, 0.020f, 0.335f), steel);
            AddChamfer("FrontSightPost", c, new Vector3(0f, 0.068f, 0.335f), new Vector3(0.004f, 0.012f, 0.005f), 0.001f, chrome);
            AddMuzzleDevice("Birdcage", c, new Vector3(0f, 0.022f, 0.495f), 0.013f, 0.048f, steel, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.022f, 0.522f));

            // Curved STANAG 30-rd Magazine
            AddExtrudedProfile("MagBody", c, CurvedMagProfile(0.056f, 0.145f, 0.022f), 0.024f, new Vector3(0f, -0.042f, 0.055f), steel);
            AddChamfer("MagFloorplate", c, new Vector3(0f, -0.175f, 0.077f), new Vector3(0.028f, 0.009f, 0.062f), 0.002f, poly, new Vector3(-10f, 0f, 0f));

            // Hooded EOTech Holo Sight
            c.Rig.AimAnchorLocalPos = BuildHoloSight(c, new Vector3(0f, 0.046f, 0.02f), poly, steel);
            c.Rig.AdsDistanceZ = 0.27f;
        }

        static void BuildAK47(BuildCtx c)
        {
            Material steel = GetMat(FinishType.MachinedSteel, new Color(0.21f, 0.22f, 0.24f));
            Material darkSteel = GetMat(FinishType.AnodizedMetal, new Color(0.15f, 0.15f, 0.17f));
            Material chrome = GetMat(FinishType.Chrome, new Color(0.78f, 0.80f, 0.84f));
            Material wood = GetMat(FinishType.WalnutWood, new Color(0.56f, 0.30f, 0.14f));
            Material bakelite = GetMat(FinishType.MattePolymer, new Color(0.50f, 0.22f, 0.11f));
            Material gripMat = GetMat(FinishType.WalnutWood, new Color(0.46f, 0.24f, 0.12f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Stamped Steel Receiver & Ribbed Top Dust Cover
            AddChamfer("Receiver", c, new Vector3(0f, 0.006f, 0.005f), new Vector3(0.042f, 0.044f, 0.235f), 0.004f, darkSteel);
            AddChamfer("DustCover", c, new Vector3(0f, 0.032f, -0.005f), new Vector3(0.037f, 0.020f, 0.205f), 0.007f, steel);
            AddChamfer("FrontTrunnion", c, new Vector3(0f, 0.022f, 0.115f), new Vector3(0.043f, 0.036f, 0.045f), 0.003f, darkSteel);

            // Ejection Port & Iconic Curved Silver Charging Handle
            AddBox("EjectSlot", c, new Vector3(0.017f, 0.022f, 0.035f), new Vector3(0.009f, 0.014f, 0.075f), boreDark);
            var boltGroup = new GameObject("BoltAssembly");
            boltGroup.transform.SetParent(c.Root, false);
            boltGroup.transform.localPosition = new Vector3(0.021f, 0.022f, 0.040f);
            AddChamfer("BoltBody", c, Vector3.zero, new Vector3(0.008f, 0.012f, 0.060f), 0.002f, chrome, Vector3.zero, boltGroup.transform);
            AddChamfer("BoltStem", c, new Vector3(0.014f, 0.001f, -0.012f), new Vector3(0.026f, 0.008f, 0.010f), 0.002f, chrome, new Vector3(0f, 0f, 12f), boltGroup.transform);
            c.Rig.BoltCarrier = boltGroup.transform;
            c.Rig.BoltRestPos = boltGroup.transform.localPosition;
            c.Rig.BoltTravelZ = -0.052f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.032f, 0.025f, 0.040f));

            // Laminated Wood Pistol Grip + Angled Wood Stock
            AddExtrudedProfile("AKGrip", c, GripProfile(0.038f, 0.102f, 20f), 0.031f, new Vector3(0f, -0.016f, -0.078f), gripMat);
            AddTriggerAssembly(c, new Vector3(0f, -0.022f, -0.022f), steel);
            AddExtrudedProfile("AKWoodStock", c, RifleStockProfile(0.22f, 0.105f, drop: 0.038f), 0.038f, new Vector3(0f, 0.006f, -0.110f), wood);
            AddChamfer("AKSteelButtplate", c, new Vector3(0f, -0.036f, -0.332f), new Vector3(0.040f, 0.112f, 0.010f), 0.002f, steel);

            // Ventilated Upper & Lower Wood Handguards
            AddChamfer("LowerHandguardWood", c, new Vector3(0f, 0.005f, 0.215f), new Vector3(0.042f, 0.032f, 0.160f), 0.007f, wood);
            AddChamfer("UpperHandguardWood", c, new Vector3(0f, 0.031f, 0.215f), new Vector3(0.032f, 0.022f, 0.145f), 0.006f, wood);
            AddChamfer("HandguardRetainer", c, new Vector3(0f, 0.016f, 0.295f), new Vector3(0.038f, 0.044f, 0.014f), 0.002f, darkSteel);

            // Gas Tube, Heavy Barrel, Under-barrel Cleaning Rod, Front Sight & Slanted Brake
            AddCylinder("GasBlockTube", c, new Vector3(0f, 0.034f, 0.285f), 0.008f, 0.14f, steel, new Vector3(90f, 0f, 0f));
            AddCylinder("AKBarrel", c, new Vector3(0f, 0.017f, 0.370f), 0.0095f, 0.31f, darkSteel, new Vector3(90f, 0f, 0f));
            AddCylinder("CleaningRod", c, new Vector3(0f, 0.003f, 0.340f), 0.0032f, 0.26f, chrome, new Vector3(90f, 0f, 0f));
            AddExtrudedProfile("AKFrontSightBase", c, AFrameProfile(0.036f, 0.048f), 0.018f, new Vector3(0f, 0.017f, 0.455f), darkSteel);
            AddMuzzleDevice("SlantedBrake", c, new Vector3(0f, 0.017f, 0.530f), 0.0115f, 0.032f, steel, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.017f, 0.550f));

            // Iconic Curved 7.62mm Banana Magazine
            AddExtrudedProfile("BananaMag", c, CurvedMagProfile(0.056f, 0.178f, 0.068f), 0.025f, new Vector3(0f, -0.015f, 0.062f), bakelite);
            AddChamfer("MagRib", c, new Vector3(0f, -0.172f, 0.125f), new Vector3(0.028f, 0.010f, 0.058f), 0.002f, steel, new Vector3(-28f, 0f, 0f));

            // Tangent Rear & Hooded Front Iron Sights (3-Dot Night Accent)
            c.Rig.AimAnchorLocalPos = Build3DotIronSights(c,
                new Vector3(0f, 0.048f, 0.11f),
                new Vector3(0f, 0.056f, 0.455f),
                darkSteel, 0.006f);
            c.Rig.AdsDistanceZ = 0.27f;
        }

        static void BuildMP5(BuildCtx c)
        {
            Material hkSteel = GetMat(FinishType.AnodizedMetal, new Color(0.14f, 0.15f, 0.17f));
            Material poly = GetMat(FinishType.MattePolymer, new Color(0.12f, 0.13f, 0.15f));
            Material knurl = GetMat(FinishType.KnurledGrip, new Color(0.15f, 0.16f, 0.18f));
            Material steel = GetMat(FinishType.MachinedSteel, new Color(0.25f, 0.27f, 0.30f));
            Material chrome = GetMat(FinishType.Chrome, new Color(0.75f, 0.78f, 0.82f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // H&K Rounded/Chamfered Receiver + Cocking Tube Above
            AddChamfer("MP5Receiver", c, new Vector3(0f, 0.010f, 0.01f), new Vector3(0.040f, 0.046f, 0.215f), 0.008f, hkSteel);
            AddChamfer("TriggerHousing", c, new Vector3(0f, -0.016f, -0.025f), new Vector3(0.036f, 0.024f, 0.145f), 0.004f, poly);
            AddCylinder("CockingTube", c, new Vector3(0f, 0.038f, 0.13f), 0.012f, 0.22f, hkSteel, new Vector3(90f, 0f, 0f));

            // Animated Cocking Handle + Ejection Port
            Transform handle = AddChamfer("CockingHandle", c, new Vector3(-0.020f, 0.042f, 0.195f), new Vector3(0.024f, 0.009f, 0.012f), 0.002f, steel, new Vector3(0f, 0f, -25f)).transform;
            c.Rig.BoltCarrier = handle;
            c.Rig.BoltRestPos = handle.localPosition;
            c.Rig.BoltTravelZ = -0.035f;
            AddBox("EjectPortRecess", c, new Vector3(0.017f, 0.020f, 0.035f), new Vector3(0.009f, 0.013f, 0.052f), boreDark);
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.028f, 0.022f, 0.035f));

            // Grip & Trigger
            AddExtrudedProfile("MP5Grip", c, GripProfile(0.040f, 0.098f, 17f), 0.031f, new Vector3(0f, -0.022f, -0.068f), knurl);
            AddTriggerAssembly(c, new Vector3(0f, -0.028f, -0.015f), steel);

            // Sliding A3 Collapsible Wire Stock
            AddChamfer("EndCap", c, new Vector3(0f, 0.012f, -0.105f), new Vector3(0.042f, 0.048f, 0.022f), 0.005f, hkSteel);
            AddCylinder("StockRailL", c, new Vector3(-0.020f, 0.008f, -0.19f), 0.0045f, 0.17f, steel, new Vector3(90f, 0f, 0f));
            AddCylinder("StockRailR", c, new Vector3(0.020f, 0.008f, -0.19f), 0.0045f, 0.17f, steel, new Vector3(90f, 0f, 0f));
            AddChamfer("ButtPad", c, new Vector3(0f, -0.002f, -0.278f), new Vector3(0.044f, 0.092f, 0.018f), 0.004f, poly);

            // Ribbed Polymer Forend + Heavy Tactical Suppressor
            AddChamfer("Forend", c, new Vector3(0f, 0.012f, 0.165f), new Vector3(0.044f, 0.044f, 0.135f), 0.010f, knurl);
            AddCylinder("SuppressorCollar", c, new Vector3(0f, 0.018f, 0.255f), 0.022f, 0.026f, steel, new Vector3(90f, 0f, 0f));
            AddMuzzleDevice("SuppressorTube", c, new Vector3(0f, 0.018f, 0.355f), 0.020f, 0.180f, hkSteel, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.018f, 0.448f));

            // Curved 9mm Magazine
            AddExtrudedProfile("MP5Mag", c, CurvedMagProfile(0.036f, 0.155f, 0.038f), 0.022f, new Vector3(0f, -0.025f, 0.068f), hkSteel);
            AddChamfer("MagRelease", c, new Vector3(0.020f, -0.020f, 0.036f), new Vector3(0.008f, 0.014f, 0.014f), 0.002f, chrome);

            // Top Claw Rail + Micro Reflex Red-Dot Sight
            AddPicatinnyRail("ClawRail", c, new Vector3(0f, 0.038f, -0.02f), 0.12f, 0.020f, steel, 6);
            c.Rig.AimAnchorLocalPos = BuildHoloSight(c, new Vector3(0f, 0.042f, -0.015f), poly, steel, compact: true);
            c.Rig.AdsDistanceZ = 0.26f;
        }

        static void BuildP90(BuildCtx c)
        {
            Material poly = GetMat(FinishType.MattePolymer, new Color(0.17f, 0.18f, 0.20f));
            Material knurl = GetMat(FinishType.KnurledGrip, new Color(0.13f, 0.14f, 0.16f));
            Material steel = GetMat(FinishType.MachinedSteel, new Color(0.24f, 0.26f, 0.29f));
            Material smokeMag = GetMat(FinishType.TintedGlass, new Color(0.26f, 0.24f, 0.20f, 0.65f));
            Material brass = GetMat(FinishType.Brass, new Color(0.86f, 0.70f, 0.26f));
            Material copper = GetMat(FinishType.Copper, new Color(0.76f, 0.42f, 0.22f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Extruded P90 Bullpup Ergonomic Thumbhole Silhouette (Upper spine + lower butt + forward grip bridge)
            AddChamfer("UpperChassis", c, new Vector3(0f, 0.018f, 0.02f), new Vector3(0.048f, 0.036f, 0.34f), 0.008f, poly);
            AddChamfer("LowerButt", c, new Vector3(0f, -0.024f, -0.14f), new Vector3(0.046f, 0.072f, 0.13f), 0.008f, poly);
            AddExtrudedProfile("BullpupGrip", c, GripProfile(0.046f, 0.095f, 16f), 0.036f, new Vector3(0f, -0.008f, 0.035f), knurl);
            AddChamfer("ForwardThumbBridge", c, new Vector3(0f, -0.025f, 0.145f), new Vector3(0.042f, 0.056f, 0.075f), 0.008f, poly);
            AddChamfer("BottomBridge", c, new Vector3(0f, -0.055f, -0.035f), new Vector3(0.034f, 0.016f, 0.27f), 0.004f, poly);
            AddChamfer("ButtPad", c, new Vector3(0f, -0.016f, -0.208f), new Vector3(0.048f, 0.098f, 0.014f), 0.004f, knurl);
            AddTriggerAssembly(c, new Vector3(0f, -0.022f, 0.088f), steel);

            // Translucent Horizontal 50-rd Top Magazine with Visible 5.7mm Brass Cartridges!
            AddChamfer("SmokeTopMag", c, new Vector3(0f, 0.046f, 0.01f), new Vector3(0.044f, 0.024f, 0.25f), 0.004f, smokeMag);
            for (int i = 0; i < 12; i++)
            {
                float zPos = -0.095f + i * 0.017f;
                AddCylinder("Round57_" + i, c, new Vector3(-0.005f, 0.046f, zPos), 0.0042f, 0.024f, brass, new Vector3(0f, 0f, 90f));
                AddCylinder("Tip57_" + i, c, new Vector3(0.011f, 0.046f, zPos), 0.0032f, 0.009f, copper, new Vector3(0f, 0f, 90f));
            }

            // Animated Side Charging Knob + Bottom Ejection Marker
            Transform bolt = AddChamfer("P90ChargingHandle", c, new Vector3(0.026f, 0.024f, 0.12f), new Vector3(0.012f, 0.010f, 0.024f), 0.002f, steel).transform;
            c.Rig.BoltCarrier = bolt;
            c.Rig.BoltRestPos = bolt.localPosition;
            c.Rig.BoltTravelZ = -0.030f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0f, -0.062f, -0.07f));

            // Forward Barrel + Dual-Pillar Upper Receiver Bridge + Reflex Sight
            AddMuzzleDevice("P90FlashHider", c, new Vector3(0f, 0.022f, 0.225f), 0.012f, 0.046f, steel, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.022f, 0.250f));
            AddChamfer("ReflexBridge", c, new Vector3(0f, 0.056f, 0.165f), new Vector3(0.038f, 0.048f, 0.055f), 0.006f, steel);
            c.Rig.AimAnchorLocalPos = BuildHoloSight(c, new Vector3(0f, 0.082f, 0.155f), poly, steel, compact: true);
            c.Rig.AdsDistanceZ = 0.25f;
        }

        static void BuildM40A3(BuildCtx c)
        {
            Material odStock = GetMat(FinishType.KnurledGrip, new Color(0.23f, 0.28f, 0.20f));
            Material darkPad = GetMat(FinishType.MattePolymer, new Color(0.11f, 0.12f, 0.13f));
            Material steel = GetMat(FinishType.MachinedSteel, new Color(0.24f, 0.26f, 0.29f));
            Material chrome = GetMat(FinishType.Chrome, new Color(0.76f, 0.79f, 0.84f));
            Material scopeDark = GetMat(FinishType.AnodizedMetal, new Color(0.14f, 0.15f, 0.17f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Contoured McMillan A4 Olive-Drab Tactical Stock + Adjustable Cheek Riser
            AddExtrudedProfile("SniperStock", c, RifleStockProfile(0.27f, 0.11f, drop: 0.030f), 0.044f, new Vector3(0f, 0.002f, -0.075f), odStock);
            AddChamfer("CheekRiser", c, new Vector3(0f, 0.038f, -0.22f), new Vector3(0.046f, 0.032f, 0.125f), 0.008f, odStock);
            AddChamfer("StockButtpad", c, new Vector3(0f, -0.025f, -0.348f), new Vector3(0.046f, 0.115f, 0.018f), 0.004f, darkPad);
            AddChamfer("SniperChassisBody", c, new Vector3(0f, -0.008f, 0.055f), new Vector3(0.046f, 0.044f, 0.28f), 0.007f, odStock);
            AddChamfer("VentedForend", c, new Vector3(0f, -0.002f, 0.26f), new Vector3(0.042f, 0.036f, 0.18f), 0.008f, odStock);
            AddExtrudedProfile("SniperGrip", c, GripProfile(0.042f, 0.095f, 22f), 0.036f, new Vector3(0f, -0.025f, -0.068f), odStock);
            AddTriggerAssembly(c, new Vector3(0f, -0.032f, -0.010f), steel);

            // Action Receiver + Animated Stainless Bolt Action
            AddCylinder("ActionReceiver", c, new Vector3(0f, 0.022f, 0.02f), 0.019f, 0.20f, steel, new Vector3(90f, 0f, 0f));
            AddBox("ActionPort", c, new Vector3(0.015f, 0.026f, 0.025f), new Vector3(0.010f, 0.014f, 0.065f), boreDark);
            var bolt = new GameObject("BoltAssembly");
            bolt.transform.SetParent(c.Root, false);
            bolt.transform.localPosition = new Vector3(0.002f, 0.024f, -0.020f);
            AddCylinder("BoltCyl", c, Vector3.zero, 0.011f, 0.09f, chrome, new Vector3(90f, 0f, 0f), bolt.transform);
            AddChamfer("BoltHandleArm", c, new Vector3(0.022f, -0.008f, -0.030f), new Vector3(0.032f, 0.008f, 0.009f), 0.002f, chrome, new Vector3(0f, 0f, -30f), bolt.transform);
            AddCylinder("BoltKnob", c, new Vector3(0.036f, -0.018f, -0.030f), 0.009f, 0.018f, steel, Vector3.zero, bolt.transform);
            c.Rig.BoltCarrier = bolt.transform;
            c.Rig.BoltRestPos = bolt.transform.localPosition;
            c.Rig.BoltTravelZ = -0.065f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.032f, 0.028f, 0.028f));

            // Heavy Fluted Free-Floating Stainless Barrel + Slotted Muzzle Brake + Folded Bipod
            AddCylinder("HeavyBarrel", c, new Vector3(0f, 0.024f, 0.39f), 0.0125f, 0.56f, chrome, new Vector3(90f, 0f, 0f));
            AddMuzzleDevice("MuzzleBrake", c, new Vector3(0f, 0.024f, 0.685f), 0.0155f, 0.052f, steel, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.024f, 0.715f));

            AddChamfer("BipodMount", c, new Vector3(0f, -0.026f, 0.32f), new Vector3(0.032f, 0.016f, 0.038f), 0.003f, steel);
            AddCylinder("BipodLegL", c, new Vector3(-0.026f, -0.032f, 0.42f), 0.0065f, 0.19f, steel, new Vector3(90f, 0f, 0f));
            AddCylinder("BipodLegR", c, new Vector3(0.026f, -0.032f, 0.42f), 0.0065f, 0.19f, steel, new Vector3(90f, 0f, 0f));

            // Precision Multi-Stage Mil-Dot Scope
            AddPicatinnyRail("ScopeBase", c, new Vector3(0f, 0.042f, 0.02f), 0.16f, 0.020f, steel, 8);
            c.Rig.AimAnchorLocalPos = BuildMilDotScope(c, new Vector3(0f, 0.072f, 0.025f), scopeDark, steel);
            c.Rig.AdsDistanceZ = 0.24f;
        }

        static void BuildW1200(BuildCtx c)
        {
            Material walnut = GetMat(FinishType.WalnutWood, new Color(0.50f, 0.27f, 0.13f));
            Material steel = GetMat(FinishType.MachinedSteel, new Color(0.24f, 0.26f, 0.29f));
            Material darkSteel = GetMat(FinishType.AnodizedMetal, new Color(0.15f, 0.16f, 0.18f));
            Material chrome = GetMat(FinishType.Chrome, new Color(0.80f, 0.82f, 0.86f));
            Material knurl = GetMat(FinishType.KnurledGrip, new Color(0.13f, 0.14f, 0.15f));
            Material brass = GetMat(FinishType.Brass, new Color(0.88f, 0.72f, 0.28f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Machined Receiver + Chrome Bolt Window
            AddChamfer("ShotgunReceiver", c, new Vector3(0f, 0.008f, 0.01f), new Vector3(0.040f, 0.048f, 0.20f), 0.007f, darkSteel);
            Transform bolt = AddChamfer("ChromeBolt", c, new Vector3(0.018f, 0.018f, 0.02f), new Vector3(0.008f, 0.016f, 0.055f), 0.002f, chrome).transform;
            c.Rig.BoltCarrier = bolt;
            c.Rig.BoltRestPos = bolt.localPosition;
            c.Rig.BoltTravelZ = -0.050f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.030f, 0.020f, 0.025f));
            AddTriggerAssembly(c, new Vector3(0f, -0.024f, -0.03f), steel);

            // Monte Carlo Walnut Stock & Wrist Grip
            AddExtrudedProfile("WalnutStock", c, RifleStockProfile(0.25f, 0.105f, drop: 0.045f), 0.038f, new Vector3(0f, 0.004f, -0.088f), walnut);
            AddChamfer("RecoilPad", c, new Vector3(0f, -0.042f, -0.340f), new Vector3(0.040f, 0.108f, 0.016f), 0.004f, knurl);

            // Over-Under Barrel + Tubular Magazine + Barrel Clamp + Vent Rib
            AddMuzzleDevice("ShotgunBarrel", c, new Vector3(0f, 0.023f, 0.34f), 0.012f, 0.46f, steel, boreDark);
            AddCylinder("MagTube", c, new Vector3(0f, 0.001f, 0.295f), 0.011f, 0.37f, darkSteel, new Vector3(90f, 0f, 0f));
            AddCylinder("MagCap", c, new Vector3(0f, 0.001f, 0.485f), 0.0125f, 0.022f, knurl, new Vector3(90f, 0f, 0f));
            AddChamfer("BarrelClamp", c, new Vector3(0f, 0.012f, 0.46f), new Vector3(0.028f, 0.044f, 0.016f), 0.003f, darkSteel);
            AddChamfer("VentRib", c, new Vector3(0f, 0.036f, 0.33f), new Vector3(0.011f, 0.006f, 0.44f), 0.001f, steel);
            AddCylinder("BrassBead", c, new Vector3(0f, 0.042f, 0.545f), 0.003f, 0.006f, brass);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.023f, 0.575f));

            // Animated Ribbed Pump Forend (slides back on fire!)
            var pumpGo = new GameObject("PumpForend");
            pumpGo.transform.SetParent(c.Root, false);
            pumpGo.transform.localPosition = new Vector3(0f, 0.001f, 0.255f);
            AddChamfer("PumpSleeve", c, Vector3.zero, new Vector3(0.042f, 0.038f, 0.145f), 0.009f, walnut, Vector3.zero, pumpGo.transform);
            for (int i = -3; i <= 3; i++)
                AddChamfer("PumpRib_" + i, c, new Vector3(0f, -0.002f, i * 0.018f), new Vector3(0.044f, 0.036f, 0.008f), 0.002f, knurl, Vector3.zero, pumpGo.transform);
            c.Rig.PumpForend = pumpGo.transform;
            c.Rig.PumpRestPos = pumpGo.transform.localPosition;

            c.Rig.AimAnchorLocalPos = Build3DotIronSights(c,
                new Vector3(0f, 0.040f, 0.08f),
                new Vector3(0f, 0.045f, 0.545f),
                darkSteel, 0.006f);
            c.Rig.AdsDistanceZ = 0.26f;
        }

        static void BuildM249(BuildCtx c)
        {
            Material odBody = GetMat(FinishType.AnodizedMetal, new Color(0.20f, 0.22f, 0.18f));
            Material steel = GetMat(FinishType.MachinedSteel, new Color(0.24f, 0.26f, 0.28f));
            Material darkPoly = GetMat(FinishType.KnurledGrip, new Color(0.14f, 0.15f, 0.14f));
            Material odBox = GetMat(FinishType.MattePolymer, new Color(0.22f, 0.26f, 0.19f));
            Material brass = GetMat(FinishType.Brass, new Color(0.88f, 0.72f, 0.26f));
            Material copper = GetMat(FinishType.Copper, new Color(0.78f, 0.44f, 0.22f));
            Material chrome = GetMat(FinishType.Chrome, new Color(0.76f, 0.79f, 0.83f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Heavy Reinforced SAW Receiver + Hinged Feed Cover
            AddChamfer("SAWReceiver", c, new Vector3(0f, 0.005f, 0.02f), new Vector3(0.062f, 0.068f, 0.27f), 0.006f, odBody);
            AddChamfer("FeedTopCover", c, new Vector3(0f, 0.044f, 0.015f), new Vector3(0.056f, 0.018f, 0.195f), 0.004f, steel);
            AddPicatinnyRail("FeedRail", c, new Vector3(0f, 0.056f, 0.015f), 0.17f, 0.022f, steel, 8);

            // Animated Side Charging Handle + Ejection Port
            Transform bolt = AddChamfer("SAWChargingHandle", c, new Vector3(0.036f, 0.018f, 0.06f), new Vector3(0.022f, 0.012f, 0.022f), 0.003f, chrome).transform;
            c.Rig.BoltCarrier = bolt;
            c.Rig.BoltRestPos = bolt.localPosition;
            c.Rig.BoltTravelZ = -0.055f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.040f, 0.012f, 0.035f));

            // Grip, Trigger & Heavy Para Tubular Stock
            AddExtrudedProfile("SAWGrip", c, GripProfile(0.042f, 0.105f, 16f), 0.034f, new Vector3(0f, -0.028f, -0.075f), darkPoly);
            AddTriggerAssembly(c, new Vector3(0f, -0.036f, -0.020f), steel);
            AddCylinder("StockUpperRod", c, new Vector3(0f, 0.020f, -0.22f), 0.010f, 0.22f, steel, new Vector3(90f, 0f, 0f));
            AddCylinder("StockLowerRod", c, new Vector3(0f, -0.015f, -0.22f), 0.008f, 0.22f, steel, new Vector3(90f, 0f, 0f));
            AddChamfer("SAWButtPad", c, new Vector3(0f, 0.002f, -0.335f), new Vector3(0.048f, 0.115f, 0.022f), 0.005f, darkPoly);

            // 100-rd OD Green Ammo Box + Linked Brass 5.56mm Belt Feeding into Left/Top Side!
            AddChamfer("AmmoBox100", c, new Vector3(-0.012f, -0.088f, 0.055f), new Vector3(0.098f, 0.105f, 0.125f), 0.008f, odBox);
            AddChamfer("AmmoBoxRib", c, new Vector3(-0.012f, -0.088f, 0.055f), new Vector3(0.102f, 0.024f, 0.128f), 0.003f, darkPoly);
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                Vector3 linkPos = Vector3.Lerp(new Vector3(-0.052f, -0.035f, 0.050f), new Vector3(-0.034f, 0.026f, 0.050f), t);
                AddCylinder("BeltCase_" + i, c, linkPos, 0.0048f, 0.034f, brass, new Vector3(90f, 0f, 0f));
                AddCylinder("BeltTip_" + i, c, linkPos + new Vector3(0f, 0f, 0.021f), 0.0036f, 0.011f, copper, new Vector3(90f, 0f, 0f));
                AddBox("BeltLink_" + i, c, linkPos, new Vector3(0.011f, 0.011f, 0.012f), darkPoly);
            }

            // Heavy Fluted Barrel + Perforated Heatshield + Carry Handle + Bipod
            AddChamfer("Heatshield", c, new Vector3(0f, 0.038f, 0.255f), new Vector3(0.048f, 0.024f, 0.20f), 0.007f, steel);
            AddChamfer("CarryHandleBar", c, new Vector3(0.028f, 0.058f, 0.20f), new Vector3(0.012f, 0.014f, 0.095f), 0.003f, darkPoly);
            AddCylinder("GasRegTube", c, new Vector3(0f, 0.005f, 0.28f), 0.011f, 0.26f, steel, new Vector3(90f, 0f, 0f));
            AddMuzzleDevice("SAWBarrel", c, new Vector3(0f, 0.026f, 0.39f), 0.013f, 0.44f, steel, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.026f, 0.615f));

            AddCylinder("SAWBipodL", c, new Vector3(-0.032f, -0.012f, 0.31f), 0.007f, 0.21f, steel, new Vector3(90f, 0f, 0f));
            AddCylinder("SAWBipodR", c, new Vector3(0.032f, -0.012f, 0.31f), 0.007f, 0.21f, steel, new Vector3(90f, 0f, 0f));

            c.Rig.AimAnchorLocalPos = BuildHoloSight(c, new Vector3(0f, 0.062f, 0.015f), darkPoly, steel);
            c.Rig.AdsDistanceZ = 0.28f;
        }

        static void BuildM9(BuildCtx c)
        {
            Material frameMat = GetMat(FinishType.AnodizedMetal, new Color(0.15f, 0.16f, 0.18f));
            Material slideMat = GetMat(FinishType.MachinedSteel, new Color(0.22f, 0.24f, 0.27f));
            Material chrome = GetMat(FinishType.Chrome, new Color(0.80f, 0.83f, 0.87f));
            Material gripMat = GetMat(FinishType.KnurledGrip, new Color(0.12f, 0.13f, 0.14f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Frame + Under-Rail + Checkered Grip + Trigger
            AddChamfer("M9Frame", c, new Vector3(0f, -0.002f, 0.035f), new Vector3(0.028f, 0.022f, 0.165f), 0.003f, frameMat);
            AddPicatinnyRail("UnderRail", c, new Vector3(0f, -0.014f, 0.095f), 0.052f, 0.018f, frameMat, 3, new Vector3(0f, 0f, 180f));
            AddExtrudedProfile("M9Grip", c, GripProfile(0.036f, 0.095f, 15f), 0.027f, new Vector3(0f, -0.010f, -0.028f), gripMat);
            AddChamfer("MagBaseplate", c, new Vector3(0f, -0.105f, -0.048f), new Vector3(0.025f, 0.010f, 0.040f), 0.002f, gripMat, new Vector3(15f, 0f, 0f));
            AddTriggerAssembly(c, new Vector3(0f, -0.018f, 0.018f), slideMat, compact: true);

            // Exposed Chrome Barrel (Open-top Beretta trademark!)
            AddMuzzleDevice("M9Barrel", c, new Vector3(0f, 0.018f, 0.072f), 0.0085f, 0.145f, chrome, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.018f, 0.148f));

            // Animated Open-Top Slide Assembly + Rear Sight + Serrations
            var slide = new GameObject("M9Slide");
            slide.transform.SetParent(c.Root, false);
            slide.transform.localPosition = new Vector3(0f, 0.018f, 0.020f);
            AddChamfer("SlideRear", c, new Vector3(0f, 0f, -0.055f), new Vector3(0.029f, 0.026f, 0.072f), 0.004f, slideMat, Vector3.zero, slide.transform);
            AddChamfer("SlideSideL", c, new Vector3(-0.0115f, -0.004f, 0.042f), new Vector3(0.006f, 0.018f, 0.125f), 0.0015f, slideMat, Vector3.zero, slide.transform);
            AddChamfer("SlideSideR", c, new Vector3(0.0115f, -0.004f, 0.042f), new Vector3(0.006f, 0.018f, 0.125f), 0.0015f, slideMat, Vector3.zero, slide.transform);
            AddChamfer("SlideBridge", c, new Vector3(0f, 0.005f, 0.095f), new Vector3(0.029f, 0.012f, 0.028f), 0.003f, slideMat, Vector3.zero, slide.transform);
            for (int i = 0; i < 6; i++)
                AddBox("Serr_" + i, c, new Vector3(0f, 0f, -0.080f + i * 0.0055f), new Vector3(0.0305f, 0.018f, 0.0022f), boreDark, Vector3.zero, slide.transform);

            c.Rig.BoltCarrier = slide.transform;
            c.Rig.BoltRestPos = slide.transform.localPosition;
            c.Rig.BoltTravelZ = -0.035f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.020f, 0.024f, 0.030f));

            AddChamfer("HammerSpur", c, new Vector3(0f, 0.009f, -0.070f), new Vector3(0.007f, 0.014f, 0.014f), 0.002f, chrome, new Vector3(-35f, 0f, 0f));

            c.Rig.AimAnchorLocalPos = Build3DotIronSights(c,
                new Vector3(0f, 0.034f, -0.062f),
                new Vector3(0f, 0.034f, 0.115f),
                slideMat, 0.005f);
            c.Rig.HipLocalPos = new Vector3(0.19f, -0.18f, 0.40f);
            c.Rig.AdsDistanceZ = 0.34f;
        }

        static void BuildDesertEagle(BuildCtx c)
        {
            Material chrome = GetMat(FinishType.Chrome, new Color(0.82f, 0.85f, 0.89f));
            Material satin = GetMat(FinishType.MachinedSteel, new Color(0.58f, 0.61f, 0.65f));
            Material gripMat = GetMat(FinishType.KnurledGrip, new Color(0.11f, 0.12f, 0.13f));
            Material darkSteel = GetMat(FinishType.AnodizedMetal, new Color(0.18f, 0.19f, 0.21f));
            Material boreDark = GetMat(FinishType.RecessDark, new Color(0.03f, 0.03f, 0.04f));

            // Heavy Frame, Wraparound Rubber Grip & Trigger
            AddChamfer("DeagleFrame", c, new Vector3(0f, 0f, 0.050f), new Vector3(0.034f, 0.026f, 0.205f), 0.004f, satin);
            AddExtrudedProfile("DeagleGrip", c, GripProfile(0.044f, 0.102f, 18f), 0.032f, new Vector3(0f, -0.010f, -0.032f), gripMat);
            AddChamfer("DeagleMagPad", c, new Vector3(0f, -0.112f, -0.056f), new Vector3(0.030f, 0.012f, 0.046f), 0.003f, chrome, new Vector3(18f, 0f, 0f));
            AddTriggerAssembly(c, new Vector3(0f, -0.018f, 0.024f), darkSteel, compact: true);

            // Signature Polygonal Triangular-Bevel Top Barrel Shroud + Huge .50 Bore
            AddExtrudedProfile("PolygonalBarrelShroud", c, DeagleShroudProfile(), 0.21f, new Vector3(0f, 0.022f, 0.075f), chrome, new Vector3(0f, 90f, 0f));
            AddPicatinnyRail("DeagleTopRib", c, new Vector3(0f, 0.044f, 0.095f), 0.12f, 0.016f, satin, 6);
            AddMuzzleDevice("Bore50AE", c, new Vector3(0f, 0.024f, 0.175f), 0.012f, 0.024f, satin, boreDark);
            c.Rig.Muzzle = MakeMarker(c.Root, "Muzzle", new Vector3(0f, 0.024f, 0.190f));

            // Animated Heavy Rear Slide + Safety Wings + Serrations
            var slide = new GameObject("DeagleSlide");
            slide.transform.SetParent(c.Root, false);
            slide.transform.localPosition = new Vector3(0f, 0.024f, -0.015f);
            AddChamfer("RearSlideBlock", c, new Vector3(0f, 0f, -0.030f), new Vector3(0.036f, 0.032f, 0.092f), 0.005f, chrome, Vector3.zero, slide.transform);
            AddChamfer("SafetyWing", c, new Vector3(-0.020f, 0.004f, -0.060f), new Vector3(0.008f, 0.012f, 0.022f), 0.002f, darkSteel, Vector3.zero, slide.transform);
            for (int i = 0; i < 6; i++)
                AddBox("DSerr_" + i, c, new Vector3(0f, 0f, -0.068f + i * 0.006f), new Vector3(0.0375f, 0.022f, 0.0025f), darkSteel, Vector3.zero, slide.transform);

            c.Rig.BoltCarrier = slide.transform;
            c.Rig.BoltRestPos = slide.transform.localPosition;
            c.Rig.BoltTravelZ = -0.042f;
            c.Rig.EjectPort = MakeMarker(c.Root, "EjectPort", new Vector3(0.024f, 0.032f, 0.035f));

            c.Rig.AimAnchorLocalPos = Build3DotIronSights(c,
                new Vector3(0f, 0.043f, -0.072f),
                new Vector3(0f, 0.046f, 0.165f),
                darkSteel, 0.0055f);
            c.Rig.HipLocalPos = new Vector3(0.19f, -0.18f, 0.41f);
            c.Rig.AdsDistanceZ = 0.34f;
        }

        // =====================================================================
        // SHARED CAD SUBASSEMBLIES (RAILS, OPTICS, SIGHTS, MUZZLE, TRIGGER)
        // =====================================================================

        static void AddPicatinnyRail(string name, BuildCtx c, Vector3 centerPos, float lengthZ, float widthX, Material mat, int teeth, Vector3 rot = default)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(c.Root, false);
            holder.transform.localPosition = centerPos;
            holder.transform.localRotation = Quaternion.Euler(rot);

            AddChamfer("Base", c, Vector3.zero, new Vector3(widthX * 0.84f, 0.005f, lengthZ), 0.001f, mat, Vector3.zero, holder.transform);
            float step = lengthZ / Mathf.Max(1, teeth);
            float startZ = -lengthZ * 0.5f + step * 0.5f;
            for (int i = 0; i < teeth; i++)
            {
                AddChamfer("T" + i, c, new Vector3(0f, 0.0045f, startZ + i * step),
                    new Vector3(widthX, 0.0045f, step * 0.56f), 0.0008f, mat, Vector3.zero, holder.transform);
            }
        }

        static void AddTriggerAssembly(BuildCtx c, Vector3 pos, Material steel, bool compact = false)
        {
            float w = compact ? 0.026f : 0.032f;
            AddChamfer("TriggerGuardBot", c, pos + new Vector3(0f, -0.012f, 0.002f), new Vector3(0.011f, 0.004f, w), 0.001f, steel);
            AddChamfer("TriggerGuardFwd", c, pos + new Vector3(0f, -0.002f, w * 0.5f), new Vector3(0.011f, 0.022f, 0.004f), 0.001f, steel);
            AddChamfer("TriggerBlade", c, pos + new Vector3(0f, -0.001f, -0.002f), new Vector3(0.005f, 0.016f, 0.005f), 0.001f, steel, new Vector3(20f, 0f, 0f));
        }

        static void AddMuzzleDevice(string name, BuildCtx c, Vector3 pos, float radius, float length, Material mat, Material boreMat)
        {
            AddCylinder(name, c, pos, radius, length, mat, new Vector3(90f, 0f, 0f));
            AddCylinder(name + "_Bore", c, pos + new Vector3(0f, 0f, length * 0.44f), radius * 0.56f, length * 0.14f, boreMat, new Vector3(90f, 0f, 0f));
        }

        static Vector3 BuildHoloSight(BuildCtx c, Vector3 basePos, Material hoodMat, Material railMat, bool compact = false)
        {
            Material glass = GetMat(FinishType.TintedGlass, new Color(0.24f, 0.50f, 0.64f, 0.18f));
            Material dotMat = GetMat(FinishType.ReticleGlow, new Color(1.0f, 0.18f, 0.14f));
            float w = compact ? 0.028f : 0.034f;
            float h = compact ? 0.022f : 0.028f;
            float l = compact ? 0.054f : 0.072f;

            var g = new GameObject("HoloOptic");
            g.transform.SetParent(c.Root, false);
            g.transform.localPosition = basePos;

            AddChamfer("MountBase", c, new Vector3(0f, 0.006f, 0f), new Vector3(w * 0.92f, 0.012f, l), 0.002f, railMat, Vector3.zero, g.transform);
            AddChamfer("HoodL", c, new Vector3(-w * 0.45f, 0.012f + h * 0.5f, -l * 0.08f), new Vector3(0.005f, h, l * 0.65f), 0.001f, hoodMat, Vector3.zero, g.transform);
            AddChamfer("HoodR", c, new Vector3(w * 0.45f, 0.012f + h * 0.5f, -l * 0.08f), new Vector3(0.005f, h, l * 0.65f), 0.001f, hoodMat, Vector3.zero, g.transform);
            AddChamfer("HoodTop", c, new Vector3(0f, 0.012f + h, -l * 0.08f), new Vector3(w, 0.005f, l * 0.65f), 0.001f, hoodMat, Vector3.zero, g.transform);
            if (!compact)
                AddChamfer("BatteryPack", c, new Vector3(0f, 0.018f, l * 0.28f), new Vector3(w * 0.85f, 0.016f, l * 0.34f), 0.003f, hoodMat, Vector3.zero, g.transform);

            Vector3 reticleLocal = new Vector3(0f, 0.012f + h * 0.5f, -l * 0.08f);
            AddBox("GlassLens", c, reticleLocal, new Vector3(w * 0.72f, h * 0.72f, 0.0015f), glass, Vector3.zero, g.transform);
            AddCylinder("ReticleDot", c, reticleLocal, 0.0014f, 0.0018f, dotMat, new Vector3(90f, 0f, 0f), g.transform);
            AddBox("ReticleTickL", c, reticleLocal + new Vector3(-0.0046f, 0f, 0f), new Vector3(0.0024f, 0.0007f, 0.0014f), dotMat, Vector3.zero, g.transform);
            AddBox("ReticleTickR", c, reticleLocal + new Vector3(0.0046f, 0f, 0f), new Vector3(0.0024f, 0.0007f, 0.0014f), dotMat, Vector3.zero, g.transform);
            AddBox("ReticleTickT", c, reticleLocal + new Vector3(0f, 0.0046f, 0f), new Vector3(0.0007f, 0.0024f, 0.0014f), dotMat, Vector3.zero, g.transform);

            return basePos + reticleLocal;
        }

        static Vector3 BuildMilDotScope(BuildCtx c, Vector3 tubeCenter, Material scopeMat, Material ringMat)
        {
            Material glass = GetMat(FinishType.TintedGlass, new Color(0.14f, 0.46f, 0.42f, 0.65f));
            Material dotMat = GetMat(FinishType.ReticleGlow, new Color(0.95f, 0.22f, 0.16f));

            var g = new GameObject("MilDotScope");
            g.transform.SetParent(c.Root, false);
            g.transform.localPosition = tubeCenter;

            // Main 30mm tube + flared objective bell + ocular eyepiece + 3 micrometer turrets + dual scope rings
            AddCylinder("MainTube", c, Vector3.zero, 0.015f, 0.17f, scopeMat, new Vector3(90f, 0f, 0f), g.transform);
            AddCylinder("ObjectiveBell", c, new Vector3(0f, 0f, 0.115f), 0.023f, 0.075f, scopeMat, new Vector3(90f, 0f, 0f), g.transform);
            AddCylinder("OcularBell", c, new Vector3(0f, 0f, -0.105f), 0.019f, 0.055f, scopeMat, new Vector3(90f, 0f, 0f), g.transform);
            AddCylinder("ElevTurret", c, new Vector3(0f, 0.020f, 0f), 0.0085f, 0.014f, ringMat, Vector3.zero, g.transform);
            AddCylinder("WindTurret", c, new Vector3(0.020f, 0f, 0f), 0.0085f, 0.014f, ringMat, new Vector3(0f, 0f, 90f), g.transform);
            AddCylinder("ParallaxTurret", c, new Vector3(-0.020f, 0f, 0f), 0.0085f, 0.014f, ringMat, new Vector3(0f, 0f, 90f), g.transform);
            AddChamfer("RingFwd", c, new Vector3(0f, -0.012f, 0.048f), new Vector3(0.036f, 0.038f, 0.018f), 0.003f, ringMat, Vector3.zero, g.transform);
            AddChamfer("RingBack", c, new Vector3(0f, -0.012f, -0.048f), new Vector3(0.036f, 0.038f, 0.018f), 0.003f, ringMat, Vector3.zero, g.transform);

            AddCylinder("OcularLens", c, new Vector3(0f, 0f, -0.132f), 0.0155f, 0.003f, glass, new Vector3(90f, 0f, 0f), g.transform);
            AddCylinder("ObjectiveLens", c, new Vector3(0f, 0f, 0.152f), 0.0195f, 0.003f, glass, new Vector3(90f, 0f, 0f), g.transform);
            AddCylinder("ReticleCenter", c, new Vector3(0f, 0f, -0.130f), 0.0014f, 0.002f, dotMat, new Vector3(90f, 0f, 0f), g.transform);

            return tubeCenter;
        }

        static Vector3 Build3DotIronSights(BuildCtx c, Vector3 rearPos, Vector3 frontPos, Material mat, float height)
        {
            Material dot = GetMat(FinishType.TritiumDot, new Color(0.28f, 1.0f, 0.42f));
            // Rear twin posts forming a crisp U-notch + glowing white/tritium alignment dots
            AddChamfer("RearSightL", c, rearPos + new Vector3(-0.007f, 0f, 0f), new Vector3(0.005f, height, 0.010f), 0.001f, mat);
            AddChamfer("RearSightR", c, rearPos + new Vector3(0.007f, 0f, 0f), new Vector3(0.005f, height, 0.010f), 0.001f, mat);
            AddChamfer("RearDotL", c, rearPos + new Vector3(-0.007f, height * 0.20f, -0.0052f), new Vector3(0.0024f, 0.0024f, 0.001f), 0.0004f, dot);
            AddChamfer("RearDotR", c, rearPos + new Vector3(0.007f, height * 0.20f, -0.0052f), new Vector3(0.0024f, 0.0024f, 0.001f), 0.0004f, dot);

            // Front blade + center tritium dot
            AddChamfer("FrontBlade", c, frontPos, new Vector3(0.0032f, height, 0.012f), 0.0006f, mat);
            AddChamfer("FrontDot", c, frontPos + new Vector3(0f, height * 0.22f, -0.0062f), new Vector3(0.0022f, 0.0022f, 0.001f), 0.0004f, dot);

            return rearPos + new Vector3(0f, height * 0.22f, 0f);
        }

        // =====================================================================
        // CAD PRIMITIVE SPAWNERS (BEVELED, EXTRUDED, HOLLOW CYLINDERS)
        // =====================================================================

        static GameObject AddChamfer(string name, BuildCtx c, Vector3 pos, Vector3 size, float bevel, Material mat, Vector3 euler = default, Transform parentOverride = null)
        {
            Mesh m = GetChamferMesh(size, bevel);
            return SpawnMeshPart(name, m, pos, Vector3.one, euler, mat, c.CastMode, parentOverride != null ? parentOverride : c.Root);
        }

        static GameObject AddBox(string name, BuildCtx c, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default, Transform parentOverride = null)
        {
            Mesh m = GetChamferMesh(size, 0f);
            return SpawnMeshPart(name, m, pos, Vector3.one, euler, mat, c.CastMode, parentOverride != null ? parentOverride : c.Root);
        }

        static GameObject AddCylinder(string name, BuildCtx c, Vector3 pos, float radius, float length, Material mat, Vector3 euler = default, Transform parentOverride = null)
        {
            Mesh m = GetCylinderMesh();
            return SpawnMeshPart(name, m, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), euler, mat, c.CastMode, parentOverride != null ? parentOverride : c.Root);
        }

        static GameObject AddExtrudedProfile(string name, BuildCtx c, Vector2[] ptsZY, float widthX, Vector3 pos, Material mat, Vector3 euler = default)
        {
            string key = "EXT_" + name + "_" + widthX.ToString("F3");
            if (!MeshCache.TryGetValue(key, out Mesh m) || m == null)
            {
                m = BuildExtrudedMesh(ptsZY, widthX);
                MeshCache[key] = m;
            }
            return SpawnMeshPart(name, m, pos, Vector3.one, euler, mat, c.CastMode, c.Root);
        }

        static GameObject SpawnMeshPart(string name, Mesh mesh, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, ShadowCastingMode castMode, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castMode;
            return go;
        }

        static Transform MakeMarker(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        // =====================================================================
        // 2D CAD PROFILE SILHOUETTES (Z = forward, Y = up)
        // =====================================================================

        static Vector2[] GripProfile(float depth, float height, float angleDeg)
        {
            float shiftZ = -Mathf.Tan(angleDeg * Mathf.Deg2Rad) * height;
            return new[]
            {
                new Vector2(-depth * 0.5f, 0f),
                new Vector2(depth * 0.5f, 0f),
                new Vector2(shiftZ + depth * 0.45f, -height * 0.94f),
                new Vector2(shiftZ + depth * 0.25f, -height),
                new Vector2(shiftZ - depth * 0.52f, -height),
                new Vector2(shiftZ - depth * 0.52f, -height * 0.48f)
            };
        }

        static Vector2[] CurvedMagProfile(float depth, float height, float curveForwardZ)
        {
            return new[]
            {
                new Vector2(-depth * 0.5f, 0f),
                new Vector2(depth * 0.5f, 0f),
                new Vector2(depth * 0.5f + curveForwardZ * 0.35f, -height * 0.50f),
                new Vector2(depth * 0.5f + curveForwardZ, -height),
                new Vector2(-depth * 0.5f + curveForwardZ, -height),
                new Vector2(-depth * 0.5f + curveForwardZ * 0.35f, -height * 0.50f)
            };
        }

        static Vector2[] RifleStockProfile(float length, float buttHeight, float drop = 0.018f)
        {
            return new[]
            {
                new Vector2(0f, 0.015f),
                new Vector2(0f, -0.025f),
                new Vector2(-length, -drop - buttHeight * 0.82f),
                new Vector2(-length, -drop + buttHeight * 0.25f),
                new Vector2(-length * 0.25f, 0.012f)
            };
        }

        static Vector2[] AFrameProfile(float baseLen = 0.042f, float topH = 0.046f)
        {
            return new[]
            {
                new Vector2(-baseLen * 0.5f, 0f),
                new Vector2(baseLen * 0.5f, 0f),
                new Vector2(0.008f, topH),
                new Vector2(-0.008f, topH)
            };
        }

        static Vector2[] DeagleShroudProfile()
        {
            // Extruded sideways along Z -> cross section in X (width) & Y (height)
            // Classic triangular chamfered top magnum barrel profile
            return new[]
            {
                new Vector2(-0.016f, -0.012f),
                new Vector2(0.016f, -0.012f),
                new Vector2(0.016f, 0.004f),
                new Vector2(0.009f, 0.019f),
                new Vector2(-0.009f, 0.019f),
                new Vector2(-0.016f, 0.004f)
            };
        }

        // =====================================================================
        // PROCEDURAL MESH GENERATORS (OUTWARD-VERIFIED NORMALS & FACETING)
        // =====================================================================

        static Mesh GetChamferMesh(Vector3 size, float bevel)
        {
            string key = $"CH_{size.x:F4}_{size.y:F4}_{size.z:F4}_{bevel:F4}";
            if (MeshCache.TryGetValue(key, out Mesh m) && m != null) return m;

            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;
            float b = Mathf.Min(bevel, Mathf.Min(hx, Mathf.Min(hy, hz)) * 0.42f);

            var mb = new MeshBuilder();
            if (b <= 0.0002f)
            {
                Vector3 c000 = new Vector3(-hx, -hy, -hz), c100 = new Vector3(hx, -hy, -hz);
                Vector3 c110 = new Vector3(hx, hy, -hz),   c010 = new Vector3(-hx, hy, -hz);
                Vector3 c001 = new Vector3(-hx, -hy, hz),  c101 = new Vector3(hx, -hy, hz);
                Vector3 c111 = new Vector3(hx, hy, hz),    c011 = new Vector3(-hx, hy, hz);
                mb.Quad(c100, c110, c111, c101, Vector3.right);
                mb.Quad(c001, c011, c010, c000, Vector3.left);
                mb.Quad(c010, c011, c111, c110, Vector3.up);
                mb.Quad(c001, c000, c100, c101, Vector3.down);
                mb.Quad(c001, c101, c111, c011, Vector3.forward);
                mb.Quad(c100, c000, c010, c110, Vector3.back);
            }
            else
            {
                float ix = hx - b, iy = hy - b, iz = hz - b;
                // 6 flat core faces
                mb.Quad(new Vector3(hx, -iy, -iz), new Vector3(hx, iy, -iz), new Vector3(hx, iy, iz), new Vector3(hx, -iy, iz), Vector3.right);
                mb.Quad(new Vector3(-hx, -iy, iz), new Vector3(-hx, iy, iz), new Vector3(-hx, iy, -iz), new Vector3(-hx, -iy, -iz), Vector3.left);
                mb.Quad(new Vector3(-ix, hy, -iz), new Vector3(-ix, hy, iz), new Vector3(ix, hy, iz), new Vector3(ix, hy, -iz), Vector3.up);
                mb.Quad(new Vector3(-ix, -hy, iz), new Vector3(-ix, -hy, -iz), new Vector3(ix, -hy, -iz), new Vector3(ix, -hy, iz), Vector3.down);
                mb.Quad(new Vector3(-ix, -iy, hz), new Vector3(ix, -iy, hz), new Vector3(ix, iy, hz), new Vector3(-ix, iy, hz), Vector3.forward);
                mb.Quad(new Vector3(ix, -iy, -hz), new Vector3(-ix, -iy, -hz), new Vector3(-ix, iy, -hz), new Vector3(ix, iy, -hz), Vector3.back);

                // 12 45-degree CAD chamfer bevels
                // X-aligned top/bottom bevels
                mb.Quad(new Vector3(-ix, hy, iz), new Vector3(-ix, iy, hz), new Vector3(ix, iy, hz), new Vector3(ix, hy, iz), new Vector3(0, 1, 1));
                mb.Quad(new Vector3(-ix, iy, -hz), new Vector3(-ix, hy, -iz), new Vector3(ix, hy, -iz), new Vector3(ix, iy, -hz), new Vector3(0, 1, -1));
                mb.Quad(new Vector3(-ix, -iy, hz), new Vector3(-ix, -hy, iz), new Vector3(ix, -hy, iz), new Vector3(ix, -iy, hz), new Vector3(0, -1, 1));
                mb.Quad(new Vector3(-ix, -hy, -iz), new Vector3(-ix, -iy, -hz), new Vector3(ix, -iy, -hz), new Vector3(ix, -hy, -iz), new Vector3(0, -1, -1));

                // Z-aligned top/bottom side bevels
                mb.Quad(new Vector3(ix, hy, -iz), new Vector3(ix, hy, iz), new Vector3(hx, iy, iz), new Vector3(hx, iy, -iz), new Vector3(1, 1, 0));
                mb.Quad(new Vector3(-hx, iy, -iz), new Vector3(-hx, iy, iz), new Vector3(-ix, hy, iz), new Vector3(-ix, hy, -iz), new Vector3(-1, 1, 0));
                mb.Quad(new Vector3(hx, -iy, -iz), new Vector3(hx, -iy, iz), new Vector3(ix, -hy, iz), new Vector3(ix, -hy, -iz), new Vector3(1, -1, 0));
                mb.Quad(new Vector3(-ix, -hy, -iz), new Vector3(-ix, -hy, iz), new Vector3(-hx, -iy, iz), new Vector3(-hx, -iy, -iz), new Vector3(-1, -1, 0));

                // Y-aligned front/back vertical corner bevels
                mb.Quad(new Vector3(ix, -iy, hz), new Vector3(hx, -iy, iz), new Vector3(hx, iy, iz), new Vector3(ix, iy, hz), new Vector3(1, 0, 1));
                mb.Quad(new Vector3(-hx, -iy, iz), new Vector3(-ix, -iy, hz), new Vector3(-ix, iy, hz), new Vector3(-hx, iy, iz), new Vector3(-1, 0, 1));
                mb.Quad(new Vector3(hx, -iy, -iz), new Vector3(ix, -iy, -hz), new Vector3(ix, iy, -hz), new Vector3(hx, iy, -iz), new Vector3(1, 0, -1));
                mb.Quad(new Vector3(-ix, -iy, -hz), new Vector3(-hx, -iy, -iz), new Vector3(-hx, iy, -iz), new Vector3(-ix, iy, -hz), new Vector3(-1, 0, -1));

                // 8 corner triangles
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 px = new Vector3(sx * hx, sy * iy, sz * iz);
                    Vector3 py = new Vector3(sx * ix, sy * hy, sz * iz);
                    Vector3 pz = new Vector3(sx * ix, sy * iy, sz * hz);
                    mb.Tri(px, py, pz, new Vector3(sx, sy, sz));
                }
            }

            m = mb.ToMesh(key);
            MeshCache[key] = m;
            return m;
        }

        static Mesh GetCylinderMesh()
        {
            const string key = "CYL_16";
            if (MeshCache.TryGetValue(key, out Mesh m) && m != null) return m;

            const int sides = 16;
            const float r = 0.5f;
            const float hh = 1.0f;
            var mb = new MeshBuilder();

            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                float x0 = Mathf.Cos(a0) * r, z0 = Mathf.Sin(a0) * r;
                float x1 = Mathf.Cos(a1) * r, z1 = Mathf.Sin(a1) * r;
                Vector3 sideN = new Vector3(x0 + x1, 0f, z0 + z1);

                mb.Quad(new Vector3(x0, -hh, z0), new Vector3(x0, hh, z0), new Vector3(x1, hh, z1), new Vector3(x1, -hh, z1), sideN);
                mb.Tri(new Vector3(0f, hh, 0f), new Vector3(x1, hh, z1), new Vector3(x0, hh, z0), Vector3.up);
                mb.Tri(new Vector3(0f, -hh, 0f), new Vector3(x0, -hh, z0), new Vector3(x1, -hh, z1), Vector3.down);
            }

            m = mb.ToMesh(key);
            MeshCache[key] = m;
            return m;
        }

        static Mesh BuildExtrudedMesh(Vector2[] profileZY, float widthX)
        {
            float hx = widthX * 0.5f;
            int n = profileZY.Length;
            var mb = new MeshBuilder();

            Vector2 center = Vector2.zero;
            for (int i = 0; i < n; i++) center += profileZY[i];
            center /= n;

            // Side caps (right +X and left -X)
            Vector3 rc = new Vector3(hx, center.y, center.x);
            Vector3 lc = new Vector3(-hx, center.y, center.x);
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = profileZY[i];
                Vector2 p1 = profileZY[(i + 1) % n];

                Vector3 r0 = new Vector3(hx, p0.y, p0.x);
                Vector3 r1 = new Vector3(hx, p1.y, p1.x);
                Vector3 l0 = new Vector3(-hx, p0.y, p0.x);
                Vector3 l1 = new Vector3(-hx, p1.y, p1.x);

                mb.Tri(rc, r0, r1, Vector3.right);
                mb.Tri(lc, l1, l0, Vector3.left);

                Vector2 mid = (p0 + p1) * 0.5f - center;
                Vector3 rimHint = new Vector3(0f, mid.y, mid.x);
                mb.Quad(l0, r0, r1, l1, rimHint);
            }

            return mb.ToMesh("Extruded");
        }

        sealed class MeshBuilder
        {
            readonly List<Vector3> _verts = new List<Vector3>();
            readonly List<Vector3> _norms = new List<Vector3>();
            readonly List<Vector2> _uvs = new List<Vector2>();
            readonly List<int> _tris = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outwardHint)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, outwardHint) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                n.Normalize();
                int i0 = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c);
                _norms.Add(n); _norms.Add(n); _norms.Add(n);
                _uvs.Add(ProjectUv(a, n));
                _uvs.Add(ProjectUv(b, n));
                _uvs.Add(ProjectUv(c, n));
                _tris.Add(i0); _tris.Add(i0 + 1); _tris.Add(i0 + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outwardHint)
            {
                Tri(a, b, c, outwardHint);
                Tri(a, c, d, outwardHint);
            }

            static Vector2 ProjectUv(Vector3 p, Vector3 n)
            {
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                if (ax >= ay && ax >= az) return new Vector2(p.z, p.y) * 4f;
                if (ay >= ax && ay >= az) return new Vector2(p.x, p.z) * 4f;
                return new Vector2(p.x, p.y) * 4f;
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_verts);
                mesh.SetNormals(_norms);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // =====================================================================
        // URP LIT PBR MATERIALS & PROCEDURAL MICRO-SURFACE TEXTURES
        // =====================================================================

        static Material GetMat(FinishType finish, Color tint)
        {
            string key = $"{(int)finish}_{ColorUtility.ToHtmlStringRGBA(tint)}";
            if (MatCache.TryGetValue(key, out Material cached) && cached != null) return cached;

            var mat = new Material(UrpLitShader) { name = "Wep_" + key, enableInstancing = true };
            float metallic = 0.2f;
            float smoothness = 0.45f;
            Texture2D tex = null;
            Color? emission = null;

            switch (finish)
            {
                case FinishType.AnodizedMetal:
                    metallic = 0.56f; smoothness = 0.48f; tex = BrushedTex; break;
                case FinishType.MachinedSteel:
                    metallic = 0.72f; smoothness = 0.56f; tex = BrushedTex; break;
                case FinishType.Chrome:
                    metallic = 0.88f; smoothness = 0.82f; tex = BrushedTex; break;
                case FinishType.MattePolymer:
                    metallic = 0.06f; smoothness = 0.34f; tex = BrushedTex; break;
                case FinishType.KnurledGrip:
                    metallic = 0.04f; smoothness = 0.28f; tex = KnurlTex; break;
                case FinishType.WalnutWood:
                    metallic = 0.00f; smoothness = 0.40f; tex = WoodTex; break;
                case FinishType.Brass:
                    metallic = 0.86f; smoothness = 0.74f; tex = BrushedTex; break;
                case FinishType.Copper:
                    metallic = 0.84f; smoothness = 0.72f; break;
                case FinishType.TintedGlass:
                    metallic = 0.10f; smoothness = 0.92f;
                    emission = new Color(tint.r * 0.12f, tint.g * 0.20f, tint.b * 0.24f);
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                    if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                    break;
                case FinishType.ReticleGlow:
                    metallic = 0f; smoothness = 0.2f; emission = tint * 3.8f; break;
                case FinishType.TritiumDot:
                    metallic = 0f; smoothness = 0.3f; emission = tint * 2.2f; break;
                case FinishType.RecessDark:
                    metallic = 0f; smoothness = 0.1f; break;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            else mat.color = tint;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                else mat.mainTexture = tex;
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }

            MatCache[key] = mat;
            return mat;
        }

        static Color Lift(Color c, float add) =>
            new Color(Mathf.Clamp01(c.r + add), Mathf.Clamp01(c.g + add), Mathf.Clamp01(c.b + add), 1f);

        static Texture2D BrushedTex
        {
            get
            {
                if (_brushedTex != null) return _brushedTex;
                const int s = 64;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
                var rng = new System.Random(101);
                for (int y = 0; y < s; y++)
                {
                    float row = 0.93f + (float)rng.NextDouble() * 0.12f;
                    for (int x = 0; x < s; x++)
                    {
                        float v = Mathf.Clamp01(row + ((float)rng.NextDouble() - 0.5f) * 0.04f);
                        t.SetPixel(x, y, new Color(v, v, v, 1f));
                    }
                }
                t.Apply();
                return _brushedTex = t;
            }
        }

        static Texture2D KnurlTex
        {
            get
            {
                if (_knurlTex != null) return _knurlTex;
                const int s = 64;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int d1 = (x + y) % 6;
                    int d2 = (x - y + s) % 6;
                    float v = (d1 == 0 || d2 == 0) ? 0.72f : 0.96f;
                    t.SetPixel(x, y, new Color(v, v, v, 1f));
                }
                t.Apply();
                return _knurlTex = t;
            }
        }

        static Texture2D WoodTex
        {
            get
            {
                if (_woodTex != null) return _woodTex;
                const int s = 64;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float wave = Mathf.Sin(y * 0.45f + Mathf.Sin(x * 0.22f) * 2.2f);
                    float v = Mathf.Lerp(0.76f, 1.0f, wave * 0.5f + 0.5f);
                    t.SetPixel(x, y, new Color(v, v * 0.92f, v * 0.84f, 1f));
                }
                t.Apply();
                return _woodTex = t;
            }
        }
    }
}
