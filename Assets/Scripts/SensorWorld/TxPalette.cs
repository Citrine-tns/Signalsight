using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>センサ ID ごとの表示色。</summary>
    public static class TxPalette
    {
        public static Color ColorOf(int txId)
        {
            if (txId == 0) return new Color(0.30f, 0.90f, 1.00f); // プレイヤー: シアン

            // ビーコンは色相環上に分散配置
            float hue = Mathf.Repeat(0.07f + 0.27f * (txId - 1), 1f);
            return Color.HSVToRGB(hue, 0.85f, 1f);
        }
    }
}
