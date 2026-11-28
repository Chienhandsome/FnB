using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        /// <summary>Chiến chốt 28/11: cấu hình máy in hóa đơn theo khu vực, lưu theo máy (kho cấu hình cục bộ), không đụng CSDL.</summary>
        private static void AreaInvoicePrinterTests(string db)
        {
            var session = new UserSession { UserId = "selftest_aip", UserName = "selftest_aip", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
            var areas = new List<AreaRow>
            {
                new AreaRow { Id = "AIP-A1", Name = "Tầng 1" },
                new AreaRow { Id = "AIP-A2", Name = "Sân vườn" },
                new AreaRow { Id = "AIP-A3", Name = "Mang về" }
            };
            string k1 = KitchenPrintRules.AreaInvoicePrinterKey("AIP-A1");
            string k2 = KitchenPrintRules.AreaInvoicePrinterKey("AIP-A2");
            string k3 = KitchenPrintRules.AreaInvoicePrinterKey("AIP-A3");

            // Đã có sẵn cấu hình cũ: khu vực 2 dùng "Máy A" (có cài), khu vực 3 trỏ máy đã gỡ ("Máy Cũ").
            var store = new MemoryKitchenPrinterStore("Máy A", "Máy B");
            store.SetConfiguredPrinter(k2, "Máy A");
            store.SetConfiguredPrinter(k3, "Máy Cũ");

            bool loadOk, saveOk;
            string loadDetail, saveDetail;
            using (var dlg = new AreaInvoicePrinterDialog(session, store, areas))
            {
                DataGridView g = dlg.GridForTest;
                var combo = (DataGridViewComboBoxColumn)g.Columns["colPrinter"];
                string r0 = Convert.ToString(g.Rows[0].Cells["colPrinter"].Value);
                string r1 = Convert.ToString(g.Rows[1].Cells["colPrinter"].Value);
                string r2 = Convert.ToString(g.Rows[2].Cells["colPrinter"].Value);
                loadOk = g.Rows.Count == 3
                    && Convert.ToString(g.Rows[0].Cells["colArea"].Value) == "Tầng 1"
                    && Convert.ToString(g.Rows[1].Cells["colArea"].Value) == "Sân vườn"
                    && Convert.ToString(g.Rows[2].Cells["colArea"].Value) == "Mang về"
                    && combo.Items.Count == 3 && combo.Items.Contains("") && combo.Items.Contains("Máy A") && combo.Items.Contains("Máy B")
                    && r0 == "" && r1 == "Máy A" && r2 == "";
                loadDetail = "rows=" + g.Rows.Count + ", items=" + combo.Items.Count + ", r=" + r0 + "|" + r1 + "|" + r2;

                // Đổi: khu vực 1 -> Máy B, khu vực 2 -> trống (mặc định Windows), khu vực 3 -> Máy A; Lưu.
                g.Rows[0].Cells["colPrinter"].Value = "Máy B";
                g.Rows[1].Cells["colPrinter"].Value = "";
                g.Rows[2].Cells["colPrinter"].Value = "Máy A";
                dlg.SaveConfig();
                saveOk = dlg.DialogResult == DialogResult.OK
                    && store.GetConfiguredPrinter(k1) == "Máy B"
                    && store.GetConfiguredPrinter(k2) == ""
                    && store.GetConfiguredPrinter(k3) == "Máy A";
                saveDetail = store.GetConfiguredPrinter(k1) + "|" + store.GetConfiguredPrinter(k2) + "|" + store.GetConfiguredPrinter(k3);
            }

            // Mở lại: hiện đúng cấu hình đã lưu. Khóa dùng cho in hóa đơn trùng khóa của luồng in (IN_HD_KV_ + id).
            bool reopenOk;
            using (var dlg2 = new AreaInvoicePrinterDialog(session, store, areas))
            {
                DataGridView g2 = dlg2.GridForTest;
                reopenOk = Convert.ToString(g2.Rows[0].Cells["colPrinter"].Value) == "Máy B"
                    && Convert.ToString(g2.Rows[1].Cells["colPrinter"].Value) == ""
                    && Convert.ToString(g2.Rows[2].Cells["colPrinter"].Value) == "Máy A"
                    && k1 == "IN_HD_KV_AIP-A1";
            }

            Check("Máy in hóa đơn theo khu vực (Chiến chốt 28/11): hộp liệt kê khu vực với máy in đã lưu (máy đã gỡ thì để trống), đổi và Lưu ghi đúng khóa IN_HD_KV_{khu vực} vào kho cấu hình cục bộ, mở lại hiện đúng",
                loadOk && saveOk && reopenOk,
                SingleLine("load=" + loadDetail + ", save=" + saveDetail + ", reopen=" + reopenOk));
        }
    }
}
