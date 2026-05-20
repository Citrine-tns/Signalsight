using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>測距点を集約する共有バス。TruthWorld が発行し、再構成側が読む。</summary>
    public class SensorBus : MonoBehaviour
    {
        public static SensorBus Instance { get; private set; }

        readonly List<Measurement> _live = new List<Measurement>(8192);
        uint _nextSeq = 1;

        /// <summary>プレイヤーの世界 XZ。</summary>
        public Vector2 EgoPosition { get; private set; }

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

        public void SetEgoPosition(Vector2 worldXZ) => EgoPosition = worldXZ;

        public void Publish(Measurement m)
        {
            m.seq = _nextSeq++;
            m.timestamp = Time.timeAsDouble;
            _live.Add(m);
        }

        void LateUpdate()
        {
            // T_decay より古い測距点を破棄。
            double cutoff = Time.timeAsDouble - SensorConfig.TDecay;
            int w = 0;
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].timestamp >= cutoff) _live[w++] = _live[i];
            if (w < _live.Count) _live.RemoveRange(w, _live.Count - w);
        }
    }
}
