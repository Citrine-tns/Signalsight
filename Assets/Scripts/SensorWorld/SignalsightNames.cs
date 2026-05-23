using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>
    /// プロジェクト全体で参照するシーン名・レイヤ名・シェーダ名の中央定数と、
    /// それらを安全に解決するヘルパー。Unity Editor 側でリネームしたらここを
    /// 1 箇所書き換える＝全コードが追従する。
    /// </summary>
    public static class SignalsightNames
    {
        public static class Scenes
        {
            public const string Core = "Core";
        }

        public static class Layers
        {
            public const string World = "World";         // 散乱体（壁・床・階段・敵・未起動ビーコン）
            public const string Marker = "Marker";       // 常時可視（プレイヤー・起動済みビーコン・爆発）
            public const string RadarImage = "RadarImage"; // 点群メッシュ
        }

        public static class Shaders
        {
            public const string RadarPoint = "Signalsight/RadarPoint";
            public const string Explosion = "Signalsight/Explosion";
        }

        /// <summary>
        /// レイヤ名を index に解決する。未定義なら警告ログを出して false を返し、
        /// 呼び出し側で skip できるようにする。`LayerMask.NameToLayer` の薄いラッパ。
        /// </summary>
        public static bool TryGetLayer(string name, out int layer)
        {
            layer = LayerMask.NameToLayer(name);
            if (layer < 0)
            {
                Debug.LogWarning($"[Signalsight] Layer '{name}' is not defined in Project Settings → Tags and Layers.");
                return false;
            }
            return true;
        }
    }
}
