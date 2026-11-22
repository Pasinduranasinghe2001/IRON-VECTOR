using System.Collections.Generic;
using FishNet.Managing;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Full combat HUD: dynamic spread crosshair, sniper scope, hitmarkers, directional damage flash,
    /// tactical radar, score/timer/Domination flags, killfeed, Tab scoreboard, death/match-end banners and pause menu.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        struct KillFeedItem
        {
            public string Text;
            public float ExpiresAt;
        }

        static readonly List<KillFeedItem> KillFeed = new List<KillFeedItem>();
        static float _hitMarkerUntil;
        static bool _hitWasKill;
        static bool _hitWasHeadshot;
        static float _damageFlashUntil;
        static Vector3 _damageOrigin;

        static Texture2D _whiteTex;
        static Texture2D _circleTex;
        static GUIStyle _boldCenter, _boldLeft, _boldRight, _richLeft, _bigBanner;

        readonly List<PlayerIdentity> _sortedPlayers = new List<PlayerIdentity>();
        readonly List<CapturePoint> _sortedPoints = new List<CapturePoint>();

        public static void NotifyHit(bool kill, bool headshot)
        {
            _hitMarkerUntil = Time.time + (kill ? 0.24f : 0.14f);
            _hitWasKill = kill;
            _hitWasHeadshot = headshot;
            FxUtil.PlaySound2D(FxUtil.HitClip, headshot ? 0.85f : 0.65f, headshot ? 1.35f : 1.0f);
        }

        public static void NotifyDamaged(Vector3 fromWorld)
        {
            _damageOrigin = fromWorld;
            _damageFlashUntil = Time.time + 0.85f;
        }

        public static void AddKillFeed(string killer, int killerTeam, string victim, int victimTeam, string source, bool headshot)
        {
            string kHtml = string.IsNullOrEmpty(killer)
                ? ""
                : $"<color=#{Teams.Hex(killerTeam)}>{killer}</color> ";
            string vHtml = $"<color=#{Teams.Hex(victimTeam)}>{victim}</color>";
            string hsTag = headshot ? " <color=#FFCC44>[HS]</color>" : "";
            KillFeed.Add(new KillFeedItem
            {
                Text = $"{kHtml}[{source}]{hsTag} {vHtml}",
                ExpiresAt = Time.time + 6.5f
            });
            while (KillFeed.Count > 5) KillFeed.RemoveAt(0);
        }

        void OnGUI()
        {
            if (Application.isBatchMode) return;
            PlayerIdentity local = PlayerIdentity.Local;
            if (local == null) return;

            EnsureTexturesAndStyles();
            GUI.depth = PlayerLook.PauseMenuOpen ? 10 : -5;

            bool alive = local.Health != null && local.Health.IsAlive;
            bool inVehicle = local.VehicleUser != null && local.VehicleUser.InVehicle;

            if (alive && !inVehicle)
            {
                if (local.Weapons != null && local.Weapons.IsSniperScope)
                    DrawSniperScope();
                else
                    DrawCrosshair(local);
            }

            DrawHitmarker();
            DrawDamageOverlay(local);
            DrawTopMatchBar();
            DrawDominationFlags();
            DrawRadar(local);
            DrawKillFeed();

            if (alive)
            {
                DrawBottomLeftVitals(local);
                DrawBottomRightWeapon(local, inVehicle);
                DrawVehiclePrompt(local);
            }
            else
            {
                DrawDeathOverlay(local);
            }

            if (MatchManager.Instance != null && MatchManager.Instance.State.Value == MatchState.Ended)
                DrawMatchEndedOverlay();
            else if (IVInput.RawHeld(IVKey.Scoreboard))
                DrawScoreboard();

            if (PlayerLook.PauseMenuOpen)
                DrawPauseMenu();
        }

        void DrawCrosshair(PlayerIdentity local)
        {
            float spread = local.Weapons != null ? local.Weapons.CurrentSpreadDegrees : 1.5f;
            float gap = Mathf.Clamp(6f + spread * 7.5f, 5f, 44f);
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float len = 9f;
            float thick = 2f;

            GUI.color = new Color(1f, 1f, 1f, 0.86f);
            GUI.DrawTexture(new Rect(cx - gap - len, cy - thick * 0.5f, len, thick), _whiteTex);
            GUI.DrawTexture(new Rect(cx + gap, cy - thick * 0.5f, len, thick), _whiteTex);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy - gap - len, thick, len), _whiteTex);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy + gap, thick, len), _whiteTex);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), _whiteTex);
            GUI.color = Color.white;
        }

        void DrawSniperScope()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float r = Mathf.Min(Screen.width, Screen.height) * 0.38f;

            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, cy - r), _whiteTex);
            GUI.DrawTexture(new Rect(0, cy + r, Screen.width, Screen.height - (cy + r)), _whiteTex);
            GUI.DrawTexture(new Rect(0, cy - r, cx - r, r * 2f), _whiteTex);
            GUI.DrawTexture(new Rect(cx + r, cy - r, Screen.width - (cx + r), r * 2f), _whiteTex);

            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
            GUI.DrawTexture(new Rect(cx - r, cy - 1f, r * 2f, 2f), _whiteTex);
            GUI.DrawTexture(new Rect(cx - 1f, cy - r, 2f, r * 2f), _whiteTex);
            GUI.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), _whiteTex);
            GUI.color = Color.white;
        }

        void DrawHitmarker()
        {
            if (Time.time >= _hitMarkerUntil) return;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.color = _hitWasKill ? new Color(1f, 0.22f, 0.22f) : (_hitWasHeadshot ? new Color(1f, 0.85f, 0.2f) : Color.white);
            float d = 8f;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    GUI.DrawTexture(new Rect(cx + i * d - 2f, cy + j * d - 2f, 4f, 4f), _whiteTex);
                    GUI.DrawTexture(new Rect(cx + i * (d + 4f) - 1.5f, cy + j * (d + 4f) - 1.5f, 3f, 3f), _whiteTex);
                }
            }
            GUI.color = Color.white;
        }

        void DrawDamageOverlay(PlayerIdentity local)
        {
            float hp01 = local.Health != null ? local.Health.Health.Value / PlayerHealth.MaxHealth : 1f;
            float lowHpAlpha = hp01 < 0.45f ? (0.45f - hp01) * 0.65f : 0f;
            float flashAlpha = Time.time < _damageFlashUntil ? (_damageFlashUntil - Time.time) * 0.35f : 0f;
            float alpha = Mathf.Clamp(lowHpAlpha + flashAlpha, 0f, 0.5f);
            if (alpha > 0.01f)
            {
                GUI.color = new Color(0.8f, 0.05f, 0.05f, alpha);
                float edge = 28f;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, Screen.height - edge, Screen.width, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, edge, Screen.height), _whiteTex);
                GUI.DrawTexture(new Rect(Screen.width - edge, 0, edge, Screen.height), _whiteTex);
            }

            if (Time.time < _damageFlashUntil)
            {
                Vector3 toSrc = _damageOrigin - local.transform.position;
                toSrc.y = 0f;
                if (toSrc.sqrMagnitude > 0.1f)
                {
                    Vector3 fwd = local.transform.forward; fwd.y = 0f;
                    float angle = Vector3.SignedAngle(fwd.normalized, toSrc.normalized, Vector3.up) * Mathf.Deg2Rad;
                    float cx = Screen.width * 0.5f + Mathf.Sin(angle) * 140f;
                    float cy = Screen.height * 0.5f - Mathf.Cos(angle) * 140f;
                    GUI.color = new Color(1f, 0.2f, 0.15f, (_damageFlashUntil - Time.time));
                    GUI.DrawTexture(new Rect(cx - 12f, cy - 12f, 24f, 24f), _circleTex);
                }
            }
            GUI.color = Color.white;
        }

        void DrawTopMatchBar()
        {
            MatchManager mm = MatchManager.Instance;
            int a = mm != null ? mm.AlphaScore.Value : 0;
            int b = mm != null ? mm.BravoScore.Value : 0;
            int secs = mm != null ? mm.TimeRemaining.Value : 600;
            string modeLabel = mm != null && mm.Mode.Value == GameMode.Domination ? "DOM" : "TDM";

            float cx = Screen.width * 0.5f;
            Rect bg = new Rect(cx - 170f, 12f, 340f, 40f);
            DrawRect(bg, new Color(0.05f, 0.07f, 0.09f, 0.82f));
            DrawRect(new Rect(bg.x, bg.y, 108f, bg.height), Teams.TeamColor(Teams.Alpha) * 0.65f);
            DrawRect(new Rect(bg.xMax - 108f, bg.y, 108f, bg.height), Teams.TeamColor(Teams.Bravo) * 0.65f);

            GUI.Label(new Rect(bg.x, bg.y, 108f, bg.height), $"ALPHA  {a}", _boldCenter);
            GUI.Label(new Rect(cx - 60f, bg.y + 1f, 120f, 22f), $"{secs / 60:00}:{secs % 60:00}", _boldCenter);
            GUI.Label(new Rect(cx - 60f, bg.y + 19f, 120f, 20f), modeLabel, _boldCenter);
            GUI.Label(new Rect(bg.xMax - 108f, bg.y, 108f, bg.height), $"{b}  BRAVO", _boldCenter);
        }

        void DrawDominationFlags()
        {
            if (MatchManager.Instance == null || MatchManager.Instance.Mode.Value != GameMode.Domination) return;
            if (CapturePoint.All.Count == 0) return;

            _sortedPoints.Clear();
            _sortedPoints.AddRange(CapturePoint.All);
            _sortedPoints.Sort((x, y) => x.LabelIndex.Value.CompareTo(y.LabelIndex.Value));

            float startX = Screen.width * 0.5f - (_sortedPoints.Count * 56f) * 0.5f;
            for (int i = 0; i < _sortedPoints.Count; i++)
            {
                CapturePoint cp = _sortedPoints[i];
                if (cp == null) continue;
                Rect r = new Rect(startX + i * 58f, 58f, 50f, 36f);
                Color baseCol = cp.OwnerTeam.Value != Teams.None ? Teams.TeamColor(cp.OwnerTeam.Value) : new Color(0.25f, 0.27f, 0.30f);
                DrawRect(r, new Color(baseCol.r * 0.7f, baseCol.g * 0.7f, baseCol.b * 0.7f, 0.85f));
                DrawRect(new Rect(r.x, r.yMax - 5f, r.width * Mathf.Clamp01(cp.Progress.Value), 5f),
                    Teams.TeamColor(cp.CapturingTeam.Value != Teams.None ? cp.CapturingTeam.Value : cp.OwnerTeam.Value));
                GUI.Label(r, cp.Letter, _boldCenter);
            }
        }

        void DrawRadar(PlayerIdentity local)
        {
            float size = 144f;
            float pad = 16f;
            Vector2 center = new Vector2(pad + size * 0.5f, pad + size * 0.5f);

            GUI.color = new Color(0.05f, 0.08f, 0.11f, 0.78f);
            GUI.DrawTexture(new Rect(pad, pad, size, size), _circleTex);
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(new Rect(center.x - size * 0.45f, center.y, size * 0.9f, 1f), _whiteTex);
            GUI.DrawTexture(new Rect(center.x, center.y - size * 0.45f, 1f, size * 0.9f), _whiteTex);

            float range = 65f;
            float invYaw = -local.transform.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(invYaw);
            float sin = Mathf.Sin(invYaw);

            // Capture zones on radar
            foreach (CapturePoint cp in CapturePoint.All)
            {
                if (cp == null) continue;
                DrawRadarDot(local.transform.position, cp.transform.position, center, size * 0.44f, range, cos, sin, Teams.TeamColor(cp.OwnerTeam.Value), 7f);
            }

            // Vehicles on radar
            foreach (VehicleController v in VehicleController.All)
            {
                if (v == null || (v.Health != null && !v.Health.IsAlive)) continue;
                DrawRadarDot(local.transform.position, v.transform.position, center, size * 0.44f, range, cos, sin, new Color(1f, 0.75f, 0.25f), 6f);
            }

            // Teammates + nearby firing enemies
            foreach (PlayerIdentity p in PlayerIdentity.All)
            {
                if (p == null || p == local || p.Health == null || !p.Health.IsAlive) continue;
                bool ally = p.Team.Value == local.Team.Value;
                bool recentlyFired = p.Weapons != null && Time.time - p.Weapons.LastShotTime < 1.8f;
                if (!ally && !recentlyFired) continue;
                DrawRadarDot(local.transform.position, p.transform.position, center, size * 0.44f, range, cos, sin, Teams.TeamColor(p.Team.Value), 6f);
            }

            // Local player dot at center
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - 4f, center.y - 4f, 8f, 8f), _circleTex);
        }

        void DrawRadarDot(Vector3 myPos, Vector3 targetPos, Vector2 center, float radiusPx, float range, float cos, float sin, Color c, float dotSize)
        {
            Vector3 d = targetPos - myPos;
            Vector2 rel = new Vector2(d.x, d.z) / range;
            if (rel.sqrMagnitude > 1f) rel.Normalize();
            float rx = rel.x * cos - rel.y * sin;
            float ry = rel.x * sin + rel.y * cos;
            Vector2 px = center + new Vector2(rx * radiusPx, -ry * radiusPx);
            GUI.color = c;
            GUI.DrawTexture(new Rect(px.x - dotSize * 0.5f, px.y - dotSize * 0.5f, dotSize, dotSize), _circleTex);
        }

        void DrawKillFeed()
        {
            for (int i = KillFeed.Count - 1; i >= 0; i--)
                if (Time.time >= KillFeed[i].ExpiresAt) KillFeed.RemoveAt(i);

            float x = Screen.width - 340f;
            float y = 16f;
            for (int i = 0; i < KillFeed.Count; i++)
            {
                Rect r = new Rect(x, y + i * 24f, 324f, 22f);
                DrawRect(r, new Color(0.04f, 0.06f, 0.08f, 0.65f));
                GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), KillFeed[i].Text, _richLeft);
            }
        }

        void DrawBottomLeftVitals(PlayerIdentity local)
        {
            float hp = local.Health != null ? local.Health.Health.Value : 100f;
            float frac = Mathf.Clamp01(hp / PlayerHealth.MaxHealth);
            Color teamCol = Teams.TeamColor(local.Team.Value);
            string stance = local.Motor != null && local.Motor.IsSliding ? "SLIDE" : (local.Motor != null && local.Motor.IsCrouching ? "CROUCH" : "STAND");

            Rect bg = new Rect(16f, Screen.height - 76f, 250f, 60f);
            DrawRect(bg, new Color(0.05f, 0.07f, 0.09f, 0.82f));
            GUI.Label(new Rect(bg.x + 12f, bg.y + 4f, 150f, 22f), $"{Teams.Name(local.Team.Value)} · {stance}", _boldLeft);
            GUI.Label(new Rect(bg.xMax - 80f, bg.y + 4f, 68f, 22f), $"{Mathf.CeilToInt(hp)} HP", _boldRight);

            Rect barBg = new Rect(bg.x + 12f, bg.y + 32f, bg.width - 24f, 14f);
            DrawRect(barBg, new Color(0.15f, 0.17f, 0.20f, 0.9f));
            Color barCol = frac > 0.35f ? teamCol : new Color(0.95f, 0.22f, 0.20f);
            DrawRect(new Rect(barBg.x, barBg.y, barBg.width * frac, barBg.height), barCol);
        }

        void DrawBottomRightWeapon(PlayerIdentity local, bool inVehicle)
        {
            Rect bg = new Rect(Screen.width - 286f, Screen.height - 86f, 270f, 70f);
            DrawRect(bg, new Color(0.05f, 0.07f, 0.09f, 0.82f));

            if (inVehicle && local.VehicleUser.Vehicle != null)
            {
                VehicleController vc = local.VehicleUser.Vehicle;
                float armor = vc.Health != null ? vc.Health.Health.Value : 0f;
                GUI.Label(new Rect(bg.x + 12f, bg.y + 6f, bg.width - 24f, 22f), vc.vehicleName, _boldRight);
                GUI.Label(new Rect(bg.x + 12f, bg.y + 28f, bg.width - 24f, 22f), $"{Mathf.RoundToInt(vc.CurrentSpeedKmh)} KM/H   |   ARMOR {Mathf.CeilToInt(armor)}", _boldRight);
                return;
            }

            WeaponController wc = local.Weapons;
            WeaponData w = wc != null ? wc.CurrentWeapon : null;
            string wName = w != null ? w.weaponName : "Unarmed";
            int mag = wc != null ? wc.CurrentAmmo : 0;
            int res = wc != null ? wc.CurrentReserve : 0;
            int frags = local.Grenades != null ? local.Grenades.Remaining : 0;

            GUI.Label(new Rect(bg.x + 12f, bg.y + 6f, bg.width - 24f, 22f), wName, _boldRight);
            GUI.Label(new Rect(bg.x + 12f, bg.y + 26f, bg.width - 24f, 22f), $"{mag} / {res}   ·   [G] FRAG x{frags}", _boldRight);

            if (wc != null && wc.IsReloading)
            {
                Rect relBg = new Rect(bg.x + 12f, bg.y + 52f, bg.width - 24f, 8f);
                DrawRect(relBg, new Color(0.2f, 0.22f, 0.25f));
                DrawRect(new Rect(relBg.x, relBg.y, relBg.width * wc.ReloadProgress01, relBg.height), new Color(0.95f, 0.65f, 0.2f));
            }
        }

        void DrawVehiclePrompt(PlayerIdentity local)
        {
            if (local.VehicleUser == null) return;
            string msg = null;
            if (local.VehicleUser.InVehicle)
                msg = "[F] Exit Vehicle   ·   [WASD] Drive   ·   [Space] Handbrake";
            else if (local.VehicleUser.NearbyVehicle != null)
                msg = $"[F] Drive {local.VehicleUser.NearbyVehicle.vehicleName}";

            if (string.IsNullOrEmpty(msg)) return;
            Rect r = new Rect(Screen.width * 0.5f - 220f, Screen.height - 132f, 440f, 30f);
            DrawRect(r, new Color(0.05f, 0.07f, 0.09f, 0.78f));
            GUI.Label(r, msg, _boldCenter);
        }

        void DrawDeathOverlay(PlayerIdentity local)
        {
            float left = Mathf.Max(0f, local.Health.LocalRespawnAt - Time.time);
            Rect r = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 55f, 440f, 110f);
            DrawRect(r, new Color(0.06f, 0.04f, 0.05f, 0.85f));
            string killer = string.IsNullOrEmpty(local.Health.LastKillerName) ? "KIA" : $"ELIMINATED BY {local.Health.LastKillerName}";
            GUI.Label(new Rect(r.x, r.y + 18f, r.width, 32f), killer, _bigBanner);
            GUI.Label(new Rect(r.x, r.y + 58f, r.width, 28f), $"Deploying in {left:0.0}s...", _boldCenter);
        }

        void DrawMatchEndedOverlay()
        {
            MatchManager mm = MatchManager.Instance;
            int wTeam = mm != null ? mm.WinnerTeam.Value : Teams.None;
            string banner = wTeam == Teams.None ? "MATCH DRAW" : $"{Teams.Name(wTeam)} VICTORY";
            Rect r = new Rect(Screen.width * 0.5f - 260f, 120f, 520f, 120f);
            DrawRect(r, new Color(0.05f, 0.07f, 0.09f, 0.90f));
            GUI.Label(new Rect(r.x, r.y + 20f, r.width, 40f), banner, _bigBanner);
            if (mm != null)
                GUI.Label(new Rect(r.x, r.y + 66f, r.width, 28f), $"ALPHA {mm.AlphaScore.Value}  —  {mm.BravoScore.Value} BRAVO   (Next round starting soon)", _boldCenter);
        }

        void DrawScoreboard()
        {
            _sortedPlayers.Clear();
            _sortedPlayers.AddRange(PlayerIdentity.All);
            _sortedPlayers.Sort((a, b) =>
            {
                int tc = a.Team.Value.CompareTo(b.Team.Value);
                return tc != 0 ? tc : b.Score.Value.CompareTo(a.Score.Value);
            });

            float w = 540f;
            float h = 76f + Mathf.Max(1, _sortedPlayers.Count) * 26f;
            Rect r = new Rect(Screen.width * 0.5f - w * 0.5f, 110f, w, h);
            DrawRect(r, new Color(0.05f, 0.07f, 0.09f, 0.92f));

            GUI.Label(new Rect(r.x + 14f, r.y + 10f, 200f, 24f), "CALLSIGN", _boldLeft);
            GUI.Label(new Rect(r.x + 220f, r.y + 10f, 80f, 24f), "TEAM", _boldCenter);
            GUI.Label(new Rect(r.x + 310f, r.y + 10f, 65f, 24f), "KILLS", _boldCenter);
            GUI.Label(new Rect(r.x + 380f, r.y + 10f, 70f, 24f), "DEATHS", _boldCenter);
            GUI.Label(new Rect(r.x + 455f, r.y + 10f, 70f, 24f), "SCORE", _boldCenter);
            DrawRect(new Rect(r.x + 10f, r.y + 38f, r.width - 20f, 1f), new Color(1f, 1f, 1f, 0.25f));

            for (int i = 0; i < _sortedPlayers.Count; i++)
            {
                PlayerIdentity p = _sortedPlayers[i];
                if (p == null) continue;
                float rowY = r.y + 44f + i * 26f;
                Color tc = Teams.TeamColor(p.Team.Value);
                DrawRect(new Rect(r.x + 10f, rowY, r.width - 20f, 24f), new Color(tc.r, tc.g, tc.b, p == PlayerIdentity.Local ? 0.28f : 0.12f));
                GUI.Label(new Rect(r.x + 14f, rowY, 200f, 24f), p.PlayerName.Value, _boldLeft);
                GUI.Label(new Rect(r.x + 220f, rowY, 80f, 24f), Teams.Name(p.Team.Value), _boldCenter);
                GUI.Label(new Rect(r.x + 310f, rowY, 65f, 24f), p.Kills.Value.ToString(), _boldCenter);
                GUI.Label(new Rect(r.x + 380f, rowY, 70f, 24f), p.Deaths.Value.ToString(), _boldCenter);
                GUI.Label(new Rect(r.x + 455f, rowY, 70f, 24f), p.Score.Value.ToString(), _boldCenter);
            }
        }

        void DrawPauseMenu()
        {
            DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            Rect r = new Rect(Screen.width * 0.5f - 175f, Screen.height * 0.5f - 130f, 350f, 260f);
            DrawRect(r, new Color(0.08f, 0.10f, 0.13f, 0.96f));

            GUILayout.BeginArea(new Rect(r.x + 22f, r.y + 18f, r.width - 44f, r.height - 36f));
            GUILayout.Label("PAUSED", _bigBanner);
            GUILayout.Space(12);

            GUILayout.Label($"Mouse Sensitivity: {LocalProfile.Sensitivity:0.0}", _boldLeft);
            LocalProfile.Sensitivity = GUILayout.HorizontalSlider(LocalProfile.Sensitivity, 0.4f, 8f);
            GUILayout.Space(8);

            GUILayout.Label($"Field of View: {LocalProfile.FieldOfView:0}", _boldLeft);
            LocalProfile.FieldOfView = GUILayout.HorizontalSlider(LocalProfile.FieldOfView, 65f, 105f);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("RESUME", GUILayout.Height(36)))
                PlayerLook.Resume();
            GUILayout.Space(8);

            if (GUILayout.Button("DISCONNECT TO MENU", GUILayout.Height(34)))
            {
                NetworkManager nm = FindAnyObjectByType<NetworkManager>();
                if (nm != null)
                {
                    if (nm.IsClientStarted) nm.ClientManager.StopConnection();
                    if (nm.IsServerStarted) nm.ServerManager.StopConnection(true);
                }
                PlayerLook.ResetInputState();
            }

            GUILayout.EndArea();
        }

        void DrawRect(Rect rect, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = Color.white;
        }

        static void EnsureTexturesAndStyles()
        {
            if (_whiteTex != null) return;
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            const int s = 64;
            _circleTex = new Texture2D(s, s, TextureFormat.ARGB32, false);
            float center = (s - 1) * 0.5f;
            float rSq = center * center;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    _circleTex.SetPixel(x, y, dx * dx + dy * dy <= rSq ? Color.white : Color.clear);
                }
            }
            _circleTex.Apply();

            _boldCenter = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _boldCenter.normal.textColor = Color.white;

            _boldLeft = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _boldLeft.normal.textColor = Color.white;

            _boldRight = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            _boldRight.normal.textColor = Color.white;

            _richLeft = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleLeft };
            _richLeft.normal.textColor = Color.white;

            _bigBanner = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _bigBanner.normal.textColor = Color.white;
        }
    }
}
