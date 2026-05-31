using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 死亡 / 任意のリスポーン要求を受けてプレイヤーをテレポートする静的ユーティリティ。
    /// 復帰先の優先順位は (1) コア周辺で地面 + LOS が通る位置 → (2) Field の StageSpawn → (3) 不能。
    /// 死亡瞬間の点群はクリアしてリセット感を出す。
    /// </summary>
    public static class RespawnService
    {
        const int MaxAttempts = 16;
        const float MinDistFromCore = 1.5f;
        const float MaxDistFromCore = 4f;
        const float LoSRayHeight = 0.3f;        // LOS チェック高さ（低い壁にも遮られるよう床に近く）
        const float GroundProbeHeight = 3f;     // 地面 Raycast 開始位置の上空高さ
        const float GroundProbeMaxDist = 6f;    // 地面検出の最大距離（無ければ落とし穴扱い）
        const float SpawnHeightAboveGround = 2.0f;  // 地面接地から少し浮かせて clipping 防止
        const float MaxVerticalDelta = 2.0f;        // コアと候補の Y 差ガード（穴底/別フロア封じ）
        const float ClearanceCheckRadius = 0.5f;    // プレイヤー胴体の概算半径
        const float ClearanceCheckHeight = 1.0f;    // 胴体中心の床からの高さ（腰位置）

        /// <summary>
        /// プレイヤーをリスポーンさせる。コアがあればコア周辺、無ければ StageSpawn にフォールバック。
        /// どちらも見つからなければ false（呼び出し側で別フォールバックを判断）。
        /// </summary>
        public static bool Respawn()
        {
            var fm = FieldManager.Instance;
            if (fm != null && fm.Core != null)
            {
                if (!TryGetCoreRespawnPosition(fm.Core.transform.position, out Vector3 pos, out Quaternion rot))
                    Debug.LogWarning("[RespawnService] 適切なコア周辺位置が見つからず、コア位置そのものにフォールバック。", fm);
                ApplyRespawn(pos, rot);
                return true;
            }

            // コア未配置（プレイヤーが回収中など）: Field の StageSpawn に戻す。
            var spawn = Object.FindFirstObjectByType<StageSpawn>();
            if (spawn != null)
            {
                Debug.Log("[RespawnService] コア未配置のため StageSpawn にリスポーン。");
                ApplyRespawn(spawn.transform.position, spawn.transform.rotation);
                return true;
            }

            Debug.LogWarning("[RespawnService] コアも StageSpawn も無いためリスポーン不可。");
            return false;
        }

        /// <summary>テレポート + 死亡瞬間の点群/保留ヒットをクリアする共通処理。</summary>
        static void ApplyRespawn(Vector3 pos, Quaternion rot)
        {
            TeleportPlayer(pos, rot);
            if (SensorBus.Instance != null) SensorBus.Instance.Clear();
            if (RadarSimulator.Instance != null) RadarSimulator.Instance.ClearPending();
        }

        /// <summary>
        /// コア起点でランダム円形にサンプリングして、地面 + LOS が通る位置を探す。
        /// 試行が全部失敗したらコア位置そのものを返し、false で「フォールバックを使った」と通知。
        /// </summary>
        static bool TryGetCoreRespawnPosition(Vector3 corePos, out Vector3 position, out Quaternion rotation)
        {
            if (!SignalsightNames.TryGetLayer(SignalsightNames.Layers.World, out int worldLayer))
            {
                position = corePos;
                rotation = Quaternion.identity;
                return false;
            }
            int worldMask = 1 << worldLayer;

            for (int i = 0; i < MaxAttempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float dist = Random.Range(MinDistFromCore, MaxDistFromCore);
                Vector3 horizontal = corePos + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                // 1) 地面 Raycast: 候補上空から下に撃つ、地面ヒットが取れなければ落とし穴/空中なので skip。
                Vector3 rayStart = horizontal + Vector3.up * GroundProbeHeight;
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit groundHit, GroundProbeMaxDist, worldMask))
                    continue;

                // 2) コアとの高低差ガード: 落とし穴の底や別フロアに飛ばさない。
                if (Mathf.Abs(groundHit.point.y - corePos.y) > MaxVerticalDelta)
                    continue;

                // 3) 壁との重なりガード: 候補位置に壁ジオメトリが重なっていれば壁の中に湧くので skip。
                if (Physics.CheckSphere(
                        groundHit.point + Vector3.up * ClearanceCheckHeight,
                        ClearanceCheckRadius,
                        worldMask,
                        QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 candidate = groundHit.point + Vector3.up * SpawnHeightAboveGround;

                // 4) LOS チェック: 地面 + LoSRayHeight の低い位置で水平に結ぶ。candidate の elevation には
                // 巻き込まれないので、低めの壁にも確実に遮られる。
                Vector3 losFrom = groundHit.point + Vector3.up * LoSRayHeight;
                Vector3 losTo = new Vector3(corePos.x, losFrom.y, corePos.z);
                if (!HasLineOfSightOnMask(losFrom, losTo, worldMask))
                    continue;

                position = candidate;
                Vector3 toCore = corePos - candidate;
                toCore.y = 0f;
                rotation = toCore.sqrMagnitude > 1e-6f
                    ? Quaternion.LookRotation(toCore, Vector3.up)
                    : Quaternion.identity;
                return true;
            }

            // 全試行失敗: コア位置にフォールバック。
            position = corePos;
            rotation = Quaternion.identity;
            return false;
        }

        static bool HasLineOfSightOnMask(Vector3 from, Vector3 to, int mask)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 1e-3f) return true;
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
