using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 死亡 / 任意のリスポーン要求を受けて、コアビーコン周辺の安全な位置にプレイヤーをテレポートする
    /// 静的ユーティリティ。LOS（Line of Sight）が通る位置を優先する。
    /// コアが未配置のときは false を返し、呼び出し側がフォールバック判断する。
    /// </summary>
    public static class RespawnService
    {
        const int MaxAttempts = 16;
        const float MinDistFromCore = 1.5f;
        const float MaxDistFromCore = 4f;
        const float LoSRayHeight = 0.5f;   // LOS チェックを足元より少し上から行う

        /// <summary>
        /// コア周辺にプレイヤーをリスポーンさせる。コア未配置なら false。
        /// 死亡瞬間の点群はクリアしてリセット感を出す。
        /// </summary>
        public static bool Respawn()
        {
            var fm = FieldManager.Instance;
            if (fm == null || fm.Core == null)
            {
                Debug.LogWarning("[RespawnService] コアビーコン未配置のためリスポーン不可。");
                return false;
            }

            TryGetRespawnPosition(fm.Core.transform.position, out Vector3 pos, out Quaternion rot);
            TeleportPlayer(pos, rot);

            // 死亡瞬間の世界状態を持ち越さない。
            if (SensorBus.Instance != null) SensorBus.Instance.Clear();
            if (RadarSimulator.Instance != null) RadarSimulator.Instance.ClearPending();

            return true;
        }

        /// <summary>
        /// コア起点でランダム円形にサンプリングして、LOS が通る位置を選ぶ。
        /// 試行回数を使い切ったらコア位置そのものを返すフォールバック。
        /// </summary>
        static void TryGetRespawnPosition(Vector3 corePos, out Vector3 position, out Quaternion rotation)
        {
            for (int i = 0; i < MaxAttempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float dist = Random.Range(MinDistFromCore, MaxDistFromCore);
                Vector3 candidate = corePos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                if (HasLineOfSight(candidate + Vector3.up * LoSRayHeight, corePos + Vector3.up * LoSRayHeight))
                {
                    position = candidate;
                    Vector3 toCore = corePos - candidate;
                    toCore.y = 0f;
                    rotation = toCore.sqrMagnitude > 1e-6f
                        ? Quaternion.LookRotation(toCore, Vector3.up)
                        : Quaternion.identity;
                    return;
                }
            }

            // フォールバック: 全部失敗 → コア位置そのものに置く。
            position = corePos;
            rotation = Quaternion.identity;
        }

        static bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 1e-3f) return true;
            if (!SignalsightNames.TryGetLayer(SignalsightNames.Layers.World, out int worldLayer))
                return true;  // レイヤ未定義ならチェック不能、許容して通す
            int mask = 1 << worldLayer;
            return !Physics.Raycast(from, dir / dist, dist, mask);
        }

        static void TeleportPlayer(Vector3 pos, Quaternion rot)
        {
            var playerGo = SignalsightRefs.PlayerGameObject;
            if (playerGo == null) return;

            var cc = playerGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerGo.transform.SetPositionAndRotation(pos, rot);
            if (cc != null) cc.enabled = true;

            if (CameraController.Instance != null) CameraController.Instance.ResetLook();
        }
    }
}
