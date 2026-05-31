using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Inventory の中身を画面に表示する IMGUI ベースの最小 HUD。
    /// Phase 10 で UI Toolkit ベースに置き換え予定。現状は最小限：所持品リストの
    /// 縦並びと、選択中ビーコンのハイライト。色は ItemKind.Color から取る。
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Tooltip("HUD 左上角の正規化位置（0=左上, 1=右下）。")]
        [SerializeField] Vector2 anchorNormalized = new Vector2(0.02f, 0.05f);
        [SerializeField] int fontSize = 18;
        [SerializeField] float slotHeight = 28f;
        [SerializeField] float slotWidth = 240f;

        GUIStyle _labelStyle;
        GUIStyle _selectedStyle;

        void OnGUI()
        {
            if (Inventory.Instance == null) return;
            EnsureStyles();

            var slots = Inventory.Instance.Slots;
            var selected = Inventory.Instance.SelectedBeaconKind;

            float x = anchorNormalized.x * Screen.width;
            float y = anchorNormalized.y * Screen.height;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.kind == null) continue;

                bool isSelected = slot.kind == selected;
                var style = isSelected ? _selectedStyle : _labelStyle;

                // ItemKind.Color で文字色をティント。style.normal.textColor を白にしておくと
                // GUI.color との乗算で純粋にカラーが反映される。
                Color old = GUI.color;
                GUI.color = slot.kind.Color;
                string text = isSelected
                    ? $"▶ {slot.kind.DisplayName} x{slot.count}"
                    : $"  {slot.kind.DisplayName} x{slot.count}";
                GUI.Label(new Rect(x, y + i * slotHeight, slotWidth, slotHeight), text, style);
                GUI.color = old;
            }
        }

        void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
            };
            _labelStyle.normal.textColor = Color.white;

            _selectedStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold,
            };
        }
    }
}
