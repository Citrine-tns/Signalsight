using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 遺構の収集数を ProgressFlags 経由で集計し、targetCount に達したらクリア演出を出す。
    /// FieldPickup が遺構を拾ったときに ProgressFlags.Set(FlagPrefix + kind.Id) する。
    /// 「集めた数」は ProgressFlags の中身で表現されるので、Phase 9 のセーブで自動的に永続化される。
    /// </summary>
    public class RelicCounter : MonoBehaviour
    {
        public static RelicCounter Instance { get; private set; }
        public const string FlagPrefix = "relic_";

        [Tooltip("クリアに必要な遺構の数。プランの 5〜7 を想定。")]
        [SerializeField] int targetCount = 5;

        int _collected;
        bool _allClear;
        GUIStyle _clearStyle;
        GUIStyle _statusStyle;

        public int Collected => _collected;
        public int Target => targetCount;
        public bool IsAllClear => _allClear;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            if (ProgressFlags.Instance != null)
                ProgressFlags.Instance.OnFlagSet += HandleFlagSet;
            RecountFromCurrentFlags();
        }

        void OnDisable()
        {
            if (ProgressFlags.Instance != null)
                ProgressFlags.Instance.OnFlagSet -= HandleFlagSet;
        }

        /// <summary>シーン遷移後等にも集計を直す（ProgressFlags 側で既存フラグから拾い直し）。</summary>
        void RecountFromCurrentFlags()
        {
            _collected = 0;
            if (ProgressFlags.Instance == null) return;
            foreach (var f in ProgressFlags.Instance.Flags)
                if (f.StartsWith(FlagPrefix)) _collected++;

            if (_collected >= targetCount) _allClear = true;
        }

        void HandleFlagSet(string flag)
        {
            if (!flag.StartsWith(FlagPrefix)) return;
            _collected++;
            if (_collected >= targetCount && !_allClear) _allClear = true;
        }

        void OnGUI()
        {
            EnsureStyles();

            // 右上に「遺構: N / M」
            string status = $"遺構: {_collected} / {targetCount}";
            GUI.Label(new Rect(Screen.width - 220f, 20f, 200f, 30f), status, _statusStyle);

            // 達成時は中央に大きく "ALL CLEAR"
            if (_allClear)
                GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "ALL CLEAR", _clearStyle);
        }

        void EnsureStyles()
        {
            if (_clearStyle != null) return;
            _clearStyle = new GUIStyle
            {
                fontSize = 80, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _clearStyle.normal.textColor = Color.cyan;

            _statusStyle = new GUIStyle { fontSize = 18, alignment = TextAnchor.MiddleRight };
            _statusStyle.normal.textColor = Color.white;
        }
    }
}
