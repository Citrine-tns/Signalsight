using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 敵。一定間隔で全方位スキャン（レーダにも映る）し、遮蔽されていなければプレイヤーを
    /// 検知して最後に見た座標へ移動する。プレイヤーが近づくと範囲爆発でゲームオーバーにする。
    /// 爆発しても外していれば（ゲームオーバーにならなければ）クールダウン後に再開する。
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] RadarSimulator simulator;
        [SerializeField] Transform player;
        [Tooltip("遮蔽判定に使う散乱体レイヤ（World）。")]
        [SerializeField] LayerMask worldMask = ~0;

        [Header("走査")]
        [SerializeField] int sensorId = 3;
        [Tooltip("スキャン間隔 [s]。")]
        [SerializeField] float detectInterval = 1f;
        [Tooltip("この距離を超えるとプレイヤーを検知しない [m]。")]
        [SerializeField] float detectRange = 30f;
        [SerializeField] ScanProfile scanProfile = new ScanProfile
        {
            slabCount = 10,
            slabSpacing = 0.20f,
            elevationStepDeg = 0f,
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

        Shader _explosionShader;
        bool _hasTarget;
        Vector3 _lastKnownPos;
        float _detectTimer;
        float _attackTimer;

        void Start()
        {
            _explosionShader = Shader.Find("Signalsight/Explosion");
            if (_explosionShader == null)
                Debug.LogError("[EnemyAI] Shader 'Signalsight/Explosion' が見つかりません。");
        }

        void Update()
        {
            if (player == null) return;

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

            // 一定間隔でスキャン（レーダに映る）＋検知。
            _detectTimer += Time.deltaTime;
            if (_detectTimer >= detectInterval)
            {
                _detectTimer = 0f;
                if (simulator != null)
                    simulator.Scan(transform.position, transform.rotation, sensorId, scanProfile);
                if (DetectPlayer())
                {
                    _hasTarget = true;
                    _lastKnownPos = player.position;
                }
            }

            // 最後に検知した座標へ移動（水平のみ）。
            if (_hasTarget)
            {
                Vector3 cur = transform.position;
                var target = new Vector3(_lastKnownPos.x, cur.y, _lastKnownPos.z);
                transform.position = Vector3.MoveTowards(cur, target, moveSpeed * Time.deltaTime);
            }

            // プレイヤーが攻撃範囲に入っていて、クールダウンが明けていれば爆発。
            if (_attackTimer <= 0f &&
                Vector3.Distance(transform.position, player.position) <= attackRange)
                Explode();
        }

        /// <summary>遮蔽されず detectRange 以内なら true（全方位スキャンがプレイヤーに到達する条件と等価）。</summary>
        bool DetectPlayer()
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

        void Explode()
        {
            _attackTimer = attackCooldown;

            // 膨張する爆発エフェクトを生成。
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Explosion";
            Destroy(go.GetComponent<Collider>());
            int marker = LayerMask.NameToLayer("Marker");
            if (marker >= 0) go.layer = marker;
            go.transform.position = transform.position;

            if (_explosionShader != null)
            {
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
