namespace Signalsight.SensorWorld
{
    /// <summary>SensorWorld / TruthWorld 共通の定数。</summary>
    public static class SensorConfig
    {
        public const int SlabCount = 30;          // 水平スラブ枚数
        public const float PlayerHeight = 1.7f;   // m
        public const float TDecay = 1.0f;         // s（残像減衰時間）
        public const int GridResolution = 256;    // グリッド解像度（1 辺）

        /// <summary>スラブ間隔 [m]。</summary>
        public static float SlabSpacing => PlayerHeight / SlabCount;

        /// <summary>スラブ index の足元基準高度 [m]。0..SlabCount-1 が 0..PlayerHeight を等分する。</summary>
        public static float SlabHeight(int slab) => PlayerHeight * (slab + 0.5f) / SlabCount;
    }
}
