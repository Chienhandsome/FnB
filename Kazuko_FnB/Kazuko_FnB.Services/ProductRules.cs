using System;
using System.Collections.Generic;
using System.Text;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật của chức năng "Danh mục mặt hàng" (docs/specs/danh-muc-mat-hang.md), viết thuần để kiểm thử được.
    /// Không đụng DB, không đụng giao diện. Thông báo nguyên văn ở ProductText (Domain) để Data dùng chung.
    /// Quyết định của Chiến 21/11 đã áp ở đây: bỏ loại 7 + bỏ "Khác" (chỉ 4 radio), gộp cảnh báo #1/#2 (dừng ở lỗi đầu),
    /// tiền decimal, Excel/In ra khỏi v1.
    /// </summary>
    public static class ProductRules
    {
        // Loại mặt hàng (DLOAIMATHANG.ID).
        public const string TypeDual = "0";         // Mặt hàng kiêm vật tư
        public const string TypeBrew = "1";         // Mặt hàng pha chế
        public const string TypeMaterial = "2";     // Vật tư nguyên liệu
        public const string TypeOpen = "3";         // Mặt hàng mở
        public const string TypeCombo = "4";        // Combo
        public const string TypeComboOpen = "5";    // Combo mở
        public const string TypeService = "6";      // Dịch vụ
        public const string TypeHourlyService = "7";// Dịch vụ theo giờ (không tạo mới qua dialog, 5.3)

        /// <summary>Các loại dialog được tạo mới (5.7: chỉ 4 radio; loại 4/5/6/7 chỉ còn ở dữ liệu cũ).</summary>
        public static bool CanCreateType(string typeId)
        {
            return typeId == TypeDual || typeId == TypeBrew || typeId == TypeMaterial || typeId == TypeOpen;
        }

        /// <summary>
        /// Kiểm tra khi lưu, theo đúng thứ tự 2.1 #1–#8. Quyết định 5.6: dừng ở lỗi đầu tiên (gộp #1/#2),
        /// giữ nguyên chữ thông báo; #3 chỉ cảnh báo, vẫn lưu.
        /// </summary>
        public static ProductValidationResult ValidateBeforeSave(ProductValidationInput input)
        {
            var result = new ProductValidationResult { ErrorLineIndex = -1 };
            if (input == null)
                return result;
            List<RecipeValidationLine> lines = input.Lines ?? new List<RecipeValidationLine>();
            string type = input.TypeId ?? "";
            bool hasBigUnit = !string.IsNullOrEmpty(input.BigUnitId);

            // #1: SuDung2DonViTinh = 30, QUYDOI = 0, đã chọn ĐVT chẵn.
            if (input.UseTwoUnits && hasBigUnit && input.QuyDoi == 0m)
            {
                result.Error = ProductText.EnterQuyDoi;
                result.FocusQuyDoi = true;
                return result;
            }
            // #2: ĐVT chẵn = ĐVT lẻ.
            if (input.UseTwoUnits && hasBigUnit && !string.IsNullOrEmpty(input.UnitId) &&
                string.Equals(input.BigUnitId, input.UnitId, StringComparison.Ordinal))
            {
                result.Error = ProductText.UnitSame;
                return result;
            }
            // #3: Combo mở — chỉ cảnh báo, vẫn lưu.
            if (type == TypeComboOpen)
                result.Warning = ProductText.ComboOpen;
            // #4: loại {0, 2, 6} mà có dòng định lượng.
            if ((type == TypeDual || type == TypeMaterial || type == TypeService) && lines.Count > 0)
            {
                result.Error = ProductText.NoRecipeAllowed;
                return result;
            }
            // #5: loại {1, 4, 5} mà không có dòng định lượng.
            if ((type == TypeBrew || type == TypeCombo || type == TypeComboOpen) && lines.Count == 0)
            {
                result.Error = ProductText.RecipeRequired;
                return result;
            }
            // #6/#7/#8: từng dòng theo thứ tự lưới.
            bool isCombo = type == TypeCombo || type == TypeComboOpen;
            for (int i = 0; i < lines.Count; i++)
            {
                RecipeValidationLine line = lines[i];
                if (line.Quantity <= 0m)
                {
                    result.Error = ProductText.EnterQuantity;
                    result.ErrorLineIndex = i;
                    return result;
                }
                if (string.IsNullOrEmpty(line.MaterialId) || line.MaterialMissing)
                {
                    result.Error = ProductText.EnterMaterial;
                    result.ErrorLineIndex = i;
                    return result;
                }
                if (isCombo && line.MaterialTypeId == TypeMaterial)
                {
                    result.Error = ProductText.ComboNoMaterial;
                    result.ErrorLineIndex = i;
                    return result;
                }
            }
            return result;
        }

        /// <summary>
        /// Sinh mã = tiền tố nhóm + dãy chữ số ngẫu nhiên (3.5, quyết định của Chiến 21/11 — không dùng GenMaHang gốc).
        /// Tổng dài = length (không nhỏ hơn 1 chữ số); null khi nhóm không có tiền tố.
        /// </summary>
        public static string GenerateCode(string prefix, int length, Random random)
        {
            if (string.IsNullOrEmpty(prefix) || random == null)
                return null;
            int digits = Math.Max(1, length - prefix.Length);
            var sb = new StringBuilder(prefix.Length + digits);
            sb.Append(prefix);
            for (int i = 0; i < digits; i++)
                sb.Append(random.Next(10));
            return sb.ToString();
        }

        /// <summary>Sinh mã không trùng (kiểm qua isTaken); trùng thì sinh lại tối đa 10 lần, rồi trả null.</summary>
        public static string GenerateUniqueCode(string prefix, int length, Func<string, bool> isTaken, Random random)
        {
            if (string.IsNullOrEmpty(prefix) || random == null)
                return null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                string code = GenerateCode(prefix, length, random);
                if (code != null && (isTaken == null || !isTaken(code)))
                    return code;
            }
            return null;
        }

        /// <summary>Thành tiền một dòng định lượng (3.4): nhập theo ĐVT lẻ, vốn theo ĐVT lẻ.</summary>
        public static void RecipeAmounts(decimal quantity, decimal materialBuyPrice, decimal? materialQuyDoi,
            bool materialHasBigUnit, decimal materialCostPrice, out decimal amountBuy, out decimal amountCost)
        {
            decimal unitBuy = ProductMath.UnitBuyPrice(materialBuyPrice, materialQuyDoi, materialHasBigUnit);
            amountBuy = ProductMath.AmountBuy(quantity, unitBuy);
            amountCost = ProductMath.AmountCost(quantity, materialCostPrice);
        }

        /// <summary>Tổng thành tiền nhập/vốn của các dòng định lượng (dòng TỔNG ở tab Định lượng).</summary>
        public static void Totals(IEnumerable<RecipeLine> lines, out decimal totalBuy, out decimal totalCost)
        {
            totalBuy = 0m;
            totalCost = 0m;
            if (lines == null)
                return;
            foreach (RecipeLine line in lines)
            {
                totalBuy += line.AmountBuy;
                totalCost += line.AmountCost;
            }
        }

        /// <summary>
        /// Thêm/sửa dòng định lượng mà loại hiện tại có CODINHLUONG = 0 thì tự chuyển sang "Mặt hàng pha chế" (3.2).
        /// Loại đã có định lượng (pha chế, Combo, Combo mở) giữ nguyên.
        /// </summary>
        public static string SwitchTypeForRecipe(string currentTypeId, bool typeHasRecipe)
        {
            return typeHasRecipe ? (currentTypeId ?? "") : TypeBrew;
        }
    }
}
