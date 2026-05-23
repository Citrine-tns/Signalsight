using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>センサが全方位レイで測った 1 つの測距点。</summary>
    public struct Measurement
    {
        public Vector2 hitPos;    // 世界 XZ のヒット点（波が surface に到達した瞬間の位置）
        public float height;      // ヒットの世界高さ Y
        public int sensorId;      // 0 = プレイヤー, 1+ = ビーコン・敵
        public double timestamp;  // 発行時刻＝波の到達時刻
    }
}
