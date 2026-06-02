using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// SaveAtCore 入力（F5 想定）でセーブを発行する単純なハンドラ。Player GameObject 配下に置く。
    /// MVP は位置制限なし（コア隣接チェックは Phase 9c 以降）。
    /// </summary>
    public class SaveInputHandler : MonoBehaviour
    {
        void Update()
        {
            if (SignalsightInput.Locked) return;
            if (!SignalsightInput.Player.SaveAtCore.WasPressedThisFrame()) return;

            var data = SaveService.Capture();
            if (SaveSystem.Save(data))
                Debug.Log($"[SaveInputHandler] セーブ完了: " +
                          $"items={data.inventory.Count}, beacons={data.placedBeacons.Count}, " +
                          $"flags={data.progressFlags.Count}");
            else
                Debug.LogError("[SaveInputHandler] セーブ失敗");
        }
    }
}
