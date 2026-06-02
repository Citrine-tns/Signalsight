using UnityEngine;
using Signalsight.SensorWorld;

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
        // Mode の要素数。Enum.GetValues は配列を new するため static readonly で 1 回だけ走らせ、
        // 毎フレ用途では生成済みの ModeCount を読む。enum に項目を増やしても自動追従する。
        static readonly int ModeCount = System.Enum.GetValues(typeof(Mode)).Length;

        /// <summary>現在のカメラモード。UI 側で 1 人称専用表示の切替などに使う。</summary>
        public Mode CurrentMode => _mode;

        [Header("初期モード")]
        [SerializeField] Mode initialMode = Mode.FirstPerson;

        [Header("見下ろしオルソ")]
        [Tooltip("オルソ投影のサイズ。")]
        [SerializeField] float orthoSize = 10f;
        [Tooltip("プレイヤーから見たオルソカメラの距離 [m]。")]
        [SerializeField] float orthoDistance = 10f;
        [Tooltip("ortho モード突入時（Z 切替・ステージ入場）のカメラ Y 回転 [度]。Euler の Y 値。0 で +Z 方向を見る。")]
        [SerializeField] float orthoEntryAzimuthDeg = 45f;
        [Tooltip("ortho モード突入時のカメラ X 回転 [度]。Euler の X 値（俯角）。0 で水平、45 で 45° 見下ろし。")]
        [SerializeField] float orthoEntryElevationDeg = 45f;

        [Header("1 人称")]
        [Tooltip("Player ピボットからのカメラ高さオフセット [m]。既定 0 でピボットに一致＝スキャン原点に一致。"
               + "Player のピボットがカプセル中心にあるため、ここを正の値にすると視点はカプセル上半身〜頭の上に上がる。"
               + "「見たもの＝撃ったもの」を一致させるため既定では 0。")]
        [SerializeField] float firstPersonHeight = 0f;
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
        // _yawDeg / _pitchDeg は **両モード共通で世界座標の絶対角度** として扱う。
        // - FirstPerson：Quaternion.Euler(_pitchDeg, _yawDeg, 0) で直接 look 回転
        // - TopDownOrtho：球面座標の (方位角, 仰角) として位置を計算し、LookRotation でプレイヤーを見る
        // Z 切替・ステージ入場のたびに ResetLook() で「そのモードの突入時初期姿勢」へリセットされる。
        float _yawDeg;
        float _pitchDeg;
        // 中央レジストリから Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        Transform _playerT;
        // Player の Renderer 配列。1 人称切替時の有効・無効切替に使う。Start で 1 度キャッシュし、
        // 以後 GetComponentsInChildren を呼ばない。Player は Core シーン常駐なので寿命は CameraController と一致。
        Renderer[] _playerRenderers;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _camera = GetComponent<Camera>();
            _mode = initialMode;
            SignalsightRefs.Camera = _camera;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (SignalsightRefs.Camera == _camera) SignalsightRefs.Camera = null;
        }

        void Start()
        {
            // Awake 群がすべて済んだ Start でなら SignalsightRefs.PlayerGameObject が登録済みのはず。
            _playerT = SignalsightRefs.PlayerTransform;
            var playerGo = SignalsightRefs.PlayerGameObject;
            if (playerGo != null)
                _playerRenderers = playerGo.GetComponentsInChildren<Renderer>(true);
            // 初期モードの「突入時初期姿勢」で _yawDeg / _pitchDeg を埋める。
            ResetLook();
            ApplyMode();
        }

        void Update()
        {
            // ゲームオーバー等で Time.timeScale = 0 のあいだは入力を受け付けない。
            // キー/スティック分は deltaTime 倍で自然に 0 になるが、マウス delta は per-frame の
            // ピクセル量で時間軸を持たないため、ガードしないと背景でカメラがマウスで回転してしまう。
            if (Time.timeScale == 0f) return;
            // ステージ入場バナー中・タイトル待機中など、入力一括ロック中は何もしない。
            if (SignalsightInput.Locked) return;

            // 同一フレで複数アクションを読むので Player マップを 1 度だけ取得する
            // （`SignalsightInput.Player` は呼ぶたび PlayerActions 構造体を new するため）。
            var input = SignalsightInput.Player;

            if (input.ModeCycle.WasPressedThisFrame())
            {
                _mode = (Mode)(((int)_mode + 1) % ModeCount);
                // 切替先モードの「突入時初期姿勢」へリセット。前モードで貯めた yaw/pitch は
                // モード間で意味（絶対値 vs オフセット、look 方向 vs 公転方向）が違ったので
                // そのまま持ち越すと破綻する。Z 押下のたびに素直な初期視点から始める。
                ResetLook();
                ApplyMode();
            }

            // 入力源ごとに別速度で扱う：キー/スティックは「時間あたりの度数」、マウスは「ピクセルあたりの度数」。
            // この差別化があるため Look 1 本に統合せず 3 アクションに分割している。
            float yawKey = input.CameraYawKey.ReadValue<float>();
            float pitchKey = input.CameraPitchKey.ReadValue<float>();
            Vector2 stick = input.CameraStick.ReadValue<Vector2>();
            Vector2 mouseDelta = input.CameraMouseDelta.ReadValue<Vector2>();

            float yawDelta = (yawKey + stick.x) * keyYawDegPerSec * Time.deltaTime
                           + mouseDelta.x * mouseYawSensitivity;
            float pitchDelta = (pitchKey - stick.y) * keyPitchDegPerSec * Time.deltaTime
                             - mouseDelta.y * mousePitchSensitivity;

            _yawDeg += yawDelta;
            _pitchDeg = ClampPitchForCurrentMode(_pitchDeg + pitchDelta);
        }

        /// <summary>
        /// 現在のモードに応じて _pitchDeg をクランプ。両モードで pitch は世界座標の絶対値
        /// （FirstPerson は look pitch、TopDownOrtho は球面座標の elevation）なので直接 clamp する。
        /// </summary>
        float ClampPitchForCurrentMode(float pitch)
        {
            switch (_mode)
            {
                case Mode.TopDownOrtho:
                    return Mathf.Clamp(pitch, orthoMinElevationDeg, orthoMaxElevationDeg);
                case Mode.FirstPerson:
                    return Mathf.Clamp(pitch, firstPersonMinPitchDeg, firstPersonMaxPitchDeg);
            }
            return pitch;
        }

        /// <summary>
        /// 現在のモードの「突入時初期姿勢」へ _yawDeg / _pitchDeg をリセットする。
        /// 呼ばれるタイミング：(a) Start で初期モードの初期化、(b) Z 押下によるモード切替直後、
        /// (c) StageManager.SwapStageScene でステージ入場のたび。
        ///   - FirstPerson：yaw = プレイヤー体の向き（player.transform.rotation.y）、pitch = 0
        ///     → 「Z で 1 人称に戻したとき / Stage に入ったとき、首は体の正面に水平」が保証される
        ///   - TopDownOrtho：yaw = orthoEntryAzimuthDeg、pitch = orthoEntryElevationDeg
        ///     → Inspector で決めた斜め見下ろし姿勢（既定 45°/45°）から始まる
        /// </summary>
        public void ResetLook()
        {
            switch (_mode)
            {
                case Mode.FirstPerson:
                    _yawDeg = _playerT != null ? _playerT.eulerAngles.y : 0f;
                    _pitchDeg = 0f;
                    break;
                case Mode.TopDownOrtho:
                    _yawDeg = orthoEntryAzimuthDeg;
                    _pitchDeg = orthoEntryElevationDeg;
                    break;
            }
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
            if (_playerT == null) return;

            Vector3 playerPos = _playerT.position;

            switch (_mode)
            {
                case Mode.TopDownOrtho:
                {
                    // _yawDeg / _pitchDeg はカメラの Euler 角 (x=pitch, y=yaw) として解釈し、
                    // FirstPerson と完全に同じ rotation 規約に揃える。
                    // 1. camera rotation = Quaternion.Euler(pitch, yaw, 0) を確定
                    // 2. その forward 方向に -orthoDistance だけ離した位置を camera 位置にする
                    //    （camera が forward 方向を向きながら player を視野中心に捉える＝公転と注視の同時成立）
                    // ortho 既定 (pitch=45, yaw=45) なら camera は player の NW 上方に立ち SE-下方向を向く。
                    // pitch は Update で orthoMin/MaxElevationDeg 範囲に既にクランプ済みなので、
                    // ここでは真上/真下で Euler の万一の崩れを防ぐ安全クランプ ±89° だけ掛ける。
                    float clampedPitch = Mathf.Clamp(_pitchDeg, -89f, 89f);
                    Quaternion lookRot = Quaternion.Euler(clampedPitch, _yawDeg, 0f);
                    Vector3 camForward = lookRot * Vector3.forward;
                    Vector3 camPos = playerPos - camForward * orthoDistance;
                    transform.SetPositionAndRotation(camPos, lookRot);
                    break;
                }

                case Mode.FirstPerson:
                    // 1 人称のカメラ位置は Player ピボット（既定で firstPersonHeight=0）。
                    // ピボットはカプセル中心にあり、PlayerActor.Update のスキャン原点と一致するので、
                    // 「カメラに見えてる位置 = ping を撃った位置」が完全に揃う。pitch は首を振るだけ。
                    // Quaternion.Euler の Z=0 で組むので roll は構造的に発生しない。
                    transform.SetPositionAndRotation(
                        playerPos + Vector3.up * firstPersonHeight,
                        Quaternion.Euler(_pitchDeg, _yawDeg, 0f));
                    break;
            }
        }

        void UpdatePlayerVisibility()
        {
            if (_playerRenderers == null) return;
            bool visible = _mode != Mode.FirstPerson;
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                var r = _playerRenderers[i];
                if (r != null) r.enabled = visible;
            }
        }
    }
}
