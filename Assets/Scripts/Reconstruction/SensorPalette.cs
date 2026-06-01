using UnityEngine;

namespace Signalsight.Reconstruction
{
    /// <summary>センサ ID ごとの表示色。</summary>
    public static class SensorPalette
    {
        // 種類数を母数として色相を等分する。RadarImageRenderer.Start で Configure される。
        // 既定はビーコン 4 種・敵 1 種で、種類が増えたら Inspector で母数を上げて Configure し直す。
        static int s_beaconCount = 4;
        static int s_enemyCount = 1;

        /// <summary>
        /// 色相分散の母数を設定する。ビーコン帯は <paramref name="beaconCount"/> 等分、
        /// 敵帯は <paramref name="enemyCount"/> 等分。種類が増えたら Inspector から再 Configure する。
        /// </summary>
        public static void Configure(int beaconCount, int enemyCount)
        {
            s_beaconCount = Mathf.Max(1, beaconCount);
            s_enemyCount = Mathf.Max(1, enemyCount);
        }

        public static Color ColorOf(int sensorId)
        {
            if (sensorId == 0) return new Color(0.30f, 0.90f, 1.00f); // プレイヤー: シアン

            if (sensorId >= 16)
            {
                // 敵帯 (16..23): 赤〜オレンジを s_enemyCount 等分。
                int local = (sensorId - 16) % s_enemyCount;
                float hue = s_enemyCount == 1
                    ? 0.03f   // 1 種類なら帯の中央寄り（赤）
                    : Mathf.Lerp(0.0f, 0.10f, local / (float)(s_enemyCount - 1));
                return Color.HSVToRGB(hue, 0.85f, 1f);
            }

            // ビーコン帯 (1..15): 黄〜紫を s_beaconCount 等分。
            int blocal = (sensorId - 1) % s_beaconCount;
            float bhue = s_beaconCount == 1
                ? 0.5f
                : Mathf.Lerp(0.15f, 0.85f, blocal / (float)(s_beaconCount - 1));
            return Color.HSVToRGB(bhue, 0.85f, 1f);
        }

        /// <summary>シェーダの float4 配列 uniform に渡すための GPU 用色テーブル。</summary>
        public static Vector4[] GetGpuColors(int count)
        {
            var arr = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                Color c = ColorOf(i);
                arr[i] = new Vector4(c.r, c.g, c.b, c.a);
            }
            return arr;
        }
    }
}
