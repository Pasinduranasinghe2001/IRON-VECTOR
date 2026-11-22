using UnityEngine;

namespace IronVector
{
    /// <summary>Local player preferences (callsign, loadout, mouse). Saved in PlayerPrefs.</summary>
    public static class LocalProfile
    {
        const string KeyName = "iv_name";
        const string KeyPrimary = "iv_primary";
        const string KeySensitivity = "iv_sensitivity";
        const string KeyFov = "iv_fov";

        static bool _loaded;
        static string _name;
        static int _primary;
        static float _sensitivity;
        static float _fov;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _name = PlayerPrefs.GetString(KeyName, "");
            if (string.IsNullOrWhiteSpace(_name)) _name = "Operator" + Random.Range(100, 1000);
            _primary = PlayerPrefs.GetInt(KeyPrimary, 0);
            _sensitivity = PlayerPrefs.GetFloat(KeySensitivity, 2f);
            _fov = PlayerPrefs.GetFloat(KeyFov, 80f);
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
    }
}
