using System.Collections;
using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Title シーン専用のコントローラ。シーン入場 → 一定時間待機（Beacon の自動 scan で
    /// SIGNALSIGHT 文字が浮かび上がる時間）→ Enter 受付 → Enter 押下で StageCleared を起動
    /// → World 公開（答え合わせ）→ Stage1 へ、という流れを管理する。
    ///
    /// 配置：Title シーンに 1 個だけ置く GameObject にアタッチ。Beacon は別途
    /// startActive=true で配置し、自動 scan で SIGNALSIGHT を浮かび上がらせる役割を担う。
    ///
    /// 入力ロックの取り扱い：BootManager は Title ロード時に SignalsightInput.Locked=true を
    /// 立てたままにする。TitleController はこのロックを「他の入力（ping / 移動 / カメラ操作 /
    /// Beacon 再起動）を遮断する」目的でそのまま残し、自分は Locked を見ずに Enter を直接読む
    /// （待機解除後の唯一の入力経路）。Field 遷移時に BootManager 側で Locked=false に戻る。
    /// </summary>
    public class TitleController : MonoBehaviour
    {
        [Tooltip("シーン入場後、Enter を受け付け開始するまでの待機秒数。" +
                 "Beacon の auto-scan が SIGNALSIGHT を浮かび上がらせる頃合いに設定（既定 2s）。")]
        [SerializeField] float titleRevealDelay = 2f;

        [Header("プロンプト表示")]
        [Tooltip("Phase 2（Enter 受付中）に画面に出すテロップ。")]
        [SerializeField] string prompt = "ENTER でゲーム開始";
        [Tooltip("プロンプトのフォントサイズ [pt]。")]
        [SerializeField] int promptFontSize = 36;
        [Tooltip("画面の正規化位置（0=左上, 1=右下）。0.5, 0.8 で中央下寄り。")]
        [SerializeField] Vector2 promptScreenPosition = new Vector2(0.5f, 0.8f);

        // Phase 2 のあいだだけ true。OnGUI がこれを見てプロンプトを描画するか決める。
        bool _acceptingInput;
        // GUIStyle は OnGUI ごとに new するとアロケが出るので初回 OnGUI で 1 度だけ作って保持。
        GUIStyle _promptStyle;

        void Start()
        {
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            // Phase 1: Beacon の自動 scan で文字が浮かび上がるまで待つ。
            // 待機中も SignalsightInput.Locked=true なので、ping / 移動 / カメラ操作は全て封じられている。
            float elapsed = 0f;
            while (elapsed < titleRevealDelay) { elapsed += Time.deltaTime; yield return null; }

            // Phase 2: Enter のみ受付。SignalsightInput.Locked は見ない（規約 [[Locked]] の意図的例外）。
            // 他の入力コンシューマは Locked を尊重して停止中なので、Enter だけが通る状態になる。
            // _acceptingInput を立てて OnGUI に「ENTER でゲーム開始」プロンプトを出させる。
            _acceptingInput = true;
            while (true)
            {
                if (SignalsightInput.Player.Activate.WasPressedThisFrame()) break;
                yield return null;
            }
            _acceptingInput = false;

            // Phase 3: Field シーンへ遷移。BootManager.SwapScene が unload→bus clear→load→teleport を行い、
            // 最終的に SignalsightInput.Locked=false に戻る（演出・banner はなし）。
            if (BootManager.Instance != null)
                BootManager.Instance.EnterField();
        }

        void OnGUI()
        {
            if (!_acceptingInput) return;

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle
                {
                    fontSize = promptFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _promptStyle.normal.textColor = Color.white;
            }

            // 正規化位置を実ピクセル化。Rect は中心 (cx, cy) に MiddleCenter 揃えで描画。
            float cx = promptScreenPosition.x * Screen.width;
            float cy = promptScreenPosition.y * Screen.height;
            GUI.Label(new Rect(cx - 200f, cy - 25f, 400f, 50f), prompt, _promptStyle);
        }
    }
}
