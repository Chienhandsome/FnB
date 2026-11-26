using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc mẫu in (STEMPLATE) và thông tin công ty (SCONFIG) phục vụ chức năng in FastReport.
    /// Không bao giờ ghi vào STEMPLATE; mọi câu SQL đều tham số hóa.
    /// </summary>
    public sealed class PrintTemplateRepository
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _databasePath;

        public PrintTemplateRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>
        /// Đọc danh sách mẫu in đang dùng (STATUS = 30) của một form (SFORMID), sắp tăng dần theo NAME.
        /// </summary>
        public List<PrintTemplateRecord> LoadTemplatesForForm(string formId)
        {
            if (string.IsNullOrEmpty(formId))
                return new List<PrintTemplateRecord>();

            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT ID, NAME, SFORMID, STABLEDESCID, TEMPLATE " +
                    "FROM STEMPLATE " +
                    "WHERE SFORMID = @FormId AND STATUS = @Status " +
                    "ORDER BY NAME";

                DataTable dt = DbHelper.Table(conn, null, sql,
                    "@FormId", formId,
                    "@Status", PrintConstants.ActiveStatus);

                return ReadTemplatesAndConfig(conn, dt);
            });
        }

        /// <summary>
        /// Đọc danh sách mẫu in đang dùng (STATUS = 30, có nội dung TEMPLATE) gắn theo bảng (STABLEDESCID),
        /// sắp tăng dần theo NAME. Dùng cho các phiếu (Đặt hàng, Báo giá) có SFORMID rỗng.
        /// </summary>
        public List<PrintTemplateRecord> LoadTemplatesForTable(string stableDescId)
        {
            if (string.IsNullOrEmpty(stableDescId))
                return new List<PrintTemplateRecord>();

            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT ID, NAME, SFORMID, STABLEDESCID, TEMPLATE " +
                    "FROM STEMPLATE " +
                    "WHERE STABLEDESCID = @TableDescId AND STATUS = @Status AND TEMPLATE IS NOT NULL " +
                    "ORDER BY NAME";

                DataTable dt = DbHelper.Table(conn, null, sql,
                    "@TableDescId", stableDescId,
                    "@Status", PrintConstants.ActiveStatus);

                return ReadTemplatesAndConfig(conn, dt);
            });
        }

        /// <summary>
        /// Đọc danh sách mẫu tem mã vạch đang dùng (STABLEDESCID, BARCODE = 30, STATUS = 30, có TEMPLATE),
        /// sắp tăng dần theo NAME. Chỉ đọc, SQL tham số hóa.
        /// </summary>
        public List<PrintTemplateRecord> LoadBarcodeTemplatesForTable(string stableDescId)
        {
            if (string.IsNullOrEmpty(stableDescId))
                return new List<PrintTemplateRecord>();

            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT ID, NAME, SFORMID, STABLEDESCID, TEMPLATE " +
                    "FROM STEMPLATE " +
                    "WHERE STABLEDESCID = @TableDescId AND BARCODE = @Barcode AND STATUS = @Status AND TEMPLATE IS NOT NULL " +
                    "ORDER BY NAME";

                DataTable dt = DbHelper.Table(conn, null, sql,
                    "@TableDescId", stableDescId,
                    "@Barcode", PrintConstants.BarcodeFlagValue,
                    "@Status", PrintConstants.ActiveStatus);

                return ReadTemplatesAndConfig(conn, dt);
            });
        }

        private static List<PrintTemplateRecord> ReadTemplatesAndConfig(FbConnection conn, DataTable dt)
        {
            var list = new List<PrintTemplateRecord>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                byte[] rawBytes;
                string xmlText;
                ReadTemplateContent(r["TEMPLATE"], out rawBytes, out xmlText);

                list.Add(new PrintTemplateRecord
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    FormId = DbHelper.Str(r["SFORMID"]) ?? "",
                    TableDescId = DbHelper.Str(r["STABLEDESCID"]) ?? "",
                    TemplateBytes = rawBytes,
                    TemplateXml = xmlText
                });
            }

            list.Sort(delegate (PrintTemplateRecord a, PrintTemplateRecord b)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(
                    a == null ? "" : (a.Name ?? ""),
                    b == null ? "" : (b.Name ?? ""));
            });

            List<string> configIds = PrintTemplateVisibilityParser.ExtractDistinctConfigIds(list);
            Dictionary<string, bool> configFlags = LoadConfigVisibilityByIds(conn, configIds);
            for (int i = 0; i < list.Count; i++)
                list[i].ConfigVisibilityById = new Dictionary<string, bool>(configFlags, StringComparer.OrdinalIgnoreCase);

            return list;
        }

        /// <summary>
        /// Đọc mẫu in mặc định của báo cáo từ SREPORT.LASTTEMPLATEID → SREPORTTEMPLATE.ID (chỉ đọc, SQL tham số hóa).
        /// Thiếu bản ghi SREPORT, thiếu LASTTEMPLATEID hoặc thiếu bản ghi SREPORTTEMPLATE đều trả null kèm câu lỗi nêu tên báo cáo,
        /// KHÔNG đoán mẫu khác.
        /// </summary>
        public PrintTemplateRecord LoadReportTemplate(string reportId, string reportDisplayName, out string errorMessage)
        {
            string fallbackName = string.IsNullOrWhiteSpace(reportDisplayName)
                ? (reportId ?? "(không tên)")
                : reportDisplayName.Trim();

            if (string.IsNullOrWhiteSpace(reportId))
            {
                errorMessage = "Thiếu mã báo cáo '" + fallbackName + "' để đọc mẫu in.";
                return null;
            }

            string localError = null;
            PrintTemplateRecord record = DbHelper.Read(_databasePath, conn =>
            {
                DataTable dtReport = DbHelper.Table(conn, null,
                    "SELECT NAME, LASTTEMPLATEID FROM SREPORT WHERE ID = @ReportId",
                    "@ReportId", reportId.Trim());

                if (dtReport.Rows.Count == 0)
                {
                    localError = FormatMissingReportError(fallbackName);
                    return null;
                }

                string dbReportName = (DbHelper.Str(dtReport.Rows[0]["NAME"]) ?? "").Trim();
                string effectiveName = string.IsNullOrWhiteSpace(reportDisplayName)
                    ? (dbReportName.Length > 0 ? dbReportName : fallbackName)
                    : reportDisplayName.Trim();
                string lastTemplateId = (DbHelper.Str(dtReport.Rows[0]["LASTTEMPLATEID"]) ?? "").Trim();

                if (lastTemplateId.Length == 0)
                {
                    localError = FormatMissingLastTemplateIdError(effectiveName);
                    return null;
                }

                DataTable dtTpl = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, SREPORTID, TEMPLATE FROM SREPORTTEMPLATE WHERE ID = @TemplateId",
                    "@TemplateId", lastTemplateId);

                if (dtTpl.Rows.Count == 0)
                {
                    localError = FormatMissingReportTemplateRecordError(effectiveName, lastTemplateId);
                    return null;
                }

                DataRow row = dtTpl.Rows[0];
                byte[] rawBytes;
                string xmlText;
                ReadTemplateContent(row["TEMPLATE"], out rawBytes, out xmlText);

                string tplName = (DbHelper.Str(row["NAME"]) ?? "").Trim();
                var tpl = new PrintTemplateRecord
                {
                    Id = DbHelper.Str(row["ID"]) ?? "",
                    Name = tplName.Length > 0 ? tplName : effectiveName,
                    FormId = DbHelper.Str(row["SREPORTID"]) ?? "",
                    TemplateBytes = rawBytes,
                    TemplateXml = xmlText
                };

                List<string> configIds = PrintTemplateVisibilityParser.ExtractDistinctConfigIds(new[] { tpl });
                tpl.ConfigVisibilityById = LoadConfigVisibilityByIds(conn, configIds);
                return tpl;
            });

            errorMessage = localError;
            return record;
        }

        public static string FormatMissingReportError(string reportName)
        {
            return "Không tìm thấy báo cáo '" + SafeReportName(reportName) + "' trong bảng SREPORT.";
        }

        public static string FormatMissingLastTemplateIdError(string reportName)
        {
            return "Báo cáo '" + SafeReportName(reportName) + "' chưa có mẫu in mặc định (thiếu LASTTEMPLATEID).";
        }

        public static string FormatMissingReportTemplateRecordError(string reportName, string templateId)
        {
            return "Không tìm thấy mẫu in '" + (templateId ?? "") + "' của báo cáo '" + SafeReportName(reportName) + "' trong bảng SREPORTTEMPLATE.";
        }

        private static string SafeReportName(string reportName)
        {
            return string.IsNullOrWhiteSpace(reportName) ? "(không tên)" : reportName.Trim();
        }

        /// <summary>
        /// Đọc trạng thái bật/tắt (INTVALUE = 30) của các bản ghi SCONFIG theo danh sách ID (SQL tham số hóa).
        /// ID không tồn tại trong SCONFIG sẽ không có trong từ điển trả về (engine giữ cột hiện, không đoán).
        /// </summary>
        public Dictionary<string, bool> LoadConfigVisibilityByIds(IEnumerable<string> configIds)
        {
            List<string> distinct = CollectDistinctIds(configIds);
            if (distinct.Count == 0)
                return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            return DbHelper.Read(_databasePath, conn => LoadConfigVisibilityByIds(conn, distinct));
        }

        /// <summary>Đọc trạng thái bật/tắt của mọi SCONFIG.ID mà một mẫu in tham chiếu qua VisibleByConfigID.</summary>
        public Dictionary<string, bool> LoadConfigVisibilityForTemplate(PrintTemplateRecord template)
        {
            if (template == null)
                return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            List<string> ids = PrintTemplateVisibilityParser.ExtractDistinctConfigIds(new[] { template });
            return LoadConfigVisibilityByIds(ids);
        }

        private static Dictionary<string, bool> LoadConfigVisibilityByIds(FbConnection conn, List<string> distinctIds)
        {
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (distinctIds == null || distinctIds.Count == 0)
                return map;

            const int chunkSize = 200;
            for (int start = 0; start < distinctIds.Count; start += chunkSize)
            {
                int len = Math.Min(chunkSize, distinctIds.Count - start);
                var placeholders = new string[len];
                var args = new object[len * 2];
                for (int i = 0; i < len; i++)
                {
                    string paramName = "@Id" + i;
                    placeholders[i] = paramName;
                    args[i * 2] = paramName;
                    args[i * 2 + 1] = distinctIds[start + i];
                }

                string sql = "SELECT ID, INTVALUE FROM SCONFIG WHERE ID IN (" + string.Join(", ", placeholders) + ")";
                DataTable dt = DbHelper.Table(conn, null, sql, args);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]);
                    if (!string.IsNullOrEmpty(id))
                        map[id.Trim()] = DbHelper.Int(r["INTVALUE"]) == PrintConstants.ConfigEnabledValue;
                }
            }
            return map;
        }

        private static List<string> CollectDistinctIds(IEnumerable<string> configIds)
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (configIds == null)
                return list;
            foreach (string raw in configIds)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                string id = raw.Trim();
                if (seen.Add(id))
                    list.Add(id);
            }
            return list;
        }

        /// <summary>Đọc thông tin công ty từ SCONFIG (TEXTVALUE): CompanyName, CompanyAddress, CompanyPhone, CompanyEmail, CompanyFax.</summary>
        public CompanyPrintInfo LoadCompanyInfo()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT NAME, TEXTVALUE FROM SCONFIG " +
                    "WHERE NAME IN (@K1, @K2, @K3, @K4, @K5)";

                DataTable dt = DbHelper.Table(conn, null, sql,
                    "@K1", PrintConstants.ConfigCompanyName,
                    "@K2", PrintConstants.ConfigCompanyAddress,
                    "@K3", PrintConstants.ConfigCompanyPhone,
                    "@K4", PrintConstants.ConfigCompanyEmail,
                    "@K5", PrintConstants.ConfigCompanyFax);

                var info = new CompanyPrintInfo();
                foreach (DataRow r in dt.Rows)
                {
                    string name = DbHelper.Str(r["NAME"]) ?? "";
                    string val = DbHelper.Str(r["TEXTVALUE"]) ?? "";
                    if (string.Equals(name, PrintConstants.ConfigCompanyName, StringComparison.Ordinal))
                        info.CompanyName = val;
                    else if (string.Equals(name, PrintConstants.ConfigCompanyAddress, StringComparison.Ordinal))
                        info.CompanyAddress = val;
                    else if (string.Equals(name, PrintConstants.ConfigCompanyPhone, StringComparison.Ordinal))
                        info.CompanyPhone = val;
                    else if (string.Equals(name, PrintConstants.ConfigCompanyEmail, StringComparison.Ordinal))
                        info.CompanyEmail = val;
                    else if (string.Equals(name, PrintConstants.ConfigCompanyFax, StringComparison.Ordinal))
                        info.CompanyFax = val;
                }
                return info;
            });
        }

        private static void ReadTemplateContent(object raw, out byte[] bytes, out string xml)
        {
            if (raw == null || raw is DBNull)
            {
                bytes = null;
                xml = "";
                return;
            }

            var blobBytes = raw as byte[];
            if (blobBytes != null)
            {
                bytes = blobBytes;
                xml = DecodeUtf8WithoutBom(blobBytes);
                return;
            }

            string text = Convert.ToString(raw) ?? "";
            xml = StripBom(text);
            bytes = xml.Length == 0 ? new byte[0] : Utf8NoBom.GetBytes(xml);
        }

        private static string DecodeUtf8WithoutBom(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return "";
            int offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                offset = 3;
            return StripBom(Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset));
        }

        private static string StripBom(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text[0] == '\uFEFF' ? text.Substring(1) : text;
        }
    }
}
