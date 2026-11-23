using System.Collections.Generic;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Shared Call of Duty 4–inspired tactical military glass UI theme, scanline/carbon backing textures,
    /// corner targeting brackets, compass radar & circular Mil-Dot sniper scope masks, and vivid map thumbs.
    /// </summary>
    public static class TacticalGuiTheme
    {
        public struct ChallengeInfo
        {
            public string Title;
            public int Current;
            public int Target;
            public int Xp;
            public bool Done => Current >= Target;
        }

        public static readonly ChallengeInfo[] Challenges =
        {
            new ChallengeInfo { Title = "Eliminate 15 hostiles in AdminHall (Kilinochchi)", Current = 15, Target = 15, Xp = 2500 },
            new ChallengeInfo { Title = "Win 3 rounds on Search & Destroy", Current = 2, Target = 3, Xp = 2500 },
            new ChallengeInfo { Title = "Score 5 headshots with M40A3 / Carbine", Current = 3, Target = 5, Xp = 5000 },
            new ChallengeInfo { Title = "Complete 3 daily tactical objectives", Current = 1, Target = 3, Xp = 7500 }
        };

        public static readonly string[] CrosshairNames = { "COD4 Green", "Crisp White", "Tactical Cyan", "Amber Gold" };

        public static readonly Color AccentAmber = new Color(0.96f, 0.70f, 0.20f, 1f);
        public static readonly Color AccentCodGreen = new Color(0.36f, 0.92f, 0.44f, 1f);
        public static readonly Color AccentGreen = AccentCodGreen;
        public static readonly Color AccentCyan = new Color(0.30f, 0.84f, 1.00f, 1f);
        public static readonly Color GlassDark = new Color(0.06f, 0.08f, 0.11f, 0.84f);
        public static readonly Color GlassCard = new Color(0.09f, 0.13f, 0.16f, 0.88f);
        public static readonly Color BorderSubtle = new Color(0.75f, 0.84f, 0.90f, 0.22f);

        static Texture2D _whiteTex, _scanlineTex, _topGradTex, _botGradTex;
        static Texture2D _adminThumb, _arenaThumb, _radarRingTex, _scopeMaskTex;
        static readonly Dictionary<string, Texture2D> SolidCache = new Dictionary<string, Texture2D>();

        public static GUIStyle TitleStyle { get; private set; }
        public static GUIStyle HeaderStyle { get; private set; }
        public static GUIStyle SubGoldStyle { get; private set; }
        public static GUIStyle BodyStyle { get; private set; }
        public static GUIStyle MutedStyle { get; private set; }
        public static GUIStyle SmallCenterStyle { get; private set; }
        public static GUIStyle RightStyle { get; private set; }
        public static GUIStyle BadgeStyle { get; private set; }
        public static GUIStyle TabBtnStyle { get; private set; }
        public static GUIStyle TabActiveStyle { get; private set; }
        public static GUIStyle ActionBtnStyle { get; private set; }
        public static GUIStyle PrimaryBtnStyle { get; private set; }
        public static GUIStyle CardBtnStyle { get; private set; }

        public static Texture2D White => _whiteTex != null ? _whiteTex : (_whiteTex = Solid(Color.white));
        public static Texture2D ScanlineTex => _scanlineTex != null ? _scanlineTex : (_scanlineTex = BuildScanlineTex());
        public static Texture2D TopVignette => _topGradTex != null ? _topGradTex : (_topGradTex = VerticalGradient(new Color(0.03f, 0.05f, 0.08f, 0.88f), new Color(0.03f, 0.05f, 0.08f, 0f)));
        public static Texture2D BottomVignette => _botGradTex != null ? _botGradTex : (_botGradTex = VerticalGradient(new Color(0.03f, 0.05f, 0.08f, 0f), new Color(0.03f, 0.05f, 0.08f, 0.90f)));
        public static Texture2D AdminThumb => _adminThumb != null ? _adminThumb : (_adminThumb = BuildAdminThumb());
        public static Texture2D ArenaThumb => _arenaThumb != null ? _arenaThumb : (_arenaThumb = BuildArenaThumb());
        public static Texture2D RadarRingTex => _radarRingTex != null ? _radarRingTex : (_radarRingTex = BuildRadarRing());
        public static Texture2D ScopeMaskTex => _scopeMaskTex != null ? _scopeMaskTex : (_scopeMaskTex = BuildScopeMask());

        public static void EnsureInitialized()
        {
            if (TitleStyle != null) return;

            Texture2D texDarkBtn = Solid(new Color(0.11f, 0.16f, 0.21f, 0.90f));
            Texture2D texHoverBtn = Solid(new Color(0.18f, 0.27f, 0.34f, 0.96f));
            Texture2D texAmberBtn = Solid(new Color(0.84f, 0.54f, 0.12f, 0.98f));
            Texture2D texAmberHover = Solid(new Color(0.98f, 0.68f, 0.20f, 1f));
            Texture2D texTabBg = Solid(new Color(0.07f, 0.11f, 0.14f, 0.80f));
            Texture2D texTabActive = Solid(new Color(0.15f, 0.23f, 0.27f, 0.95f));

            TitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            TitleStyle.normal.textColor = Color.white;

            HeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            HeaderStyle.normal.textColor = Color.white;

            SubGoldStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            SubGoldStyle.normal.textColor = AccentAmber;

            BodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            BodyStyle.normal.textColor = new Color(0.91f, 0.94f, 0.97f);

            MutedStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            MutedStyle.normal.textColor = new Color(0.68f, 0.76f, 0.84f);

            SmallCenterStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            SmallCenterStyle.normal.textColor = Color.white;

            RightStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            RightStyle.normal.textColor = AccentCodGreen;

            BadgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            BadgeStyle.normal.textColor = Color.white;

            TabBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            TabBtnStyle.normal.background = texTabBg;
            TabBtnStyle.hover.background = texHoverBtn;
            TabBtnStyle.normal.textColor = new Color(0.80f, 0.86f, 0.90f);
            TabBtnStyle.hover.textColor = Color.white;

            TabActiveStyle = new GUIStyle(TabBtnStyle);
            TabActiveStyle.normal.background = texTabActive;
            TabActiveStyle.normal.textColor = AccentCodGreen;

            ActionBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            ActionBtnStyle.normal.background = texDarkBtn;
            ActionBtnStyle.hover.background = texHoverBtn;
            ActionBtnStyle.normal.textColor = Color.white;
            ActionBtnStyle.hover.textColor = AccentCodGreen;

            PrimaryBtnStyle = new GUIStyle(ActionBtnStyle) { fontSize = 13 };
            PrimaryBtnStyle.normal.background = texAmberBtn;
            PrimaryBtnStyle.hover.background = texAmberHover;
            PrimaryBtnStyle.normal.textColor = Color.white;

            CardBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4) };
            CardBtnStyle.normal.background = texDarkBtn;
            CardBtnStyle.hover.background = texHoverBtn;
            CardBtnStyle.normal.textColor = new Color(0.90f, 0.94f, 0.97f);
        }

        public static void DrawGlassPanel(Rect r, Color? fill = null, Color? accentLeft = null, Color? border = null, bool brackets = false)
        {
            FillRect(r, fill ?? GlassDark);
            // Subtle COD4 scanline tactical grid overlay
            GUI.color = new Color(1f, 1f, 1f, 0.14f);
            GUI.DrawTextureWithTexCoords(r, ScanlineTex, new Rect(0, 0, r.width / 64f, r.height / 64f));
            GUI.color = Color.white;

            DrawBorder(r, border ?? BorderSubtle, 1f);
            if (accentLeft.HasValue)
                FillRect(new Rect(r.x, r.y, 3f, r.height), accentLeft.Value);
            if (brackets)
                DrawCornerBrackets(r, accentLeft ?? AccentCodGreen, 9f, 2f);
        }

        public static void DrawCornerBrackets(Rect r, Color c, float len = 9f, float thick = 2f)
        {
            GUI.color = c;
            // Top-Left & Top-Right
            GUI.DrawTexture(new Rect(r.x, r.y, len, thick), White);
            GUI.DrawTexture(new Rect(r.x, r.y, thick, len), White);
            GUI.DrawTexture(new Rect(r.xMax - len, r.y, len, thick), White);
            GUI.DrawTexture(new Rect(r.xMax - thick, r.y, thick, len), White);
            // Bottom-Left & Bottom-Right
            GUI.DrawTexture(new Rect(r.x, r.yMax - thick, len, thick), White);
            GUI.DrawTexture(new Rect(r.x, r.yMax - len, thick, len), White);
            GUI.DrawTexture(new Rect(r.xMax - len, r.yMax - thick, len, thick), White);
            GUI.DrawTexture(new Rect(r.xMax - thick, r.yMax - len, thick, len), White);
            GUI.color = Color.white;
        }

        public static void DrawProgressBar(Rect r, float frac, Color fillColor, Color? bg = null)
        {
            FillRect(r, bg ?? new Color(0.14f, 0.19f, 0.24f, 0.90f));
            float w = r.width * Mathf.Clamp01(frac);
            if (w > 0.5f) FillRect(new Rect(r.x, r.y, w, r.height), fillColor);
        }

        public static void FillRect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = Color.white;
        }

        public static void DrawBorder(Rect r, Color c, float t = 1f)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), White);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), White);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), White);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), White);
            GUI.color = Color.white;
        }

        static Texture2D Solid(Color c)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c);
            if (SolidCache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            var t = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            SolidCache[key] = t;
            return t;
        }

        static Texture2D BuildScanlineTex()
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool scan = (y % 4) == 0;
                bool diag = ((x + y) % 8) == 0;
                float a = scan ? 0.12f : (diag ? 0.08f : 0.0f);
                t.SetPixel(x, y, new Color(0.45f, 0.65f, 0.72f, a));
            }
            t.Apply();
            return t;
        }

        static Texture2D VerticalGradient(Color top, Color bot)
        {
            const int h = 32;
            var t = new Texture2D(1, h, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                t.SetPixel(0, y, Color.Lerp(bot, top, y / (float)(h - 1)));
            t.Apply();
            return t;
        }

        static Texture2D BuildRadarRing()
        {
            const int s = 256;
            var t = new Texture2D(s, s, TextureFormat.ARGB32, false);
            float half = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - half, dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / half;
                Color col = Color.clear;
                if (dist <= 0.99f)
                {
                    col = new Color(0.04f, 0.09f, 0.11f, Mathf.Lerp(0.82f, 0.68f, dist));
                    if (dist >= 0.94f) col = new Color(0.36f, 0.90f, 0.45f, 0.85f);
                    else if (Mathf.Abs(dist - 0.50f) < 0.012f) col = new Color(0.36f, 0.90f, 0.45f, 0.24f);
                    else if ((Mathf.Abs(dx) < 1.2f || Mathf.Abs(dy) < 1.2f) && dist > 0.12f) col = new Color(0.4f, 0.85f, 0.5f, 0.18f);
                }
                t.SetPixel(x, y, col);
            }
            t.Apply();
            return t;
        }

        static Texture2D BuildScopeMask()
        {
            const int s = 512;
            var t = new Texture2D(s, s, TextureFormat.ARGB32, false);
            float half = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color col;
                if (d > 0.96f) col = new Color(0.01f, 0.01f, 0.02f, 0.96f);
                else if (d > 0.91f) col = new Color(0.06f, 0.08f, 0.10f, Mathf.SmoothStep(0.15f, 0.96f, (d - 0.91f) / 0.05f));
                else if (d > 0.82f) col = new Color(0f, 0.04f, 0.06f, 0.16f * ((d - 0.82f) / 0.09f));
                else col = Color.clear;
                t.SetPixel(x, y, col);
            }
            t.Apply();
            return t;
        }

        static Texture2D BuildAdminThumb()
        {
            const int w = 220, h = 104;
            var t = new Texture2D(w, h, TextureFormat.ARGB32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(new Color(0.28f, 0.56f, 0.86f), new Color(0.08f, 0.30f, 0.64f), y / (float)h);
                if (y < 24) c = Color.Lerp(new Color(0.18f, 0.44f, 0.16f), new Color(0.26f, 0.54f, 0.22f), (x % 9) / 9f);
                if (y < 14 && Mathf.Abs(x - 110) < 42) c = new Color(0.56f, 0.53f, 0.49f);
                // Side tropical trees silhouette
                if ((x < 34 || x > 186) && y >= 22 && y <= 58 && ((x - 18) * (x - 18) + (y - 44) * (y - 44) < 340 || (x - 202) * (x - 202) + (y - 44) * (y - 44) < 340))
                    c = new Color(0.14f, 0.42f, 0.16f);
                // 3-Story Cream & Terracotta Kilinochchi Admin Block
                if (x >= 38 && x <= 182 && y >= 24 && y <= 76)
                {
                    c = new Color(0.92f, 0.88f, 0.78f);
                    if (y == 40 || y == 57 || y >= 73) c = new Color(0.76f, 0.30f, 0.16f);
                    else if ((y >= 29 && y <= 36) || (y >= 45 && y <= 52) || (y >= 61 && y <= 69))
                        if ((x - 42) % 14 < 10) c = new Color(0.22f, 0.52f, 0.82f);
                }
                // Grand Portico columns & skylight center
                if (x >= 92 && x <= 128 && y >= 24 && y <= 82)
                    c = ((x - 92) % 9 < 3) ? new Color(0.96f, 0.92f, 0.84f) : new Color(0.28f, 0.58f, 0.88f);
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static Texture2D BuildArenaThumb()
        {
            const int w = 220, h = 104;
            var t = new Texture2D(w, h, TextureFormat.ARGB32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(new Color(0.58f, 0.74f, 0.90f), new Color(0.24f, 0.46f, 0.74f), y / (float)h);
                if (y < 28) c = new Color(0.68f, 0.56f, 0.38f);
                if (x >= 26 && x <= 74 && y >= 28 && y <= 58) c = new Color(0.18f, 0.44f, 0.72f);
                if (x >= 146 && x <= 194 && y >= 28 && y <= 62) c = new Color(0.76f, 0.30f, 0.20f);
                if (x >= 88 && x <= 132 && y >= 28 && y <= 70) c = new Color(0.32f, 0.36f, 0.42f);
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }
    }
}
