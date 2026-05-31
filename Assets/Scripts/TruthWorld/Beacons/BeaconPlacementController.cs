using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーの手元↔Field の間でビーコンを出し入れする。プレイヤー GameObject 配下に置く。
    ///   - PlaceBeacon: Inventory の選択中ビーコンを前方に Instantiate（単押し）
    ///   - RecoverBeacon: プレイヤー周辺の最近接 PlacedBeacon を Destroy して Inventory に戻す
    /// 長押しカーソル設置は後の Phase で追加予定。
    /// </summary>
    public class BeaconPlacementController : MonoBehaviour
    {
        [Tooltip("単押し時にプレイヤー前方どこに置くか [m]。")]
        [SerializeField] float placeDistance = 1.5f;

        [Tooltip("配置位置の Y 軸オフセット [m]（足元基準）。ScanProfile.Default の上下スラブ展開が " +
                 "±0.9m なので、0.9 で「足元から 1.8m 弱までスキャンが届く（人間身長相当）」を満たす。")]
        [SerializeField] float placeHeightOffset = 0.9f;

        [Tooltip("RecoverBeacon 入力で回収できる最大距離 [m]。プレイヤーからこの距離内の最近接ビーコンを回収。")]
        [SerializeField] float recoverRange = 3f;

        // 中央参照から Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        CharacterController _cc;
        Camera _cam;

        void Start()
        {
            _cc = GetComponent<CharacterController>();
            _cam = SignalsightRefs.Camera;
        }

        void Update()
        {
            if (SignalsightInput.Locked) return;
            if (SignalsightInput.Player.PlaceBeacon.WasPressedThisFrame()) TryPlace();
            if (SignalsightInput.Player.RecoverBeacon.WasPressedThisFrame()) TryRecover();
            if (SignalsightInput.Player.CycleBeacon.WasPressedThisFrame())
            {
                if (Inventory.Instance != null) Inventory.Instance.CycleSelectedBeacon();
            }
        }

        void TryPlace()
        {
            var inv = Inventory.Instance;
            if (inv == null || inv.SelectedBeaconKind == null) return;

            var itemKind = inv.SelectedBeaconKind;
            var beaconKind = itemKind.BeaconKind;
            if (beaconKind == null || beaconKind.Prefab == null)
            {
                Debug.LogWarning("[BeaconPlacementController] 選択 ItemKind に BeaconKind または Prefab が未設定。", this);
                return;
            }
            if (inv.CountOf(itemKind) <= 0) return;

            // カメラの forward を XZ 平面に投影して「見ている方向の水平前方」を取る。
            // FirstPerson ではカメラ = プレイヤー視線で自然。TopDownOrtho は下向きで投影が
            // ほぼ 0 になるので、フォールバックで player.transform.forward を使う。
            Vector3 fwd = _cam != null ? _cam.transform.forward : transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = transform.forward;
            else fwd.Normalize();

            // プレイヤー足元の Y を基準に offset 分上に置く。pivot が中心でも足元でも
            // CharacterController の center / height から足元を一意に求められる。
            float feetY = _cc != null
                ? transform.position.y + _cc.center.y - _cc.height * 0.5f
                : transform.position.y;
            Vector3 position = new Vector3(
                transform.position.x + fwd.x * placeDistance,
                feetY + placeHeightOffset,
                transform.position.z + fwd.z * placeDistance);

            if (!CanPlace(position, beaconKind))
            {
                Debug.Log("[BeaconPlacementController] 同種ビーコンが近すぎるため配置不可。", this);
                return;
            }

            Instantiate(beaconKind.Prefab, position, Quaternion.LookRotation(fwd, Vector3.up));
            inv.Remove(itemKind, 1);
        }

        bool CanPlace(Vector3 position, BeaconKind kind)
        {
            float minDistSqr = kind.MinDistanceToSameKind * kind.MinDistanceToSameKind;
            var existing = FindObjectsByType<PlacedBeacon>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                var pb = existing[i];
                if (pb == null || pb.Kind != kind) continue;
                Vector3 diff = pb.transform.position - position;
                if (diff.sqrMagnitude < minDistSqr) return false;
            }
            return true;
        }

        void TryRecover()
        {
            var inv = Inventory.Instance;
            if (inv == null) return;

            var target = FindClosestPlacedBeacon();
            if (target == null) return;

            var beaconKind = target.Kind;
            if (beaconKind == null || beaconKind.ItemKind == null)
            {
                Debug.LogWarning("[BeaconPlacementController] PlacedBeacon の Kind または ItemKind 参照が未設定で回収不可。", target);
                return;
            }

            inv.Add(beaconKind.ItemKind, 1);
            Destroy(target.gameObject);
        }

        PlacedBeacon FindClosestPlacedBeacon()
        {
            var all = FindObjectsByType<PlacedBeacon>(FindObjectsSortMode.None);
            PlacedBeacon closest = null;
            float maxSqr = recoverRange * recoverRange;
            float closestSqr = float.MaxValue;
            Vector3 myPos = transform.position;
            for (int i = 0; i < all.Length; i++)
            {
                var pb = all[i];
                if (pb == null) continue;
                float distSqr = (pb.transform.position - myPos).sqrMagnitude;
                if (distSqr > maxSqr) continue;
                if (distSqr < closestSqr) { closest = pb; closestSqr = distSqr; }
            }
            return closest;
        }
    }
}
