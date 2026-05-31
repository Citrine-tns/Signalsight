using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーが拾える/合成で使える「アイテム種類」の定義。
    /// ScriptableObject なので Project に Asset として並べて編集する。
    /// Beacon / Material / Relic の 3 カテゴリを共通スキーマで持つ。
    /// ビーコン固有データ（sensorId / scanProfile / prefab）は Phase 4 で
    /// BeaconKind SO に分離する想定。本クラスはアイテムとしての扱いに必要な
    /// 最小限の表示・識別データだけ持つ。
    /// </summary>
    [CreateAssetMenu(menuName = "Signalsight/Item Kind", fileName = "ItemKind")]
    public class ItemKind : ScriptableObject
    {
        public enum Category
        {
            Beacon,
            Material,
            Relic,
        }

        [Tooltip("セーブデータでの参照に使う一意な ID 文字列。半角英数とアンダースコア推奨。\n" +
                 "例: 'beacon_normal', 'material_iron_ore', 'relic_ancient_pendulum'")]
        [SerializeField] string id;

        [Tooltip("UI で表示する名前。日本語可。")]
        [SerializeField] string displayName;

        [SerializeField] Category category;

        [Tooltip("HUD・インベントリ・合成 UI で使う色。Beacon の場合は sensorId が指す色と揃えると統一感が出る。")]
        [SerializeField] Color color = Color.white;

        [Tooltip("HUD・インベントリで使うアイコン（未指定なら色付きの□で代替）。")]
        [SerializeField] Sprite icon;

        [Tooltip("Beacon カテゴリのとき参照する BeaconKind（配置時の挙動データ）。他カテゴリでは null のまま。")]
        [SerializeField] BeaconKind beaconKind;

        public string Id => id;
        public string DisplayName => displayName;
        public Category Cat => category;
        public Color Color => color;
        public Sprite Icon => icon;
        public BeaconKind BeaconKind => beaconKind;
    }
}
