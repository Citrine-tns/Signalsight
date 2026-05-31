using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// PlacedBeacon と並列にアタッチする「拠点コア」マーカー。
    /// FieldManager に自身を登録し、RespawnService が「コア周辺の安全な位置」を計算するための
    /// 参照点になる。1 Field に 1 個だけ存在する想定（複数あれば最後に Enable された方が採用）。
    /// </summary>
    [RequireComponent(typeof(PlacedBeacon))]
    public class CoreBeacon : MonoBehaviour
    {
        void OnEnable()
        {
            if (FieldManager.Instance != null)
                FieldManager.Instance.RegisterCore(this);
        }

        void OnDisable()
        {
            if (FieldManager.Instance != null)
                FieldManager.Instance.UnregisterCore(this);
        }
    }
}
