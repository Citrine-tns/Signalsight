using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// プレイヤーが killY より下へ落ちたらゲームオーバーにする。床の高さは y=0。
    /// 発動は <see cref="GameOverController.Trigger"/> 経由なので、ステージクリア演出中の
    /// 無敵状態では落下してもゲームオーバーにならない。プレイヤーにアタッチする。
    /// </summary>
    public class FallDeath : MonoBehaviour
    {
        [Tooltip("この高さより下に落ちるとゲームオーバー [m]。床は y=0。")]
        [SerializeField] float killY = -10f;

        void Update()
        {
            if (transform.position.y < killY)
                GameOverController.Trigger();
        }
    }
}
