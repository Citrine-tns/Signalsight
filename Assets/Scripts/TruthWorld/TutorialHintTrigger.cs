using System.Collections.Generic;
using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 指定の条件を満たした瞬間、画面にチュートリアルメッセージを表示する。
    /// 指定時間経過で末尾 0.5 秒かけてフェードアウトして自己破棄する。
    /// 発火条件：
    ///   - PlayerCollider … プレイヤーがトリガーに入った瞬間（GameObject に isTrigger Collider 必須）
    ///   - BeaconActivation … Beacon Target に指定した Beacon が起動した瞬間（Collider 不要）
    /// </summary>
    public class TutorialHintTrigger : MonoBehaviour
    {
        const float FadeDuration = 0.5f;

        public enum FireMode { PlayerCollider, BeaconActivation }

        [Header("発火条件")]
        [Tooltip("どのイベントで発火するか。")]
        [SerializeField] FireMode fireOn = FireMode.PlayerCollider;
        [Tooltip("FireOn = BeaconActivation の動作。指定したらその Beacon に限定、未指定（None）なら「シーン内のどれかが最初に起動したとき」発火。")]
        [SerializeField] Beacon beaconTarget;

        Beacon[] _cachedBeacons;
        bool _beaconsCached;

        // GameOver による Core 再ロードでは static は持ち越されるので、一度発火した
        // ヒントは再表示されない。プロセス再起動（Editor 停止→再 Play / アプリ再起動）
        // では static が初期化されるので自然にリセットされる。キーは「シーン名/GameObject名」。
        static readonly HashSet<string> _firedKeys = new HashSet<string>();
        string Key => gameObject.scene.name + "/" + gameObject.name;

        // 現在表示中のヒントは常に最大1つ。新しいヒントが Fire した時点で古いものは破棄。
        static TutorialHintTrigger _current;

        [Header("メッセージ")]
        [TextArea(1, 3)]
        [Tooltip("表示するメッセージ。短く 1〜2 行で。")]
        [SerializeField] string message = "ヒント";
        [Tooltip("表示時間 [s]。末尾 0.5 秒で自動フェードアウト。")]
        [SerializeField] float duration = 5f;

        [Header("レイアウト")]
        [Tooltip("画面上の表示位置（ボックス中心、0=左上、1=右下の正規化座標）。")]
        [SerializeField] Vector2 screenPosition = new Vector2(0.85f, 0.25f);
        [Tooltip("テキストボックスのサイズ [px]。")]
        [SerializeField] Vector2 size = new Vector2(400f, 60f);
        [Tooltip("フォントサイズ [pt]。")]
        [SerializeField] int fontSize = 40;
        [Tooltip("ボックス内のテキスト揃え。")]
        [SerializeField] TextAnchor textAnchor = TextAnchor.MiddleCenter;

        float _showAt = -1f;
        GUIStyle _style;

        void Awake()
        {
            // 過去のプレイで既に発火済みなら、復活させずに即破棄。
            if (_firedKeys.Contains(Key)) Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (fireOn != FireMode.PlayerCollider) return;
            if (!SignalsightRefs.IsPlayer(other)) return;
            Fire();
        }

        void Update()
        {
            // Update が動くのは BeaconActivation モードかつ未発火のときだけ。
            // 発火後は _showAt >= 0f で即 return するので、以下の監視ループは止まる。
            // （PlayerCollider モードはトリガーイベント駆動なので Update は不要。）
            if (fireOn != FireMode.BeaconActivation) return;
            if (_showAt >= 0f) return;

            if (beaconTarget != null)
            {
                if (beaconTarget.IsActive) Fire();
                return;
            }

            // Beacon 未指定：シーン内のどれかが起動するまで毎フレ監視（＝最初に起動したやつで発火）。
            // ループ自体は Fire() 後に Update 冒頭ガードで止まるので、永続走行にはならない。
            if (!_beaconsCached)
            {
                _cachedBeacons = FindObjectsByType<Beacon>(FindObjectsSortMode.None);
                _beaconsCached = true;
            }
            for (int i = 0; i < _cachedBeacons.Length; i++)
            {
                var b = _cachedBeacons[i];
                if (b != null && b.IsActive) { Fire(); return; }
            }
        }

        void Fire()
        {
            if (_showAt >= 0f) return;   // 既に表示中ならスルー
            _showAt = Time.time;
            _firedKeys.Add(Key);   // ステージリスタートを跨いで再発火しないよう記録。
            // 既に他のヒントが表示中ならそれを破棄して入れ替える。
            if (_current != null && _current != this) Destroy(_current.gameObject);
            _current = this;
            // Collider があれば以後の OnTriggerEnter を止める。
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        void OnGUI()
        {
            if (_showAt < 0f) return;
            float elapsed = Time.time - _showAt;
            if (elapsed > duration) { Destroy(gameObject); return; }

            // 寿命の末尾 FadeDuration を線形フェード。
            float alpha = elapsed > duration - FadeDuration
                ? Mathf.Max(0f, (duration - elapsed) / FadeDuration)
                : 1f;

            if (_style == null)
            {
                _style = new GUIStyle();
                _style.normal.textColor = Color.white;
            }
            _style.fontSize = fontSize;
            _style.alignment = textAnchor;

            float cx = screenPosition.x * Screen.width;
            float cy = screenPosition.y * Screen.height;
            var rect = new Rect(cx - size.x * 0.5f, cy - size.y * 0.5f, size.x, size.y);

            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, message, _style);
            GUI.color = old;
        }
    }
}
