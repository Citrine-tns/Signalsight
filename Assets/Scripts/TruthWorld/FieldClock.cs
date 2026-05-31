using System;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Field シーンに常駐するグローバル拍。全 PlacedBeacon と FieldClock 対応 Enemy が
    /// この Tick イベントを購読し、同じタイミングで RadarSimulator.Scan を発火する。
    /// プレイヤー ping は本クラスを介さず、押した瞬間に RadarSimulator.Scan を直接呼ぶ。
    ///
    /// 「同種類のビーコン 2 個を時間ずらして置くと実質高頻度ビーコンになる」抜け道を、
    /// 全ビーコンが共通の tick で同時発火することで封じる設計。Time.time (scaled) ベース
    /// なので timeScale=0 (banner などの freeze) で自然に止まる。
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class FieldClock : MonoBehaviour
    {
        public static FieldClock Instance { get; private set; }

        [Tooltip("Tick 周期 [s]。全ビーコン/エネミーがこの周期の整数倍で発火する。")]
        [SerializeField] float pulseInterval = 0.5f;

        /// <summary>Tick 発火時のイベント。引数は累積 tick 番号（0 から開始）。</summary>
        public event Action<int> OnTick;

        int _tickIndex = -1;     // 最初の Update で +1 されて 0 になる
        float _nextTickTime;

        /// <summary>累積 tick 番号。デバッグ・チェック用。</summary>
        public int TickIndex => _tickIndex;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _nextTickTime = Time.time + pulseInterval;
        }

        void Update()
        {
            // Time.time は scaled なので timeScale=0 中は進まない → 自然に Tick も止まる。
            if (Time.time < _nextTickTime) return;
            _nextTickTime += pulseInterval;
            _tickIndex++;
            OnTick?.Invoke(_tickIndex);
        }
    }
}
