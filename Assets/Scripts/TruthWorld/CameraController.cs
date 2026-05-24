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
        public static CameraController Instance { get; private set; }

        public enum Mode { TopDownOrtho, FirstPerson }

        /// <summary>現在のカメラモード。UI 側で 1 人称専用表示の切替などに使う。</summary>
        public Mode CurrentMode => _mode;

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

        [Header("Pitch クランプ：1 人称")]
        [Tooltip("1 人称時の最低 pitch [度]。負値 = 上を向く方向。")]
        [SerializeField] float firstPersonMinPitchDeg = -80f;
        [Tooltip("1 人称時の最高 pitch [度]。正値 = 下を向く方向。")]
        [SerializeField] float firstPersonMaxPitchDeg = 80f;

        [Header("Pitch クランプ：オルソ（カメラ仰角の絶対値）")]
        [Tooltip("オルソ時のカメラの最低仰角 [度]。5 でほぼ水平、これ以下にカメラは下がらない。")]
        [SerializeField] float orthoMinElevationDeg = 5f;
        [Tooltip("オルソ時のカメラの最高仰角 [度]。89 でほぼ真上、これ以上にカメラは上がらない。")]
        [SerializeField] float orthoMaxElevationDeg = 89f;

        Camera _camera;
        Mode _mode;
        float _yawDeg;
        float _pitchDeg;

        // ortho の中立姿勢。Scene 初期カメラを Player 中心の球面座標
        // （方位角・仰角・距離）に分解して保持。yaw/pitch を Euler 合成すると roll が
        // 出るので、球面座標で位置を出して LookRotation で常にプレイヤーを見る方式にする。
        float _orthoBaseAzimuthDeg;
        float _orthoBaseElevationDeg;
        float _orthoDistance;
        bool _orthoBaseCaptured;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _camera = GetComponent<Camera>();
            _mode = initialMode;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            CaptureOrthoBase();
            ApplyMode();
        }

        // Capture 失敗時のフォールバック姿勢。Scene 設置ミスでも操作不能になるのを防ぐ。
        const float FallbackOrthoDistance = 10f;
        const float FallbackOrthoElevationDeg = 60f;
        const float FallbackOrthoAzimuthDeg = 0f;

        /// <summary>
        /// Scene にセットされたカメラの初期位置を Player 中心の球面座標
        /// （方位角・仰角・距離）に分解して保存。yaw/pitch 入力はこの基準値からの
        /// オフセットとして角度に加算する。
        /// Player 不在 / カメラ＝Player 位置などで capture 不能ならフォールバック姿勢で
        /// 起動を続行する（赤エラー＋操作不能より、警告＋動作継続を優先）。
        /// </summary>
        void CaptureOrthoBase()
        {
            var player = PlayerActor.Instance;
            if (player == null)
            {
                Debug.LogWarning("[CameraController] PlayerActor.Instance 未登録のためフォールバック姿勢で起動します。Scene に Player を配置してください。", this);
                UseFallbackOrthoBase();
                return;
            }
            Vector3 offset = transform.position - player.transform.position;
            float dist = offset.magnitude;
            if (dist < 1e-4f)
            {
                Debug.LogWarning("[CameraController] カメラとプレイヤーがほぼ同位置のためフォールバック姿勢で起動します。Scene でカメラを離してください。", this);
                UseFallbackOrthoBase();
                return;
            }
            _orthoDistance = dist;
            _orthoBaseElevationDeg = Mathf.Asin(Mathf.Clamp(offset.y / dist, -1f, 1f)) * Mathf.Rad2Deg;
            _orthoBaseAzimuthDeg = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _orthoBaseCaptured = true;
        }

        void UseFallbackOrthoBase()
        {
            _orthoDistance = FallbackOrthoDistance;
            _orthoBaseElevationDeg = FallbackOrthoElevationDeg;
            _orthoBaseAzimuthDeg = FallbackOrthoAzimuthDeg;
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
                _pitchDeg = ClampPitchForCurrentMode(_pitchDeg + pitchDelta);
        }

        /// <summary>
        /// 現在のモードに応じて _pitchDeg をクランプ。1 人称は pitch 絶対値で、オルソは
        /// 「base elevation + pitch」が許容仰角レンジ内に収まる範囲で clamp する。
        /// </summary>
        float ClampPitchForCurrentMode(float pitch)
        {
            switch (_mode)
            {
                case Mode.TopDownOrtho:
                    if (!_orthoBaseCaptured) return pitch;
                    return Mathf.Clamp(pitch,
                        orthoMinElevationDeg - _orthoBaseElevationDeg,
                        orthoMaxElevationDeg - _orthoBaseElevationDeg);
                case Mode.FirstPerson:
                    return Mathf.Clamp(pitch, firstPersonMinPitchDeg, firstPersonMaxPitchDeg);
            }
            return pitch;
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
            // モード切替時は新モードのクランプを即時適用（前モードで貯めた pitch を新範囲へ）。
            _pitchDeg = ClampPitchForCurrentMode(_pitchDeg);
            UpdatePlayerVisibility();
        }

        void LateUpdate()
        {
            var player = PlayerActor.Instance;
            if (player == null) return;

            Vector3 playerPos = player.transform.position;

            switch (_mode)
            {
                case Mode.TopDownOrtho:
                {
                    if (!_orthoBaseCaptured) break;
                    // 球面座標で位置を出して LookRotation で向きを決める。
                    // Quaternion を Euler 合成で重ねないので roll が出ない。
                    float azim = _orthoBaseAzimuthDeg + _yawDeg;
                    // _pitchDeg は Update で orthoMin/MaxElevationDeg 範囲内に既にクランプ済み。
                    // ここでは真上 / 真下で LookRotation が崩れる極の安全クランプだけ掛ける。
                    float elev = Mathf.Clamp(_orthoBaseElevationDeg + _pitchDeg, -89f, 89f);
                    float azimRad = azim * Mathf.Deg2Rad;
                    float elevRad = elev * Mathf.Deg2Rad;
                    float cosE = Mathf.Cos(elevRad);
                    Vector3 offset = new Vector3(
                        _orthoDistance * cosE * Mathf.Sin(azimRad),
                        _orthoDistance * Mathf.Sin(elevRad),
                        _orthoDistance * cosE * Mathf.Cos(azimRad));
                    Vector3 camPos = playerPos + offset;
                    Quaternion camRot = Quaternion.LookRotation(playerPos - camPos, Vector3.up);
                    transform.SetPositionAndRotation(camPos, camRot);
                    break;
                }

                case Mode.FirstPerson:
                    // 1 人称はカメラ位置はプレイヤーの「頭」固定で、pitch は首を振るだけ。
                    // Quaternion.Euler の Z=0 で組むので roll は構造的に発生しない。
                    transform.SetPositionAndRotation(
                        playerPos + Vector3.up * firstPersonHeight,
                        Quaternion.Euler(_pitchDeg, _yawDeg, 0f));
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
