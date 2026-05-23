using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>測距点を集約する共有バス。TruthWorld が発行し、再構成側が読む。</summary>
    public class SensorBus : MonoBehaviour
    {
        public static SensorBus Instance { get; private set; }

        readonly List<Measurement> _live = new List<Measurement>(8192);

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
            // 出現時刻（timestamp + delay）から T_decay より古い測距点を破棄。
            // 未出現（now < timestamp + delay）の点は将来出現するので残す。
            double cutoff = Time.timeAsDouble - SensorConfig.TDecay;
            int w = 0;
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].timestamp + _live[i].delay >= cutoff) _live[w++] = _live[i];
            if (w < _live.Count) _live.RemoveRange(w, _live.Count - w);
        }
    }
}
