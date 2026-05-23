using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Signalsight.SensorWorld;

namespace Signalsight.Reconstruction
{
    /// <summary>
    /// SensorBus の測距点を 3D 点群として描画する。各測距点を、当たった世界座標に置いた
    /// カメラ正対のソフトな円ビルボードとして毎フレーム 1 つの動的メッシュに流し込む。
    /// </summary>
    public class RadarImageRenderer : MonoBehaviour
    {
        [Header("点群")]
        [Tooltip("測距点の描画サイズ（半径 [m]）。")]
        [SerializeField] float pointSize = 0.12f;
        [Tooltip("表示ゲイン。点が暗ければ上げる。")]
        [SerializeField] float brightness = 1.5f;
        [Tooltip("同時に描画する測距点の上限。")]
        [SerializeField] int maxPoints = 40000;
        [Tooltip("ビルボードの基準カメラ。未指定なら Camera.main。")]
        [SerializeField] Camera viewCamera;

        Mesh _mesh;
        Material _material;
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<int> _indices = new List<int>();

        void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            var shader = Shader.Find("Signalsight/RadarPoint");
            if (shader == null)
                Debug.LogError("[RadarImageRenderer] Shader 'Signalsight/RadarPoint' が見つかりません。");

            _mesh = new Mesh { name = "RadarPointCloud", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _material = new Material(shader);

            var go = new GameObject("RadarPointCloud");
            int layer = LayerMask.NameToLayer("RadarImage");
            if (layer >= 0) go.layer = layer;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = _material;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        void LateUpdate()
        {
            var bus = SensorBus.Instance;
            if (bus == null || _mesh == null) return;

            if (_material != null) _material.SetFloat("_Brightness", brightness);

            // カメラに正対させるための右・上ベクトル。
            Vector3 right = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            Vector3 up = viewCamera != null ? viewCamera.transform.up : Vector3.up;
            Vector3 rx = right * pointSize;
            Vector3 uy = up * pointSize;

            _verts.Clear();
            _colors.Clear();
            _uvs.Clear();
            _indices.Clear();

            double now = Time.timeAsDouble;
            var live = bus.Live;
            int start = Mathf.Max(0, live.Count - maxPoints);
            for (int i = start; i < live.Count; i++)
            {
                var m = live[i];
                float fade = 1f - (float)(now - m.timestamp) / SensorConfig.TDecay; // 鋸歯減衰
                if (fade <= 0f) continue;

                var center = new Vector3(m.hitPos.x, m.height, m.hitPos.y);
                Color col = SensorPalette.ColorOf(m.sensorId) * fade;

                int v = _verts.Count;
                _verts.Add(center - rx - uy);
                _verts.Add(center + rx - uy);
                _verts.Add(center + rx + uy);
                _verts.Add(center - rx + uy);
                _colors.Add(col); _colors.Add(col); _colors.Add(col); _colors.Add(col);
                _uvs.Add(new Vector2(0f, 0f));
                _uvs.Add(new Vector2(1f, 0f));
                _uvs.Add(new Vector2(1f, 1f));
                _uvs.Add(new Vector2(0f, 1f));
                _indices.Add(v); _indices.Add(v + 1); _indices.Add(v + 2);
                _indices.Add(v); _indices.Add(v + 2); _indices.Add(v + 3);
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetIndices(_indices, MeshTopology.Triangles, 0);
        }
    }
}
