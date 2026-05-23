using UnityEngine;
using UnityEngine.InputSystem;

namespace Signalsight.TruthWorld
{
    /// <summary>ping 入力でプレイヤーのスキャンを発火する。</summary>
    public class PlayerActor : MonoBehaviour
    {
        public static PlayerActor Instance { get; private set; }

        [SerializeField] int sensorId = 0;
        [Tooltip("ping のクールタイム [s]。この間隔以内は再発火しない。")]
        [SerializeField] float pingCooldown = 0.3f;
        [Tooltip("プレイヤーの走査ジオメトリ。")]
        [SerializeField] ScanProfile scanProfile = new ScanProfile
        {
            slabCount = 10,
            slabSpacing = 0.20f,
            elevationStepDeg = 1f,
        };

        float _lastPingTime = -999f;

        /// <summary>ping のクールタイム長 [s]。UI 表示用に公開。</summary>
        public float PingCooldown => pingCooldown;
        /// <summary>最後に ping を撃った時刻 [s]。UI 表示用に公開。</summary>
        public float LastPingTime => _lastPingTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var simulator = RadarSimulator.Instance;
            if (simulator == null) return;

            bool wantPing = PingPressedThisFrame() || PingHeld();
            if (wantPing && Time.time - _lastPingTime >= pingCooldown)
            {
                simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
                _lastPingTime = Time.time;
            }
        }

        static bool PingPressedThisFrame()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.rightTrigger.wasPressedThisFrame) return true;
            return false;
        }

        static bool PingHeld()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.rightTrigger.ReadValue() > 0.5f) return true;
            return false;
        }
    }
}
