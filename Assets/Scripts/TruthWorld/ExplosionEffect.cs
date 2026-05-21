using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>膨張しながらフェードする爆発エフェクト。終了で自壊する。</summary>
    public class ExplosionEffect : MonoBehaviour
    {
        float _duration = 0.5f;
        float _maxRadius = 3f;
        float _t;
        Material _mat;
        Color _color0;

        /// <summary>半径・継続時間・マテリアル・色を与えて再生を開始する。</summary>
        public void Play(float radius, float duration, Material material, Color color)
        {
            _maxRadius = radius;
            _duration = Mathf.Max(0.01f, duration);
            _mat = material;
            _color0 = color;
            if (_mat != null) _mat.SetColor("_Color", color);
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            // timeScale=0（ゲームオーバー）でも進むよう unscaled time を使う。
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / _duration);

            // 球プリミティブは scale 1 で直径 1 なので、半径 R には scale = 2R。
            transform.localScale = Vector3.one * (_maxRadius * 2f * k);
            if (_mat != null) _mat.SetColor("_Color", _color0 * (1f - k));

            if (k >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
