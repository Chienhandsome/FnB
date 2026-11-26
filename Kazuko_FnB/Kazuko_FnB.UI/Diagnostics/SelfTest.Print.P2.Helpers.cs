using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Windows.Forms;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class P2PrintFixture
        {
            public string GroupId;
            public string UnitId;
            public string CoffeeId;
            public string PeachTeaId;
            public string AreaId;
            public string Room1Id;
            public string Room2Id;
            public string ReasonId;
            public string StoreId;
            public List<string> CreatedBookingIds = new List<string>();
            public List<string> CreatedReceiptIds = new List<string>();
            public List<string> CreatedQuoteIds = new List<string>();
        }

        private static P2PrintFixture CreateP2PrintFixture(string db)
        {
            var fx = new P2PrintFixture
            {
                GroupId = DbHelper.NewId(),
                UnitId = DbHelper.NewId(),
                CoffeeId = DbHelper.NewId(),
                PeachTeaId = DbHelper.NewId(),
                AreaId = DbHelper.NewId(),
                Room1Id = DbHelper.NewId(),
                Room2Id = DbHelper.NewId(),
                ReasonId = DbHelper.NewId(),
                StoreId = DbHelper.NewId()
            };

            BookingExec(db,
                "INSERT INTO \"DCUAHANG\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                "@id", fx.StoreId, "@n", "CH Thử In P2");
            BookingExec(db,
                "INSERT INTO \"DLYDOTHUCHI\" (\"ID\", \"NAME\", \"LOAILYDO\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 0, 30, 'selftest')",
                "@id", fx.ReasonId, "@n", "Đặt cọc In P2");
            BookingExec(db,
                "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest')",
                "@id", fx.GroupId, "@n", "NHÓM IN P2", "@c", "P2GRP");
            BookingExec(db,
                "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                "@id", fx.UnitId, "@n", "Ly");
            BookingExec(db,
                "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                "VALUES (@id, @c, @n, @g, @u, @p, 30, 'selftest')",
                "@id", fx.CoffeeId, "@c", "P2-CF", "@n", "Cà phê sữa", "@g", fx.GroupId, "@u", fx.UnitId, "@p", 25000m);
            BookingExec(db,
                "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                "VALUES (@id, @c, @n, @g, @u, @p, 30, 'selftest')",
                "@id", fx.PeachTeaId, "@c", "P2-TD", "@n", "Trà đào", "@g", fx.GroupId, "@u", fx.UnitId, "@p", 30000m);
            BookingExec(db,
                "INSERT INTO \"DKHUVUC\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                "@id", fx.AreaId, "@n", "Khu In P2");
            BookingExec(db,
                "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                "@id", fx.Room1Id, "@n", "Phòng VIP 1", "@a", fx.AreaId);
            BookingExec(db,
                "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                "@id", fx.Room2Id, "@n", "Phòng VIP 2", "@a", fx.AreaId);

            return fx;
        }

        private static void CleanupP2PrintFixture(string db, P2PrintFixture fx)
        {
            if (fx == null)
                return;
            foreach (string id in fx.CreatedReceiptIds)
            {
                try { BookingExec(db, "DELETE FROM \"TTHUCHI\" WHERE \"ID\" = @id", "@id", id); } catch { }
            }
            foreach (string id in fx.CreatedBookingIds)
            {
                try { BookingExec(db, "DELETE FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", id); } catch { }
                try { BookingExec(db, "DELETE FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", id); } catch { }
            }
            foreach (string id in fx.CreatedQuoteIds)
            {
                try { BookingExec(db, "DELETE FROM \"TBAOGIACHITIET\" WHERE \"TBAOGIAID\" = @id", "@id", id); } catch { }
                try { BookingExec(db, "DELETE FROM \"TBAOGIA\" WHERE \"ID\" = @id", "@id", id); } catch { }
            }
            try { BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" IN (@a, @b)", "@a", fx.Room1Id, "@b", fx.Room2Id); } catch { }
            try { BookingExec(db, "DELETE FROM \"DKHUVUC\" WHERE \"ID\" = @id", "@id", fx.AreaId); } catch { }
            try { BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" IN (@a, @b)", "@a", fx.CoffeeId, "@b", fx.PeachTeaId); } catch { }
            try { BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", fx.GroupId); } catch { }
            try { BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" = @id", "@id", fx.UnitId); } catch { }
            try { BookingExec(db, "DELETE FROM \"DLYDOTHUCHI\" WHERE \"ID\" = @id", "@id", fx.ReasonId); } catch { }
            try { BookingExec(db, "DELETE FROM \"DCUAHANG\" WHERE \"ID\" = @id", "@id", fx.StoreId); } catch { }
        }

        private static string SingleLineDetail(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            return raw.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string SnapshotTableCountsAndTotals(string db, string headerTable, string detailTable)
        {
            return DbHelper.Read(db, conn =>
            {
                DataTable h = DbHelper.Table(conn, null,
                    "SELECT COUNT(*) AS CNT, COALESCE(SUM(\"TONGCONG\"), 0) AS TOT FROM \"" + headerTable + "\"");
                DataTable d = DbHelper.Table(conn, null,
                    "SELECT COUNT(*) AS CNT, COALESCE(SUM(\"THANHTIEN\"), 0) AS TOT FROM \"" + detailTable + "\"");
                return headerTable + ":" + DbHelper.Int(h.Rows[0]["CNT"]) + ":" + DbHelper.Dec(h.Rows[0]["TOT"]).ToString(CultureInfo.InvariantCulture)
                    + "|" + detailTable + ":" + DbHelper.Int(d.Rows[0]["CNT"]) + ":" + DbHelper.Dec(d.Rows[0]["TOT"]).ToString(CultureInfo.InvariantCulture);
            });
        }

        private static bool CheckPreparedMoneyCell(
            List<TemplateObjInfo> map,
            List<KeyValuePair<string, string>> pageTexts,
            string bandKind,
            string token,
            string excludeToken,
            decimal expected,
            out string actualOut)
        {
            string objName = FindObjName(map, bandKind, token, excludeToken);
            actualOut = FindLastObjectText(pageTexts, objName);
            decimal parsed;
            return objName.Length > 0 && TryParseCellNumber(actualOut, out parsed) && parsed == expected;
        }

        private static bool CheckPreparedTextCell(
            List<TemplateObjInfo> map,
            List<KeyValuePair<string, string>> pageTexts,
            string bandKind,
            string token,
            string expectedSubstring,
            out string actualOut)
        {
            string objName = FindObjName(map, bandKind, token, null);
            actualOut = FindLastObjectText(pageTexts, objName);
            return objName.Length > 0
                && !string.IsNullOrEmpty(actualOut)
                && actualOut.IndexOf(expectedSubstring, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static PrintTemplateRecord FindTemplateByName(IList<PrintTemplateRecord> list, string name)
        {
            if (list == null)
                return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && string.Equals((list[i].Name ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return list[i];
            }
            return null;
        }

        /// <summary>Recorder mở rộng lưu cả toàn bộ chữ trang in để kiểm số mới trên trang sau khi dialog tự lưu.</summary>
        private sealed class CapturingPrintOutputRecorder : IPrintOutputHandler
        {
            public int PreviewCount { get; private set; }
            public int PrintCount { get; private set; }
            public PrintTemplateRecord LastTemplate { get; private set; }
            public string LastPrinterName { get; private set; }
            public List<KeyValuePair<string, string>> LastPageTexts { get; private set; }
            public List<string> ErrorMessages { get; private set; }
            public List<string> InfoMessages { get; private set; }

            public CapturingPrintOutputRecorder()
            {
                LastPageTexts = new List<KeyValuePair<string, string>>();
                ErrorMessages = new List<string>();
                InfoMessages = new List<string>();
            }

            public void ShowPreview(IWin32Window owner, Report preparedReport, PrintTemplateRecord template, string title)
            {
                PreviewCount++;
                LastTemplate = template;
                LastPageTexts = FastReportEngine.ExtractPreparedPageTexts(preparedReport);
            }

            public void PrintToPrinter(Report preparedReport, PrintTemplateRecord template, string printerName)
            {
                PrintCount++;
                LastTemplate = template;
                LastPrinterName = printerName;
                LastPageTexts = FastReportEngine.ExtractPreparedPageTexts(preparedReport);
            }

            public string PromptSaveFile(IWin32Window owner, PrintActionKind exportKind, string defaultFileName) { return null; }
            public void ExportExcel(Report preparedReport, PrintTemplateRecord template, string filePath) { }
            public void ExportPdf(Report preparedReport, PrintTemplateRecord template, string filePath) { }
            public bool ConfirmOpenFile(IWin32Window owner, string filePath) { return false; }
            public void OpenFile(string filePath) { }
            public void ShowError(IWin32Window owner, string message) { ErrorMessages.Add(SingleLineDetail(message)); }
            public void ShowInfo(IWin32Window owner, string message) { InfoMessages.Add(SingleLineDetail(message)); }
        }
    }
}
