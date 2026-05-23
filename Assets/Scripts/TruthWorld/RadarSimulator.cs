using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 1 つのセンサから全方位へレイを撃ち、レーダ波が surface に追いついた瞬間に
    /// ヒット点を SensorBus に発行する。全センサ（プレイヤー / ビーコン / 敵）で共有。
    ///
    /// Scan はレイバッチを非同期スケジュールするだけで即 return し、main thread を
    /// ブロックしない。ワーカーが結果を埋め終わった batch を LateUpdate で回収し、
    /// ヒットを保留キューへ積む。
    ///
    /// 保留キューでは毎フレーム、「scan からの経過時間 × propagationSpeed」が
    /// 「センサ原点と surface 現在位置の距離」以上になった瞬間、つまり広がる球面が
    /// 動く surface に追いついた瞬間に発行する。発行位置は surface のそのときの
    /// 現在位置を使うので、動く対象でもタイミングと位置が同じ物理事象を指す。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RadarSimulator : MonoBehaviour
    {
        public static RadarSimulator Instance { get; private set; }

        [Header("レイ照射")]
        [Tooltip("散乱体（壁・床・人体）のレイヤだけを含めること。プレイヤー/ビーコンは除外。未設定なら Awake で World レイヤを自動セット。")]
        [SerializeField] LayerMask worldMask;
        [Tooltip("1 スラブあたりのレイ本数（全方位の角度分解能、最大 " + nameof(MaxRaysPerSlab) + "）。")]
        [SerializeField] int raysPerSlab = 240;
        [SerializeField] float maxRayDistance = 60f;
        [Tooltip("レーダ波の見かけ伝播速度 [m/s]。波が surface に追いついた時点で測距点を発行する。")]
        [SerializeField] float propagationSpeed = 60f;

        // 黄金角（ラジアン）。レイを低不一致に分散させる。
        const float GoldenAngleRad = 2.39996322972865332f;

        // 波より速く逃げる surface など、いつまでも追いつかない pending を捨てる係数。
        // 「停止していたときに想定される最大遅延」の何倍を超えたら諦めるか。
        const float MaxWaitFactor = 1.5f;

        // stackalloc で確保する cos/sin テーブルの上限。これ以上は stack overflow リスクが出る。
        const int MaxRaysPerSlab = 1024;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct PendingHit
        {
            public Transform transform;   // 8 byte
            public double scanTime;       // 8 byte
            public Vector3 localPoint;    // 12 byte
            public Vector3 sensorOrigin;  // 12 byte
            public int sensorId;          // 4 byte
            // 計 44 byte（Pack=4 で末尾 padding なし）
        }

        struct InFlightBatch
        {
            public JobHandle handle;
            public NativeArray<RaycastCommand> commands;
            public NativeArray<RaycastHit> hits;
            public Vector3 sensorPos;
            public int sensorId;
            public double scanTime;
            public int total;
        }

        readonly List<PendingHit> _pending = new(16384);
        readonly List<InFlightBatch> _inFlight = new(8);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (worldMask == 0 && SignalsightNames.TryGetLayer(SignalsightNames.Layers.World, out int worldLayer))
            {
                worldMask = 1 << worldLayer;
                Debug.LogWarning($"[{GetType().Name}] worldMask 未設定だったため World レイヤを自動設定しました。Inspector で明示推奨。", this);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // NativeArray を取りこぼさないよう in-flight を強制 drain。
            for (int i = 0; i < _inFlight.Count; i++)
            {
                _inFlight[i].handle.Complete();
                _inFlight[i].commands.Dispose();
                _inFlight[i].hits.Dispose();
            }
            _inFlight.Clear();
        }

        /// <summary>保留中と処理中のものをすべて破棄する（ステージ切り替え時など）。</summary>
        public void ClearPending()
        {
            for (int i = 0; i < _inFlight.Count; i++)
            {
                _inFlight[i].handle.Complete();
                _inFlight[i].commands.Dispose();
                _inFlight[i].hits.Dispose();
            }
            _inFlight.Clear();
            _pending.Clear();
        }

        /// <summary>
        /// sensorPos から profile に従ってスラブごとにレイを撃つジョブを非同期スケジュール
        /// する。結果の回収・発行は LateUpdate で行う。
        /// </summary>
        public void Scan(Vector3 sensorPos, Quaternion sensorRot, int sensorId, ScanProfile profile)
        {
            int slabCount = Mathf.Max(1, profile.slabCount);
            int rays = Mathf.Clamp(raysPerSlab, 1, MaxRaysPerSlab);
            int total = slabCount * rays;
            var qp = new QueryParameters(worldMask, false, QueryTriggerInteraction.Ignore, false);

            var commands = new NativeArray<RaycastCommand>(total, Allocator.TempJob);
            var hits = new NativeArray<RaycastHit>(total, Allocator.TempJob);

            // 水平角の cos / sin は ray index にしか依存しないので 1 度だけ計算して全 slab で使い回す。
            // stackalloc なので GC アロケなし。
            Span<float> cosA = stackalloc float[rays];
            Span<float> sinA = stackalloc float[rays];
            for (int r = 0; r < rays; r++)
            {
                float a = r * GoldenAngleRad;
                cosA[r] = Mathf.Cos(a);
                sinA[r] = Mathf.Sin(a);
            }

            // スラブはセンサのローカル上方向に積み、中心から離れるほどレイに仰角が付く（扇型放射）。
            Vector3 upWorld = sensorRot * Vector3.up;
            float mid = (slabCount - 1) * 0.5f;
            for (int s = 0; s < slabCount; s++)
            {
                float vOffset = (s - mid) * profile.slabSpacing;
                var origin = sensorPos + upWorld * vOffset;

                float elevRad = (s - mid) * profile.elevationStepDeg * Mathf.Deg2Rad;
                float cosE = Mathf.Cos(elevRad);
                float sinE = Mathf.Sin(elevRad);

                int baseIdx = s * rays;
                for (int r = 0; r < rays; r++)
                {
                    var localDir = new Vector3(cosA[r] * cosE, sinE, sinA[r] * cosE);
                    var worldDir = sensorRot * localDir;
                    // 発射体の半径ぶん原点を外へずらし、自己ヒットを防ぐ。
                    var rayOrigin = origin + worldDir * profile.emitterRadius;
                    commands[baseIdx + r] = new RaycastCommand(rayOrigin, worldDir, qp, maxRayDistance);
                }
            }

            // 非同期スケジュール。Complete はせず、_inFlight に積んで LateUpdate で回収する。
            var handle = RaycastCommand.ScheduleBatch(commands, hits, 64, 1, default);
            _inFlight.Add(new InFlightBatch
            {
                handle = handle,
                commands = commands,
                hits = hits,
                sensorPos = sensorPos,
                sensorId = sensorId,
                scanTime = Time.timeAsDouble,
                total = total,
            });
        }

        void LateUpdate()
        {
            ProcessCompletedBatches();
            EmitArrivedWaves();
        }

        /// <summary>完了済みの batch を引き取り、ヒットを保留キューへ移す。</summary>
        void ProcessCompletedBatches()
        {
            if (_inFlight.Count == 0) return;

            int w = 0;
            for (int i = 0; i < _inFlight.Count; i++)
            {
                var b = _inFlight[i];
                if (!b.handle.IsCompleted)
                {
                    _inFlight[w++] = b;
                    continue;
                }
                b.handle.Complete(); // すでに done なので no-op。Safety system に明示する目的。

                for (int j = 0; j < b.total; j++)
                {
                    var hit = b.hits[j];
                    if (hit.collider == null) continue;
                    var t = hit.collider.transform;
                    _pending.Add(new PendingHit
                    {
                        transform = t,
                        scanTime = b.scanTime,
                        localPoint = t.InverseTransformPoint(hit.point),
                        sensorOrigin = b.sensorPos,
                        sensorId = b.sensorId,
                    });
                }
                b.commands.Dispose();
                b.hits.Dispose();
            }
            if (w < _inFlight.Count) _inFlight.RemoveRange(w, _inFlight.Count - w);
        }

        /// <summary>波が surface に追いついた保留点を bus に発行する。</summary>
        void EmitArrivedWaves()
        {
            if (_pending.Count == 0) return;
            var bus = SensorBus.Instance;
            if (bus == null) return;

            double now = Time.timeAsDouble;
            float speed = Mathf.Max(propagationSpeed, 0.01f);
            float maxWait = maxRayDistance / speed * MaxWaitFactor;

            int w = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];

                // collider が破棄されていれば捨てる。
                if (p.transform == null) continue;

                float elapsed = (float)(now - p.scanTime);

                // 波より速く逃げて永久に追いつかれない場合のセーフティ。
                if (elapsed > maxWait) continue;

                Vector3 worldPos = p.transform.TransformPoint(p.localPoint);
                Vector3 diff = worldPos - p.sensorOrigin;
                float currentDistSqr = diff.sqrMagnitude;
                float waveRadius = elapsed * speed;
                float waveRadiusSqr = waveRadius * waveRadius;

                if (waveRadiusSqr < currentDistSqr)
                {
                    // 波がまだ届いていない。次フレームに再判定。
                    _pending[w++] = p;
                    continue;
                }

                // 波が surface に追いついた。surface の現在位置で発行する。
                bus.Publish(new Measurement
                {
                    hitPos = new Vector2(worldPos.x, worldPos.z),
                    height = worldPos.y,
                    sensorId = p.sensorId,
                });
            }
            if (w < _pending.Count) _pending.RemoveRange(w, _pending.Count - w);
        }
    }
}
