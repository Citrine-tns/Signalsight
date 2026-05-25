using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>ping 入力でプレイヤーのスキャンを発火する。</summary>
    public class PlayerActor : MonoBehaviour
    {
        public static PlayerActor Instance { get; private set; }

        [Header("ping")]
        [SerializeField] int sensorId = 0;
        [Tooltip("ping のクールタイム [s]。この間隔以内は再発火しない。")]
        [SerializeField] float pingCooldown = 0.3f;
        [Tooltip("プレイヤーの走査ジオメトリ。既定は全センサ共通の ScanProfile.Default。")]
        [SerializeField] ScanProfile scanProfile = ScanProfile.Default;

        // 長時間プレイで float 精度が落ちるのを避けるため、ping のタイムスタンプは double で保持する
        // （SensorBus / RadarSimulator が Time.timeAsDouble を採用しているのと揃える）。
        double _lastPingTime = -999.0;
        // 中央参照から Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        RadarSimulator _simulator;

        /// <summary>ping のクールタイム長 [s]。UI 表示用に公開。</summary>
        public float PingCooldown => pingCooldown;
        /// <summary>最後に ping を撃った時刻 [s]（Time.timeAsDouble 基準）。UI 表示用に公開。</summary>
        public double LastPingTime => _lastPingTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SignalsightRefs.PlayerGameObject = gameObject;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (SignalsightRefs.PlayerGameObject == gameObject) SignalsightRefs.PlayerGameObject = null;
        }

        void Start()
        {
            _simulator = RadarSimulator.Instance;
        }

        void Update()
        {
            if (_simulator == null) return;
            if (SignalsightInput.Locked) return;

            // 押下エッジ or 押しっぱなしどちらでも反応（クールダウンを噛ませて連射制限）。
            var ping = SignalsightInput.Player.Ping;
            bool wantPing = ping.WasPressedThisFrame() || ping.IsPressed();
            double now = Time.timeAsDouble;
            if (wantPing && now - _lastPingTime >= pingCooldown)
            {
                _simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
                _lastPingTime = now;
            }
        }
    }
}
