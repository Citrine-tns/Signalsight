using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Signalsight.SensorWorld;

namespace Signalsight.Reconstruction
{
    /// <summary>
    /// SensorBus の測距点を 3D 点群として描画する。1 点 = 1 PointData を ComputeBuffer に
    /// 詰めて GPU に渡し、頂点シェーダが SV_VertexID から 4 角形に展開する。CPU は
    /// billboard 展開・fade 計算・色決定を一切行わない（すべて shader 側で実施）。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RadarImageRenderer : MonoBehaviour
    {
        const int MaxSensorColors = 16;
        const int VerticesPerQuad = 6;   // 2 triangles

        [Header("点群")]
        [Tooltip("測距点の描画サイズ（半径 [m]）。")]
        [SerializeField] float pointSize = 0.12f;
        [Tooltip("表示ゲイン。点が暗ければ上げる。")]
        [SerializeField] float brightness = 1.5f;
        [Tooltip("同時に描画する測距点の上限。Start で確保した後は固定。")]
        [SerializeField] int maxPoints = 40000;
        [Tooltip("ビルボードの基準カメラ。未指定なら SignalsightRefs.Camera を使用。")]
        [SerializeField] Camera viewCamera;

        // C# 側と HLSL 側で stride 20 byte（Vector3 + int + float）。順序も合わせる。
        [StructLayout(LayoutKind.Sequential)]
        struct PointData
        {
            public Vector3 worldPos;
            public int sensorId;
            public float timestampRel;
        }

        Mesh _mesh;
        Material _material;
        ComputeBuffer _pointBuffer;
        PointData[] _points;
        // 中央参照から Start で 1 回キャッシュ。
        SensorBus _bus;
        // Time.timeAsDouble をそのまま float に渡すと長時間プレイで精度が落ちるので
        // 起動時刻を引いた相対秒で渡す。
        double _epochTime;

        static readonly int IdPoints = Shader.PropertyToID("_Points");
        static readonly int IdPointSize = Shader.PropertyToID("_PointSize");
        static readonly int IdBrightness = Shader.PropertyToID("_Brightness");
        static readonly int IdNow = Shader.PropertyToID("_Now");
        static readonly int IdTDecay = Shader.PropertyToID("_TDecay");
        static readonly int IdSensorColors = Shader.PropertyToID("_SensorColors");
        static readonly int IdCamRight = Shader.PropertyToID("_CamRight");
        static readonly int IdCamUp = Shader.PropertyToID("_CamUp");

        void Start()
        {
            // Reconstruction → TruthWorld 依存禁止のため、CameraController を直接参照せず
            // SensorWorld の中央レジストリ経由で受け取る。
            if (viewCamera == null) viewCamera = SignalsightRefs.Camera;
            _bus = SensorBus.Instance;
            _epochTime = Time.timeAsDouble;

            var shader = Shader.Find(SignalsightNames.Shaders.RadarPoint);
            if (shader == null)
            {
                Debug.LogError($"[RadarImageRenderer] Shader '{SignalsightNames.Shaders.RadarPoint}' が見つかりません。");
                return;
            }
            _material = new Material(shader);

            // GPU バッファと CPU 側コピー（毎フレーム SetData する元）。
            _points = new PointData[maxPoints];
            _pointBuffer = new ComputeBuffer(maxPoints, Marshal.SizeOf<PointData>());
            _material.SetBuffer(IdPoints, _pointBuffer);

            // SensorBus 側の List 容量も同じ上限に合わせ、ピーク時の動的 resize を回避する。
            // 最大同時保持点数のハードリミットは描画側の maxPoints で律速されるため、
            // この値をシステム共通の上限として SensorBus に push する。
            if (_bus != null) _bus.EnsureCapacity(maxPoints);

            // 起動時に 1 度だけ送る uniform
            _material.SetVectorArray(IdSensorColors, SensorPalette.GetGpuColors(MaxSensorColors));
            _material.SetFloat(IdTDecay, SensorConfig.TDecay);
            // Inspector 値で起動後不変な uniform は Start で 1 回 + OnValidate で edit-time 追従。
            ApplyStaticUniforms();

            // ダミーメッシュ。頂点位置は使われず（shader が SV_VertexID から導出する）、
            // インデックスは 0..N-1 を並べただけ。bounds は十分大きくしてフラスタムカリング回避。
            _mesh = new Mesh { name = "RadarPointCloud", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            int totalVerts = maxPoints * VerticesPerQuad;
            var dummyVerts = new Vector3[totalVerts];
            var indices = new int[totalVerts];
            for (int i = 0; i < totalVerts; i++) indices[i] = i;
            _mesh.SetVertices(dummyVerts);
            _mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            var go = new GameObject("RadarPointCloud");
            if (SignalsightNames.TryGetLayer(SignalsightNames.Layers.RadarImage, out int layer))
                go.layer = layer;

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
            _pointBuffer?.Dispose();
            _pointBuffer = null;
        }

        void OnValidate()
        {
            // Play 中の Inspector 編集に追従。_material 未生成（edit-time）なら no-op。
            if (_material != null) ApplyStaticUniforms();
        }

        // pointSize / brightness は Inspector 値で起動後不変なので毎フレ SetFloat は不要。
        void ApplyStaticUniforms()
        {
            _material.SetFloat(IdPointSize, pointSize);
            _material.SetFloat(IdBrightness, brightness);
        }

        void LateUpdate()
        {
            if (_bus == null || _material == null || _pointBuffer == null) return;

            // カメラ右・上ベクトルをワールド系で渡す。shader 側で billboard 展開に使う。
            Vector3 camRight = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            Vector3 camUp = viewCamera != null ? viewCamera.transform.up : Vector3.up;
            _material.SetVector(IdCamRight, camRight);
            _material.SetVector(IdCamUp, camUp);
            _material.SetFloat(IdNow, (float)(Time.timeAsDouble - _epochTime));

            // SensorBus の最新測距点を maxPoints 件まで PointData として詰める。
            // LiveCount/LiveAt の具象 API 経由で、IReadOnlyList の仮想呼び出しを避ける。
            int liveCount = _bus.LiveCount;
            int start = Mathf.Max(0, liveCount - maxPoints);
            int count = 0;
            for (int i = start; i < liveCount; i++)
            {
                var m = _bus.LiveAt(i);
                _points[count++] = new PointData
                {
                    worldPos = m.hitPos,
                    sensorId = m.sensorId,
                    timestampRel = (float)(m.timestamp - _epochTime),
                };
            }
            if (count > 0) _pointBuffer.SetData(_points, 0, 0, count);

            // 描画する三角形数を現フレームのアクティブ点数に合わせる。
            // Mesh.Clear() は使わず、SubMesh の range だけを書き換えることで GPU バッファ再確保を避ける。
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0,
                new SubMeshDescriptor(0, count * VerticesPerQuad, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds
                    | MeshUpdateFlags.DontValidateIndices
                    | MeshUpdateFlags.DontResetBoneBounds
                    | MeshUpdateFlags.DontNotifyMeshUsers);
        }
    }
}
