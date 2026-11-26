using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Dựng PrintJobData và mẫu FastReport 1.2.47 tối giản (không script) cho phiếu bếp / chế biến (patch sddv-d).
    /// Hỗ trợ cả mẫu STEMPLATE (nếu cấu hình MauInCheBien trỏ tới mẫu hợp lệ) lẫn mẫu dựng sẵn an toàn.
    /// </summary>
    internal static class KitchenTicketPrintDataBuilder
    {
        public static PrintJobData Build(
            string billName,
            string tableName,
            string areaName,
            string drinkTypeCaption,
            int printSeq,
            IList<KitchenPendingItem> items,
            CompanyPrintInfo companyInfo,
            string printedBy,
            DateTime printedAt)
        {
            var ds = new DataSet("Data");
            DataTable table0 = CreateTable0Schema();

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    KitchenPendingItem it = items[i];
                    if (it == null)
                        continue;
                    DataRow dr = table0.NewRow();
                    dr["TENHANG"] = it.ItemName ?? "";
                    dr["DMATHANG_NAME"] = it.ItemName ?? "";
                    dr["DVT"] = it.UnitName ?? "";
                    dr["DDONVITINH_NAME"] = it.UnitName ?? "";
                    dr["DDONVITINHID"] = it.UnitId ?? "";
                    dr["DLOAIDOID"] = it.DrinkTypeId ?? ServicePrintConstants.DrinkTypeDrink;
                    dr["SOLUONG"] = it.Quantity;
                    dr["SOLUONGSTR"] = FormatKitchenQty(it.Quantity);
                    dr["DONGIA"] = it.UnitPrice;
                    dr["NOTE"] = it.Note ?? "";
                    table0.Rows.Add(dr);
                }
            }
            ds.Tables.Add(table0);
            // Mẫu STEMPLATE của màn "In chế biến" khai nguồn dữ liệu tên "DataSet" (Data.DataSet); mẫu dựng sẵn dùng Table0.
            DataTable dataSetCopy = table0.Copy();
            dataSetCopy.TableName = "DataSet";
            ds.Tables.Add(dataSetCopy);

            CompanyPrintInfo company = companyInfo ?? new CompanyPrintInfo();
            string title = string.IsNullOrWhiteSpace(drinkTypeCaption)
                ? ServicePrintConstants.KitchenTitlePrefix
                : (ServicePrintConstants.KitchenTitlePrefix + " - " + drinkTypeCaption.Trim().ToUpper(CultureInfo.CurrentCulture));

            var job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = "Phieu-che-bien-" + BookingPrintDataBuilder.SanitizeFileNamePart(billName ?? ""),
                PreviewTitle = title
            };

            job.Parameters["Tiêu đề"] = title;
            job.Parameters["TÊN BÀN"] = tableName ?? "";
            job.Parameters["KHU VỰC"] = areaName ?? "";
            job.Parameters["LẦN IN"] = printSeq;
            job.Parameters["LOAIDO"] = drinkTypeCaption ?? "";
            job.Parameters["TENBAN"] = tableName ?? "";
            job.Parameters["DBAN_NAME"] = tableName ?? "";
            job.Parameters["TENKHUVUC"] = areaName ?? "";
            job.Parameters["DKHUVUC_NAME"] = areaName ?? "";
            job.Parameters["SOHOADON"] = billName ?? "";
            job.Parameters["NAME"] = billName ?? "";
            job.Parameters["LANIN"] = printSeq;
            job.Parameters["LANSO"] = printSeq;
            job.Parameters["NGAYIN"] = printedAt;
            job.Parameters[PrintConstants.PrintedByParameterName] = printedBy ?? "";
            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";

            return job;
        }

        /// <summary>
        /// Tạo mẫu FastReport 1.2.47 tối giản (80mm, không script) cho phiếu in bếp khi hệ thống không cấu hình MauInCheBien trong STEMPLATE.
        /// </summary>
        public static PrintTemplateRecord CreateBuiltInTemplate(bool includePrice)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Report ReportInfo.CreatorVersion=\"1.2.47.0\">");
            sb.AppendLine("  <Dictionary>");
            sb.AppendLine("    <TableDataSource Name=\"Table0\" ReferenceName=\"Data.Table0\" DataType=\"System.Int32\" Enabled=\"true\">");
            sb.AppendLine("      <Column Name=\"TENHANG\" DataType=\"System.String\"/>");
            sb.AppendLine("      <Column Name=\"DVT\" DataType=\"System.String\"/>");
            sb.AppendLine("      <Column Name=\"SOLUONG\" DataType=\"System.Decimal\"/>");
            sb.AppendLine("      <Column Name=\"SOLUONGSTR\" DataType=\"System.String\"/>");
            sb.AppendLine("      <Column Name=\"DONGIA\" DataType=\"System.Decimal\"/>");
            sb.AppendLine("      <Column Name=\"NOTE\" DataType=\"System.String\"/>");
            sb.AppendLine("    </TableDataSource>");
            sb.AppendLine("  </Dictionary>");
            sb.AppendLine("  <ReportPage Name=\"Page1\" PaperWidth=\"80\" LeftMargin=\"3\" TopMargin=\"3\" RightMargin=\"3\" BottomMargin=\"3\">");
            sb.AppendLine("    <ReportTitleBand Name=\"ReportTitle1\" Width=\"279.72\" Height=\"106\" CanGrow=\"true\">");
            sb.AppendLine("      <TextObject Name=\"txtTitle\" Left=\"4\" Top=\"2\" Width=\"270\" Height=\"24\" Text=\"[Tiêu đề]\" HorzAlign=\"Center\" VertAlign=\"Center\" Font=\"Tahoma, 11pt, style=Bold\"/>");
            sb.AppendLine("      <TextObject Name=\"txtBan\" Left=\"4\" Top=\"28\" Width=\"270\" Height=\"20\" Text=\"Bàn: [TENBAN] ([TENKHUVUC])\" HorzAlign=\"Center\" VertAlign=\"Center\" Font=\"Tahoma, 10pt, style=Bold\"/>");
            sb.AppendLine("      <TextObject Name=\"txtBill\" Left=\"4\" Top=\"50\" Width=\"150\" Height=\"18\" Text=\"Số phiếu: [SOHOADON]\" Font=\"Tahoma, 8pt\"/>");
            sb.AppendLine("      <TextObject Name=\"txtLanIn\" Left=\"156\" Top=\"50\" Width=\"118\" Height=\"18\" Text=\"Lần in: [LANIN]\" HorzAlign=\"Right\" Font=\"Tahoma, 8pt, style=Bold\"/>");
            sb.AppendLine("      <TextObject Name=\"txtUser\" Left=\"4\" Top=\"70\" Width=\"150\" Height=\"18\" Text=\"In bởi: [In bởi]\" Font=\"Tahoma, 8pt\"/>");
            sb.AppendLine("      <TextObject Name=\"txtTime\" Left=\"156\" Top=\"70\" Width=\"118\" Height=\"18\" Text=\"[NGAYIN]\" Format=\"Custom\" Format.Format=\"dd/MM HH:mm\" HorzAlign=\"Right\" Font=\"Tahoma, 8pt\"/>");
            sb.AppendLine("      <TableObject Name=\"tblHeader\" Left=\"4\" Top=\"90\" Width=\"270\" Height=\"16\">");
            if (includePrice)
            {
                sb.AppendLine("        <TableColumn Name=\"hc1\" Width=\"126\"/>");
                sb.AppendLine("        <TableColumn Name=\"hc2\" Width=\"44\"/>");
                sb.AppendLine("        <TableColumn Name=\"hc3\" Width=\"42\"/>");
                sb.AppendLine("        <TableColumn Name=\"hc4\" Width=\"58\"/>");
                sb.AppendLine("        <TableRow Name=\"hr1\" Height=\"16\">");
                sb.AppendLine("          <TableCell Name=\"hCell1\" Border.Lines=\"All\" Text=\"Mặt hàng\" Font=\"Tahoma, 8pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"hCell2\" Border.Lines=\"All\" Text=\"SL\" HorzAlign=\"Right\" Font=\"Tahoma, 8pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"hCell3\" Border.Lines=\"All\" Text=\"ĐVT\" HorzAlign=\"Center\" Font=\"Tahoma, 8pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"hCell4\" Border.Lines=\"All\" Text=\"Đơn giá\" HorzAlign=\"Right\" Font=\"Tahoma, 8pt, style=Bold\"/>");
                sb.AppendLine("        </TableRow>");
            }
            else
            {
                sb.AppendLine("        <TableColumn Name=\"hc1\" Width=\"172\"/>");
                sb.AppendLine("        <TableColumn Name=\"hc2\" Width=\"48\"/>");
                sb.AppendLine("        <TableColumn Name=\"hc3\" Width=\"50\"/>");
                sb.AppendLine("        <TableRow Name=\"hr1\" Height=\"16\">");
                sb.AppendLine("          <TableCell Name=\"hCell1\" Border.Lines=\"All\" Text=\"Mặt hàng\" Font=\"Tahoma, 8pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"hCell2\" Border.Lines=\"All\" Text=\"SL\" HorzAlign=\"Right\" Font=\"Tahoma, 8pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"hCell3\" Border.Lines=\"All\" Text=\"ĐVT\" HorzAlign=\"Center\" Font=\"Tahoma, 8pt, style=Bold\"/>");
                sb.AppendLine("        </TableRow>");
            }
            sb.AppendLine("      </TableObject>");
            sb.AppendLine("    </ReportTitleBand>");
            sb.AppendLine("    <DataBand Name=\"Data1\" Top=\"110\" Width=\"279.72\" Height=\"34\" CanGrow=\"true\" CanShrink=\"true\" DataSource=\"Table0\">");
            sb.AppendLine("      <TableObject Name=\"tblDetail\" Left=\"4\" Top=\"0\" Width=\"270\" Height=\"34\">");
            if (includePrice)
            {
                sb.AppendLine("        <TableColumn Name=\"dc1\" Width=\"126\"/>");
                sb.AppendLine("        <TableColumn Name=\"dc2\" Width=\"44\"/>");
                sb.AppendLine("        <TableColumn Name=\"dc3\" Width=\"42\"/>");
                sb.AppendLine("        <TableColumn Name=\"dc4\" Width=\"58\"/>");
                sb.AppendLine("        <TableRow Name=\"dr1\" Height=\"18\" AutoSize=\"true\">");
                sb.AppendLine("          <TableCell Name=\"dCellName\" Border.Lines=\"Left, Right, Top\" Text=\"[Table0.TENHANG]\" Font=\"Tahoma, 9pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellQty\" Border.Lines=\"Left, Right, Top\" Text=\"[Table0.SOLUONGSTR]\" HorzAlign=\"Right\" Font=\"Tahoma, 9pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellUnit\" Border.Lines=\"Left, Right, Top\" Text=\"[Table0.DVT]\" HorzAlign=\"Center\" Font=\"Tahoma, 8pt\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellPrice\" Border.Lines=\"Left, Right, Top\" Text=\"[Table0.DONGIA]\" Format=\"Custom\" Format.Format=\"n0\" HorzAlign=\"Right\" Font=\"Tahoma, 8pt\"/>");
                sb.AppendLine("        </TableRow>");
                sb.AppendLine("        <TableRow Name=\"dr2\" Height=\"16\" VisibleExpression=\"[Table0.NOTE]\" AutoSize=\"true\">");
                sb.AppendLine("          <TableCell Name=\"dCellNote\" Border.Lines=\"Left, Right, Bottom\" Text=\"Ghi chú: [Table0.NOTE]\" Font=\"Tahoma, 8pt, style=Italic\" ColSpan=\"4\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellN2\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellN3\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellN4\"/>");
                sb.AppendLine("        </TableRow>");
            }
            else
            {
                sb.AppendLine("        <TableColumn Name=\"dc1\" Width=\"172\"/>");
                sb.AppendLine("        <TableColumn Name=\"dc2\" Width=\"48\"/>");
                sb.AppendLine("        <TableColumn Name=\"dc3\" Width=\"50\"/>");
                sb.AppendLine("        <TableRow Name=\"dr1\" Height=\"18\" AutoSize=\"true\">");
                sb.AppendLine("          <TableCell Name=\"dCellName\" Border.Lines=\"Left, Right, Top\" Text=\"[Table0.TENHANG]\" Font=\"Tahoma, 9pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellQty\" Border.Lines=\"Left, Right, Top\" Text=\"[Table0.SOLUONGSTR]\" HorzAlign=\"Right\" Font=\"Tahoma, 9pt, style=Bold\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellUnit\" Border.Lines=\"Left, Right, Top\" Text=\"[Table0.DVT]\" HorzAlign=\"Center\" Font=\"Tahoma, 8pt\"/>");
                sb.AppendLine("        </TableRow>");
                sb.AppendLine("        <TableRow Name=\"dr2\" Height=\"16\" VisibleExpression=\"[Table0.NOTE]\" AutoSize=\"true\">");
                sb.AppendLine("          <TableCell Name=\"dCellNote\" Border.Lines=\"Left, Right, Bottom\" Text=\"Ghi chú: [Table0.NOTE]\" Font=\"Tahoma, 8pt, style=Italic\" ColSpan=\"3\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellN2\"/>");
                sb.AppendLine("          <TableCell Name=\"dCellN3\"/>");
                sb.AppendLine("        </TableRow>");
            }
            sb.AppendLine("      </TableObject>");
            sb.AppendLine("    </DataBand>");
            sb.AppendLine("  </ReportPage>");
            sb.AppendLine("</Report>");

            string xml = sb.ToString();
            return new PrintTemplateRecord
            {
                Id = "builtin-kitchen-80mm",
                Name = "Phiếu chế biến 80mm",
                TemplateXml = xml,
                TemplateBytes = new UTF8Encoding(false).GetBytes(xml)
            };
        }

        private static string FormatKitchenQty(decimal qty)
        {
            string prefix = qty > 0m ? "+" : "";
            string body = qty == Math.Truncate(qty)
                ? qty.ToString("0", CultureInfo.InvariantCulture)
                : qty.ToString("0.##", CultureInfo.InvariantCulture);
            return qty < 0m ? body : (prefix + body);
        }

        private static DataTable CreateTable0Schema()
        {
            var t = new DataTable("Table0");
            t.Columns.Add("TENHANG", typeof(string));
            t.Columns.Add("DMATHANG_NAME", typeof(string));
            t.Columns.Add("DVT", typeof(string));
            t.Columns.Add("DDONVITINH_NAME", typeof(string));
            t.Columns.Add("DDONVITINHID", typeof(string));
            t.Columns.Add("DLOAIDOID", typeof(string));
            t.Columns.Add("SOLUONG", typeof(decimal));
            t.Columns.Add("SOLUONGSTR", typeof(string));
            t.Columns.Add("DONGIA", typeof(decimal));
            t.Columns.Add("NOTE", typeof(string));
            return t;
        }
    }
}
