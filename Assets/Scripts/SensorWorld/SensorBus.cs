using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>測距点を集約する共有バス。TruthWorld が発行し、再構成側が読む。</summary>
    public class SensorBus : MonoBehaviour
    {
        public static SensorBus Instance { get; private set; }

        // 初期容量は RadarSimulator._pending と揃える。動的 resize による spike を避ける。
        readonly List<Measurement> _live = new(16384);

        /// <summary>有効な測距点（発行順）。</summary>
        public IReadOnlyList<Measurement> Live => _live;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Publish(Measurement m)
        {
            m.timestamp = Time.timeAsDouble;
            _live.Add(m);
        }

        /// <summary>全測距点を破棄する（ステージ切り替え時など）。</summary>
        public void Clear() => _live.Clear();

        void LateUpdate()
        {
            // 発行時刻から T_decay より古い測距点を破棄。発行＝波の到達時刻なので、
            // 「波が届く前」の点は RadarSimulator 側の保留キューに留まりここには来ない。
            double cutoff = Time.timeAsDouble - SensorConfig.TDecay;
            int w = 0;
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].timestamp >= cutoff) _live[w++] = _live[i];
            if (w < _live.Count) _live.RemoveRange(w, _live.Count - w);
        }
    }
}
