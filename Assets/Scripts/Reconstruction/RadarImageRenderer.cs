using UnityEngine;
using UnityEngine.Rendering;
using Signalsight.SensorWorld;

namespace Signalsight.Reconstruction
{
    /// <summary>
    /// SensorBus の測距点を読み、世界高度ごとのスラブ像へ点群として描画する。
    /// 起動時にスラブ枚数ぶんの半透明クアッドを生成し、高さ方向に積層する。
    /// </summary>
    public class RadarImageRenderer : MonoBehaviour
    {
        [Header("像の範囲")]
        [Tooltip("1 スラブ像がカバーする世界の一辺の長さ [m]。")]
        [SerializeField] float worldExtent = 48f;

        [Header("表示の高さ範囲")]
        [Tooltip("表示スタックがカバーする世界高度の下端 [m]。")]
        [SerializeField] float displayMinHeight = 0f;
        [Tooltip("表示スタックがカバーする世界高度の上端 [m]。下端〜上端を段数で等分する。")]
        [SerializeField] float displayMaxHeight = 3f;

        [Header("点群")]
        [Tooltip("測距点の描画サイズ σ [m]。")]
        [SerializeField] float pointSigmaMeters = 0.15f;
        [Tooltip("表示ゲイン。点が暗ければ上げる。")]
        [SerializeField] float brightness = 1.5f;

        [Header("残像減衰")]
        [Tooltip("指数減衰の時定数 [s]。小さいほど速く消える。")]
        [SerializeField] float fadeTau = 0.25f;

        SlabImage[] _slabs;
        GaussianBrush _brush;
        Vector2 _origin;    // 像の世界アンカー（XZ）
        float _mpp;         // meters per pixel
        float _band;        // 1 段が表す世界高度 [m]
        int _res;
        uint _lastSeq;

        void Start()
        {
            _res = SensorConfig.GridResolution;
            _mpp = worldExtent / _res;
            _origin = new Vector2(transform.position.x, transform.position.z);
            _brush = new GaussianBrush(pointSigmaMeters / _mpp);
            _band = (displayMaxHeight - displayMinHeight) / Mathf.Max(1, SensorConfig.SlabCount);

            _slabs = new SlabImage[SensorConfig.SlabCount];
            int radarLayer = LayerMask.NameToLayer("RadarImage");
            var shader = Shader.Find("Signalsight/RadarSlab");
            if (shader == null)
                Debug.LogError("[RadarImageRenderer] Shader 'Signalsight/RadarSlab' が見つかりません。");

            for (int s = 0; s < _slabs.Length; s++)
            {
                _slabs[s] = new SlabImage(_res);

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Slab {s}";
                if (radarLayer >= 0) go.layer = radarLayer;
                Destroy(go.GetComponent<Collider>());

                var tr = go.transform;
                tr.SetParent(transform, false);
                tr.localRotation = Quaternion.Euler(90f, 0f, 0f); // 水平に寝かせる
                // クアッドはその段が表す世界高度（バンド中心）に置く。
                tr.localPosition = new Vector3(0f, displayMinHeight + (s + 0.5f) * _band, 0f);
                tr.localScale = Vector3.one * worldExtent;

                var mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var mat = new Material(shader);
                mat.mainTexture = _slabs[s].texture;
                mr.sharedMaterial = mat;
            }
        }

        void Update()
        {
            var bus = SensorBus.Instance;
            if (bus == null || _slabs == null) return;

            float fade = Mathf.Exp(-Time.deltaTime / Mathf.Max(1e-3f, fadeTau));
            for (int s = 0; s < _slabs.Length; s++) _slabs[s].Decay(fade);

            var live = bus.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var m = live[i];
                if (m.seq <= _lastSeq) continue;

                int layer = HeightToLayer(m.height);
                Vector2 px = WorldToPixel(m.hitPos);
                Color col = TxPalette.ColorOf(m.sensorId) * m.power;
                _slabs[layer].Splat(_brush, px.x, px.y, col);
            }
            if (live.Count > 0) _lastSeq = live[live.Count - 1].seq;

            for (int s = 0; s < _slabs.Length; s++) _slabs[s].Compose(brightness);
        }

        Vector2 WorldToPixel(Vector2 worldXZ)
        {
            return new Vector2(
                (worldXZ.x - _origin.x) / _mpp + _res * 0.5f,
                (worldXZ.y - _origin.y) / _mpp + _res * 0.5f);
        }

        /// <summary>世界高度 Y を高度バンドへ振り分けて表示レイヤー index にする。</summary>
        int HeightToLayer(float worldY)
        {
            int layer = Mathf.FloorToInt((worldY - displayMinHeight) / Mathf.Max(1e-4f, _band));
            return Mathf.Clamp(layer, 0, _slabs.Length - 1);
        }
    }
}
