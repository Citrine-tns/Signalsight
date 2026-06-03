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
        /// <summary>
        /// 現在の世界状態を SaveData にスナップショット。Core 常駐分（ProgressFlags / Inventory）と
        /// Field 常駐分（PlacedBeacons）の 2 ステップで埋める。新規セーブ項目を足すときは
        /// 「Core or Field のどちらに属するか」で <see cref="CaptureCore"/> / <see cref="CaptureField"/>
        /// のどちらに行を足すかを決める。
        /// </summary>
        public static SaveData Capture()
        {
            var data = new SaveData
            {
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            CaptureCore(data);
            CaptureField(data);
            return data;
        }

        /// <summary>Core シーン常駐の状態（ProgressFlags / Inventory）を data に書き込む。</summary>
        static void CaptureCore(SaveData data)
        {
            // ProgressFlags
            if (ProgressFlags.Instance != null)
            {
                foreach (var f in ProgressFlags.Instance.Flags)
                    data.progressFlags.Add(f);
            }

            // Inventory
            var inv = Inventory.Instance;
            if (inv == null) return;
            foreach (var slot in inv.Slots)
            {
                if (slot.Kind == null || slot.Count <= 0) continue;
                data.inventory.Add(new InventorySlotRecord
                {
                    itemKindId = slot.Kind.Id,
                    count = slot.Count,
                });
            }
            if (inv.SelectedBeaconKind != null)
                data.selectedBeaconItemId = inv.SelectedBeaconKind.Id;
        }

        /// <summary>
        /// Field シーン常駐の状態（PlacedBeacons）を data に書き込む。FieldManager が
        /// 居なければ Field 外なので no-op（Stage1/2 直プレイ等を想定しない呼び出しは空セーブ）。
        /// </summary>
        static void CaptureField(SaveData data)
        {
            var fm = FieldManager.Instance;
            if (fm == null) return;
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
