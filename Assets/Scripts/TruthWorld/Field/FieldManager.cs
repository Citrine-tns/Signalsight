using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Field シーンに常駐する管理 singleton。現状はコアビーコンの追跡が主責務。
    /// Phase 9 以降でセーブ/ロードのフック（マップ可変オブジェクトのスナップショット作成）を足す予定。
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class FieldManager : MonoBehaviour
    {
        public static FieldManager Instance { get; private set; }

        /// <summary>現在登録されているコアビーコン。配置されていなければ null。</summary>
        public CoreBeacon Core { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
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
    }
}
