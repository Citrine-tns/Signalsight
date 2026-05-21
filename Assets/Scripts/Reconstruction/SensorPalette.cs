using UnityEngine;

namespace Signalsight.Reconstruction
{
    /// <summary>センサ ID ごとの表示色。</summary>
    public static class SensorPalette
    {
        public static Color ColorOf(int sensorId)
        {
            if (sensorId == 0) return new Color(0.30f, 0.90f, 1.00f); // プレイヤー: シアン

            // ビーコン・敵は色相環上に分散配置
            float hue = Mathf.Repeat(0.07f + 0.27f * (sensorId - 1), 1f);
            return Color.HSVToRGB(hue, 0.85f, 1f);
        }
    }
}
