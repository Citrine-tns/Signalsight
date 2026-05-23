using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// チュートリアル用：プレイヤーがトリガーに入った瞬間、指定 EnemyAI 群の
    /// MonoBehaviour.enabled を true にして起動する。一度発火したら自己破棄。
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

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerActor>() == null) return;
            if (targets != null)
                foreach (var t in targets)
                    if (t != null) t.enabled = true;
            Destroy(gameObject);
        }
    }
}
