using Unity.Collections;
using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 1 つのセンサから全方位へレイを撃ち、ヒット点を測距点として SensorBus に発行する。
    /// 全センサ（プレイヤー / ビーコン / 敵）が共有する単一インスタンス。
    /// </summary>
    public class RadarSimulator : MonoBehaviour
    {
        public static RadarSimulator Instance { get; private set; }

        [Header("レイ照射")]
        [Tooltip("散乱体（壁・床・人体）のレイヤだけを含めること。プレイヤー/ビーコンは除外。")]
        [SerializeField] LayerMask worldMask = ~0;
        [Tooltip("1 スラブあたりのレイ本数（全方位の角度分解能）。")]
        [SerializeField] int raysPerSlab = 240;
        [SerializeField] float maxRayDistance = 60f;
        [Tooltip("レーダ波の見かけ伝播速度 [m/s]。各測距点は ヒット距離 / 速度 だけ遅れて出現する。")]
        [SerializeField] float propagationSpeed = 60f;

        // 黄金角（ラジアン）。レイを低不一致に分散させる。
        const float GoldenAngleRad = 2.39996322972865332f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// sensorPos から profile に従ってスラブごとにレイを撃ち、ヒット点を発行する。
        /// スラブ面・レイ方向は sensorRot に従う（センサを傾ければ斜めにスキャンする）。
        /// </summary>
        public void Scan(Vector3 sensorPos, Quaternion sensorRot, int sensorId, ScanProfile profile)
        {
            var bus = SensorBus.Instance;
            if (bus == null) return;

            int slabCount = Mathf.Max(1, profile.slabCount);
            int rays = Mathf.Max(1, raysPerSlab);
            int total = slabCount * rays;
            var qp = new QueryParameters(worldMask, false, QueryTriggerInteraction.Ignore, false);

            var commands = new NativeArray<RaycastCommand>(total, Allocator.TempJob);
            var hits = new NativeArray<RaycastHit>(total, Allocator.TempJob);

            // 中心スラブを 0 とした相対 index。スラブはセンサのローカル上方向に積み、
            // 中心から離れるほどレイに仰角が付く（扇型放射）。
            float mid = (slabCount - 1) * 0.5f;
            for (int s = 0; s < slabCount; s++)
            {
                float vOffset = (s - mid) * profile.slabSpacing;
                var origin = sensorPos + sensorRot * (Vector3.up * vOffset);

                float elevRad = (s - mid) * profile.elevationStepDeg * Mathf.Deg2Rad;
                float cosE = Mathf.Cos(elevRad);
                float sinE = Mathf.Sin(elevRad);

                int baseIdx = s * rays;
                for (int r = 0; r < rays; r++)
                {
                    float a = r * GoldenAngleRad;
                    var localDir = new Vector3(Mathf.Cos(a) * cosE, sinE, Mathf.Sin(a) * cosE);
                    var worldDir = sensorRot * localDir;
                    // 発射体の半径ぶん原点を外へずらし、自己ヒットを防ぐ。
                    var rayOrigin = origin + worldDir * profile.emitterRadius;
                    commands[baseIdx + r] = new RaycastCommand(rayOrigin, worldDir, qp, maxRayDistance);
                }
            }

            RaycastCommand.ScheduleBatch(commands, hits, 64, 1, default).Complete();

            // 0 除算を避けつつ、ヒット距離 ÷ 伝播速度 を出現遅延として持たせる。
            float invSpeed = 1f / Mathf.Max(propagationSpeed, 0.01f);

            for (int s = 0; s < slabCount; s++)
            {
                int baseIdx = s * rays;
                for (int r = 0; r < rays; r++)
                {
                    var hit = hits[baseIdx + r];
                    if (hit.collider == null) continue;

                    bus.Publish(new Measurement
                    {
                        hitPos = new Vector2(hit.point.x, hit.point.z),
                        height = hit.point.y,
                        sensorId = sensorId,
                        delay = hit.distance * invSpeed,
                    });
                }
            }

            commands.Dispose();
            hits.Dispose();
        }
    }
}
