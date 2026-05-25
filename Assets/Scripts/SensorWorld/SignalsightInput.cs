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
