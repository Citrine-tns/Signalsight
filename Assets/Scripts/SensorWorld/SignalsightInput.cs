using UnityEngine;

namespace Signalsight.SensorWorld
{
    /// <summary>
    /// プロジェクト全体の入力アクセスを 1 箇所に集約する中央ラッパ。
    /// `SignalsightActions` は隣の `.inputactions` アセットから Unity が自動生成する partial class。
    ///
    /// 使い方：
    ///   if (SignalsightInput.Player.Jump.WasPressedThisFrame()) ...
    ///   Vector2 move = SignalsightInput.Player.Move.ReadValue&lt;Vector2&gt;();
    ///
    /// `Keyboard.current` / `Mouse.current` / `Gamepad.current` の直叩きはこの集約により
    /// プロジェクトから完全排除する。デバイス分岐や VR 等への拡張は `.inputactions` 側で行う。
    ///
    /// ライフサイクル：
    /// - 初回アクセスで `SignalsightActions` インスタンスを生成し Player マップを Enable。
    /// - 同じ初回アクセス時に `Application.quitting` を購読し、終了時に明示的に Disable + Dispose。
    ///   これを怠ると `SignalsightActions` の finalizer が
    ///   "SignalsightActions.Player.Disable() has not been called." の Assert を吐く。
    /// - Domain Reload で静的フィールドがクリアされるため次プレイで再生成される。
    /// </summary>
    public static class SignalsightInput
    {
        static SignalsightActions _actions;

        public static SignalsightActions.PlayerActions Player => Get().Player;

        /// <summary>
        /// 全プレイヤー入力を一括で遮断するフラグ。「世界は動いているがプレイヤーは介入できない」
        /// 状態（ステージ入場バナー表示中・タイトルの待機中など）で true にする。
        ///
        /// 規約：入力を読む側（PlayerActor / PlayerController / CameraController / Beacon の
        /// Activate 入力部）は Update 冒頭で `if (SignalsightInput.Locked) return;` でガードする。
        /// 例外：
        /// - PlayerController の重力・CharacterController.Move は Locked 中も継続（足元が抜けないため）
        /// - Beacon の起動済み定期 scan は継続（Title の auto-scan で文字を浮かび上がらせるため）
        /// - GameOverController.Restart は GameOver 中は Locked を無視（リスタート出口は常時開ける）
        /// - TitleController は自分で Locked を見ずに Enter を直接読む（待機解除後の唯一の入力経路）
        ///
        /// Core 再ロード（GameOver→R）でも static は持ち越されるため、Restart 経路で明示的に false に
        /// 戻してから LoadScene("Core") する。
        /// </summary>
        public static bool Locked { get; set; }

        static SignalsightActions Get()
        {
            if (_actions == null)
            {
                _actions = new SignalsightActions();
                _actions.Player.Enable();
                Application.quitting += OnApplicationQuitting;
            }
            return _actions;
        }

        static void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;
            if (_actions == null) return;
            _actions.Player.Disable();
            _actions.Dispose();
            _actions = null;
        }
    }
}
