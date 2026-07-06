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

            // 遺構の場合は「一度でも入手した」フラグを per-pickup id で立てる。
            // RelicCounter がこれを集計して進行状況を出す。合成で遺構を消費しても
            // フラグはそのまま残り、入手歴は維持される (= ALL CLEAR は永続)。
            // id 必須: 同じ ItemKind の遺構をマップに 5 個点在させたとき、kind.Id では
            // 1 つにまとまって 1 個分しか数えられない。pickup 個別の id で初めて
            // 「点在 5 個を全部入手」が正しく判定できる。
            if (kind.Cat == ItemKind.Category.Relic && ProgressFlags.Instance != null)
            {
                if (string.IsNullOrEmpty(id))
                    Debug.LogWarning("[FieldPickup] Relic カテゴリの pickup に id が未設定。" +
                                     "RelicCounter で集計されない。pickup 個別の一意 id を Inspector で設定すること。", this);
                else
                    ProgressFlags.Instance.Set(RelicCounter.FlagPrefix + id);
            }

            // セーブで取得済みを記録。ロード時に Awake でチェックして自己 Destroy する。
            if (!string.IsNullOrEmpty(id) && ProgressFlags.Instance != null)
                ProgressFlags.Instance.Set("pickup_" + id);

            if (destroyOnPickup) Destroy(gameObject);
        }
    }
}
