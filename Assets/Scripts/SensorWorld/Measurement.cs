using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>センサが全方位レイで測った 1 つの測距点。</summary>
    public struct Measurement
    {
        public Vector2 hitPos;    // 世界 XZ のヒット点
        public float height;      // ヒットの世界高さ Y
        public int sensorId;      // 0 = プレイヤー, 1+ = ビーコン・敵
        public double timestamp;  // 走査時刻
        public float delay;       // 走査時刻からの出現遅延 [s]（ヒット距離 ÷ レーダ伝播速度）
    }
}
