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
    ///   - Chasing   : 最後に検知した位置へ経路探索で接近
    ///   - Returning : ロスト後、初期位置へ経路探索で帰還
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("走査・検知")]
        [Tooltip("遮蔽判定に使う散乱体レイヤ（World）。")]
        [SerializeField] LayerMask worldMask = ~0;
        [SerializeField] int sensorId = 3;
        [Tooltip("スキャン間隔 [s]。")]
        [SerializeField] float detectInterval = 1f;
        [Tooltip("この距離を超えるとプレイヤーを検知しない [m]。")]
        [SerializeField] float detectRange = 30f;
        [SerializeField] ScanProfile scanProfile = new ScanProfile
        {
            slabCount = 10,
            slabSpacing = 0.20f,
            elevationStepDeg = 1f,
            emitterRadius = 0.3f,
        };

        [Header("行動")]
        [SerializeField] float moveSpeed = 2f;
        [Tooltip("プレイヤーがこの距離以内に入ると爆発する [m]。")]
        [SerializeField] float attackRange = 2f;
        [Tooltip("爆発の半径 [m]。")]
        [SerializeField] float blastRadius = 3f;
        [Tooltip("爆発後、再び爆発できるまでのクールダウン [s]。")]
        [SerializeField] float attackCooldown = 2f;
        [Tooltip("追跡中にこの時間検知が途切れたらロストして初期位置へ帰還を始める [s]。")]
        [SerializeField] float loseSightTimeout = 3f;
        [Tooltip("目的地に到達したと判定する距離 [m]。NavMeshAgent.stoppingDistance に設定する。")]
        [SerializeField] float arrivalDistance = 0.5f;

        enum State { Idle, Chasing, Returning }

        Shader _explosionShader;
        NavMeshAgent _agent;
        State _state = State.Idle;
        Vector3 _initialPos;
        Vector3 _lastKnownPos;
        float _detectTimer;
        float _attackTimer;
        float _lostTimer;

        void Awake()
        {
            // Awake は enabled=false でも実行されるので、無効化状態で開始しても寸法と
            // baseOffset がここで確定し、敵は最初から床に正しく立っていられる。
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
            _agent.stoppingDistance = arrivalDistance;
            MatchAgentToBody();
            _initialPos = transform.position;

            _explosionShader = Shader.Find(SignalsightNames.Shaders.Explosion);
            if (_explosionShader == null)
                Debug.LogError($"[EnemyAI] Shader '{SignalsightNames.Shaders.Explosion}' が見つかりません。");
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
            if (capsule == null)
            {
                Debug.LogWarning("[EnemyAI] CapsuleCollider が無いため NavMeshAgent の寸法を自動調整できません。");
                return;
            }
            Vector3 s = transform.lossyScale;
            _agent.radius = capsule.radius * Mathf.Max(s.x, s.z);
            _agent.height = capsule.height * s.y;
            // メッシュ底が transform - (height/2 - center.y) * s.y にあるので、その分だけ
            // 持ち上げて足元（メッシュ底）を NavMesh に乗せる。
            _agent.baseOffset = (capsule.height * 0.5f - capsule.center.y) * s.y;
        }

        void Update()
        {
            var pa = PlayerActor.Instance;
            if (pa == null) return;
            Transform player = pa.transform;

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

            // 一定間隔でスキャン（レーダに映る）＋検知。
            bool detectedThisFrame = false;
            _detectTimer += Time.deltaTime;
            if (_detectTimer >= detectInterval)
            {
                _detectTimer -= detectInterval;
                var simulator = RadarSimulator.Instance;
                if (simulator != null)
                    simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
                if (DetectPlayer(player))
                {
                    _lastKnownPos = player.position;
                    detectedThisFrame = true;
                }
            }

            StepStateMachine(detectedThisFrame);

            // プレイヤーが攻撃範囲に入っていて、クールダウンが明けていれば爆発。
            if (_attackTimer <= 0f &&
                Vector3.Distance(transform.position, player.position) <= attackRange)
                Explode(player);
        }

        /// <summary>1 フレームぶんの状態遷移と移動指示を行う。</summary>
        void StepStateMachine(bool detectedThisFrame)
        {
            switch (_state)
            {
                case State.Idle:
                    if (detectedThisFrame) EnterChasing();
                    break;

                case State.Chasing:
                    if (detectedThisFrame)
                    {
                        _lostTimer = 0f;
                        _agent.SetDestination(_lastKnownPos);
                    }
                    else
                    {
                        _lostTimer += Time.deltaTime;
                        // ロスト＝最後に見た位置に到達した／一定時間検知無しのどちらか。
                        if (Arrived() || _lostTimer >= loseSightTimeout)
                            EnterReturning();
                    }
                    break;

                case State.Returning:
                    if (detectedThisFrame) EnterChasing();
                    else if (Arrived()) _state = State.Idle;
                    break;
            }
        }

        bool Arrived()
        {
            return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;
        }

        void EnterChasing()
        {
            _state = State.Chasing;
            _lostTimer = 0f;
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
            if (d < 1e-3f) return true;

            Vector3 dir = to / d;
            float r = scanProfile.emitterRadius;
            return !Physics.Raycast(transform.position + dir * r, dir, d - r,
                                    worldMask, QueryTriggerInteraction.Ignore);
        }

        void Explode(Transform player)
        {
            _attackTimer = attackCooldown;

            // shader が無いと ExplosionEffect が付けられず Sphere がリークするので、
            // shader 欠落時は視覚エフェクトを丸ごとスキップする（ゲームオーバー判定は継続）。
            if (_explosionShader != null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Explosion";
                Destroy(go.GetComponent<Collider>());
                if (SignalsightNames.TryGetLayer(SignalsightNames.Layers.Marker, out int marker))
                    go.layer = marker;
                go.transform.position = transform.position;

                var mat = new Material(_explosionShader);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                go.AddComponent<ExplosionEffect>().Play(blastRadius, 0.5f, mat, new Color(1f, 0.5f, 0.15f));
            }

            // 爆風がプレイヤーを捉えていればゲームオーバー。外していれば敵は行動を続ける。
            if (Vector3.Distance(transform.position, player.position) <= blastRadius)
                GameOverController.Trigger();
        }
    }
}
