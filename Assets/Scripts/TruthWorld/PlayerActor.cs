using UnityEngine;
using UnityEngine.InputSystem;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>プレイヤーの自己位置を SensorBus へ送り、ping 入力でスキャンを発火する。</summary>
    public class PlayerActor : MonoBehaviour
    {
        [SerializeField] RadarSimulator simulator;
        [SerializeField] int sensorId = 0;
        [Tooltip("ping のクールタイム [s]。この間隔以内は再発火しない。")]
        [SerializeField] float pingCooldown = 0.3f;

        float _lastPingTime = -999f;

        void Update()
        {
            var bus = SensorBus.Instance;
            if (bus != null)
                bus.SetEgoPosition(new Vector2(transform.position.x, transform.position.z));

            if (simulator == null) return;

            bool wantPing = PingPressedThisFrame() || PingHeld();
            if (wantPing && Time.time - _lastPingTime >= pingCooldown)
            {
                simulator.Scan(transform.position, sensorId);
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
