using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーが拾って置く Field 上のビーコン。FieldClock の Tick を購読して
    /// kindMultiplier ごとに RadarSimulator.Scan を発火する。
    ///
    /// 旧 Beacon との違い:
    ///   - 起動入力 (Activate) はなし。インスタンス化された時点で稼働扱い
    ///   - 自前タイマーを持たず FieldClock 同期 → 抜け道なし
    ///   - sensorId は「ビーコンの種類」を表す固定値（Phase 4 で BeaconKind SO に集約予定）
    /// </summary>
    public class PlacedBeacon : MonoBehaviour
    {
        [Tooltip("このビーコンの kind（sensorId / scanProfile / tickMultiplier 等を提供）。" +
                 "Prefab 化したビーコンは BeaconKind 参照を持ち、複製しても本 SO の値で挙動が決まる。")]
        [SerializeField] BeaconKind kind;

        /// <summary>BeaconPlacementController が同種ビーコン距離チェックに使う。</summary>
        public BeaconKind Kind => kind;

        // 中央参照から Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        RadarSimulator _simulator;

        void Start()
        {
            _simulator = RadarSimulator.Instance;
        }

        void OnEnable()
        {
            // FieldClock は DefaultExecutionOrder=-300 で先に Awake するため、
            // 通常 PlacedBeacon.OnEnable 時点で Instance は確立済み。null ガードは
            // FieldClock が無い Stage1/2 で誤って配置された場合の保護。
            if (FieldClock.Instance != null) FieldClock.Instance.OnTick += HandleTick;
        }

        void OnDisable()
        {
            // FieldClock が先に Destroy 済みなら Instance=null。その場合は何もしない
            // （イベント自体が消えるので unsubscribe 不要）。
            if (FieldClock.Instance != null) FieldClock.Instance.OnTick -= HandleTick;
        }

        void HandleTick(int tickIndex)
        {
            if (_simulator == null || kind == null) return;
            // tickMultiplier=1 なら毎 tick、2 以上なら整数倍の tick だけ発火。
            // 「同種ビーコンは同じ tickMultiplier」を前提に、複数置いても全部同タイミングで撃つ。
            if (kind.TickMultiplier > 1 && tickIndex % kind.TickMultiplier != 0) return;
            _simulator.Scan(transform.position, transform.rotation, kind.SensorId, kind.ScanProfile);
        }
    }
}
