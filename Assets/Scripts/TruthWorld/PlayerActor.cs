using UnityEngine;
using UnityEngine.InputSystem;

namespace Signalsight.TruthWorld
{
    /// <summary>ping 入力でプレイヤーのスキャンを発火する。</summary>
    public class PlayerActor : MonoBehaviour
    {
        [SerializeField] RadarSimulator simulator;
        [SerializeField] int sensorId = 0;
        [Tooltip("ping のクールタイム [s]。この間隔以内は再発火しない。")]
        [SerializeField] float pingCooldown = 0.3f;
        [Tooltip("プレイヤーの走査ジオメトリ。")]
        [SerializeField] ScanProfile scanProfile = new ScanProfile
        {
            slabCount = 17,
            slabSpacing = 0.10f,
            elevationStepDeg = 0.25f,
        };

        float _lastPingTime = -999f;

        void Update()
        {
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
