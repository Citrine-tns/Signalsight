namespace Signalsight.SensorWorld
{
    /// <summary>
    /// プロジェクト全体で参照するシーン名・レイヤ名・シェーダ名の中央定数。
    /// Unity Editor 側でリネームしたらここを 1 箇所書き換える＝全コードが追従する。
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
    }
}
