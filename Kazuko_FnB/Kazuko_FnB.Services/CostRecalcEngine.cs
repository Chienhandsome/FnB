using System;
using System.Collections.Generic;
using System.Linq;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Tính giá vốn bình quân cuối kỳ theo tháng (docs/specs/tinh-lai-gia-von.md §2), thuần bộ nhớ, không chạm CSDL.
    /// Bước A: nguyên liệu và kiêm vật tư (tồn, giá trị mang sang tháng sau; giá vốn tháng = giá trị / tồn).
    /// Bước B: món định lượng và combo (loại '1', '4'), bung công thức tối đa 2 cấp theo giá vốn vật tư của tháng.
    /// Chỉ ghi nhận thay đổi khi giá mới khác giá hiện có (làm tròn 2 số lẻ).
    /// </summary>
    public static class CostRecalcEngine
    {
        public static CostRecalcPlan Compute(
            IList<CostProduct> products,
            IList<CostRecipeLine> recipes,
            IList<CostLine> lines,
            Action<int, int> progress)
        {
            var plan = new CostRecalcPlan();
            if (products == null || lines == null || lines.Count == 0)
                return plan;

            var productById = new Dictionary<string, CostProduct>(StringComparer.Ordinal);
            foreach (CostProduct p in products)
                productById[p.Id] = p;

            var recipeByProduct = new Dictionary<string, List<CostRecipeLine>>(StringComparer.Ordinal);
            if (recipes != null)
            {
                foreach (CostRecipeLine r in recipes)
                {
                    List<CostRecipeLine> list;
                    if (!recipeByProduct.TryGetValue(r.ProductId, out list))
                    {
                        list = new List<CostRecipeLine>();
                        recipeByProduct[r.ProductId] = list;
                    }
                    list.Add(r);
                }
            }

            // Gom dòng theo tháng rồi theo mặt hàng; bỏ dòng của mặt hàng không tham gia (loại khác 0, 1, 2, 4).
            var months = new SortedDictionary<int, Dictionary<string, List<CostLine>>>();
            foreach (CostLine l in lines)
            {
                CostProduct p;
                if (!productById.TryGetValue(l.ProductId, out p) || (!p.IsIngredientType && !p.IsRecipeType))
                    continue;
                Dictionary<string, List<CostLine>> byProduct;
                if (!months.TryGetValue(l.MonthKey, out byProduct))
                {
                    byProduct = new Dictionary<string, List<CostLine>>(StringComparer.Ordinal);
                    months[l.MonthKey] = byProduct;
                }
                List<CostLine> pl;
                if (!byProduct.TryGetValue(l.ProductId, out pl))
                {
                    pl = new List<CostLine>();
                    byProduct[l.ProductId] = pl;
                }
                pl.Add(l);
            }

            plan.MonthCount = months.Count;
            if (months.Count == 0)
                return plan;

            var ton = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var giaTri = new Dictionary<string, decimal>(StringComparer.Ordinal);
            // Giá vốn gần nhất (đến hết tháng đang xử lý) của từng mặt hàng đã tính ở bước A.
            var costNow = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var recipeProductsWithLines = new HashSet<string>(StringComparer.Ordinal);
            var ingredientProductsWithLines = new HashSet<string>(StringComparer.Ordinal);
            var lineUpdates = new Dictionary<string, decimal>(StringComparer.Ordinal);

            int done = 0;
            foreach (KeyValuePair<int, Dictionary<string, List<CostLine>>> month in months)
            {
                // Bước A.
                foreach (KeyValuePair<string, List<CostLine>> kv in month.Value)
                {
                    if (!productById[kv.Key].IsIngredientType)
                        continue;
                    ingredientProductsWithLines.Add(kv.Key);

                    decimal t, v, last;
                    ton.TryGetValue(kv.Key, out t);
                    giaTri.TryGetValue(kv.Key, out v);
                    costNow.TryGetValue(kv.Key, out last);

                    foreach (CostLine l in kv.Value)
                    {
                        if (l.IsPurchaseLike)
                        {
                            t += l.QtyIn;
                            v += l.Amount;
                        }
                    }

                    // Tồn <= 0: giữ giá vốn tháng trước (T3), không chia cho 0.
                    decimal giaVon = t > 0m ? Math.Round(v / t, 2, MidpointRounding.AwayFromZero) : last;

                    foreach (CostLine l in kv.Value)
                    {
                        AddIfChanged(lineUpdates, l, giaVon);
                        if (!l.IsPurchaseLike)
                        {
                            decimal net = l.QtyOut - l.QtyIn;
                            t -= net;
                            v -= net * giaVon;
                        }
                    }
                    if (t == 0m)
                        v = 0m;

                    ton[kv.Key] = t;
                    giaTri[kv.Key] = v;
                    costNow[kv.Key] = giaVon;
                }

                // Bước B.
                foreach (KeyValuePair<string, List<CostLine>> kv in month.Value)
                {
                    if (!productById[kv.Key].IsRecipeType)
                        continue;
                    List<CostRecipeLine> recipe;
                    if (!recipeByProduct.TryGetValue(kv.Key, out recipe) || recipe.Count == 0)
                        continue;
                    recipeProductsWithLines.Add(kv.Key);

                    decimal cost = RecipeCost(kv.Key, recipeByProduct, productById, costNow);
                    foreach (CostLine l in kv.Value)
                    {
                        if (!l.IsPurchaseLike)
                            AddIfChanged(lineUpdates, l, cost);
                    }
                }

                done++;
                if (progress != null)
                    progress(done, months.Count);
            }

            foreach (KeyValuePair<string, decimal> u in lineUpdates)
                plan.LineUpdates.Add(new CostLineUpdate { LineId = u.Key, NewCost = u.Value });

            // Giá vốn mặt hàng = giá tháng cuối, chỉ khi khác giá hiện có, chỉ cho mặt hàng đã được tính (T1).
            foreach (string id in ingredientProductsWithLines)
            {
                decimal c = costNow[id];
                if (Math.Round(productById[id].CurrentCost, 2) != c)
                    plan.ProductUpdates.Add(new CostProductUpdate { ProductId = id, NewCost = c });
            }
            foreach (string id in recipeProductsWithLines)
            {
                decimal c = RecipeCost(id, recipeByProduct, productById, costNow);
                if (Math.Round(productById[id].CurrentCost, 2) != c)
                    plan.ProductUpdates.Add(new CostProductUpdate { ProductId = id, NewCost = c });
            }
            return plan;
        }

        /// <summary>
        /// Giá vốn món = Round(Σ(SL vật tư × giá vốn vật tư), 2). Vật tư là món có công thức thì bung thêm một cấp
        /// (tối đa 2 cấp như app gốc); vật tư chưa có giá vốn tính 0.
        /// </summary>
        public static decimal RecipeCost(
            string productId,
            Dictionary<string, List<CostRecipeLine>> recipeByProduct,
            Dictionary<string, CostProduct> productById,
            Dictionary<string, decimal> costNow)
        {
            decimal sum = 0m;
            List<CostRecipeLine> level1;
            if (!recipeByProduct.TryGetValue(productId, out level1))
                return 0m;

            foreach (CostRecipeLine a in level1)
            {
                CostProduct ing;
                List<CostRecipeLine> level2;
                if (productById.TryGetValue(a.IngredientId, out ing) && ing.IsRecipeType
                    && recipeByProduct.TryGetValue(a.IngredientId, out level2) && level2.Count > 0)
                {
                    foreach (CostRecipeLine b in level2)
                        sum += a.Quantity * b.Quantity * CostOf(b.IngredientId, costNow);
                }
                else
                {
                    sum += a.Quantity * CostOf(a.IngredientId, costNow);
                }
            }
            return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal CostOf(string productId, Dictionary<string, decimal> costNow)
        {
            decimal c;
            return costNow.TryGetValue(productId, out c) ? c : 0m;
        }

        private static void AddIfChanged(Dictionary<string, decimal> updates, CostLine line, decimal newCost)
        {
            if (Math.Round(line.CurrentCost, 2) != newCost)
                updates[line.Id] = newCost;
            else
                updates.Remove(line.Id);
        }
    }
}
