using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
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
                    "SELECT ID, NAME, SFORMID, TEMPLATE " +
                    "FROM STEMPLATE " +
                    "WHERE SFORMID = @FormId AND STATUS = @Status " +
                    "ORDER BY NAME";

                DataTable dt = DbHelper.Table(conn, null, sql,
                    "@FormId", formId,
                    "@Status", PrintConstants.ActiveStatus);

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
                return list;
            });
        }

        /// <summary>Đọc 4 thông tin công ty từ SCONFIG (TEXTVALUE): CompanyName, CompanyAddress, CompanyPhone, CompanyEmail.</summary>
        public CompanyPrintInfo LoadCompanyInfo()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT NAME, TEXTVALUE FROM SCONFIG " +
                    "WHERE NAME IN (@K1, @K2, @K3, @K4)";

                DataTable dt = DbHelper.Table(conn, null, sql,
                    "@K1", PrintConstants.ConfigCompanyName,
                    "@K2", PrintConstants.ConfigCompanyAddress,
                    "@K3", PrintConstants.ConfigCompanyPhone,
                    "@K4", PrintConstants.ConfigCompanyEmail);

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
