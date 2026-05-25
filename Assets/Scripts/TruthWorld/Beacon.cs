using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 探索で発見・起動するビーコン。起動前は不可視・停止、起動後は可視化してスキャンを開始する。
    /// </summary>
    public class Beacon : MonoBehaviour
    {
        [Header("スキャン")]
        [Tooltip("センサごとに一意（1, 2, ...）。色コードと対応する。")]
        [SerializeField] int sensorId = 1;
        [SerializeField] float pulseInterval = 0.5f;    // スキャン間隔 [s]
        [Tooltip("ビーコンの走査ジオメトリ。既定は全センサ共通の ScanProfile.Default。")]
        [SerializeField] ScanProfile scanProfile = ScanProfile.Default;

        [Header("起動")]
        [Tooltip("この距離以内で起動キーを押すと起動できる [m]（3 次元直線距離）。")]
        [SerializeField] float activationRange = 4f;
        [Tooltip("シーン開始時点で起動済み状態でスタートする。タイトル画面の自動 scan beacon 等で使用。" +
                 "起動後の動作は通常通り（Marker 表示 + 定期 scan）。プレイヤーが居なくても scan する。")]
        [SerializeField] bool startActive = false;

        bool _active;
        float _timer;
        // 中央参照から Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        Transform _playerT;
        RadarSimulator _simulator;

        /// <summary>起動済みなら true。外部の UI / チュートリアル等から参照する。</summary>
        public bool IsActive => _active;

        void Awake()
        {
            SetLayer(SignalsightNames.Layers.World);   // 起動前：レイに映るがカメラには映らない
            SetVisible(false);
        }

        void Start()
        {
            _playerT = SignalsightRefs.PlayerTransform;
            _simulator = RadarSimulator.Instance;

            // startActive はここで反映（Awake では _simulator / Refs キャッシュが未完了のため）。
            if (startActive) Activate();
        }

        void Update()
        {
            if (!_active)
            {
                // 起動入力（Enter / A）だけ Locked でガード。プレイヤーが居ないか圏外でも
                // どのみち InRange() で弾かれるが、Locked 中の早期 return で意図を明示する。
                if (SignalsightInput.Locked) return;
                if (InRange() && SignalsightInput.Player.Activate.WasPressedThisFrame()) Activate();
                return;
            }

            // 起動済み定期 scan は Locked を無視して継続する。
            // タイトル画面では入力ロック中も beacon が SIGNALSIGHT を浮かび上がらせる必要があるため。
            if (_simulator == null) return;
            _timer += Time.deltaTime;
            if (_timer >= pulseInterval)
            {
                _timer -= pulseInterval;
                _simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
            }
        }

        bool InRange()
        {
            if (_playerT == null) return false;
            Vector3 d = _playerT.position - transform.position;
            return d.sqrMagnitude <= activationRange * activationRange;
        }

        void Activate()
        {
            _active = true;
            _timer = 0f;
            SetLayer(SignalsightNames.Layers.Marker);   // 起動後：レイに映らずマーカーとして常時表示
            SetVisible(true);
            if (_simulator != null)
                _simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
        }

        void SetLayer(string layerName)
        {
            if (!SignalsightNames.TryGetLayer(layerName, out int newLayer)) return;
            // 「親と同じレイヤだった子」だけ追従させる。Inspector で明示的に別レイヤを
            // 設定した子（発光エフェクト等）は保護されるので、ビーコン下に異レイヤ子を
            // 置く設計を Stage2 以降で自由に取れる。
            int previousLayer = gameObject.layer;
            SetLayerRecursive(transform, previousLayer, newLayer);
        }

        static void SetLayerRecursive(Transform t, int matchLayer, int newLayer)
        {
            if (t.gameObject.layer != matchLayer) return;
            t.gameObject.layer = newLayer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), matchLayer, newLayer);
        }

        void SetVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }
    }
}
