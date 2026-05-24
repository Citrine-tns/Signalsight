using UnityEngine;
using UnityEngine.InputSystem;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>WASD / スティックで移動、Space / 北ボタンでジャンプ。CharacterController 駆動。</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 4f;
        [SerializeField] float gravity = 20f;
        [Tooltip("ジャンプの最高到達高 [m]。")]
        [SerializeField] float jumpHeight = 1.2f;
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
            Vector2 move = Vector2.zero;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
            }

            var gp = Gamepad.current;
            if (gp != null) move += gp.leftStick.ReadValue();

            move = Vector2.ClampMagnitude(move, 1f);

            // カメラの向きを水平面へ投影した基準（right はロール無しなら常に水平）。
            Vector3 right = viewTransform != null ? viewTransform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            Vector3 forward = Vector3.Cross(right, Vector3.up);

            Vector3 dir = right * move.x + forward * move.y;

            // 接地中のみジャンプ可、それ以外は重力で落下。
            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (JumpPressed())
                    _verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
            _verticalVelocity -= gravity * Time.deltaTime;

            Vector3 velocity = dir * moveSpeed;
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);
        }

        static bool JumpPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonNorth.wasPressedThisFrame) return true;
            return false;
        }
    }
}
