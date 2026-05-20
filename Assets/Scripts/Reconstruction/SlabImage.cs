using UnityEngine;

namespace Signalsight.Reconstruction
{
    /// <summary>事前計算したガウスブラシ。点の描画に使い回す。</summary>
    public sealed class GaussianBrush
    {
        public readonly int radius;
        public readonly int width;
        public readonly float[] weights;

        public GaussianBrush(float sigmaPx)
        {
            float sigma = Mathf.Max(0.5f, sigmaPx);
            radius = Mathf.Max(1, Mathf.CeilToInt(sigma * 2.5f));
            width = radius * 2 + 1;
            weights = new float[width * width];

            float inv2s2 = 1f / (2f * sigma * sigma);
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    weights[(dy + radius) * width + (dx + radius)] =
                        Mathf.Exp(-(dx * dx + dy * dy) * inv2s2);
        }
    }

    /// <summary>1 スラブ分の点群像。測距点を色つきで累積し、減衰させて Texture2D に書き出す。</summary>
    public class SlabImage
    {
        public readonly Texture2D texture;

        readonly int _res;
        readonly Color[] _accum;
        readonly Color32[] _pixels;

        public SlabImage(int res)
        {
            _res = res;
            int cells = res * res;
            _accum = new Color[cells];
            _pixels = new Color32[cells];
            texture = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "SlabImage",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        /// <summary>点群を一律に減衰させる（fade は毎フレームの乗算係数）。</summary>
        public void Decay(float fade)
        {
            for (int i = 0; i < _accum.Length; i++)
            {
                var c = _accum[i];
                _accum[i] = new Color(c.r * fade, c.g * fade, c.b * fade, 0f);
            }
        }

        /// <summary>測距点 1 つをガウスブラシで塗布する。</summary>
        public void Splat(GaussianBrush brush, float fx, float fy, Color color)
        {
            int cx = Mathf.RoundToInt(fx);
            int cy = Mathf.RoundToInt(fy);
            int r = brush.radius;
            int w = brush.width;
            var bw = brush.weights;

            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= _res) continue;
                int bRow = (dy + r) * w;
                int iRow = y * _res;

                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= _res) continue;

                    float g = bw[bRow + dx + r];
                    int idx = iRow + x;
                    var c = _accum[idx];
                    _accum[idx] = new Color(
                        c.r + color.r * g,
                        c.g + color.g * g,
                        c.b + color.b * g, 0f);
                }
            }
        }

        /// <summary>累積点群を Texture2D に書き出す。</summary>
        public void Compose(float brightness)
        {
            for (int i = 0; i < _pixels.Length; i++)
            {
                var c = _accum[i];
                _pixels[i] = new Color32(
                    ToByte(c.r * brightness),
                    ToByte(c.g * brightness),
                    ToByte(c.b * brightness),
                    255);
            }
            texture.SetPixels32(_pixels);
            texture.Apply(false);
        }

        static byte ToByte(float v) => (byte)(Mathf.Clamp01(v) * 255f);
    }
}
