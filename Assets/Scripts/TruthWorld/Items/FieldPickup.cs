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
        [Tooltip("セーブで取得済みを記録する一意 ID。例: 'beacon_normal_1', 'relic_pendulum_1'。" +
                 "空文字なら追跡しない（テスト用配置 or 永続的に拾える pickup 用）。")]
        [SerializeField] string id;

        void Reset()
        {
            // 新規アタッチ時に Collider を自動で Trigger 化しておく（物理衝突で
            // プレイヤーが弾かれる事故を防ぐ）。
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void Awake()
        {
            // ロード時にこの pickup が「取得済み」フラグ持ちなら、出現させずに自己 Destroy。
            if (string.IsNullOrEmpty(id)) return;
            if (ProgressFlags.Instance != null && ProgressFlags.Instance.Has("pickup_" + id))
                Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            // プレイヤー判定は中央レジストリの IsPlayer 経由。GetComponentInParent の
            // ツリー探索 + Player 構造変更時の不揃いリスクを回避（Conventions「中央レジストリ経由」遵守）。
            if (!SignalsightRefs.IsPlayer(other)) return;
            if (Inventory.Instance == null || kind == null) return;

            Inventory.Instance.Add(kind, amount);

            // 遺構の場合は ProgressFlags に「拾った」記録を残す。
            // RelicCounter が集計してクリア判定する。Phase 9 のセーブで永続化される。
            if (kind.Cat == ItemKind.Category.Relic && ProgressFlags.Instance != null)
                ProgressFlags.Instance.Set(RelicCounter.FlagPrefix + kind.Id);

            // セーブで取得済みを記録。ロード時に Awake でチェックして自己 Destroy する。
            if (!string.IsNullOrEmpty(id) && ProgressFlags.Instance != null)
                ProgressFlags.Instance.Set("pickup_" + id);

            if (destroyOnPickup) Destroy(gameObject);
        }
    }
}
