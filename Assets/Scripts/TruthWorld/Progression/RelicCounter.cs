using UnityEngine;
using UnityEngine.UIElements;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 「一度でも入手した固有遺構の数」を集計する。クリア条件は「マップに点在する 5〜7 個の
    /// 遺構を全部入手すること」で、合成で消費しても入手歴はそのまま残る。Inventory 表示
    /// （現在所持数）とは独立して動く: 合成しなければ両者は一致、合成したら数字がズレる。
    ///
    /// ストアは ProgressFlags の <see cref="FlagPrefix"/> + 各 FieldPickup の per-pickup id。
    /// per-pickup id で記録するので、同じ ItemKind を持つ別個の遺構（マップに 5 個点在）
    /// が個別にカウントされる。Phase 9 のセーブで永続化される。
    ///
    /// 描画は UIDocument + RelicCounter.uxml。Inspector でこの GameObject に
    /// UIDocument コンポーネントをアタッチし、Source Asset に RelicCounter.uxml を、
    /// Panel Settings に SignalsightPanelSettings を割り当てる。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class RelicCounter : MonoBehaviour
    {
        public static RelicCounter Instance { get; private set; }

        /// <summary>「遺構を入手済み」フラグの prefix。FieldPickup が relic_&lt;pickup.id&gt; を Set する。</summary>
        public const string FlagPrefix = "relic_";

        [Tooltip("クリアに必要な遺構の数。プランの 5〜7 を想定。")]
        [SerializeField] int targetCount = 5;

        int _collected;
        bool _allClear;

        // UI Toolkit 要素キャッシュ。OnEnable で UIDocument.rootVisualElement から取得する。
        UIDocument _doc;
        Label _statusLabel;
        Label _allClearLabel;

        public int Collected => _collected;
        public int Target => targetCount;
        public bool IsAllClear => _allClear;

        // Field シーンの load/unload に追従して UI を見せ隠しする際の最後の値。
        // 毎フレ Set すると不要 dirty が出るので変化時のみ反映する。
        bool _lastVisible = true;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _doc = GetComponent<UIDocument>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            // UIDocument は OnEnable で rootVisualElement を組み立てる。Awake では取れないので
            // 必ず OnEnable 以降で Q<T>() する。
            var root = _doc.rootVisualElement;
            _statusLabel = root.Q<Label>("status");
            _allClearLabel = root.Q<Label>("all-clear");

            if (ProgressFlags.Instance != null)
                ProgressFlags.Instance.OnFlagSet += HandleFlagSet;
            RecountFromFlags();
            RefreshUI();
        }

        void OnDisable()
        {
            if (ProgressFlags.Instance != null)
                ProgressFlags.Instance.OnFlagSet -= HandleFlagSet;
        }

        void Update()
        {
            // Title 等の Field 外シーンでは UI を隠す。GameObject は Core 常駐なので
            // FieldManager.Instance の有無 (= Field シーンが現在 load されているか) を signal にする。
            bool visible = FieldManager.Instance != null;
            if (visible == _lastVisible) return;
            _lastVisible = visible;
            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root != null)
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// シーン遷移・セーブロード後にも ProgressFlags から数え直す。
        /// 「一度立てたら下がらない」「フラグは per-pickup id で一意」が保証されるので、
        /// 合成で遺構を消費しても _collected は減らない。
        /// </summary>
        void RecountFromFlags()
        {
            _collected = 0;
            if (ProgressFlags.Instance != null)
            {
                foreach (var f in ProgressFlags.Instance.Flags)
                    if (f.StartsWith(FlagPrefix)) _collected++;
            }
            // ALL CLEAR は flag ベースなのでスティッキー（一度立ったら消えない）。
            // 合成で遺構を全部消費しても ALL CLEAR は維持される。
            if (_collected >= targetCount) _allClear = true;
        }

        void HandleFlagSet(string flag)
        {
            if (!flag.StartsWith(FlagPrefix)) return;
            _collected++;
            if (_collected >= targetCount) _allClear = true;
            RefreshUI();
        }

        /// <summary>
        /// UI の文字列と ALL CLEAR の表示を現在の状態に合わせる。
        /// OnGUI と違って per-frame で呼ぶ必要はなく、状態変化時のみ呼べばよい。
        /// </summary>
        void RefreshUI()
        {
            if (_statusLabel != null)
                _statusLabel.text = $"遺構: {_collected} / {targetCount}";

            if (_allClearLabel != null)
            {
                // class の付け外しで display を切り替える。Theme.uss の transition を将来追加すれば
                // フェードインなどもここを触らず実現できる。
                if (_allClear) _allClearLabel.AddToClassList("rc-all-clear--visible");
                else _allClearLabel.RemoveFromClassList("rc-all-clear--visible");
            }
        }
    }
}
