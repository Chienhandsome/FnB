using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số của chức năng Tính lại giá vốn (docs/specs/tinh-lai-gia-von.md).</summary>
    public static class CostRecalcConstants
    {
        /// <summary>Tên chức năng/menu và tên quyền (SFUNCTION).</summary>
        public const string FunctionName = "Tính lại giá vốn";

        public const int ActiveStatus = 30;

        /// <summary>Loại phiếu tính như phiếu nhập: nhập kho (1) và tồn kho ban đầu (97).</summary>
        public const int OrderTypePurchase = 1;
        public const int OrderTypeOpening = 97;

        public const string NoPermission = "Bạn không có quyền tính lại giá vốn.";
        public const string NoData = "Chưa có phát sinh nào để tính giá vốn.";
        public const string AutoFailedWarning =
            "Đã lưu phiếu nhưng chưa tính lại được giá vốn, hãy chạy Tính lại giá vốn.";

        public static string Done(int products, int lines)
        {
            return string.Format("Đã tính xong: {0} mặt hàng, {1} dòng chứng từ cập nhật.", products, lines);
        }
    }

    /// <summary>Mặt hàng tham gia tính giá vốn.</summary>
    public sealed class CostProduct
    {
        public string Id { get; set; }
        /// <summary>Loại hiệu lực: "0" kiêm vật tư (cũng là loại mặc định khi để trống), "2" nguyên liệu, "1"/"4" món có công thức.</summary>
        public string TypeId { get; set; }
        public decimal CurrentCost { get; set; }

        public bool IsIngredientType
        {
            get { return TypeId == "0" || TypeId == "2"; }
        }

        public bool IsRecipeType
        {
            get { return TypeId == "1" || TypeId == "4"; }
        }
    }

    /// <summary>Một dòng công thức (DDINHLUONG): món <see cref="ProductId"/> dùng <see cref="Quantity"/> của <see cref="IngredientId"/>.</summary>
    public sealed class CostRecipeLine
    {
        public string ProductId { get; set; }
        public string IngredientId { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>Một dòng chứng từ (TDONHANGCHITIET) tham gia tính giá vốn.</summary>
    public sealed class CostLine
    {
        public string Id { get; set; }
        public string ProductId { get; set; }
        public int OrderType { get; set; }
        public DateTime Date { get; set; }
        public decimal QtyIn { get; set; }
        public decimal QtyOut { get; set; }
        public decimal Amount { get; set; }
        public decimal CurrentCost { get; set; }

        /// <summary>Khóa tháng yyyymm.</summary>
        public int MonthKey { get { return Date.Year * 100 + Date.Month; } }

        public bool IsPurchaseLike
        {
            get { return OrderType == CostRecalcConstants.OrderTypePurchase || OrderType == CostRecalcConstants.OrderTypeOpening; }
        }
    }

    /// <summary>Giá vốn mới của một dòng chứng từ.</summary>
    public sealed class CostLineUpdate
    {
        public string LineId { get; set; }
        public decimal NewCost { get; set; }
    }

    /// <summary>Giá vốn mới của một mặt hàng (DMATHANG.GIAVON).</summary>
    public sealed class CostProductUpdate
    {
        public string ProductId { get; set; }
        public decimal NewCost { get; set; }
    }

    /// <summary>Kết quả tính (chưa ghi).</summary>
    public sealed class CostRecalcPlan
    {
        public CostRecalcPlan()
        {
            LineUpdates = new List<CostLineUpdate>();
            ProductUpdates = new List<CostProductUpdate>();
        }

        public List<CostLineUpdate> LineUpdates { get; private set; }
        public List<CostProductUpdate> ProductUpdates { get; private set; }
        public int MonthCount { get; set; }
    }

    /// <summary>Kết quả chạy tính lại giá vốn.</summary>
    public sealed class CostRecalcResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public int ProductsChanged { get; set; }
        public int LinesChanged { get; set; }

        public static CostRecalcResult Fail(string message)
        {
            return new CostRecalcResult { Ok = false, Message = message };
        }
    }
}
