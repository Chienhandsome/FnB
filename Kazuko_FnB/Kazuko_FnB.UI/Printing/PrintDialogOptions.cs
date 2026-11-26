using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Cấu hình giao diện và thứ tự/mặc định mẫu in cho <see cref="PrintTemplateDialog"/>:
    /// hỗ trợ cả hộp "CHỌN MẪU IN" (A1, danh sách, OK/Cancel) và hộp "IN BÁO CÁO" (B, ô thả, Thực hiện/Thoát).
    /// </summary>
    internal sealed class PrintDialogOptions
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string OkButtonText { get; set; }
        public string CancelButtonText { get; set; }
        public bool UseComboBox { get; set; }
        public bool HideActionOptions { get; set; }
        public string DefaultTemplateId { get; set; }
        public string DefaultTemplateName { get; set; }
        public bool SelectLastByDefault { get; set; }
        public bool UseBookingOrder { get; set; }

        public static PrintDialogOptions ForFormSelectDialog()
        {
            return new PrintDialogOptions
            {
                Title = PrintConstants.SelectTemplateDialogTitle,
                Subtitle = "Chọn mẫu in và hình thức xuất báo cáo",
                OkButtonText = "OK",
                CancelButtonText = "Cancel",
                UseComboBox = false,
                SelectLastByDefault = true
            };
        }

        public static PrintDialogOptions ForBookingReportDialog()
        {
            return new PrintDialogOptions
            {
                Title = PrintConstants.PrintReportDialogTitle,
                Subtitle = "Chọn mẫu in phiếu đặt hàng và hình thức thực hiện",
                OkButtonText = "Thực hiện",
                CancelButtonText = "Thoát",
                UseComboBox = true,
                DefaultTemplateName = PrintConstants.DefaultBookingTemplateName,
                SelectLastByDefault = false,
                UseBookingOrder = true
            };
        }

        public static PrintDialogOptions ForQuoteReportDialog()
        {
            return new PrintDialogOptions
            {
                Title = PrintConstants.PrintReportDialogTitle,
                Subtitle = "Chọn mẫu in báo giá và hình thức thực hiện",
                OkButtonText = "Thực hiện",
                CancelButtonText = "Thoát",
                UseComboBox = true,
                DefaultTemplateName = PrintConstants.DefaultQuoteTemplateName,
                SelectLastByDefault = false,
                UseBookingOrder = false
            };
        }

        public static PrintDialogOptions ForSalesInvoiceReportDialog(string preferredTemplateIdOrName = null)
        {
            return new PrintDialogOptions
            {
                Title = PrintConstants.PrintReportDialogTitle,
                Subtitle = "Chọn mẫu in hóa đơn và hình thức thực hiện",
                OkButtonText = "Thực hiện",
                CancelButtonText = "Thoát",
                UseComboBox = true,
                DefaultTemplateId = preferredTemplateIdOrName ?? "",
                DefaultTemplateName = PrintConstants.DefaultSalesInvoiceTemplateName,
                SelectLastByDefault = false,
                UseBookingOrder = false
            };
        }

        public static PrintDialogOptions ForBarcodeDialog()
        {
            return new PrintDialogOptions
            {
                Title = PrintConstants.BarcodeTemplateDialogTitle,
                Subtitle = "Chọn mẫu tem mã vạch để xem trước",
                OkButtonText = "Chấp nhận",
                CancelButtonText = "Thoát",
                UseComboBox = false,
                HideActionOptions = true,
                SelectLastByDefault = false,
                UseBookingOrder = false
            };
        }
    }

    /// <summary>
    /// Sắp xếp danh sách mẫu in và chọn mẫu mặc định theo quy tắc của từng màn (thuần logic, không SQL).
    /// Đặt hàng: bảng tên cố định 54 mm, 54 mm(2 dòng), 80 mm, A4, A5 (tên lạ xếp cuối).
    /// </summary>
    internal static class PrintTemplateOrdering
    {
        private static readonly string[][] BookingOrderGroups =
        {
            new[] { "Mẫu đặt hàng 54 mm", "Mẫu đặt hàng 54mm" },
            new[] { "Mẫu đặt hàng 54 mm(2 dòng)", "Mẫu đặt hàng 54 mm (2 dòng)", "Mẫu đặt hàng 54 mm 2 dòng", "Mẫu đặt hàng 54mm(2 dòng)", "Mẫu đặt hàng 54mm (2 dòng)", "Mẫu đặt hàng 54mm 2 dòng" },
            new[] { "Mẫu đặt hàng 80 mm", "Mẫu đặt hàng 80mm" },
            new[] { "Mẫu đặt hàng A4" },
            new[] { "Mẫu đặt hàng A5" }
        };

        public static List<PrintTemplateRecord> OrderAndFindDefault(
            IList<PrintTemplateRecord> templates,
            PrintDialogOptions options,
            out int defaultIndex)
        {
            PrintDialogOptions effective = options ?? PrintDialogOptions.ForFormSelectDialog();
            var ordered = new List<PrintTemplateRecord>();
            if (templates != null)
            {
                for (int i = 0; i < templates.Count; i++)
                {
                    if (templates[i] != null)
                        ordered.Add(templates[i]);
                }
            }

            if (effective.UseBookingOrder)
            {
                ordered.Sort(CompareBookingTemplates);
            }
            else
            {
                ordered.Sort(delegate (PrintTemplateRecord a, PrintTemplateRecord b)
                {
                    return StringComparer.OrdinalIgnoreCase.Compare(a.Name ?? "", b.Name ?? "");
                });
            }

            defaultIndex = FindDefaultIndex(ordered, effective);
            return ordered;
        }

        public static PrintTemplateRecord PickDefaultTemplate(
            IList<PrintTemplateRecord> templates,
            PrintDialogOptions options)
        {
            int idx;
            List<PrintTemplateRecord> ordered = OrderAndFindDefault(templates, options, out idx);
            if (idx < 0 || idx >= ordered.Count)
                return null;
            return ordered[idx];
        }

        private static int FindDefaultIndex(List<PrintTemplateRecord> ordered, PrintDialogOptions options)
        {
            if (ordered.Count == 0)
                return -1;

            if (!string.IsNullOrWhiteSpace(options.DefaultTemplateId))
            {
                string wantId = options.DefaultTemplateId.Trim();
                for (int i = 0; i < ordered.Count; i++)
                {
                    string id = (ordered[i].Id ?? "").Trim();
                    string name = (ordered[i].Name ?? "").Trim();
                    if (string.Equals(id, wantId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, wantId, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(options.DefaultTemplateName))
            {
                string want = options.DefaultTemplateName.Trim();
                for (int i = 0; i < ordered.Count; i++)
                {
                    string name = (ordered[i].Name ?? "").Trim();
                    if (string.Equals(name, want, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                return 0;
            }

            return options.SelectLastByDefault ? ordered.Count - 1 : 0;
        }

        private static int CompareBookingTemplates(PrintTemplateRecord a, PrintTemplateRecord b)
        {
            string na = (a == null ? "" : (a.Name ?? "")).Trim();
            string nb = (b == null ? "" : (b.Name ?? "")).Trim();
            int ra = BookingTemplateRank(na);
            int rb = BookingTemplateRank(nb);
            if (ra != rb)
                return ra.CompareTo(rb);
            return StringComparer.OrdinalIgnoreCase.Compare(na, nb);
        }

        internal static int BookingTemplateRank(string name)
        {
            string clean = (name ?? "").Trim();
            for (int r = 0; r < BookingOrderGroups.Length; r++)
            {
                string[] aliases = BookingOrderGroups[r];
                for (int j = 0; j < aliases.Length; j++)
                {
                    if (string.Equals(clean, aliases[j], StringComparison.OrdinalIgnoreCase))
                        return r;
                }
            }
            return int.MaxValue;
        }
    }
}
