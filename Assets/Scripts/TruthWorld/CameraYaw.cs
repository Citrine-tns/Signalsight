using UnityEngine;
using UnityEngine.InputSystem;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤー基準のカメラ水平回転（ヨー）。Player の transform を Y 軸まわりに回し、
    /// 子であるカメラを軌道させる。プレイヤーの Capsule メッシュは Y 軸対称なので
    /// 回転自体は不可視で、視覚上はカメラだけがプレイヤーの周りを回って見える。
    /// 入力：ゲームパッド右スティック X / キーボード矢印左右 / マウス X 移動。
    /// プレイヤー GameObject にアタッチする。
    /// </summary>
    public class CameraYaw : MonoBehaviour
    {
        [Tooltip("キーボード矢印・ゲームパッド全倒し時の回転速度 [度/秒]。")]
        [SerializeField] float keySpeedDegPerSec = 120f;
        [Tooltip("マウス感度 [度/px]。")]
        [SerializeField] float mouseSensitivity = 0.2f;

        void Update()
        {
            float delta = 0f;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) delta -= keySpeedDegPerSec * Time.deltaTime;
                if (kb.rightArrowKey.isPressed) delta += keySpeedDegPerSec * Time.deltaTime;
            }

            var gp = Gamepad.current;
            if (gp != null)
                delta += gp.rightStick.ReadValue().x * keySpeedDegPerSec * Time.deltaTime;

            var mouse = Mouse.current;
            if (mouse != null)
                delta += mouse.delta.ReadValue().x * mouseSensitivity;

            if (Mathf.Abs(delta) > 0f)
                transform.Rotate(0f, delta, 0f, Space.World);
        }
    }
}
