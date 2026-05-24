using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>膨張しながらフェードする爆発エフェクト。終了で自壊する。</summary>
    public class ExplosionEffect : MonoBehaviour
    {
        // 全爆発で共有する Material（Shader.Find は初回のみ）。Domain Reload で消えても
        // Unity の overload された == が destroyed object を null 扱いするので再生成できる。
        static Material _sharedMaterial;
        static readonly int IdColor = Shader.PropertyToID("_Color");

        /// <summary>
        /// 共有 Material を返す（初回のみ生成）。Shader が見つからなければ null。
        /// 呼び出し側はこの null を見て「視覚エフェクトをスキップ」できる。
        /// </summary>
        public static Material GetSharedMaterial()
        {
            if (_sharedMaterial == null)
            {
                var shader = Shader.Find(SignalsightNames.Shaders.Explosion);
                if (shader == null)
                {
                    Debug.LogError($"[ExplosionEffect] Shader '{SignalsightNames.Shaders.Explosion}' が見つかりません。");
                    return null;
                }
                _sharedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            return _sharedMaterial;
        }

        float _duration = 0.5f;
        float _maxRadius = 3f;
        float _t;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        Color _color0;

        /// <summary>半径・継続時間・対象レンダラ・色を与えて再生を開始する。</summary>
        public void Play(float radius, float duration, MeshRenderer renderer, Color color)
        {
            _maxRadius = radius;
            _duration = Mathf.Max(0.01f, duration);
            _renderer = renderer;
            _color0 = color;
            _block = new MaterialPropertyBlock();
            transform.localScale = Vector3.zero;
            ApplyColor(color);
        }

        void Update()
        {
            // timeScale=0（ゲームオーバー）でも進むよう unscaled time を使う。
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / _duration);

            // 球プリミティブは scale 1 で直径 1 なので、半径 R には scale = 2R。
            transform.localScale = Vector3.one * (_maxRadius * 2f * k);
            ApplyColor(_color0 * (1f - k));

            if (k >= 1f) Destroy(gameObject);
        }

        void ApplyColor(Color c)
        {
            if (_renderer == null || _block == null) return;
            _block.SetColor(IdColor, c);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
