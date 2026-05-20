using UnityEngine;
using UnityEngine.InputSystem;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 探索で発見・起動するビーコン。起動前は不可視・停止、起動後は可視化してスキャンを開始する。
    /// </summary>
    public class Beacon : MonoBehaviour
    {
        const string HiddenLayer = "World";    // 起動前：レイに映る／カメラには映らない
        const string ActiveLayer = "Marker";   // 起動後：レイに映らない／マーカー表示

        [SerializeField] RadarSimulator simulator;
        [SerializeField] Transform player;
        [Tooltip("センサごとに一意（1, 2, ...）。色コードと対応する。")]
        [SerializeField] int sensorId = 1;
        [SerializeField] float pulseInterval = 0.5f;
        [Tooltip("この距離以内で起動キーを押すと起動できる [m]。")]
        [SerializeField] float activationRange = 4f;

        bool _active;
        float _timer;

        void Awake()
        {
            SetLayer(HiddenLayer);
            SetVisible(false);
        }

        void Update()
        {
            if (!_active)
            {
                if (InRange() && ActivatePressed()) Activate();
                return;
            }

            if (simulator == null) return;
            _timer += Time.deltaTime;
            if (_timer >= pulseInterval)
            {
                _timer -= pulseInterval;
                simulator.Scan(transform.position, sensorId);
            }
        }

        bool InRange()
        {
            if (player == null) return false;
            float dx = player.position.x - transform.position.x;
            float dz = player.position.z - transform.position.z;
            return dx * dx + dz * dz <= activationRange * activationRange;
        }

        void Activate()
        {
            _active = true;
            _timer = 0f;
            SetLayer(ActiveLayer);
            SetVisible(true);
            if (simulator != null)
                simulator.Scan(transform.position, sensorId);
        }

        void SetLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[Beacon] レイヤ '{layerName}' が未定義です。");
                return;
            }
            SetLayerRecursive(transform, layer);
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }

        void SetVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }

        static bool ActivatePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                return true;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true;
            return false;
        }
    }
}
