using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// ゲームオーバー表示。EnemyAI から Trigger() で呼ぶ。シーンに事前配置は不要
    /// （初回 Trigger 時に自分で生成する）。R キーで現在シーンを再読み込みする。
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        static GameOverController _instance;
        bool _over;
        GUIStyle _style;

        /// <summary>ゲームオーバーを発動する（ゲームを停止し GAME OVER を表示）。</summary>
        public static void Trigger()
        {
            if (_instance == null)
            {
                var go = new GameObject("GameOverController");
                _instance = go.AddComponent<GameOverController>();
            }
            if (_instance._over) return;
            _instance._over = true;
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (!_over) return;

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        void OnGUI()
        {
            if (!_over) return;

            if (_style == null)
            {
                _style = new GUIStyle
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _style.normal.textColor = Color.red;
            }

            _style.fontSize = 64;
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "GAME OVER", _style);

            _style.fontSize = 24;
            GUI.Label(new Rect(0f, Screen.height * 0.5f + 60f, Screen.width, 40f),
                "R でリスタート", _style);
        }
    }
}
