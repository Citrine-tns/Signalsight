using UnityEngine;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 合成 UI（IMGUI 版）。OpenCrafting 入力で開閉し、表示中は menu の中の Craft ボタンで
    /// CraftingService.TryCraft を呼ぶ。Phase 10 で UI Toolkit に置き換え予定。
    /// 場所はどこでも合成可能なので Core シーンに常駐させる。
    /// </summary>
    public class CraftingMenu : MonoBehaviour
    {
        [Tooltip("利用可能なレシピ一覧。Inspector で drag & drop。")]
        [SerializeField] CraftRecipe[] recipes;
        [Tooltip("menu パネルの左上の正規化位置（0=左上, 1=右下）。")]
        [SerializeField] Vector2 anchorNormalized = new Vector2(0.3f, 0.2f);
        [SerializeField] float panelWidth = 480f;
        [SerializeField] float rowHeight = 36f;

        bool _open;
        GUIStyle _headerStyle;
        GUIStyle _rowStyle;
        GUIStyle _buttonStyle;

        void Update()
        {
            if (SignalsightInput.Locked) return;
            if (SignalsightInput.Player.OpenCrafting.WasPressedThisFrame())
                _open = !_open;
        }

        void OnGUI()
        {
            if (!_open) return;
            if (recipes == null || recipes.Length == 0) return;
            EnsureStyles();

            float x = anchorNormalized.x * Screen.width;
            float y = anchorNormalized.y * Screen.height;
            float panelHeight = rowHeight * (recipes.Length + 1) + 16f;

            // 背景パネル（半透明黒）。
            Color old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none);
            GUI.color = old;

            GUI.Label(new Rect(x + 8f, y + 4f, panelWidth - 16f, rowHeight), "合成メニュー", _headerStyle);

            var inv = Inventory.Instance;
            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i];
                if (r == null) continue;

                float rowY = y + rowHeight * (i + 1) + 8f;
                bool can = CraftingService.CanCraft(r, inv);

                GUI.color = can ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                GUI.Label(new Rect(x + 8f, rowY, panelWidth - 120f, rowHeight), r.DisplayName, _rowStyle);
                GUI.color = old;

                if (GUI.Button(new Rect(x + panelWidth - 108f, rowY + 4f, 100f, rowHeight - 8f),
                    can ? "Craft" : "材料不足", _buttonStyle))
                {
                    if (can && CraftingService.TryCraft(r, inv))
                        Debug.Log($"[CraftingMenu] {r.DisplayName} 合成成功", this);
                }
            }
        }

        void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _headerStyle.normal.textColor = Color.white;
            _rowStyle = new GUIStyle { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            _rowStyle.normal.textColor = Color.white;
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }
    }
}
