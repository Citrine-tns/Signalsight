using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーの ping クールダウンを、プレイヤーの脇に小さな縦バーで可視化する。
    /// ping した瞬間に出現し、リチャージ中は下から上へ充填、満タン後 fadeOutGrace 秒で
    /// 不可視に戻す。プレイヤーにアタッチする。
    /// </summary>
    public class PingGauge : MonoBehaviour
    {
        [Tooltip("満タンになってから非表示にするまでの猶予 [s]。")]
        [SerializeField] float fadeOutGrace = 0.5f;
        [Tooltip("バーのサイズ [px]。X が幅、Y が高さ。")]
        [SerializeField] Vector2 size = new Vector2(14f, 90f);
        [Tooltip("プレイヤー画面位置からのオフセット [px]。+X で右、+Y で下。")]
        [SerializeField] Vector2 offset = new Vector2(80f, 0f);
        [Tooltip("背景色（満タン位置を視認できるよう、黒背景に溶けない色を）。")]
        [SerializeField] Color backgroundColor = new Color(0.35f, 0.05f, 0.05f, 0.85f);
        [Tooltip("充填部の色。")]
        [SerializeField] Color fillColor = Color.white;

        Texture2D _white;

        void OnDestroy()
        {
            if (_white != null) Destroy(_white);
        }

        void OnGUI()
        {
            var pa = PlayerActor.Instance;
            if (pa == null) return;
            float cooldown = pa.PingCooldown;
            if (cooldown <= 0f) return;

            float since = Time.time - pa.LastPingTime;
            if (since > cooldown + fadeOutGrace) return;   // 完全に非表示

            float fill = Mathf.Clamp01(since / cooldown);

            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(pa.transform.position);
            if (sp.z < 0f) return;   // カメラ後方

            // OnGUI 座標系は y が上から下なので反転。
            float cx = sp.x + offset.x;
            float cy = (Screen.height - sp.y) + offset.y;

            var bg = new Rect(cx - size.x * 0.5f, cy - size.y * 0.5f, size.x, size.y);
            DrawRect(bg, backgroundColor);

            // 充填部は下から上へ。
            float h = bg.height * fill;
            var fillRect = new Rect(bg.x, bg.y + bg.height - h, bg.width, h);
            DrawRect(fillRect, fillColor);
        }

        void DrawRect(Rect r, Color c)
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }
    }
}
