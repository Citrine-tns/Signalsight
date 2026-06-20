using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 任意の進行フラグ集合（HashSet&lt;string&gt; のラッパ）。
    /// MonoBehaviour シングルトン、Core シーンに常駐。Phase 9 のセーブで永続化される予定。
    /// 「遺構を取った」「ゲートを解錠した」「初回死亡」など状態の有無で表せる進行情報を全部扱う。
    /// </summary>
    public class ProgressFlags : MonoBehaviour
    {
        public static ProgressFlags Instance { get; private set; }

        readonly HashSet<string> _flags = new();
        public event Action<string> OnFlagSet;

        public IReadOnlyCollection<string> Flags => _flags;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool Has(string flag) => _flags.Contains(flag);

        /// <summary>フラグを立てる。既に立っていれば no-op。新規追加時のみ OnFlagSet 発火。</summary>
        public void Set(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (_flags.Add(flag)) OnFlagSet?.Invoke(flag);
        }

        /// <summary>全フラグをクリアする。ロード前の初期化に使う。OnFlagSet は発火しない。</summary>
        public void ClearAll()
        {
            _flags.Clear();
        }
    }
}
