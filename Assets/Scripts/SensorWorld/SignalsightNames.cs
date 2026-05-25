using System.Collections.Generic;
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
            public const string Title = "Title";
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

        // 未定義レイヤ名の警告は初回のみ出す。爆発ごと・クリア演出ごとに同じ警告で
        // Console が溢れるのを防ぐ。Domain Reload でクリアされるので、修正後の再 Play で
        // 警告が再度出るリスクは無い。
        static readonly HashSet<string> _warnedMissingLayers = new();

        /// <summary>
        /// レイヤ名を index に解決する。未定義なら警告ログを出して（同名は初回のみ）
        /// false を返し、呼び出し側で skip できるようにする。`LayerMask.NameToLayer` の薄いラッパ。
        /// </summary>
        public static bool TryGetLayer(string name, out int layer)
        {
            layer = LayerMask.NameToLayer(name);
            if (layer < 0)
            {
                if (_warnedMissingLayers.Add(name))
                    Debug.LogWarning($"[SignalsightNames] Layer '{name}' is not defined in Project Settings → Tags and Layers.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// LayerMask が未設定（0）なら World レイヤだけ立った値で埋め、警告を出す。
        /// 各センサ系コンポーネントの Awake で「worldMask の貼り忘れ」フェイルセーフとして呼ぶ。
        /// 警告プレフィックスは <paramref name="owner"/> の型名になる。
        /// </summary>
        public static void EnsureWorldMask(ref LayerMask mask, Component owner)
        {
            if (mask != 0) return;
            if (!TryGetLayer(Layers.World, out int worldLayer)) return;
            mask = 1 << worldLayer;
            Debug.LogWarning(
                $"[{owner.GetType().Name}] worldMask 未設定だったため World レイヤを自動設定しました。Inspector で明示推奨。",
                owner);
        }
    }
}
