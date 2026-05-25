using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>
    /// プロジェクト全体で参照されるシングルトン MonoBehaviour の弱参照レジストリ。
    /// `SignalsightNames` が「文字列の中央集約」なのに対し、こちらは「オブジェクト参照の中央集約」。
    ///
    /// 目的：
    /// - `Camera.main` を全コードから排除し、Camera 参照を 1 箇所に揃える。
    /// - Player 判定（`other.GetComponent&lt;PlayerActor&gt;()` / `== PlayerActor.Instance.gameObject`）の
    ///   散在パターンを `IsPlayer(Collider)` に集約。
    /// - Reconstruction → TruthWorld の依存禁止を維持しながら全アセンブリから読めるようにするため、
    ///   下位レイヤである SensorWorld に置く。書き込み側（CameraController / PlayerActor）が
    ///   `Awake` で publish、`OnDestroy` で自分が登録中なら null 化する。
    ///
    /// 型は object として保持し、SensorWorld からは具象 PlayerActor 型を知らずに済ませる
    /// （PlayerActor は TruthWorld 所属で、SensorWorld から見えない）。利用側で型を知っている
    /// ところはキャスト or GameObject 比較で十分。
    /// </summary>
    public static class SignalsightRefs
    {
        /// <summary>主カメラ。`CameraController.Awake` で publish される。</summary>
        public static Camera Camera { get; set; }

        /// <summary>プレイヤー本体の GameObject。`PlayerActor.Awake` で publish される。</summary>
        public static GameObject PlayerGameObject { get; set; }

        /// <summary>プレイヤー本体の Transform。位置参照のショートカット。</summary>
        public static Transform PlayerTransform => PlayerGameObject != null ? PlayerGameObject.transform : null;

        /// <summary>
        /// 渡された Collider がプレイヤー本体のものか判定する。`GetComponent` を呼ばないので
        /// per-frame パスでもアロケなし。Collider が子オブジェクト側に付いている設計に変えた場合は
        /// このメソッドを `.transform.IsChildOf(...)` 等に拡張する想定。
        /// </summary>
        public static bool IsPlayer(Collider other)
            => PlayerGameObject != null && other != null && other.gameObject == PlayerGameObject;
    }
}
