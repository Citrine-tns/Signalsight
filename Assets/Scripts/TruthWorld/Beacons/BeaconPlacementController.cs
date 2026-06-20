using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーの手元↔Field の間でビーコンを出し入れする。プレイヤー GameObject 配下に置く。
    ///   - PlaceBeacon: Inventory の選択中ビーコンを前方に Instantiate（単押し）。
    ///     設置位置が壁/床にめり込む or 下に床が無い場合は配置不可。
    ///   - RecoverBeacon: 視点中央レティクルで捉えた PlacedBeacon を Destroy して Inventory に戻す。
    ///     敵越しでも回収可能（敵は raycast 透過）、壁の向こうは不可（World レイヤが遮蔽）。
    ///   - CycleBeacon: 所持ビーコンの選択切替。
    ///   - OnGUI で画面中央に「+」レティクルを描画。
    /// 長押しカーソル設置は後の Phase で追加予定。
    /// </summary>
    public class BeaconPlacementController : MonoBehaviour
    {
        [Tooltip("単押し時にプレイヤー前方どこに置くか [m]。")]
        [SerializeField] float placeDistance = 1.5f;

        [Tooltip("配置位置の Y 軸オフセット [m]（足元基準）。ScanProfile.Default の上下スラブ展開が " +
                 "±0.9m なので、0.9 で「足元から 1.8m 弱までスキャンが届く（人間身長相当）」を満たす。")]
        [SerializeField] float placeHeightOffset = 0.9f;

        [Tooltip("ビーコン全種で共通の最小設置間隔 [m]。種類が違っても密集禁止。")]
        [SerializeField] float globalMinDistance = 2f;

        // 回収レイ用定数。レティクル = カメラ前方の理想的な線（半径 0）。
        const float MaxRecoverRange = 100f;     // 実質無制限。安全のため上限

        // 設置可否チェック用定数。
        const float PlacementClearanceRadius = 0.5f;   // 設置位置の壁めり込み判定の球半径
        const float FloorCheckDistance = 5f;           // 直下これより遠くに床が無ければ不可

        // 中央参照から Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        CharacterController _cc;
        Camera _cam;
        GUIStyle _reticleStyle;

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
            // 1) コア限定: Field に既にコアがあれば新規コアは置けない。先に回収させる。
            if (kind.IsCore && FieldManager.Instance != null && FieldManager.Instance.Core != null)
            {
                Debug.Log("[BeaconPlacementController] コアは Field に 1 個までしか置けない。先に既存コアを回収してください。", this);
                return false;
            }

            // 2) 全種共通の密集禁止: kind を問わず globalMinDistance 以内に既存 PlacedBeacon があれば不可。
            // FieldManager の中央登録簿から取得。FieldManager が無ければチェック省略（Stage1/2 互換）。
            float minDistSqr = globalMinDistance * globalMinDistance;
            var fm = FieldManager.Instance;
            if (fm != null)
            {
                var existing = fm.AllPlaced;
                for (int i = 0; i < existing.Count; i++)
                {
                    var pb = existing[i];
                    if (pb == null) continue;
                    Vector3 diff = pb.transform.position - position;
                    if (diff.sqrMagnitude < minDistSqr) return false;
                }
            }

            if (!SignalsightNames.TryGetLayer(SignalsightNames.Layers.World, out int worldLayer))
                return true;  // World レイヤ未定義は素通り（テスト環境保護）。
            int worldMask = 1 << worldLayer;

            // 3) 壁・床との重なりチェック: position 周辺に World ジオメトリがあれば壁の中。
            if (Physics.CheckSphere(position, PlacementClearanceRadius, worldMask, QueryTriggerInteraction.Ignore))
            {
                Debug.Log("[BeaconPlacementController] 壁/床にめり込むため配置不可。", this);
                return false;
            }

            // 4) 床の有無チェック: 直下に World 床があるか。崖の先や穴の上には置けない。
            if (!Physics.Raycast(position, Vector3.down, FloorCheckDistance, worldMask, QueryTriggerInteraction.Ignore))
            {
                Debug.Log("[BeaconPlacementController] 下に床が無いため配置不可。", this);
                return false;
            }

            return true;
        }

        void TryRecover()
        {
            var inv = Inventory.Instance;
            if (inv == null) return;

            var target = FindAimedBeacon();
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

        /// <summary>
        /// 画面中央レティクル = カメラ前方の理想的な「線」で当たった PlacedBeacon を返す。
        /// Raycast (半径 0) なので、aim cone のような角度の緩みは無く、コライダーに正確に
        /// 当てる必要がある（遠くの別ビーコンを誤回収するリスクなし）。
        /// 距離順に手前から判定し：
        ///   - PlayerActor（自分）は無視
        ///   - PlacedBeacon に当たれば即返す = 回収対象確定
        ///   - 敵 (EnemyAI) は透過してビーコン捜索を続ける（敵越しでも回収可）
        ///   - それ以外（壁/床など）に当たれば遮蔽として打ち切り
        /// </summary>
        PlacedBeacon FindAimedBeacon()
        {
            if (_cam == null) return null;

            Vector3 origin = _cam.transform.position;
            Vector3 dir = _cam.transform.forward;

            var hits = Physics.RaycastAll(origin, dir, MaxRecoverRange,
                                           ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return null;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null) continue;

                // 自分（プレイヤー本体の CharacterController など）は無視。
                if (col.GetComponentInParent<PlayerActor>() != null) continue;

                var pb = col.GetComponentInParent<PlacedBeacon>();
                if (pb != null) return pb;

                // 敵は透過してビーコン捜索を続ける（敵越しにも回収可能）。
                if (col.GetComponentInParent<EnemyAI>() != null) continue;

                // それ以外（壁・床など）は遮蔽として打ち切り。
                return null;
            }
            return null;
        }

        void OnGUI()
        {
            if (_reticleStyle == null)
            {
                _reticleStyle = new GUIStyle
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _reticleStyle.normal.textColor = Color.white;
            }
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.Label(new Rect(cx - 10f, cy - 10f, 20f, 20f), "+", _reticleStyle);
        }
    }
}
