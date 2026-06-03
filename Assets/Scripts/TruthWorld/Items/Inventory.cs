using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーの所持品。ビーコン / 素材 / 遺構の 3 カテゴリを共通の Slot リストで保持する。
    /// 設置操作（Phase 4）が SelectedBeaconKind を参照して「いま手に持っているビーコン」を決定する。
    /// 中身が変わったら OnChanged を発火し、HUD などの subscriber が描画更新する。
    ///
    /// 検索は ItemKind reference で O(1)（Dictionary）。表示・列挙は List で順序保証。
    /// 2 構造を Add/Remove のたびに同期する責務は Inventory のみが持つ。
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        /// <summary>
        /// 1 スロット。kind は不変、count は Inventory のみが書き換える（nested の
        /// private setter は外側の Inventory からアクセス可能）。外部 reader は読み取り専用。
        /// </summary>
        public class Slot
        {
            public ItemKind Kind { get; }
            public int Count { get; private set; }

            public Slot(ItemKind kind, int count) { Kind = kind; Count = count; }

            internal void SetCount(int value) { Count = value; }
        }

        // 表示順序を保つ List と、O(1) 逆引き用 Dictionary を並行管理。
        readonly List<Slot> _slots = new();
        readonly Dictionary<ItemKind, Slot> _slotByKind = new();
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
            if (_slotByKind.TryGetValue(kind, out var slot))
            {
                slot.SetCount(slot.Count + amount);
            }
            else
            {
                var newSlot = new Slot(kind, amount);
                _slots.Add(newSlot);
                _slotByKind[kind] = newSlot;
            }

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
            if (!_slotByKind.TryGetValue(kind, out var slot) || slot.Count < amount) return false;
            int newCount = slot.Count - amount;
            if (newCount <= 0)
            {
                _slots.Remove(slot);
                _slotByKind.Remove(kind);
            }
            else
            {
                slot.SetCount(newCount);
            }

            if (SelectedBeaconKind == kind && CountOf(kind) == 0)
                SelectedBeaconKind = FindFirstBeaconKind();

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>指定 kind の所持数。所持なし=0。</summary>
        public int CountOf(ItemKind kind)
        {
            if (kind == null) return 0;
            return _slotByKind.TryGetValue(kind, out var s) ? s.Count : 0;
        }

        /// <summary>全スロットと選択中ビーコンをクリアする。ロード前の初期化に使う。</summary>
        public void ClearAll()
        {
            _slots.Clear();
            _slotByKind.Clear();
            SelectedBeaconKind = null;
            OnChanged?.Invoke();
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

        /// <summary>
        /// 選択中ビーコンを次の Beacon カテゴリスロット（在庫 1 以上）に巡回する。
        /// 現在の選択が見つからない場合は先頭から探索。Beacon が 1 種類しか無ければ no-op。
        /// </summary>
        public void CycleSelectedBeacon()
        {
            int n = _slots.Count;
            if (n == 0) return;

            // 現在の SelectedBeaconKind がリスト内にあれば、その次から探索開始。
            int startIdx = -1;
            for (int i = 0; i < n; i++)
                if (_slots[i].Kind == SelectedBeaconKind) { startIdx = i; break; }

            // offset=1 から n まで巡回。startIdx=-1（現在の選択が無効）なら idx=0 から始まる。
            for (int offset = 1; offset <= n; offset++)
            {
                int idx = ((startIdx + offset) % n + n) % n;
                var s = _slots[idx];
                if (s.Kind != null && s.Kind.Cat == ItemKind.Category.Beacon && s.Count > 0)
                {
                    SetSelectedBeacon(s.Kind);
                    return;
                }
            }
        }

        ItemKind FindFirstBeaconKind()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.Kind != null && s.Kind.Cat == ItemKind.Category.Beacon && s.Count > 0)
                    return s.Kind;
            }
            return null;
        }
    }
}
