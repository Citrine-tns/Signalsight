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
            // 世界停止中（ステージ入場 banner / GameOver）は落下判定もスキップ。
            // 位置比較 `position.y < killY` は deltaTime に依存しないので timeScale=0 だけでは
            // 止まらない（フレーム毎に評価される）。これがないと banner 直前に転落してた場合に
            // banner 中に GameOver が走る。
            if (Time.timeScale == 0f) return;
            if (transform.position.y < killY)
                GameOverController.Trigger();
        }
    }
}
