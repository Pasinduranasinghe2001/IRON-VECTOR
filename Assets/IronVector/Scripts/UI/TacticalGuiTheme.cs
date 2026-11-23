using System.Collections.Generic;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Shared tactical glassmorphism GUI styling, crisp 1px metallic borders, map preview thumbnails,
    /// daily challenge tracking, and stat bars matching a modern military FPS lobby.
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
            new ChallengeInfo { Title = "Deploy & eliminate 15 hostiles in AdminHall", Current = 15, Target = 15, Xp = 2500 },
            new ChallengeInfo { Title = "Win 3 rounds on Search & Destroy", Current = 2, Target = 3, Xp = 2500 },
            new ChallengeInfo { Title = "Score 5 headshots with Marksman / Carbine", Current = 3, Target = 5, Xp = 5000 },
            new ChallengeInfo { Title = "Complete 3 daily tactical objectives", Current = 1, Target = 3, Xp = 7500 }
        };

        public static readonly Color AccentAmber = new Color(0.92f, 0.62f, 0.18f, 1f);
        public static readonly Color AccentCyan = new Color(0.28f, 0.78f, 0.96f, 1f);
        public static readonly Color AccentGreen = new Color(0.46f, 0.82f, 0.34f, 1f);
        public static readonly Color GlassDark = new Color(0.05f, 0.07f, 0.10f, 0.80f);
        public static readonly Color GlassCard = new Color(0.08f, 0.11f, 0.15f, 0.84f);
        public static readonly Color BorderSubtle = new Color(1f, 1f, 1f, 0.16f);

        static Texture2D _whiteTex;
        static Texture2D _topGradTex;
        static Texture2D _botGradTex;
        static Texture2D _adminThumb;
        static Texture2D _arenaThumb;
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
        public static Texture2D TopVignette => _topGradTex != null ? _topGradTex : (_topGradTex = VerticalGradient(new Color(0.02f, 0.04f, 0.07f, 0.88f), new Color(0.02f, 0.04f, 0.07f, 0f)));
        public static Texture2D BottomVignette => _botGradTex != null ? _botGradTex : (_botGradTex = VerticalGradient(new Color(0.02f, 0.04f, 0.07f, 0f), new Color(0.02f, 0.04f, 0.07f, 0.90f)));
        public static Texture2D AdminThumb => _adminThumb != null ? _adminThumb : (_adminThumb = BuildAdminThumb());
        public static Texture2D ArenaThumb => _arenaThumb != null ? _arenaThumb : (_arenaThumb = BuildArenaThumb());

        public static void EnsureInitialized()
        {
            if (TitleStyle != null) return;

            Texture2D texDarkBtn = Solid(new Color(0.11f, 0.15f, 0.21f, 0.88f));
            Texture2D texHoverBtn = Solid(new Color(0.18f, 0.24f, 0.33f, 0.95f));
            Texture2D texAmberBtn = Solid(new Color(0.84f, 0.50f, 0.12f, 0.96f));
            Texture2D texAmberHover = Solid(new Color(0.95f, 0.62f, 0.18f, 1f));
            Texture2D texTabBg = Solid(new Color(0.07f, 0.10f, 0.14f, 0.72f));
            Texture2D texTabActive = Solid(new Color(0.15f, 0.20f, 0.27f, 0.94f));

            TitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            TitleStyle.normal.textColor = Color.white;

            HeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            HeaderStyle.normal.textColor = new Color(0.95f, 0.97f, 1f);

            SubGoldStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            SubGoldStyle.normal.textColor = AccentAmber;

            BodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            BodyStyle.normal.textColor = new Color(0.88f, 0.91f, 0.95f);

            MutedStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            MutedStyle.normal.textColor = new Color(0.62f, 0.69f, 0.78f);

            SmallCenterStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            SmallCenterStyle.normal.textColor = new Color(0.90f, 0.93f, 0.97f);

            RightStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            RightStyle.normal.textColor = AccentCyan;

            BadgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            BadgeStyle.normal.textColor = Color.white;

            TabBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            TabBtnStyle.normal.background = texTabBg;
            TabBtnStyle.hover.background = texHoverBtn;
            TabBtnStyle.normal.textColor = new Color(0.76f, 0.81f, 0.88f);
            TabBtnStyle.hover.textColor = Color.white;

            TabActiveStyle = new GUIStyle(TabBtnStyle);
            TabActiveStyle.normal.background = texTabActive;
            TabActiveStyle.normal.textColor = Color.white;

            ActionBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            ActionBtnStyle.normal.background = texDarkBtn;
            ActionBtnStyle.hover.background = texHoverBtn;
            ActionBtnStyle.normal.textColor = Color.white;
            ActionBtnStyle.hover.textColor = Color.white;

            PrimaryBtnStyle = new GUIStyle(ActionBtnStyle) { fontSize = 13 };
            PrimaryBtnStyle.normal.background = texAmberBtn;
            PrimaryBtnStyle.hover.background = texAmberHover;
            PrimaryBtnStyle.normal.textColor = Color.white;

            CardBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4) };
            CardBtnStyle.normal.background = texDarkBtn;
            CardBtnStyle.hover.background = texHoverBtn;
            CardBtnStyle.normal.textColor = new Color(0.88f, 0.92f, 0.96f);
        }

        public static void DrawGlassPanel(Rect r, Color? fill = null, Color? accentLeft = null, Color? border = null)
        {
            FillRect(r, fill ?? GlassDark);
            DrawBorder(r, border ?? BorderSubtle, 1f);
            if (accentLeft.HasValue)
                FillRect(new Rect(r.x, r.y, 3f, r.height), accentLeft.Value);
        }

        public static void DrawProgressBar(Rect r, float frac, Color fillColor, Color? bg = null)
        {
            FillRect(r, bg ?? new Color(0.14f, 0.18f, 0.24f, 0.85f));
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

        static Texture2D VerticalGradient(Color top, Color bot)
        {
            const int h = 32;
            var t = new Texture2D(1, h, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                t.SetPixel(0, y, Color.Lerp(bot, top, y / (float)(h - 1)));
            t.Apply();
            return t;
        }

        static Texture2D BuildAdminThumb()
        {
            const int w = 160, h = 76;
            var t = new Texture2D(w, h, TextureFormat.ARGB32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(new Color(0.06f, 0.11f, 0.18f), new Color(0.14f, 0.25f, 0.38f), y / (float)h);
                if (y < 18) c = new Color(0.12f, 0.22f, 0.14f); // green Kilinochchi campus lawn
                // 3-story cream facade & terracotta cornice bands
                if (x >= 26 && x <= 134 && y >= 18 && y <= 54)
                {
                    c = new Color(0.80f, 0.77f, 0.70f);
                    if (y == 30 || y == 42 || y == 53) c = new Color(0.68f, 0.30f, 0.18f);
                    else if ((y >= 22 && y <= 27) || (y >= 34 && y <= 39) || (y >= 45 && y <= 50))
                        if (x % 12 < 8) c = new Color(0.22f, 0.44f, 0.66f);
                }
                if (x >= 68 && x <= 92 && y >= 18 && y <= 58)
                    c = (x == 72 || x == 88) ? new Color(0.88f, 0.85f, 0.78f) : new Color(0.32f, 0.56f, 0.78f);
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static Texture2D BuildArenaThumb()
        {
            const int w = 160, h = 76;
            var t = new Texture2D(w, h, TextureFormat.ARGB32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(new Color(0.16f, 0.13f, 0.10f), new Color(0.32f, 0.24f, 0.17f), y / (float)h);
                if (y < 22) c = new Color(0.20f, 0.18f, 0.16f);
                if (x >= 20 && x <= 56 && y >= 22 && y <= 44) c = new Color(0.18f, 0.30f, 0.44f);
                if (x >= 102 && x <= 140 && y >= 22 && y <= 48) c = new Color(0.56f, 0.28f, 0.18f);
                if (x >= 66 && x <= 96 && y >= 22 && y <= 54) c = new Color(0.28f, 0.31f, 0.34f);
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }
    }
}
