using UnityEngine;
using UnityEngine.InputSystem;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 探索で発見・起動するビーコン。起動前は不可視・停止、起動後は可視化してスキャンを開始する。
    /// </summary>
    public class Beacon : MonoBehaviour
    {
        [Tooltip("センサごとに一意（1, 2, ...）。色コードと対応する。")]
        [SerializeField] int sensorId = 1;
        [SerializeField] float pulseInterval = 0.5f;    // スキャン間隔 [s]
        [Tooltip("この距離以内で起動キーを押すと起動できる [m]（3 次元直線距離）。")]
        [SerializeField] float activationRange = 4f;
        [Tooltip("ビーコンの走査ジオメトリ。既定は全センサ共通の ScanProfile.Default。")]
        [SerializeField] ScanProfile scanProfile = ScanProfile.Default;

        bool _active;
        float _timer;

        /// <summary>起動済みなら true。外部の UI / チュートリアル等から参照する。</summary>
        public bool IsActive => _active;

        void Awake()
        {
            SetLayer(SignalsightNames.Layers.World);   // 起動前：レイに映るがカメラには映らない
            SetVisible(false);
        }

        void Update()
        {
            if (!_active)
            {
                if (InRange() && ActivatePressed()) Activate();
                return;
            }

            var simulator = RadarSimulator.Instance;
            if (simulator == null) return;
            _timer += Time.deltaTime;
            if (_timer >= pulseInterval)
            {
                _timer -= pulseInterval;
                simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
            }
        }

        bool InRange()
        {
            var player = PlayerActor.Instance;
            if (player == null) return false;
            Vector3 d = player.transform.position - transform.position;
            return d.sqrMagnitude <= activationRange * activationRange;
        }

        void Activate()
        {
            _active = true;
            _timer = 0f;
            SetLayer(SignalsightNames.Layers.Marker);   // 起動後：レイに映らずマーカーとして常時表示
            SetVisible(true);
            var simulator = RadarSimulator.Instance;
            if (simulator != null)
                simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
        }

        void SetLayer(string layerName)
        {
            if (SignalsightNames.TryGetLayer(layerName, out int layer))
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
