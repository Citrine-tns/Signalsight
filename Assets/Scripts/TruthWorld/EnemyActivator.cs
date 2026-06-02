using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// チュートリアル用：プレイヤーがトリガーに入った瞬間、指定 EnemyAI 群の
    /// MonoBehaviour.enabled を true にして起動する。一度発火したら自己破棄。
    /// セーブで Id を記録し、ロード時に Awake で「発火済み」を検出して即時起動 + 自己破棄する。
    ///
    /// 配置：
    /// - 起動ゾーンの GameObject に Collider（isTrigger = true）と本コンポーネント
    /// - targets に「眠らせておく EnemyAI」を割り当て
    /// - 各 EnemyAI 側の Inspector ヘッダの「Enabled」チェックを外しておく
    ///   （起動前は Update が走らず、コライダ・NavMeshAgent・メッシュは生きたまま）
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EnemyActivator : MonoBehaviour
    {
        [Tooltip("起動する EnemyAI 群。トリガー突入で全部 enabled=true になる。")]
        [SerializeField] EnemyAI[] targets;
        [Tooltip("セーブで「発火済み」を記録する一意 ID。空文字なら追跡しない（テスト用配置）。")]
        [SerializeField] string id;

        void Awake()
        {
            // ロード時に発火済みなら即時起動 + 自己破棄。
            if (string.IsNullOrEmpty(id)) return;
            if (ProgressFlags.Instance != null && ProgressFlags.Instance.Has("activator_" + id))
                ActivateAndDestroy();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!SignalsightRefs.IsPlayer(other)) return;
            if (!string.IsNullOrEmpty(id) && ProgressFlags.Instance != null)
                ProgressFlags.Instance.Set("activator_" + id);
            ActivateAndDestroy();
        }

        void ActivateAndDestroy()
        {
            if (targets != null)
                foreach (var t in targets)
                    if (t != null) t.enabled = true;
            Destroy(gameObject);
        }
    }
}
