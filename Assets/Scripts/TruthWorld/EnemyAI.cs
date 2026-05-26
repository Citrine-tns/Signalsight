using UnityEngine;
using UnityEngine.AI;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 敵。一定間隔で全方位スキャン（レーダにも映る）し、遮蔽されていなければプレイヤーを
    /// 検知して最後に見た座標へ移動する。プレイヤーが近づくと範囲爆発でゲームオーバーにする。
    /// 爆発しても外していれば（ゲームオーバーにならなければ）クールダウン後に再開する。
    ///
    /// 移動は NavMeshAgent 駆動。ステージ Scene には NavMeshSurface をベイクしておくこと。
    /// 状態遷移：
    ///   - Idle      : 初期位置で停止
    ///   - Chasing   : 最後に検知した位置（LKP）へ経路探索で接近。検知がロストしても LKP に
    ///                 到達するまで歩き続け、到達後の次スキャンで検知できなければ帰還する。
    ///                 （途中ロストでは諦めない。プレイヤーが少し奥に逃げてもしばらく追える。）
    ///   - Returning : 初期位置へ経路探索で帰還
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("走査・検知")]
        [Tooltip("遮蔽判定に使う散乱体レイヤ（World）。未設定なら Awake で World レイヤを自動セット。")]
        [SerializeField] LayerMask worldMask;
        [SerializeField] int sensorId = 3;
        [Tooltip("スキャン間隔 [s]。")]
        [SerializeField] float detectInterval = 1f;
        [Tooltip("この距離を超えるとプレイヤーを検知しない [m]。")]
        [SerializeField] float detectRange = 30f;
        [Tooltip("敵の走査ジオメトリ。既定は全センサ共通の ScanProfile.Default。")]
        [SerializeField] ScanProfile scanProfile = ScanProfile.Default;

        [Header("行動")]
        [SerializeField] float moveSpeed = 2f;
        [Tooltip("プレイヤーがこの距離以内に入ると爆発する [m]。")]
        [SerializeField] float attackRange = 2f;
        [Tooltip("爆発の半径 [m]。")]
        [SerializeField] float blastRadius = 3f;
        [Tooltip("爆発後、再び爆発できるまでのクールダウン [s]。")]
        [SerializeField] float attackCooldown = 2f;
        [Tooltip("目的地に到達したと判定する距離 [m]。NavMeshAgent.stoppingDistance に設定する。")]
        [SerializeField] float arrivalDistance = 0.5f;

        enum State { Idle, Chasing, Returning }

        NavMeshAgent _agent;
        // 中央参照から Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        Transform _playerT;
        RadarSimulator _simulator;
        State _state = State.Idle;
        Vector3 _initialPos;
        Vector3 _lastKnownPos;
        float _detectTimer;
        float _attackTimer;
        // Chasing 中に LKP に到達済みかを保持。到達後の最初の失敗スキャンで Returning へ。
        bool _arrivedAtLastKnown;

        void Awake()
        {
            // Awake は enabled=false でも実行されるので、無効化状態で開始しても寸法と
            // baseOffset がここで確定し、敵は最初から床に正しく立っていられる。
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
            _agent.stoppingDistance = arrivalDistance;
            MatchAgentToBody();
            _initialPos = transform.position;

            SignalsightNames.EnsureWorldMask(ref worldMask, this);
        }

        void Start()
        {
            _playerT = SignalsightRefs.PlayerTransform;
            _simulator = RadarSimulator.Instance;
        }

        /// <summary>
        /// NavMeshAgent の寸法・足元オフセットを敵本体の CapsuleCollider に合わせる。
        /// NavMeshAgent の radius / height / baseOffset は Transform スケールが乗らない
        /// ワールド固定値なので、シリンダー本体の見た目に合わせて手動で計算してやる。
        ///
        /// Cylinder プリミティブのピボットはメッシュ中心にあるため、足元をナビメッシュ
        /// に乗せるには baseOffset を「メッシュの半身ぶん」上げる必要がある。
        /// </summary>
        void MatchAgentToBody()
        {
            var capsule = GetComponent<CapsuleCollider>();
            Vector3 s = transform.lossyScale;
            _agent.radius = capsule.radius * Mathf.Max(s.x, s.z);
            _agent.height = capsule.height * s.y;
            // メッシュ底が transform - (height/2 - center.y) * s.y にあるので、その分だけ
            // 持ち上げて足元（メッシュ底）を NavMesh に乗せる。
            _agent.baseOffset = (capsule.height * 0.5f - capsule.center.y) * s.y;
        }

        void Update()
        {
            if (_playerT == null) return;
            // 世界停止中（ステージ入場 banner / GameOver）は敵を完全に止める。
            // 攻撃判定 `_attackTimer <= 0 && sqrToPlayer <= attackRange²` は deltaTime に依存しない
            // ので timeScale=0 だけでは止まらない（フレーム毎に評価され続ける）ため、明示ガード。
            if (Time.timeScale == 0f) return;

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

            // 一定間隔でスキャン（レーダに映る）＋検知。
            // scannedThisFrame と detectedThisFrame を分けて返す：
            //   - detectedThisFrame=true … 検知に成功
            //   - scannedThisFrame=true && !detectedThisFrame … 検知が走ったが失敗
            //   - scannedThisFrame=false … このフレームは検知が走らなかった
            // Chasing の「到達後の次スキャン失敗で帰還」判定で両方の区別が必要。
            bool scannedThisFrame = false;
            bool detectedThisFrame = false;
            _detectTimer += Time.deltaTime;
            if (_detectTimer >= detectInterval)
            {
                _detectTimer -= detectInterval;
                if (_simulator != null)
                    _simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
                scannedThisFrame = true;
                if (DetectPlayer(_playerT))
                {
                    _lastKnownPos = _playerT.position;
                    detectedThisFrame = true;
                }
            }

            StepStateMachine(detectedThisFrame, scannedThisFrame);

            // プレイヤーが攻撃範囲に入っていて、クールダウンが明けていれば爆発。
            // sqrMagnitude を 1 回算出して Explode に渡し、爆風判定でも再利用する。
            float sqrToPlayer = (_playerT.position - transform.position).sqrMagnitude;
            if (_attackTimer <= 0f && sqrToPlayer <= attackRange * attackRange)
                Explode(sqrToPlayer);
        }

        /// <summary>1 フレームぶんの状態遷移と移動指示を行う。</summary>
        void StepStateMachine(bool detectedThisFrame, bool scannedThisFrame)
        {
            switch (_state)
            {
                case State.Idle:
                    if (detectedThisFrame) EnterChasing(_lastKnownPos);
                    break;

                case State.Chasing:
                    if (detectedThisFrame)
                    {
                        // 新しい検知で destination を更新、到達フラグはリセット。
                        // _lastKnownPos は Update 側で既に更新済み。
                        _arrivedAtLastKnown = false;
                        _agent.SetDestination(_lastKnownPos);
                    }
                    else
                    {
                        // ロスト中も LKP まで歩き続ける。到達したら「次スキャン待ち」フェーズに入る。
                        if (!_arrivedAtLastKnown && Arrived())
                            _arrivedAtLastKnown = true;
                        // 到達後、最初に走った失敗スキャンで諦めて帰還。
                        if (_arrivedAtLastKnown && scannedThisFrame)
                            EnterReturning();
                    }
                    break;

                case State.Returning:
                    if (detectedThisFrame) EnterChasing(_lastKnownPos);
                    else if (Arrived()) _state = State.Idle;
                    break;
            }
        }

        bool Arrived()
        {
            if (_agent.pathPending) return false;
            // PathInvalid（NavMesh が無い／途切れている）も「到達」として扱い、次スキャン判定に進める。
            // Chasing の旧 loseSightTimeout が stuck 救済も兼ねていたので、それを消した分の代替。
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
            return _agent.remainingDistance <= _agent.stoppingDistance;
        }

        void EnterChasing(Vector3 target)
        {
            _state = State.Chasing;
            _lastKnownPos = target;
            _arrivedAtLastKnown = false;
            _agent.SetDestination(_lastKnownPos);
        }

        void EnterReturning()
        {
            _state = State.Returning;
            _agent.SetDestination(_initialPos);
        }

        /// <summary>遮蔽されず detectRange 以内なら true（全方位スキャンがプレイヤーに到達する条件と等価）。</summary>
        bool DetectPlayer(Transform player)
        {
            Vector3 to = player.position - transform.position;
            float d = to.magnitude;
            if (d > detectRange) return false;
            // emitter 半径以内（敵にほぼ密着）は遮蔽判定不要。早期 return しないと
            // Raycast の maxDistance が 0 以下になり Unity の degenerate ケース挙動に依存する。
            float r = scanProfile.emitterRadius;
            if (d <= r) return true;

            Vector3 dir = to / d;
            return !Physics.Raycast(transform.position + dir * r, dir, d - r,
                                    worldMask, QueryTriggerInteraction.Ignore);
        }

        void Explode(float sqrToPlayer)
        {
            _attackTimer = attackCooldown;

            // 共有 Material は ExplosionEffect が一元管理。null 戻りは shader 欠落で
            // 視覚エフェクトを丸ごとスキップしたいケース（ゲームオーバー判定は継続）。
            var sharedMat = ExplosionEffect.GetSharedMaterial();
            if (sharedMat != null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Explosion";
                Destroy(go.GetComponent<Collider>());
                if (SignalsightNames.TryGetLayer(SignalsightNames.Layers.Marker, out int marker))
                    go.layer = marker;
                go.transform.position = transform.position;

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = sharedMat;
                go.AddComponent<ExplosionEffect>().Play(blastRadius, 0.5f, mr, new Color(1f, 0.5f, 0.15f));
            }

            // 爆風がプレイヤーを捉えていればゲームオーバー、外していれば敵は行動を続ける。
            if (sqrToPlayer <= blastRadius * blastRadius)
                GameOverController.Trigger();
        }
    }
}
