using System;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// World ↔ SaveData の橋渡し。<see cref="Capture"/> で現状を SaveData に組み立て、
    /// <see cref="Restore"/> で SaveData を世界に反映する。SO の逆引きは <see cref="SaveRegistry"/> 経由。
    /// </summary>
    public static class SaveService
    {
        /// <summary>現在の世界状態を SaveData にスナップショット。Field がロード済みの状態で呼ぶ。</summary>
        public static SaveData Capture()
        {
            var data = new SaveData
            {
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };

            // ProgressFlags
            if (ProgressFlags.Instance != null)
            {
                foreach (var f in ProgressFlags.Instance.Flags)
                    data.progressFlags.Add(f);
            }

            // Inventory
            var inv = Inventory.Instance;
            if (inv != null)
            {
                foreach (var slot in inv.Slots)
                {
                    if (slot.kind == null || slot.count <= 0) continue;
                    data.inventory.Add(new InventorySlotRecord
                    {
                        itemKindId = slot.kind.Id,
                        count = slot.count,
                    });
                }
                if (inv.SelectedBeaconKind != null)
                    data.selectedBeaconItemId = inv.SelectedBeaconKind.Id;
            }

            // PlacedBeacons（Field 内に存在する PlacedBeacon を全部）
            // FieldManager の中央登録簿から取得。Field 外（Stage1/2 直接プレイ等）では FieldManager が
            // 無いので空セーブになるが、その状況でセーブを呼ぶことは想定していない。
            var fm = FieldManager.Instance;
            if (fm != null)
            {
                var all = fm.AllPlaced;
                for (int i = 0; i < all.Count; i++)
                {
                    var pb = all[i];
                    if (pb == null || pb.Kind == null) continue;
                    var item = pb.Kind.ItemKind;
                    if (item == null) continue;
                    data.placedBeacons.Add(new PlacedBeaconRecord
                    {
                        itemKindId = item.Id,
                        position = pb.transform.position,
                        rotation = pb.transform.rotation,
                    });
                }
            }

            return data;
        }

        /// <summary>
        /// Field 読込前: Inventory + ProgressFlags を復元。FieldPickup.Awake が
        /// ProgressFlags を見て自己 Destroy するため、必ずシーンロード前に呼ぶ。
        /// </summary>
        public static void RestorePreSceneLoad(SaveData data)
        {
            if (data == null) return;
            var registry = SaveRegistry.Instance;
            if (registry == null)
            {
                Debug.LogError("[SaveService] SaveRegistry が見つからない。Core シーンに配置してください。");
                return;
            }

            var inv = Inventory.Instance;
            if (inv != null)
            {
                inv.ClearAll();
                for (int i = 0; i < data.inventory.Count; i++)
                {
                    var rec = data.inventory[i];
                    var k = registry.FindItemKind(rec.itemKindId);
                    if (k != null) inv.Add(k, rec.count);
                }
                if (!string.IsNullOrEmpty(data.selectedBeaconItemId))
                {
                    var sel = registry.FindItemKind(data.selectedBeaconItemId);
                    if (sel != null) inv.SetSelectedBeacon(sel);
                }
            }

            if (ProgressFlags.Instance != null)
            {
                ProgressFlags.Instance.ClearAll();
                for (int i = 0; i < data.progressFlags.Count; i++)
                    ProgressFlags.Instance.Set(data.progressFlags[i]);
            }
        }

        /// <summary>Field 読込後: PlacedBeacons を再生成。シーンがロードされた状態で呼ぶ。</summary>
        public static void RestorePostSceneLoad(SaveData data)
        {
            if (data == null) return;
            var registry = SaveRegistry.Instance;
            if (registry == null) return;

            for (int i = 0; i < data.placedBeacons.Count; i++)
            {
                var rec = data.placedBeacons[i];
                var bkind = registry.FindBeaconKindByItemId(rec.itemKindId);
                if (bkind == null || bkind.Prefab == null) continue;
                UnityEngine.Object.Instantiate(bkind.Prefab, rec.position, rec.rotation);
            }
        }

        /// <summary>
        /// Pre/Post を順に呼ぶ互換ラッパ。Field が既にロード済みの状態で呼ぶ前提（同一 Field 内
        /// での再 Restore など）。Title→Field 経路の正規ロードでは BootManager が Pre/Post を分けて呼ぶ。
        /// </summary>
        public static void Restore(SaveData data)
        {
            RestorePreSceneLoad(data);
            RestorePostSceneLoad(data);
        }
    }
}
