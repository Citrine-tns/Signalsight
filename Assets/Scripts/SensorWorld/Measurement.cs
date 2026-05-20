using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>センサが全方位レイで測った 1 つの測距点。</summary>
    public struct Measurement
    {
        public uint seq;          // 発行順の通し番号
        public Vector2 hitPos;    // 世界 XZ のヒット点
        public float height;      // ヒットの世界高さ Y
        public float power;       // 戻り強度
        public int sensorId;      // 0 = プレイヤー, 1.. = ビーコン
        public double timestamp;  // 走査時刻
    }
}
