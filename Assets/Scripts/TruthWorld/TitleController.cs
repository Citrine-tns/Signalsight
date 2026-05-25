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
    /// 入力ロックの取り扱い：StageManager.EnterStage は Title シーンの場合に限り
    /// SignalsightInput.Locked=true のまま return する。TitleController はこのロックを
    /// 「他の入力（ping / 移動 / カメラ操作 / Beacon 再起動）を遮断する」目的でそのまま残し、
    /// 自分は Locked を見ずに Enter を直接読む（待機解除後の唯一の入力経路）。
    /// </summary>
    public class TitleController : MonoBehaviour
    {
        [Tooltip("シーン入場後、Enter を受け付け開始するまでの待機秒数。" +
                 "Beacon の auto-scan が SIGNALSIGHT を浮かび上がらせる頃合いに設定（既定 2s）。")]
        [SerializeField] float titleRevealDelay = 2f;

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
            while (true)
            {
                if (SignalsightInput.Player.Activate.WasPressedThisFrame()) break;
                yield return null;
            }

            // Phase 3: クリアシーケンスを起動。showText=false で「STAGE CLEAR」テキストは出さず、
            // World 公開（答え合わせ）+ celebrationDuration 待機 + Stage1 への EnterStage（banner 含む）が
            // チェーンで実行され、最終的に SignalsightInput.Locked=false に戻る。
            if (StageManager.Instance != null)
                StageManager.Instance.StageCleared(showText: false);
        }
    }
}
