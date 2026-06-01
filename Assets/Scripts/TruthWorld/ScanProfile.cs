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
        [Tooltip("水平 FOV [度]。0 か 360 以上で全方位（黄金角分散）、それ未満でビーコン前方の" +
                 "±FOV/2° 扇形に均一分散。Normal=45 で指向、Wide=360 で全周のような差を表現する。")]
        public float horizontalFovDeg;

        /// <summary>
        /// プレイヤー・ビーコン・敵が共有する「基本形」。仕様書 §2.2 に準拠。
        /// 各センサのフィールド初期化子から参照する。Inspector で個別調整は可能だが、
        /// 「共通既定」を変えるときはここを 1 箇所書き換えれば全センサに反映される。
        /// </summary>
        public static ScanProfile Default => new ScanProfile
        {
            slabCount = 10,
            slabSpacing = 0.20f,
            elevationStepDeg = 1f,
            emitterRadius = 0.3f,
        };
    }
}
