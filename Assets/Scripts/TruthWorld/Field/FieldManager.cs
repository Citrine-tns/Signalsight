using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Field シーンに常駐する管理 singleton。コアビーコン追跡、全 PlacedBeacon の登録簿、
    /// StageSpawn のキャッシュなど「Field 内で何度も探したくなる参照」を集約する。
    /// 散在していた FindObjectsByType / FindFirstObjectByType をここで一元化することで
    /// ホットパス（敵 Tick / セーブ / リスポーン）からシーン全走査を排除する。
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class FieldManager : MonoBehaviour
    {
        public static FieldManager Instance { get; private set; }

        /// <summary>現在登録されているコアビーコン。配置されていなければ null。</summary>
        public CoreBeacon Core { get; private set; }

        // PlacedBeacon の登録簿。各 PlacedBeacon が OnEnable/OnDisable で出入りする。
        readonly List<PlacedBeacon> _placed = new();
        public IReadOnlyList<PlacedBeacon> AllPlaced => _placed;

        // Field シーンに 1 つだけ存在する StageSpawn を Awake でキャッシュ。
        StageSpawn _spawn;
        public StageSpawn Spawn => _spawn;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // 同じ Field シーンの StageSpawn を 1 回だけ検索してキャッシュ。
            // FindFirstObjectByType は Transform を持つ既存オブジェクトを返すので、
            // StageSpawn 側に Awake が無くても問題なし。
            _spawn = FindFirstObjectByType<StageSpawn>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>CoreBeacon.OnEnable から呼ばれて自身を登録。複数検出時は警告 + 最新採用。</summary>
        public void RegisterCore(CoreBeacon core)
        {
            if (Core != null && Core != core)
                Debug.LogWarning("[FieldManager] CoreBeacon が複数検出されました。最新を採用します。", core);
            Core = core;
        }

        /// <summary>CoreBeacon.OnDisable から呼ばれて登録解除。</summary>
        public void UnregisterCore(CoreBeacon core)
        {
            if (Core == core) Core = null;
        }

        /// <summary>PlacedBeacon.OnEnable から呼ばれて登録簿に追加。</summary>
        /// <remarks>
        /// 重複チェックなし。Unity の OnEnable/OnDisable が対であることに依存。
        /// PlacedBeacon が OnDisable を経ずに OnEnable を二重実行することは通常無いため、
        /// 防御的 Contains は per-Add O(N) コストに見合わない。
        /// </remarks>
        public void RegisterPlaced(PlacedBeacon pb)
        {
            if (pb == null) return;
            _placed.Add(pb);
        }

        /// <summary>PlacedBeacon.OnDisable から呼ばれて登録簿から外す。</summary>
        public void UnregisterPlaced(PlacedBeacon pb)
        {
            if (pb == null) return;
            _placed.Remove(pb);
        }
    }
}
