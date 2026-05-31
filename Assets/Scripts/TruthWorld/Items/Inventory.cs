using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーの所持品。ビーコン / 素材 / 遺構の 3 カテゴリを共通の Slot リストで保持する。
    /// 設置操作（Phase 4）が SelectedBeaconKind を参照して「いま手に持っているビーコン」を決定する。
    /// 中身が変わったら OnChanged を発火し、HUD などの subscriber が描画更新する。
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        [Serializable]
        public class Slot
        {
            public ItemKind kind;
            public int count;
        }

        readonly List<Slot> _slots = new();
        public IReadOnlyList<Slot> Slots => _slots;

        /// <summary>選択中のビーコン kind。Phase 4 の配置入力が参照する。</summary>
        public ItemKind SelectedBeaconKind { get; private set; }

        /// <summary>Inventory の中身（スロット内容 or 選択ビーコン）が変わったときに発火。</summary>
        public event Action OnChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// アイテムを <paramref name="amount"/> 個追加。同種があれば既存スロットに加算、無ければ新スロット。
        /// ビーコンを最初に拾ったときは自動で SelectedBeaconKind に入る。
        /// </summary>
        public void Add(ItemKind kind, int amount = 1)
        {
            if (kind == null || amount <= 0) return;
            var slot = FindSlot(kind);
            if (slot != null) slot.count += amount;
            else _slots.Add(new Slot { kind = kind, count = amount });

            // 最初のビーコン取得時に自動選択。
            if (kind.Cat == ItemKind.Category.Beacon && SelectedBeaconKind == null)
                SelectedBeaconKind = kind;

            OnChanged?.Invoke();
        }

        /// <summary>
        /// アイテムを <paramref name="amount"/> 個減らす。在庫不足なら何もせず false を返す。
        /// 選択中ビーコンを使い切ったら次の有効な Beacon kind に自動切替（無ければ null）。
        /// </summary>
        public bool Remove(ItemKind kind, int amount = 1)
        {
            if (kind == null || amount <= 0) return false;
            var slot = FindSlot(kind);
            if (slot == null || slot.count < amount) return false;
            slot.count -= amount;
            if (slot.count <= 0) _slots.Remove(slot);

            if (SelectedBeaconKind == kind && CountOf(kind) == 0)
                SelectedBeaconKind = FindFirstBeaconKind();

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>指定 kind の所持数。所持なし=0。</summary>
        public int CountOf(ItemKind kind)
        {
            var slot = FindSlot(kind);
            return slot != null ? slot.count : 0;
        }

        /// <summary>
        /// 選択中ビーコンを切り替える。Beacon カテゴリ以外を渡すと無視。
        /// 在庫 0 でも選択は可能（UI でハイライト目的）。
        /// </summary>
        public void SetSelectedBeacon(ItemKind kind)
        {
            if (kind != null && kind.Cat != ItemKind.Category.Beacon) return;
            if (SelectedBeaconKind == kind) return;
            SelectedBeaconKind = kind;
            OnChanged?.Invoke();
        }

        Slot FindSlot(ItemKind kind)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].kind == kind) return _slots[i];
            return null;
        }

        ItemKind FindFirstBeaconKind()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.kind != null && s.kind.Cat == ItemKind.Category.Beacon && s.count > 0)
                    return s.kind;
            }
            return null;
        }
    }
}
