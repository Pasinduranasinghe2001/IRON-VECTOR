using UnityEngine;

namespace IronVector
{
    /// <summary>Grows and fades a simple FX primitive (explosions, smoke).</summary>
    public class FxExpand : MonoBehaviour
    {
        float _targetSize;
        float _life;
        float _time;
        Vector3 _startScale;
        Renderer _renderer;
        Color _startColor;

        public void Init(float targetSize, float life)
        {
            _targetSize = targetSize;
            _life = Mathf.Max(0.01f, life);
            _startScale = transform.localScale;
            _renderer = GetComponent<Renderer>();
            if (_renderer != null) _startColor = _renderer.material.color;
        }

        void Update()
        {
            _time += Time.deltaTime;
            float k = Mathf.Clamp01(_time / _life);
            float eased = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.Lerp(_startScale, Vector3.one * _targetSize, eased);
            if (_renderer != null)
            {
                Color c = _startColor;
                c.a = _startColor.a * (1f - k);
                _renderer.material.color = c;
            }
        }
    }
}
