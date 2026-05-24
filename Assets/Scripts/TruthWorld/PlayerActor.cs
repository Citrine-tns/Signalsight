using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>ping 入力でプレイヤーのスキャンを発火する。</summary>
    public class PlayerActor : MonoBehaviour
    {
        public static PlayerActor Instance { get; private set; }

        [SerializeField] int sensorId = 0;
        [Tooltip("ping のクールタイム [s]。この間隔以内は再発火しない。")]
        [SerializeField] float pingCooldown = 0.3f;
        [Tooltip("プレイヤーの走査ジオメトリ。既定は全センサ共通の ScanProfile.Default。")]
        [SerializeField] ScanProfile scanProfile = ScanProfile.Default;

        float _lastPingTime = -999f;

        /// <summary>ping のクールタイム長 [s]。UI 表示用に公開。</summary>
        public float PingCooldown => pingCooldown;
        /// <summary>最後に ping を撃った時刻 [s]。UI 表示用に公開。</summary>
        public float LastPingTime => _lastPingTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // Player の GameObject 参照を中央レジストリへ publish。Collider 判定や位置参照は
            // SignalsightRefs 経由に統一する（GetComponent 連打 / Camera.main 並列を排除）。
            SignalsightRefs.PlayerGameObject = gameObject;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (SignalsightRefs.PlayerGameObject == gameObject) SignalsightRefs.PlayerGameObject = null;
        }

        void Update()
        {
            var simulator = RadarSimulator.Instance;
            if (simulator == null) return;

            // 押下エッジ or 押しっぱなしどちらでも反応（クールダウンを噛ませて連射制限）。
            var ping = SignalsightInput.Player.Ping;
            bool wantPing = ping.WasPressedThisFrame() || ping.IsPressed();
            if (wantPing && Time.time - _lastPingTime >= pingCooldown)
            {
                simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
                _lastPingTime = Time.time;
            }
        }
    }
}
