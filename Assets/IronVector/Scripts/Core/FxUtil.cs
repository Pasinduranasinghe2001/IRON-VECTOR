using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronVector
{
    /// <summary>
    /// Placeholder VFX + procedural SFX so the prototype has "juice" with zero art assets.
    /// Replace with VFX Graph / real audio later; call sites stay the same.
    /// </summary>
    public static class FxUtil
    {
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        static AudioClip _shot, _boom, _click, _reload, _hit;

        public static bool Enabled => !Application.isBatchMode;

        public static Material ColorMaterial(Color c)
        {
            if (Materials.TryGetValue(c, out Material m) && m != null) return m;
            m = new Material(Shader.Find("Sprites/Default")) { color = c };
            Materials[c] = m;
            return m;
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.025f, float life = 0.06f)
        {
            if (!Enabled) return;
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = ColorMaterial(Color.white);
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = width;
            lr.endWidth = width * 0.4f;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0.1f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Object.Destroy(go, life);
        }

        public static void Flash(Vector3 position, Color color, float range, float intensity, float life)
        {
            if (!Enabled) return;
            var go = new GameObject("Flash");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            Object.Destroy(go, life);
        }

        public static GameObject Blob(PrimitiveType type, Vector3 position, float size, Color color, float life)
        {
            if (!Enabled) return null;
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "FX";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = ColorMaterial(color);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            Object.Destroy(go, life);
            return go;
        }

        public static void MuzzleFlash(Vector3 position, Vector3 direction)
        {
            if (!Enabled) return;
            Flash(position, new Color(1f, 0.78f, 0.4f), 7f, 4f, 0.05f);
            Blob(PrimitiveType.Sphere, position + direction * 0.05f, 0.13f, new Color(1f, 0.85f, 0.45f), 0.035f);
        }

        public static void Impact(Vector3 position, Vector3 normal, bool flesh)
        {
            if (!Enabled) return;
            Color c = flesh ? new Color(0.75f, 0.05f, 0.05f) : new Color(0.85f, 0.78f, 0.6f);
            Blob(PrimitiveType.Cube, position + normal * 0.02f, flesh ? 0.12f : 0.07f, c, 0.3f);
        }

        public static void Explosion(Vector3 position, float radius)
        {
            if (!Enabled) return;
            Flash(position + Vector3.up, new Color(1f, 0.6f, 0.25f), radius * 3f, 10f, 0.25f);
            GameObject fire = Blob(PrimitiveType.Sphere, position, 0.5f, new Color(1f, 0.55f, 0.15f, 0.95f), 0.55f);
            if (fire != null)
            {
                fire.GetComponent<Renderer>().material = new Material(ColorMaterial(new Color(1f, 0.55f, 0.15f, 0.95f)));
                fire.AddComponent<FxExpand>().Init(radius * 0.9f, 0.55f);
            }
            Smoke(position + Vector3.up * 0.5f, radius * 1.1f, 2.5f);
            PlaySound(BoomClip, position, 1f, 220f, Random.Range(0.9f, 1.05f));
        }

        public static void Smoke(Vector3 position, float size = 1.5f, float life = 1.5f)
        {
            if (!Enabled) return;
            GameObject smoke = Blob(PrimitiveType.Sphere, position, size * 0.3f, new Color(0.25f, 0.25f, 0.25f, 0.6f), life);
            if (smoke == null) return;
            smoke.GetComponent<Renderer>().material = new Material(ColorMaterial(new Color(0.25f, 0.25f, 0.25f, 0.6f)));
            smoke.AddComponent<FxExpand>().Init(size, life);
        }

        public static void PlaySound(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 120f, float pitch = 1f)
        {
            if (!Enabled || clip == null) return;
            var go = new GameObject("SFX");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public static void PlaySound2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!Enabled || clip == null) return;
            var go = new GameObject("SFX2D");
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 0f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public static AudioClip ShotClip => _shot != null ? _shot : (_shot = MakeNoise("shot", 0.25f, 20f, 0.55f, 0.9f));
        public static AudioClip BoomClip => _boom != null ? _boom : (_boom = MakeNoise("boom", 1.6f, 3.2f, 0.06f, 1f));
        public static AudioClip ClickClip => _click != null ? _click : (_click = MakeNoise("click", 0.05f, 110f, 0.9f, 0.5f));
        public static AudioClip ReloadClip => _reload != null ? _reload : (_reload = MakeNoise("reload", 0.14f, 35f, 0.35f, 0.6f));
        public static AudioClip HitClip => _hit != null ? _hit : (_hit = MakeNoise("hit", 0.06f, 70f, 0.95f, 0.7f));

        /// <summary>Generates filtered white noise with an exponential decay envelope.</summary>
        static AudioClip MakeNoise(string name, float length, float decay, float lowpass, float volume)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * length);
            var data = new float[samples];
            var rng = new System.Random(name.GetHashCode());
            float y = 0f, peak = 0.0001f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                y += lowpass * (x - y);
                data[i] = y * Mathf.Exp(-t * decay);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float gain = volume / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;

            AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
