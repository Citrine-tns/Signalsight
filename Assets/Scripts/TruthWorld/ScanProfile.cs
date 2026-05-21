using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>センサ 1 種の走査ジオメトリ。</summary>
    [System.Serializable]
    public struct ScanProfile
    {
        [Tooltip("水平スラブの枚数。")]
        public int slabCount;
        [Tooltip("スラブの垂直間隔 [m]。")]
        public float slabSpacing;
        [Tooltip("中心スラブから 1 段ごとに付く仰角 [度]。0 で全スラブ水平。")]
        public float elevationStepDeg;
        [Tooltip("発射体の半径 [m]。レイ原点をこのぶん外側にずらし自己ヒットを防ぐ。")]
        public float emitterRadius;
    }
}
