using System;
using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 合成レシピ定義。入力アイテム配列を消費して出力アイテムを生成する。
    /// Phase 9 で CraftRegistry を入れたら本 SO のリストを集約予定。
    /// </summary>
    [CreateAssetMenu(menuName = "Signalsight/Craft Recipe", fileName = "CraftRecipe")]
    public class CraftRecipe : ScriptableObject
    {
        [Serializable]
        public struct Ingredient
        {
            public ItemKind kind;
            public int amount;
        }

        [Tooltip("セーブデータでの参照に使う一意な ID。例: 'craft_normal_to_wide'")]
        [SerializeField] string id;

        [Tooltip("UI に表示する名前。日本語可。")]
        [SerializeField] string displayName;

        [Tooltip("消費するアイテム一覧。同じ ItemKind を複数行に書かないこと。")]
        [SerializeField] Ingredient[] inputs;

        [Tooltip("生成される ItemKind。")]
        [SerializeField] ItemKind output;

        [SerializeField] int outputAmount = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public Ingredient[] Inputs => inputs;
        public ItemKind Output => output;
        public int OutputAmount => outputAmount;
    }
}
