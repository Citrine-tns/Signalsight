using UnityEngine;
using UnityEngine.InputSystem;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// カメラの全挙動（yaw / pitch / モード切替 / 位置）を一括で担当する。
    /// Player の transform には何もせず、毎 LateUpdate で player.transform.position
    /// を基準に world 空間でカメラの位置と回転を確定する（Player の非一様 scale や
    /// 親子関係に依存しない）。
    /// 切替は Z キー（キーボード）または Select / View ボタン（Gamepad）でサイクル。
    /// Main Camera の GameObject にアタッチする。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public enum Mode { TopDownOrtho, FirstPerson }

        [Header("初期モード")]
        [SerializeField] Mode initialMode = Mode.TopDownOrtho;

        [Header("見下ろしオルソ（base 姿勢は Start で Player 基準で取得）")]
        [Tooltip("オルソ投影のサイズ。")]
        [SerializeField] float orthoSize = 10f;

        [Header("1 人称")]
        [Tooltip("プレイヤー位置からのカメラ高さオフセット [m]。0 でちょうど player.transform.position。")]
        [SerializeField] float firstPersonHeight = 1.5f;
        [SerializeField] float firstPersonFov = 90f;

        [Header("Yaw 入力（左右視点）")]
        [Tooltip("キー / スティック全倒し時の yaw 速度 [度/秒]。")]
        [SerializeField] float keyYawDegPerSec = 60f;
        [Tooltip("マウスの yaw 感度 [度/px]。")]
        [SerializeField] float mouseYawSensitivity = 0.2f;

        [Header("Pitch 入力（上下視点）")]
        [Tooltip("キー / スティック全倒し時の pitch 速度 [度/秒]。")]
        [SerializeField] float keyPitchDegPerSec = 90f;
        [Tooltip("マウスの pitch 感度 [度/px]。")]
        [SerializeField] float mousePitchSensitivity = 0.2f;
        [SerializeField] float minPitchDeg = -80f;
        [SerializeField] float maxPitchDeg = 80f;

        Camera _camera;
        Mode _mode;
        float _yawDeg;
        float _pitchDeg;

        // ortho の中立姿勢（Scene の初期カメラ姿勢）。Player を原点としたローカル空間で保持する。
        Vector3 _orthoOffsetPlayerLocal;
        Quaternion _orthoRotPlayerLocal;
        bool _orthoBaseCaptured;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _mode = initialMode;
        }

        void Start()
        {
            CaptureOrthoBase();
            ApplyMode();
        }

        /// <summary>
        /// Scene にセットされたカメラの初期位置・回転を、Player を原点としたローカル空間に
        /// 変換して保存。以後 ortho モードでは現在の yaw を掛け直して再現する。
        /// </summary>
        void CaptureOrthoBase()
        {
            var player = PlayerActor.Instance;
            if (player == null)
            {
                Debug.LogError("[CameraController] PlayerActor.Instance 未登録のため ortho 基準姿勢を取得できません。", this);
                return;
            }
            Vector3 worldOffset = transform.position - player.transform.position;
            Quaternion playerRotInv = Quaternion.Inverse(player.transform.rotation);
            _orthoOffsetPlayerLocal = playerRotInv * worldOffset;
            _orthoRotPlayerLocal = playerRotInv * transform.rotation;
            _orthoBaseCaptured = true;
        }

        void Update()
        {
            if (CycleModePressed())
            {
                _mode = (Mode)(((int)_mode + 1) % 2);
                ApplyMode();
            }

            float yawDelta = 0f;
            float pitchDelta = 0f;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) yawDelta -= keyYawDegPerSec * Time.deltaTime;
                if (kb.rightArrowKey.isPressed) yawDelta += keyYawDegPerSec * Time.deltaTime;
                if (kb.upArrowKey.isPressed) pitchDelta -= keyPitchDegPerSec * Time.deltaTime;
                if (kb.downArrowKey.isPressed) pitchDelta += keyPitchDegPerSec * Time.deltaTime;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 rs = gp.rightStick.ReadValue();
                yawDelta += rs.x * keyYawDegPerSec * Time.deltaTime;
                pitchDelta -= rs.y * keyPitchDegPerSec * Time.deltaTime;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 md = mouse.delta.ReadValue();
                yawDelta += md.x * mouseYawSensitivity;
                pitchDelta -= md.y * mousePitchSensitivity;
            }

            _yawDeg += yawDelta;
            if (Mathf.Abs(pitchDelta) > 0f)
                _pitchDeg = Mathf.Clamp(_pitchDeg + pitchDelta, minPitchDeg, maxPitchDeg);
        }

        void ApplyMode()
        {
            switch (_mode)
            {
                case Mode.TopDownOrtho:
                    _camera.orthographic = true;
                    _camera.orthographicSize = orthoSize;
                    break;
                case Mode.FirstPerson:
                    _camera.orthographic = false;
                    _camera.fieldOfView = firstPersonFov;
                    break;
            }
            UpdatePlayerVisibility();
        }

        void LateUpdate()
        {
            var player = PlayerActor.Instance;
            if (player == null) return;

            Vector3 playerPos = player.transform.position;
            Quaternion yawRot = Quaternion.Euler(0f, _yawDeg, 0f);
            Quaternion pitchRot = Quaternion.Euler(_pitchDeg, 0f, 0f);

            switch (_mode)
            {
                case Mode.TopDownOrtho:
                    if (!_orthoBaseCaptured) break;
                    // yaw / pitch 共に player.position を中心としたオービタル回転。
                    // 同じ orbit を offset にも rotation にも掛けるので「常にプレイヤーを
                    // 見たまま」位置と向きが一緒に回り込む。
                    Quaternion orbit = yawRot * pitchRot;
                    transform.SetPositionAndRotation(
                        playerPos + orbit * _orthoOffsetPlayerLocal,
                        orbit * _orthoRotPlayerLocal);
                    break;

                case Mode.FirstPerson:
                    // 1 人称はカメラ位置はプレイヤーの「頭」固定で、pitch は首を振るだけ。
                    transform.SetPositionAndRotation(
                        playerPos + Vector3.up * firstPersonHeight,
                        yawRot * pitchRot);
                    break;
            }
        }

        void UpdatePlayerVisibility()
        {
            var player = PlayerActor.Instance;
            if (player == null) return;
            bool visible = _mode != Mode.FirstPerson;
            var renderers = player.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }

        static bool CycleModePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.zKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.selectButton.wasPressedThisFrame) return true;
            return false;
        }
    }
}
