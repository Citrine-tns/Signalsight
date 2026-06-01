namespace Signalsight.TruthWorld
{
    /// <summary>
    /// 合成の実行ロジック。Inventory の在庫を見て可否を判定し、消費 + 追加を連続して行う。
    /// </summary>
    public static class CraftingService
    {
        /// <summary>このレシピを今すぐ合成できるか（在庫充足を判定）。</summary>
        public static bool CanCraft(CraftRecipe recipe, Inventory inv)
        {
            if (recipe == null || inv == null || recipe.Output == null) return false;
            var inputs = recipe.Inputs;
            if (inputs == null) return false;

            for (int i = 0; i < inputs.Length; i++)
            {
                var ing = inputs[i];
                if (ing.kind == null || ing.amount <= 0) return false;
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
