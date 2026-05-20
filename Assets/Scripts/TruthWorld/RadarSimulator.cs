using Unity.Collections;
using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>1 つのセンサから全方位へレイを撃ち、ヒット点を測距点として SensorBus に発行する。</summary>
    public class RadarSimulator : MonoBehaviour
    {
        [Header("レイ照射")]
        [Tooltip("散乱体（壁・床・人体）のレイヤだけを含めること。プレイヤー/ビーコンは除外。")]
        [SerializeField] LayerMask worldMask = ~0;
        [Tooltip("1 スラブあたりのレイ本数（全方位の角度分解能）。")]
        [SerializeField] int raysPerSlab = 240;
        [SerializeField] float maxRayDistance = 60f;

        // 黄金角（ラジアン）。レイを低不一致に分散させる。
        const float GoldenAngleRad = 2.39996322972865332f;

        /// <summary>sensorPos からスラブごとに全方位レイを撃ち、ヒット点を発行する。</summary>
        public void Scan(Vector3 sensorPos, int sensorId)
        {
            var bus = SensorBus.Instance;
            if (bus == null) return;

            int rays = Mathf.Max(1, raysPerSlab);
            int total = SensorConfig.SlabCount * rays;
            var qp = new QueryParameters(worldMask, false, QueryTriggerInteraction.Ignore, false);

            var commands = new NativeArray<RaycastCommand>(total, Allocator.TempJob);
            var hits = new NativeArray<RaycastHit>(total, Allocator.TempJob);

            for (int s = 0; s < SensorConfig.SlabCount; s++)
            {
                // スラブはセンサの高さを中心に身長範囲で上下に積む。
                float y = sensorPos.y + SensorConfig.SlabHeight(s) - SensorConfig.PlayerHeight * 0.5f;
                var origin = new Vector3(sensorPos.x, y, sensorPos.z);
                int baseIdx = s * rays;
                for (int r = 0; r < rays; r++)
                {
                    float a = r * GoldenAngleRad;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    commands[baseIdx + r] = new RaycastCommand(origin, dir, qp, maxRayDistance);
                }
            }

            RaycastCommand.ScheduleBatch(commands, hits, 64, 1, default).Complete();

            for (int s = 0; s < SensorConfig.SlabCount; s++)
            {
                int baseIdx = s * rays;
                for (int r = 0; r < rays; r++)
                {
                    var hit = hits[baseIdx + r];
                    if (hit.collider == null) continue;

                    // レイは水平なので hit.point.y ＝ スラブの世界高度。
                    bus.Publish(new Measurement
                    {
                        hitPos = new Vector2(hit.point.x, hit.point.z),
                        height = hit.point.y,
                        power = 1f,
                        sensorId = sensorId,
                    });
                }
            }

            commands.Dispose();
            hits.Dispose();
        }
    }
}
