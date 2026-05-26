using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>測距点を集約する共有バス。TruthWorld が発行し、再構成側が読む。</summary>
    public class SensorBus : MonoBehaviour
    {
        public static SensorBus Instance { get; private set; }

        // 動的 resize による spike を避けるため、起動時に十分な容量を持たせる。
        // 最終的な capacity は RadarImageRenderer の maxPoints に合わせて
        // EnsureCapacity で押し上げられる（最大同時保持点数のハードリミットに揃える）。
        readonly List<Measurement> _live = new(16384);

        /// <summary>
        /// 有効な測距点の件数。ホットパス用に LiveCount + LiveAt のペアで提供する
        /// （IReadOnlyList で公開すると indexer が interface 経由になり仮想呼び出しが入る）。
        /// </summary>
        public int LiveCount => _live.Count;

        /// <summary>
        /// 有効な測距点を index で取り出す。List&lt;T&gt; の具象 indexer なので JIT インライン可能。
        /// </summary>
        public Measurement LiveAt(int index) => _live[index];

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 測距点を 1 件発行する。timestamp は SensorBus 側で `Time.timeAsDouble` を立てるため
        /// 呼び出し側は座標とセンサ ID だけ渡せばよい（誰が発行時刻を決めるかを API 形で明示）。
        /// </summary>
        public void Publish(Vector3 hitPos, int sensorId)
        {
            _live.Add(new Measurement
            {
                hitPos = hitPos,
                sensorId = sensorId,
                timestamp = Time.timeAsDouble,
            });
        }

        /// <summary>全測距点を破棄する（ステージ切り替え時など）。</summary>
        public void Clear() => _live.Clear();

        /// <summary>
        /// 内部リストの確保済み容量を最低 <paramref name="capacity"/> まで引き上げる。
        /// RadarImageRenderer.Start から自身の maxPoints で呼び、ピーク時の resize スパイクを抑える。
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            if (_live.Capacity < capacity) _live.Capacity = capacity;
        }

        void LateUpdate()
        {
            // 発行時刻から T_decay より古い測距点を破棄。発行＝波の到達時刻なので、
            // 「波が届く前」の点は RadarSimulator 側の保留キューに留まりここには来ない。
            //
            // Publish は常に Time.timeAsDouble を timestamp にセットして末尾追加するので、
            // _live は timestamp 昇順で並んでいる。よって先頭から「cutoff 以上」になる
            // 最初の位置までを一括削除すれば足りる。
            double cutoff = Time.timeAsDouble - SensorConfig.TDecay;
            int drop = 0;
            while (drop < _live.Count && _live[drop].timestamp < cutoff) drop++;
            if (drop > 0) _live.RemoveRange(0, drop);
        }
    }
}
