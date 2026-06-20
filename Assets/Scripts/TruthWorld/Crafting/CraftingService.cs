using UnityEngine;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 合成の実行ロジック。Inventory の在庫を見て可否を判定し、消費 + 追加を連続して行う。
    /// </summary>
    public static class CraftingService
    {
        /// <summary>このレシピを今すぐ合成できるか（在庫充足を判定）。</summary>
        /// <remarks>
        /// 同じ ItemKind を複数行に書いた不正レシピは reject する。重複行があると
        /// CountOf がスロット 1 個に対して個別判定されるため CanCraft が嘘の true を返し、
        /// TryCraft 中で Remove が失敗して在庫が中途半端に消費される。
        /// CraftRecipe コメントの運用ルールをコード側で防御。
        /// </remarks>
        public static bool CanCraft(CraftRecipe recipe, Inventory inv)
        {
            if (recipe == null || inv == null || recipe.Output == null) return false;
            var inputs = recipe.Inputs;
            if (inputs == null) return false;

            for (int i = 0; i < inputs.Length; i++)
            {
                var ing = inputs[i];
                if (ing.kind == null || ing.amount <= 0) return false;
                // 重複 kind 検出（inputs は通常 2-4 行で O(N²) は無視できる）。
                for (int j = i + 1; j < inputs.Length; j++)
                {
                    if (inputs[j].kind == ing.kind)
                    {
                        Debug.LogError($"[CraftingService] Recipe '{recipe.Id}' に同じ ItemKind '{ing.kind.Id}' が複数行に存在。合成不可。", recipe);
                        return false;
                    }
                }
                if (inv.CountOf(ing.kind) < ing.amount) return false;
            }
            return true;
        }

        /// <summary>
        /// レシピに従って合成を実行する。可否を内部で判定し、不可なら何もせず false を返す。
        /// 在庫の Remove と Add は連続して行い、外から見て中途半端な状態が観測されないようにする。
        /// </summary>
        public static bool TryCraft(CraftRecipe recipe, Inventory inv)
        {
            if (!CanCraft(recipe, inv)) return false;

            var inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Length; i++)
                inv.Remove(inputs[i].kind, inputs[i].amount);

            inv.Add(recipe.Output, recipe.OutputAmount);
            return true;
        }
    }
}
