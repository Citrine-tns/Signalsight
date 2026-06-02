using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// セーブ/ロード時に SO を ID で逆引きするための登録簿。Core シーンに常駐させる。
    /// Inspector から ItemKind / BeaconKind の SO を配列に drag & drop して登録する。
    /// 種類が増えたら配列を更新する。Phase 9 MVP は手動登録、自動収集は将来。
    /// 内部は Awake 時に Dictionary 化して O(1) 逆引きにする（線形探索を避ける）。
    /// </summary>
    public class SaveRegistry : MonoBehaviour
    {
        public static SaveRegistry Instance { get; private set; }

        [Tooltip("ロード時に ID 逆引きする全 ItemKind。新規追加したら必ずここに登録。")]
        [SerializeField] ItemKind[] itemKinds;

        [Tooltip("ロード時に ID 逆引きする全 BeaconKind。各 BeaconKind は ItemKind 参照経由で逆引きされる。")]
        [SerializeField] BeaconKind[] beaconKinds;

        readonly Dictionary<string, ItemKind> _itemById = new();
        readonly Dictionary<string, BeaconKind> _beaconByItemId = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildLookup();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildLookup()
        {
            _itemById.Clear();
            _beaconByItemId.Clear();
            if (itemKinds != null)
                for (int i = 0; i < itemKinds.Length; i++)
                {
                    var k = itemKinds[i];
                    if (k != null && !string.IsNullOrEmpty(k.Id)) _itemById[k.Id] = k;
                }
            if (beaconKinds != null)
                for (int i = 0; i < beaconKinds.Length; i++)
                {
                    var bk = beaconKinds[i];
                    if (bk == null || bk.ItemKind == null) continue;
                    var id = bk.ItemKind.Id;
                    if (!string.IsNullOrEmpty(id)) _beaconByItemId[id] = bk;
                }
        }

        /// <summary>ID 文字列から ItemKind を逆引き。未登録なら null。</summary>
        public ItemKind FindItemKind(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _itemById.TryGetValue(id, out var k) ? k : null;
        }

        /// <summary>ItemKind.Id から対応する BeaconKind を逆引き。未登録なら null。</summary>
        public BeaconKind FindBeaconKindByItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return _beaconByItemId.TryGetValue(itemId, out var bk) ? bk : null;
        }
    }
}
