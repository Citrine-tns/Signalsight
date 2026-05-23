using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// ゲームオーバー表示。EnemyAI / FallDeath から Trigger() で呼ぶ。シーンに事前配置は不要
    /// （初回 Trigger 時に自分で生成する）。R キーで Core から入り直す（リスタート）。
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        public static GameOverController Instance { get; private set; }
        bool _over;
        GUIStyle _titleStyle;
        GUIStyle _subStyle;

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
        /// 無敵フラグ。true の間は <see cref="Trigger"/> が無効化される
        /// （ステージクリア演出中の落下・爆発でゲームオーバーにしないため）。
        /// </summary>
        public static bool Invincible { get; private set; }

        /// <summary>無敵状態を切り替える。StageManager がクリア演出中に呼ぶ。</summary>
        public static void SetInvincible(bool value) => Invincible = value;

        /// <summary>ゲームオーバーを発動する（ゲームを停止し GAME OVER を表示）。無敵中は何もしない。</summary>
        public static void Trigger()
        {
            if (Invincible) return;
            if (Instance == null)
            {
                // シーンに居なければ自前生成（lazy）。AddComponent が Awake を駆動し Instance がセットされる。
                var go = new GameObject(nameof(GameOverController));
                go.AddComponent<GameOverController>();
            }
            if (Instance._over) return;
            Instance._over = true;
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (!_over) return;

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                // static フィールドは LoadScene を跨いで残るため、リスタート前に明示的に
                // 初期化しておく（無敵が居残ると次プレイで Trigger が効かなくなる）。
                Invincible = false;
                // Main Camera と StageManager は Core 側に常駐し、ステージはそこから
                // 追加ロードされる構成。アクティブシーンを単純に再読込するとカメラごと
                // 消えるので、必ず Core を Single モードで入り直す。
                SceneManager.LoadScene(SignalsightNames.Scenes.Core);
            }
        }

        void OnGUI()
        {
            if (!_over) return;

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle
                {
                    fontSize = 64,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _titleStyle.normal.textColor = Color.red;
            }
            if (_subStyle == null)
            {
                _subStyle = new GUIStyle
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _subStyle.normal.textColor = Color.red;
            }

            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "GAME OVER", _titleStyle);
            GUI.Label(new Rect(0f, Screen.height * 0.5f + 60f, Screen.width, 40f),
                "R でリスタート", _subStyle);
        }
    }
}
