using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>WASD / スティックで移動、Space / 北ボタンでジャンプ。CharacterController 駆動。</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移動")]
        [SerializeField] float moveSpeed = 4f;
        [SerializeField] float gravity = 20f;
        [Tooltip("ジャンプの最高到達高 [m]。")]
        [SerializeField] float jumpHeight = 1.2f;

        [Header("視点基準")]
        [Tooltip("移動方向の基準（通常はカメラ）。未指定なら SignalsightRefs.Camera を使う。")]
        [SerializeField] Transform viewTransform;

        CharacterController _cc;
        float _verticalVelocity;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        void Start()
        {
            // viewTransform 未指定なら中央レジストリの Camera を採用。
            // Awake で publish される SignalsightRefs.Camera を Start で参照することで
            // シーン読込順のばらつきを回避する。
            if (viewTransform == null && SignalsightRefs.Camera != null)
                viewTransform = SignalsightRefs.Camera.transform;
        }

        void Update()
        {
            // Locked 中も重力・CharacterController.Move は走らせる（足元が抜けないため）。
            // 入力（Move / Jump）の読みだけをロック中ゼロ扱いする。
            // 同一フレで複数アクションを読むので Player マップを 1 度だけ取得する
            // （`SignalsightInput.Player` は呼ぶたび PlayerActions 構造体を new するため）。
            bool inputEnabled = !SignalsightInput.Locked;
            var input = SignalsightInput.Player;

            Vector2 move = inputEnabled
                ? Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f)
                : Vector2.zero;

            // カメラの向きを水平面へ投影した基準（right はロール無しなら常に水平）。
            Vector3 right = viewTransform != null ? viewTransform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            Vector3 forward = Vector3.Cross(right, Vector3.up);

            Vector3 dir = right * move.x + forward * move.y;

            // プレイヤー本体を移動方向に向ける（移動中のみ即時更新）。停止中は最後の向きを維持。
            // CameraController.ResetLook の FirstPerson 分岐が player.rotation.y を読むので、
            // Z で 1 人称に戻したとき / 次ステージ入場時のカメラ初期視線が「直近の進行方向」に揃う。
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            // 接地中のみジャンプ可、それ以外は重力で落下。
            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (inputEnabled && input.Jump.WasPressedThisFrame())
                    _verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
            _verticalVelocity -= gravity * Time.deltaTime;

            Vector3 velocity = dir * moveSpeed;
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);
        }
    }
}
