using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>
    /// センサが全方位レイで測った 1 つの測距点。SensorBus の内部表現。
    /// 外部からは <see cref="SensorBus.Publish(Vector3, int)"/> で発行し、SensorBus 側が
    /// timestamp を立てる。
    /// </summary>
    public struct Measurement
    {
        public Vector3 hitPos;    // 世界座標のヒット点
        public int sensorId;      // 0 = プレイヤー, 1+ = ビーコン/エネミー
        public double timestamp;  // 発行時刻 = 波の到達時刻
    }
}
