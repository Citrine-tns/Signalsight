using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// PlacedBeacon の挙動データ（sensorId / ScanProfile / tickMultiplier / 配置制限 / Prefab）を
    /// 集約する SO。PlacedBeacon Prefab は本 SO への参照を 1 個だけ持ち、BeaconKind を編集すれば
    /// 全 PlacedBeacon インスタンスの挙動が一括変更される。
    /// ItemKind の Beacon カテゴリは対応する BeaconKind を 1 個参照する。
    /// </summary>
    [CreateAssetMenu(menuName = "Signalsight/Beacon Kind", fileName = "BeaconKind")]
    public class BeaconKind : ScriptableObject
    {
        [Tooltip("対応する ItemKind（拾える/所持できる形態）。インベントリ表示・回収判定で使う。")]
        [SerializeField] ItemKind itemKind;

        [Tooltip("センサ ID（1〜15）。色と一意に対応する。同じ kind の複数インスタンスは同じ ID/色を共有。")]
        [SerializeField] int sensorId = 1;

        [Tooltip("スキャンの放射プロファイル（スラブ数・仰角・送信半径など）。")]
        [SerializeField] ScanProfile scanProfile = ScanProfile.Default;

        [Tooltip("FieldClock の何 tick ごとに発火するか。1=毎 tick (高頻度)、2=1 つ飛ばし、4=4 つ飛ばし (低頻度)。")]
        [SerializeField] int tickMultiplier = 1;

        [Tooltip("このビーコンはコア（拠点核）か。Field 内に 1 個までの制限を受ける。")]
        [SerializeField] bool isCore;

        [Tooltip("BeaconPlacementController が Instantiate する PlacedBeacon Prefab。")]
        [SerializeField] GameObject prefab;

        public ItemKind ItemKind => itemKind;
        public int SensorId => sensorId;
        public ScanProfile ScanProfile => scanProfile;
        public int TickMultiplier => tickMultiplier;
        public bool IsCore => isCore;
        public GameObject Prefab => prefab;
    }
}
