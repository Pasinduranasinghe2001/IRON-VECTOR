using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// Procedural multi-part 3D Tactical Operator rig with articulated legs/arms, armored plate carrier,
    /// pouches, radio + antenna, combat helmet, dual-tube NVGs, headset mic, goggles, and sculpted face.
    /// Used in both Player.prefab (with walk/crouch animation & team uniform tinting) and the Lobby Showcase.
    /// </summary>
    public static class SoldierVisualBuilder
    {
        public sealed class SoldierRigRefs
        {
            public Transform TorsoPivot;
            public Transform LeftLegPivot;
            public Transform RightLegPivot;
            public Transform HeadRoot;
            public Transform WeaponMount;
            public Renderer[] TeamUniformRenderers;
            public Renderer[] AllBodyRenderers;
        }

        public delegate Material MaterialFactory(string name, Color col, float smoothness, float metallic, Texture2D tex, Color? emission);

        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();
        static Texture2D _camoTex;

        public static SoldierRigRefs Build(
            Transform playerRoot,
            Transform existingHeadParent,
            bool lobbyPose,
            Color uniformBase,
            Color teamTrim,
            MaterialFactory matFactory = null)
        {
            Shader lit = (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultShader : null)
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            Material GetM(string key, Color col, float sm, float met, Texture2D tex = null, Color? em = null) =>
                matFactory != null ? matFactory(key, col, sm, met, tex, em) : MakeMat(key, col, sm, met, lit, tex, em);

            Material matUniform = GetM("Uniform", uniformBase, 0.20f, 0.04f, matFactory == null ? CamoTex : null);
            Material matTrim = GetM("Trim", teamTrim, 0.35f, 0.12f);
            Material matVest = GetM("Vest", new Color(0.16f, 0.18f, 0.17f), 0.28f, 0.14f);
            Material matLeather = GetM("BootGlove", new Color(0.11f, 0.11f, 0.12f), 0.34f, 0.08f);
            Material matSteel = GetM("GearSteel", new Color(0.28f, 0.31f, 0.35f), 0.56f, 0.65f);
            Material matSkin = GetM("Skin", new Color(0.76f, 0.60f, 0.49f), 0.26f, 0.0f);
            Material matHair = GetM("HairBeard", new Color(0.15f, 0.13f, 0.11f), 0.18f, 0.0f);
            Material matHelmet = GetM("Helmet", new Color(0.18f, 0.21f, 0.19f), 0.36f, 0.24f);
            Material matGoggles = GetM("Goggles", new Color(0.08f, 0.22f, 0.30f), 0.90f, 0.45f);
            Material matNvgGlow = GetM("NvgLens", new Color(0.18f, 0.72f, 0.58f), 0.85f, 0.2f, null, new Color(0.15f, 0.65f, 0.48f));

            var bodyRenderers = new List<Renderer>();
            var trimRenderers = new List<Renderer>();

            // Articulated Hip / Torso Root
            var bodyRoot = new GameObject("SoldierRig");
            bodyRoot.transform.SetParent(playerRoot, false);

            // Legs with hip pivots at Y = 0.86m so walking/running animates cleanly
            var legLPivot = new GameObject("LegPivot_L");
            legLPivot.transform.SetParent(bodyRoot.transform, false);
            legLPivot.transform.localPosition = new Vector3(-0.13f, 0.86f, 0f);
            BuildLeg(legLPivot.transform, -1, matUniform, matVest, matLeather, matSteel, bodyRenderers, trimRenderers);

            var legRPivot = new GameObject("LegPivot_R");
            legRPivot.transform.SetParent(bodyRoot.transform, false);
            legRPivot.transform.localPosition = new Vector3(0.13f, 0.86f, 0f);
            BuildLeg(legRPivot.transform, 1, matUniform, matVest, matLeather, matSteel, bodyRenderers, trimRenderers);

            // Torso Assembly (Pelvis + Armored Plate Carrier + Chest Pouches + Radio + Backpack + Arms)
            var torso = new GameObject("TorsoPivot");
            torso.transform.SetParent(bodyRoot.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.86f, 0f);

            AddPart(torso.transform, "Pelvis", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.36f, 0.15f, 0.23f), matUniform, Vector3.zero, bodyRenderers, trimRenderers);
            AddPart(torso.transform, "BattleBelt", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0f), new Vector3(0.39f, 0.07f, 0.26f), matLeather, Vector3.zero, bodyRenderers);
            AddPart(torso.transform, "BeltBuckle", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0.135f), new Vector3(0.06f, 0.055f, 0.02f), matSteel, Vector3.zero, bodyRenderers);
            AddPart(torso.transform, "DropHolster", PrimitiveType.Cube, new Vector3(0.21f, -0.14f, 0.02f), new Vector3(0.06f, 0.18f, 0.12f), matVest, Vector3.zero, bodyRenderers);

            // Combat Shirt & Kevlar Plate Carrier
            AddPart(torso.transform, "CombatShirt", PrimitiveType.Cube, new Vector3(0f, 0.30f, 0f), new Vector3(0.38f, 0.44f, 0.23f), matUniform, Vector3.zero, bodyRenderers, trimRenderers);
            AddPart(torso.transform, "PlateCarrier", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0.01f), new Vector3(0.35f, 0.36f, 0.27f), matVest, Vector3.zero, bodyRenderers);
            AddPart(torso.transform, "ChestTeamPatch", PrimitiveType.Cube, new Vector3(0f, 0.41f, 0.148f), new Vector3(0.14f, 0.06f, 0.015f), matTrim, Vector3.zero, bodyRenderers, trimRenderers);

            // Triple Magazine Chest Rig + Admin Pouch + Radio + Antenna + Assault Pack
            for (int m = -1; m <= 1; m++)
                AddPart(torso.transform, "ChestPouch_" + (m + 1), PrimitiveType.Cube, new Vector3(m * 0.088f, 0.22f, 0.155f), new Vector3(0.074f, 0.125f, 0.055f), matVest, Vector3.zero, bodyRenderers);
            AddPart(torso.transform, "RadioBody", PrimitiveType.Cube, new Vector3(-0.15f, 0.35f, 0.14f), new Vector3(0.055f, 0.11f, 0.04f), matLeather, Vector3.zero, bodyRenderers);
            AddPart(torso.transform, "RadioAntenna", PrimitiveType.Cylinder, new Vector3(-0.16f, 0.50f, 0.135f), new Vector3(0.009f, 0.12f, 0.009f), matSteel, Vector3.zero, bodyRenderers);
            AddPart(torso.transform, "AssaultPack", PrimitiveType.Cube, new Vector3(0f, 0.33f, -0.175f), new Vector3(0.28f, 0.34f, 0.14f), matVest, Vector3.zero, bodyRenderers);

            // Collars & Shoulder Pauldrons w/ Team Armband Stripes
            AddPart(torso.transform, "CollarNeck", PrimitiveType.Cylinder, new Vector3(0f, 0.56f, 0f), new Vector3(0.12f, 0.07f, 0.12f), matSkin, Vector3.zero, bodyRenderers);
            AddPart(torso.transform, "ShoulderPad_L", PrimitiveType.Cube, new Vector3(-0.22f, 0.45f, 0f), new Vector3(0.10f, 0.14f, 0.18f), matUniform, new Vector3(0f, 0f, -12f), bodyRenderers, trimRenderers);
            AddPart(torso.transform, "ShoulderPad_R", PrimitiveType.Cube, new Vector3(0.22f, 0.45f, 0f), new Vector3(0.10f, 0.14f, 0.18f), matUniform, new Vector3(0f, 0f, 12f), bodyRenderers, trimRenderers);
            AddPart(torso.transform, "TeamArmband_L", PrimitiveType.Cube, new Vector3(-0.235f, 0.41f, 0f), new Vector3(0.105f, 0.045f, 0.185f), matTrim, Vector3.zero, bodyRenderers, trimRenderers);
            AddPart(torso.transform, "TeamArmband_R", PrimitiveType.Cube, new Vector3(0.235f, 0.41f, 0f), new Vector3(0.105f, 0.045f, 0.185f), matTrim, Vector3.zero, bodyRenderers, trimRenderers);

            // Articulated Combat Arms + Armored Gloves
            if (lobbyPose)
            {
                AddPart(torso.transform, "UpperArm_R", PrimitiveType.Capsule, new Vector3(0.24f, 0.36f, 0.05f), new Vector3(0.11f, 0.18f, 0.11f), matUniform, new Vector3(28f, -16f, 14f), bodyRenderers, trimRenderers);
                AddPart(torso.transform, "Forearm_R", PrimitiveType.Capsule, new Vector3(0.18f, 0.22f, 0.19f), new Vector3(0.095f, 0.16f, 0.095f), matUniform, new Vector3(72f, -35f, 0f), bodyRenderers, trimRenderers);
                AddPart(torso.transform, "Glove_R", PrimitiveType.Cube, new Vector3(0.10f, 0.23f, 0.27f), new Vector3(0.08f, 0.07f, 0.09f), matLeather, Vector3.zero, bodyRenderers);

                AddPart(torso.transform, "UpperArm_L", PrimitiveType.Capsule, new Vector3(-0.23f, 0.35f, 0.08f), new Vector3(0.11f, 0.18f, 0.11f), matUniform, new Vector3(38f, 22f, -14f), bodyRenderers, trimRenderers);
                AddPart(torso.transform, "Forearm_L", PrimitiveType.Capsule, new Vector3(-0.12f, 0.26f, 0.22f), new Vector3(0.095f, 0.16f, 0.095f), matUniform, new Vector3(68f, 44f, 0f), bodyRenderers, trimRenderers);
                AddPart(torso.transform, "Glove_L", PrimitiveType.Cube, new Vector3(-0.04f, 0.28f, 0.28f), new Vector3(0.08f, 0.07f, 0.09f), matLeather, Vector3.zero, bodyRenderers);
            }
            else
            {
                AddPart(torso.transform, "Arm_R", PrimitiveType.Capsule, new Vector3(0.24f, 0.28f, 0.10f), new Vector3(0.11f, 0.24f, 0.11f), matUniform, new Vector3(58f, -12f, 8f), bodyRenderers, trimRenderers);
                AddPart(torso.transform, "Arm_L", PrimitiveType.Capsule, new Vector3(-0.22f, 0.28f, 0.13f), new Vector3(0.11f, 0.24f, 0.11f), matUniform, new Vector3(64f, 22f, -8f), bodyRenderers, trimRenderers);
                AddPart(torso.transform, "Glove_R", PrimitiveType.Cube, new Vector3(0.19f, 0.27f, 0.26f), new Vector3(0.08f, 0.07f, 0.09f), matLeather, Vector3.zero, bodyRenderers);
                AddPart(torso.transform, "Glove_L", PrimitiveType.Cube, new Vector3(-0.07f, 0.28f, 0.29f), new Vector3(0.08f, 0.07f, 0.09f), matLeather, Vector3.zero, bodyRenderers);
            }

            // Head & Tactical MICH Helmet + Dual NVGs + Headset + Mic + Beard/Face Sculpt
            Transform headParent = existingHeadParent;
            if (headParent == null)
            {
                var hg = new GameObject("Head");
                hg.transform.SetParent(playerRoot, false);
                hg.transform.localPosition = new Vector3(0f, 1.62f, 0f);
                headParent = hg.transform;
            }
            BuildHeadAndHelmet(headParent, matSkin, matHair, matHelmet, matGoggles, matNvgGlow, matSteel, matTrim, bodyRenderers, trimRenderers);

            // Weapon Mount for Lobby Operator display
            Transform weaponMount = null;
            if (lobbyPose)
            {
                var wm = new GameObject("WeaponDisplayMount");
                wm.transform.SetParent(torso.transform, false);
                wm.transform.localPosition = new Vector3(0.03f, 0.27f, 0.27f);
                wm.transform.localRotation = Quaternion.Euler(26f, -52f, -12f);
                wm.transform.localScale = Vector3.one * 1.12f;
                weaponMount = wm.transform;
            }

            return new SoldierRigRefs
            {
                TorsoPivot = torso.transform,
                LeftLegPivot = legLPivot.transform,
                RightLegPivot = legRPivot.transform,
                HeadRoot = headParent,
                WeaponMount = weaponMount,
                TeamUniformRenderers = trimRenderers.ToArray(),
                AllBodyRenderers = bodyRenderers.ToArray()
            };
        }

        static void BuildLeg(Transform pivot, int sideSign, Material uniform, Material vest, Material boot, Material steel, List<Renderer> all, List<Renderer> trim)
        {
            // Hip-centered offsets down to foot sole at Y = -0.86
            AddPart(pivot, "Thigh", PrimitiveType.Capsule, new Vector3(0f, -0.22f, 0f), new Vector3(0.16f, 0.24f, 0.17f), uniform, Vector3.zero, all, trim);
            AddPart(pivot, "CargoPocket", PrimitiveType.Cube, new Vector3(sideSign * 0.07f, -0.24f, 0.01f), new Vector3(0.05f, 0.14f, 0.12f), uniform, Vector3.zero, all, trim);
            AddPart(pivot, "KneePad", PrimitiveType.Cube, new Vector3(0f, -0.44f, 0.075f), new Vector3(0.12f, 0.13f, 0.055f), vest, Vector3.zero, all);
            AddPart(pivot, "Shin", PrimitiveType.Capsule, new Vector3(0f, -0.60f, -0.01f), new Vector3(0.135f, 0.20f, 0.145f), uniform, Vector3.zero, all, trim);
            AddPart(pivot, "BootAnkle", PrimitiveType.Cylinder, new Vector3(0f, -0.74f, 0f), new Vector3(0.12f, 0.07f, 0.13f), boot, Vector3.zero, all);
            AddPart(pivot, "BootSole", PrimitiveType.Cube, new Vector3(0f, -0.81f, 0.045f), new Vector3(0.125f, 0.10f, 0.26f), boot, Vector3.zero, all);
            AddPart(pivot, "BootToeCap", PrimitiveType.Cube, new Vector3(0f, -0.80f, 0.155f), new Vector3(0.115f, 0.07f, 0.05f), steel, Vector3.zero, all);
        }

        static void BuildHeadAndHelmet(
            Transform headParent, Material skin, Material hair, Material helmet, Material goggles, Material nvgGlow, Material steel, Material trim,
            List<Renderer> all, List<Renderer> trimList)
        {
            AddPart(headParent, "Cranium", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.25f, 0.24f), skin, Vector3.zero, all);
            AddPart(headParent, "JawBeard", PrimitiveType.Cube, new Vector3(0f, -0.07f, 0.045f), new Vector3(0.15f, 0.09f, 0.13f), hair, Vector3.zero, all);
            AddPart(headParent, "Nose", PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.12f), new Vector3(0.032f, 0.048f, 0.035f), skin, new Vector3(-15f, 0f, 0f), all);
            AddPart(headParent, "Mustache", PrimitiveType.Cube, new Vector3(0f, -0.040f, 0.112f), new Vector3(0.085f, 0.022f, 0.025f), hair, Vector3.zero, all);

            // Tactical Helmet Shell, Rim, Rails & Team IFF Patch
            AddPart(headParent, "HelmetDome", PrimitiveType.Sphere, new Vector3(0f, 0.05f, -0.01f), new Vector3(0.265f, 0.22f, 0.275f), helmet, Vector3.zero, all);
            AddPart(headParent, "HelmetRim", PrimitiveType.Cube, new Vector3(0f, 0.02f, -0.02f), new Vector3(0.26f, 0.07f, 0.27f), helmet, Vector3.zero, all);
            AddPart(headParent, "HelmetTeamStrip", PrimitiveType.Cube, new Vector3(0f, 0.15f, -0.02f), new Vector3(0.10f, 0.018f, 0.10f), trim, Vector3.zero, all, trimList);

            // Dual-Tube NVG Goggles + Mount + Polycarbonate Combat Shades + Comms Headset & Boom Mic
            AddPart(headParent, "NvgMount", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0.145f), new Vector3(0.06f, 0.055f, 0.045f), steel, Vector3.zero, all);
            AddPart(headParent, "NvgTubeL", PrimitiveType.Cylinder, new Vector3(-0.042f, 0.125f, 0.175f), new Vector3(0.038f, 0.038f, 0.038f), helmet, new Vector3(68f, 0f, 0f), all);
            AddPart(headParent, "NvgTubeR", PrimitiveType.Cylinder, new Vector3(0.042f, 0.125f, 0.175f), new Vector3(0.038f, 0.038f, 0.038f), helmet, new Vector3(68f, 0f, 0f), all);
            AddPart(headParent, "NvgLensL", PrimitiveType.Cylinder, new Vector3(-0.042f, 0.132f, 0.208f), new Vector3(0.028f, 0.008f, 0.028f), nvgGlow, new Vector3(68f, 0f, 0f), all);
            AddPart(headParent, "NvgLensR", PrimitiveType.Cylinder, new Vector3(0.042f, 0.132f, 0.208f), new Vector3(0.028f, 0.008f, 0.028f), nvgGlow, new Vector3(68f, 0f, 0f), all);

            AddPart(headParent, "CombatVisor", PrimitiveType.Cube, new Vector3(0f, 0.022f, 0.115f), new Vector3(0.185f, 0.054f, 0.045f), goggles, Vector3.zero, all);
            AddPart(headParent, "HeadsetL", PrimitiveType.Cylinder, new Vector3(-0.132f, 0.0f, -0.01f), new Vector3(0.075f, 0.026f, 0.085f), steel, new Vector3(0f, 0f, 90f), all);
            AddPart(headParent, "HeadsetR", PrimitiveType.Cylinder, new Vector3(0.132f, 0.0f, -0.01f), new Vector3(0.075f, 0.026f, 0.085f), steel, new Vector3(0f, 0f, 90f), all);
            AddPart(headParent, "MicBoom", PrimitiveType.Cube, new Vector3(-0.07f, -0.052f, 0.08f), new Vector3(0.11f, 0.012f, 0.012f), steel, new Vector3(0f, -40f, 0f), all);
        }

        static GameObject AddPart(
            Transform parent, string partName, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat,
            Vector3 euler, List<Renderer> allRenderers, List<Renderer> trimRenderers = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            allRenderers?.Add(r);
            trimRenderers?.Add(r);
            return go;
        }

        static Material MakeMat(string key, Color col, float smoothness, float metallic, Shader sh, Texture2D tex, Color? emission = null)
        {
            string fullKey = $"{key}_{ColorUtility.ToHtmlStringRGBA(col)}";
            if (MatCache.TryGetValue(fullKey, out Material m) && m != null) return m;
            m = new Material(sh) { name = "Soldier_" + key, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            else m.color = col;
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                else m.mainTexture = tex;
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            MatCache[fullKey] = m;
            return m;
        }

        static Texture2D CamoTex
        {
            get
            {
                if (_camoTex != null) return _camoTex;
                const int s = 64;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.14f, y * 0.14f);
                    float n2 = Mathf.PerlinNoise(x * 0.32f + 17f, y * 0.32f + 9f);
                    float v = n > 0.62f ? 0.68f : (n2 > 0.56f ? 0.86f : 1.0f);
                    t.SetPixel(x, y, new Color(v, v, v, 1f));
                }
                t.Apply();
                return _camoTex = t;
            }
        }
    }
}
