using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IronVector
{
    /// <summary>
    /// Shared procedural 3D environment foliage (tropical shade trees, Sri Lankan coconut palms,
    /// flowering campus planters, flagpoles, lamp posts) and crisp skybox/lighting enhancer.
    /// Used both at bake time in IronVectorProjectSetup and as a safety net at runtime in MapConfig.
    /// </summary>
    public static class WorldPropsBuilder
    {
        static readonly Dictionary<string, Material> RuntimeMats = new Dictionary<string, Material>();

        public static void EnhanceLoadedMap(MapConfig cfg)
        {
            if (cfg == null || Application.isBatchMode) return;

            ApplyClearTropicalSkyAndExposure();

            if (GameObject.Find("OutsideFoliage") == null)
            {
                bool isAdmin = cfg.gameObject.scene.name == "AdminHall" || cfg.mapDisplayName.Contains("Admin");
                var holder = new GameObject("OutsideFoliage");
                if (isAdmin) PopulateAdminHallExterior(holder.transform, RuntimeMat);
                else PopulateArenaExterior(holder.transform, RuntimeMat);
            }
        }

        public static void ApplyClearTropicalSkyAndExposure()
        {
            Shader skySh = Shader.Find("Skybox/Procedural");
            if (skySh != null && (RenderSettings.skybox == null || RenderSettings.skybox.shader != skySh))
            {
                var skyMat = new Material(skySh) { name = "IronVectorTropicalSky" };
                if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", new Color(0.26f, 0.56f, 0.94f));
                if (skyMat.HasProperty("_GroundColor")) skyMat.SetColor("_GroundColor", new Color(0.32f, 0.36f, 0.28f));
                if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", 0.92f);
                if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", 1.25f);
                RenderSettings.skybox = skyMat;
            }
            RenderSettings.ambientIntensity = 1.25f;
            DynamicGI.UpdateEnvironment();
            GraphicsSettingsManager.Apply();
        }

        public delegate Material MatFactory(string name, Color col, float smoothness, float metallic, Color? emission = null);

        public static void PopulateAdminHallExterior(Transform parent, MatFactory makeMat)
        {
            Material matBark = makeMat("Mat_TreeBark", new Color(0.34f, 0.24f, 0.16f), 0.10f, 0f);
            Material matLeafDark = makeMat("Mat_TreeLeafDark", new Color(0.14f, 0.42f, 0.16f), 0.16f, 0f);
            Material matLeafLight = makeMat("Mat_TreeLeafLight", new Color(0.24f, 0.58f, 0.20f), 0.20f, 0f);
            Material matPalmFrond = makeMat("Mat_PalmFrond", new Color(0.18f, 0.50f, 0.18f), 0.22f, 0f);
            Material matCoconut = makeMat("Mat_Coconut", new Color(0.36f, 0.24f, 0.11f), 0.25f, 0f);
            Material matFlowerPink = makeMat("Mat_FlowerBougain", new Color(0.86f, 0.18f, 0.48f), 0.15f, 0f);
            Material matFlowerGold = makeMat("Mat_FlowerMarigold", new Color(0.95f, 0.72f, 0.12f), 0.15f, 0f);
            Material matPoleSteel = makeMat("Mat_Steel", new Color(0.36f, 0.40f, 0.46f), 0.60f, 0.65f);
            Material matFlagRed = makeMat("Mat_FlagCrimson", new Color(0.76f, 0.14f, 0.16f), 0.18f, 0f);
            Material matFlagGold = makeMat("Mat_FlagGold", new Color(0.96f, 0.76f, 0.18f), 0.45f, 0.60f);
            Material matLampGlow = makeMat("Mat_CeilingLight", Color.white, 0.90f, 0f, new Color(1.7f, 1.6f, 1.35f));

            const float lawnY = -0.15f;

            // Towering Sri Lankan Coconut Palms lining South Entrance Avenue & Perimeter Lawns
            Vector3[] palmSpots =
            {
                new Vector3(-16f, lawnY, -46f), new Vector3(-28f, lawnY, -47f), new Vector3(-42f, lawnY, -47f),
                new Vector3(16f, lawnY, -46f),  new Vector3(28f, lawnY, -47f),  new Vector3(42f, lawnY, -47f),
                new Vector3(-53f, lawnY, -34f), new Vector3(-53f, lawnY, -14f), new Vector3(-53f, lawnY, 12f),
                new Vector3(53f, lawnY, -34f),  new Vector3(53f, lawnY, -14f),  new Vector3(53f, lawnY, 12f),
                new Vector3(-34f, lawnY, 36f),  new Vector3(0f, lawnY, 38f),    new Vector3(34f, lawnY, 36f)
            };
            for (int i = 0; i < palmSpots.Length; i++)
                BuildPalmTree(parent, $"Palm_{i}", palmSpots[i], 8.2f + (i % 3) * 1.4f, matBark, matPalmFrond, matCoconut, i * 19 + 7);

            // Broad Tropical Canopy Shade Trees (Neem / Rain Tree silhouettes around Campus Lawns)
            Vector3[] shadeSpots =
            {
                new Vector3(-56f, lawnY, -44f), new Vector3(-56f, lawnY, -24f), new Vector3(-56f, lawnY, 0f),
                new Vector3(-56f, lawnY, 26f),  new Vector3(-46f, lawnY, 40f),  new Vector3(-18f, lawnY, 39f),
                new Vector3(18f, lawnY, 39f),   new Vector3(46f, lawnY, 40f),   new Vector3(56f, lawnY, 26f),
                new Vector3(56f, lawnY, 0f),    new Vector3(56f, lawnY, -24f),  new Vector3(56f, lawnY, -44f)
            };
            for (int i = 0; i < shadeSpots.Length; i++)
                BuildTropicalTree(parent, $"ShadeTree_{i}", shadeSpots[i], 6.4f + (i % 3) * 1.1f, matBark, matLeafDark, matLeafLight, i * 31 + 3);

            // Ornamental Planter Shrubs & Tropical Flowers on South Courtyard Planters (top Y = 1.3m)
            BuildShrubCluster(parent, "PlanterShrub_W", new Vector3(-22f, 1.3f, -34f), matLeafDark, matFlowerPink);
            BuildShrubCluster(parent, "PlanterShrub_E", new Vector3(22f, 1.3f, -34f), matLeafLight, matFlowerGold);
            BuildShrubCluster(parent, "MonumentGarden_S", new Vector3(0f, 1.4f, -37f), matLeafDark, matFlowerPink);

            // Dual University Faculty Flagpoles flanking Grand Portico & Streetlamps along Driveway
            BuildFlagpole(parent, "Flagpole_West", new Vector3(-13.5f, 0f, -31.5f), matPoleSteel, matFlagRed, matFlagGold);
            BuildFlagpole(parent, "Flagpole_East", new Vector3(13.5f, 0f, -31.5f), matPoleSteel, matFlagGold, matFlagGold);

            float[] lampX = { -38f, -18f, 18f, 38f };
            for (int i = 0; i < lampX.Length; i++)
                BuildCampusLamp(parent, $"DrivewayLamp_{i}", new Vector3(lampX[i], 0f, -23.6f), matPoleSteel, matLampGlow);
        }

        public static void PopulateArenaExterior(Transform parent, MatFactory makeMat)
        {
            Material matBark = makeMat("Mat_TreeBark", new Color(0.36f, 0.25f, 0.16f), 0.10f, 0f);
            Material matLeaf = makeMat("Mat_TreeLeafDark", new Color(0.20f, 0.44f, 0.18f), 0.16f, 0f);
            Material matLeafHi = makeMat("Mat_TreeLeafLight", new Color(0.30f, 0.54f, 0.22f), 0.20f, 0f);
            Material matPalmFrond = makeMat("Mat_PalmFrond", new Color(0.22f, 0.48f, 0.20f), 0.22f, 0f);
            Material matCoconut = makeMat("Mat_Coconut", new Color(0.36f, 0.24f, 0.11f), 0.25f, 0f);

            Vector3[] desertPalms =
            {
                new Vector3(-56f, 0f, 40f), new Vector3(-28f, 0f, 43f), new Vector3(28f, 0f, 43f), new Vector3(56f, 0f, 40f),
                new Vector3(-56f, 0f, -40f), new Vector3(-24f, 0f, -43f), new Vector3(24f, 0f, -43f), new Vector3(56f, 0f, -40f)
            };
            for (int i = 0; i < desertPalms.Length; i++)
                BuildPalmTree(parent, $"OutpostPalm_{i}", desertPalms[i], 7.4f + (i % 2) * 1.4f, matBark, matPalmFrond, matCoconut, i * 13);

            Vector3[] scrubTrees =
            {
                new Vector3(-44f, 0f, 41f), new Vector3(0f, 0f, 43f), new Vector3(44f, 0f, 41f),
                new Vector3(-44f, 0f, -42f), new Vector3(44f, 0f, -42f)
            };
            for (int i = 0; i < scrubTrees.Length; i++)
                BuildTropicalTree(parent, $"ScrubTree_{i}", scrubTrees[i], 5.0f, matBark, matLeaf, matLeafHi, i * 29);
        }

        static void BuildTropicalTree(Transform parent, string name, Vector3 basePos, float height, Material bark, Material leafDark, Material leafLight, int seed)
        {
            var tree = new GameObject(name);
            tree.transform.SetParent(parent, false);
            tree.transform.position = basePos;
            float yaw = (seed * 53) % 360;
            tree.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            float trunkH = height * 0.46f;
            float r = height * 0.055f;
            var trunk = Part(tree.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, trunkH * 0.5f, 0f), new Vector3(r * 2f, trunkH * 0.5f, r * 2f), bark, collider: true);
            _ = trunk;

            // Angled Main Branches
            for (int b = 0; b < 3; b++)
            {
                float ba = b * 120f + 20f;
                Vector3 dir = Quaternion.Euler(0f, ba, 0f) * new Vector3(0f, 0.7f, 0.7f);
                Part(tree.transform, "Branch_" + b, PrimitiveType.Cylinder,
                    new Vector3(0f, trunkH * 0.85f, 0f) + dir * (height * 0.16f),
                    new Vector3(r * 1.1f, height * 0.18f, r * 1.1f),
                    bark, euler: new Vector3(36f, ba, 0f), collider: false);
            }

            // Rich Multi-Sphere Foliage Crown (Deep emerald base + upper sunlit leaf puffs)
            float crownY = height * 0.66f;
            Part(tree.transform, "CanopyCore", PrimitiveType.Sphere, new Vector3(0f, crownY, 0f), new Vector3(height * 0.62f, height * 0.42f, height * 0.62f), leafDark, collider: false);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                float dist = height * (0.20f + (i % 2) * 0.08f);
                float puffScale = height * (0.34f + ((seed + i) % 3) * 0.05f);
                Part(tree.transform, "CanopyLobe_" + i, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(ang) * dist, crownY + (i % 2 == 0 ? 0.35f : -0.15f), Mathf.Sin(ang) * dist),
                    new Vector3(puffScale, puffScale * 0.72f, puffScale),
                    (i % 2 == 0 ? leafLight : leafDark), collider: false);
            }
            Part(tree.transform, "CanopyTop", PrimitiveType.Sphere, new Vector3(0f, crownY + height * 0.22f, 0f),
                new Vector3(height * 0.44f, height * 0.30f, height * 0.44f), leafLight, collider: false);
        }

        static void BuildPalmTree(Transform parent, string name, Vector3 basePos, float height, Material bark, Material frond, Material coconut, int seed)
        {
            var palm = new GameObject(name);
            palm.transform.SetParent(parent, false);
            palm.transform.position = basePos;
            palm.transform.rotation = Quaternion.Euler(0f, (seed * 41) % 360, 0f);

            const int segs = 5;
            float segH = height / segs;
            Vector3 cur = Vector3.zero;
            float lean = 0.045f + (seed % 3) * 0.015f;
            for (int s = 0; s < segs; s++)
            {
                float tilt = s * 3.8f;
                float radius = Mathf.Lerp(0.24f, 0.13f, s / (float)segs);
                Vector3 next = cur + Quaternion.Euler(tilt, 0f, 0f) * (Vector3.up * segH);
                Vector3 mid = (cur + next) * 0.5f;
                Part(palm.transform, "TrunkSeg_" + s, PrimitiveType.Cylinder, mid,
                    new Vector3(radius * 2f, segH * 0.55f, radius * 2f), bark, new Vector3(tilt, 0f, 0f), collider: s < 2);
                cur = next + new Vector3(0f, 0f, lean);
            }

            // Coconuts tucked under the frond crown
            for (int c = 0; c < 4; c++)
            {
                float a = c * 90f * Mathf.Deg2Rad;
                Part(palm.transform, "Coconut_" + c, PrimitiveType.Sphere,
                    cur + new Vector3(Mathf.Cos(a) * 0.22f, -0.22f, Mathf.Sin(a) * 0.22f),
                    Vector3.one * 0.26f, coconut, collider: false);
            }

            // Arched Tropical Coconut Palm Fronds (2 tiers = 10 lush arching leaves)
            for (int f = 0; f < 10; f++)
            {
                float yaw = f * 36f + (f % 2) * 14f;
                float droop = f % 2 == 0 ? 18f : 34f;
                float frondLen = height * (f % 2 == 0 ? 0.36f : 0.42f);
                var pivot = new GameObject("FrondPivot_" + f);
                pivot.transform.SetParent(palm.transform, false);
                pivot.transform.localPosition = cur;
                pivot.transform.localRotation = Quaternion.Euler(droop, yaw, 0f);

                Part(pivot.transform, "LeafBase", PrimitiveType.Cube, new Vector3(0f, 0f, frondLen * 0.48f),
                    new Vector3(0.56f, 0.06f, frondLen), frond, new Vector3(8f, 0f, 0f), collider: false);
                Part(pivot.transform, "LeafTip", PrimitiveType.Cube, new Vector3(0f, -0.24f, frondLen * 1.05f),
                    new Vector3(0.32f, 0.04f, frondLen * 0.48f), frond, new Vector3(28f, 0f, 0f), collider: false);
            }
        }

        static void BuildShrubCluster(Transform parent, string name, Vector3 pos, Material leaf, Material flower)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            for (int i = -1; i <= 1; i++)
            {
                Part(g.transform, "Bush_" + (i + 1), PrimitiveType.Sphere, new Vector3(i * 1.55f, 0.38f, 0f), new Vector3(1.5f, 0.95f, 1.4f), leaf, collider: false);
                Part(g.transform, "BloomA_" + (i + 1), PrimitiveType.Sphere, new Vector3(i * 1.55f - 0.3f, 0.65f, 0.35f), Vector3.one * 0.38f, flower, collider: false);
                Part(g.transform, "BloomB_" + (i + 1), PrimitiveType.Sphere, new Vector3(i * 1.55f + 0.35f, 0.72f, -0.25f), Vector3.one * 0.34f, flower, collider: false);
            }
        }

        static void BuildFlagpole(Transform parent, string name, Vector3 pos, Material pole, Material flagCloth, Material gold)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            Part(g.transform, "Plinth", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.8f, 0.25f, 0.8f), pole, collider: true);
            Part(g.transform, "Mast", PrimitiveType.Cylinder, new Vector3(0f, 4.8f, 0f), new Vector3(0.14f, 4.6f, 0.14f), pole, collider: false);
            Part(g.transform, "Finial", PrimitiveType.Sphere, new Vector3(0f, 9.5f, 0f), Vector3.one * 0.28f, gold, collider: false);
            Part(g.transform, "FlagBanner", PrimitiveType.Cube, new Vector3(1.1f, 8.3f, 0f), new Vector3(2.2f, 1.3f, 0.06f), flagCloth, collider: false);
            Part(g.transform, "FlagBorder", PrimitiveType.Cube, new Vector3(1.1f, 8.3f, 0.04f), new Vector3(2.22f, 0.20f, 0.02f), gold, collider: false);
        }

        static void BuildCampusLamp(Transform parent, string name, Vector3 pos, Material pole, Material glow)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            Part(g.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 2.3f, 0f), new Vector3(0.16f, 2.3f, 0.16f), pole, collider: true);
            Part(g.transform, "Arm", PrimitiveType.Cube, new Vector3(0f, 4.55f, -0.45f), new Vector3(0.14f, 0.12f, 1.1f), pole, collider: false);
            Part(g.transform, "LampHead", PrimitiveType.Cube, new Vector3(0f, 4.44f, -0.88f), new Vector3(0.45f, 0.12f, 0.55f), glow, collider: false);
        }

        static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!collider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Object.Destroy(col);
                    else Object.DestroyImmediate(col);
                }
            }
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Material RuntimeMat(string name, Color col, float smoothness, float metallic, Color? emission = null)
        {
            string key = $"{name}_{ColorUtility.ToHtmlStringRGBA(col)}";
            if (RuntimeMats.TryGetValue(key, out Material cached) && cached != null) return cached;
            Shader sh = (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultShader : null)
                        ?? Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
            var m = new Material(sh) { name = name, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            else m.color = col;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            RuntimeMats[key] = m;
            return m;
        }
    }
}
