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
        [Tooltip("（オルソ時）バーのサイズ [px]。X が幅、Y が高さ。")]
        [SerializeField] Vector2 size = new Vector2(14f, 90f);
        [Tooltip("（オルソ時）プレイヤー画面位置からのオフセット [px]。+X で右、+Y で下。")]
        [SerializeField] Vector2 offset = new Vector2(80f, 0f);
        [Tooltip("（1 人称時）バーのサイズ [px]。視認性のためオルソより大きめが推奨。")]
        [SerializeField] Vector2 firstPersonSize = new Vector2(36f, 240f);
        [Tooltip("（1 人称時）画面の正規化アンカー。0=左上, 1=右下。バー中心がここに来る。")]
        [SerializeField] Vector2 firstPersonAnchor = new Vector2(0.95f, 0.5f);
        [Tooltip("（1 人称時）アンカーからのピクセルオフセット。")]
        [SerializeField] Vector2 firstPersonOffset = Vector2.zero;
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

            float cx, cy;
            Vector2 barSize;
            var camCtrl = CameraController.Instance;
            bool firstPerson = camCtrl != null && camCtrl.CurrentMode == CameraController.Mode.FirstPerson;

            if (firstPerson)
            {
                // 1 人称：カメラ位置 ≒ プレイヤー位置で WorldToScreenPoint が不安定になるので、
                // 画面に対する固定位置にバーを置く。
                cx = firstPersonAnchor.x * Screen.width + firstPersonOffset.x;
                cy = firstPersonAnchor.y * Screen.height + firstPersonOffset.y;
                barSize = firstPersonSize;
            }
            else
            {
                // オルソ：プレイヤーの画面位置に追従させ、その横に出す。
                var cam = Camera.main;
                if (cam == null) return;
                Vector3 sp = cam.WorldToScreenPoint(pa.transform.position);
                if (sp.z < 0f) return;   // カメラ後方
                // OnGUI 座標系は y が上から下なので反転。
                cx = sp.x + offset.x;
                cy = (Screen.height - sp.y) + offset.y;
                barSize = size;
            }

            var bg = new Rect(cx - barSize.x * 0.5f, cy - barSize.y * 0.5f, barSize.x, barSize.y);
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
