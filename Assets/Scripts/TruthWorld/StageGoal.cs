using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 到達するとステージクリアになるゴール。ステージ Scene に置く。
    /// プレイヤーが reachRange 以内に入ると StageManager に次ステージへの遷移を依頼する。
    /// 判定は 3 次元直線距離なので、階段の上など高さの違う位置に置いても正しく機能する。
    /// </summary>
    public class StageGoal : MonoBehaviour
    {
        [Tooltip("プレイヤーがこの距離以内に入るとクリア [m]（3 次元直線距離）。")]
        [SerializeField] float reachRange = 2f;

        bool _reached;
        // 中央レジストリから Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        Transform _playerT;

        void Start()
        {
            _playerT = SignalsightRefs.PlayerTransform;
        }

        void Update()
        {
            if (_reached) return;
            if (_playerT == null) return;

            Vector3 d = _playerT.position - transform.position;
            if (d.sqrMagnitude > reachRange * reachRange) return;

            _reached = true;
            if (StageManager.Instance != null)
                StageManager.Instance.StageCleared();
        }
    }
}
