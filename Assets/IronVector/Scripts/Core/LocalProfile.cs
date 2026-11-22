using UnityEngine;

namespace IronVector
{
    /// <summary>Local player preferences (callsign, loadout, strategy, graphics, HUD). Saved in PlayerPrefs.</summary>
    public static class LocalProfile
    {
        const string KeyName = "iv_name";
        const string KeyPrimary = "iv_primary";
        const string KeySecondary = "iv_secondary";
        const string KeyStrategy = "iv_strategy";
        const string KeySensitivity = "iv_sensitivity";
        const string KeyFov = "iv_fov";
        const string KeyGraphics = "iv_gfx";
        const string KeyFps = "iv_fps";
        const string KeyVSync = "iv_vsync";
        const string KeyShowHud = "iv_hud";
        const string KeyShowPerf = "iv_perf";

        static bool _loaded;
        static string _name;
        static int _primary;
        static int _secondary;
        static int _strategy;
        static float _sensitivity;
        static float _fov;
        static int _graphics;
        static int _targetFps;
        static bool _vsync;
        static bool _showHud;
        static bool _showPerf;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _name = PlayerPrefs.GetString(KeyName, "");
            if (string.IsNullOrWhiteSpace(_name)) _name = "Operator" + Random.Range(100, 1000);
            _primary = PlayerPrefs.GetInt(KeyPrimary, 0);
            _secondary = PlayerPrefs.GetInt(KeySecondary, 0);
            _strategy = PlayerPrefs.GetInt(KeyStrategy, 0);
            _sensitivity = PlayerPrefs.GetFloat(KeySensitivity, 2.0f);
            _fov = PlayerPrefs.GetFloat(KeyFov, 82f);
            _graphics = Mathf.Clamp(PlayerPrefs.GetInt(KeyGraphics, 2), 0, 3); // Default High
            _targetFps = PlayerPrefs.GetInt(KeyFps, 144);
            _vsync = PlayerPrefs.GetInt(KeyVSync, 0) == 1;
            _showHud = PlayerPrefs.GetInt(KeyShowHud, 1) == 1;
            _showPerf = PlayerPrefs.GetInt(KeyShowPerf, 1) == 1;
        }

        public static string PlayerName
        {
            get { Load(); return _name; }
            set { Load(); if (_name == value) return; _name = value; PlayerPrefs.SetString(KeyName, value); }
        }

        public static int PrimaryWeapon
        {
            get { Load(); return _primary; }
            set { Load(); if (_primary == value) return; _primary = value; PlayerPrefs.SetInt(KeyPrimary, value); }
        }

        public static int SecondaryWeapon
        {
            get { Load(); return _secondary; }
            set { Load(); if (_secondary == value) return; _secondary = value; PlayerPrefs.SetInt(KeySecondary, value); }
        }

        public static int Strategy
        {
            get { Load(); return _strategy; }
            set { Load(); int c = Mathf.Clamp(value, 0, 3); if (_strategy == c) return; _strategy = c; PlayerPrefs.SetInt(KeyStrategy, c); }
        }

        public static float Sensitivity
        {
            get { Load(); return _sensitivity; }
            set { Load(); if (Mathf.Approximately(_sensitivity, value)) return; _sensitivity = value; PlayerPrefs.SetFloat(KeySensitivity, value); }
        }

        public static float FieldOfView
        {
            get { Load(); return _fov; }
            set { Load(); if (Mathf.Approximately(_fov, value)) return; _fov = value; PlayerPrefs.SetFloat(KeyFov, value); }
        }

        public static int GraphicsQuality
        {
            get { Load(); return _graphics; }
            set { Load(); int c = Mathf.Clamp(value, 0, 3); if (_graphics == c) return; _graphics = c; PlayerPrefs.SetInt(KeyGraphics, c); GraphicsSettingsManager.Apply(); }
        }

        public static int TargetFps
        {
            get { Load(); return _targetFps; }
            set { Load(); if (_targetFps == value) return; _targetFps = value; PlayerPrefs.SetInt(KeyFps, value); GraphicsSettingsManager.Apply(); }
        }

        public static bool VSync
        {
            get { Load(); return _vsync; }
            set { Load(); if (_vsync == value) return; _vsync = value; PlayerPrefs.SetInt(KeyVSync, value ? 1 : 0); GraphicsSettingsManager.Apply(); }
        }

        public static bool ShowHud
        {
            get { Load(); return _showHud; }
            set { Load(); if (_showHud == value) return; _showHud = value; PlayerPrefs.SetInt(KeyShowHud, value ? 1 : 0); }
        }

        public static bool ShowPerformance
        {
            get { Load(); return _showPerf; }
            set { Load(); if (_showPerf == value) return; _showPerf = value; PlayerPrefs.SetInt(KeyShowPerf, value ? 1 : 0); }
        }
    }
}
