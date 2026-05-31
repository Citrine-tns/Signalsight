using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Field 上に置く「拾えるオブジェクト」。Beacon / Material / Relic すべて本クラスで扱い、
    /// 中身の違いは ItemKind の category と参照先で区別する。
    /// プレイヤーが Trigger Collider に触れた瞬間に Inventory に加算され自身を破棄する。
    ///
    /// Phase 4 で「E キーで interact 方式」に切り替える可能性あり（宝箱を開ける感などを出したい場合）。
    /// 現状は Trigger で最小実装。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FieldPickup : MonoBehaviour
    {
        [Tooltip("拾ったときに Inventory に加算する種類。")]
        [SerializeField] ItemKind kind;
        [Tooltip("拾ったときに何個加算するか。")]
        [SerializeField] int amount = 1;
        [Tooltip("拾われた後にこのオブジェクトを破棄するか。基本 true。")]
        [SerializeField] bool destroyOnPickup = true;

        void Reset()
        {
            // 新規アタッチ時に Collider を自動で Trigger 化しておく（物理衝突で
            // プレイヤーが弾かれる事故を防ぐ）。
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            // プレイヤー以外（壁や弾など）に反応しないよう、CharacterController の有無で判定。
            // このプロジェクトでは CharacterController を持つのはプレイヤーだけなので確実。
            if (other.GetComponentInParent<CharacterController>() == null) return;
            if (Inventory.Instance == null || kind == null) return;

            Inventory.Instance.Add(kind, amount);
            if (destroyOnPickup) Destroy(gameObject);
        }
    }
}
