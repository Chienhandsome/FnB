using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Services;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>
    /// Kiểm thử tự động nhóm Hệ thống trên BẢN SAO DATA.FDB (lệnh --selftest &lt;db&gt; &lt;file kết quả&gt;).
    /// Chạy các ca trong docs/specs/he-thong.md mục 6; so sánh schema trước/sau để chứng minh không có DDL.
    /// Không bao giờ chạy trên file gốc: người chạy phải truyền bản sao.
    /// </summary>
    public static partial class SelfTest
    {
        private static readonly List<string> Lines = new List<string>();
        private static int _pass, _fail;

        private static void Check(string name, bool ok, string detail = null)
        {
            if (ok) _pass++; else _fail++;
            Lines.Add((ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
        }

        public static int Run(string databasePath, string outFile)
        {
            Lines.Clear();
            _pass = 0;
            _fail = 0;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest",
                    UserName = "selftest",
                    DisplayName = "selftest",
                    IsAdmin = true,
                    DatabasePath = databasePath,
                    Permissions = new FunctionPermissions(true)
                };
                DisableAutoCostForTests(databasePath);
                string schemaBefore = Schema(databasePath);

                ConfigTests(databasePath);
                RunExtra(databasePath);

                string schemaAfter = Schema(databasePath);
                Check("Không có DDL (schema trước = sau)", schemaBefore == schemaAfter);
            }
            catch (Exception ex)
            {
                Check("Ngoại lệ không mong đợi", false, ex.ToString());
            }
            Lines.Add("");
            Lines.Add("TỔNG: " + _pass + " đạt, " + _fail + " lỗi");
            File.WriteAllText(outFile, string.Join(Environment.NewLine, Lines.ToArray()), Encoding.UTF8);
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>Danh sách bảng/cột người dùng (để so sánh trước/sau).</summary>
        private static bool HasBlock(List<ReferenceBlock> blocks, string tableName)
        {
            foreach (ReferenceBlock b in blocks)
                if (b.TableName == tableName && b.Count > 0)
                    return true;
            return false;
        }

        private static string Schema(string db)
        {
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT TRIM(R.RDB$RELATION_NAME), TRIM(F.RDB$FIELD_NAME), F.RDB$FIELD_SOURCE " +
                    "FROM RDB$RELATIONS R JOIN RDB$RELATION_FIELDS F ON F.RDB$RELATION_NAME = R.RDB$RELATION_NAME " +
                    "WHERE R.RDB$SYSTEM_FLAG = 0 ORDER BY 1, 2");
                var sb = new StringBuilder();
                foreach (DataRow r in dt.Rows)
                    sb.Append(r[0]).Append('.').Append(r[1]).Append(':').Append(r[2]).Append(';');
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ H3 Cấu hình

        private static readonly string[] SampleKeys =
        {
            "ChoPhepTrungTenKhachHang", "CachTinhDiem", "DoanhSoTuongUngVoi1Diem", "TuDongNangCapThanhVienKhiDatHanMuc",
            "SoFileSaoLuu", "LamTronTien", "CoTienGio", "KichHoatLuuVetHoatDong", "ThoiGianChoPhepHuyBill", "CompanyName"
        };

        private static void ConfigTests(string db)
        {
            SystemConfig.Reload(db);
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                foreach (string key in SampleKeys)
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT DATATYPE, INTVALUE, TEXTVALUE FROM SCONFIG WHERE NAME = @N", "@N", key);
                    if (dt.Rows.Count == 0)
                    {
                        Check("SystemConfig đọc " + key + " (không có dòng → mặc định)", SystemConfig.GetInt(key) == 0);
                        continue;
                    }
                    int dataType = DbHelper.Int(dt.Rows[0][0]);
                    if (dataType == 1)
                        Check("SystemConfig đọc " + key, SystemConfig.GetText(key) == (DbHelper.Str(dt.Rows[0][2]) ?? ""));
                    else
                        Check("SystemConfig đọc " + key, SystemConfig.GetInt(key) == DbHelper.Int(dt.Rows[0][1]));
                }
            }

            // Ghi và đọc lại: ô số, ô tích, ô chữ; rồi hoàn lại giá trị cũ.
            var repo = new ConfigRepository(db);
            int oldBackup = SystemConfig.GetInt("SoFileSaoLuu");
            bool oldTrung = SystemConfig.GetBool("ChoPhepTrungTenKhachHang");
            string oldCompany = SystemConfig.GetText("CompanyName");
            int written = repo.Save(new[]
            {
                new ConfigChange { Name = "SoFileSaoLuu", DataType = 3, IntValue = 7 },
                new ConfigChange { Name = "ChoPhepTrungTenKhachHang", DataType = 3, IntValue = oldTrung ? 0 : 30 },
                new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = "CÔNG TY THỬ NGHIỆM" },
                new ConfigChange { Name = "KhongCoKhoaNay", DataType = 3, IntValue = 1 }
            }, "selftest");
            SystemConfig.Reload(db);
            Check("Save ghi đúng số khóa (khóa chưa có dòng bị bỏ qua)", written == 3, "written=" + written);
            Check("Ghi ô số", SystemConfig.GetInt("SoFileSaoLuu") == 7);
            Check("Ghi ô tích 30/0", SystemConfig.GetBool("ChoPhepTrungTenKhachHang") == !oldTrung);
            Check("Ghi ô chữ (tiếng Việt có dấu)", SystemConfig.GetText("CompanyName") == "CÔNG TY THỬ NGHIỆM");
            repo.Save(new[]
            {
                new ConfigChange { Name = "SoFileSaoLuu", DataType = 3, IntValue = oldBackup },
                new ConfigChange { Name = "ChoPhepTrungTenKhachHang", DataType = 3, IntValue = oldTrung ? 30 : 0 },
                new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = oldCompany }
            }, "selftest");
            SystemConfig.Reload(db);
            Check("Hoàn lại giá trị cũ", SystemConfig.GetInt("SoFileSaoLuu") == oldBackup && SystemConfig.GetText("CompanyName") == oldCompany);

            // ViewModel: sửa một ô tích + một ô số → IsDirty → Save → đọc lại → hoàn lại.
            var vm = new ConfigViewModel(db);
            Check("ViewModel mới nạp chưa có thay đổi", !vm.IsDirty);
            bool oldGiam = (bool)vm.Get("ChoPhepNhapGiamGia");
            decimal oldHuy = (decimal)vm.Get("ThoiGianChoPhepHuyBill");
            vm.Set("ChoPhepNhapGiamGia", !oldGiam);
            vm.Set("ThoiGianChoPhepHuyBill", oldHuy + 5m);
            Check("ViewModel báo có thay đổi", vm.IsDirty);
            int n = vm.Save("selftest");
            Check("ViewModel ghi đúng 2 khóa", n == 2, "n=" + n);
            Check("Sau Save SystemConfig thấy giá trị mới", SystemConfig.GetBool("ChoPhepNhapGiamGia") == !oldGiam && SystemConfig.GetInt("ThoiGianChoPhepHuyBill") == (int)oldHuy + 5);
            Check("Sau Save không còn thay đổi", !vm.IsDirty);
            vm.Set("ChoPhepNhapGiamGia", oldGiam);
            vm.Set("ThoiGianChoPhepHuyBill", oldHuy);
            vm.Save("selftest");
            Check("Hoàn lại qua ViewModel", SystemConfig.GetBool("ChoPhepNhapGiamGia") == oldGiam && SystemConfig.GetInt("ThoiGianChoPhepHuyBill") == (int)oldHuy);
            Check("Khóa giao diện cũ không hiện trên màn", vm.AllItems.TrueForAll(i => i.Name != "GiaoDienChuongTrinh" && i.Name != "DoToChuHienThi"));

            List<ConfigGroup> groups = repo.LoadVisibleGroups();
            Check("Nhóm hiển thị không có nhóm Ẩn", groups.Count > 0 && !groups.Exists(g => g.Name == "Ẩn"), "n=" + groups.Count);
            List<ConfigItem> items = repo.LoadItems();
            var badCombo = items.FindAll(i => i.Control == ConfigControl.Combo && i.ComboItems.Length == 0);
            Check("Mọi ô combo có danh sách mục", badCombo.Count == 0, string.Join(",", badCombo.ConvertAll(i => i.Name).ToArray()));
            Check("Khóa nội bộ (không nhãn) không hiện", !items.Exists(i => i.Name == "SharedCode" || i.Name == "CompliedCode" || i.Name == "DatabaseVersion"));
        }

        // ------------------------------------------------------------------ Quỹ tiền (docs/specs/quy-tien.md §4.2, §4.3, §4.5)

        /// <summary>Mô tả các dòng quỹ thành chuỗi ngắn để so sánh: "TM +100; TK[A] +200; THE[A] +300".</summary>
        private static string Funds(List<FundEntry> entries)
        {
            var parts = new List<string>();
            foreach (FundEntry e in entries)
                parts.Add((e.Fund == FundKind.Cash ? "TM" : (e.IsCard ? "THE[" : "TK[") + e.AccountId + "]") + " " +
                          e.Amount.ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture));
            return string.Join("; ", parts);
        }

        private static void CheckFunds(string name, List<FundEntry> entries, string expected)
        {
            string actual = Funds(entries);
            Check("Quỹ: " + name, actual == expected, "ra: " + actual);
        }

        private static FundPaymentResult Pay(decimal due, decimal cash, decimal transfer, decimal card, decimal nonCash, string account)
        {
            return FundRules.SettlePayment(new FundPaymentInput
            {
                Due = due, CashTendered = cash, Transfer = transfer, Card = card, NonCash = nonCash, AccountId = account
            });
        }

        private static void CheckPay(string name, FundPaymentResult r, decimal change, decimal cashKept)
        {
            Check("Quỹ: " + name, r.Error == null && r.Change == change && r.CashKept == cashKept,
                  r.Error ?? ("trả lại " + r.Change + ", TIENMAT " + r.CashKept));
        }

        /// <summary>Hóa đơn đã thanh toán dựng từ kết quả hộp thanh toán, để kiểm tiền vào quỹ từ đầu đến cuối.</summary>
        private static List<FundEntry> PaidBill(FundPaymentResult r, decimal transfer, decimal card, string account)
        {
            return FundRules.ForBill(new FundBill
            {
                PaidFlag = 30, Cash = r.CashKept, Transfer = transfer, Card = card, AccountId = account
            });
        }

        private static void FundTests()
        {
            // Hóa đơn 480.000 theo bốn cách trả.
            FundPaymentResult r = Pay(480000m, 500000m, 0m, 0m, 0m, "");
            CheckPay("tiền mặt đưa 500.000", r, 20000m, 480000m);
            CheckFunds("bill tiền mặt", PaidBill(r, 0m, 0m, ""), "TM +480000");
            r = Pay(480000m, 0m, 480000m, 0m, 0m, "A");
            CheckFunds("bill chuyển khoản", PaidBill(r, 480000m, 0m, "A"), "TK[A] +480000");
            r = Pay(480000m, 0m, 0m, 480000m, 0m, "A");
            CheckFunds("bill quẹt thẻ vào quỹ tài khoản", PaidBill(r, 0m, 480000m, "A"), "THE[A] +480000");
            r = Pay(480000m, 180000m, 300000m, 0m, 0m, "A");
            CheckFunds("bill tiền mặt + chuyển khoản", PaidBill(r, 300000m, 0m, "A"), "TM +180000; TK[A] +300000");

            // §4.3: chuyển dư, trả lại tiền mặt.
            r = Pay(480000m, 0m, 500000m, 0m, 0m, "A");
            CheckPay("chuyển khoản dư", r, 20000m, -20000m);
            CheckFunds("chuyển khoản dư trừ tiền mặt", PaidBill(r, 500000m, 0m, "A"), "TM -20000; TK[A] +500000");

            // Q2.
            Check("Quỹ: quẹt thẻ quá số phải trả bị chặn", Pay(480000m, 0m, 0m, 500000m, 0m, "A").Error == FundText.CardOverDue);
            Check("Quỹ: thẻ cộng chuyển khoản quá bill bị chặn", Pay(480000m, 0m, 300000m, 200000m, 0m, "A").Error == FundText.CardOverDue);
            Check("Quỹ: thẻ cộng voucher quá bill bị chặn", Pay(480000m, 0m, 0m, 100000m, 400000m, "A").Error == FundText.CardOverDue);
            CheckPay("voucher lớn hơn bill không trả lại tiền", Pay(480000m, 0m, 0m, 0m, 500000m, ""), 0m, 0m);
            CheckPay("voucher dư, khách vẫn đưa tiền mặt thì trả lại đủ tiền mặt", Pay(480000m, 50000m, 0m, 0m, 500000m, ""), 50000m, 0m);
            CheckPay("thẻ 100.000 + đưa tiền mặt 500.000", Pay(480000m, 500000m, 0m, 100000m, 0m, "A"), 120000m, 380000m);
            CheckPay("trả thiếu (ghi nợ) không có tiền trả lại", Pay(480000m, 400000m, 0m, 0m, 0m, ""), 0m, 400000m);
            Check("Quỹ: chuyển khoản thiếu tài khoản bị chặn", Pay(480000m, 0m, 480000m, 0m, 0m, "").Error == FundText.AccountRequired);
            Check("Quỹ: quẹt thẻ thiếu tài khoản bị chặn", Pay(480000m, 0m, 0m, 480000m, 0m, null).Error == FundText.AccountRequired);
            Check("Quỹ: số tiền âm bị chặn", Pay(480000m, -1m, 0m, 0m, 0m, "").Error == FundText.NegativeAmount);

            // Đặt trước 200.000 (phiếu thu) rồi đóng bill 480.000: chỉ còn phải trả 280.000.
            var deposit = FundRules.ForVoucher(new FundVoucher { Income = 200000m });
            deposit.AddRange(PaidBill(Pay(280000m, 280000m, 0m, 0m, 0m, ""), 0m, 0m, ""));
            decimal total = 0m;
            foreach (FundEntry e in deposit) total += e.Amount;
            Check("Quỹ: đặt trước không bị tính hai lần", total == 480000m, "ra " + total);
            CheckPay("hóa đơn âm không nhận thêm tiền", Pay(-50000m, 0m, 0m, 0m, 0m, ""), 0m, 0m);
            Check("Quỹ: hóa đơn âm nhận thêm tiền bị chặn", Pay(-50000m, 10000m, 0m, 0m, 0m, "").Error == FundText.NothingDue);

            // Hóa đơn không được tính.
            CheckFunds("bill chưa thanh toán", FundRules.ForBill(new FundBill { PaidFlag = 0, Cash = 100m }), "");
            CheckFunds("bill đã xóa mềm", FundRules.ForBill(new FundBill { PaidFlag = 30, Status = -30, Cash = 100m }), "");
            CheckFunds("bill LOAI 5", FundRules.ForBill(new FundBill { Type = 5, PaidFlag = 30, Cash = 100m }), "");
            CheckFunds("bill LOAI 6", FundRules.ForBill(new FundBill { Type = 6, PaidFlag = 30, Cash = 100m }), "");
            CheckFunds("bill quẹt thẻ không ghi tài khoản", FundRules.ForBill(new FundBill { PaidFlag = 30, Card = 100m }), "THE[] +100");

            // Phiếu thu/chi.
            CheckFunds("phiếu thu tiền mặt", FundRules.ForVoucher(new FundVoucher { Income = 100m, AccountId = "A" }), "TM +100");
            CheckFunds("phiếu chi chuyển khoản", FundRules.ForVoucher(new FundVoucher { Expense = 100m, TransferFlag = 30, AccountId = "A" }), "TK[A] -100");
            CheckFunds("cờ chuyển khoản khác 0 và khác 30", FundRules.ForVoucher(new FundVoucher { Income = 100m, TransferFlag = 1, AccountId = "A" }), "TK[A] +100");
            CheckFunds("phiếu đã xóa mềm", FundRules.ForVoucher(new FundVoucher { Status = -30, Income = 100m }), "");

            // Phiếu nhập và xuất trả hàng.
            CheckFunds("phiếu nhập cũ không ghi hình thức", FundRules.ForStockDocument(new FundStockDocument { Type = 1, PaidAmount = 300m }), "TM -300");
            CheckFunds("phiếu nhập chuyển khoản", FundRules.ForStockDocument(new FundStockDocument { Type = 1, PaidAmount = 300m, Transfer = 300m, AccountId = "A" }), "TK[A] -300");
            CheckFunds("xuất trả hàng NCC là tiền vào", FundRules.ForStockDocument(new FundStockDocument { Type = 2, SupplierId = "NCC", PaidAmount = 300m }), "TM +300");
            CheckFunds("LOAI 2 không có NCC", FundRules.ForStockDocument(new FundStockDocument { Type = 2, PaidAmount = 300m }), "");
            CheckFunds("phiếu nhập đã xóa mềm", FundRules.ForStockDocument(new FundStockDocument { Type = 1, Status = -30, PaidAmount = 300m }), "");
            CheckFunds("chứng từ LOAI 5", FundRules.ForStockDocument(new FundStockDocument { Type = 5, PaidAmount = 300m }), "");

            FundStockPaymentResult s = FundRules.StockPaymentFields(PayMethod.Cash, 300m, "A");
            Check("Quỹ: ghi phiếu nhập tiền mặt", s.Error == null && s.Cash == 300m && s.Transfer == 0m && s.AccountId == "");
            s = FundRules.StockPaymentFields(PayMethod.Transfer, 300m, "A");
            Check("Quỹ: ghi phiếu nhập chuyển khoản", s.Error == null && s.Cash == 0m && s.Transfer == 300m && s.AccountId == "A");
            Check("Quỹ: phiếu nhập chưa chọn hình thức bị chặn", FundRules.StockPaymentFields(null, 300m, "").Error == FundText.MethodRequired);
            Check("Quỹ: phiếu nhập chuyển khoản thiếu tài khoản bị chặn", FundRules.StockPaymentFields(PayMethod.Transfer, 300m, "").Error == FundText.AccountRequired);
            s = FundRules.StockPaymentFields(null, 0m, "");
            Check("Quỹ: phiếu nhập chưa trả tiền không cần hình thức", s.Error == null && s.Cash == 0m && s.Transfer == 0m);
        }

        // ------------------------------------------------------------------ Sổ quỹ (docs/specs/quy-tien.md §4.4, §4.7)

        private static DateTime Day(int day, int hour = 0, int minute = 0)
        {
            return new DateTime(2026, 11, day, hour, minute, 0);
        }

        private static FundSource Receipt(string number, DateTime time, string store, decimal income, decimal expense = 0m,
            string account = null, string reason = null)
        {
            return new FundSource
            {
                Kind = FundSourceKind.Voucher, Number = number, DocumentDate = time, StoreId = store, ReasonId = reason,
                Voucher = new FundVoucher { Income = income, Expense = expense, TransferFlag = account == null ? 0 : 30, AccountId = account }
            };
        }

        private static FundSource Opening(string number, DateTime time, string store, decimal amount, string account = null)
        {
            return Receipt(number, time, store, amount, 0m, account, FundReasons.OpeningBalanceId);
        }

        private static FundLedger Ledger(List<FundSource> sources, FundNodeKind node, int from, int to,
            string account = null, string onlyStore = null)
        {
            var stores = new List<string> { "CHA", "CHB" };
            return FundLedgerRules.Build(sources, new FundQuery
            {
                From = Day(from), To = Day(to), Node = node, AccountId = account,
                KnownAccountIds = new List<string> { "TK1", "TK2" },
                Scope = FundLedgerRules.ResolveScope(true, stores, new List<string>(), onlyStore)
            });
        }

        private static string Totals(FundLedger l)
        {
            return "đầu " + l.OpeningBalance + ", nhập " + l.OpeningEntered + ", thu " + l.Income + ", chi " + l.Expense +
                   ", cuối " + l.ClosingBalance + ", " + l.Lines.Count + " dòng";
        }

        private static void CheckLedger(string name, FundLedger l, decimal opening, decimal entered, decimal income,
            decimal expense, decimal closing)
        {
            Check("Sổ quỹ: " + name, l.OpeningBalance == opening && l.OpeningEntered == entered && l.Income == income &&
                                     l.Expense == expense && l.ClosingBalance == closing, Totals(l));
        }

        private static void FundLedgerTests()
        {
            // Q13: hai cửa hàng chung tài khoản.
            var shared = new List<FundSource>
            {
                Opening("DK", Day(1, 8), "CHA", 10000000m, "TK1"),
                Receipt("A1", Day(2, 9), "CHA", 2000000m, 0m, "TK1"),
                Receipt("B1", Day(2, 10), "CHB", 3000000m, 0m, "TK1")
            };
            CheckLedger("Q13 xem tất cả cửa hàng", Ledger(shared, FundNodeKind.Account, 1, 30, "TK1"), 0m, 10000000m, 5000000m, 0m, 15000000m);
            FundLedger onlyA = Ledger(shared, FundNodeKind.Account, 1, 30, "TK1", "CHA");
            Check("Sổ quỹ: Q13 lọc A chỉ có thu của A, không có tồn",
                  !onlyA.ShowBalance && onlyA.Income == 2000000m && onlyA.Lines.Count == 1 && onlyA.Lines[0].Balance == null
                  && onlyA.OpeningBalance == 0m && onlyA.ClosingBalance == 0m, Totals(onlyA));
            Check("Sổ quỹ: Q13 nút Tất cả khi lọc cửa hàng cũng không có tồn", !Ledger(shared, FundNodeKind.All, 1, 30, null, "CHA").ShowBalance);
            Check("Sổ quỹ: Q13 tiền mặt vẫn có tồn khi lọc cửa hàng", Ledger(shared, FundNodeKind.Cash, 1, 30, null, "CHA").ShowBalance);
            FundStoreScope one = FundLedgerRules.ResolveScope(false, new List<string> { "CHA" }, new List<string> { "CHA" }, "CHA");
            Check("Sổ quỹ: hệ thống một cửa hàng thì lọc nó vẫn là xem tất cả", one.CoversAll);
            FundStoreScope limited = FundLedgerRules.ResolveScope(false, new List<string> { "CHA", "CHB" }, new List<string> { "CHA" }, "");
            Check("Sổ quỹ: user bị giới hạn cửa hàng không phải xem tất cả", !limited.CoversAll && limited.StoreIds.Count == 1);
            FundStoreScope outside = FundLedgerRules.ResolveScope(false, new List<string> { "CHA", "CHB" }, new List<string> { "CHA" }, "CHB");
            Check("Sổ quỹ: chọn cửa hàng ngoài quyền thì không thấy gì", !outside.CoversAll && outside.StoreIds.Count == 0);
            Check("Sổ quỹ: chưa gán cửa hàng, một cửa hàng thì xem được",
                  FundLedgerRules.ResolveScope(false, new List<string> { "CHA" }, new List<string>(), "").StoreIds.Count == 1);
            Check("Sổ quỹ: chưa gán cửa hàng, nhiều cửa hàng thì không thấy",
                  FundLedgerRules.ResolveScope(false, new List<string> { "CHA", "CHB" }, new List<string>(), "").StoreIds.Count == 0);

            // Mốc số dư đầu kỳ tiền mặt: theo từng cửa hàng.
            var cash = new List<FundSource>
            {
                Receipt("TRUOC", Day(4, 9), "CHA", 111m),
                Receipt("CUNGNGAY-TRUOC", Day(5, 7), "CHA", 222m),
                Opening("DK-A", Day(5, 8), "CHA", 1000m),
                Receipt("CUNGNGAY-SAU", Day(5, 9), "CHA", 50m),
                Receipt("SAU", Day(6, 9), "CHA", 0m, 30m),
                Receipt("B-CHUAMOC", Day(3, 9), "CHB", 7m)
            };
            CheckLedger("mốc: chỉ cộng giao dịch sau mốc", Ledger(cash, FundNodeKind.Cash, 1, 30, null, "CHA"), 0m, 1000m, 50m, 30m, 1020m);
            CheckLedger("cửa hàng chưa có mốc tính từ đầu", Ledger(cash, FundNodeKind.Cash, 1, 30, null, "CHB"), 0m, 0m, 7m, 0m, 7m);
            CheckLedger("hai cửa hàng, mỗi quỹ một mốc", Ledger(cash, FundNodeKind.Cash, 1, 30), 0m, 1000m, 57m, 30m, 1027m);
            FundLedger missing = Ledger(cash, FundNodeKind.Cash, 1, 30);
            Check("Sổ quỹ: ghi chú cửa hàng chưa nhập số đầu kỳ",
                  missing.CashStoresWithoutOpening.Count == 1 && missing.CashStoresWithoutOpening[0] == "CHB");
            CheckLedger("xem khoảng trước ngày mốc không cộng giao dịch cũ", Ledger(cash, FundNodeKind.Cash, 1, 4, null, "CHA"), 0m, 0m, 0m, 0m, 0m);

            var zero = new List<FundSource> { Receipt("CU", Day(1, 9), "CHA", 500m), Opening("DK0", Day(2, 8), "CHA", 0m) };
            CheckLedger("số dư đầu kỳ bằng 0 vẫn là mốc", Ledger(zero, FundNodeKind.Cash, 1, 30, null, "CHA"), 0m, 0m, 0m, 0m, 0m);

            // Hai tài khoản mốc khác nhau + khoản chưa xác định.
            var banks = new List<FundSource>
            {
                Opening("DK1", Day(2, 8), "CHA", 100m, "TK1"),
                Opening("DK2", Day(10, 8), "CHA", 200m, "TK2"),
                Receipt("T1", Day(5, 9), "CHA", 10m, 0m, "TK1"),
                Receipt("T2-TRUOC", Day(5, 9), "CHA", 20m, 0m, "TK2"),
                Receipt("T2-SAU", Day(12, 9), "CHB", 5m, 0m, "TK2"),
                Receipt("LA", Day(13, 9), "CHA", 3m, 0m, "KHONG-CO"),
                new FundSource
                {
                    Kind = FundSourceKind.Sale, Number = "HD-THE", DocumentDate = Day(14), PaidTime = Day(14, 20), StoreId = "CHA",
                    Bill = new FundBill { PaidFlag = 30, Card = 7m }
                }
            };
            decimal total = Ledger(banks, FundNodeKind.AccountAll, 1, 30).ClosingBalance;
            decimal sum = Ledger(banks, FundNodeKind.Account, 1, 30, "TK1").ClosingBalance +
                          Ledger(banks, FundNodeKind.Account, 1, 30, "TK2").ClosingBalance +
                          Ledger(banks, FundNodeKind.UnknownAccount, 1, 30).ClosingBalance;
            Check("Sổ quỹ: hai tài khoản mốc khác nhau tính độc lập",
                  Ledger(banks, FundNodeKind.Account, 1, 30, "TK1").ClosingBalance == 110m &&
                  Ledger(banks, FundNodeKind.Account, 1, 30, "TK2").ClosingBalance == 205m);
            Check("Sổ quỹ: tổng tài khoản + chưa xác định = quỹ Tài khoản", total == sum && total == 325m, total + " / " + sum);
            Check("Sổ quỹ: có nút chưa xác định tài khoản", Ledger(banks, FundNodeKind.All, 1, 30).HasUnknownAccount);

            var mixed = new List<FundSource>
            {
                Receipt("CK", Day(3, 9), "CHA", 40m, 0m, "TK1"),
                new FundSource
                {
                    Kind = FundSourceKind.Sale, Number = "HD", DocumentDate = Day(3), PaidTime = Day(3, 10), StoreId = "CHA",
                    Bill = new FundBill { PaidFlag = 30, Cash = -20m, Transfer = 500m, Card = 60m, AccountId = "TK1" }
                }
            };
            FundLedger detail = Ledger(mixed, FundNodeKind.Account, 1, 30, "TK1");
            Check("Sổ quỹ: tách tổng chuyển khoản và quẹt thẻ", detail.TransferNet == 540m && detail.CardNet == 60m,
                  detail.TransferNet + " / " + detail.CardNet);
            CheckLedger("tiền trả lại hiện ở cột chi tiền mặt", Ledger(mixed, FundNodeKind.Cash, 1, 30), 0m, 0m, 0m, 20m, -20m);

            // Tồn đầu D+1 = tồn cuối D, mọi ngày, mọi nút.
            var all = new List<FundSource>();
            all.AddRange(cash);
            all.AddRange(banks);
            all.AddRange(mixed);
            all.Add(new FundSource
            {
                Kind = FundSourceKind.Stock, Number = "PN", DocumentDate = Day(11), CreatedTime = Day(11, 15), StoreId = "CHA",
                Stock = new FundStockDocument { Type = 1, PaidAmount = 300m }
            });
            string broken = "";
            foreach (FundNodeKind node in new[] { FundNodeKind.All, FundNodeKind.Cash, FundNodeKind.AccountAll })
                for (int d = 1; d < 20; d++)
                {
                    FundLedger today = Ledger(all, node, d, d);
                    FundLedger next = Ledger(all, node, d + 1, d + 1);
                    if (today.ClosingBalance != next.OpeningBalance)
                        broken += node + " ngày " + d + "; ";
                    if (today.ClosingBalance != today.OpeningBalance + today.OpeningEntered + today.Income - today.Expense)
                        broken += "cộng sai " + node + " ngày " + d + "; ";
                }
            Check("Sổ quỹ: tồn đầu hôm sau = tồn cuối hôm trước", broken.Length == 0, broken);

            // Thời điểm quỹ.
            Check("Sổ quỹ: bill qua đêm tính theo giờ thanh toán",
                  FundLedgerRules.FundTime(new FundSource { Kind = FundSourceKind.Sale, DocumentDate = Day(1), PaidTime = Day(2, 1) }) == Day(2, 1));
            Check("Sổ quỹ: NGAY không giờ, tạo cùng ngày thì lấy giờ tạo",
                  FundLedgerRules.FundTime(new FundSource { Kind = FundSourceKind.Voucher, DocumentDate = Day(3), CreatedTime = Day(3, 14) }) == Day(3, 14));
            Check("Sổ quỹ: phiếu lùi ngày vẫn nằm đúng ngày của phiếu",
                  FundLedgerRules.FundTime(new FundSource { Kind = FundSourceKind.Stock, DocumentDate = Day(3), CreatedTime = Day(9, 14) }) == Day(3));
            Check("Sổ quỹ: NGAY có giờ thì dùng NGAY",
                  FundLedgerRules.FundTime(new FundSource { Kind = FundSourceKind.Voucher, DocumentDate = Day(3, 6), CreatedTime = Day(3, 14) }) == Day(3, 6));
        }

        /// <summary>Seed giao dịch vào bản sao DB, đọc qua FundRepository rồi xóa. Dùng năm 2001 để không đụng dữ liệu khác.</summary>
        private static void FundRepositoryTests(string db)
        {
            var repo = new FundRepository(db);
            FundData before = repo.Load(new DateTime(2001, 3, 31));
            string store = before.Stores.Count > 0 ? before.Stores[0].Id : "";
            string account = before.Accounts.Count > 0 ? before.Accounts[0].Id : "";
            Check("Quỹ DB: đọc được cửa hàng và tài khoản", store.Length > 0 && account.Length > 0);
            var orders = new List<string>();
            var vouchers = new List<string>();
            string warehouse = DbHelper.NewId();
            try
            {
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, STATUS, DCUAHANGID, USERCREATEDID) VALUES (@I, 'KHO SELFTEST QUY', 30, @S, 'selftest')",
                    "@I", warehouse, "@S", store);

                Action<string, int, int, DateTime, decimal, decimal, decimal, decimal, string> order =
                    (name, type, status, date, cashAmount, transfer, card, paid, orderStore) =>
                    {
                        string id = DbHelper.NewId();
                        orders.Add(id);
                        BookingExec(db,
                            "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, GIOTHANHTOAN, TIENMAT, CHUYENKHOAN, THE, " +
                            "TIENTHANHTOAN, DTAIKHOANNGANHANGID, DCUAHANGID, DKHONHAPID, USERCREATEDID) VALUES (@I, @N, @L, @S, @P, @D, @G, @M, @C, @T, @A, @K, @H, @W, 'selftest')",
                            "@I", id, "@N", name, "@L", type, "@S", status, "@P", type == 0 ? (object)30 : 0,
                            "@D", date.Date, "@G", type == 0 ? (object)date : null,
                            "@M", cashAmount, "@C", transfer, "@T", card, "@A", paid,
                            "@K", transfer != 0m || card != 0m ? account : null,
                            "@H", orderStore, "@W", type == 1 ? warehouse : null);
                    };
                order("STQ-HD", 0, 30, new DateTime(2001, 3, 5, 10, 0, 0), -20000m, 500000m, 100000m, 580000m, store);
                order("STQ-HD-XOA", 0, -30, new DateTime(2001, 3, 5, 11, 0, 0), 999m, 0m, 0m, 999m, store);
                order("STQ-PN-CU", 1, 30, new DateTime(2001, 3, 6), 0m, 0m, 0m, 70000m, null); // cửa hàng lấy theo kho
                order("STQ-PN-CK", 1, 30, new DateTime(2001, 3, 7), 0m, 30000m, 0m, 30000m, store);
                order("STQ-SAU", 0, 30, new DateTime(2001, 4, 2, 9, 0, 0), 888m, 0m, 0m, 888m, store);

                Action<string, DateTime, decimal, decimal, int, string, int> voucher =
                    (name, date, income, expense, flag, reason, status) =>
                    {
                        string id = DbHelper.NewId();
                        vouchers.Add(id);
                        BookingExec(db,
                            "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, THU, CHI, CHUYENKHOAN, DTAIKHOANNGANHANGID, DCUAHANGID, DLYDOTHUCHIID, STATUS, USERCREATEDID) " +
                            "VALUES (@I, @N, @D, @D, @T, @C, @F, @K, @H, @R, @S, 'selftest')",
                            "@I", id, "@N", name, "@D", date, "@T", income, "@C", expense, "@F", flag,
                            "@K", flag != 0 ? account : null, "@H", store, "@R", reason, "@S", status);
                    };
                voucher("STQ-PT", new DateTime(2001, 3, 8, 8, 0, 0), 150000m, 0m, 0, null, 30);
                voucher("STQ-PC", new DateTime(2001, 3, 8, 9, 0, 0), 0m, 40000m, 30, null, 30);
                voucher("STQ-PT-XOA", new DateTime(2001, 3, 8, 10, 0, 0), 777m, 0m, 0, null, -30);

                FundData data = repo.Load(new DateTime(2001, 3, 31));
                Func<FundNodeKind, string, FundLedger> read = (node, accountId) => FundLedgerRules.Build(
                    data.Sources.FindAll(x => x.Number != null && x.Number.StartsWith("STQ-", StringComparison.Ordinal)),
                    new FundQuery
                    {
                        From = new DateTime(2001, 3, 1), To = new DateTime(2001, 3, 31), Node = node, AccountId = accountId,
                        KnownAccountIds = new List<string> { account },
                        Scope = new FundStoreScope { CoversAll = false, StoreIds = new List<string> { store } }
                    });
                // Tiền mặt: -20.000 (bill) - 70.000 (phiếu nhập cũ) + 150.000 (phiếu thu) = 60.000.
                CheckLedger("DB tiền mặt khớp số tính tay", read(FundNodeKind.Cash, null), 0m, 0m, 150000m, 90000m, 60000m);
                // Tài khoản (lọc cửa hàng nên chỉ xét phát sinh): thu 600.000; chi 30.000 (nhập CK) + 40.000 (phiếu chi).
                FundLedger bank = read(FundNodeKind.Account, account);
                Check("Quỹ DB: tài khoản khớp số tính tay", bank.Income == 600000m && bank.Expense == 70000m &&
                      bank.CardNet == 100000m && bank.TransferNet == 430000m, Totals(bank) + " thẻ " + bank.CardNet);
                Check("Quỹ DB: không lấy giao dịch sau ngày Đến", !data.Sources.Exists(x => x.Number == "STQ-SAU"));
                Check("Quỹ DB: không lấy bản ghi đã xóa mềm",
                      !data.Sources.Exists(x => x.Number == "STQ-HD-XOA" || x.Number == "STQ-PT-XOA"));
                Check("Quỹ DB: phiếu nhập không ghi cửa hàng lấy theo kho",
                      data.Sources.Exists(x => x.Number == "STQ-PN-CU" && x.StoreId == store));

                voucher("STQ-DK", new DateTime(2001, 4, 20, 8, 0, 0), 5000m, 0m, 0, FundReasons.OpeningBalanceId, 30);
                Check("Quỹ DB: phiếu số dư đầu kỳ sau ngày Đến vẫn được lấy làm mốc",
                      repo.Load(new DateTime(2001, 3, 31)).Sources.Exists(x => x.Number == "STQ-DK"));
            }
            finally
            {
                foreach (string id in orders)
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", id);
                foreach (string id in vouchers)
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", id);
                BookingExec(db, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", warehouse);
            }
        }

        // Các bước sau (ảnh, người dùng, quyền, mật khẩu) đăng ký thêm ở đây.
        private static void RunExtra(string db)
        {
            ThemeTests();
            FundTests();
            FundLedgerTests();
            FundRepositoryTests(db);
            CatalogTests(db);
            PhieuThuTests(db);
            ImageTests(db);
            UserTests(db);
            RoleTests(db);
            PasswordTests(db);
            BookingTests(db);
            ProductTests(db);
            RoomTests(db);
            DetailTabTests(db);
            CalendarTests(db);
            ActivityLogTests(db);
            CancelledBillTests(db);
            ItemSalesTests(db);
            RevenueReportTests(db);
            DailyActivityTests(db);
            BusinessResultTests(db);
            QuoteTests(db);
            BookingTwoUnitTests(db);
            NumberBoxTests(db);
            NumberGridTests(db);
            PrintP0Tests(db);
            PrintP1RevenueTests(db);
            PrintP1EmbeddedTests(db);
            PrintP2BookingTests(db);
            PrintP2QuoteTests(db);
            PrintP3aGridExcelTests(db);
            PrintP4BarcodeTests(db);
            CustomerTests(db);
            ServiceOrderTests(db);
            SalesManageTests(db);
            VoucherTests(db);
            WarehouseTests(db);
            SupplierTests(db);
            PurchaseTests(db);
            StockDocumentTests(db);
            InventoryTests(db);
            DebtTests(db);
            StockTakeTests(db);
            CostRecalcTests(db);
            WithMenuFlagsOn(db, "LuaChonVoucherTuDanhSach", () => GiftVoucherTests(db));
            WithMenuFlagsOn(db, "KichHoatKhuyenMaiTuDong", () => PromotionTests(db));
            LoyaltyTests(db);
            OpeningStockTests(db);
            OpeningDebtTests(db);
            MaterialRegenTests(db);
            StoreTests(db);
            EmployeeTests(db);
            ShiftTests(db);
            RewardPenaltyTests(db);
            SalaryAdvanceTests(db);
            PayrollTests(db);
            PayrollPayoutTests(db);
            RepairNoticeTests(db);
            WithMenuFlagsOn(db, "InMatKhauWifiTrenBill", () => WifiPasswordTests(db));
            WifiInvoiceTests(db);
            AreaInvoicePrinterTests(db);
            MenuConfigTests(db);
            AllWarehousesInventoryTests(db);
            WithMenuFlagsOn(db, "BatChucNangKiemSoatOrder", () => OrderControlTests(db));
            PrepaidCardTests(db);
            Chot2811BTests(db);
        }

        // ------------------------------------------------------------------ Tab liên quan dưới lưới (pane dưới)

        private static string TabTitles(List<DetailTab> tabs)
        {
            var parts = new List<string>();
            foreach (DetailTab t in tabs)
                parts.Add(t.Title);
            return string.Join(" | ", parts.ToArray());
        }

        private static void DetailTabTests(string db)
        {
            var repo = new DetailRepository(db);
            List<DetailTab> room = repo.LoadTabs("DBAN");
            Check("Tab bàn: đủ Bảng giá theo bàn, Đặt hàng, Hóa đơn nhà hàng, Phiếu nhập/xuất/chuyển kho, Phiếu kiểm kê, Sửa chữa",
                room.Exists(t => t.Title == "Bảng giá theo bàn") && room.Exists(t => t.Title == "Đặt hàng")
                && room.Exists(t => t.Title == "Hóa đơn nhà hàng") && room.Exists(t => t.Title == "Phiếu nhập kho")
                && room.Exists(t => t.Title == "Phiếu xuất kho") && room.Exists(t => t.Title == "Phiếu chuyển kho")
                && room.Exists(t => t.Title == "Phiếu kiểm kê") && room.Exists(t => t.Title == "Sửa chữa"), TabTitles(room));
            Check("Tab bàn: nhóm Đơn hàng lặp 2 lần (TDONHANG và TDONHANGGIO, như app gốc)",
                room.FindAll(t => t.Title == "Hóa đơn nhà hàng").Count == 2, TabTitles(room));
            Check("Tab bàn: Bảng giá theo bàn đứng trước Đặt hàng",
                room.FindIndex(t => t.Title == "Bảng giá theo bàn") < room.FindIndex(t => t.Title == "Đặt hàng"));

            List<DetailTab> booking = repo.LoadTabs("TDATHANG");
            string bookingTitles = TabTitles(booking);
            Check("Tab đặt hàng: đúng 4 tab (REFCONFIG ẩn phiếu kho/lưu tạm/phiếu chi)",
                bookingTitles == "Đặt hàng chi tiết | Hóa đơn nhà hàng | Phiếu thu | Phiếu thu công nợ", bookingTitles);

            bool allOk = true;
            string failed = "";
            foreach (DetailTab t in room)
            {
                try
                {
                    repo.LoadRows(t, "khong-co-dong-nay");
                }
                catch (Exception ex)
                {
                    allOk = false;
                    failed += t.Title + ": " + ex.Message + "; ";
                }
            }
            foreach (DetailTab t in booking)
            {
                try
                {
                    repo.LoadRows(t, "khong-co-dong-nay");
                }
                catch (Exception ex)
                {
                    allOk = false;
                    failed += t.Title + ": " + ex.Message + "; ";
                }
            }
            Check("Mọi truy vấn tab chạy được (SQL đúng tên cột)", allOk, failed);
            using (var box = new Kazuko_FnB.UI.Controls.InputBox())
            {
                int changed = 0;
                box.TextChanged += delegate { changed++; };
                box.Placeholder = "Tìm... (F3)";
                box.Text = "abc";
                Check("Ô tìm (InputBox): gõ chữ bắn TextChanged và đọc lại được Text", changed >= 1 && box.Text == "abc", "changed=" + changed);
                box.Text = "";
                Check("Ô tìm (InputBox): xóa chữ → Text rỗng, không đệ quy khi đọc Text", box.Text.Length == 0 && box.Inner.TextLength == 0);
            }
            // Thư mục "Phương thức đặt": thêm / sửa / xóa như thư mục thường.
            var methods = new BookingMethodRepository(db);
            string mErr;
            string rootId = null;
            string childId = null;
            try
            {
                CheckBookingMethodLoadAll(db, methods);
                var root = new BookingMethodRow { Name = "  THƯ MỤC THỬ KĐH  " };
                rootId = methods.Save(root, "selftest", out mErr);
                Check("Thêm thư mục gốc (cắt khoảng trắng hai đầu tên)", rootId != null && root.Name == "THƯ MỤC THỬ KĐH", mErr);
                BookingMethodRow back = methods.Load(rootId);
                Check("Thư mục mới: STATUS 30, người tạo, SORTORDER sau các thư mục cũ",
                    back != null && back.Status == 30 && back.CreatedById == "selftest" && string.CompareOrdinal(back.SortOrder, "ZZZd") > 0,
                    back == null ? "null" : back.SortOrder);
                var dup = new BookingMethodRow { Name = "thư mục thử kđh" };
                string dupId = methods.Save(dup, "selftest", out mErr);
                Check("Trùng tên (không phân biệt hoa/thường) → chặn", dupId == null && mErr == BookingMethodRepository.NameDuplicate("thư mục thử kđh"), mErr);
                Check("Tên trống → chặn", methods.Save(new BookingMethodRow { Name = "  " }, "selftest", out mErr) == null && mErr == BookingMethodRepository.NameEmpty);
                var child = new BookingMethodRow { Name = "THƯ MỤC CON THỬ", ParentId = rootId };
                childId = methods.Save(child, "selftest", out mErr);
                BookingMethodRow childBack = methods.Load(childId);
                Check("Thêm thư mục con: PARENTID và PARENTDIR đúng", childBack != null && childBack.ParentId == rootId && childBack.ParentDir == rootId + ":", mErr);
                back.Name = "THƯ MỤC THỬ ĐỔI TÊN";
                Check("Sửa tên thư mục", methods.Save(back, "selftest", out mErr) == rootId && methods.Load(rootId).Name == "THƯ MỤC THỬ ĐỔI TÊN" && methods.Load(rootId).ModifiedById == "selftest", mErr);
                Check("Xóa thư mục còn thư mục con → chặn", methods.Delete(rootId) != null && methods.Load(rootId) != null);
                var filtered = new BookingRepository(db).LoadBookings(new BookingFilter
                {
                    FromDate = new DateTime(2000, 1, 1), ToDate = DateTime.Today,
                    MethodIds = new List<string> { rootId, childId }
                });
                Check("Lọc phiếu theo thư mục và thư mục con chạy được (IN tham số hóa)", filtered != null);
            }
            finally
            {
                if (childId != null)
                    methods.Delete(childId);
                if (rootId != null)
                    methods.Delete(rootId);
            }
            Check("Xóa thư mục con rồi xóa thư mục cha → xóa được, không để lại dòng",
                rootId != null && methods.Load(rootId) == null && methods.Load(childId) == null);

            using (var view = new BookingListView(db))
            {
                var menuField = typeof(BookingListView).GetField("_gridMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var menu = menuField == null ? null : menuField.GetValue(view) as System.Windows.Forms.ContextMenuStrip;
                var names = new List<string>();
                if (menu != null)
                    foreach (System.Windows.Forms.ToolStripItem i in menu.Items)
                        names.Add(i.Text);
                Check("Lưới Khách đặt hàng có menu chuột phải (Thêm mới/Chỉnh sửa/Xóa/Sắp xếp/Refresh/Sao chép/Cột hiển thị)",
                    names.Contains("Thêm mới") && names.Contains("Chỉnh sửa") && names.Contains("Xóa") && names.Contains("Sắp xếp theo")
                    && names.Contains("Refresh") && names.Contains("Sao chép") && names.Contains("Cột hiển thị"), string.Join(" | ", names.ToArray()));
            }
            using (var db2 = new Kazuko_FnB.UI.Controls.DateBox())
            {
                db2.Value = new DateTime(2026, 11, 23);
                db2.Inner.Text = "5/1/2026";
                DateTime parsed = db2.Value;
                Check("DateBox: gõ 5/1/2026 → 05/01/2026", parsed == new DateTime(2026, 1, 5) && db2.Inner.Text == "05/01/2026", db2.Inner.Text);
                db2.Inner.Text = "abc";
                Check("DateBox: ngày sai → giữ ngày cũ", db2.Value == new DateTime(2026, 1, 5) && db2.Inner.Text == "05/01/2026", db2.Inner.Text);
            }
            Check("Tên cột khóa lạ bị từ chối (không ghép vào SQL)",
                repo.LoadRows(new DetailTab { TableName = "TDATHANG", KeyColumn = "ID; DROP TABLE X", Loai = -1 }, "x").Columns.Count == 0);
        }

        // ------------------------------------------------------------------ H6 Danh mục mặt hàng

        private static ProductValidationInput ValidBrewInput()
        {
            return new ProductValidationInput
            {
                TypeId = "1",
                TypeHasRecipe = true,
                UseTwoUnits = false,
                QuyDoi = 0m,
                UnitId = "u1",
                BigUnitId = null,
                Lines = new List<RecipeValidationLine>
                {
                    new RecipeValidationLine { MaterialId = "m1", MaterialTypeId = "2", Quantity = 1m }
                }
            };
        }

        // ------------------------------------------------------------------ H7 Khách đặt hàng

        private static BookingRules.BookingValidationInput ValidBookingInput()
        {
            return new BookingRules.BookingValidationInput
            {
                RoomIds = new List<string> { "r1" },
                Number = "DH26/00001",
                TenKhach = "Nguyễn Văn A",
                TuGio = new DateTime(2026, 11, 21, 18, 0, 0),
                DenGio = new DateTime(2026, 11, 21, 20, 0, 0),
                GioDat = new DateTime(2026, 11, 21, 10, 0, 0)
            };
        }

        private static BookingRow NewTestBooking(string number, string customerId, string methodId, string purposeId, List<BookingLine> lines)
        {
            BookingRules.BookingTotals t = BookingRules.CalcTotals(lines, 0m, 0m, false, 0m, 0m, 1000m);
            DateTime tuGio = DateTime.Today.AddHours(18);
            DateTime denGio = DateTime.Today.AddHours(20);
            DateTime tuNgay, denNgay;
            BookingRules.DeriveDates(tuGio, denGio, out tuNgay, out denNgay);
            return new BookingRow
            {
                Name = number,
                Ngay = DateTime.Today,
                GioDat = DateTime.Now,
                TuGio = tuGio,
                DenGio = denGio,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                CustomerId = customerId,
                TenKhach = "Nguyễn Văn A",
                DienThoai = "0901234567",
                DiaChi = "12 Xuân Thủy",
                MethodId = methodId,
                PurposeId = purposeId,
                Note = "Ghi chú thử",
                ColorArgb = -23296,
                TienHang = t.TienHang,
                TileGiamGia = t.TileGiamGia,
                TienGiamGia = t.TienGiamGia,
                TileThue = 0m,
                TienThue = t.TienThue,
                PhiVanChuyen = 0m,
                TongCong = t.TongCong
            };
        }

        private static void BookingExec(string db, string sql, params object[] args)
        {
            DbHelper.Read(db, conn => DbHelper.Exec(conn, null, sql, args));
        }

        private static int BookingCount(string db, string sql, params object[] args)
        {
            return DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, sql, args)));
        }

        private static void BookingTests(string db)
        {
            // Luật thuần §2.1 (đúng thứ tự, dừng ở lỗi đầu).
            BookingRules.BookingValidationInput input = ValidBookingInput();
            input.RoomIds = new List<string>();
            BookingRules.BookingValidationResult vr = BookingRules.ValidateBeforeSave(input);
            Check("2.1 chưa chọn bàn → chặn đúng câu gốc", vr.Error == BookingText.SelectRoomFirst, vr.Error);
            input = ValidBookingInput();
            input.Number = "";
            vr = BookingRules.ValidateBeforeSave(input);
            Check("2.1 thiếu số phiếu → chặn", vr.Error == BookingText.Required("Số phiếu"), vr.Error);
            input = ValidBookingInput();
            input.TenKhach = "";
            vr = BookingRules.ValidateBeforeSave(input);
            Check("2.1 thiếu người đặt → chặn", vr.Error == BookingText.Required("Người đặt"), vr.Error);
            input = ValidBookingInput();
            input.TuGio = null;
            vr = BookingRules.ValidateBeforeSave(input);
            Check("2.1 thiếu giờ vào → chặn", vr.Error == BookingText.Required("Giờ vào"), vr.Error);
            input = ValidBookingInput();
            input.DenGio = null;
            vr = BookingRules.ValidateBeforeSave(input);
            Check("2.1 thiếu giờ ra → chặn", vr.Error == BookingText.Required("Giờ ra"), vr.Error);
            input = ValidBookingInput();
            input.GioDat = null;
            vr = BookingRules.ValidateBeforeSave(input);
            Check("2.1 thiếu đặt lúc → chặn", vr.Error == BookingText.Required("Đặt lúc"), vr.Error);
            input = ValidBookingInput();
            input.DenGio = new DateTime(2026, 11, 21, 17, 0, 0);
            vr = BookingRules.ValidateBeforeSave(input);
            Check("5.4 giờ ra trước giờ vào → chặn, câu đã sửa", vr.Error == BookingText.DateOrder, vr.Error);
            input = ValidBookingInput();
            input.DenGio = new DateTime(2026, 11, 22, 1, 0, 0);
            vr = BookingRules.ValidateBeforeSave(input);
            Check("5.4 giờ ra sau giờ vào (khác ngày) → hợp lệ", vr.IsValid, vr.Error);
            Check("2.1 phiếu hợp lệ → lưu được", BookingRules.ValidateBeforeSave(ValidBookingInput()).IsValid);

            // BeforeSave #3/#4: TUNGAY/DENNGAY = ngày của TUGIO/DENGIO lúc 00:00.
            DateTime tuNgay, denNgay;
            BookingRules.DeriveDates(new DateTime(2026, 11, 21, 18, 30, 0), new DateTime(2026, 11, 22, 1, 0, 0), out tuNgay, out denNgay);
            Check("TUNGAY/DENNGAY suy từ giờ vào/ra", tuNgay == new DateTime(2026, 11, 21) && denNgay == new DateTime(2026, 11, 22),
                tuNgay.ToString("dd/MM/yyyy") + "/" + denNgay.ToString("dd/MM/yyyy"));

            // Tính tiền §3.3 + Q1 (LamTron 1000, ghi đè bội 100 của app gốc) + Q7.
            Check("Q7 thành tiền dòng = SL × đơn giá × (1 - %giảm)", BookingRules.CalcLineAmount(2m, 30000m, 10m) == 54000m);
            var lines155 = new List<BookingLine>
            {
                new BookingLine { ProductId = "p", UnitId = "u", Quantity = 1m, Price = 155000m, DiscountPct = 0m, Amount = 155000m }
            };
            BookingRules.BookingTotals t = BookingRules.CalcTotals(lines155, 10m, 0m, false, 10m, 0m, 1000m);
            Check("Q1 giảm 10% trên 155000 → làm tròn 16000 (LamTron 1000, AwayFromZero)",
                t.TienGiamGia == 16000m && t.TienHang == 155000m, t.TienGiamGia.ToString());
            Check("Thuế 10% trên (155000-16000) = 13900; tổng 152900",
                t.TienThue == 13900m && t.TongCong == 152900m, t.TienThue + "/" + t.TongCong);
            t = BookingRules.CalcTotals(lines155, 0m, 5000m, true, 0m, 0m, 1000m);
            Check("Giảm theo tiền 5000 → suy ngược tỉ lệ, tổng 150000",
                t.TileGiamGia == 5000m * 100m / 155000m && t.TongCong == 150000m, t.TileGiamGia.ToString());
            t = BookingRules.CalcTotals(lines155, 10m, 0m, false, 0m, 0m, 0m);
            Check("roundStep = 0 → không làm tròn (15500)", t.TienGiamGia == 15500m, t.TienGiamGia.ToString());

            // Sinh số DH(yy)/(*****).
            Check("Sinh số DH26/00001", DocumentNumber.Format("DH(yy)/(*****)", new DateTime(2026, 11, 21), 1) == "DH26/00001");
            Check("Sinh số PT26/00012", DocumentNumber.Format("PT(yy)/(*****)", new DateTime(2026, 11, 21), 12) == "PT26/00012");
            Check("Tiền tố số DH26/", DocumentNumber.LiteralPrefix("DH(yy)/(*****)", new DateTime(2026, 11, 21)) == "DH26/");
            Check("Số tiếp theo sau 00001/00009 là 10",
                DocumentNumber.NextSequence(new[] { "DH26/00001", "DH26/00009" }, "DH26/") == 10);

            // Gộp dòng trùng hàng + ĐVT (Q6.2: NhapMotMatHangNhieuLanTrongPhieu = 0 → cộng dồn).
            var mergeLines = new List<BookingLine>
            {
                new BookingLine { ProductId = "p1", UnitId = "u1", Quantity = 2m, Price = 30000m, DiscountPct = 0m, Amount = 60000m }
            };
            Check("Không cho gộp → thêm dòng mới", !BookingRules.TryMergeLine(mergeLines,
                new BookingLine { ProductId = "p1", UnitId = "u1", Quantity = 3m, Price = 30000m, DiscountPct = 0m }, false)
                && mergeLines.Count == 1);
            Check("Cho gộp: trùng hàng + ĐVT → cộng dồn SL (2+3=5)",
                BookingRules.TryMergeLine(mergeLines,
                    new BookingLine { ProductId = "p1", UnitId = "u1", Quantity = 3m, Price = 30000m, DiscountPct = 0m }, true)
                && mergeLines[0].Quantity == 5m && mergeLines[0].Amount == 150000m, mergeLines[0].Quantity.ToString());
            Check("Khác ĐVT → không gộp", !BookingRules.TryMergeLine(mergeLines,
                new BookingLine { ProductId = "p1", UnitId = "u2", Quantity = 1m, Price = 30000m, DiscountPct = 0m }, true));

            // Kế hoạch đoàn §3.1 (thuần).
            var plan = BookingRules.PlanGroupChanges(
                new List<GroupRowRef> { new GroupRowRef { Id = "A", RoomId = "r1", Name = "DH1" } }, "A",
                new List<string> { "r1", "r2" });
            Check("Đoàn: thêm bàn → giữ dòng cũ + tạo dòng mới",
                plan.CurrentRoomId == "r1" && plan.Keep.Count == 1 && plan.CreateRoomIds.Count == 1 && plan.CreateRoomIds[0] == "r2"
                && plan.Reuse.Count == 0 && plan.DeleteIds.Count == 0);
            plan = BookingRules.PlanGroupChanges(
                new List<GroupRowRef>(), "NEW", new List<string> { "r1", "r2" });
            Check("Đoàn: phiếu mới — dòng đang sửa đã chiếm bàn đầu, chỉ tạo thêm bàn 2",
                plan.CurrentRoomId == "r1" && plan.Keep.Count == 1 && plan.Keep[0].Id == "NEW" && plan.Keep[0].RoomId == "r1"
                && plan.CreateRoomIds.Count == 1 && plan.CreateRoomIds[0] == "r2" && plan.DeleteIds.Count == 0);
            plan = BookingRules.PlanGroupChanges(
                new List<GroupRowRef> { new GroupRowRef { Id = "A", RoomId = "r1", Name = "DH1" } }, "A",
                new List<string> { "r2" });
            Check("Đoàn: bàn cũ bị bỏ → dòng đang sửa lấy bàn đầu",
                plan.CurrentRoomId == "r2" && plan.Keep.Count == 1 && plan.Keep[0].RoomId == "r2" && plan.CreateRoomIds.Count == 0);
            plan = BookingRules.PlanGroupChanges(
                new List<GroupRowRef>
                {
                    new GroupRowRef { Id = "A", RoomId = "r1", Name = "DH1" },
                    new GroupRowRef { Id = "B", RoomId = "r2", Name = "DH2" }
                }, "A", new List<string> { "r2", "r3" });
            Check("Đoàn: dòng đang sửa lấy bàn đầu, dòng bàn bị lấy tái dùng cho bàn mới",
                plan.CurrentRoomId == "r2" && plan.Keep.Count == 1 && plan.Reuse.Count == 1 && plan.Reuse[0].Id == "B"
                && plan.Reuse[0].RoomId == "r3" && plan.DeleteIds.Count == 0);
            plan = BookingRules.PlanGroupChanges(
                new List<GroupRowRef>
                {
                    new GroupRowRef { Id = "A", RoomId = "r1", Name = "DH1" },
                    new GroupRowRef { Id = "B", RoomId = "r2", Name = "DH2" }
                }, "A", new List<string> { "r1" });
            Check("Đoàn: bớt bàn → dòng thừa bị xóa",
                plan.Keep.Count == 1 && plan.DeleteIds.Count == 1 && plan.DeleteIds[0] == "B");

            // Cấu hình + quyền §4: SFUNCTION "Đặt hàng" dùng chung với "Theo dõi đặt phòng".
            Check("Nhãn bàn/phòng mặc định \"Phòng\" (POS)", BookingRules.RoomLabel == "Phòng", BookingRules.RoomLabel);
            Check("App POS không phải HOTEL", !BookingRules.IsHotelApp);
            var perms = new FunctionPermissions(false);
            perms.Add(BookingText.FunctionId, BookingText.FunctionName, 0x50);
            Check("4 Thu ngân: Xem + Thêm", perms.Can("Đặt hàng", RoleModes.View) && perms.Can("Đặt hàng", RoleModes.Add));
            Check("4 Thu ngân: không Sửa/Xóa", !perms.Can("Đặt hàng", RoleModes.Edit) && !perms.Can("Đặt hàng", RoleModes.Remove));
            Check("4 không có dòng quyền = khóa", !new FunctionPermissions(false).Can("Đặt hàng", RoleModes.View));
            Check("4 Quản trị: đủ quyền", new FunctionPermissions(true).Can("Đặt hàng", RoleModes.Remove));

            // DB: tạo khu vực/bàn/khách/mặt hàng → lưu đơn 1 bàn, trùng số, hồi quy, đoàn nhiều bàn, cọc, xóa chặn, dọn sạch.
            var bookingRepo = new BookingRepository(db);
            var phieuThuRepo = new PhieuThuRepository(db);
            var productRepo = new ProductRepository(db);
            var groupRepo = new ProductGroupRepository(db);
            var formRepo = new FormRepository(db);
            List<LookupItem> units = formRepo.LoadAll("DDONVITINH");
            Check("Có đơn vị tính để thử", units.Count > 0, "n=" + units.Count);
            if (units.Count == 0)
                return;
            string unitId = units[0].Id;
            List<LookupItem> methods = formRepo.LoadAll("DPHUONGTHUCDAT");
            List<LookupItem> purposes = formRepo.LoadAll("DMUCDICHDAT");
            List<LookupItem> reasons = formRepo.LoadAll("DLYDOTHUCHI");
            string methodId = methods.Count > 0 ? methods[0].Id : null;
            string purposeId = purposes.Count > 0 ? purposes[0].Id : null;
            string reasonId = reasons.Count > 0 ? reasons[0].Id : null;

            string groupId = null;
            string productId = null;
            string bookingId = null;
            string retryId = null;
            string depositId = null;
            string areaId = DbHelper.NewId();
            string room1Id = DbHelper.NewId();
            string room2Id = DbHelper.NewId();
            string room3Id = DbHelper.NewId();
            string customerId = DbHelper.NewId();
            string groupGuid = null;
            try
            {
                string gerr;
                groupId = groupRepo.Save(new ProductGroupRow { Name = "NHÓM THỬ KĐH", Code = "KĐH" }, "selftest", out gerr);
                Check("Tạo nhóm thử", gerr == null && groupId != null, gerr);
                if (groupId == null)
                    return;
                var options = new ProductSaveOptions { AllowDuplicateName = false, CodeRequired = true, AutoUpdateBuyPrice = true, AutoUpdateCostPrice = true };
                var product = ProductRow.NewDefault();
                product.Name = "MẶT HÀNG THỬ KĐH";
                product.TypeId = "0";
                product.UnitId = unitId;
                product.GroupId = groupId;
                product.Code = "MHKDH001";
                product.SellPrice = 30000m;
                ProductSaveResult savedProduct = productRepo.SaveProduct(product, new List<RecipeLine>(), "selftest", options);
                Check("Tạo mặt hàng thử", savedProduct.Ok && savedProduct.SavedId != null, savedProduct.ErrorMessage);
                productId = savedProduct.SavedId;
                if (productId == null)
                    return;

                BookingExec(db, "INSERT INTO \"DKHUVUC\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", areaId, "@n", "KHU VỰC THỬ KĐH");
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                    "@id", room1Id, "@n", "BÀN THỬ KĐH 1", "@a", areaId);
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                    "@id", room2Id, "@n", "BÀN THỬ KĐH 2", "@a", areaId);
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                    "@id", room3Id, "@n", "BÀN THỬ KĐH 3", "@a", areaId);
                BookingExec(db, "INSERT INTO \"DKHACHHANG\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", customerId, "@n", "KHÁCH THỬ KĐH");

                List<BookingRoomItem> rooms = bookingRepo.LoadRooms();
                Check("LoadRooms thấy 3 bàn thử", rooms.Exists(r => r.Id == room1Id) && rooms.Exists(r => r.Id == room2Id) && rooms.Exists(r => r.Id == room3Id),
                    "n=" + rooms.Count);
                List<BookingSaleItem> items = bookingRepo.LoadSaleItems(false);
                Check("LoadSaleItems thấy mặt hàng thử", items.Exists(i => i.Id == productId), "n=" + items.Count);
                BookingCustomerInfo cust = bookingRepo.LoadCustomer(customerId);
                Check("LoadCustomer đúng tên", cust != null && cust.Name == "KHÁCH THỬ KĐH", cust == null ? "null" : cust.Name);

                string number = bookingRepo.GenerateNumber();
                Check("Sinh số phiếu DH26/00001 trên DB trống", number == "DH26/00001", number);
                var lines = new List<BookingLine>
                {
                    new BookingLine { ProductId = productId, ProductName = "MẶT HÀNG THỬ KĐH", UnitId = unitId, UnitName = units[0].Name, Quantity = 2m, Price = 30000m, DiscountPct = 0m, Amount = 60000m }
                };
                BookingRow row = NewTestBooking(number, customerId, methodId, purposeId, lines);
                BookingSaveResult saved = bookingRepo.SaveBooking(row, lines, new List<string> { room1Id }, "selftest");
                Check("Lưu phiếu 1 bàn", saved.Ok && saved.SavedId != null, saved.ErrorMessage);
                bookingId = saved.SavedId;
                if (bookingId == null)
                    return;
                BookingRow back = bookingRepo.LoadBooking(bookingId);
                Check("Đọc lại: đúng số phiếu/tên khách/tổng 60000",
                    back != null && back.Name == number && back.TenKhach == "Nguyễn Văn A" && back.TongCong == 60000m,
                    back == null ? "null" : back.Name + "/" + back.TongCong);
                Check("Đọc lại: 1 dòng đoàn, 1 chi tiết",
                    back.GroupRows != null && back.GroupRows.Count == 1 && back.Lines != null && back.Lines.Count == 1,
                    back.GroupRows == null ? "null" : back.GroupRows.Count.ToString());
                Check("Đọc lại: chi tiết đúng SL/đơn giá/thành tiền",
                    back.Lines[0].Quantity == 2m && back.Lines[0].Price == 30000m && back.Lines[0].Amount == 60000m);
                Check("Đọc lại: GUID đoàn đã sinh + STATUS 30",
                    !string.IsNullOrEmpty(back.Guid) && back.Status == 30, back.Guid);
                Check("Đọc lại: LOAI/LOAIGIA giữ 0", back.Loai == 0 && back.LoaiGia == 0);

                List<BookingListRow> found = bookingRepo.LoadBookings(new BookingFilter
                {
                    NumberText = number,
                    FromDate = DateTime.Today.AddDays(-30),
                    ToDate = DateTime.Today,
                    CustomerId = null,
                    MethodId = null
                });
                Check("Lọc theo số phiếu thấy 1 dòng", found.Count == 1, "n=" + found.Count);

                BookingRow dup = NewTestBooking(number, customerId, methodId, purposeId, lines);
                BookingSaveResult d = bookingRepo.SaveBooking(dup, lines, new List<string> { room1Id }, "selftest");
                Check("Trùng số phiếu bị chặn (nguyên văn)", !d.Ok && d.ErrorMessage == BookingText.NumberDuplicate(number), d.ErrorMessage);

                // Hồi quy: lưu mới trùng số (lỗi) → đổi số → lưu lại phải ghi thật, không để dòng mồ côi.
                BookingRow retry = NewTestBooking(number, customerId, methodId, purposeId, lines);
                BookingSaveResult r1 = bookingRepo.SaveBooking(retry, lines, new List<string> { room1Id }, "selftest");
                Check("Hồi quy: lưu trùng số → lỗi và row.Id vẫn rỗng", !r1.Ok && string.IsNullOrEmpty(retry.Id), "Id=" + retry.Id);
                retry.Name = bookingRepo.GenerateNumber();
                BookingSaveResult r2 = bookingRepo.SaveBooking(retry, lines, new List<string> { room1Id }, "selftest");
                Check("Hồi quy: đổi số rồi lưu lại → Ok", r2.Ok && r2.SavedId != null, r2.ErrorMessage);
                retryId = r2.SavedId;
                if (retryId == null)
                    return;
                Check("Hồi quy: số mới là DH26/00002", retry.Name == "DH26/00002", retry.Name);

                // Đoàn: thêm bàn → 2 dòng cùng GUID, chi tiết chép sang cả 2 dòng.
                BookingRow retryBack = bookingRepo.LoadBooking(retryId);
                BookingSaveResult m = bookingRepo.SaveBooking(retryBack, lines, new List<string> { room1Id, room2Id }, "selftest");
                Check("Sửa: thêm bàn → đoàn 2 dòng", m.Ok && m.GroupIds != null && m.GroupIds.Count == 2, m.ErrorMessage);
                BookingRow mBack = bookingRepo.LoadBooking(retryId);
                groupGuid = mBack.Guid;
                Check("Sửa: 2 dòng cùng GUID", mBack.GroupRows.Count == 2 && !string.IsNullOrEmpty(mBack.Guid),
                    mBack.GroupRows.Count + "/" + mBack.Guid);
                string secondId = null;
                foreach (GroupRowRef gr in mBack.GroupRows)
                    if (gr.Id != retryId)
                        secondId = gr.Id;
                Check("Sửa: chi tiết chép sang dòng đoàn thứ 2",
                    secondId != null && BookingCount(db, "SELECT COUNT(*) FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", secondId) == 1,
                    "second=" + secondId);

                // Đoàn: đổi sang 2 bàn khác — dòng đang sửa lấy bàn đầu, dòng kia tái dùng cho bàn mới.
                BookingSaveResult m2 = bookingRepo.SaveBooking(mBack, lines, new List<string> { room2Id, room3Id }, "selftest");
                Check("Sửa: đổi đoàn sang 2 bàn khác", m2.Ok && m2.GroupIds != null && m2.GroupIds.Count == 2, m2.ErrorMessage);
                BookingRow m2Back = bookingRepo.LoadBooking(retryId);
                var roomSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (GroupRowRef gr in m2Back.GroupRows)
                    roomSet.Add(gr.RoomId);
                Check("Sửa: đoàn còn đúng bàn 2 + bàn 3, giữ nguyên số phiếu",
                    m2Back.GroupRows.Count == 2 && roomSet.Contains(room2Id) && roomSet.Contains(room3Id)
                    && m2Back.Name == retry.Name, m2Back.Name);

                // Đoàn: bớt bàn → dòng thừa bị xóa.
                BookingSaveResult m3 = bookingRepo.SaveBooking(m2Back, lines, new List<string> { room2Id }, "selftest");
                Check("Sửa: bớt bàn → đoàn còn 1 dòng", m3.Ok && m3.GroupIds != null && m3.GroupIds.Count == 1, m3.ErrorMessage);
                Check("Sửa: dòng thừa đã xóa khỏi DB",
                    BookingCount(db, "SELECT COUNT(*) FROM \"TDATHANG\" WHERE \"GUID\" = @g", "@g", groupGuid) == 1, "");

                // Đặt tiền trước §3.5: phiếu thu THU = TONGCONG, gắn TDATHANGID.
                string ptNumber = phieuThuRepo.GenerateNumber();
                Check("Sinh số phiếu thu PT26/00001", ptNumber == "PT26/00001", ptNumber);
                var dep = new BookingDeposit
                {
                    BookingId = retryId,
                    Number = ptNumber,
                    Date = DateTime.Today,
                    ObjectType = 2,
                    CustomerId = customerId,
                    CustomerName = "Nguyễn Văn A",
                    Address = "12 Xuân Thủy",
                    ReasonId = reasonId,
                    Note = BookingText.DepositNote(m2Back.Name),
                    Amount = 60000m,
                    Transfer = false
                };
                depositId = phieuThuRepo.SaveDeposit(dep, "selftest");
                Check("Lưu phiếu thu cọc THU = TONGCONG", depositId != null, depositId ?? "null");
                BookingDepositInfo info = phieuThuRepo.LoadDeposit(retryId);
                Check("Đọc lại cọc: đúng số phiếu + số tiền",
                    info != null && info.Number == ptNumber && info.Amount == 60000m,
                    info == null ? "null" : info.Number + "/" + info.Amount);
                Check("Lý do cọc gần nhất = lý do vừa lưu", phieuThuRepo.LoadLatestDepositReasonId() == reasonId,
                    phieuThuRepo.LoadLatestDepositReasonId() ?? "null");

                BookingDeleteResult blocked = bookingRepo.DeleteBooking(retryId);
                Check("Xóa bị chặn khi có phiếu thu cọc", !blocked.Ok && blocked.ErrorMessage == BookingText.DeleteBlocked, blocked.ErrorMessage);
                BookingExec(db, "DELETE FROM \"TTHUCHI\" WHERE \"ID\" = @id", "@id", depositId);
                depositId = null;
                string deletedId = retryId;
                BookingDeleteResult deleted = bookingRepo.DeleteBooking(retryId);
                Check("Xóa đoàn sau khi gỡ cọc", deleted.Ok, deleted.ErrorMessage);
                retryId = null;
                Check("Xóa không để lại dòng TDATHANG",
                    BookingCount(db, "SELECT COUNT(*) FROM \"TDATHANG\" WHERE \"GUID\" = @g", "@g", groupGuid) == 0, "");
                Check("Xóa không để lại dòng chi tiết mồ côi",
                    BookingCount(db, "SELECT COUNT(*) FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", deletedId) == 0, "");

                BookingDeleteResult d1 = bookingRepo.DeleteBooking(bookingId);
                Check("Xóa phiếu 1 bàn", d1.Ok, d1.ErrorMessage);
                bookingId = null;

                var ghost = NewTestBooking(bookingRepo.GenerateNumber(), customerId, methodId, purposeId, lines);
                ghost.Id = "KHONG-TON-TAI-KDH";
                BookingSaveResult g = bookingRepo.SaveBooking(ghost, lines, new List<string> { room1Id }, "selftest");
                Check("Sửa dòng không tồn tại → lỗi, không báo Ok", !g.Ok && g.ErrorMessage == BookingText.NotFound, g.ErrorMessage);
            }
            finally
            {
                try { if (bookingId != null) bookingRepo.DeleteBooking(bookingId); } catch { }
                try { if (retryId != null) bookingRepo.DeleteBooking(retryId); } catch { }
                try { if (depositId != null) BookingExec(db, "DELETE FROM \"TTHUCHI\" WHERE \"ID\" = @id", "@id", depositId); } catch { }
                try { if (productId != null) productRepo.DeleteProduct(productId); } catch { }
                try { if (groupId != null) groupRepo.Delete(groupId); } catch { }
                try { BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" = @id", "@id", room1Id); } catch { }
                try { BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" = @id", "@id", room2Id); } catch { }
                try { BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" = @id", "@id", room3Id); } catch { }
                try { BookingExec(db, "DELETE FROM \"DKHUVUC\" WHERE \"ID\" = @id", "@id", areaId); } catch { }
                try { BookingExec(db, "DELETE FROM \"DKHACHHANG\" WHERE \"ID\" = @id", "@id", customerId); } catch { }
            }
        }

        // ------------------------------------------------------------------ Theo dõi đặt phòng (lưới lịch)

        private static BookingRow NewCalendarBooking(string number, string customerId, string methodId, string purposeId,
            DateTime tuGio, DateTime denGio)
        {
            BookingRules.BookingTotals t = BookingRules.CalcTotals(new List<BookingLine>(), 0m, 0m, false, 0m, 0m, 1000m);
            DateTime tuNgay, denNgay;
            BookingRules.DeriveDates(tuGio, denGio, out tuNgay, out denNgay);
            return new BookingRow
            {
                Name = number,
                Ngay = DateTime.Today,
                GioDat = DateTime.Now,
                TuGio = tuGio,
                DenGio = denGio,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                CustomerId = customerId,
                TenKhach = "Nguyễn Văn A",
                DienThoai = "0901234567",
                DiaChi = "12 Xuân Thủy",
                MethodId = methodId,
                PurposeId = purposeId,
                Note = "Ghi chú thử",
                ColorArgb = -23296,
                TienHang = t.TienHang,
                TileGiamGia = t.TileGiamGia,
                TienGiamGia = t.TienGiamGia,
                TileThue = 0m,
                TienThue = t.TienThue,
                PhiVanChuyen = 0m,
                TongCong = t.TongCong
            };
        }

        private static string CalendarRowValues(string db, string bookingId)
        {
            return DbHelper.Read(db, conn =>
            {
                using (FbCommand cmd = new FbCommand(
                    "SELECT \"DBANID\", \"TUNGAY\", \"DENNGAY\", \"TUGIO\", \"DENGIO\", \"GUID\" FROM \"TDATHANG\" WHERE \"ID\" = @id", conn))
                {
                    cmd.Parameters.Add("@id", FbDbType.VarChar).Value = bookingId;
                    using (FbDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return "MISSING";
                        return r.GetString(0) + "|" + r.GetDateTime(1).ToString("yyyy-MM-dd") + "|" +
                               r.GetDateTime(2).ToString("yyyy-MM-dd") + "|" +
                               r.GetDateTime(3).ToString("yyyy-MM-dd HH:mm") + "|" +
                               r.GetDateTime(4).ToString("yyyy-MM-dd HH:mm") + "|" + r.GetString(5);
                    }
                }
            });
        }

        private static void CalendarTests(string db)
        {
            // --- Luật thuần (không cần DB) ---
            Check("Lịch nhãn thứ: Thứ 2 = T2", BookingCalendarRules.WeekdayLabel(new DateTime(2026, 11, 23)) == "23.T2");
            Check("Lịch nhãn thứ: Thứ 3 = T3", BookingCalendarRules.WeekdayLabel(new DateTime(2026, 11, 24)) == "24.T3");
            Check("Lịch nhãn thứ: Thứ 4 hiện T5 (giữ bug app gốc)", BookingCalendarRules.WeekdayLabel(new DateTime(2026, 11, 25)) == "25.T5");
            Check("Lịch nhãn thứ: Thứ 5 hiện T4 (giữ bug app gốc)", BookingCalendarRules.WeekdayLabel(new DateTime(2026, 11, 26)) == "26.T4");
            Check("Lịch nhãn thứ: Thứ 6 = T6", BookingCalendarRules.WeekdayLabel(new DateTime(2026, 11, 27)) == "27.T6");
            Check("Lịch nhãn thứ: Thứ 7 = T7", BookingCalendarRules.WeekdayLabel(new DateTime(2026, 11, 28)) == "28.T7");
            Check("Lịch nhãn thứ: Chủ nhật = CN", BookingCalendarRules.WeekdayLabel(new DateTime(2026, 11, 29)) == "29.CN");
            Check("Lịch rộng cột ngày: (1536,7)=181", BookingCalendarRules.DayColumnWidth(1536, 7) == 181);
            Check("Lịch rộng cột ngày: tối thiểu 40",
                BookingCalendarRules.DayColumnWidth(300, 7) == 40 && BookingCalendarRules.DayColumnWidth(1000, 90) == 40);
            DateTime from = DateTime.Today.AddDays(1);
            int days = 7;
            int first, last;
            Check("Lịch cắt thanh: trong cửa sổ [1,3]",
                BookingCalendarRules.TryClipBar(from.AddDays(1), from.AddDays(3), from, days, out first, out last) && first == 1 && last == 3);
            Check("Lịch cắt thanh: bao trùm cửa sổ → cắt còn [0,6]",
                BookingCalendarRules.TryClipBar(from.AddDays(-2), from.AddDays(10), from, days, out first, out last) && first == 0 && last == 6);
            Check("Lịch cắt thanh: ngoài hẳn (trước) → không vẽ",
                !BookingCalendarRules.TryClipBar(from.AddDays(-3), from.AddDays(-1), from, days, out first, out last));
            Check("Lịch cắt thanh: ngoài hẳn (sau) → không vẽ",
                !BookingCalendarRules.TryClipBar(from.AddDays(7), from.AddDays(9), from, days, out first, out last));
            Check("Lịch cắt thanh: phiếu 1 ngày [2,2]",
                BookingCalendarRules.TryClipBar(from.AddDays(2), from.AddDays(2), from, days, out first, out last) && first == 2 && last == 2);
            Check("Lịch cột Tổng: (1,3)=2; phiếu 1 ngày=0 (giữ bug thiếu +1)",
                BookingCalendarRules.ClippedBarDays(1, 3) == 2 && BookingCalendarRules.ClippedBarDays(2, 2) == 0);
            Check("Lịch vượt ngoài cửa sổ: bắt đầu trước",
                BookingCalendarRules.IsOutOfWindow(from.AddDays(-1), from.AddDays(1), from, days));
            Check("Lịch vượt ngoài cửa sổ: kết thúc sau",
                BookingCalendarRules.IsOutOfWindow(from.AddDays(1), from.AddDays(7), from, days));
            Check("Lịch trong cửa sổ: không vượt",
                !BookingCalendarRules.IsOutOfWindow(from.AddDays(1), from.AddDays(3), from, days));
            Check("Lịch đổi số ngày: 30 /2 = 15", BookingCalendarRules.ChangeDaysCount(30, "/2") == 15);
            Check("Lịch đổi số ngày: 15 /2 = 8 (làm tròn lên)", BookingCalendarRules.ChangeDaysCount(15, "/2") == 8);
            Check("Lịch đổi số ngày: 7 /4 kẹp tối thiểu 7", BookingCalendarRules.ChangeDaysCount(7, "/4") == 7);
            Check("Lịch đổi số ngày: 30 2x = 60", BookingCalendarRules.ChangeDaysCount(30, "2x") == 60);
            Check("Lịch đổi số ngày: 60 4x kẹp tối đa 90", BookingCalendarRules.ChangeDaysCount(60, "4x") == 90);
            Check("Lịch đổi số ngày: 90 2x kẹp tối đa 90", BookingCalendarRules.ChangeDaysCount(90, "2x") == 90);
            DateTime mFrom; int mDays;
            BookingCalendarRules.MonthViewRange(new DateTime(2026, 11, 23), 0, out mFrom, out mDays);
            Check("Lịch tháng này: 01/11, 30 ngày", mFrom == new DateTime(2026, 11, 1) && mDays == 30);
            BookingCalendarRules.MonthViewRange(new DateTime(2026, 11, 23), -1, out mFrom, out mDays);
            Check("Lịch tháng trước: 01/10, 31 ngày", mFrom == new DateTime(2026, 10, 1) && mDays == 31);
            BookingCalendarRules.MonthViewRange(new DateTime(2026, 11, 23), 1, out mFrom, out mDays);
            Check("Lịch tháng sau: 01/12, 31 ngày", mFrom == new DateTime(2026, 12, 1) && mDays == 31);
            Check("Lịch vị trí thả: hợp lệ", BookingCalendarRules.ValidateDrop(0, 3, 1, 3, 7));
            Check("Lịch vị trí thả: hàng ngoài → sai",
                !BookingCalendarRules.ValidateDrop(3, 3, 1, 3, 7) && !BookingCalendarRules.ValidateDrop(-1, 3, 1, 3, 7));
            Check("Lịch vị trí thả: ngày ngoài → sai",
                !BookingCalendarRules.ValidateDrop(0, 3, -1, 3, 7) && !BookingCalendarRules.ValidateDrop(0, 3, 1, 7, 7));
            Check("Lịch vị trí thả: bắt đầu sau kết thúc → sai", !BookingCalendarRules.ValidateDrop(0, 3, 3, 1, 7));
            List<string> changes = BookingCalendarRules.BuildDragChanges("bàn 1", "bàn 2",
                new DateTime(2026, 11, 24), new DateTime(2026, 11, 26),
                new DateTime(2026, 11, 24), new DateTime(2026, 11, 26));
            Check("Lịch kéo thả: chỉ đổi phòng → 1 dòng", changes.Count == 1 && changes[0] == "Phòng: bàn 1 --> bàn 2",
                string.Join(";", changes.ToArray()));
            changes = BookingCalendarRules.BuildDragChanges("bàn 1", "bàn 1",
                new DateTime(2026, 11, 24), new DateTime(2026, 11, 26),
                new DateTime(2026, 11, 25), new DateTime(2026, 11, 27));
            Check("Lịch kéo thả: chỉ đổi ngày → 2 dòng", changes.Count == 2 &&
                changes[0] == "Từ ngày: 24/11/2026 --> 25/11/2026" &&
                changes[1] == "Đến ngày: 26/11/2026 --> 27/11/2026", string.Join(";", changes.ToArray()));
            Check("Lịch kéo thả: không đổi → 0 dòng", BookingCalendarRules.BuildDragChanges("bàn 1", "bàn 1",
                new DateTime(2026, 11, 24), new DateTime(2026, 11, 26),
                new DateTime(2026, 11, 24), new DateTime(2026, 11, 26)).Count == 0);
            string confirm = BookingCalendarRules.BuildDragConfirmMessage("Nguyễn Văn A",
                new List<string> { "Phòng: bàn 1 --> bàn 2" });
            Check("Lịch kéo thả: câu xác nhận đúng mẫu gốc",
                confirm == "Bạn có muốn cập nhật thay đổi phiếu đặt phòng của 'Nguyễn Văn A'\nPhòng: bàn 1 --> bàn 2", confirm);
            // 4 hàng tổng (thuần): trùng ngày trên cùng phòng chỉ tính 1 phòng.
            var pure = new CalendarData { TotalRooms = 2 };
            pure.Types.Add(new CalendarRoomType { Id = "t1", Name = "VIP" });
            pure.Rooms.Add(new CalendarRoom { Id = "r1", Name = "bàn 1", TypeId = "t1" });
            pure.Rooms.Add(new CalendarRoom { Id = "r2", Name = "bàn 2", TypeId = "t1" });
            pure.Bars.Add(new CalendarBookingBar { Id = "b1", RoomId = "r1", TuNgay = from.AddDays(1), DenNgay = from.AddDays(3) });
            pure.Bars.Add(new CalendarBookingBar { Id = "b2", RoomId = "r1", TuNgay = from.AddDays(2), DenNgay = from.AddDays(2) });
            pure.Bars.Add(new CalendarBookingBar { Id = "b3", RoomId = "r2", TuNgay = from.AddDays(2), DenNgay = from.AddDays(2) });
            BookingCalendarRules.CalendarTotals pt = BookingCalendarRules.BuildTotals(pure, from, days);
            Check("Lịch tổng thuần: Đã bán ngày 1=1, ngày 2=2, ngày 3=1",
                pt.SoldPerDay[1] == 1 && pt.SoldPerDay[2] == 2 && pt.SoldPerDay[3] == 1 && pt.SoldPerDay[0] == 0,
                string.Join(",", Array.ConvertAll(pt.SoldPerDay, i => i.ToString())));
            Check("Lịch tổng thuần: Còn lại = Tổng − Đã bán",
                pt.RemainingPerDay[2] == 0 && pt.RemainingPerDay[0] == 2);
            Check("Lịch tổng thuần: Công suất ngày 2 = 100.0%, ngày 1 = 50.0%",
                pt.CapacityPerDay[2] == 100.0 && pt.CapacityPerDay[1] == 50.0);
            Check("Lịch tổng thuần: hàng nhóm VIP ngày 2 = 2", pt.SoldPerDayByType["t1"][2] == 2);
            Check("Lịch tổng thuần: cột Tổng phòng r1 = 2 (thiếu +1), r2 = 0",
                pt.TotalDaysByRoom["r1"] == 2 && pt.TotalDaysByRoom["r2"] == 0);
            Check("Lịch tổng thuần: tổng Đã bán = 4, Công suất tổng = 28.6%",
                pt.TotalSoldDays == 4 && pt.TotalCapacity == 28.6,
                pt.TotalSoldDays + "/" + pt.TotalCapacity);

            // --- DB: tạo loại/phòng/khách/phiếu → tải lưới, kéo thả, dọn sạch ---
            var calRepo = new BookingCalendarRepository(db);
            var bookingRepo = new BookingRepository(db);
            var formRepo = new FormRepository(db);
            CalendarData before = calRepo.LoadCalendar(from, days, "");
            Check("Lịch DB trống ban đầu: 0 loại, 0 phòng, 0 thanh (DEMO)",
                before.Types.Count == 0 && before.Rooms.Count == 0 && before.Bars.Count == 0,
                before.Types.Count + "/" + before.Rooms.Count + "/" + before.Bars.Count);
            if (before.Types.Count != 0 || before.Rooms.Count != 0 || before.Bars.Count != 0)
                return;
            int existingRooms = CountActiveRoomsSql(db);
            int expectedTotalRooms = existingRooms + 4;
            string typeVip = DbHelper.NewId();
            string typeThuong = DbHelper.NewId();
            string room1Id = DbHelper.NewId();
            string room2Id = DbHelper.NewId();
            string room3Id = DbHelper.NewId();
            string room4Id = DbHelper.NewId();
            string customerId = DbHelper.NewId();
            string b1Id = null, b2Id = null, b3Id = null, b4Id = null;
            try
            {
                BookingExec(db, "INSERT INTO \"DLOAIPHONG\" (\"ID\", \"NAME\", \"STATUS\", \"SORTORDER\", \"MAUCHU\", \"MAUNEN\", \"USERCREATEDID\") VALUES (@id, @n, 30, @s, @chu, @nen, 'selftest')",
                    "@id", typeVip, "@n", "VIP", "@s", "1", "@chu", -16777216, "@nen", -1);
                BookingExec(db, "INSERT INTO \"DLOAIPHONG\" (\"ID\", \"NAME\", \"STATUS\", \"SORTORDER\", \"MAUCHU\", \"MAUNEN\", \"USERCREATEDID\") VALUES (@id, @n, 30, @s, @chu, @nen, 'selftest')",
                    "@id", typeThuong, "@n", "Thường", "@s", "2", "@chu", -1, "@nen", -65536);
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DLOAIPHONGID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @t, 30, 'selftest')",
                    "@id", room1Id, "@n", "bàn 1", "@t", typeVip);
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DLOAIPHONGID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @t, 30, 'selftest')",
                    "@id", room2Id, "@n", "bàn 2", "@t", typeVip);
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DLOAIPHONGID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @t, 30, 'selftest')",
                    "@id", room3Id, "@n", "bàn 3", "@t", typeThuong);
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", room4Id, "@n", "bàn không loại");
                BookingExec(db, "INSERT INTO \"DKHACHHANG\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", customerId, "@n", "KHÁCH THỬ LỊCH");
                List<LookupItem> methods = formRepo.LoadAll("DPHUONGTHUCDAT");
                List<LookupItem> purposes = formRepo.LoadAll("DMUCDICHDAT");
                string methodId = methods.Count > 0 ? methods[0].Id : null;
                string purposeId = purposes.Count > 0 ? purposes[0].Id : null;
                var emptyLines = new List<BookingLine>();
                // B1: bàn 1, [from+1, from+3]. B2: đoàn bàn 2 + bàn 3, 1 ngày [from+2].
                // B3: bao trùm cửa sổ [from-2, from+10] (truy vấn BETWEEN loại — giữ bug app gốc).
                // B4: bắt đầu trước cửa sổ, kết thúc trong [from-1, from+1] (DENNGAY BETWEEN nên hiện).
                BookingRow row1 = NewCalendarBooking(bookingRepo.GenerateNumber(), customerId, methodId, purposeId,
                    from.AddDays(1).AddHours(18), from.AddDays(3).AddHours(20));
                BookingSaveResult s1 = bookingRepo.SaveBooking(row1, emptyLines, new List<string> { room1Id }, "selftest");
                Check("Lịch lưu B1 (bàn 1, 3 ngày)", s1.Ok && s1.SavedId != null, s1.ErrorMessage);
                b1Id = s1.SavedId;
                BookingRow row2 = NewCalendarBooking(bookingRepo.GenerateNumber(), customerId, methodId, purposeId,
                    from.AddDays(2).AddHours(18), from.AddDays(2).AddHours(20));
                BookingSaveResult s2 = bookingRepo.SaveBooking(row2, emptyLines, new List<string> { room2Id, room3Id }, "selftest");
                Check("Lịch lưu B2 (đoàn 2 bàn, 1 ngày)", s2.Ok && s2.SavedId != null, s2.ErrorMessage);
                b2Id = s2.SavedId;
                BookingRow row3 = NewCalendarBooking(bookingRepo.GenerateNumber(), customerId, methodId, purposeId,
                    from.AddDays(-2).AddHours(18), from.AddDays(10).AddHours(20));
                BookingSaveResult s3 = bookingRepo.SaveBooking(row3, emptyLines, new List<string> { room1Id }, "selftest");
                Check("Lịch lưu B3 (bao trùm cửa sổ)", s3.Ok && s3.SavedId != null, s3.ErrorMessage);
                b3Id = s3.SavedId;
                BookingRow row4 = NewCalendarBooking(bookingRepo.GenerateNumber(), customerId, methodId, purposeId,
                    from.AddDays(-1).AddHours(18), from.AddDays(1).AddHours(20));
                BookingSaveResult s4 = bookingRepo.SaveBooking(row4, emptyLines, new List<string> { room2Id }, "selftest");
                Check("Lịch lưu B4 (bắt đầu trước cửa sổ)", s4.Ok && s4.SavedId != null, s4.ErrorMessage);
                b4Id = s4.SavedId;
                if (b1Id == null || b2Id == null || b3Id == null || b4Id == null)
                    return;

                CalendarData loaded = calRepo.LoadCalendar(from, days, "");
                Check("Lịch tải lưới: 2 loại theo SORTORDER",
                    loaded.Types.Count == 2 && loaded.Types[0].Id == typeVip && loaded.Types[1].Id == typeThuong);
                Check("Lịch tải lưới: màu loại VIP/Thường đúng ARGB",
                    loaded.Types[0].ForeColorArgb == -16777216 && loaded.Types[0].BackColorArgb == -1 &&
                    loaded.Types[1].ForeColorArgb == -1 && loaded.Types[1].BackColorArgb == -65536);
                Check("Lịch tải lưới: 3 phòng có loại (phòng không loại không hiện)",
                    loaded.Rooms.Count == 3 && loaded.Rooms.Exists(r => r.Id == room1Id) &&
                    loaded.Rooms.Exists(r => r.Id == room2Id) && loaded.Rooms.Exists(r => r.Id == room3Id));
                Check("Lịch tải lưới: tổng số phòng = P + 4 (kể cả phòng không loại)",
                    loaded.TotalRooms == expectedTotalRooms,
                    "actual=" + loaded.TotalRooms + ", expected=" + expectedTotalRooms + " (P=" + existingRooms + ")");
                Check("Lịch tải lưới: 4 thanh (B3 bao trùm không hiện)", loaded.Bars.Count == 4,
                    loaded.Bars.Count.ToString());
                Check("Lịch tải lưới: B3 không có trong danh sách", !loaded.Bars.Exists(b => b.Id == b3Id));
                Check("Lịch tải lưới: B4 có (DENNGAY trong cửa sổ)", loaded.Bars.Exists(b => b.Id == b4Id));
                CalendarData vipOnly = calRepo.LoadCalendar(from, days, typeVip);
                Check("Lịch lọc loại VIP: 1 loại, 2 phòng, 3 thanh (B1+B2 bàn 2+B4)",
                    vipOnly.Types.Count == 1 && vipOnly.Rooms.Count == 2 && vipOnly.Bars.Count == 3,
                    vipOnly.Types.Count + "/" + vipOnly.Rooms.Count + "/" + vipOnly.Bars.Count);
                Check("Lịch lọc loại VIP: tổng số phòng vẫn là P + 4",
                    vipOnly.TotalRooms == expectedTotalRooms,
                    "actual=" + vipOnly.TotalRooms + ", expected=" + expectedTotalRooms + " (P=" + existingRooms + ")");

                BookingCalendarRules.CalendarTotals lt = BookingCalendarRules.BuildTotals(loaded, from, days);
                Check("Lịch tổng trên DB: Đã bán [1,2,3,1,0,0,0]",
                    lt.SoldPerDay[0] == 1 && lt.SoldPerDay[1] == 2 && lt.SoldPerDay[2] == 3 &&
                    lt.SoldPerDay[3] == 1 && lt.SoldPerDay[4] == 0,
                    string.Join(",", Array.ConvertAll(lt.SoldPerDay, i => i.ToString())));
                CheckCalendarTotalsOnDb(lt, existingRooms);
                Check("Lịch tổng trên DB: hàng nhóm VIP ngày 2 = 2, Thường ngày 2 = 1",
                    lt.SoldPerDayByType[typeVip][2] == 2 && lt.SoldPerDayByType[typeThuong][2] == 1);
                Check("Lịch tổng trên DB: cột Tổng bàn 1 = 2, bàn 2 = 1, bàn 3 = 0",
                    lt.TotalDaysByRoom[room1Id] == 2 && lt.TotalDaysByRoom[room2Id] == 1 &&
                    lt.TotalDaysByRoom[room3Id] == 0,
                    lt.TotalDaysByRoom[room1Id] + "/" + lt.TotalDaysByRoom[room2Id] + "/" + lt.TotalDaysByRoom[room3Id]);

                // Kéo thả: B2 (bàn 2) → bàn 1, ngày [from+3, from+4].
                string b2Guid = CalendarRowValues(db, b2Id).Split('|')[5];
                string b2Room3Id = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                    "SELECT \"ID\" FROM \"TDATHANG\" WHERE \"GUID\" = @g AND \"DBANID\" = @r",
                    "@g", b2Guid, "@r", room3Id)));
                BookingCalendarMoveResult drag = calRepo.ApplyDragMove(b2Id, room1Id, from.AddDays(3), from.AddDays(4), "selftest");
                Check("Lịch kéo thả: ghi thành công", drag.Ok, drag.ErrorMessage);
                string moved = CalendarRowValues(db, b2Id);
                string movedRoom3 = CalendarRowValues(db, b2Room3Id);
                Check("Lịch kéo thả: phiếu kéo đổi bàn + ngày, giữ giờ vào/ra",
                    moved.StartsWith(room1Id + "|" + from.AddDays(3).ToString("yyyy-MM-dd") + "|" +
                        from.AddDays(4).ToString("yyyy-MM-dd") + "|" +
                        from.AddDays(2).ToString("yyyy-MM-dd") + " 18:00|" +
                        from.AddDays(2).ToString("yyyy-MM-dd") + " 20:00|"), moved);
                Check("Lịch kéo thả: dòng cùng đoàn chỉ đổi ngày, không đổi bàn",
                    movedRoom3.StartsWith(room3Id + "|" + from.AddDays(3).ToString("yyyy-MM-dd") + "|" +
                        from.AddDays(4).ToString("yyyy-MM-dd") + "|"), movedRoom3);
                Check("Lịch kéo thả: phiếu không tồn tại → lỗi",
                    !calRepo.ApplyDragMove(DbHelper.NewId(), room1Id, from, from, "selftest").Ok);
                CalendarData after = calRepo.LoadCalendar(from, days, "");
                Check("Lịch kéo thả: tải lại thấy B2 trên bàn 1 [3,4]",
                    after.Bars.Exists(b => b.Id == b2Id && b.RoomId == room1Id));
            }
            finally
            {
                // Dọn sạch: xóa phiếu (cả đoàn) rồi xóa phòng/loại/khách.
                foreach (string id in new[] { b1Id, b2Id, b3Id, b4Id })
                {
                    if (id != null)
                    {
                        try { bookingRepo.DeleteBooking(id); } catch { }
                    }
                }
                try { BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" IN (@a, @b, @c, @d)", "@a", room1Id, "@b", room2Id, "@c", room3Id, "@d", room4Id); } catch { }
                try { BookingExec(db, "DELETE FROM \"DLOAIPHONG\" WHERE \"ID\" IN (@a, @b)", "@a", typeVip, "@b", typeThuong); } catch { }
                try { BookingExec(db, "DELETE FROM \"DKHACHHANG\" WHERE \"ID\" = @id", "@id", customerId); } catch { }
            }
        }

        private static void ProductTests(string db)
        {
            // Luật thuần 2.1 (dừng ở lỗi đầu, quyết định 5.6 gộp #1/#2).
            ProductValidationInput input = ValidBrewInput();
            input.UseTwoUnits = true;
            input.BigUnitId = "u2";
            input.QuyDoi = 0m;
            ProductValidationResult vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #1 thiếu quy đổi → chặn + focus ô Quy đổi", vr.Error == "Mời bạn nhập quy đổi" && vr.FocusQuyDoi, vr.Error);
            input.QuyDoi = 5m;
            input.BigUnitId = "u1";
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #2 ĐVT lẻ = ĐVT chẵn → chặn", vr.Error == "Đơn vị lẻ và đơn vị chẵn không được trùng nhau", vr.Error);
            input.QuyDoi = 0m;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 gộp #1/#2: dừng ở lỗi đầu (#1)", vr.Error == "Mời bạn nhập quy đổi", vr.Error);
            input = ValidBrewInput();
            input.TypeId = "5";
            input.Lines[0].MaterialTypeId = "1"; // tránh #8: Combo mở chứa nguyên liệu loại 2 sẽ bị chặn
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #3 Combo mở chỉ cảnh báo, vẫn lưu", vr.IsValid && vr.Warning == "Phiên bản hiện tại mới hỗ trợ combo mở ở giao diện quản trị", vr.Warning);
            input = ValidBrewInput();
            input.TypeId = "0";
            input.TypeHasRecipe = false;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #4 loại 0/2/6 có định lượng → chặn", vr.Error == "Mặt hàng này không có vật tư / định lượng đi kèm", vr.Error);
            input = ValidBrewInput();
            input.Lines = new List<RecipeValidationLine>();
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #5 pha chế không có định lượng → chặn", vr.Error == "Vui lòng nhập vật tư/định lượng cho mặt hàng", vr.Error);
            input = ValidBrewInput();
            input.Lines[0].Quantity = 0m;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #6 số lượng 0 → chặn đúng dòng", vr.Error == "Mời bạn nhập số lượng cho định lượng" && vr.ErrorLineIndex == 0, vr.Error);
            input = ValidBrewInput();
            input.Lines[0].MaterialId = null;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #7 chưa chọn vật tư → chặn", vr.Error == "Mời bạn nhập vật tư cho định lượng", vr.Error);
            input = ValidBrewInput();
            input.TypeId = "4";
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #8 Combo chứa nguyên liệu → chặn", vr.Error == "Combo không được phép chứa nguyên vật liệu", vr.Error);
            Check("2.1 pha chế hợp lệ → lưu được", ProductRules.ValidateBeforeSave(ValidBrewInput()).IsValid);

            // Sinh mã 3.5 + công thức giá 3.4 + chuyển loại 3.2.
            string code = ProductRules.GenerateCode("BIA", 7, new Random(1));
            bool codeOk = code != null && code.Length == 7 && code.StartsWith("BIA", StringComparison.Ordinal);
            if (codeOk)
            {
                for (int i = 3; i < code.Length; i++)
                    codeOk = codeOk && char.IsDigit(code[i]);
            }
            Check("3.5 sinh mã = tiền tố + số ngẫu nhiên, đủ 7 ký tự", codeOk, code);
            Check("3.5 nhóm không có tiền tố → không sinh mã", ProductRules.GenerateCode("", 7, new Random(1)) == null);
            Check("3.5 sinh mã trùng hết → null sau 10 lần thử",
                ProductRules.GenerateUniqueCode("BIA", 7, delegate { return true; }, new Random(1)) == null);
            string unique = ProductRules.GenerateUniqueCode("BIA", 7, delegate { return false; }, new Random(1));
            Check("3.5 sinh mã không trùng → trả mã hợp lệ", unique != null && unique.Length == 7 && unique.StartsWith("BIA", StringComparison.Ordinal), unique);
            Check("3.4 giá nhập lẻ = GIANHAP / QUYDOI", ProductMath.UnitBuyPrice(12000m, 12m, true) == 1000m);
            Check("3.4 không có ĐVT chẵn → giá nhập lẻ = GIANHAP", ProductMath.UnitBuyPrice(12000m, null, false) == 12000m);
            Check("3.4 thành tiền nhập = SL × giá lẻ", ProductMath.AmountBuy(2m, 1000m) == 2000m);
            Check("3.4 thành tiền vốn = SL × giá vốn", ProductMath.AmountCost(2m, 8000m) == 16000m);
            decimal totalBuy, totalCost;
            ProductRules.Totals(new[]
            {
                new RecipeLine { AmountBuy = 2000m, AmountCost = 16000m },
                new RecipeLine { AmountBuy = 500m, AmountCost = 4000m }
            }, out totalBuy, out totalCost);
            Check("3.4 tổng định lượng", totalBuy == 2500m && totalCost == 20000m, totalBuy + "/" + totalCost);
            Check("3.2 thêm dòng cho loại không định lượng → chuyển pha chế", ProductRules.SwitchTypeForRecipe("0", false) == "1");
            Check("3.2 loại đã có định lượng giữ nguyên", ProductRules.SwitchTypeForRecipe("4", true) == "4");
            Check("5.7 chỉ tạo mới được loại 0–3 qua dialog", ProductRules.CanCreateType("0") && ProductRules.CanCreateType("3") && !ProductRules.CanCreateType("4") && !ProductRules.CanCreateType("7"));

            // Quyền 4: Thu ngân Xem + Thêm (0x50), không Sửa/Xóa, không Xem giá nhập.
            var perms = new FunctionPermissions(false);
            perms.Add("9bcaf32e-e0c6-4b62-8ab4-65afb965bd2e", "Danh mục mặt hàng", 0x50);
            Check("4 Thu ngân: Xem + Thêm", perms.Can("Danh mục mặt hàng", RoleModes.View) && perms.Can("Danh mục mặt hàng", RoleModes.Add));
            Check("4 Thu ngân: không Sửa/Xóa", !perms.Can("Danh mục mặt hàng", RoleModes.Edit) && !perms.Can("Danh mục mặt hàng", RoleModes.Remove));
            Check("4 Thu ngân: không có Xem giá nhập", !perms.Can("Xem giá nhập", RoleModes.View));
            Check("4 không có dòng quyền = khóa", !new FunctionPermissions(false).Can("Danh mục mặt hàng", RoleModes.View));

            // DB: tạo nhóm/vật tư/mặt hàng pha chế → kiểm lưu, trùng, cascade giá, chặn xóa, dọn sạch.
            var productRepo = new ProductRepository(db);
            var groupRepo = new ProductGroupRepository(db);
            List<LookupItem> units = new FormRepository(db).LoadAll("DDONVITINH");
            Check("Có đơn vị tính để thử", units.Count > 0, "n=" + units.Count);
            if (units.Count == 0)
                return;
            string unitId = units[0].Id;
            string groupId = null;
            string materialId = null;
            string brewId = null;
            try
            {
                string gerr;
                groupId = groupRepo.Save(new ProductGroupRow { Name = "NHÓM THỬ DMH", Code = "THU" }, "selftest", out gerr);
                Check("Tạo nhóm thử", gerr == null && groupId != null, gerr);
                if (groupId == null)
                    return;
                var options = new ProductSaveOptions { AllowDuplicateName = false, CodeRequired = true, AutoUpdateBuyPrice = true, AutoUpdateCostPrice = true };
                var material = ProductRow.NewDefault();
                material.Name = "VẬT TƯ THỬ DMH";
                material.TypeId = "2";
                material.UnitId = unitId;
                material.GroupId = groupId;
                material.Code = "VTDMH001";
                material.BuyPrice = 12000m;
                material.CostPrice = 8000m;
                ProductSaveResult saved = productRepo.SaveProduct(material, new List<RecipeLine>(), "selftest", options);
                Check("Lưu vật tư", saved.Ok && saved.SavedId != null, saved.ErrorMessage);
                materialId = saved.SavedId;
                if (materialId == null)
                    return;
                ProductRow back = productRepo.LoadProduct(materialId);
                Check("3.1b thêm mới ghi đúng STATUS/ID/người tạo", back != null && back.Status == 30 && back.CreatedById == "selftest" && back.CreatedAt.HasValue);
                var dupName = ProductRow.NewDefault();
                dupName.Name = "VẬT TƯ THỬ DMH";
                dupName.TypeId = "2";
                dupName.UnitId = unitId;
                dupName.GroupId = groupId;
                dupName.Code = "VTDMH002";
                ProductSaveResult dup = productRepo.SaveProduct(dupName, new List<RecipeLine>(), "selftest", options);
                Check("Trùng tên bị chặn (nguyên văn)", !dup.Ok && dup.ErrorMessage == ProductText.NameDuplicate("VẬT TƯ THỬ DMH"), dup.ErrorMessage);
                var dupCode = ProductRow.NewDefault();
                dupCode.Name = "VẬT TƯ KHÁC DMH";
                dupCode.TypeId = "2";
                dupCode.UnitId = unitId;
                dupCode.GroupId = groupId;
                dupCode.Code = "VTDMH001";
                dup = productRepo.SaveProduct(dupCode, new List<RecipeLine>(), "selftest", options);
                Check("Trùng mã bị chặn (nguyên văn)", !dup.Ok && dup.ErrorMessage == ProductText.CodeDuplicate("VTDMH001"), dup.ErrorMessage);

                // Hồi quy: thêm mới → trùng tên (lỗi) → sửa tên → lưu lại phải ghi thật, không để định lượng mồ côi.
                var retry = ProductRow.NewDefault();
                retry.Name = "VẬT TƯ THỬ DMH";
                retry.TypeId = "1";
                retry.UnitId = unitId;
                retry.GroupId = groupId;
                retry.Code = "MHDMH0RT";
                var retryLines = new List<RecipeLine> { new RecipeLine { MaterialId = materialId, Quantity = 1m } };
                ProductSaveResult r1 = productRepo.SaveProduct(retry, retryLines, "selftest", options);
                Check("Hồi quy: lưu mới trùng tên → lỗi và row.Id vẫn rỗng", !r1.Ok && string.IsNullOrEmpty(retry.Id), "Id=" + retry.Id);
                retry.Name = "MẶT HÀNG THỬ LẠI DMH";
                ProductSaveResult r2 = productRepo.SaveProduct(retry, retryLines, "selftest", options);
                Check("Hồi quy: sửa tên rồi lưu lại → Ok", r2.Ok && !string.IsNullOrEmpty(r2.SavedId), r2.ErrorMessage);
                if (r2.SavedId != null)
                {
                    ProductRow retryBack = productRepo.LoadProduct(r2.SavedId);
                    Check("Hồi quy: ghi thật vào DMATHANG (STATUS 30, đúng tên)", retryBack != null && retryBack.Status == 30 && retryBack.Name == "MẶT HÀNG THỬ LẠI DMH");
                    int retryLinesN = productRepo.LoadRecipeLines(r2.SavedId).Count;
                    Check("Hồi quy: định lượng đúng 1 dòng", retryLinesN == 1, "n=" + retryLinesN);
                    productRepo.DeleteProduct(r2.SavedId);
                }
                int retryOrphans = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DDINHLUONG D WHERE D.DVATTUID = @M AND NOT EXISTS (SELECT 1 FROM DMATHANG P WHERE P.ID = D.DMATHANGID)", "@M", materialId)));
                Check("Hồi quy: không có dòng DDINHLUONG mồ côi", retryOrphans == 0, "n=" + retryOrphans);
                var ghost = ProductRow.NewDefault();
                ghost.Id = "KHONG-TON-TAI-DMH";
                ghost.Name = "MA DMH";
                ghost.Code = "MADMH001";
                ghost.TypeId = "2";
                ghost.UnitId = unitId;
                ghost.GroupId = groupId;
                ProductSaveResult rg = productRepo.SaveProduct(ghost, new List<RecipeLine>(), "selftest", options);
                Check("Sửa dòng không tồn tại → lỗi, không báo Ok", !rg.Ok && rg.ErrorMessage == "Mặt hàng không còn tồn tại.", rg.ErrorMessage);

                var brew = ProductRow.NewDefault();
                brew.Name = "MẶT HÀNG THỬ DMH";
                brew.TypeId = "1";
                brew.UnitId = unitId;
                brew.GroupId = groupId;
                brew.Code = "MHDMH001";
                brew.SellPrice = 30000m;
                ProductSaveResult savedBrew = productRepo.SaveProduct(brew,
                    new List<RecipeLine> { new RecipeLine { MaterialId = materialId, Quantity = 2m } }, "selftest", options);
                Check("Lưu mặt hàng pha chế + định lượng", savedBrew.Ok && savedBrew.SavedId != null, savedBrew.ErrorMessage);
                brewId = savedBrew.SavedId;
                if (brewId == null)
                    return;
                ProductRow brewBack = productRepo.LoadProduct(brewId);
                Check("3.4 GIANHAP = tổng thành tiền nhập (2 × 12000)", brewBack != null && brewBack.BuyPrice == 24000m, brewBack == null ? "null" : brewBack.BuyPrice.ToString());
                Check("3.4 GIAVON = tổng thành tiền vốn (2 × 8000)", brewBack != null && brewBack.CostPrice == 16000m, brewBack == null ? "null" : brewBack.CostPrice.ToString());
                List<RecipeLine> lines = productRepo.LoadRecipeLines(brewId);
                Check("Định lượng đọc lại đủ dòng", lines.Count == 1 && lines[0].MaterialId == materialId && lines[0].Quantity == 2m, "n=" + lines.Count);

                ProductDeleteResult blocked = productRepo.DeleteProduct(materialId);
                Check("3.6 xóa vật tư còn tham chiếu → chặn", !blocked.Ok, blocked.ErrorMessage);
                ProductDeleteResult deletedBrew = productRepo.DeleteProduct(brewId);
                Check("Xóa hẳn mặt hàng pha chế", deletedBrew.Ok, deletedBrew.ErrorMessage);
                brewId = null;
                int orphans = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DDINHLUONG WHERE DMATHANGID = @P", "@P", savedBrew.SavedId)));
                Check("Xóa không để lại dòng định lượng mồ côi", orphans == 0, "n=" + orphans);
                ProductDeleteResult deletedMat = productRepo.DeleteProduct(materialId);
                Check("Xóa vật tư sau khi hết tham chiếu", deletedMat.Ok, deletedMat.ErrorMessage);
                materialId = null;
                ProductDeleteResult km = productRepo.DeleteProduct(ProductRepository.SystemProductId);
                Check("Không xóa được mặt hàng hệ thống 'Khuyến mại tự động'", !km.Ok, km.ErrorMessage);
                string delGroup = groupRepo.Delete(groupId);
                Check("Xóa nhóm thử", delGroup == null, delGroup);
                groupId = null;
            }
            finally
            {
                try { if (brewId != null) productRepo.DeleteProduct(brewId); } catch { }
                try { if (materialId != null) productRepo.DeleteProduct(materialId); } catch { }
                try { if (groupId != null) groupRepo.Delete(groupId); } catch { }
            }
        }

        // ------------------------------------------------------------------ H6b Danh mục bàn khu vực

        private static void RoomTests(string db)
        {
            // Luật thuần (spec 3.11 + Q6): sinh tên, khoảng số, trùng trong lần sinh, kiểm tra, mức giá.
            Check("3.11 TUSO=DENSO sinh đúng 1 bàn", RoomRules.GenerateNames("C", 5, 5, 2).Count == 1
                && RoomRules.GenerateNames("C", 5, 5, 2)[0] == "C05");
            Check("3.11 sinh tên đệm 0 theo chiều dài số",
                string.Join(",", RoomRules.GenerateNames("B", 1, 3, 2).ToArray()) == "B01,B02,B03");
            Check("3.11 xem trước DULIEU dạng 'đầu ... cuối'",
                RoomRules.PreviewNames(RoomRules.GenerateNames("B", 1, 12, 2)) == "B01 ... B12");
            Check("3.11 một tên → xem trước chính tên đó", RoomRules.PreviewNames(new List<string> { "B01" }) == "B01");
            Check("Q6 TUSO > DENSO → lỗi 'Khoảng số không hợp lệ'", RoomRules.ValidateRange(5, 3) == "Khoảng số không hợp lệ");
            Check("Q6 TUSO = DENSO hợp lệ", RoomRules.ValidateRange(3, 3) == null);
            Check("Q6 TUSO < DENSO hợp lệ", RoomRules.ValidateRange(1, 3) == null);
            Check("3.11 trùng trong lần sinh → phát hiện",
                RoomRules.FindBatchDuplicate(new List<string> { "D01", "D02", "D01" }) == "D01");
            Check("3.11 không trùng trong lần sinh",
                RoomRules.FindBatchDuplicate(new List<string> { "D01", "D02" }) == null);
            Check("3.11 DigitsOf", RoomRules.DigitsOf(0) == 1 && RoomRules.DigitsOf(9) == 1
                && RoomRules.DigitsOf(10) == 2 && RoomRules.DigitsOf(100) == 3);
            Check("R8 TUSO âm → lỗi; quá 500 bàn → lỗi; đúng 500 → hợp lệ",
                RoomRules.ValidateRange(-1, 3) == RoomText.InvalidRange
                && RoomRules.ValidateRange(1, 501) == RoomText.QuickAddTooMany && RoomRules.ValidateRange(1, 500) == null);
            Check("R3/R4 theo bảng giá mà thiếu bảng giá → chặn; tiền âm → chặn; không CoTienGio → bỏ qua",
                RoomRules.ValidateRoom("B01", "a", true, RoomCalc.ByPriceList, null, 0m, 0m) == RoomText.PriceListRequired
                && RoomRules.ValidateRoom("B01", "a", true, RoomCalc.ByArea, null, -1m, 0m) == RoomText.MoneyNegative
                && RoomRules.ValidateArea("Lầu 1", true, RoomCalc.ByPriceList, "p", 0m, 0m) == null
                && RoomRules.ValidateRoom("B01", "a", false, RoomCalc.ByPriceList, null, -5m, -5m) == null);
            Check("ánh xạ tạm Q8 (CHƯA XÁC MINH số 2): 0 theo khu vực, 1 theo giờ, 2 theo bảng giá",
                RoomRules.CalcModeText(RoomCalc.ByArea) == "Theo khu vực" && RoomRules.CalcModeText(RoomCalc.ByHour) == "Theo giờ"
                && RoomRules.CalcModeText(RoomCalc.ByPriceList) == "Theo bảng giá");
            Check("Q8 số lạ → nhãn chỉ-xem, không đoán", RoomRules.CalcModeText(99) == "Không rõ (99)");
            Check("3.1 bàn thiếu tên → chặn", RoomRules.ValidateRoom("", "a") == RoomText.RoomNameEmpty);
            Check("3.1 bàn thiếu khu vực → chặn", RoomRules.ValidateRoom("B01", "") == RoomText.RoomAreaEmpty);
            Check("3.1 bàn hợp lệ", RoomRules.ValidateRoom("B01", "a") == null);
            Check("3.2 khu vực thiếu tên → chặn", RoomRules.ValidateArea(" ") == RoomText.AreaNameEmpty);
            Check("3.2 khu vực hợp lệ", RoomRules.ValidateArea("Lầu 1") == null);
            var levels = new List<LookupItem>
            {
                new LookupItem { Id = "0", Name = "Hiển thị để lựa chọn" },
                new LookupItem { Id = "1", Name = "Giá chung" },
                new LookupItem { Id = "2", Name = "Giá bán t2" },
                new LookupItem { Id = "3", Name = "Giá bán t3" },
                new LookupItem { Id = "4", Name = "Giá cafe" }
            };
            List<LookupItem> filtered = RoomRules.FilterPriceLevels(levels, false, false, false, "2");
            Check("3.10 mức giá 2/3/4 ẩn khi cấu hình tắt, giữ lại mức đang dùng",
                filtered.Count == 3 && filtered[2].Id == "2", "n=" + filtered.Count);
            Check("3.10 mức giá đủ khi cấu hình bật",
                RoomRules.FilterPriceLevels(levels, true, true, true, "0").Count == 5);

            // Quyền 4: Quản lý/Kế toán MODE 240 đủ 4 quyền; MODE 0 không quyền nào; không có dòng = khóa.
            var perms = new FunctionPermissions(false);
            perms.Add("194c969a-962f-47c8-b808-0330e2a60350", RoomRules.FunctionName, 240);
            Check("4 Quản lý MODE 240: đủ Xem/Thêm/Sửa/Xóa",
                perms.Can(RoomRules.FunctionName, RoleModes.View) && perms.Can(RoomRules.FunctionName, RoleModes.Add)
                && perms.Can(RoomRules.FunctionName, RoleModes.Edit) && perms.Can(RoomRules.FunctionName, RoleModes.Remove));
            perms = new FunctionPermissions(false);
            perms.Add("194c969a-962f-47c8-b808-0330e2a60350", RoomRules.FunctionName, 0);
            Check("4 Thu ngân MODE 0: không quyền nào",
                !perms.Can(RoomRules.FunctionName, RoleModes.View) && !perms.Can(RoomRules.FunctionName, RoleModes.Add)
                && !perms.Can(RoomRules.FunctionName, RoleModes.Edit) && !perms.Can(RoomRules.FunctionName, RoleModes.Remove));
            Check("4 Thủ kho không có dòng quyền = khóa",
                !new FunctionPermissions(false).Can(RoomRules.FunctionName, RoleModes.View));

            // DB: tạo khu vực/bàn → trùng tên, Thêm nhanh, chặn xóa, xóa cứng, giữ cột thiết kế, dọn sạch.
            var areaRepo = new AreaRepository(db);
            var roomRepo = new RoomRepository(db);
            string areaId = null, area2Id = null, roomId = null, orderId = null;
            try
            {
                var area = AreaRow.NewDefault();
                area.Name = "KHU VỰC THỬ BKV";
                AreaSaveResult areaSaved = areaRepo.Save(area,
                    new List<PriceAssignment>(), new List<string>(), new List<AreaDefaultItem>(), "selftest");
                areaId = areaSaved.SavedId;
                Check("Tạo khu vực thử", areaSaved.Ok && areaId != null, areaSaved.ErrorMessage);
                if (areaId == null)
                    return;
                AreaRow areaBack = areaRepo.Load(areaId);
                Check("3.2 thêm khu vực ghi đúng STATUS/người tạo",
                    areaBack != null && areaBack.Status == 30 && areaBack.CreatedById == "selftest" && areaBack.CreatedAt.HasValue);
                var dupArea = AreaRow.NewDefault();
                dupArea.Name = "khu vực thử bkv"; // khác hoa thường → vẫn trùng (Q5)
                AreaSaveResult dupA = areaRepo.Save(dupArea,
                    new List<PriceAssignment>(), new List<string>(), new List<AreaDefaultItem>(), "selftest");
                Check("Trùng tên khu vực không phân biệt hoa thường → chặn",
                    !dupA.Ok && dupA.ErrorMessage == RoomText.AreaNameDuplicate("khu vực thử bkv"), dupA.ErrorMessage);

                var room = RoomRow.NewDefault();
                room.Name = "B01";
                room.AreaId = areaId;
                RoomSaveResult saved = roomRepo.SaveRoom(room, new List<PriceAssignment>(), "selftest");
                Check("Lưu bàn", saved.Ok && saved.SavedId != null, saved.ErrorMessage);
                roomId = saved.SavedId;
                if (roomId == null)
                    return;
                RoomRow roomBack = roomRepo.LoadRoom(roomId);
                Check("3.1 bàn mới: theo khu vực, giá 0",
                    roomBack != null && roomBack.CalcHour == RoomCalc.ByArea && roomBack.UnitPrice == 0m && roomBack.OpenAmount == 0m);
                var dupRoom = RoomRow.NewDefault();
                dupRoom.Name = "b01";
                dupRoom.AreaId = areaId;
                RoomSaveResult dup = roomRepo.SaveRoom(dupRoom, new List<PriceAssignment>(), "selftest");
                Check("Trùng tên bàn không phân biệt hoa thường → chặn",
                    !dup.Ok && dup.ErrorMessage == RoomText.RoomNameDuplicate("b01"), dup.ErrorMessage);

                // Thêm nhanh: B02, B03 vào cùng khu vực; trùng trong DB → không ghi dòng nào.
                QuickAddResult qa = roomRepo.QuickAddRooms(new List<QuickAddRequest>
                {
                    new QuickAddRequest { AreaId = areaId, Names = new List<string> { "B02", "B03" } }
                }, "selftest");
                Check("3.11 Thêm nhanh B02, B03", qa.Ok && qa.CreatedCount == 2, qa.ErrorMessage);
                int countBefore = roomRepo.LoadRooms().Count;
                QuickAddResult qaDup = roomRepo.QuickAddRooms(new List<QuickAddRequest>
                {
                    new QuickAddRequest { AreaId = areaId, Names = new List<string> { "B04", "B01" } }
                }, "selftest");
                Check("3.11 Thêm nhanh trùng tên trong DB → lỗi, không ghi dòng nào",
                    !qaDup.Ok && qaDup.ErrorMessage == RoomText.QuickAddExists("B01")
                    && roomRepo.LoadRooms().Count == countBefore, qaDup.ErrorMessage);
                QuickAddResult qaBatch = roomRepo.QuickAddRooms(new List<QuickAddRequest>
                {
                    new QuickAddRequest { AreaId = areaId, Names = new List<string> { "D01", "D01" } }
                }, "selftest");
                Check("3.11 Thêm nhanh trùng trong lần sinh → lỗi, không ghi dòng nào",
                    !qaBatch.Ok && qaBatch.ErrorMessage == RoomText.QuickAddDuplicate("D01")
                    && roomRepo.LoadRooms().Count == countBefore, qaBatch.ErrorMessage);

                // Giữ cột thiết kế/runtime khi sửa (3.1): set trước bằng SQL rồi SaveRoom đổi tên + ghi chú.
                DbHelper.Read(db, conn =>
                {
                    DbHelper.Exec(conn, null, "UPDATE DBAN SET POSLEFT = 10, WIDTH = 100, TRANGTHAI = 1 WHERE ID = @I", "@I", roomId);
                    return 0;
                });
                RoomRow edit = roomRepo.LoadRoom(roomId);
                edit.Name = "B01-ĐỔI";
                edit.Note = "ghi chú thử";
                RoomSaveResult edited = roomRepo.SaveRoom(edit, new List<PriceAssignment>(), "selftest");
                Check("Sửa bàn", edited.Ok, edited.ErrorMessage);
                int posLeft = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT POSLEFT FROM DBAN WHERE ID = @I", "@I", roomId)));
                int width = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT WIDTH FROM DBAN WHERE ID = @I", "@I", roomId)));
                int trangThai = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT TRANGTHAI FROM DBAN WHERE ID = @I", "@I", roomId)));
                Check("3.1 sửa bàn giữ nguyên cột thiết kế/runtime",
                    posLeft == 10 && width == 100 && trangThai == 1, posLeft + "/" + width + "/" + trangThai);

                // Chặn xóa bàn có đơn chưa thanh toán (Q4): trigger tự gán DBAN.TDONHANGID khi INSERT đơn.
                orderId = DbHelper.NewId();
                DbHelper.Read(db, conn =>
                {
                    DbHelper.Exec(conn, null,
                        "INSERT INTO TDONHANG (ID, NAME, STATUS, USERCREATEDID, LOAI, DATHANHTOAN, DBANID) VALUES (@I, @N, 30, 'selftest', 0, 0, @B)",
                        "@I", orderId, "@N", "HĐ THỬ BKV", "@B", roomId);
                    return 0;
                });
                string tdId = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", roomId)));
                Check("Trigger gán TDONHANGID khi có đơn chưa thanh toán", tdId == orderId, tdId ?? "null");
                RoomDeleteResult blocked = roomRepo.DeleteRoom(roomId);
                Check("Q4 xóa bàn có đơn chưa thanh toán → chặn", !blocked.Ok, blocked.ErrorMessage);
                DbHelper.Read(db, conn =>
                {
                    DbHelper.Exec(conn, null, "DELETE FROM TDONHANG WHERE ID = @I", "@I", orderId);
                    return 0;
                });
                orderId = null;
                string tdIdAfter = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", roomId)));
                Check("Xóa đơn → trigger dọn TDONHANGID về NULL", tdIdAfter == null, tdIdAfter ?? "null");

                // B3: đơn đặt (TDATHANG) chưa có hóa đơn trỏ tới bàn → chặn xóa; đã chuyển hóa đơn → không chặn.
                string bookId = DbHelper.NewId();
                string bookOrderId = DbHelper.NewId();
                try
                {
                    DbHelper.Read(db, conn =>
                    {
                        DbHelper.Exec(conn, null,
                            "INSERT INTO TDATHANG (ID, NAME, STATUS, USERCREATEDID, DBANID) VALUES (@I, @N, 30, 'selftest', @B)",
                            "@I", bookId, "@N", "ĐH THỬ BKV", "@B", roomId);
                        return 0;
                    });
                    Check("B3 đơn đặt chưa có hóa đơn → xóa bàn bị chặn",
                        HasBlock(roomRepo.CheckRoomReferences(roomId), "TDATHANG"));
                    RoomDeleteResult blockedBook = roomRepo.DeleteRoom(roomId);
                    Check("B3 DeleteRoom thật sự từ chối khi còn đơn đặt", !blockedBook.Ok, blockedBook.ErrorMessage);
                    DbHelper.Read(db, conn =>
                    {
                        DbHelper.Exec(conn, null,
                            "INSERT INTO TDONHANG (ID, NAME, STATUS, USERCREATEDID, LOAI, DATHANHTOAN, DBANID, TDATHANGID) VALUES (@I, @N, 30, 'selftest', 0, 1, @B, @D)",
                            "@I", bookOrderId, "@N", "HĐ THỬ ĐẶT BKV", "@B", roomId, "@D", bookId);
                        return 0;
                    });
                    Check("B3 đơn đặt đã chuyển hóa đơn → không chặn nữa",
                        !HasBlock(roomRepo.CheckRoomReferences(roomId), "TDATHANG"));
                }
                finally
                {
                    DbHelper.Read(db, conn =>
                    {
                        DbHelper.Exec(conn, null, "DELETE FROM TDONHANG WHERE ID = @I", "@I", bookOrderId);
                        DbHelper.Exec(conn, null, "DELETE FROM TDATHANG WHERE ID = @I", "@I", bookId);
                        return 0;
                    });
                }

                // Xóa bàn xóa kèm bảng giá áp dụng, không để dòng mồ côi.
                DbHelper.Read(db, conn =>
                {
                    DbHelper.Exec(conn, null,
                        "INSERT INTO DBANGGIATHEOBAN (ID, DBANID, DBANGGIAID, STATUS, USERCREATEDID) VALUES (@I, @B, '0', 30, 'selftest')",
                        "@I", DbHelper.NewId(), "@B", roomId);
                    return 0;
                });
                RoomDeleteResult deleted = roomRepo.DeleteRoom(roomId);
                Check("Xóa cứng bàn", deleted.Ok, deleted.ErrorMessage);
                string deletedRoomId = saved.SavedId;
                roomId = null;
                int orphans = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DBANGGIATHEOBAN WHERE DBANID = @B", "@B", deletedRoomId)));
                Check("Xóa bàn không để dòng bảng giá mồ côi", orphans == 0, "n=" + orphans);

                // Xóa khu vực bị chặn khi còn bàn; xóa khu vực xóa kèm 3 bảng con.
                string areaErr = areaRepo.Delete(areaId);
                Check("Q4 xóa khu vực còn bàn → chặn", areaErr != null, areaErr);
                var area2 = AreaRow.NewDefault();
                area2.Name = "KHU VỰC THỬ BKV 2";
                AreaSaveResult area2Saved = areaRepo.Save(area2,
                    new List<PriceAssignment>(), new List<string>(), new List<AreaDefaultItem>(), "selftest");
                area2Id = area2Saved.SavedId;
                Check("Tạo khu vực thử thứ hai", area2Saved.Ok && area2Id != null, area2Saved.ErrorMessage);
                string area2IdSaved = area2Id;
                DbHelper.Read(db, conn =>
                {
                    DbHelper.Exec(conn, null,
                        "INSERT INTO DBANGGIATHEOKHUVUC (ID, DKHUVUCID, DBANGGIAID, STATUS, USERCREATEDID) VALUES (@I, @A, '0', 30, 'selftest')",
                        "@I", DbHelper.NewId(), "@A", area2IdSaved);
                    DbHelper.Exec(conn, null,
                        "INSERT INTO DTKTHEOKHUVUC (ID, DKHUVUCID, SUSERID, STATUS, USERCREATEDID) VALUES (@I, @A, 'selftest', 30, 'selftest')",
                        "@I", DbHelper.NewId(), "@A", area2IdSaved);
                    DbHelper.Exec(conn, null,
                        "INSERT INTO DMATHANGMACDINH (ID, DKHUVUCID, DMATHANGID, STATUS, USERCREATEDID) VALUES (@I, @A, 'x', 30, 'selftest')",
                        "@I", DbHelper.NewId(), "@A", area2IdSaved);
                    return 0;
                });
                string del2 = areaRepo.Delete(area2Id);
                Check("Xóa khu vực không còn bàn", del2 == null, del2);
                area2Id = null;
                int c1 = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DBANGGIATHEOKHUVUC WHERE DKHUVUCID = @A", "@A", area2IdSaved)));
                int c2 = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DTKTHEOKHUVUC WHERE DKHUVUCID = @A", "@A", area2IdSaved)));
                int c3 = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DMATHANGMACDINH WHERE DKHUVUCID = @A", "@A", area2IdSaved)));
                Check("Xóa khu vực xóa kèm bảng giá/truy cập/mặt hàng mặc định", c1 + c2 + c3 == 0, c1 + "/" + c2 + "/" + c3);

                // Dialog: tab giá ẩn khi CoTienGio = 0 (DB hiện = 0); dialog khu vực chỉ có tab chính khi mọi cấu hình tắt.
                using (var dlg = new Kazuko_FnB.UI.Dialogs.RoomEditDialog(AppSession.Current, null, areaId, new List<string>()))
                {
                    Check("1.2 tab 'Bảng giá áp dụng' ẩn khi CoTienGio = 0", !dlg.HasPriceTab);
                }
                using (var dlg = new Kazuko_FnB.UI.Dialogs.AreaEditDialog(AppSession.Current, null, new List<string>()))
                {
                    Check("1.3 dialog khu vực chỉ có tab chính khi cấu hình tắt", dlg.TabCount == 1, "tabs=" + dlg.TabCount);
                }
            }
            finally
            {
                try
                {
                    if (orderId != null)
                        DbHelper.Read(db, conn =>
                        {
                            DbHelper.Exec(conn, null, "DELETE FROM TDONHANG WHERE ID = @I", "@I", orderId);
                            return 0;
                        });
                }
                catch { }
                try { if (roomId != null) roomRepo.DeleteRoom(roomId); } catch { }
                try
                {
                    DbHelper.Read(db, conn =>
                    {
                        DbHelper.Exec(conn, null, "DELETE FROM DBAN WHERE DKHUVUCID = @A AND NAME IN ('B02', 'B03')", "@A", areaId);
                        return 0;
                    });
                }
                catch { }
                try { if (areaId != null) areaRepo.Delete(areaId); } catch { }
                try { if (area2Id != null) areaRepo.Delete(area2Id); } catch { }
            }
        }

        // ------------------------------------------------------------------ H5 Đổi mật khẩu

        private static void PasswordTests(string db)
        {
            Check("Mật khẩu cũ sai → chặn", PasswordPolicy.Check("abc", "x", "n", "n") == PasswordCheck.WrongOld);
            Check("Nhập lại không khớp → chặn", PasswordPolicy.Check("abc", "abc", "n1", "n2") == PasswordCheck.Mismatch);
            Check("Mới trùng cũ → chặn", PasswordPolicy.Check("abc", "abc", "abc", "abc") == PasswordCheck.SameAsOld);
            Check("Mới để trống → hỏi xác nhận (cho phép)", PasswordPolicy.Check("abc", "abc", "", "") == PasswordCheck.EmptyNeedsConfirm);
            Check("Admin mật khẩu rỗng đổi sang rỗng → trùng cũ, chặn", PasswordPolicy.Check("", "", "", "") == PasswordCheck.SameAsOld);
            Check("Hợp lệ", PasswordPolicy.Check("abc", "abc", "mới 1", "mới 1") == PasswordCheck.Ok);
            Check("Phân biệt hoa thường", PasswordPolicy.Check("abc", "ABC", "n", "n") == PasswordCheck.WrongOld);
            Check("Không giới hạn độ dài tối thiểu (1 ký tự hợp lệ)", PasswordPolicy.Check("abcdef", "abcdef", "1", "1") == PasswordCheck.Ok);

            var users = new UserAdminRepository(db);
            var groups = new GroupRepository(db);
            string gid = groups.Save(new GroupRow { Name = "NHÓM MẬT KHẨU" }, "selftest");
            string uid = users.Save(new UserRow { Name = "MK THỬ", UserName = "mk.thu", Password = "cu", GroupId = gid }, new string[0], "selftest");
            users.SetPassword(uid, "moi", "selftest");
            Check("Đổi mật khẩu ghi vào DB (chữ thường)", users.GetPassword(uid) == "moi");
            users.Delete(uid);
            groups.Delete(gid);
        }

        // ------------------------------------------------------------------ H2 Phân quyền

        private static void RoleTests(string db)
        {
            // Quy tắc đổi cờ.
            Check("RoleLogic: tích Thêm kéo theo Xem (0x50)", RoleLogic.SetBit(0, RoleModes.Add, true) == 0x50);
            Check("RoleLogic: bỏ Xem = khóa", RoleLogic.SetBit(0xF0, RoleModes.View, false) == 0);
            Check("RoleLogic: bỏ ô Khóa → Xem (0x10)", RoleLogic.SetLocked(0, false) == 0x10);
            Check("RoleLogic: tích Khóa → 0", RoleLogic.SetLocked(0xF0, true) == 0);
            Check("RoleLogic: Tất cả = 0xF0; chức năng chỉ-xem = 0x10", RoleLogic.SetAll(0x10, true, false) == 0xF0 && RoleLogic.SetAll(0, true, true) == 0x10);
            Check("RoleLogic: bỏ Sửa giữ Xem + Thêm", RoleLogic.SetBit(0x70, RoleModes.Edit, false) == 0x50);
            Check("LamTronTien: 1499 → 1000 (bội 1000)", Kazuko_FnB.Services.MoneyRounding.RoundToStep(1499m, 1000m) == 1000m);
            Check("LamTronTien: 1500 → 2000 (hòa làm tròn xa 0)", Kazuko_FnB.Services.MoneyRounding.RoundToStep(1500m, 1000m) == 2000m);
            Check("LamTronTien: -1500 → -2000 (xa 0 cả số âm)", Kazuko_FnB.Services.MoneyRounding.RoundToStep(-1500m, 1000m) == -2000m);
            Check("LamTronTien: 0 → 0", Kazuko_FnB.Services.MoneyRounding.RoundToStep(0m, 1000m) == 0m);
            bool stepThrew = false;
            try { Kazuko_FnB.Services.MoneyRounding.RoundToStep(1m, 0m); }
            catch (ArgumentOutOfRangeException) { stepThrew = true; }
            Check("LamTronTien: bước 0 ném ArgumentOutOfRangeException", stepThrew);

            // Thành phần UI dùng chung (không cần hiện cửa sổ).
            try
            {
                using (var cm = new Kazuko_FnB.UI.Controls.ThemedContextMenu())
                {
                    cm.AddItem("Mục A", null);
                    cm.AddSeparator();
                    cm.AddItem("Mục B", System.Windows.Forms.Keys.F5, null);
                    Check("ThemedContextMenu: 2 mục + 1 vạch ngăn", cm.Items.Count == 3);
                    Check("ThemedContextMenu: dùng ThemedMenuRenderer của menu chính", cm.Renderer is Kazuko_FnB.UI.Controls.ThemedMenuRenderer);
                    Check("ThemedContextMenu: mục B có phím tắt F5", ((System.Windows.Forms.ToolStripMenuItem)cm.Items[2]).ShortcutKeys == System.Windows.Forms.Keys.F5);
                }
                using (var panel = new System.Windows.Forms.Panel())
                {
                    var ib = new Kazuko_FnB.UI.Controls.InputBox();
                    var plain = new System.Windows.Forms.Button();
                    panel.Controls.Add(ib);
                    panel.Controls.Add(plain);
                    Kazuko_FnB.Theme.ThemeTokens t0 = Kazuko_FnB.Theme.ThemeManager.Current;
                    Kazuko_FnB.UI.ThemeApplier.Apply(panel, t0);
                    Check("ThemeApplier tô InputBox theo token (focus = Primary, chữ = Text)", ib.FocusColor == t0.Primary && ib.TextColor == t0.Text);
                    Check("ThemeApplier tô Button thô theo theme (nền = Surface, phẳng)", plain.BackColor == t0.Surface && plain.FlatStyle == System.Windows.Forms.FlatStyle.Flat);
                }
                using (var f = new Kazuko_FnB.UI.BaseForm())
                    Check("BaseForm: font Segoe UI 10pt, AutoScaleMode.None",
                        f.Font.Name == "Segoe UI" && Math.Abs(f.Font.Size - 10f) < 0.01f && f.AutoScaleMode == System.Windows.Forms.AutoScaleMode.None);
            }
            catch (Exception ex)
            {
                Check("Thành phần UI dùng chung dựng được", false, ex.GetType().Name + ": " + ex.Message);
            }

            var roles = new RoleRepository(db);
            var groups = new GroupRepository(db);
            var users = new UserAdminRepository(db);
            List<FunctionRow> funcs = roles.LoadFunctions();
            Check("Đủ 86 chức năng trong ma trận", funcs.Count == 86, "n=" + funcs.Count);
            Check("Có chức năng chỉ-xem (NOTE=View) và chức năng đủ quyền", funcs.Exists(f => f.ViewOnly) && funcs.Exists(f => !f.ViewOnly));

            FunctionRow login = funcs.Find(f => f.Id == LoginService.LoginFunctionId);
            FunctionRow kh = funcs.Find(f => f.Name == "Danh mục khách hàng");
            Check("Có chức năng Đăng nhập và Danh mục khách hàng", login != null && kh != null);
            if (login == null || kh == null)
                return;

            string gid = groups.Save(new GroupRow { Name = "NHÓM QUYỀN THỬ" }, "selftest");
            string uid = users.Save(new UserRow { Name = "QUYỀN THỬ", UserName = "quyen.thu", Password = "q", GroupId = gid }, new string[0], "selftest");
            var svc = new LoginService();

            Check("Nhóm mới chưa có quyền: không đăng nhập được", svc.Login(db, "quyen.thu", "q").Code == LoginResultCode.NoLoginPermission);

            roles.SaveModes(gid, new Dictionary<string, int> { { login.Id, 0x10 }, { kh.Id, 0x50 } }, "selftest");
            Dictionary<string, int> modes = roles.LoadModes(gid);
            Check("Ghi quyền rồi đọc lại đúng cờ", modes[login.Id] == 0x10 && modes[kh.Id] == 0x50);
            LoginResult ok = svc.Login(db, "quyen.thu", "q");
            Check("Có quyền Đăng nhập → đăng nhập được", ok.Success);
            if (ok.Success)
            {
                FunctionPermissions p = ok.Session.Permissions;
                Check("Quyền khách hàng: Xem + Thêm, không Sửa/Xóa",
                    p.Can("Danh mục khách hàng", RoleModes.View) && p.Can("Danh mục khách hàng", RoleModes.Add) &&
                    !p.Can("Danh mục khách hàng", RoleModes.Edit) && !p.Can("Danh mục khách hàng", RoleModes.Remove));
                Check("Chức năng không có dòng quyền = khóa", !p.AllowsName("Danh mục mặt hàng") && p.ModeByName("Danh mục mặt hàng") == 0);
            }

            roles.SaveModes(gid, new Dictionary<string, int> { { kh.Id, 0xF0 } }, "selftest");
            Check("Cập nhật dòng có sẵn (không thêm dòng trùng)", roles.LoadModes(gid)[kh.Id] == 0xF0);
            using (FbConnection conn = KazukoConnection.Open(db))
                Check("Mỗi (nhóm, chức năng) chỉ một dòng",
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SGROUPROLE WHERE SGROUPUSERID = @G AND SFUNCTIONID = @F", "@G", gid, "@F", kh.Id)) == 1);

            roles.SaveModes(gid, new Dictionary<string, int> { { login.Id, 0 } }, "selftest");
            Check("Khóa Đăng nhập → không đăng nhập được", svc.Login(db, "quyen.thu", "q").Code == LoginResultCode.NoLoginPermission);

            // Quyền xem báo cáo.
            List<ReportRow> reps = roles.LoadReports().FindAll(r => !r.IsFolder);
            Check("Có báo cáo để phân quyền (126 đang dùng)", reps.Count >= 100, "n=" + reps.Count);
            roles.SetReportsAllowed(gid, new[] { reps[0].Id, reps[1].Id }, true, "selftest");
            HashSet<string> allowed = roles.LoadAllowedReports(gid);
            Check("Cho xem 2 báo cáo", allowed.Count == 2 && allowed.Contains(reps[0].Id));
            roles.SetReportsAllowed(gid, new[] { reps[0].Id }, true, "selftest");
            Check("Cho xem lần nữa không nhân đôi dòng", roles.LoadAllowedReports(gid).Count == 2);
            roles.SetReportsAllowed(gid, new[] { reps[0].Id }, false, "selftest");
            Check("Bỏ quyền xem 1 báo cáo", roles.LoadAllowedReports(gid).Count == 1);

            users.Delete(uid);
            groups.Delete(gid);
            using (FbConnection conn = KazukoConnection.Open(db))
                Check("Xóa nhóm kéo theo dòng quyền sử dụng và quyền báo cáo",
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SGROUPROLE WHERE SGROUPUSERID = @G", "@G", gid)) == 0 &&
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SREPORTROLE WHERE SGROUPUSERID = @G", "@G", gid)) == 0);
        }

        // ------------------------------------------------------------------ H1/H2 Người dùng và nhóm

        private static void UserTests(string db)
        {
            var groups = new GroupRepository(db);
            var users = new UserAdminRepository(db);

            Check("Danh sách người dùng không có Admin", users.LoadUsers(null).TrueForAll(u => u.UserName != "Admin"));
            GroupRow thuNgan = groups.LoadAll().Find(g => g.Name == "Thu ngân");
            Check("Có nhóm Thu ngân", thuNgan != null);
            if (thuNgan != null)
            {
                CheckThuNganDeleteBlocked(db, groups, users, thuNgan);
            }

            // Nhóm mới: AUTOID/SORTORDER theo quy ước, chặn trùng tên.
            int maxAuto;
            using (FbConnection conn = KazukoConnection.Open(db))
                maxAuto = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT MAX(AUTOID) FROM SGROUPUSER"));
            string gid = groups.Save(new GroupRow { Name = "NHÓM THỬ", Note = "ghi chú thử", ImageId = null }, "selftest");
            GroupRow g1 = groups.LoadAll().Find(g => g.Id == gid);
            Check("Thêm nhóm", g1 != null && g1.Name == "NHÓM THỬ" && g1.Note == "ghi chú thử");
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                var dt = DbHelper.Table(conn, null, "SELECT AUTOID, SORTORDER, ITEMTYPE, STATUS FROM SGROUPUSER WHERE ID = @I", "@I", gid);
                Check("Nhóm mới: AUTOID = lớn nhất + 1, SORTORDER dạng ZZZ###, STATUS 30",
                    DbHelper.Int(dt.Rows[0][0]) == maxAuto + 1 && (DbHelper.Str(dt.Rows[0][1]) ?? "").StartsWith("ZZZ") && DbHelper.Int(dt.Rows[0][3]) == 30,
                    "autoid=" + dt.Rows[0][0] + " sort=" + dt.Rows[0][1]);
            }
            Check("Chặn trùng tên nhóm (không phân biệt hoa thường)", groups.IsNameTaken("nhóm thử", null) && !groups.IsNameTaken("nhóm thử", gid));
            groups.Save(new GroupRow { Id = gid, Name = "NHÓM THỬ 2", Note = "", ImageId = null }, "selftest");
            Check("Sửa nhóm", groups.LoadAll().Exists(g => g.Id == gid && g.Name == "NHÓM THỬ 2"));

            // Tài khoản mới + cửa hàng.
            var stores = users.LoadStores(null);
            Check("Thêm mới: mọi cửa hàng được tích sẵn", stores.Count > 0 && stores.TrueForAll(x => x.Access));
            var storeIds = new List<string>();
            foreach (StoreAccess st in stores) storeIds.Add(st.StoreId);
            string uid = users.Save(new UserRow { Name = "NGƯỜI THỬ", UserName = "nguoi.thu", Password = "p1", Email = "t@t", GroupId = gid }, storeIds, "selftest");
            UserRow u1 = users.Get(uid);
            Check("Thêm tài khoản (tiếng Việt có dấu)", u1 != null && u1.Name == "NGƯỜI THỬ" && u1.UserName == "nguoi.thu" && u1.GroupId == gid);
            Check("Ghi quyền cửa hàng đúng số dòng", users.LoadStores(uid).FindAll(x => x.Access).Count == storeIds.Count);
            Check("Trùng tài khoản/họ tên bị phát hiện (không phân biệt hoa thường)", users.IsTaken("USERNAME", "NGUOI.THU", null) && users.IsTaken("NAME", "người thử", null));
            Check("Sửa chính tài khoản đó không báo trùng", !users.IsTaken("USERNAME", "nguoi.thu", uid) && !users.IsTaken("NAME", "NGƯỜI THỬ", uid));
            Check("Tài khoản hiện trong danh sách nhóm", users.LoadUsers(gid).Exists(x => x.Id == uid));

            // Sửa giữ nguyên các cột không có trên form.
            using (FbConnection conn = KazukoConnection.Open(db))
            using (FbTransaction tx = conn.BeginTransaction())
            {
                DbHelper.Exec(conn, tx, "UPDATE SUSER SET CARDCODE = 'THE-123', SMTP = 'smtp.x', PORT = 25, VIETINPUT = 30 WHERE ID = @I", "@I", uid);
                tx.Commit();
            }
            users.Save(new UserRow { Id = uid, Name = "NGƯỜI THỬ", UserName = "nguoi.thu", Password = "p2", Email = "x@x", GroupId = gid }, new string[0], "selftest");
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                var dt = DbHelper.Table(conn, null, "SELECT CARDCODE, SMTP, PORT, VIETINPUT, \"PASSWORD\", EMAIL, ISADMIN FROM SUSER WHERE ID = @I", "@I", uid);
                Check("Sửa giữ nguyên CARDCODE/SMTP/PORT/VIETINPUT",
                    DbHelper.Str(dt.Rows[0][0]) == "THE-123" && DbHelper.Str(dt.Rows[0][1]) == "smtp.x" && DbHelper.Int(dt.Rows[0][2]) == 25 && DbHelper.Int(dt.Rows[0][3]) == 30);
                Check("Sửa ghi mật khẩu/email mới", DbHelper.Str(dt.Rows[0][4]) == "p2" && DbHelper.Str(dt.Rows[0][5]) == "x@x");
            }
            Check("Bỏ tích hết cửa hàng → không còn dòng cửa hàng", users.LoadStores(uid).TrueForAll(x => !x.Access));

            // Đổi mật khẩu.
            Check("GetPassword đọc đúng", users.GetPassword(uid) == "p2");
            users.SetPassword(uid, "p3", "selftest");
            Check("SetPassword ghi mật khẩu mới (chữ thường)", users.GetPassword(uid) == "p3");

            // Xóa: nhóm còn tài khoản bị chặn; xóa tài khoản rồi xóa nhóm.
            Check("Nhóm thử còn 1 tài khoản → chặn xóa", groups.CountReferences(gid) == 1);
            Check("Tài khoản chưa có hóa đơn → xóa được", users.CountReferences(uid) == 0);
            users.Delete(uid);
            Check("Xóa tài khoản", users.Get(uid) == null);
            Check("Nhóm hết tài khoản → xóa được", groups.CountReferences(gid) == 0);
            groups.Delete(gid);
            Check("Xóa nhóm", !groups.LoadAll().Exists(g => g.Id == gid));
            using (FbConnection conn = KazukoConnection.Open(db))
                Check("Xóa nhóm kéo theo dòng quyền (không còn mồ côi)",
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SGROUPROLE WHERE SGROUPUSERID = @G", "@G", gid)) == 0);
        }

        // ------------------------------------------------------------------ H4 Ảnh

        private static byte[] SamplePng(System.Drawing.Color c)
        {
            using (var bmp = new System.Drawing.Bitmap(8, 8))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    g.Clear(c);
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private static void ImageTests(string db)
        {
            var repo = new ImageRepository(db);
            int before = repo.LoadAll().Count;
            byte[] a = SamplePng(System.Drawing.Color.Red), b = SamplePng(System.Drawing.Color.Blue);
            string id = repo.Add("ẢNH THỬ", a, "selftest");
            Check("Thêm ảnh: số ảnh +1", repo.LoadAll().Count == before + 1);
            Check("Ảnh đọc lại giống ảnh đã ghi", Equal(repo.Get(id), a));
            repo.Replace(id, b, "selftest");
            Check("Thay thế ảnh", Equal(repo.Get(id), b));
            Check("Ảnh mới chưa ai dùng (CountUsage = 0)", repo.CountUsage(id) == 0);

            string usedId;
            using (FbConnection conn = KazukoConnection.Open(db))
                usedId = DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT FIRST 1 SIMAGEID FROM SGROUPUSER WHERE SIMAGEID IS NOT NULL"));
            Check("Ảnh đang được nhóm người dùng dùng thì CountUsage > 0", usedId != null && repo.CountUsage(usedId) > 0);
            Check("Thông báo chặn xóa đúng chữ gốc",
                ReferenceChecker.BlockedMessage("ảnh", "Sao", 3) == "Bạn không thể xóa ảnh 'Sao' vì còn (3) dữ liệu khác liên kết tới 'Sao'");

            repo.Delete(id);
            Check("Xóa ảnh không còn dùng", repo.LoadAll().Count == before && repo.Get(id) == null);
        }

        // ------------------------------------------------------------------ Bản thử Phiếu thu (layout từ SFORM.AELAYOUT)

        private static void PhieuThuTests(string db)
        {
            // Luật thuần (SFORM.CODE của form gốc).
            ObjectFieldState s0 = PhieuThuRules.ForObjectType(0), s1 = PhieuThuRules.ForObjectType(1),
                s2 = PhieuThuRules.ForObjectType(2), s3 = PhieuThuRules.ForObjectType(3), sx = PhieuThuRules.ForObjectType(-1);
            Check("Loại đối tượng 0 (khác): bật tên và địa chỉ, tắt nhân viên/khách/NCC",
                s0.Name && s0.Address && !s0.Employee && !s0.Customer && !s0.Supplier);
            Check("Loại đối tượng 1 (nhân viên): chỉ bật ô nhân viên", s1.Employee && !s1.Customer && !s1.Supplier && !s1.Name && !s1.Address);
            Check("Loại đối tượng 2 (khách hàng): chỉ bật ô khách hàng", s2.Customer && !s2.Employee && !s2.Supplier && !s2.Name && !s2.Address);
            Check("Loại đối tượng 3 (nhà cung cấp): chỉ bật ô nhà cung cấp", s3.Supplier && !s3.Employee && !s3.Customer && !s3.Name && !s3.Address);
            Check("Chưa chọn loại: tắt hết", !sx.Name && !sx.Address && !sx.Employee && !sx.Customer && !sx.Supplier);
            string n, a;
            PhieuThuRules.FillFromEmployee("Bùi Thu Minh", out n, out a);
            Check("Chọn nhân viên: tên = tên nhân viên, địa chỉ trống", n == "Bùi Thu Minh" && a == "");
            PhieuThuRules.FillFromParty("Anh Chung", "12 Xuân Thủy", out n, out a);
            Check("Chọn khách/nhà cung cấp: tên và địa chỉ của họ", n == "Anh Chung" && a == "12 Xuân Thủy");
            PhieuThuRules.FillFromParty(null, null, out n, out a);
            Check("Bỏ chọn: xóa tên và địa chỉ", n == "" && a == "");
            Check("Tích chuyển khoản bật ô tài khoản ngân hàng", PhieuThuRules.BankAccountEnabled(true) && !PhieuThuRules.BankAccountEnabled(false));
            Check("Chuyển khoản mà chưa chọn tài khoản: báo đúng chữ gốc",
                PhieuThuRules.Validate(null, true, false) == "Mời bạn chọn tài khoản ngân hàng");
            Check("Chuyển khoản đã chọn tài khoản hoặc không chuyển khoản: hợp lệ",
                PhieuThuRules.Validate(null, true, true) == null && PhieuThuRules.Validate(new List<string>(), false, false) == null);
            Check("Thiếu trường bắt buộc: báo tên trường",
                PhieuThuRules.Validate(new List<string> { "Số phiếu", "Cửa hàng" }, false, false) == "Mời bạn nhập: Số phiếu, Cửa hàng");

            // Layout đọc từ DB.
            var repo = new FormRepository(db);
            ScreenLayout lay = repo.LoadLayout("Phiếu thu");
            Check("Đọc được layout form Phiếu thu từ SFORM.AELAYOUT", lay != null);
            if (lay == null)
                return;
            Check("Layout: bảng đích TTHUCHI, kích thước gốc 527x379", lay.Table == "TTHUCHI" && lay.Width == 527 && lay.Height == 379,
                lay.Table + " " + lay.Width + "x" + lay.Height);
            Check("Layout: 21 dòng ánh xạ trường", lay.Maps.Count == 21, "n=" + lay.Maps.Count);
            var required = new List<string>();
            foreach (FieldMap m in lay.Maps)
                if (m.Required)
                    required.Add(m.DataField);
            required.Sort();
            Check("Layout: đúng 3 trường bắt buộc (CUAHANG, NAME, THU)", string.Join(",", required.ToArray()) == "DCUAHANGID,NAME,THU", string.Join(",", required.ToArray()));
            int editors = lay.Controls.FindAll(c => c.Kind == LayoutKind.Text || c.Kind == LayoutKind.Number || c.Kind == LayoutKind.Date
                || c.Kind == LayoutKind.Combo || c.Kind == LayoutKind.Lookup || c.Kind == LayoutKind.Check).Count;
            Check("Layout: 17 ô nhập và ô tích", editors == 17, "n=" + editors);
            LayoutControl cbo = lay.Find("cboLOAIDOITUONG");
            Check("Layout: ô Loại đối tượng có 4 mục đúng thứ tự",
                cbo != null && string.Join("|", cbo.Items.ToArray()) == "Đối tượng khác|Nhân viên|Khách hàng|Nhà cung cấp");
            LayoutControl kh = lay.Find("lueDKHACHHANGID");
            Check("Layout: ô Khách hàng lúc đầu bị tắt", kh != null && !kh.Enabled);

            Dictionary<string, string> tables = repo.LookupTables("TTHUCHI");
            Check("Ô tra cứu trỏ đúng bảng (lý do, khách hàng, nhân viên, nhà cung cấp, cửa hàng, tài khoản ngân hàng)",
                tables.ContainsKey("DLYDOTHUCHIID") && tables["DLYDOTHUCHIID"] == "DLYDOTHUCHI"
                && tables["DKHACHHANGID"] == "DKHACHHANG" && tables["DNHANVIENID"] == "DNHANVIEN" && tables["DNHACUNGCAPID"] == "DNHACUNGCAP"
                && tables["DCUAHANGID"] == "DCUAHANG" && tables["DTAIKHOANNGANHANGID"] == "DTAIKHOANNGANHANG");

            int reasons, allCustomers, userStores;
            string anyUser;
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                reasons = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DLYDOTHUCHI WHERE STATUS = 30 AND LALYDOTHU = 30"));
                allCustomers = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DKHACHHANG WHERE STATUS = 30"));
                anyUser = DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT FIRST 1 SUSERID FROM TNGUOIDUNGTHEOCUAHANG"));
                userStores = anyUser == null ? 0 : DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DCUAHANG WHERE STATUS = 30 AND ID IN (SELECT DCUAHANGID FROM TNGUOIDUNGTHEOCUAHANG WHERE SUSERID = @u)", "@u", anyUser));
            }
            Check("Danh sách lý do thu = số dòng LALYDOTHU = 30", repo.LoadIncomeReasons().Count == reasons, "n=" + reasons);
            Check("Danh sách khách hàng = số dòng đang dùng", repo.LoadAll("DKHACHHANG").Count == allCustomers, "n=" + allCustomers);
            if (anyUser != null)
                Check("Cửa hàng của người dùng không phải admin = các cửa hàng được gán", repo.LoadStoresOfUser(anyUser).Count == userStores, "n=" + userStores);

            // Mục menu: "Tạo phiếu thu" mở bản thử, "Tạo phiếu chi" thì chưa.
            MenuNode thu = null, chi = null;
            foreach (MenuNode node in Flatten(new MenuRepository(db).LoadTree()))
            {
                if (node.Title == "Tạo phiếu thu") thu = node;
                if (node.Title == "Tạo phiếu chi") chi = node;
            }
            UserSession sess = AppSession.Current;
            Check("Menu \"Tạo phiếu thu\" mở bản thử Phiếu thu", thu != null && UI.Dialogs.ScreenFactory.IsNewIncomeVoucher(thu, sess));
            Check("Menu \"Tạo phiếu chi\" chưa có màn (vẫn là tab tạm)", chi != null && !UI.Dialogs.ScreenFactory.IsNewIncomeVoucher(chi, sess));

            // Dựng màn từ layout (không cần cửa sổ).
            try
            {
                using (var form = new UI.DynamicForm(lay))
                {
                    int count = 0;
                    foreach (string nm in form.EditorNames)
                        count++;
                    Check("Màn dựng đủ 17 ô nhập từ layout", count == 17, "n=" + count);
                    Check("Mọi control trong layout đều dựng được (không bỏ sót)", form.Skipped.Count == 0, string.Join(",", new List<string>(form.Skipped).ToArray()));
                    var lblNum = form.LabelOf("txtNAME");
                    Check("Trường bắt buộc có dấu * ở nhãn", lblNum != null && lblNum.Text.EndsWith("*"));
                    Check("Ô tra cứu và ô chọn dựng đúng loại", form.Editor("lueDKHACHHANGID") is UI.LookupCombo && form.Editor("cboLOAIDOITUONG") is System.Windows.Forms.ComboBox);
                }
            }
            catch (Exception ex)
            {
                Check("Dựng màn từ layout", false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ Danh mục dùng chung (xem)

        private static readonly string[] CatalogTables =
        {
            "DBANGGIA", "DCALAMVIEC", "DCUAHANG", "DKHOHANG", "DLOAIPHONG", "DLYDOTHUCHI", "DNHANVIEN", "DTAIKHOANNGANHANG",
            "DBAN", "DDOTKHUYENMAI", "DKHACHHANG", "DMATHANG", "DMATKHAUWIFI", "DNHACUNGCAP", "DTHETRATRUOC", "DVOUCHER"
        };

        private static void CatalogTests(string db)
        {
            // Hàm thuần (không cần DB).
            Check("Tìm không dấu: 'Đặng Văn Ơi' → 'dang van oi'", CatalogRepository.Fold("Đặng Văn Ơi") == "dang van oi");
            Check("Tìm không dấu: 'Cà phê Sữa' → 'ca phe sua'", CatalogRepository.Fold("Cà phê Sữa") == "ca phe sua");
            Check("Thoát ký tự đặc biệt khi lọc", CatalogRepository.EscapeLike("a'b[c]*%") == "a''b[[]c[]][*][%]");

            List<GridColumnSpec> g = CatalogRepository.ParseGrid("CODE|63|true|NAME|217|false|NOTE|56|true ");
            Check("Đọc cấu hình lưới SGRID (3 cột, cột 2 ẩn, độ rộng 217)",
                g.Count == 3 && g[0].Name == "CODE" && g[0].Visible && g[1].Name == "NAME" && !g[1].Visible && g[1].Width == 217 && g[2].Visible);

            var nodes = new Dictionary<string, CatalogNode>
            {
                { "a", new CatalogNode { Id = "a", ParentId = null } },
                { "b", new CatalogNode { Id = "b", ParentId = "a" } },
                { "c", new CatalogNode { Id = "c", ParentId = "b" } }
            };
            Check("Đường đi nhóm cha → con", CatalogRepository.CategoryPath(nodes, "c") == "|a|b|c|");
            nodes["a"].ParentId = "c";
            Check("Vòng lặp nhóm không treo máy", CatalogRepository.CategoryPath(nodes, "c").Length > 0);

            // Trên DB.
            var repo = new CatalogRepository(db);
            var before = new Dictionary<string, string>();
            foreach (string t in CatalogTables)
                before[t] = Stamp(db, t);

            int treeCount = 0;
            foreach (string t in CatalogTables)
            {
                string id = repo.FindIdByTable(t);
                Check("[" + t + "] có định nghĩa trong STABLEDESC", id != null);
                if (id == null)
                    continue;
                Check("[" + t + "] dùng được màn danh mục chung", repo.IsSupported(id));
                CatalogDefinition def = repo.LoadDefinition(id);
                Check("[" + t + "] có cột hiển thị (kể cả cột tên)", def != null && def.Columns.Count > 0 && def.Columns.Exists(c => c.Name == "NAME"),
                    def == null ? "null" : def.Columns.Count + " cột");
                if (def == null)
                    continue;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                CatalogData data = repo.Load(def);
                sw.Stop();
                int expected;
                using (FbConnection conn = KazukoConnection.Open(db))
                    expected = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM \"" + t + "\" WHERE STATUS = 30"));
                Check("[" + t + "] số dòng hiện = số dòng STATUS = 30", data.Rows.Count == expected, data.Rows.Count + " / " + expected + ", đọc " + sw.ElapsedMilliseconds + " ms");
                if (def.CategoryTable != null)
                {
                    treeCount++;
                    Check("[" + t + "] cây nhóm đọc được", data.Nodes.Count >= 0 && def.CategoryColumn != null);
                }
            }
            Check("Có 4 danh mục dạng cây nhóm (bàn, đợt khuyến mại, khách hàng, mặt hàng)", treeCount == 4, "n=" + treeCount);

            string khId = repo.FindIdByTable("DKHACHHANG");
            if (khId != null)
            {
                CatalogDefinition kh = repo.LoadDefinition(khId);
                Check("Khách hàng: thứ tự cột theo SGRID của app gốc (Tên, Nhóm, Mã khách…), ghi chú ẩn",
                    kh.Columns.Count == 10 && kh.Columns[0].Name == "NAME" && kh.Columns[1].Name == "DNHOMKHACHHANGID" && kh.Columns[2].Name == "MAKHACH"
                    && !kh.Columns.Exists(c => c.Name == "NOTE"), kh.Columns.Count + " cột");
                Check("Khách hàng: không còn cột CMND (POS TAP đã bỏ)", !kh.Columns.Exists(c => c.Name == "SOCMND"));
            }

            // Menu: số mục dùng được màn chung.
            int supported = 0;
            foreach (MenuNode n in Flatten(new MenuRepository(db).LoadTree()))
                if (n.Operation == 0 && !string.IsNullOrEmpty(n.StableDescId) && repo.IsSupported(n.StableDescId))
                    supported++;
            Check("Menu có 16 mục dùng màn danh mục chung", supported == 16, "n=" + supported);

            bool unchanged = true;
            var changed = new List<string>();
            foreach (string t in CatalogTables)
                if (Stamp(db, t) != before[t])
                {
                    unchanged = false;
                    changed.Add(t);
                }
            Check("Mở danh mục không ghi gì vào DB (số dòng và TIMEMODIFIED không đổi)", unchanged, string.Join(",", changed.ToArray()));
        }

        private static string Stamp(string db, string table)
        {
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                DataTable t = DbHelper.Table(conn, null, "SELECT COUNT(*), MAX(TIMEMODIFIED) FROM \"" + table + "\"");
                return Convert.ToString(t.Rows[0][0]) + "|" + Convert.ToString(t.Rows[0][1]);
            }
        }

        private static IEnumerable<MenuNode> Flatten(IEnumerable<MenuNode> roots)
        {
            foreach (MenuNode n in roots)
            {
                yield return n;
                foreach (MenuNode c in Flatten(n.Children))
                    yield return c;
            }
        }

        // ------------------------------------------------------------------ Giao diện (bảng màu)

        private static void ThemeTests()
        {
            var all = new List<Theme.ThemeTokens>(Theme.BuiltInThemes.All());
            Check("Có đúng 4 bảng màu", all.Count == 4, "n=" + all.Count);

            var names = new HashSet<string>();
            bool unique = true;
            foreach (Theme.ThemeTokens t in all)
                unique &= names.Add(t.Name);
            Check("Tên các bảng màu không trùng nhau", unique);
            Check("Bảng màu mặc định là Cam Nắng", Theme.BuiltInThemes.Default.Name == "Cam Nắng");
            Check("Tên bảng màu cũ (Nền tối) rơi về mặc định", ReferenceEquals(Theme.BuiltInThemes.Find("Nền tối"), Theme.BuiltInThemes.Default));
            Check("Tên rỗng rơi về mặc định", ReferenceEquals(Theme.BuiltInThemes.Find(null), Theme.BuiltInThemes.Default));

            foreach (Theme.ThemeTokens t in all)
            {
                bool light = Theme.ContrastChecker.Luminance(t.Background) > 0.8 && Theme.ContrastChecker.Luminance(t.Surface) > 0.8
                    && Theme.ContrastChecker.Luminance(t.Text) < 0.1;
                Check("Bảng màu \"" + t.Name + "\" là nền sáng", light);

                bool hasInfo = !string.IsNullOrWhiteSpace(t.Tag) && !string.IsNullOrWhiteSpace(t.Audience) && !string.IsNullOrWhiteSpace(t.Description);
                Check("Bảng màu \"" + t.Name + "\" có nhãn, đối tượng, mô tả", hasInfo);

                var fails = new List<string>();
                double min = double.MaxValue;
                foreach (Theme.ContrastPair p in Theme.ContrastChecker.Evaluate(t))
                {
                    min = Math.Min(min, p.Ratio);
                    if (!p.Pass)
                        fails.Add(p.Name + " " + p.Ratio.ToString("0.00") + "<" + p.Minimum.ToString("0.0"));
                }
                Check("Bảng màu \"" + t.Name + "\" đạt tương phản AA", fails.Count == 0,
                    fails.Count == 0 ? "thấp nhất " + min.ToString("0.00") : string.Join("; ", fails.ToArray()));
            }
        }

        private static bool Equal(byte[] x, byte[] y)
        {
            if (x == null || y == null || x.Length != y.Length)
                return false;
            for (int i = 0; i < x.Length; i++)
                if (x[i] != y[i])
                    return false;
            return true;
        }

        // ------------------------------------------------------------------ Lưu vết hoạt động (docs/specs/luu-vet-hoat-dong.md §7)

        private static void ActivityLogTests(string db)
        {
            // 1. Enum ActivityKind mang đúng số gốc 0–16 và tên hiển thị theo bảng §2.5 (Q9).
            var expectedKinds = new[]
            {
                new KeyValuePair<int, string>((int)ActivityKind.MoBan, "Mở bàn"),
                new KeyValuePair<int, string>((int)ActivityKind.DongBan, "Đóng bàn"),
                new KeyValuePair<int, string>((int)ActivityKind.InBill, "In bill"),
                new KeyValuePair<int, string>((int)ActivityKind.InCheBien, "In chế biến"),
                new KeyValuePair<int, string>((int)ActivityKind.ThemMon, "Thêm món"),
                new KeyValuePair<int, string>((int)ActivityKind.XoaMon, "Xóa món"),
                new KeyValuePair<int, string>((int)ActivityKind.GiamMon, "Giảm món"),
                new KeyValuePair<int, string>((int)ActivityKind.ChuyenBan, "Chuyển bàn"),
                new KeyValuePair<int, string>((int)ActivityKind.GopBan, "Gộp bàn"),
                new KeyValuePair<int, string>((int)ActivityKind.TangMon, "Tăng món"),
                new KeyValuePair<int, string>((int)ActivityKind.DatSoLuong, "Đặt số lượng"),
                new KeyValuePair<int, string>((int)ActivityKind.DoiGiaBan, "Đổi giá bán"),
                new KeyValuePair<int, string>((int)ActivityKind.DatGiamGiaMatHang, "Đặt giảm giá mặt hàng"),
                new KeyValuePair<int, string>((int)ActivityKind.HuyBill, "Hủy bill"),
                new KeyValuePair<int, string>((int)ActivityKind.Khac, "Khác"),
                new KeyValuePair<int, string>((int)ActivityKind.ChuyenMatHang, "Chuyển mặt hàng"),
                new KeyValuePair<int, string>((int)ActivityKind.TraDo, "Trả đồ")
            };
            bool kindsOk = true;
            for (int i = 0; i < expectedKinds.Length; i++)
                if (expectedKinds[i].Key != i || ActivityKindInfo.DisplayName(i) != expectedKinds[i].Value)
                    kindsOk = false;
            Check("Lưu vết: enum ActivityKind đúng số gốc 0–16 và đúng tên hiển thị", kindsOk);
            Check("Lưu vết: PHANLOAI ngoài 0–16 coi như Khác, không màu, không lỗi",
                ActivityKindInfo.DisplayName(-1) == "Khác" && ActivityKindInfo.DisplayName(99) == "Khác"
                && ActivityKindInfo.ToneOf(-1) == ActivityRowTone.Default && ActivityKindInfo.ToneOf(99) == ActivityRowTone.Default);

            // 2. Tô màu theo bảng §2.3 (6 loại có màu + mặc định).
            Check("Lưu vết: tông màu đúng bảng §2.3",
                ActivityKindInfo.ToneOf((int)ActivityKind.HuyBill) == ActivityRowTone.DangerBold
                && ActivityKindInfo.ToneOf((int)ActivityKind.ChuyenBan) == ActivityRowTone.Info
                && ActivityKindInfo.ToneOf((int)ActivityKind.GopBan) == ActivityRowTone.SuccessBold
                && ActivityKindInfo.ToneOf((int)ActivityKind.XoaMon) == ActivityRowTone.Danger
                && ActivityKindInfo.ToneOf((int)ActivityKind.GiamMon) == ActivityRowTone.Danger
                && ActivityKindInfo.ToneOf((int)ActivityKind.DoiGiaBan) == ActivityRowTone.Danger
                && ActivityKindInfo.ToneOf((int)ActivityKind.DatGiamGiaMatHang) == ActivityRowTone.Danger
                && ActivityKindInfo.ToneOf((int)ActivityKind.MoBan) == ActivityRowTone.Default
                && ActivityKindInfo.ToneOf((int)ActivityKind.ThemMon) == ActivityRowTone.Default);

            // 3. Cắt chuỗi quá 36 ký tự (Q5).
            bool cutShort, cutLong;
            string kept = ActivityLogger.TruncateField("Admin", 36, "TAIKHOAN", out cutShort);
            string longName = new string('A', 45);
            string trimmed = ActivityLogger.TruncateField(longName, 36, "TAIKHOAN", out cutLong);
            Check("Lưu vết: cắt ngắn TAIKHOAN/CHUCNANG > 36 ký tự (Q5)",
                kept == "Admin" && !cutShort && trimmed.Length == 36 && cutLong);

            // 4. Quy tắc hiện nút Xóa lưu vết & hộp mật khẩu 9876 (§3.4, Q1).
            var adminSession = new UserSession { UserId = "selftest", UserName = "Admin", DisplayName = "Administrator", IsAdmin = true, DatabasePath = db };
            var staffSession = new UserSession { UserId = "staff1", UserName = "thungan", DisplayName = "Thu ngân", IsAdmin = false, DatabasePath = db };
            Check("Lưu vết: nút xóa chỉ hiện khi admin + Ctrl+Shift",
                ActivityLogger.CanShowDeleteButton(adminSession, true)
                && !ActivityLogger.CanShowDeleteButton(adminSession, false)
                && !ActivityLogger.CanShowDeleteButton(staffSession, true));
            Check("Lưu vết: kiểm mật khẩu 9876 so chuỗi thô",
                ActivityLogger.IsDeletePasswordValid("9876")
                && !ActivityLogger.IsDeletePasswordValid("987")
                && !ActivityLogger.IsDeletePasswordValid(" 9876")
                && !ActivityLogger.IsDeletePasswordValid(""));
            using (var dlg = new Dialogs.ConfirmDeleteActivityLogDialog())
            {
                bool initialOff = !dlg.IsAcceptEnabled;
                dlg.SetPasswordForTest("1234");
                bool wrongOff = !dlg.IsAcceptEnabled;
                dlg.SetPasswordForTest("9876");
                bool rightOn = dlg.IsAcceptEnabled;
                Check("Lưu vết: hộp xác nhận xóa bật nút Chấp nhận đúng khi nhập 9876", initialOff && wrongOff && rightOn);
            }

            // 5. Seed bill, bill hủy, lưu vết trên bản sao DB và kiểm các tiêu chí §7.
            var repo = new ActivityLogRepository(db);
            var logger = new ActivityLogger(db, "Admin", "selftest");
            var cfgRepo = new ConfigRepository(db);
            int origFlag = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            UserSession savedAppSession = AppSession.Current;
            int preExistingLogs = 0;
            using (FbConnection conn = KazukoConnection.Open(db))
                preExistingLogs = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TLUUVET"));

            string tableId = DbHelper.NewId();
            string billDoneId = DbHelper.NewId();
            string billInUseId = DbHelper.NewId();
            string billOutId = DbHelper.NewId();
            string billStockId = DbHelper.NewId();
            string billCancelId = DbHelper.NewId();
            string billCancelRowId = DbHelper.NewId();
            string billCancelOutId = DbHelper.NewId();
            string billCancelOutRowId = DbHelper.NewId();
            var seededLogIds = new List<string>();

            try
            {
                // Bật cờ KichHoatLuuVetHoatDong = 30.
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 } }, "selftest");
                SystemConfig.Reload(db);

                BookingExec(db,
                    "INSERT INTO DBAN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')",
                    "@I", tableId, "@N", "Bàn ST-LV 01");

                // 3 bill bán (LOAI=0) + 1 phiếu nhập (LOAI=1) + 2 bill hủy (TDONHANGHUY).
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DBANID, USERCREATEDID) " +
                    "VALUES (@I, @N, 0, 30, 30, @D, @B, 'selftest')",
                    "@I", billDoneId, "@N", "LV-HD-01", "@D", new DateTime(2002, 5, 10, 10, 0, 0), "@B", tableId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DBANID, USERCREATEDID) " +
                    "VALUES (@I, @N, 0, 30, 0, @D, @B, 'selftest')",
                    "@I", billInUseId, "@N", "LV-HD-02", "@D", new DateTime(2002, 5, 10, 23, 30, 0), "@B", tableId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DBANID, USERCREATEDID) " +
                    "VALUES (@I, @N, 0, 30, 30, @D, @B, 'selftest')",
                    "@I", billOutId, "@N", "LV-OUT'03", "@D", new DateTime(2002, 4, 1, 9, 0, 0), "@B", tableId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DBANID, USERCREATEDID) " +
                    "VALUES (@I, @N, 1, 30, 30, @D, @B, 'selftest')",
                    "@I", billStockId, "@N", "LV-PN-04", "@D", new DateTime(2002, 5, 10, 11, 0, 0), "@B", tableId);

                BookingExec(db,
                    "INSERT INTO TDONHANGHUY (ID, NAME, STATUS, USERCREATEDID, NGAY, TDONHANGID, BAN, LYDOHUY) " +
                    "VALUES (@I, @N, 30, 'selftest', @D, @H, @B, @L)",
                    "@I", billCancelRowId, "@N", "LV-HUY-01", "@D", new DateTime(2002, 5, 10, 15, 0, 0),
                    "@H", billCancelId, "@B", "Bàn Chụp Hủy", "@L", "Khách đổi ý");
                BookingExec(db,
                    "INSERT INTO TDONHANGHUY (ID, NAME, STATUS, USERCREATEDID, NGAY, TDONHANGID, BAN, LYDOHUY) " +
                    "VALUES (@I, @N, 30, 'selftest', @D, @H, @B, @L)",
                    "@I", billCancelOutRowId, "@N", "LV-HUY'OUT", "@D", new DateTime(2002, 4, 2, 12, 0, 0),
                    "@H", billCancelOutId, "@B", "Bàn Hủy Ngoài", "@L", "Hủy ngoài khoảng");

                // Kiểm nạp danh sách hóa đơn theo ngày (trọn ngày Đến, 2 nhánh, sắp theo NAME, loại LOAI != 0).
                List<ActivityBillItem> dayBills = logger.LoadBills(new DateTime(2002, 5, 10), new DateTime(2002, 5, 10), "");
                List<ActivityBillItem> seeded = dayBills.FindAll(b => b.Number != null && b.Number.StartsWith("LV-", StringComparison.Ordinal));
                Check("Lưu vết: lưới trái đủ 2 nhánh (kết thúc/đang dùng/hủy), trọn ngày Đến, sắp theo tên, loại LOAI!=0",
                    seeded.Count == 3
                    && seeded[0].Number == "LV-HD-01" && seeded[0].Kind == ActivityBillKind.Completed && seeded[0].StatusText == "Kết thúc" && seeded[0].TableName == "Bàn ST-LV 01"
                    && seeded[1].Number == "LV-HD-02" && seeded[1].Kind == ActivityBillKind.InUse && seeded[1].StatusText == "Đang sử dụng"
                    && seeded[2].Number == "LV-HUY-01" && seeded[2].Kind == ActivityBillKind.Cancelled && seeded[2].StatusText == "Khách đổi ý" && seeded[2].TableName == "Bàn Chụp Hủy",
                    "n=" + seeded.Count);

                // Nhập Số HĐ có dấu nháy đơn ngoài khoảng ngày (cả bill thường lẫn bill hủy, P1).
                List<ActivityBillItem> outNormal = logger.LoadBills(new DateTime(2002, 5, 10), new DateTime(2002, 5, 10), "LV-OUT'03");
                List<ActivityBillItem> outCancel = logger.LoadBills(new DateTime(2002, 5, 10), new DateTime(2002, 5, 10), "LV-HUY'OUT");
                Check("Lưu vết: nhập Số HĐ có dấu ' hiện bill ngoài khoảng ngày (thường & hủy, không lỗi SQL)",
                    outNormal.Exists(b => b.Id == billOutId && b.Number == "LV-OUT'03")
                    && outCancel.Exists(b => b.Id == billCancelOutId && b.Number == "LV-HUY'OUT" && b.Kind == ActivityBillKind.Cancelled));

                // Ghi đơn qua ActivityLogger.Track (ban rỗng tự đọc tên bàn của bill; NGAY = ngày giờ DB; THIETBI = MachineName).
                bool trackedSingle = logger.Write(ActivityConstants.FuncSuDungDichVu, ActivityKind.MoBan, billDoneId, "", "Mở bàn từ selftest");
                Check("Lưu vết: Track ghi đơn khi cờ = 30", trackedSingle);

                // Ghi nhiều dòng trong một transaction (kèm cắt TAIKHOAN/CHUCNANG > 36 ký tự, số tiền decimal).
                string longAccount = "tai_khoan_rat_dai_hon_ba_muoi_sau_ky_tu_12345";
                string longFunc = "Chức năng có tên rất dài vượt quá 36 ký tự để kiểm cắt";
                DateTime t1 = new DateTime(2002, 5, 10, 10, 5, 1);
                DateTime t2 = new DateTime(2002, 5, 10, 10, 5, 2);
                DateTime t3 = new DateTime(2002, 5, 10, 10, 5, 3);
                int batchCount = logger.WriteBatch(new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = longFunc, Kind = ActivityKind.ThemMon, BillId = billDoneId,
                        TableName = "Bàn ST-LV 01", Note = "Thêm Cà phê sữa", ItemName = "Cà phê sữa",
                        Quantity = 2m, UnitPrice = 25000m, Amount = 50000m, Time = t1, Account = longAccount
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncQuanLyBanHang, Kind = ActivityKind.GopBan, BillId = billDoneId,
                        TableName = "Bàn ST-LV 01", Note = "Gộp bàn thử", Time = t2, Account = "thungan_st"
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncDieuChinhHoaDon, Kind = ActivityKind.XoaMon, BillId = billDoneId,
                        TableName = "Bàn ST-LV 01", Note = "Xóa món thử", Time = t3, Account = "thungan_st"
                    }
                });
                Check("Lưu vết: WriteBatch ghi nhiều dòng trong một transaction", batchCount == 3, "n=" + batchCount);

                // Ghi lưu vết cho bill đã hủy (kiểm tra COALESCE tên số phiếu & tên bàn chụp).
                logger.Write(ActivityConstants.FuncSuDungDichVu, ActivityKind.HuyBill, billCancelId, "", "Hủy bill thử");

                // Chèn 1 dòng có PHANLOAI ngoài 0–16 (99) để kiểm tra đọc không lỗi.
                string weirdId = DbHelper.NewId();
                seededLogIds.Add(weirdId);
                BookingExec(db,
                    "INSERT INTO TLUUVET (ID, STATUS, USERCREATEDID, TIMECREATED, NGAY, GIO, SODONHANG, TAIKHOAN, THIETBI, PHANLOAI, BAN, CHUCNANG, NOTE, TENHANG, SOLUONG, DONGIA, THANHTIEN) " +
                    "VALUES (@I, 30, 'selftest', @T, @D, @T, @S, 'Admin', @M, 99, 'Bàn ST-LV 01', 'Sử dụng dịch vụ', 'Phân loại lạ 99', '', 0, 0, 0)",
                    "@I", weirdId, "@T", new DateTime(2002, 5, 10, 10, 5, 4), "@D", new DateTime(2002, 5, 10),
                    "@S", billDoneId, "@M", Environment.MachineName);

                // Kiểm đọc lưu vết: chưa chọn bill → trống.
                Check("Lưu vết: chưa chọn hóa đơn → lưới phải trống", logger.LoadLogs("", "").Count == 0);

                // Đọc lưu vết của billDoneId: đúng số dòng, sắp tăng dần theo ngày/giờ, đúng tên số phiếu, tên bàn, thiết bị, cắt 36 ký tự.
                List<ActivityLogEntry> logs = logger.LoadLogs(billDoneId, "");
                foreach (ActivityLogEntry l in logs)
                    seededLogIds.Add(l.Id);
                bool sortedAsc = true;
                for (int i = 1; i < logs.Count; i++)
                {
                    if (logs[i].Date < logs[i - 1].Date || (logs[i].Date == logs[i - 1].Date && logs[i].Time < logs[i - 1].Time))
                        sortedAsc = false;
                }
                ActivityLogEntry moBanRow = logs.Find(l => l.KindValue == (int)ActivityKind.MoBan);
                ActivityLogEntry themMonRow = logs.Find(l => l.KindValue == (int)ActivityKind.ThemMon);
                ActivityLogEntry weirdRow = logs.Find(l => l.KindValue == 99);
                Check("Lưu vết: đọc đủ dòng của bill, sắp tăng dần theo ngày/giờ, hiện đúng số phiếu",
                    logs.Count == 5 && sortedAsc && logs.TrueForAll(l => l.BillId == billDoneId && l.BillNumber == "LV-HD-01"),
                    "n=" + logs.Count);
                Check("Lưu vết: Track ghi đúng NGAY=ngày giờ DB, BAN tự đọc từ bill, TAIKHOAN, THIETBI",
                    moBanRow != null && moBanRow.Date == moBanRow.Time.Date && moBanRow.TableName == "Bàn ST-LV 01"
                    && moBanRow.Account == "Admin" && moBanRow.Device == Environment.MachineName);
                Check("Lưu vết: cắt ngắn TAIKHOAN/CHUCNANG <= 36 ký tự và giữ đúng decimal tiền/SL",
                    themMonRow != null && themMonRow.Account.Length == 36 && themMonRow.FunctionName.Length == 36
                    && themMonRow.Quantity == 2m && themMonRow.UnitPrice == 25000m && themMonRow.Amount == 50000m);
                Check("Lưu vết: dòng PHANLOAI = 99 đọc ra tên Khác, không lỗi",
                    weirdRow != null && weirdRow.KindDisplayName == "Khác" && weirdRow.Tone == ActivityRowTone.Default);

                // Lọc theo tài khoản.
                List<ActivityLogEntry> byStaff = logger.LoadLogs(billDoneId, "thungan_st");
                Check("Lưu vết: lọc theo tài khoản chỉ lấy thao tác của tài khoản đó",
                    byStaff.Count == 2 && byStaff.TrueForAll(l => l.Account == "thungan_st"), "n=" + byStaff.Count);

                // Lọc chuỗi phía lưới (không dấu / hoa thường / tên phân loại).
                Check("Lưu vết: ô lọc dữ liệu khớp nội dung và tên phân loại",
                    ActivityLogger.MatchesFilter(themMonRow, "ca phe")
                    && ActivityLogger.MatchesFilter(themMonRow, "them mon")
                    && !ActivityLogger.MatchesFilter(themMonRow, "khong-ton-tai"));

                // Lưu vết bill đã hủy vẫn hiện đúng số phiếu và tên bàn chụp.
                List<ActivityLogEntry> cancelLogs = logger.LoadLogs(billCancelId, "");
                foreach (ActivityLogEntry l in cancelLogs)
                    seededLogIds.Add(l.Id);
                Check("Lưu vết: bill đã hủy hiện đúng số phiếu (COALESCE TDONHANGHUY.NAME) và tên bàn chụp",
                    cancelLogs.Count == 1 && cancelLogs[0].BillNumber == "LV-HUY-01"
                    && cancelLogs[0].TableName == "Bàn Chụp Hủy" && cancelLogs[0].KindValue == (int)ActivityKind.HuyBill);

                // Tắt cờ KichHoatLuuVetHoatDong = 0 → không ghi lưu vết và UI hiện cảnh báo 2 dòng.
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 0 } }, "selftest");
                SystemConfig.Reload(db);
                bool writtenWhenOff = logger.Write(ActivityConstants.FuncSuDungDichVu, ActivityKind.InBill, billDoneId, "Bàn ST-LV 01", "Không được ghi");
                Check("Lưu vết: cờ KichHoatLuuVetHoatDong = 0 → không ghi vào DB",
                    !writtenWhenOff && logger.LoadLogs(billDoneId, "").Count == 5);

                using (var viewOff = new ActivityLogView(adminSession, false) { SuppressWarningPopup = true })
                {
                    Check("Lưu vết UI: cờ = 0 vẫn mở được và hiện đúng cảnh báo 2 dòng nguyên văn",
                        viewOff.WarningBannerVisible && viewOff.WarningBannerText == ActivityConstants.DisabledWarningText);
                }

                // Bật lại cờ = 30 và kiểm UI: đúng 9 cột theo thứ tự §1, ngày mặc định DB, nút Xóa ẩn/hiện đúng điều kiện.
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 } }, "selftest");
                SystemConfig.Reload(db);

                // Ca kiểm hàm tĩnh ActivityLogger.Track (lấy phiên AppSession, ghi TAIKHOAN = tên đăng nhập, GIO lệch giờ DB <= 1 phút).
                DateTime dbServerNow;
                using (FbConnection conn = KazukoConnection.Open(db))
                    dbServerNow = Convert.ToDateTime(DbHelper.Scalar(conn, null, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE"));
                AppSession.Current = new UserSession
                {
                    UserId = "uid_st_track",
                    UserName = "user_track_st",
                    DisplayName = "Tên Hiển Thị Khác",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };
                bool staticTracked = ActivityLogger.Track(
                    ActivityConstants.FuncSuDungDichVu,
                    ActivityKind.InBill,
                    billInUseId,
                    "",
                    "Ghi qua hàm tĩnh ActivityLogger.Track");
                AppSession.Current = savedAppSession;
                List<ActivityLogEntry> staticLogs = logger.LoadLogs(billInUseId, "");
                foreach (ActivityLogEntry l in staticLogs)
                    seededLogIds.Add(l.Id);
                ActivityLogEntry staticRow = staticLogs.Find(l => l.KindValue == (int)ActivityKind.InBill);
                double diffMinutes = staticRow == null ? double.MaxValue : Math.Abs((staticRow.Time - dbServerNow).TotalMinutes);
                Check("Lưu vết (§7 ActivityLogger.Track tĩnh): lấy phiên AppSession, ghi đúng TAIKHOAN = tên đăng nhập và GIO lệch giờ DB <= 1 phút",
                    staticTracked && staticRow != null
                    && staticRow.Account == "user_track_st"
                    && staticRow.CreatedById == "uid_st_track"
                    && staticRow.Date == staticRow.Time.Date
                    && staticRow.Device == Environment.MachineName
                    && diffMinutes <= 1.0,
                    staticRow == null ? "null" : ("tk=" + staticRow.Account + ", lech=" + diffMinutes.ToString("0.00") + "p"));

                // Seed đủ các loại phân loại trên billInUseId để kiểm màu/font thật và độ rộng cột in đậm trên ActivityLogView.
                logger.WriteBatch(new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu, Kind = ActivityKind.MoBan, BillId = billInUseId,
                        TableName = "Bàn ST-LV 01", Note = "Mở bàn kiểm màu", Time = new DateTime(2002, 5, 10, 11, 0, 1), Account = "Admin"
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncQuanLyBanHang, Kind = ActivityKind.XoaMon, BillId = billInUseId,
                        TableName = "Bàn ST-LV 01", Note = "Xóa món kiểm màu", Time = new DateTime(2002, 5, 10, 11, 0, 2), Account = "Admin"
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncDieuChinhHoaDon, Kind = ActivityKind.ChuyenBan, BillId = billInUseId,
                        TableName = "Bàn ST-LV 01", Note = "Chuyển bàn kiểm màu", Time = new DateTime(2002, 5, 10, 11, 0, 3), Account = "Admin"
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncDieuChinhHoaDon, Kind = ActivityKind.GopBan, BillId = billInUseId,
                        TableName = "Bàn ST-LV 01", Note = "Gộp bàn kiểm màu đậm", Time = new DateTime(2002, 5, 10, 11, 0, 4), Account = "Admin"
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu, Kind = ActivityKind.HuyBill, BillId = billInUseId,
                        TableName = "Bàn ST-LV 01", Note = "Hủy bill kiểm màu đậm và độ rộng cột", Time = new DateTime(2002, 5, 10, 11, 0, 5), Account = "Admin"
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncDieuChinhHoaDon, Kind = ActivityKind.DatGiamGiaMatHang, BillId = billInUseId,
                        TableName = "Bàn ST-LV 01", Note = "Giảm giá kiểm độ rộng phân loại", Time = new DateTime(2002, 5, 10, 11, 0, 6), Account = "Admin"
                    }
                });

                string[] expectedHeaders =
                {
                    "Ngày", "Giờ", "Số đơn hàng", "Nội dung", "Tài khoản", "Thiết bị", "Bàn", "Chức năng", "Phân loại"
                };
                using (var viewAdminNormal = new ActivityLogView(adminSession, false) { SuppressWarningPopup = true })
                {
                    Check("Lưu vết UI (§7 mở màn): Từ = Đến = ngày máy chủ DB",
                        viewAdminNormal.FromDateValue.Date == dbServerNow.Date && viewAdminNormal.ToDateValue.Date == dbServerNow.Date,
                        "tu=" + viewAdminNormal.FromDateValue.ToString("yyyy-MM-dd") + ", den=" + viewAdminNormal.ToDateValue.ToString("yyyy-MM-dd") + ", db=" + dbServerNow.Date.ToString("yyyy-MM-dd"));

                    List<string> cols = viewAdminNormal.LogColumnHeaders();
                    bool headersMatch = cols.Count == expectedHeaders.Length;
                    for (int i = 0; headersMatch && i < expectedHeaders.Length; i++)
                        if (cols[i] != expectedHeaders[i])
                            headersMatch = false;
                    Check("Lưu vết UI: lưới phải đúng 9 cột theo thứ tự §1 (không có Tên hàng/SL/Đơn giá/Thành tiền)",
                        headersMatch, string.Join(" | ", cols.ToArray()));
                    Check("Lưu vết UI: nút Xóa lưu vết ẩn mặc định khi không giữ Ctrl+Shift",
                        !viewAdminNormal.DeleteButtonVisible && !viewAdminNormal.WarningBannerVisible);
                    Check("Lưu vết UI: chưa chọn hóa đơn → lưới phải trống", viewAdminNormal.LogRowCount == 0);

                    // Kiểm màu thật của ô lưới trái & phải (CellFormatting) và độ rộng cột dòng in đậm.
                    viewAdminNormal.SetDateRangeForTest(new DateTime(2002, 5, 10), new DateTime(2002, 5, 10));
                    Theme.ThemeTokens theme = Theme.ThemeManager.Current;

                    int leftCancelIdx = viewAdminNormal.FindBillRowIndex(billCancelId);
                    int leftInUseIdx = viewAdminNormal.FindBillRowIndex(billInUseId);
                    int leftDoneIdx = viewAdminNormal.FindBillRowIndex(billDoneId);
                    System.Windows.Forms.DataGridViewCellStyle leftCancelStyle = viewAdminNormal.GetBillCellStyle(leftCancelIdx, 0);
                    System.Windows.Forms.DataGridViewCellStyle leftInUseStyle = viewAdminNormal.GetBillCellStyle(leftInUseIdx, 0);
                    System.Windows.Forms.DataGridViewCellStyle leftDoneStyle = viewAdminNormal.GetBillCellStyle(leftDoneIdx, 0);
                    Check("Lưu vết UI (§7 màu thật lưới trái): bill hủy = Danger, bill đang dùng = Info, bill kết thúc = Text",
                        leftCancelStyle != null && leftCancelStyle.ForeColor == theme.Danger
                        && leftInUseStyle != null && leftInUseStyle.ForeColor == theme.Info
                        && leftDoneStyle != null && leftDoneStyle.ForeColor == theme.Text,
                        "cancel=" + (leftCancelStyle == null ? "null" : leftCancelStyle.ForeColor.ToString())
                        + ", inUse=" + (leftInUseStyle == null ? "null" : leftInUseStyle.ForeColor.ToString()));

                    bool billSelected = viewAdminNormal.SelectBillById(billInUseId);
                    int rowHuy = viewAdminNormal.FindLogRowIndex(ActivityKind.HuyBill);
                    int rowGop = viewAdminNormal.FindLogRowIndex(ActivityKind.GopBan);
                    int rowChuyen = viewAdminNormal.FindLogRowIndex(ActivityKind.ChuyenBan);
                    int rowXoa = viewAdminNormal.FindLogRowIndex(ActivityKind.XoaMon);
                    int rowMo = viewAdminNormal.FindLogRowIndex(ActivityKind.MoBan);
                    System.Windows.Forms.DataGridViewCellStyle stHuy = viewAdminNormal.GetLogCellStyle(rowHuy, 0);
                    System.Windows.Forms.DataGridViewCellStyle stGop = viewAdminNormal.GetLogCellStyle(rowGop, 0);
                    System.Windows.Forms.DataGridViewCellStyle stChuyen = viewAdminNormal.GetLogCellStyle(rowChuyen, 0);
                    System.Windows.Forms.DataGridViewCellStyle stXoa = viewAdminNormal.GetLogCellStyle(rowXoa, 0);
                    System.Windows.Forms.DataGridViewCellStyle stMo = viewAdminNormal.GetLogCellStyle(rowMo, 0);
                    Check("Lưu vết UI (§7 màu & font thật lưới phải): Hủy bill=Danger+đậm, Gộp bàn=Success+đậm, Chuyển bàn=Info, Xóa món=Danger, Mở bàn=Text thường",
                        billSelected
                        && stHuy != null && stHuy.ForeColor == theme.Danger && stHuy.Font != null && stHuy.Font.Bold
                        && stGop != null && stGop.ForeColor == theme.Success && stGop.Font != null && stGop.Font.Bold
                        && stChuyen != null && stChuyen.ForeColor == theme.Info && stChuyen.Font != null && !stChuyen.Font.Bold
                        && stXoa != null && stXoa.ForeColor == theme.Danger && stXoa.Font != null && !stXoa.Font.Bold
                        && stMo != null && stMo.ForeColor == theme.Text && stMo.Font != null && !stMo.Font.Bold,
                        "selected=" + billSelected + ", huy=" + (stHuy == null ? "null" : stHuy.ForeColor + "/" + (stHuy.Font != null && stHuy.Font.Bold)));

                    // Kiểm độ rộng 8 cột cố định lưới phải >= độ rộng chữ đo bằng TextRenderer.MeasureText với font đậm của lưới (dòng Hủy bill + chuỗi thường gặp).
                    System.Drawing.Font boldFont = viewAdminNormal.BoldGridFont;
                    var widthChecks = new[]
                    {
                        new KeyValuePair<string, string[]>("colNgay", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colNgay"), "25/11/2026" }),
                        new KeyValuePair<string, string[]>("colGio", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colGio"), "23:59:59" }),
                        new KeyValuePair<string, string[]>("colSoDonHang", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colSoDonHang"), "Số đơn hàng" }),
                        new KeyValuePair<string, string[]>("colTaiKhoan", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colTaiKhoan"), "Tài khoản" }),
                        new KeyValuePair<string, string[]>("colThietBi", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colThietBi"), "Thiết bị" }),
                        new KeyValuePair<string, string[]>("colBan", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colBan"), "Bàn" }),
                        new KeyValuePair<string, string[]>("colChucNang", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colChucNang"), "Sử dụng dịch vụ", "Điều chỉnh hóa đơn" }),
                        new KeyValuePair<string, string[]>("colPhanLoai", new[] { viewAdminNormal.GetLogCellText(rowHuy, "colPhanLoai"), "Đặt giảm giá mặt hàng" })
                    };
                    var narrowCols = new List<string>();
                    foreach (KeyValuePair<string, string[]> kv in widthChecks)
                    {
                        int colW = viewAdminNormal.LogColumnWidth(kv.Key);
                        foreach (string sample in kv.Value)
                        {
                            int needW = System.Windows.Forms.TextRenderer.MeasureText(sample ?? "", boldFont).Width;
                            if (colW < needW)
                                narrowCols.Add(kv.Key + "(" + colW + "<" + needW + " '" + sample + "')");
                        }
                    }
                    bool noteIsFill = viewAdminNormal.IsLogColumnFill("colNoiDung");
                    Check("Lưu vết UI (§7 độ rộng cột lưới phải): mọi cột cố định >= độ rộng chữ đậm (Hủy bill & dữ liệu thường gặp), Nội dung co giãn",
                        rowHuy >= 0 && narrowCols.Count == 0 && noteIsFill,
                        narrowCols.Count == 0 ? ("noteFill=" + noteIsFill) : string.Join("; ", narrowCols.ToArray()));
                }
                using (var viewAdminKey = new ActivityLogView(adminSession, true) { SuppressWarningPopup = true })
                {
                    Check("Lưu vết UI: nút Xóa lưu vết hiện khi admin + Ctrl+Shift lúc mở", viewAdminKey.DeleteButtonVisible);
                }
                using (var viewStaffKey = new ActivityLogView(staffSession, true) { SuppressWarningPopup = true })
                {
                    Check("Lưu vết UI: tài khoản không phải admin không thấy nút Xóa dù giữ Ctrl+Shift", !viewStaffKey.DeleteButtonVisible);
                }

                // Kiểm chặn xóa ở lớp Services khi không phải admin hoặc sai mật khẩu.
                bool nonAdminBlocked = false;
                try { logger.DeleteAll(staffSession, "9876"); } catch (UnauthorizedAccessException) { nonAdminBlocked = true; }
                bool badPassBlocked = false;
                try { logger.DeleteAll(adminSession, "0000"); } catch (InvalidOperationException) { badPassBlocked = true; }
                Check("Lưu vết: Services chặn DeleteAll nếu không phải admin hoặc sai mật khẩu", nonAdminBlocked && badPassBlocked);

                // Nếu bảng TLUUVET ban đầu trống (như bản sao DB chuẩn), kiểm luôn DeleteAll xóa toàn bảng.
                if (preExistingLogs == 0)
                {
                    int deleted = logger.DeleteAll(adminSession, "9876");
                    int remaining;
                    using (FbConnection conn = KazukoConnection.Open(db))
                        remaining = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TLUUVET"));
                    Check("Lưu vết: DeleteAll xóa toàn bảng TLUUVET khi admin + đúng 9876", deleted >= 6 && remaining == 0,
                        "deleted=" + deleted + ", remain=" + remaining);
                }
            }
            finally
            {
                // Hoàn lại phiên, cấu hình và dọn toàn bộ dữ liệu seed.
                AppSession.Current = savedAppSession;
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = origFlag } }, "selftest");
                SystemConfig.Reload(db);
                foreach (string id in seededLogIds)
                    BookingExec(db, "DELETE FROM TLUUVET WHERE ID = @I", "@I", id);
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG IN (@A, @B, @C)", "@A", billDoneId, "@B", billCancelId, "@C", billInUseId);
                BookingExec(db, "DELETE FROM TDONHANGHUY WHERE ID IN (@A, @B)", "@A", billCancelRowId, "@B", billCancelOutRowId);
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID IN (@A, @B, @C, @D)", "@A", billDoneId, "@B", billInUseId, "@C", billOutId, "@D", billStockId);
                BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tableId);
            }
        }

        // ------------------------------------------------------------------ Danh mục hóa đơn hủy (docs/specs/danh-muc-hoa-don-huy.md §6)

        private static void CollectAllButtons(System.Windows.Forms.Control root, List<System.Windows.Forms.Control> into)
        {
            if (root == null)
                return;
            if (root is Controls.FlatButton || root is System.Windows.Forms.Button)
                into.Add(root);
            foreach (System.Windows.Forms.Control child in root.Controls)
                CollectAllButtons(child, into);
        }

        private static bool MenuContainsForbiddenWriteAction(System.Windows.Forms.ContextMenuStrip menu)
        {
            if (menu == null || menu.Items.Count == 0)
                return true;
            foreach (System.Windows.Forms.ToolStripItem item in menu.Items)
            {
                string text = (item.Text ?? "").Trim();
                if (text.Length == 0)
                    continue;
                if (text.IndexOf("Thêm", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("Sửa", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("Xóa", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool MenuTreeContainsId(IEnumerable<MenuNode> nodes, string menuId)
        {
            if (nodes == null)
                return false;
            foreach (MenuNode n in nodes)
            {
                if (string.Equals(n.Id, menuId, StringComparison.Ordinal))
                    return true;
                if (MenuTreeContainsId(n.Children, menuId))
                    return true;
            }
            return false;
        }

        private static void CancelledBillTests(string db)
        {
            var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

            // 1. DateBox: chế độ mặc định (AllowEmpty = false) giữ nguyên hành vi cũ; chế độ AllowEmpty = true cho phép trống và không âm thầm đổi chữ sai thành ngày lạ.
            using (var boxDefault = new Controls.DateBox())
            {
                int changedCount = 0;
                boxDefault.ValueChanged += delegate { changedCount++; };
                bool initDefault = !boxDefault.AllowEmpty && boxDefault.Value == DateTime.Today && boxDefault.NullableValue == DateTime.Today;
                boxDefault.Inner.Text = "";
                boxDefault.Commit();
                bool emptyReverts = boxDefault.Value == DateTime.Today && boxDefault.Inner.Text == DateTime.Today.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) && changedCount == 0;
                boxDefault.Inner.Text = "ngay-sai";
                boxDefault.Commit();
                bool invalidReverts = boxDefault.Value == DateTime.Today && changedCount == 0;
                boxDefault.NullableValue = null;
                bool nullIgnored = boxDefault.NullableValue == DateTime.Today && changedCount == 0;
                boxDefault.Inner.Text = "15/06/2026";
                boxDefault.Commit();
                bool validUpdates = boxDefault.Value == new DateTime(2026, 6, 15) && changedCount == 1;
                Check("Hóa đơn hủy (DateBox mặc định AllowEmpty=false): luôn có ngày, xóa/gõ sai giữ ngày cũ, gõ đúng cập nhật",
                    initDefault && emptyReverts && invalidReverts && nullIgnored && validUpdates,
                    "init=" + initDefault + ", emptyRev=" + emptyReverts + ", invRev=" + invalidReverts + ", nullIgn=" + nullIgnored + ", ok=" + validUpdates);
            }

            using (var boxNullable = new Controls.DateBox(true))
            {
                int changedCount = 0;
                boxNullable.ValueChanged += delegate { changedCount++; };
                bool initEmpty = boxNullable.AllowEmpty && boxNullable.NullableValue == null && boxNullable.Inner.Text == "";
                boxNullable.Inner.Text = "abc-99/99";
                boxNullable.Commit();
                bool badFromEmptyStaysEmpty = boxNullable.NullableValue == null && boxNullable.Inner.Text == "" && changedCount == 0;
                boxNullable.Inner.Text = "31/02/2026";
                boxNullable.Commit();
                bool impossibleDateStaysEmpty = boxNullable.NullableValue == null && boxNullable.Inner.Text == "" && changedCount == 0;
                boxNullable.Inner.Text = "10/06/2003";
                boxNullable.Commit();
                bool setValidDate = boxNullable.NullableValue == new DateTime(2003, 6, 10) && boxNullable.Inner.Text == "10/06/2003" && changedCount == 1;
                boxNullable.Inner.Text = "sai-dinh-dang";
                boxNullable.Commit();
                bool badFromDateReverts = boxNullable.NullableValue == new DateTime(2003, 6, 10) && boxNullable.Inner.Text == "10/06/2003" && changedCount == 1;
                boxNullable.Inner.Text = "   ";
                boxNullable.Commit();
                bool clearedToEmpty = boxNullable.NullableValue == null && boxNullable.Inner.Text == "" && changedCount == 2;
                Check("Hóa đơn hủy (DateBox AllowEmpty=true): mở trống, gõ sai không thành ngày lạ, gõ đúng nhận ngày, xóa hết thành trống",
                    initEmpty && badFromEmptyStaysEmpty && impossibleDateStaysEmpty && setValidDate && badFromDateReverts && clearedToEmpty,
                    "init=" + initEmpty + ", badEmpty=" + badFromEmptyStaysEmpty + ", feb31=" + impossibleDateStaysEmpty
                    + ", valid=" + setValidDate + ", badDate=" + badFromDateReverts + ", cleared=" + clearedToEmpty);
            }

            // 2. Route ScreenFactory (không vào Map hộp thoại, đi qua CreateView theo MenuId trước kiểm Operation) & quyền qua MainMenuBuilder (§2.4, §6).
            var adminSession = new UserSession
            {
                UserId = "selftest",
                UserName = "Admin",
                DisplayName = "Administrator",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            Check("Hóa đơn hủy (§6 route): MenuId không nằm trong Map hộp thoại",
                !Dialogs.ScreenFactory.Has(CancelledBillView.MenuId));
            var menuNode = new MenuNode
            {
                Id = CancelledBillView.MenuId,
                Title = CancelledBillConstants.FunctionName,
                Operation = 4,
                StableDescId = null
            };
            using (System.Windows.Forms.Control created = Dialogs.ScreenFactory.CreateView(menuNode, adminSession))
            {
                Check("Hóa đơn hủy (§6 route): ScreenFactory.CreateView dựng CancelledBillView dù Operation=4 và không có StableDescId",
                    created is CancelledBillView);
            }

            // Kiểm tra mapping quyền §2.4 trong DB + lọc menu qua MainMenuBuilder.
            var permsAllowed = new FunctionPermissions(false);
            permsAllowed.Add(CancelledBillConstants.FunctionId, CancelledBillConstants.FunctionName, RoleModes.View);
            var permsDenied = new FunctionPermissions(false);
            MenuModel menuAllowed = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_allow", UserName = "u_allow", DisplayName = "Cho phép", IsAdmin = false,
                DatabasePath = db, Permissions = permsAllowed
            });
            MenuModel menuDenied = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_deny", UserName = "u_deny", DisplayName = "Chặn", IsAdmin = false,
                DatabasePath = db, Permissions = permsDenied
            });
            Check("Hóa đơn hủy (§2.4/§6 phân quyền): MainMenuBuilder hiện menu khi có quyền SFUNCTION và ẩn khi không có quyền",
                MenuTreeContainsId(menuAllowed.Roots, CancelledBillView.MenuId)
                && !MenuTreeContainsId(menuDenied.Roots, CancelledBillView.MenuId));

            // 3. Seed 4 bản hóa đơn hủy (tiền tố HDH-, năm 2003) kèm chi tiết vào bản sao DB; thêm HDH-05 sau khi mở màn để kiểm Refresh thật; dọn con trước cha trong finally.
            string bill1Id = DbHelper.NewId();
            string bill2Id = DbHelper.NewId();
            string bill3Id = DbHelper.NewId();
            string bill4Id = DbHelper.NewId();
            string bill5Id = DbHelper.NewId();

            string line1aId = DbHelper.NewId();
            string line1bId = DbHelper.NewId();
            string line1cId = DbHelper.NewId();
            string line2aId = DbHelper.NewId();
            string line4aId = DbHelper.NewId();
            string line4bId = DbHelper.NewId();
            string line5aId = DbHelper.NewId();

            try
            {
                // HDH-01: NGAY = 08/06/2003 (khác NGAYHUY = 10/06/2003 09:15), DATHANHTOAN = 30, TIENGIAMGIA = TILEGIAMGIA = 15 (Q3), STATUS = 30.
                BookingExec(db,
                    "INSERT INTO TDONHANGHUY (" +
                    "ID, NAME, STATUS, USERCREATEDID, NOTE, NGAY, KHACHHANG, NHANVEN, THUNGAN, " +
                    "GIOHUY, NGAYHUY, DOITRA, DATHANHTOAN, GIOTHANHTOAN, TRALAI, " +
                    "TIENHANG, TILETHUE, TIENTHUE, TILEGIAMGIA, TIENGIAMGIA, PHIVANCHUYEN, " +
                    "THANHTOANBOI, LYDOHUY, TIENGIO, PHIDICHVU, TDONHANGID, BAN) VALUES (" +
                    "@ID, @NAME, 30, 'selftest', @NOTE, @NGAY, @KH, @NV, @TN, " +
                    "@GIOHUY, @NGAYHUY, @DOITRA, 30, @GIOTT, @TRALAI, " +
                    "@TIENHANG, @TLTHUE, @TTHUE, @TLGG, @TGG, @PVC, " +
                    "@TTBOI, @LYDO, @TGIO, @PDV, @ORIG, @BAN)",
                    "@ID", bill1Id, "@NAME", "HDH-01", "@NOTE", "Ghi chú bill 1",
                    "@NGAY", new DateTime(2003, 6, 8, 0, 0, 0),
                    "@KH", "Nguyễn Thị Lan", "@NV", "Trần Văn B", "@TN", "Thu ngân 1",
                    "@GIOHUY", new DateTime(2003, 6, 10, 9, 15, 0),
                    "@NGAYHUY", new DateTime(2003, 6, 10, 9, 15, 0),
                    "@DOITRA", 5000m,
                    "@GIOTT", new DateTime(2003, 6, 8, 18, 45, 0),
                    "@TRALAI", 10000m, "@TIENHANG", 125000m,
                    "@TLTHUE", 10m, "@TTHUE", 12500m,
                    "@TLGG", 15m, "@TGG", 15m,
                    "@PVC", 20000m, "@TTBOI", "Thu ngân 2",
                    "@LYDO", "Khách đổi ý", "@TGIO", 30000m, "@PDV", 15000m,
                    "@ORIG", DbHelper.NewId(), "@BAN", "Bàn 05");

                // HDH-02: STATUS = 0 (kiểm hiện mọi STATUS), NGAYHUY giữa ngày Đến (10/06/2003 16:30), DATHANHTOAN = 0, nhiều cột NULL.
                BookingExec(db,
                    "INSERT INTO TDONHANGHUY (" +
                    "ID, NAME, STATUS, USERCREATEDID, NOTE, NGAY, KHACHHANG, NHANVEN, THUNGAN, " +
                    "GIOHUY, NGAYHUY, DOITRA, DATHANHTOAN, GIOTHANHTOAN, TRALAI, " +
                    "TIENHANG, TILETHUE, TIENTHUE, TILEGIAMGIA, TIENGIAMGIA, PHIVANCHUYEN, " +
                    "THANHTOANBOI, LYDOHUY, TIENGIO, PHIDICHVU, TDONHANGID, BAN) VALUES (" +
                    "@ID, @NAME, 0, 'selftest', NULL, @NGAY, NULL, NULL, @TN, " +
                    "@GIOHUY, @NGAYHUY, NULL, 0, NULL, NULL, " +
                    "@TIENHANG, NULL, NULL, NULL, NULL, NULL, " +
                    "NULL, @LYDO, NULL, NULL, @ORIG, NULL)",
                    "@ID", bill2Id, "@NAME", "HDH-02",
                    "@NGAY", new DateTime(2003, 6, 10, 0, 0, 0),
                    "@TN", "Thu ngân 2",
                    "@GIOHUY", new DateTime(2003, 6, 10, 16, 30, 0),
                    "@NGAYHUY", new DateTime(2003, 6, 10, 16, 30, 0),
                    "@TIENHANG", 120000m, "@LYDO", "Nhập nhầm món",
                    "@ORIG", DbHelper.NewId());

                // HDH-03: NGAY = 10/06/2003 nhưng NGAYHUY = 11/06/2003 00:00 (00:00 hôm sau → không tính khi Đến = 10/06, chứng minh lọc đúng NGAYHUY).
                // DATHANHTOAN = NULL (ô đánh dấu tắt), TILEGIAMGIA = 12.5, TIENGIAMGIA = 25000, 0 dòng chi tiết.
                BookingExec(db,
                    "INSERT INTO TDONHANGHUY (" +
                    "ID, NAME, STATUS, USERCREATEDID, NOTE, NGAY, KHACHHANG, NHANVEN, THUNGAN, " +
                    "GIOHUY, NGAYHUY, DOITRA, DATHANHTOAN, GIOTHANHTOAN, TRALAI, " +
                    "TIENHANG, TILETHUE, TIENTHUE, TILEGIAMGIA, TIENGIAMGIA, PHIVANCHUYEN, " +
                    "THANHTOANBOI, LYDOHUY, TIENGIO, PHIDICHVU, TDONHANGID, BAN) VALUES (" +
                    "@ID, @NAME, 30, 'selftest', @NOTE, @NGAY, @KH, @NV, @TN, " +
                    "@GIOHUY, @NGAYHUY, 0, NULL, NULL, 0, " +
                    "@TIENHANG, 0, 0, @TLGG, @TGG, 0, " +
                    "@TTBOI, @LYDO, 0, 0, @ORIG, @BAN)",
                    "@ID", bill3Id, "@NAME", "HDH-03", "@NOTE", "Hủy 00:00 hôm sau",
                    "@NGAY", new DateTime(2003, 6, 10, 0, 0, 0),
                    "@KH", "Khách vãng lai", "@NV", "NV 03", "@TN", "Thu ngân 3",
                    "@GIOHUY", new DateTime(2003, 6, 11, 0, 0, 0),
                    "@NGAYHUY", new DateTime(2003, 6, 11, 0, 0, 0),
                    "@TIENHANG", 200000m, "@TLGG", 12.5m, "@TGG", 25000m,
                    "@TTBOI", "", "@LYDO", "Hủy đầu ngày hôm sau",
                    "@ORIG", DbHelper.NewId(), "@BAN", "Bàn 09");

                // HDH-04: NGAYHUY = 09/06/2003 23:59 (sớm nhất → đứng đầu khi mở màn, có 2 dòng chi tiết để chứng minh mở màn tự nạp chi tiết dòng đầu).
                BookingExec(db,
                    "INSERT INTO TDONHANGHUY (" +
                    "ID, NAME, STATUS, USERCREATEDID, NOTE, NGAY, KHACHHANG, NHANVEN, THUNGAN, " +
                    "GIOHUY, NGAYHUY, DOITRA, DATHANHTOAN, GIOTHANHTOAN, TRALAI, " +
                    "TIENHANG, TILETHUE, TIENTHUE, TILEGIAMGIA, TIENGIAMGIA, PHIVANCHUYEN, " +
                    "THANHTOANBOI, LYDOHUY, TIENGIO, PHIDICHVU, TDONHANGID, BAN) VALUES (" +
                    "@ID, @NAME, 30, 'selftest', @NOTE, @NGAY, @KH, @NV, @TN, " +
                    "@GIOHUY, @NGAYHUY, 0, 30, @GIOTT, 0, " +
                    "@TIENHANG, 0, 0, 0, 0, 0, " +
                    "@TTBOI, @LYDO, 0, 0, @ORIG, @BAN)",
                    "@ID", bill4Id, "@NAME", "HDH-04", "@NOTE", "Hủy hôm trước",
                    "@NGAY", new DateTime(2003, 6, 9, 0, 0, 0),
                    "@KH", "Khách cũ", "@NV", "NV 04", "@TN", "Thu ngân 4",
                    "@GIOHUY", new DateTime(2003, 6, 9, 23, 59, 0),
                    "@NGAYHUY", new DateTime(2003, 6, 9, 23, 59, 0),
                    "@GIOTT", new DateTime(2003, 6, 9, 22, 10, 0),
                    "@TIENHANG", 50000m, "@TTBOI", "Thu ngân 4", "@LYDO", "Hủy hôm trước",
                    "@ORIG", DbHelper.NewId(), "@BAN", "Bàn 01");

                // Chi tiết cho HDH-01 (3 dòng, TIMECREATED tăng dần).
                BookingExec(db,
                    "INSERT INTO TDONHANGHUYCHITIET (ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) " +
                    "VALUES (@ID, 30, 'selftest', @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                    "@ID", line1aId, "@TC", new DateTime(2003, 6, 10, 9, 15, 1), "@PID", bill1Id,
                    "@MA", "CF-01", "@TEN", "Cà phê sữa đá", "@DVT", "Ly", "@SL", 2m, "@DG", 25000m, "@TT", 50000m, "@NOTE", "Ít đường");
                BookingExec(db,
                    "INSERT INTO TDONHANGHUYCHITIET (ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) " +
                    "VALUES (@ID, 30, 'selftest', @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                    "@ID", line1bId, "@TC", new DateTime(2003, 6, 10, 9, 15, 2), "@PID", bill1Id,
                    "@MA", "TR-02", "@TEN", "Trà đào cam sả", "@DVT", "Ly", "@SL", 1.5m, "@DG", 35000m, "@TT", 52500m, "@NOTE", "Nhiều đá");
                BookingExec(db,
                    "INSERT INTO TDONHANGHUYCHITIET (ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) " +
                    "VALUES (@ID, 30, 'selftest', @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                    "@ID", line1cId, "@TC", new DateTime(2003, 6, 10, 9, 15, 3), "@PID", bill1Id,
                    "@MA", "DX-03", "@TEN", "Đá xay Matcha", "@DVT", "Ly", "@SL", 1m, "@DG", 22500m, "@TT", 22500m, "@NOTE", "Ít ngọt");

                // Chi tiết cho HDH-02 (1 dòng) và HDH-04 (2 dòng).
                BookingExec(db,
                    "INSERT INTO TDONHANGHUYCHITIET (ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) " +
                    "VALUES (@ID, 30, 'selftest', @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                    "@ID", line2aId, "@TC", new DateTime(2003, 6, 10, 16, 30, 1), "@PID", bill2Id,
                    "@MA", "BN-01", "@TEN", "Bánh ngọt socola", "@DVT", "Cái", "@SL", 3m, "@DG", 40000m, "@TT", 120000m, "@NOTE", "Ghi chú bánh");
                BookingExec(db,
                    "INSERT INTO TDONHANGHUYCHITIET (ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) " +
                    "VALUES (@ID, 30, 'selftest', @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                    "@ID", line4aId, "@TC", new DateTime(2003, 6, 9, 23, 59, 1), "@PID", bill4Id,
                    "@MA", "TS-01", "@TEN", "Trà sữa trân châu", "@DVT", "Ly", "@SL", 1m, "@DG", 30000m, "@TT", 30000m, "@NOTE", "Ít đá");
                BookingExec(db,
                    "INSERT INTO TDONHANGHUYCHITIET (ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) " +
                    "VALUES (@ID, 30, 'selftest', @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                    "@ID", line4bId, "@TC", new DateTime(2003, 6, 9, 23, 59, 2), "@PID", bill4Id,
                    "@MA", "NC-01", "@TEN", "Nước cam ép", "@DVT", "Ly", "@SL", 1m, "@DG", 20000m, "@TT", 20000m, "@NOTE", "Không đường");

                // 4. Repository: kiểm biên lọc ngày trên NGAYHUY, từng đầu trống, mọi STATUS, thứ tự sắp xếp, tham số hóa (P4).
                var repo = new CancelledBillRepository(db);
                Func<List<CancelledBillRow>, List<CancelledBillRow>> onlySeeded = rows =>
                    rows.FindAll(r => r.Name != null && r.Name.StartsWith("HDH-", StringComparison.Ordinal));

                List<CancelledBillRow> allSeeded = onlySeeded(repo.LoadBills(null, null));
                Check("Hóa đơn hủy (§6/Q16): hai đầu trống tải đủ mọi bản HDH (kể cả STATUS=0), sắp tăng NGAYHUY, GIOHUY, NAME",
                    allSeeded.Count == 4
                    && allSeeded[0].Name == "HDH-04"
                    && allSeeded[1].Name == "HDH-01"
                    && allSeeded[2].Name == "HDH-02" && allSeeded[2].Status == 0
                    && allSeeded[3].Name == "HDH-03",
                    "n=" + allSeeded.Count);

                List<CancelledBillRow> day10 = onlySeeded(repo.LoadBills(new DateTime(2003, 6, 10), new DateTime(2003, 6, 10)));
                Check("Hóa đơn hủy (§6/Q10 biên lọc NGAYHUY): tính giữa ngày Đến (16:30), loại 00:00 hôm sau dù NGAY=10/06, lấy bản NGAY=08/06 hủy 10/06",
                    day10.Count == 2 && day10[0].Name == "HDH-01" && day10[1].Name == "HDH-02",
                    "n=" + day10.Count);

                List<CancelledBillRow> fromOnly = onlySeeded(repo.LoadBills(new DateTime(2003, 6, 10), null));
                Check("Hóa đơn hủy (§6 đầu Đến trống): lấy từ 10/06 trở đi (HDH-01, HDH-02, HDH-03)",
                    fromOnly.Count == 3 && fromOnly[0].Name == "HDH-01" && fromOnly[1].Name == "HDH-02" && fromOnly[2].Name == "HDH-03",
                    "n=" + fromOnly.Count);

                List<CancelledBillRow> toOnly = onlySeeded(repo.LoadBills(null, new DateTime(2003, 6, 10)));
                Check("Hóa đơn hủy (§6 đầu Từ trống): lấy đến trọn 10/06 (HDH-04, HDH-01, HDH-02)",
                    toOnly.Count == 3 && toOnly[0].Name == "HDH-04" && toOnly[1].Name == "HDH-01" && toOnly[2].Name == "HDH-02",
                    "n=" + toOnly.Count);

                List<CancelledBillLine> bill1Lines = repo.LoadLines(bill1Id);
                List<CancelledBillLine> emptyLines = repo.LoadLines("");
                List<CancelledBillLine> injectionSafe = repo.LoadLines("x' OR '1'='1");
                Check("Hóa đơn hủy (§6/P4 chi tiết & tham số hóa): LoadLines trả đúng 3 dòng theo TIMECREATED, rỗng/SQL-injection an toàn",
                    bill1Lines.Count == 3
                    && bill1Lines[0].MaHang == "CF-01" && bill1Lines[1].MaHang == "TR-02" && bill1Lines[2].MaHang == "DX-03"
                    && emptyLines.Count == 0 && injectionSafe.Count == 0,
                    "n=" + bill1Lines.Count);

                // 5. Dựng CancelledBillView trong Form được Show thật (đặt ngoài màn hình, ShowInTaskbar=false); mọi thao tác đi qua sự kiện thật của control.
                string[] expectedMasterHeaders =
                {
                    "Số phiếu", "Ghi chú", "Ngày", "Khách hàng", "Nhân viên", "Thu ngân hủy",
                    "Giờ hủy", "Ngày hủy", "Đổi trả", "Đã thanh toán", "Giờ thanh toán", "Trả lại",
                    "Tiền hàng", "Tỉ lệ thuế", "Tiền thuế", "Tỉ lệ giảm giá (%)", "Tiền giảm giá",
                    "Phí vận chuyển", "Thanh toán bởi", "Lý do hủy", "Tiền giờ", "Phí dịch vụ"
                };
                string[] expectedDetailHeaders =
                {
                    "Ghi chú", "Mã hàng", "Tên hàng", "ĐVT", "Số lượng", "Đơn giá", "Thành tiền"
                };

                using (var hostForm = new System.Windows.Forms.Form
                {
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new System.Drawing.Point(-20000, -20000),
                    ClientSize = new System.Drawing.Size(1360, 740)
                })
                {
                    var view = new CancelledBillView(adminSession)
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        SuppressErrorPopup = true
                    };
                    hostForm.Controls.Add(view);
                    hostForm.Show();
                    System.Windows.Forms.Application.DoEvents();
                    try
                    {
                        // Mở màn (trên Form đã Show): 2 ô ngày trống, nạp đủ 4 bản HDH, chọn dòng đầu và nạp chi tiết của dòng đầu đó.
                        var firstRowTag = view.MasterRowCount > 0 ? view.MasterGridForTest.Rows[0].Tag as CancelledBillRow : null;
                        int expectedFirstDetailCount = firstRowTag == null ? 0 : repo.LoadLines(firstRowTag.Id).Count;
                        bool firstSelectedOnOpen = view.MasterGridForTest.SelectedRows.Count == 1
                            && view.MasterGridForTest.SelectedRows[0].Index == 0
                            && firstRowTag != null
                            && view.DetailRowCount == expectedFirstDetailCount
                            && (firstRowTag.Id != bill4Id || view.DetailRowCount == 2);
                        Check("Hóa đơn hủy UI (§6 mở màn trên Form đã Show): hai ô ngày trống, nạp đủ bản HDH, tự chọn dòng đầu và nạp chi tiết của dòng đầu",
                            view.FromDateValue == null && view.ToDateValue == null
                            && view.FindMasterRowIndexById(bill1Id) >= 0
                            && view.FindMasterRowIndexById(bill2Id) >= 0
                            && view.FindMasterRowIndexById(bill3Id) >= 0
                            && view.FindMasterRowIndexById(bill4Id) >= 0
                            && firstSelectedOnOpen,
                            "firstId=" + (firstRowTag == null ? "null" : firstRowTag.Name) + ", detailRows=" + view.DetailRowCount + "/" + expectedFirstDetailCount);

                        // Đúng 22 cột lưới trên + đúng nhãn, thứ tự; không cột Bàn / Tổng cộng; không ép Fill để cuộn ngang; độ rộng >= nhãn tiêu đề.
                        List<string> masterHeaders = view.MasterColumnHeaders();
                        bool masterHeadersOk = masterHeaders.Count == expectedMasterHeaders.Length;
                        for (int i = 0; masterHeadersOk && i < expectedMasterHeaders.Length; i++)
                            if (masterHeaders[i] != expectedMasterHeaders[i])
                                masterHeadersOk = false;
                        bool noBanNoTotal = !masterHeaders.Contains("Bàn") && !masterHeaders.Contains("Tổng cộng");
                        bool noFillOnMaster = true;
                        bool headersNotClipped = true;
                        int totalMasterWidth = 0;
                        foreach (System.Windows.Forms.DataGridViewColumn col in view.MasterGridForTest.Columns)
                        {
                            if (col.AutoSizeMode == System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill)
                                noFillOnMaster = false;
                            int needW = System.Windows.Forms.TextRenderer.MeasureText(col.HeaderText, view.HeadingFontForTest).Width;
                            if (col.Width < needW)
                                headersNotClipped = false;
                            totalMasterWidth += col.Width;
                        }
                        Check("Hóa đơn hủy UI (§6/Q8 lưới trên 22 cột): đúng nhãn/thứ tự, không Bàn/Tổng cộng, không Fill, đủ rộng cuộn ngang",
                            masterHeadersOk && noBanNoTotal && noFillOnMaster && headersNotClipped && totalMasterWidth > 1400,
                            "cols=" + masterHeaders.Count + ", totalW=" + totalMasterWidth + ", noFill=" + noFillOnMaster);

                        // Đúng 7 cột lưới dưới theo spec §1 (Q9).
                        List<string> detailHeaders = view.DetailColumnHeaders();
                        bool detailHeadersOk = detailHeaders.Count == expectedDetailHeaders.Length;
                        for (int i = 0; detailHeadersOk && i < expectedDetailHeaders.Length; i++)
                            if (detailHeaders[i] != expectedDetailHeaders[i])
                                detailHeadersOk = false;
                        Check("Hóa đơn hủy UI (§6/Q9 lưới dưới 7 cột): đúng nhãn và thứ tự ViewConfig (Ghi chú đứng đầu)",
                            detailHeadersOk, string.Join(" | ", detailHeaders.ToArray()));

                        // Chuỗi hiển thị thật của ô trên lưới master (ngày, giờ hủy HH:mm, giờ TT dd/MM HH:mm, n0, 0.##, ô đánh dấu, NULL trống, TIENGIAMGIA=TILEGIAMGIA Q3).
                        int r1 = view.FindMasterRowIndexById(bill1Id);
                        int r2 = view.FindMasterRowIndexById(bill2Id);
                        int r3 = view.FindMasterRowIndexById(bill3Id);
                        string fNgay1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colNgay"));
                        string fGioHuy1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colGioHuy"));
                        string fNgayHuy1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colNgayHuy"));
                        string fGioTT1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colGioThanhToan"));
                        object fPaid1 = view.GetMasterFormattedValue(r1, "colDaThanhToan");
                        string fTienHang1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colTienHang"));
                        string fTiLeGG1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colTiLeGiamGia"));
                        string fTienGG1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colTienGiamGia"));
                        string fNhanVien1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colNhanVien"));
                        string fPhiVC1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colPhiVanChuyen"));
                        string fTienGio1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colTienGio"));
                        string fPhiDV1 = Convert.ToString(view.GetMasterFormattedValue(r1, "colPhiDichVu"));

                        Check("Hóa đơn hủy UI (§6 định dạng ô thật): dd/MM/yyyy, GIOHUY HH:mm, GIOTHANHTOAN dd/MM HH:mm, DATHANHTOAN=30 đánh dấu, n0, NHANVEN",
                            fNgay1 == "08/06/2003"
                            && fGioHuy1 == "09:15"
                            && fNgayHuy1 == "10/06/2003"
                            && fGioTT1 == "08/06 18:45"
                            && (fPaid1 is bool && (bool)fPaid1)
                            && fTienHang1 == 125000m.ToString("n0", vi)
                            && fPhiVC1 == 20000m.ToString("n0", vi)
                            && fTienGio1 == 30000m.ToString("n0", vi)
                            && fPhiDV1 == 15000m.ToString("n0", vi)
                            && fNhanVien1 == "Trần Văn B",
                            "ngay=" + fNgay1 + ", gioHuy=" + fGioHuy1 + ", gioTT=" + fGioTT1 + ", paid=" + fPaid1 + ", th=" + fTienHang1);

                        // Q3: TIENGIAMGIA = TILEGIAMGIA hiện nguyên trạng + dòng ghi chú hiện trên màn; TILEGIAMGIA định dạng 0.##; NULL để trống; DATHANHTOAN != 30 không đánh dấu.
                        object fPaid2 = view.GetMasterFormattedValue(r2, "colDaThanhToan");
                        string fGioTT2 = Convert.ToString(view.GetMasterFormattedValue(r2, "colGioThanhToan"));
                        string fTienGG2 = Convert.ToString(view.GetMasterFormattedValue(r2, "colTienGiamGia"));
                        string fTiLeGG2 = Convert.ToString(view.GetMasterFormattedValue(r2, "colTiLeGiamGia"));
                        object fPaid3 = view.GetMasterFormattedValue(r3, "colDaThanhToan");
                        string fTiLeGG3 = Convert.ToString(view.GetMasterFormattedValue(r3, "colTiLeGiamGia"));
                        string fTienGG3 = Convert.ToString(view.GetMasterFormattedValue(r3, "colTienGiamGia"));

                        Check("Hóa đơn hủy UI (§6/Q3 TIENGIAMGIA & NULL): bản cũ TIENGIAMGIA=TILEGIAMGIA hiện đúng DB kèm ghi chú, NULL để trống, ô đánh dấu tắt khi !=30/NULL",
                            fTiLeGG1 == 15m.ToString("0.##", vi)
                            && fTienGG1 == 15m.ToString("n0", vi)
                            && view.NoteVisible && view.NoteText.IndexOf("Tiền giảm giá", StringComparison.OrdinalIgnoreCase) >= 0
                            && (fPaid2 is bool && !(bool)fPaid2)
                            && fGioTT2 == "" && fTienGG2 == "" && fTiLeGG2 == ""
                            && (fPaid3 is bool && !(bool)fPaid3)
                            && fTiLeGG3 == 12.5m.ToString("0.##", vi)
                            && fTienGG3 == 25000m.ToString("n0", vi),
                            "r1=" + fTiLeGG1 + "/" + fTienGG1 + ", r3=" + fTiLeGG3 + "/" + fTienGG3 + ", note=" + view.NoteVisible);

                        // Đổi dòng lưới trên CHỈ bằng CurrentCell/Selected (qua sự kiện thật SelectionChanged, KHÔNG gọi ReloadLines) + kiểm chuỗi hiển thị chi tiết.
                        bool sel1 = view.SelectMasterById(bill1Id);
                        string dSl1b = Convert.ToString(view.GetDetailFormattedValue(1, "colDetailSoLuong"));
                        string dDg1b = Convert.ToString(view.GetDetailFormattedValue(1, "colDetailDonGia"));
                        string dTt1b = Convert.ToString(view.GetDetailFormattedValue(1, "colDetailThanhTien"));
                        int countBill1 = view.DetailRowCount;

                        bool sel2 = view.SelectMasterById(bill2Id);
                        int countBill2 = view.DetailRowCount;
                        string dTen2a = Convert.ToString(view.GetDetailFormattedValue(0, "colDetailTenHang"));

                        bool sel3 = view.SelectMasterById(bill3Id);
                        int countBill3 = view.DetailRowCount;

                        Check("Hóa đơn hủy UI (§6 sự kiện SelectionChanged & định dạng chi tiết): đổi CurrentCell tự nạp chi tiết đúng SL/Đơn giá/Thành tiền",
                            sel1 && countBill1 == 3
                            && dSl1b == 1.5m.ToString("#,##0.##", vi)
                            && dDg1b == 35000m.ToString("n0", vi)
                            && dTt1b == 52500m.ToString("n0", vi)
                            && sel2 && countBill2 == 1 && dTen2a == "Bánh ngọt socola"
                            && sel3 && countBill3 == 0,
                            "c1=" + countBill1 + " (" + dSl1b + "/" + dDg1b + "/" + dTt1b + "), c2=" + countBill2 + ", c3=" + countBill3);

                        // Ô Lọc: CHỈ gán FilterBoxForTest.Text (qua sự kiện thật TextChanged, KHÔNG gọi ApplyDetailFilter).
                        int masterBeforeFilter = view.MasterRowCount;
                        view.SelectMasterById(bill1Id);
                        view.FilterBoxForTest.Text = "TRÀ ĐÀO";
                        bool filterViUpper = view.DetailRowCount == 1
                            && Convert.ToString(view.GetDetailFormattedValue(0, "colDetailTenHang")) == "Trà đào cam sả"
                            && view.MasterRowCount == masterBeforeFilter;

                        view.FilterBoxForTest.Text = "đá xay";
                        bool filterViD = view.DetailRowCount == 1
                            && Convert.ToString(view.GetDetailFormattedValue(0, "colDetailMaHang")) == "DX-03";

                        view.FilterBoxForTest.Text = "cf-01";
                        bool filterCode = view.DetailRowCount == 1
                            && Convert.ToString(view.GetDetailFormattedValue(0, "colDetailMaHang")) == "CF-01";

                        view.FilterBoxForTest.Text = "Ít đường"; // chỉ có trong NOTE, không có trong TENHANG/MAHANG
                        bool filterNoteIgnored = view.DetailRowCount == 0;

                        view.FilterBoxForTest.Text = "bánh";
                        int bill1WithBanh = view.DetailRowCount;
                        view.SelectMasterById(bill2Id);
                        int bill2WithBanh = view.DetailRowCount; // đổi dòng lưới trên vẫn áp chuỗi đang gõ

                        Check("Hóa đơn hủy UI (§6/Q2/Q13 sự kiện TextChanged ô Lọc): chỉ gán Text lọc TENHANG/MAHANG chữ Việt và giữ khi đổi dòng",
                            filterViUpper && filterViD && filterCode && filterNoteIgnored
                            && bill1WithBanh == 0 && bill2WithBanh == 1,
                            "vi=" + filterViUpper + ", d=" + filterViD + ", code=" + filterCode + ", noteIgn=" + filterNoteIgnored
                            + ", switch=" + bill1WithBanh + "->" + bill2WithBanh);

                        // Đổi từng ô ngày CHỈ bằng gán Inner.Text + Commit() (qua sự kiện thật ValueChanged, KHÔNG gọi ReloadBills).
                        // 1) Đổi Từ = 10/06/2003 → loại HDH-04, tự chọn dòng đầu mới (HDH-01) và nạp chi tiết (đang lọc "bánh" → 0 dòng).
                        view.FromDateBoxForTest.Inner.Text = "10/06/2003";
                        view.FromDateBoxForTest.Commit();
                        bool fromEventReloaded = view.FindMasterRowIndexById(bill4Id) < 0
                            && view.FindMasterRowIndexById(bill1Id) == 0
                            && view.FindMasterRowIndexById(bill3Id) >= 0
                            && view.MasterGridForTest.SelectedRows.Count == 1
                            && view.MasterGridForTest.SelectedRows[0].Index == 0;

                        // 2) Đổi Đến = 10/06/2003 → loại HDH-03, còn HDH-01 & HDH-02, tự chọn dòng đầu (HDH-01).
                        view.ToDateBoxForTest.Inner.Text = "10/06/2003";
                        view.ToDateBoxForTest.Commit();
                        bool toEventReloaded = view.MasterRowCount == 2
                            && view.FindMasterRowIndexById(bill1Id) == 0
                            && view.FindMasterRowIndexById(bill2Id) == 1
                            && view.FindMasterRowIndexById(bill3Id) < 0
                            && view.MasterGridForTest.SelectedRows.Count == 1
                            && view.MasterGridForTest.SelectedRows[0].Index == 0;

                        Check("Hóa đơn hủy UI (§6 sự kiện ValueChanged Từ & Đến): gán Inner.Text + Commit tự tải lại lưới trên và chọn dòng đầu",
                            fromEventReloaded && toEventReloaded,
                            "fromOk=" + fromEventReloaded + ", toOk=" + toEventReloaded + ", rows=" + view.MasterRowCount);

                        // Refresh thật bằng PerformClick(): chọn sang dòng thứ 2 (HDH-02), chèn thêm HDH-05 sau khi mở màn, bấm Refresh.
                        // Chứng minh: bản mới HDH-05 xuất hiện, giữ 2 ô ngày & ô Lọc "bánh", KHÔNG giữ vị trí dòng cũ (quay về chọn dòng đầu HDH-01).
                        view.SelectMasterById(bill2Id);
                        bool beforeRefreshOnBill2 = view.MasterGridForTest.SelectedRows.Count == 1
                            && view.MasterGridForTest.SelectedRows[0].Index == 1
                            && view.DetailRowCount == 1;

                        BookingExec(db,
                            "INSERT INTO TDONHANGHUY (" +
                            "ID, NAME, STATUS, USERCREATEDID, NOTE, NGAY, KHACHHANG, NHANVEN, THUNGAN, " +
                            "GIOHUY, NGAYHUY, DOITRA, DATHANHTOAN, GIOTHANHTOAN, TRALAI, " +
                            "TIENHANG, TILETHUE, TIENTHUE, TILEGIAMGIA, TIENGIAMGIA, PHIVANCHUYEN, " +
                            "THANHTOANBOI, LYDOHUY, TIENGIO, PHIDICHVU, TDONHANGID, BAN) VALUES (" +
                            "@ID, @NAME, 30, 'selftest', @NOTE, @NGAY, @KH, @NV, @TN, " +
                            "@GIOHUY, @NGAYHUY, 0, 30, @GIOTT, 0, " +
                            "@TIENHANG, 0, 0, 0, 0, 0, " +
                            "@TTBOI, @LYDO, 0, 0, @ORIG, @BAN)",
                            "@ID", bill5Id, "@NAME", "HDH-05", "@NOTE", "Chèn mới trước Refresh",
                            "@NGAY", new DateTime(2003, 6, 10, 0, 0, 0),
                            "@KH", "Khách mới", "@NV", "NV 05", "@TN", "Thu ngân 5",
                            "@GIOHUY", new DateTime(2003, 6, 10, 17, 45, 0),
                            "@NGAYHUY", new DateTime(2003, 6, 10, 17, 45, 0),
                            "@GIOTT", new DateTime(2003, 6, 10, 17, 0, 0),
                            "@TIENHANG", 60000m, "@TTBOI", "Thu ngân 5", "@LYDO", "Kiểm nút Refresh thật",
                            "@ORIG", DbHelper.NewId(), "@BAN", "Bàn 10");
                        BookingExec(db,
                            "INSERT INTO TDONHANGHUYCHITIET (ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) " +
                            "VALUES (@ID, 30, 'selftest', @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                            "@ID", line5aId, "@TC", new DateTime(2003, 6, 10, 17, 45, 1), "@PID", bill5Id,
                            "@MA", "BN-05", "@TEN", "Bánh bông lan", "@DVT", "Cái", "@SL", 2m, "@DG", 30000m, "@TT", 60000m, "@NOTE", "Mới thêm");

                        view.RefreshButtonForTest.PerformClick();
                        bool refreshLoadedNewAndResetToFirst = beforeRefreshOnBill2
                            && view.FindMasterRowIndexById(bill5Id) >= 0
                            && view.FromDateValue == new DateTime(2003, 6, 10)
                            && view.ToDateValue == new DateTime(2003, 6, 10)
                            && view.FilterText == "bánh"
                            && view.MasterGridForTest.SelectedRows.Count == 1
                            && view.MasterGridForTest.SelectedRows[0].Index == 0
                            && view.DetailRowCount == 0; // dòng đầu là HDH-01, đang giữ lọc "bánh" → 0 dòng

                        // Xóa chuỗi ô Lọc → trả đủ 3 dòng của HDH-01 đang chọn ở dòng đầu; thử luôn Refresh từ menu chuột phải khi đang chọn HDH-05 → quay về dòng đầu HDH-01 (3 dòng).
                        view.FilterBoxForTest.Text = "";
                        bool clearFilterRestoresAll = view.DetailRowCount == 3;

                        view.SelectMasterById(bill5Id);
                        bool onBill5BeforeMenuRefresh = view.DetailRowCount == 1;
                        System.Windows.Forms.ToolStripItem menuRefreshItem = null;
                        foreach (System.Windows.Forms.ToolStripItem item in view.MasterMenuForTest.Items)
                            if (string.Equals(item.Text, "Refresh", StringComparison.Ordinal))
                                menuRefreshItem = item;
                        if (menuRefreshItem != null)
                            menuRefreshItem.PerformClick();
                        bool menuRefreshResetsToFirst = onBill5BeforeMenuRefresh
                            && menuRefreshItem != null
                            && view.MasterGridForTest.SelectedRows.Count == 1
                            && view.MasterGridForTest.SelectedRows[0].Index == 0
                            && view.DetailRowCount == 3;

                        // Đổi sang khoảng ngày không có dữ liệu bằng Inner.Text + Commit() → lưới trên rỗng và lưới dưới rỗng.
                        view.FromDateBoxForTest.Inner.Text = "01/01/2000";
                        view.FromDateBoxForTest.Commit();
                        view.ToDateBoxForTest.Inner.Text = "01/01/2000";
                        view.ToDateBoxForTest.Commit();
                        bool emptyMasterClearsDetail = view.MasterRowCount == 0 && view.DetailRowCount == 0;

                        Check("Hóa đơn hủy UI (§6/Q11 nút Refresh & menu Refresh): PerformClick tải bản mới HDH-05, giữ ngày & ô Lọc, chọn lại dòng đầu, rỗng→chi tiết rỗng",
                            refreshLoadedNewAndResetToFirst && clearFilterRestoresAll && menuRefreshResetsToFirst && emptyMasterClearsDetail,
                            "refNew=" + refreshLoadedNewAndResetToFirst + ", clear=" + clearFilterRestoresAll
                            + ", menuRef=" + menuRefreshResetsToFirst + ", empty=" + emptyMasterClearsDetail);

                        // Chỉ đọc, đủ cao cảm ứng, không có nút Thêm/Sửa/Xóa/Excel/In, menu chuột phải hai lưới không có Thêm/Sửa/Xóa (Q15).
                        bool gridsReadOnly = view.MasterGridForTest.ReadOnly
                            && !view.MasterGridForTest.AllowUserToAddRows
                            && !view.MasterGridForTest.AllowUserToDeleteRows
                            && view.DetailGridForTest.ReadOnly
                            && !view.DetailGridForTest.AllowUserToAddRows
                            && !view.DetailGridForTest.AllowUserToDeleteRows;
                        bool touchFriendlyRows = view.MasterGridForTest.RowTemplate.Height >= 32
                            && view.DetailGridForTest.RowTemplate.Height >= 32;

                        var allButtons = new List<System.Windows.Forms.Control>();
                        CollectAllButtons(view, allButtons);
                        bool onlyRefreshButton = allButtons.Count == 1
                            && string.Equals(allButtons[0].Text, "Refresh", StringComparison.Ordinal);

                        bool menusReadOnly = !MenuContainsForbiddenWriteAction(view.MasterMenuForTest)
                            && !MenuContainsForbiddenWriteAction(view.DetailMenuForTest);

                        Check("Hóa đơn hủy UI (§6/Q15 chỉ xem): hai lưới chỉ đọc + đủ cao cảm ứng, chỉ có nút Refresh, menu chuột phải không có Thêm/Sửa/Xóa",
                            gridsReadOnly && touchFriendlyRows && onlyRefreshButton && menusReadOnly,
                            "ro=" + gridsReadOnly + ", touch=" + touchFriendlyRows + ", btns=" + allButtons.Count + ", menuRo=" + menusReadOnly);
                    }
                    finally
                    {
                        hostForm.Close();
                    }
                }

                // 6. Preview form --render-screen preview.cancelledbill (Show ngoài màn hình) với tùy chọn select=1 đi qua sự kiện thật.
                using (System.Windows.Forms.Form previewForm = Dialogs.ScreenFactory.Create("preview.cancelledbill", adminSession))
                {
                    var pv = previewForm as Dialogs.CancelledBillPreviewForm;
                    bool previewOk = false;
                    if (pv != null)
                    {
                        pv.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                        pv.ShowInTaskbar = false;
                        pv.Location = new System.Drawing.Point(-20000, -20000);
                        pv.Show();
                        System.Windows.Forms.Application.DoEvents();
                        try
                        {
                            pv.Preview("from=10/06/2003");
                            pv.Preview("to=10/06/2003");
                            pv.Preview("select=1");
                            previewOk = pv.ViewForTest.MasterRowCount >= 2
                                && pv.ViewForTest.MasterGridForTest.SelectedRows.Count == 1
                                && pv.ViewForTest.MasterGridForTest.SelectedRows[0].Index == 1
                                && pv.ViewForTest.DetailRowCount == 1;
                        }
                        finally
                        {
                            pv.Close();
                        }
                    }
                    Check("Hóa đơn hủy (preview.cancelledbill): Show form chụp màn hình và select=1 đổi dòng + nạp chi tiết qua sự kiện thật",
                        previewOk);
                }
            }
            finally
            {
                // Dọn con (TDONHANGHUYCHITIET) trước cha (TDONHANGHUY).
                BookingExec(db,
                    "DELETE FROM TDONHANGHUYCHITIET WHERE TDONHANGHUYID IN (@A, @B, @C, @D, @E)",
                    "@A", bill1Id, "@B", bill2Id, "@C", bill3Id, "@D", bill4Id, "@E", bill5Id);
                BookingExec(db,
                    "DELETE FROM TDONHANGHUY WHERE ID IN (@A, @B, @C, @D, @E)",
                    "@A", bill1Id, "@B", bill2Id, "@C", bill3Id, "@D", bill4Id, "@E", bill5Id);
            }
        }

        // ------------------------------------------------------------------ Thống kê mặt hàng bán (docs/specs/thong-ke-mat-hang-ban.md §6)

        private static bool TreeContainsText(System.Windows.Forms.TreeNodeCollection nodes, string text)
        {
            if (nodes == null)
                return false;
            foreach (System.Windows.Forms.TreeNode n in nodes)
            {
                if (string.Equals(n.Text, text, StringComparison.Ordinal))
                    return true;
                if (TreeContainsText(n.Nodes, text))
                    return true;
            }
            return false;
        }

        private static void ItemSalesTests(string db)
        {
            var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

            // 1. ItemSalesCalculator thuần decimal (§2.2, §2.3, §7 — ≥ 8 ca tính tay).
            Check("Thống kê MH bán (Calculator LAI dương)", ItemSalesCalculator.ComputeProfit(120000m, 40000m) == 80000m);
            Check("Thống kê MH bán (Calculator LAI âm / lỗ)", ItemSalesCalculator.ComputeProfit(50000m, 65000m) == -15000m);
            Check("Thống kê MH bán (Calculator LAI bằng 0)", ItemSalesCalculator.ComputeProfit(0m, 0m) == 0m);
            Check("Thống kê MH bán (Calculator TILELAI chuẩn)", ItemSalesCalculator.ComputeProfitRate(100000m, 30000m) == 30m);
            Check("Thống kê MH bán (Calculator TILELAI âm)", ItemSalesCalculator.ComputeProfitRate(50000m, -10000m) == -20m);
            Check("Thống kê MH bán (§6 TILELAI = 0 khi THANHTIEN = 0)", ItemSalesCalculator.ComputeProfitRate(0m, -5000m) == 0m);

            var calcRows = new List<ItemSalesRow>
            {
                new ItemSalesRow { Quantity = 3m, UnitPrice = 30000m, SalesAmount = 80000m, CostAmount = 36000m },
                new ItemSalesRow { Quantity = 2m, UnitPrice = 50000m, SalesAmount = 100000m, CostAmount = 60000m }
            };
            ItemSalesCalculator.ApplyRowMetrics(calcRows);
            // Tổng tiền bán = 3*30000 + 2*50000 = 190000; Tổng tiền nhập = 96000; Lãi bán hàng = 94000
            // Giảm giá = 25000; Thuế = 12000; Phí DV = 8000; Tiền giờ = 5000 -> Thực lãi = 94000 - 25000 + 12000 + 8000 + 5000 = 94000
            ItemSalesSummary calcSum = ItemSalesCalculator.ComputeSummary(calcRows, new ItemSalesBillTotals
            {
                TotalDiscount = 25000m,
                Tax = 12000m,
                ServiceFee = 8000m,
                TimeCharge = 5000m
            });
            Check("Thống kê MH bán (Calculator ApplyRowMetrics & ComputeSummary khớp số tay)",
                calcRows[0].Profit == 44000m && calcRows[0].ProfitRate == 55m
                && calcRows[1].Profit == 40000m && calcRows[1].ProfitRate == 40m
                && calcSum.TotalSalesBeforeDiscount == 190000m
                && calcSum.TotalCost == 96000m
                && calcSum.GrossSalesProfit == 94000m
                && calcSum.NetProfit == 94000m);

            ItemSalesSummary emptySum = ItemSalesCalculator.ComputeSummary(null, null);
            Check("Thống kê MH bán (Calculator rỗng/null → mọi tổng = 0)",
                emptySum.TotalSalesBeforeDiscount == 0m && emptySum.TotalCost == 0m && emptySum.NetProfit == 0m);

            // 2. Route ScreenFactory (không vào Map hộp thoại, đi qua CreateView theo MenuId trước kiểm Operation) & quyền menu.
            var adminSession = new UserSession
            {
                UserId = "selftest_admin",
                UserName = "Admin",
                DisplayName = "Administrator",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            Check("Thống kê MH bán (§6 route): MenuId không nằm trong Map hộp thoại",
                !Dialogs.ScreenFactory.Has(ItemSalesView.MenuId));
            var menuNode = new MenuNode
            {
                Id = ItemSalesView.MenuId,
                Title = ItemSalesConstants.FunctionName,
                Operation = 4,
                StableDescId = null
            };
            using (System.Windows.Forms.Control created = Dialogs.ScreenFactory.CreateView(menuNode, adminSession))
            {
                Check("Thống kê MH bán (§6 route): ScreenFactory.CreateView dựng ItemSalesView dù Operation=4 và không có StableDescId",
                    created is ItemSalesView);
            }

            var permsViewOnly = new FunctionPermissions(false);
            permsViewOnly.Add(ItemSalesConstants.FunctionId, ItemSalesConstants.FunctionName, RoleModes.View);
            var permsNone = new FunctionPermissions(false);
            MenuModel menuHas = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_is_allow", UserName = "u_is_allow", DisplayName = "Cho xem", IsAdmin = false,
                DatabasePath = db, Permissions = permsViewOnly
            });
            MenuModel menuNo = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_is_deny", UserName = "u_is_deny", DisplayName = "Chặn", IsAdmin = false,
                DatabasePath = db, Permissions = permsNone
            });
            Check("Thống kê MH bán (§2.1/§6 phân quyền menu): MainMenuBuilder hiện khi có quyền SFUNCTION và ẩn khi không có quyền",
                MenuTreeContainsId(menuHas.Roots, ItemSalesView.MenuId)
                && !MenuTreeContainsId(menuNo.Roots, ItemSalesView.MenuId));

            // 3. Seed dữ liệu mẫu (tiền tố TKMH-, tháng 7/2002) trên bản sao DB; dọn con trước cha trong finally.
            string storeAId = DbHelper.NewId();
            string storeBId = DbHelper.NewId();
            string userAId = DbHelper.NewId();
            string userNoStoreId = DbHelper.NewId();
            string userStoreLinkId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string custId = DbHelper.NewId();
            string bookingId = DbHelper.NewId();

            string grpDrinkId = DbHelper.NewId();
            string grpDrinkSubId = DbHelper.NewId();
            string grpCakeId = DbHelper.NewId();
            string grpMatId = DbHelper.NewId();

            string prodAId = DbHelper.NewId();
            string prodBId = DbHelper.NewId();
            string prodCId = DbHelper.NewId();
            string prodDId = DbHelper.NewId();
            string prodMatId = DbHelper.NewId();

            string billA1Id = DbHelper.NewId();
            string billA2Id = DbHelper.NewId();
            string billB1Id = DbHelper.NewId();
            string billUnpaidId = DbHelper.NewId();
            string billDeletedId = DbHelper.NewId();
            string billImportId = DbHelper.NewId();
            string billNextDayId = DbHelper.NewId();

            var lineIds = new List<string>();
            var cfgRepo = new ConfigRepository(db);
            int origSortOrder = SystemConfig.GetInt(ItemSalesConstants.ConfigSortOrderKey);
            int origUseCode = SystemConfig.GetInt(ItemSalesConstants.ConfigUseCodeKey);
            UserSession savedAppSession = AppSession.Current;

            try
            {
                // Cửa hàng, user, liên kết user–cửa hàng, ĐVT, khách hàng, phiếu đặt hàng.
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeAId, "@N", "TKMH-CH-A");
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeBId, "@N", "TKMH-CH-B");
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", userAId, "@N", "TKMH Thu ngân A", "@U", "tkmh_user_a");
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", userNoStoreId, "@N", "TKMH Không CH", "@U", "tkmh_user_none");
                BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", userStoreLinkId, "@U", userAId, "@C", storeAId);

                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", unitId, "@N", "TKMH-Ly");
                BookingExec(db, "INSERT INTO DKHACHHANG (ID, NAME, MAKHACH, DIACHI, DIENTHOAI, STATUS, USERCREATEDID) VALUES (@I, @N, @M, @DC, @DT, 30, 'selftest')",
                    "@I", custId, "@N", "TKMH Khách A", "@M", "TKMH-KH01", "@DC", "12 Nguyễn Huệ", "@DT", "0909000111");
                BookingExec(db, "INSERT INTO TDATHANG (ID, NAME, NGAY, STATUS, USERCREATEDID) VALUES (@I, @N, @D, 30, 'selftest')",
                    "@I", bookingId, "@N", "TKMH-DH01", "@D", new DateTime(2002, 7, 10));

                // 4 nhóm: grpDrink (cha), grpDrinkSub (con của grpDrink), grpCake, grpMat (DLOAIDOID='4' nguyên liệu → phải bị loại khỏi cây).
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '1', '', '0001', 30, 'selftest')",
                    "@I", grpDrinkId, "@N", "TKMH-Đồ uống");
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '1', @P, '0001', 30, 'selftest')",
                    "@I", grpDrinkSubId, "@N", "TKMH-Trà", "@P", grpDrinkId);
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '0', '', '0002', 30, 'selftest')",
                    "@I", grpCakeId, "@N", "TKMH-Bánh");
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '4', '', '0003', 30, 'selftest')",
                    "@I", grpMatId, "@N", "TKMH-Nguyên liệu");

                // 5 mặt hàng (4 mặt hàng bán + 1 nguyên liệu DLOAIMATHANGID='2').
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, BAOHANH, MASANCO, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '0', 12000, 10000, 30000, 'BH-SP-A', 'MSC-A', 30, 'selftest')",
                    "@I", prodAId, "@C", "TKMH-03", "@N", "TKMH-Cà phê sữa", "@G", grpDrinkId, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '0', 18000, 20000, 50000, 30, 'selftest')",
                    "@I", prodBId, "@C", "TKMH-01", "@N", "TKMH-Trà đào cam sả", "@G", grpDrinkSubId, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '0', 22000, 25000, 40000, 30, 'selftest')",
                    "@I", prodCId, "@C", "TKMH-02", "@N", "TKMH-Bánh tiramisu", "@G", grpCakeId, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '0', NULL, NULL, 0, 30, 'selftest')",
                    "@I", prodDId, "@C", "TKMH-04", "@N", "TKMH-Nước lọc NULL", "@G", grpCakeId, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '2', 5000, 4000, 0, 30, 'selftest')",
                    "@I", prodMatId, "@C", "TKMH-05", "@N", "TKMH-Sữa đặc NL", "@G", grpMatId, "@U", unitId);

                // Hóa đơn hợp lệ:
                // billA1 (storeA, 10/07/2002 09:00, GIOTHANHTOAN 09:30): giảm tổng 10000 + giảm MH 10000, giờ 5000, phí DV 8000, thuế 12000.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, DKHACHHANGID, USERTHANHTOANID, TDATHANGID, " +
                    "TIENHANG, TONGCONG, PHIVANCHUYEN, TIENGIAMGIA, TILEGIAMGIA, TIENGIAMGIATONG, GIAMGIAMATHANG, TIENTHUE, TILETHUE, TIENGIO, PHIDICHVU, DIENGIAI, KHACHDUA, TRALAI, GIAOHANG, DOITRA, USERCREATEDID) " +
                    "VALUES (@ID, 'TKMH-HD-A1', 'Ghi chú A1', 0, 30, 30, @NGAY, @GIOTT, @CH, @KH, @UTT, @DH, " +
                    "185000, 200000, 5000, 10000, 5, 10000, 10000, 12000, 10, 5000, 8000, 'Diễn giải A1', 250000, 50000, 'Tại quầy', 0, 'selftest')",
                    "@ID", billA1Id, "@NGAY", new DateTime(2002, 7, 10, 9, 0, 0), "@GIOTT", new DateTime(2002, 7, 10, 9, 30, 0),
                    "@CH", storeAId, "@KH", custId, "@UTT", userAId, "@DH", bookingId);

                // billA2 (storeA, 11/07/2002 15:30 — kiểm 15:30 ngày Đến được tính; TIENGIO & PHIDICHVU NULL).
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, " +
                    "TIENHANG, TONGCONG, TIENGIAMGIATONG, GIAMGIAMATHANG, TIENTHUE, TIENGIO, PHIDICHVU, USERCREATEDID) " +
                    "VALUES (@ID, 'TKMH-HD-A2', 'Ghi chú A2', 0, 30, 30, @NGAY, @GIOTT, @CH, " +
                    "140000, 138000, 5000, NULL, 3000, NULL, NULL, 'selftest')",
                    "@ID", billA2Id, "@NGAY", new DateTime(2002, 7, 11, 15, 30, 0), "@GIOTT", new DateTime(2002, 7, 11, 15, 45, 0),
                    "@CH", storeAId);

                // billB1 (storeB, 10/07/2002 11:00, GIOTHANHTOAN 11:15 — nằm giữa billA1 và billA2 theo giờ thanh toán).
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, " +
                    "TIENHANG, TONGCONG, TIENGIAMGIATONG, GIAMGIAMATHANG, TIENTHUE, TIENGIO, PHIDICHVU, USERCREATEDID) " +
                    "VALUES (@ID, 'TKMH-HD-B1', 0, 30, 30, @NGAY, @GIOTT, @CH, " +
                    "30000, 35000, 2000, 0, 4000, 1000, 2000, 'selftest')",
                    "@ID", billB1Id, "@NGAY", new DateTime(2002, 7, 10, 11, 0, 0), "@GIOTT", new DateTime(2002, 7, 10, 11, 15, 0),
                    "@CH", storeBId);

                // 4 hóa đơn BỊ LOẠI (cùng storeA, đều có dòng prodA @ 30000 và số tiền giảm/thuế lớn để lộ ngay nếu bị lọt):
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENGIAMGIATONG, TIENTHUE, USERCREATEDID) " +
                    "VALUES (@ID, 'TKMH-UNPAID', 0, 0, 30, @NGAY, @NGAY, @CH, 999000, 888000, 'selftest')",
                    "@ID", billUnpaidId, "@NGAY", new DateTime(2002, 7, 10, 12, 0, 0), "@CH", storeAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENGIAMGIATONG, TIENTHUE, USERCREATEDID) " +
                    "VALUES (@ID, 'TKMH-DEL', 0, 30, 0, @NGAY, @NGAY, @CH, 999000, 888000, 'selftest')",
                    "@ID", billDeletedId, "@NGAY", new DateTime(2002, 7, 10, 12, 30, 0), "@CH", storeAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENGIAMGIATONG, TIENTHUE, USERCREATEDID) " +
                    "VALUES (@ID, 'TKMH-IMP', 1, 30, 30, @NGAY, @NGAY, @CH, 999000, 888000, 'selftest')",
                    "@ID", billImportId, "@NGAY", new DateTime(2002, 7, 10, 13, 0, 0), "@CH", storeAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENGIAMGIATONG, TIENTHUE, USERCREATEDID) " +
                    "VALUES (@ID, 'TKMH-NEXT00', 0, 30, 30, @NGAY, @NGAY, @CH, 999000, 888000, 'selftest')",
                    "@ID", billNextDayId, "@NGAY", new DateTime(2002, 7, 12, 0, 0, 0), "@CH", storeAId);

                Action<string, string, string, decimal?, decimal?, decimal?, decimal?, decimal?, decimal?, decimal?, decimal?, decimal?, int, string, string, DateTime> addLine =
                    delegate(string billId, string prodId, string tenHang, decimal? slXuatCqd, decimal? slNhapCqd, decimal? slXuat, decimal? slNhap,
                        decimal? donGia, decimal? tlGiam, decimal? tienGiam, decimal? thanhTien, decimal? giaVon, int xuatVatTu, string comboParent, string baoHanh, DateTime tc)
                    {
                        string lid = DbHelper.NewId();
                        lineIds.Add(lid);
                        BookingExec(db,
                            "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, " +
                            "SLXUATCHUAQUYDOI, SLNHAPCHUAQUYDOI, SLXUAT, SLNHAP, DONGIA, TILEGIAMGIA, TIENGIAMGIA, THANHTIEN, GIAVON, " +
                            "XUATVATTU, COMBOPARENTID, BAOHANH, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                            "VALUES (@ID, @BID, @PID, @TEN, @U, @SXC, @SNC, @SX, @SN, @DG, @TLG, @TG, @TT, @GV, @XVT, @CP, @BH, @NOTE, 30, 'selftest', @TC)",
                            "@ID", lid, "@BID", billId, "@PID", prodId, "@TEN", tenHang, "@U", unitId,
                            "@SXC", slXuatCqd, "@SNC", slNhapCqd, "@SX", slXuat, "@SN", slNhap,
                            "@DG", donGia, "@TLG", tlGiam, "@TG", tienGiam, "@TT", thanhTien, "@GV", giaVon,
                            "@XVT", xuatVatTu, "@CP", comboParent, "@BH", baoHanh, "@NOTE", "Ghi chú " + tenHang, "@TC", tc);
                    };

                // Dòng trên billA1:
                addLine(billA1Id, prodAId, "TKMH-Cà phê sữa", 2m, 0m, 2m, 0m, 30000m, 0m, 0m, 60000m, 10000m, 0, "", "BH-DONG-1", new DateTime(2002, 7, 10, 9, 1, 0));
                addLine(billA1Id, prodBId, "TKMH-Trà đào cam sả", 2m, 0m, 2m, 0m, 50000m, 10m, 10000m, 90000m, 20000m, 0, "", "", new DateTime(2002, 7, 10, 9, 2, 0));
                // Cùng mặt hàng prodA nhưng khác đơn giá (35000) → không được gộp với 30000!
                addLine(billA1Id, prodAId, "TKMH-Cà phê sữa", 1m, 0m, 1m, 0m, 35000m, 0m, 0m, 35000m, 10000m, 0, "", "", new DateTime(2002, 7, 10, 9, 3, 0));
                // Nguyên liệu (DLOAIMATHANGID='2'): loại khỏi lưới chính, nhưng HIỆN ở dialog chi tiết.
                addLine(billA1Id, prodMatId, "TKMH-Sữa đặc NL", 2m, 0m, 2m, 0m, 5000m, 0m, 0m, 10000m, 4000m, 0, "", "", new DateTime(2002, 7, 10, 9, 4, 0));
                // Xuất vật tư (XUATVATTU=30) và combo con (COMBOPARENTID<>''): loại khỏi cả lưới chính lẫn dialog chi tiết.
                addLine(billA1Id, prodCId, "TKMH-Bánh tiramisu", 1m, 0m, 1m, 0m, 40000m, 0m, 0m, 40000m, 15000m, 30, "", "", new DateTime(2002, 7, 10, 9, 5, 0));
                addLine(billA1Id, prodCId, "TKMH-Bánh tiramisu", 1m, 0m, 1m, 0m, 40000m, 0m, 0m, 40000m, 15000m, 0, "combo-parent-1", "", new DateTime(2002, 7, 10, 9, 6, 0));

                // Dòng trên billA2: trả hàng bằng SLNHAP != 0 (prodA: 3 - 1 = 2), trả hàng bằng số lượng âm (prodC: 3 + (-1) = 2), và dòng NULL tiền (prodD).
                addLine(billA2Id, prodAId, "TKMH-Cà phê sữa", 3m, 1m, 3m, 1m, 30000m, 0m, 0m, 60000m, 10000m, 0, "", "", new DateTime(2002, 7, 11, 15, 31, 0));
                addLine(billA2Id, prodCId, "TKMH-Bánh tiramisu", 3m, 0m, 3m, 0m, 40000m, 0m, 0m, 120000m, 25000m, 0, "", "", new DateTime(2002, 7, 11, 15, 32, 0));
                addLine(billA2Id, prodCId, "TKMH-Bánh tiramisu", -1m, 0m, -1m, 0m, 40000m, 0m, 0m, -40000m, 25000m, 0, "", "", new DateTime(2002, 7, 11, 15, 33, 0));
                addLine(billA2Id, prodDId, "TKMH-Nước lọc NULL", 1m, 0m, 1m, 0m, null, null, null, null, null, 0, "", "", new DateTime(2002, 7, 11, 15, 34, 0));

                // Dòng trên billB1 (storeB):
                addLine(billB1Id, prodAId, "TKMH-Cà phê sữa", 1m, 0m, 1m, 0m, 30000m, 0m, 0m, 30000m, 10000m, 0, "", "", new DateTime(2002, 7, 10, 11, 1, 0));

                // Dòng trên 4 bill bị loại:
                addLine(billUnpaidId, prodAId, "TKMH-Cà phê sữa", 10m, 0m, 10m, 0m, 30000m, 0m, 0m, 300000m, 10000m, 0, "", "", new DateTime(2002, 7, 10, 12, 1, 0));
                addLine(billDeletedId, prodAId, "TKMH-Cà phê sữa", 10m, 0m, 10m, 0m, 30000m, 0m, 0m, 300000m, 10000m, 0, "", "", new DateTime(2002, 7, 10, 12, 31, 0));
                addLine(billImportId, prodAId, "TKMH-Cà phê sữa", 10m, 0m, 10m, 0m, 30000m, 0m, 0m, 300000m, 10000m, 0, "", "", new DateTime(2002, 7, 10, 13, 1, 0));
                addLine(billNextDayId, prodAId, "TKMH-Cà phê sữa", 10m, 0m, 10m, 0m, 30000m, 0m, 0m, 300000m, 10000m, 0, "", "", new DateTime(2002, 7, 12, 0, 1, 0));

                var repo = new ItemSalesRepository(db);
                DateTime from = new DateTime(2002, 7, 10);
                DateTime to = new DateTime(2002, 7, 11);

                // 4. Kiểm phạm vi bill, loại trừ, gộp, trả hàng, 15:30 ngày Đến vs 00:00 hôm sau, hai cửa hàng, user không cửa hàng, admin.
                // 4a. Admin không chọn cửa hàng (Store A + Store B), Theo giá vốn:
                var adminScope = new BillReportScope { FromDate = from, ToDate = to, StoreId = "", UserId = "selftest_admin", IsAdmin = true };
                List<ItemSalesRow> adminCostRows = repo.LoadItemSales(adminScope, ItemSalesCostMode.LineCost, false);
                ItemSalesCalculator.ApplyRowMetrics(adminCostRows);
                ItemSalesBillTotals adminBillTotals = repo.LoadBillSummaryTotals(adminScope);
                ItemSalesSummary adminCostSum = ItemSalesCalculator.ComputeSummary(adminCostRows, adminBillTotals);

                ItemSalesRow rowA30 = adminCostRows.Find(r => r.ProductId == prodAId && r.UnitPrice == 30000m);
                ItemSalesRow rowA35 = adminCostRows.Find(r => r.ProductId == prodAId && r.UnitPrice == 35000m);
                ItemSalesRow rowB = adminCostRows.Find(r => r.ProductId == prodBId);
                ItemSalesRow rowC = adminCostRows.Find(r => r.ProductId == prodCId);
                ItemSalesRow rowD = adminCostRows.Find(r => r.ProductId == prodDId);
                bool noMaterialInGrid = !adminCostRows.Exists(r => r.ProductId == prodMatId);

                Check("Thống kê MH bán (§6 lưới & loại trừ & không gộp khác giá): đúng 5 dòng, loại NL/vật tư/combo con, loại 4 bill sai điều kiện/ngày",
                    adminCostRows.Count == 5 && noMaterialInGrid
                    && rowA30 != null && rowA30.Quantity == 5m && rowA30.SalesAmount == 150000m && rowA30.UnitCost == 10000m && rowA30.CostAmount == 50000m && rowA30.Profit == 100000m
                    && rowA35 != null && rowA35.Quantity == 1m && rowA35.SalesAmount == 35000m && rowA35.CostAmount == 10000m && rowA35.Profit == 25000m
                    && rowB != null && rowB.Quantity == 2m && rowB.DiscountRate == 10m && rowB.LineDiscountAmount == 10000m && rowB.SalesAmount == 90000m && rowB.CostAmount == 40000m && rowB.Profit == 50000m
                    && rowC != null && rowC.Quantity == 2m && rowC.SalesAmount == 80000m && rowC.CostAmount == 50000m && rowC.Profit == 30000m && rowC.ProfitRate == 37.5m
                    && rowD != null && rowD.Quantity == 1m && rowD.UnitPrice == 0m && rowD.SalesAmount == 0m && rowD.CostAmount == 0m && rowD.Profit == 0m && rowD.ProfitRate == 0m,
                    "rows=" + adminCostRows.Count + ", A30=" + (rowA30 == null ? "null" : rowA30.Quantity + "/" + rowA30.SalesAmount) + ", C=" + (rowC == null ? "null" : rowC.Quantity + "/" + rowC.SalesAmount));

                // Số tính tay admin (Store A + B, giá vốn):
                // Tổng bán = 5*30k + 1*35k + 2*50k + 2*40k + 0 = 365,000; Tổng nhập = 150,000; Lãi BH = 215,000
                // Giảm giá = (10k+10k) + (5k+0) + (2k+0) = 27,000; Giờ = 6,000; Phí DV = 10,000; Thuế = 19,000
                // Tổng tiền lãi = 215,000 - 27,000 + 19,000 + 10,000 + 6,000 = 223,000.
                Check("Thống kê MH bán (§6 khối tổng admin, cộng thuế/phí/giờ, trừ TIENGIAMGIATONG+GIAMGIAMATHANG): khớp số tay",
                    adminCostSum.TotalSalesBeforeDiscount == 365000m
                    && adminCostSum.TotalCost == 150000m
                    && adminCostSum.GrossSalesProfit == 215000m
                    && adminCostSum.TotalDiscount == 27000m
                    && adminCostSum.TimeCharge == 6000m
                    && adminCostSum.ServiceFee == 10000m
                    && adminCostSum.Tax == 19000m
                    && adminCostSum.NetProfit == 223000m,
                    "sales=" + adminCostSum.TotalSalesBeforeDiscount + ", cost=" + adminCostSum.TotalCost + ", net=" + adminCostSum.NetProfit);

                // 4b. User thường (chỉ gắn storeA, không chọn cửa hàng) → chỉ thấy storeA ở CẢ lưới, khối tổng và dialog!
                // Số tính tay storeA: Tổng bán = 335,000; Tổng nhập = 140,000; Lãi BH = 195,000; Giảm = 25,000; Giờ = 5,000; Phí = 8,000; Thuế = 15,000; Thực lãi = 198,000.
                var userAScope = new BillReportScope { FromDate = from, ToDate = to, StoreId = "", UserId = userAId, IsAdmin = false };
                List<ItemSalesRow> userARows = repo.LoadItemSales(userAScope, ItemSalesCostMode.LineCost, false);
                ItemSalesCalculator.ApplyRowMetrics(userARows);
                ItemSalesSummary userASum = ItemSalesCalculator.ComputeSummary(userARows, repo.LoadBillSummaryTotals(userAScope));
                List<ItemSalesBillRow> userADetailBills = repo.LoadDetailBills(userAScope, new ItemSalesDetailKey { ProductId = prodAId, UnitPrice = 30000m, DiscountRate = 0m });
                ItemSalesRow userARowA30 = userARows.Find(r => r.ProductId == prodAId && r.UnitPrice == 30000m);

                Check("Thống kê MH bán (§6/Q1 ràng buộc cửa hàng user thường): chỉ thấy storeA ở CẢ lưới, khối tổng và dialog",
                    userARowA30 != null && userARowA30.Quantity == 4m && userARowA30.SalesAmount == 120000m
                    && userASum.TotalSalesBeforeDiscount == 335000m
                    && userASum.TotalCost == 140000m
                    && userASum.TotalDiscount == 25000m
                    && userASum.Tax == 15000m && userASum.ServiceFee == 8000m && userASum.TimeCharge == 5000m
                    && userASum.NetProfit == 198000m
                    && userADetailBills.Count == 2
                    && userADetailBills[0].Id == billA1Id && userADetailBills[1].Id == billA2Id,
                    "qty=" + (userARowA30 == null ? -1 : userARowA30.Quantity) + ", net=" + userASum.NetProfit + ", detailBills=" + userADetailBills.Count);

                // 4c. User không có dòng nào trong TNGUOIDUNGTHEOCUAHANG → thấy RỖNG ở cả lưới, khối tổng, dialog và lookup (kể cả khi storeA đã gắn cho userA).
                var noStoreScope = new BillReportScope { FromDate = from, ToDate = to, StoreId = "", UserId = userNoStoreId, IsAdmin = false };
                List<ItemSalesRow> noStoreRows = repo.LoadItemSales(noStoreScope, ItemSalesCostMode.LineCost, false);
                ItemSalesSummary noStoreSum = ItemSalesCalculator.ComputeSummary(noStoreRows, repo.LoadBillSummaryTotals(noStoreScope));
                List<ItemSalesBillRow> noStoreBills = repo.LoadDetailBills(noStoreScope, new ItemSalesDetailKey { ProductId = prodAId, UnitPrice = 30000m, DiscountRate = 0m });
                List<StoreRecord> noStoreLookup = repo.LoadAllowedStores(userNoStoreId, false);

                Check("Thống kê MH bán (§6/Q1 user không có cửa hàng): thấy rỗng ở cả lưới, khối tổng, dialog và lookup",
                    noStoreRows.Count == 0 && noStoreSum.TotalSalesBeforeDiscount == 0m && noStoreSum.TotalDiscount == 0m
                    && noStoreSum.NetProfit == 0m && noStoreBills.Count == 0 && noStoreLookup.Count == 0);

                // 4d. Sắp xếp theo mã (SapXepThuTuTheo=0 && SuDungMaHang=30) vs theo tên.
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ItemSalesConstants.ConfigSortOrderKey, DataType = 3, IntValue = 0 },
                    new ConfigChange { Name = ItemSalesConstants.ConfigUseCodeKey, DataType = 3, IntValue = 30 }
                }, "selftest");
                bool sortByCodeEnabled = repo.ShouldSortByCode();
                List<ItemSalesRow> byCode = repo.LoadItemSales(adminScope, ItemSalesCostMode.LineCost, sortByCodeEnabled);
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ItemSalesConstants.ConfigSortOrderKey, DataType = 3, IntValue = origSortOrder },
                    new ConfigChange { Name = ItemSalesConstants.ConfigUseCodeKey, DataType = 3, IntValue = origUseCode }
                }, "selftest");
                List<ItemSalesRow> byName = repo.LoadItemSales(adminScope, ItemSalesCostMode.LineCost, false);

                Check("Thống kê MH bán (§6/§2.0 sắp xếp): cấu hình SapXepThuTuTheo=0 & SuDungMaHang=30 sắp theo DMATHANG.CODE, ngược lại theo TENHANG",
                    sortByCodeEnabled
                    && byCode.Count == 5 && byCode[0].ProductCode == "TKMH-01" && byCode[1].ProductCode == "TKMH-02" && byCode[2].ProductCode == "TKMH-03" && byCode[4].ProductCode == "TKMH-04"
                    && byName.Count == 5 && byName[0].ItemName == "TKMH-Bánh tiramisu",
                    "byCode0=" + (byCode.Count > 0 ? byCode[0].ProductCode : "") + ", byName0=" + (byName.Count > 0 ? byName[0].ItemName : ""));

                // 5. Test giao diện THẬT trên Form đã Show ngoài màn hình: sự kiện thật, màu thật, quyền thật.
                DateTime dbToday = repo.GetServerDate();
                string[] expectedMainHeaders =
                {
                    "Mặt hàng", "Số lượng", "ĐVT", "Đơn giá", "Tiền giảm", "Thành tiền bán",
                    "Giá vốn", "Giảm giá %", "Thành tiền nhập", "Lãi", "Tỉ lệ lãi"
                };

                using (var hostForm = new System.Windows.Forms.Form
                {
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new System.Drawing.Point(-20000, -20000),
                    ClientSize = new System.Drawing.Size(1400, 780)
                })
                {
                    var view = new ItemSalesView(adminSession)
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        SuppressErrorPopup = true
                    };
                    hostForm.Controls.Add(view);
                    hostForm.Show();
                    System.Windows.Forms.Application.DoEvents();
                    try
                    {
                        // Mở màn: Từ = Đến = hôm nay theo giờ CSDL; đúng 11 cột; không cắt tiêu đề; cây nhóm có "Tất cả", không có nguyên liệu & không có "Chưa thiết lập".
                        List<string> headers = view.GridColumnHeaders();
                        bool headersOk = headers.Count == expectedMainHeaders.Length;
                        for (int i = 0; headersOk && i < expectedMainHeaders.Length; i++)
                            if (headers[i] != expectedMainHeaders[i])
                                headersOk = false;
                        bool noHeaderClip = true;
                        foreach (System.Windows.Forms.DataGridViewColumn col in view.GridForTest.Columns)
                        {
                            int needW = System.Windows.Forms.TextRenderer.MeasureText(col.HeaderText, view.HeadingFontForTest).Width;
                            if (col.Width < needW)
                                noHeaderClip = false;
                        }
                        bool treeOk = view.GroupTreeForTest.Nodes.Count == 1
                            && view.GroupTreeForTest.Nodes[0].Text == "Tất cả"
                            && view.FindGroupNodeById(grpDrinkId) != null
                            && view.FindGroupNodeById(grpDrinkSubId) != null
                            && view.FindGroupNodeById(grpDrinkSubId).Parent == view.FindGroupNodeById(grpDrinkId)
                            && view.FindGroupNodeById(grpCakeId) != null
                            && view.FindGroupNodeById(grpMatId) == null
                            && !TreeContainsText(view.GroupTreeForTest.Nodes, "Chưa thiết lập");
                        bool storeBlankFirst = view.StoreComboForTest.Items.Count >= 3
                            && ((StoreRecord)view.StoreComboForTest.Items[0]).Id == ""
                            && ((StoreRecord)view.StoreComboForTest.Items[0]).Name == "";
                        bool radioDefaultCost = view.RadioCostPriceForTest.Checked && !view.RadioBuyPriceForTest.Checked
                            && view.RadioCostPriceForTest.Text == "Theo giá vốn" && view.RadioBuyPriceForTest.Text == "Theo giá nhập";

                        Check("Thống kê MH bán UI (§6 mở màn): ngày mặc định theo giờ DB, 11 cột đúng thứ tự không cắt chữ, cây nhóm đúng phân cấp & bỏ nguyên liệu/Chưa thiết lập, combo có mục trống, radio mặc định Theo giá vốn",
                            view.FromDateValue.Date == dbToday && view.ToDateValue.Date == dbToday
                            && headersOk && noHeaderClip && treeOk && storeBlankFirst && radioDefaultCost
                            && view.GridForTest.ReadOnly && view.GridForTest.RowTemplate.Height >= 32
                            && view.NetProfitValueLabelForTest.ForeColor == Theme.ThemeManager.Current.Danger
                            && view.NetProfitTooltipForTest.IndexOf("Thuế", StringComparison.OrdinalIgnoreCase) >= 0,
                            "db=" + dbToday.ToString("yyyy-MM-dd") + ", headers=" + headersOk + ", tree=" + treeOk + ", store0=" + storeBlankFirst);

                        // Đổi Từ/Đến qua sự kiện thật Inner.Text + Commit() → tự tải lại 5 dòng và đúng khối tổng 223,000.
                        view.FromDateBoxForTest.Inner.Text = "10/07/2002";
                        view.FromDateBoxForTest.Commit();
                        view.ToDateBoxForTest.Inner.Text = "11/07/2002";
                        view.ToDateBoxForTest.Commit();
                        Check("Thống kê MH bán UI (§6 sự kiện đổi Từ/Đến): tải lại ngay, đúng 5 dòng và khối tổng khớp số tay",
                            view.GridRowCount == 5
                            && view.GetSummaryText("sales") == 365000m.ToString("n0", vi)
                            && view.GetSummaryText("cost") == 150000m.ToString("n0", vi)
                            && view.GetSummaryText("gross") == 215000m.ToString("n0", vi)
                            && view.GetSummaryText("discount") == 27000m.ToString("n0", vi)
                            && view.GetSummaryText("tax") == 19000m.ToString("n0", vi)
                            && view.GetSummaryText("service") == 10000m.ToString("n0", vi)
                            && view.GetSummaryText("time") == 6000m.ToString("n0", vi)
                            && view.GetSummaryText("net") == 223000m.ToString("n0", vi),
                            "rows=" + view.GridRowCount + ", net=" + view.GetSummaryText("net"));

                        // Chọn nhóm cha grpDrink trên cây (sự kiện thật AfterSelect): CHỈ lấy đúng nhóm đó (2 dòng prodA), KHÔNG gồm nhóm con grpDrinkSub (prodB), và KHÔNG đổi khối tổng!
                        System.Windows.Forms.TreeNode nodeDrink = view.FindGroupNodeById(grpDrinkId);
                        view.GroupTreeForTest.SelectedNode = nodeDrink;
                        bool exactGroupOnly = view.GridRowCount == 2
                            && view.CurrentSummaryForTest.NetProfit == 223000m
                            && view.GetSummaryText("net") == 223000m.ToString("n0", vi);

                        // Gõ ô Lọc chữ Việt không phân biệt hoa thường (sự kiện thật TextChanged): lọc theo TENHANG, không đổi khối tổng.
                        view.GroupTreeForTest.SelectedNode = view.GroupTreeForTest.Nodes[0]; // Tất cả
                        view.FilterBoxForTest.Text = "TRÀ ĐÀO";
                        bool filterViOk = view.GridRowCount == 1
                            && Convert.ToString(view.GridForTest.Rows[0].Cells["colTenHang"].FormattedValue) == "TKMH-Trà đào cam sả"
                            && view.GetSummaryText("net") == 223000m.ToString("n0", vi);

                        // Chọn lại grpDrink, xóa ô lọc, bấm Refresh (F5) bằng PerformClick() → giữ nguyên bộ lọc và giữ nhóm đang chọn (P4).
                        view.FilterBoxForTest.Text = "";
                        view.GroupTreeForTest.SelectedNode = view.FindGroupNodeById(grpDrinkId);
                        view.RefreshButtonForTest.PerformClick();
                        bool refreshKeepsGroup = view.GroupTreeForTest.SelectedNode != null
                            && string.Equals(view.GroupTreeForTest.SelectedNode.Tag as string, grpDrinkId, StringComparison.Ordinal)
                            && view.GridRowCount == 2
                            && view.FromDateValue == from && view.ToDateValue == to;

                        Check("Thống kê MH bán UI (§6/P4 cây nhóm, ô Lọc, nút Refresh): chọn nhóm khớp chính xác (không nhóm con), ô Lọc tiếng Việt không đổi khối tổng, Refresh giữ nhóm đang chọn",
                            exactGroupOnly && filterViOk && refreshKeepsGroup,
                            "grp=" + exactGroupOnly + ", filter=" + filterViOk + ", refKeep=" + refreshKeepsGroup);

                        // Đổi radio sang "Theo giá nhập" qua sự kiện thật CheckedChanged → đổi cột Giá vốn/Thành tiền nhập và khối tổng (152,000 / 221,000).
                        view.GroupTreeForTest.SelectedNode = view.GroupTreeForTest.Nodes[0];
                        view.RadioBuyPriceForTest.Checked = true;
                        bool buyPriceModeOk = view.GridRowCount == 5
                            && view.CurrentSummaryForTest.TotalCost == 152000m
                            && view.CurrentSummaryForTest.NetProfit == 221000m
                            && view.GetSummaryText("cost") == 152000m.ToString("n0", vi)
                            && view.GetSummaryText("net") == 221000m.ToString("n0", vi);

                        // Đổi lại "Theo giá vốn" và chọn cửa hàng storeA qua SelectedIndex (sự kiện thật SelectedIndexChanged) → khối tổng đổi sang storeA (198,000).
                        view.RadioCostPriceForTest.Checked = true;
                        int storeAIdx = -1;
                        for (int i = 0; i < view.StoreComboForTest.Items.Count; i++)
                        {
                            var sr = view.StoreComboForTest.Items[i] as StoreRecord;
                            if (sr != null && sr.Id == storeAId) { storeAIdx = i; break; }
                        }
                        if (storeAIdx >= 0)
                            view.StoreComboForTest.SelectedIndex = storeAIdx;
                        bool storeSwitchOk = storeAIdx > 0
                            && view.CurrentSummaryForTest.TotalSalesBeforeDiscount == 335000m
                            && view.CurrentSummaryForTest.TotalCost == 140000m
                            && view.CurrentSummaryForTest.NetProfit == 198000m;

                        // Bấm nút In báo cáo bằng PerformClick() → đi qua luồng in P0 với interface giả (không bật hộp thoại/máy in thật).
                        var printStub = new PrintOutputRecorder();
                        view.PrintOutputHandlerForTest = printStub;
                        view.PrintTemplatePickerForTest = delegate (System.Windows.Forms.IWin32Window owner, IList<PrintTemplateRecord> templates)
                        {
                            return templates != null && templates.Count > 0
                                ? new PrintSelectionResult { Template = templates[templates.Count - 1], Action = PrintActionKind.Preview }
                                : null;
                        };
                        view.PrintButtonForTest.PerformClick();
                        bool printFlowOk = printStub.PreviewCount == 1
                            && printStub.PrintCount == 0
                            && printStub.ErrorMessages.Count == 0;
                        view.PrintOutputHandlerForTest = null;
                        view.PrintTemplatePickerForTest = null;

                        // Tên ca giữ nguyên để so mốc; nội dung kiểm đã đổi theo spec in (kiểm nút In đi qua luồng in với interface giả).
                        Check("Thống kê MH bán UI (§6 sự kiện radio, combo cửa hàng, nút In): đổi radio đổi giá & tổng, đổi cửa hàng tải lại ngay, nút In báo chưa hỗ trợ",
                            buyPriceModeOk && storeSwitchOk && printFlowOk,
                            "buy=" + buyPriceModeOk + ", store=" + storeSwitchOk + ", print=" + printFlowOk);

                        // 6. Dialog chi tiết (mở từ dòng prodA @ 30,000 của admin không chọn cửa hàng): Show ngoài màn hình, kiểm 22+11 cột, chọn bill đầu, đổi bill bằng CurrentCell, màu + chữ đậm thật sau CellFormatting (kể cả khi đang chọn dòng).
                        view.StoreComboForTest.SelectedIndex = 0;
                        int idxProdA30 = -1;
                        int idxProdC = -1;
                        for (int i = 0; i < view.GridForTest.Rows.Count; i++)
                        {
                            var r = view.GridForTest.Rows[i].Tag as ItemSalesRow;
                            if (r != null && r.ProductId == prodAId && r.UnitPrice == 30000m) idxProdA30 = i;
                            if (r != null && r.ProductId == prodCId) idxProdC = i;
                        }

                        // Phiên admin: chọn dòng prodC (Đơn giá 40000; 5 số được bảo vệ là 80000, 25000, 50000, 30000, 37.5 — không trùng số hiển thị) → chuỗi sao chép vẫn có đủ giá vốn.
                        bool adminSelC = view.SelectRow(idxProdC);
                        string adminCopyC = Controls.DetailGridMenu.BuildSelectionCopyText(view.GridForTest) ?? "";
                        Check("Thống kê MH bán UI (§2.1/DetailGridMenu phiên admin): chuỗi sao chép vùng chọn có giá vốn và số liệu lãi",
                            adminSelC
                            && adminCopyC.IndexOf("25000", StringComparison.Ordinal) >= 0
                            && adminCopyC.IndexOf("80000", StringComparison.Ordinal) >= 0
                            && adminCopyC.IndexOf("50000", StringComparison.Ordinal) >= 0
                            && adminCopyC.IndexOf("30000", StringComparison.Ordinal) >= 0
                            && adminCopyC.IndexOf("37.5", StringComparison.Ordinal) >= 0,
                            "copy=" + adminCopyC);

                        string[] expectedBillHeaders =
                        {
                            "Ghi chú", "Số phiếu", "Ngày", "Khách hàng", "Mã khách", "Địa chỉ", "Điện thoại",
                            "Tổng cộng", "Phí vận chuyển", "Giảm giá", "% giảm giá", "Thuế", "% thuế",
                            "Tiền hàng", "Diễn giải", "Giờ thanh toán", "Khách đưa", "Trả lại",
                            "Thanh toán bởi", "Số phiếu đặt hàng", "Giao hàng", "Đổi trả"
                        };
                        string[] expectedLineHeaders =
                        {
                            "Ghi chú", "Tên mặt hàng", "ĐVT", "Mã hàng", "Bảo hành", "Mã sẵn có",
                            "Bảo hành (dòng)", "CK%", "Thành tiền", "Đơn giá", "S.lượng"
                        };

                        using (Dialogs.ItemSalesDetailDialog dlg = view.CreateDetailDialogForRow(idxProdA30))
                        {
                            dlg.SuppressErrorPopup = true;
                            dlg.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                            dlg.ShowInTaskbar = false;
                            dlg.Location = new System.Drawing.Point(-20000, -20000);
                            dlg.Show();
                            System.Windows.Forms.Application.DoEvents();
                            try
                            {
                                List<string> bh = dlg.BillColumnHeaders();
                                bool bhOk = bh.Count == expectedBillHeaders.Length;
                                for (int i = 0; bhOk && i < expectedBillHeaders.Length; i++)
                                    if (bh[i] != expectedBillHeaders[i]) bhOk = false;

                                List<string> lh = dlg.LineColumnHeaders();
                                bool lhOk = lh.Count == expectedLineHeaders.Length;
                                for (int i = 0; lhOk && i < expectedLineHeaders.Length; i++)
                                    if (lh[i] != expectedLineHeaders[i]) lhOk = false;

                                // 3 bill hợp lệ sắp tăng theo GIOTHANHTOAN: billA1 (09:30), billB1 (11:15), billA2 (15:45); bill đầu chọn sẵn.
                                bool billsOrderAndFirst = dlg.BillRowCount == 3
                                    && dlg.FindBillRowIndexById(billA1Id) == 0
                                    && dlg.FindBillRowIndexById(billB1Id) == 1
                                    && dlg.FindBillRowIndexById(billA2Id) == 2
                                    && dlg.BillsGridForTest.SelectedRows.Count == 1
                                    && dlg.BillsGridForTest.SelectedRows[0].Index == 0;

                                // Lưới dưới của billA1: 4 dòng (gồm prodMat nguyên liệu, loại xuất vật tư & combo con).
                                int lineProdAIdx = dlg.FindLineRowIndexByProductId(prodAId);
                                int lineProdBIdx = dlg.FindLineRowIndexByProductId(prodBId);
                                int lineMatIdx = dlg.FindLineRowIndexByProductId(prodMatId);
                                bool linesBillA1Ok = dlg.LineRowCount == 4 && lineProdAIdx >= 0 && lineProdBIdx >= 0 && lineMatIdx >= 0;

                                // Kiểm màu & chữ đậm thật sau CellFormatting (kể cả khi dòng đang chọn!).
                                Theme.ThemeTokens curTheme = Theme.ThemeManager.Current;
                                System.Windows.Forms.DataGridViewCellStyle stHit = dlg.CaptureLineCellStyle(lineProdAIdx, 1);
                                dlg.LinesGridForTest.CurrentCell = dlg.LinesGridForTest.Rows[lineProdAIdx].Cells[1];
                                dlg.LinesGridForTest.Rows[lineProdAIdx].Selected = true;
                                System.Windows.Forms.DataGridViewCellStyle stHitSelected = dlg.CaptureLineCellStyle(lineProdAIdx, 1);
                                System.Windows.Forms.DataGridViewCellStyle stOther = dlg.CaptureLineCellStyle(lineProdBIdx, 1);

                                bool highlightStyleOk = stHit != null && stHit.ForeColor == curTheme.Danger && stHit.Font != null && stHit.Font.Bold
                                    && stHitSelected != null && stHitSelected.SelectionForeColor == curTheme.Danger && stHitSelected.ForeColor == curTheme.Danger && stHitSelected.Font != null && stHitSelected.Font.Bold
                                    && stOther != null && stOther.ForeColor == curTheme.Text && stOther.Font != null && !stOther.Font.Bold;

                                // Đổi bill trên lưới trên qua CurrentCell → SelectionChanged tự nạp chi tiết billB1 (1 dòng) và billA2 (kiểm SLXUAT nguyên giá trị = 3, không trừ SLNHAP).
                                dlg.SelectBillRow(1);
                                int linesBillB1 = dlg.LineRowCount;
                                dlg.SelectBillRow(2);
                                int lineA2ProdA = dlg.FindLineRowIndexByProductId(prodAId);
                                string slXuatRaw = lineA2ProdA >= 0 ? Convert.ToString(dlg.LinesGridForTest.Rows[lineA2ProdA].Cells["colLineSoLuong"].FormattedValue) : "";

                                Check("Thống kê MH bán Dialog (§6/§2.0 chi tiết): đúng 22+11 cột, chọn bill đầu sắp GIOTHANHTOAN, giữ nguyên liệu/loại vật tư+combo, tô đỏ đậm thật kể cả khi chọn, đổi bill qua sự kiện, SLXUAT nguyên giá trị",
                                    bhOk && lhOk && billsOrderAndFirst && linesBillA1Ok && highlightStyleOk
                                    && linesBillB1 == 1 && slXuatRaw == 3m.ToString("#,##0.##", vi)
                                    && dlg.BillsGridForTest.ReadOnly && dlg.LinesGridForTest.ReadOnly
                                    && dlg.ExitButtonForTest.Text == "Thoát",
                                    "bh=" + bhOk + ", lh=" + lhOk + ", ord=" + billsOrderAndFirst + ", linesA1=" + dlg.LineRowCount
                                    + ", hl=" + highlightStyleOk + ", linesB1=" + linesBillB1 + ", slRaw=" + slXuatRaw);
                            }
                            finally
                            {
                                dlg.Close();
                            }
                        }
                    }
                    finally
                    {
                        hostForm.Close();
                    }
                }

                // 7. Quyền Xem giá nhập: dựng view bằng session KHÔNG có quyền Xem giá nhập (trong khi AppSession.Current vẫn là admin!)
                // → kiểm 5 cột thật sự ẩn, 6 cột còn lại hiện, khối tổng ẩn, nút In ẩn.
                AppSession.Current = adminSession;
                var noCostPerms = new FunctionPermissions(false);
                noCostPerms.Add(ItemSalesConstants.FunctionId, ItemSalesConstants.FunctionName, RoleModes.View);
                var noCostSession = new UserSession
                {
                    UserId = userAId,
                    UserName = "tkmh_user_a",
                    DisplayName = "TKMH Thu ngân A",
                    IsAdmin = false,
                    DatabasePath = db,
                    Permissions = noCostPerms
                };
                using (var hostNoCost = new System.Windows.Forms.Form
                {
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new System.Drawing.Point(-20000, -20000),
                    ClientSize = new System.Drawing.Size(1400, 780)
                })
                {
                    var viewNoCost = new ItemSalesView(noCostSession)
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        SuppressErrorPopup = true
                    };
                    hostNoCost.Controls.Add(viewNoCost);
                    hostNoCost.Show();
                    System.Windows.Forms.Application.DoEvents();
                    try
                    {
                        bool hidden5 = !viewNoCost.GridForTest.Columns["colThanhTien"].Visible
                            && !viewNoCost.GridForTest.Columns["colGiaVon"].Visible
                            && !viewNoCost.GridForTest.Columns["colThanhTienNhap"].Visible
                            && !viewNoCost.GridForTest.Columns["colLai"].Visible
                            && !viewNoCost.GridForTest.Columns["colTiLeLai"].Visible;
                        bool visible6 = viewNoCost.GridForTest.Columns["colTenHang"].Visible
                            && viewNoCost.GridForTest.Columns["colSoLuong"].Visible
                            && viewNoCost.GridForTest.Columns["colDvt"].Visible
                            && viewNoCost.GridForTest.Columns["colDonGia"].Visible
                            && viewNoCost.GridForTest.Columns["colTienGiam"].Visible
                            && viewNoCost.GridForTest.Columns["colTiLeGiamGia"].Visible;
                        // Combo cửa hàng của userA chỉ có mục trống + storeA (không có storeB).
                        bool userAStoreLookupOk = viewNoCost.StoreComboForTest.Items.Count == 2
                            && ((StoreRecord)viewNoCost.StoreComboForTest.Items[0]).Id == ""
                            && ((StoreRecord)viewNoCost.StoreComboForTest.Items[1]).Id == storeAId;

                        Check("Thống kê MH bán UI (§6/§2.1 quyền Xem giá nhập): không có quyền ẩn đúng 5 cột, khối tổng và nút In (dùng đúng phiên của view); lookup chỉ liệt kê cửa hàng của user",
                            hidden5 && visible6 && !viewNoCost.BottomPanelForTest.Visible && !viewNoCost.PrintButtonForTest.Visible && userAStoreLookupOk,
                            "h5=" + hidden5 + ", v6=" + visible6 + ", bot=" + viewNoCost.BottomPanelForTest.Visible
                            + ", prn=" + viewNoCost.PrintButtonForTest.Visible + ", stores=" + viewNoCost.StoreComboForTest.Items.Count);

                        // Chống lộ dữ liệu qua sao chép/ô lưới khi không có quyền Xem giá nhập (§2.1):
                        // Nạp dữ liệu 10/07/2002 → 11/07/2002 qua sự kiện thật; chọn dòng prodC (Đơn giá 40000; 5 số bị cấm là 80000, 25000, 50000, 30000, 37.5 — không trùng 2 hay 40000).
                        viewNoCost.FromDateBoxForTest.Inner.Text = "10/07/2002";
                        viewNoCost.FromDateBoxForTest.Commit();
                        viewNoCost.ToDateBoxForTest.Inner.Text = "11/07/2002";
                        viewNoCost.ToDateBoxForTest.Commit();

                        int idxNoCostC = -1;
                        int idxNoCostA30 = -1;
                        for (int i = 0; i < viewNoCost.GridForTest.Rows.Count; i++)
                        {
                            var k = viewNoCost.GridForTest.Rows[i].Tag as ItemSalesDetailKey;
                            if (k != null && k.ProductId == prodCId) idxNoCostC = i;
                            if (k != null && k.ProductId == prodAId && k.UnitPrice == 30000m) idxNoCostA30 = i;
                        }
                        bool noCostSelC = viewNoCost.SelectRow(idxNoCostC);
                        string noCostCopyHidden = Controls.DetailGridMenu.BuildSelectionCopyText(viewNoCost.GridForTest) ?? "";
                        bool copyHasAllowedOnly = noCostCopyHidden.IndexOf("TKMH-Bánh tiramisu", StringComparison.Ordinal) >= 0
                            && noCostCopyHidden.IndexOf("40000", StringComparison.Ordinal) >= 0
                            && noCostCopyHidden.IndexOf("80000", StringComparison.Ordinal) < 0
                            && noCostCopyHidden.IndexOf("25000", StringComparison.Ordinal) < 0
                            && noCostCopyHidden.IndexOf("50000", StringComparison.Ordinal) < 0
                            && noCostCopyHidden.IndexOf("30000", StringComparison.Ordinal) < 0
                            && noCostCopyHidden.IndexOf("37.5", StringComparison.Ordinal) < 0
                            && noCostCopyHidden.IndexOf("37,5", StringComparison.Ordinal) < 0;

                        // Bật 5 cột bị cấm lên bằng code: giá trị 5 ô vẫn rỗng (null / "") và sao chép vẫn không lộ số bị cấm.
                        string[] protectedCols = { "colThanhTien", "colGiaVon", "colThanhTienNhap", "colLai", "colTiLeLai" };
                        bool cellsEmptyEvenWhenUnhidden = true;
                        foreach (string colName in protectedCols)
                            viewNoCost.GridForTest.Columns[colName].Visible = true;
                        foreach (string colName in protectedCols)
                        {
                            System.Windows.Forms.DataGridViewCell cell = viewNoCost.GridForTest.Rows[idxNoCostC].Cells[colName];
                            if (cell.Value != null || !string.IsNullOrEmpty(Convert.ToString(cell.FormattedValue)))
                                cellsEmptyEvenWhenUnhidden = false;
                        }
                        string noCostCopyForcedVisible = Controls.DetailGridMenu.BuildSelectionCopyText(viewNoCost.GridForTest) ?? "";
                        bool stillNoLeakWhenUnhidden = noCostCopyForcedVisible.IndexOf("80000", StringComparison.Ordinal) < 0
                            && noCostCopyForcedVisible.IndexOf("25000", StringComparison.Ordinal) < 0
                            && noCostCopyForcedVisible.IndexOf("50000", StringComparison.Ordinal) < 0
                            && noCostCopyForcedVisible.IndexOf("30000", StringComparison.Ordinal) < 0
                            && noCostCopyForcedVisible.IndexOf("37.5", StringComparison.Ordinal) < 0;
                        foreach (string colName in protectedCols)
                            viewNoCost.GridForTest.Columns[colName].Visible = false;

                        // Nhấp đúp (mở dialog cho dòng prodA @ 30000) khi không có quyền Xem giá nhập vẫn ra đúng 2 hóa đơn của storeA.
                        bool noCostDetailOk = false;
                        using (Dialogs.ItemSalesDetailDialog dlgNoCost = viewNoCost.CreateDetailDialogForRow(idxNoCostA30))
                        {
                            if (dlgNoCost != null)
                            {
                                dlgNoCost.SuppressErrorPopup = true;
                                dlgNoCost.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                                dlgNoCost.ShowInTaskbar = false;
                                dlgNoCost.Location = new System.Drawing.Point(-20000, -20000);
                                dlgNoCost.Show();
                                System.Windows.Forms.Application.DoEvents();
                                try
                                {
                                    noCostDetailOk = dlgNoCost.BillRowCount == 2
                                        && dlgNoCost.FindBillRowIndexById(billA1Id) == 0
                                        && dlgNoCost.FindBillRowIndexById(billA2Id) == 1;
                                }
                                finally { dlgNoCost.Close(); }
                            }
                        }

                        Check("Thống kê MH bán UI (§2.1 chống lộ dữ liệu khi không có quyền Xem giá nhập): sao chép không chứa 5 số bị cấm, 5 ô rỗng kể cả khi bật cột bằng code, mở dialog vẫn đúng 2 bill",
                            noCostSelC && copyHasAllowedOnly && cellsEmptyEvenWhenUnhidden && stillNoLeakWhenUnhidden && noCostDetailOk,
                            "copy=" + noCostCopyHidden + ", empty5=" + cellsEmptyEvenWhenUnhidden + ", noLeak=" + stillNoLeakWhenUnhidden + ", dlg=" + noCostDetailOk);
                    }
                    finally
                    {
                        hostNoCost.Close();
                    }
                }

                // 7b. DetailGridMenu (dùng chung) trên lưới thường có cột ẩn: bỏ qua cột Visible = false cho cả sao chép vùng chọn và sao chép ô, không sinh dòng trống.
                using (var hostGridTest = new System.Windows.Forms.Form
                {
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new System.Drawing.Point(-20000, -20000),
                    ClientSize = new System.Drawing.Size(600, 300)
                })
                {
                    var plainGrid = new System.Windows.Forms.DataGridView
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        ReadOnly = true,
                        AllowUserToAddRows = false,
                        RowHeadersVisible = false,
                        SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect,
                        MultiSelect = true
                    };
                    plainGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "cVis1", HeaderText = "Hiện 1", Visible = true });
                    plainGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "cSecret", HeaderText = "Ẩn", Visible = false });
                    plainGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "cVis2", HeaderText = "Hiện 2", Visible = true });
                    plainGrid.Rows.Add("VIS_A1", "SECRET_73915", "VIS_B1");
                    plainGrid.Rows.Add("VIS_A2", "SECRET_84269", "VIS_B2");
                    hostGridTest.Controls.Add(plainGrid);
                    hostGridTest.Show();
                    System.Windows.Forms.Application.DoEvents();
                    try
                    {
                        plainGrid.ClearSelection();
                        plainGrid.CurrentCell = plainGrid.Rows[0].Cells["cVis1"];
                        plainGrid.Rows[0].Selected = true;
                        plainGrid.Rows[1].Selected = true;
                        string fullRowCopy = Controls.DetailGridMenu.BuildSelectionCopyText(plainGrid);
                        bool fullRowOk = fullRowCopy == "VIS_A1\tVIS_B1\r\nVIS_A2\tVIS_B2"
                            && fullRowCopy.IndexOf("SECRET_73915", StringComparison.Ordinal) < 0
                            && fullRowCopy.IndexOf("SECRET_84269", StringComparison.Ordinal) < 0;

                        // Chọn dòng 0 chỉ ô ẩn + dòng 1 ô hiện → không sinh dòng trống cho dòng 0.
                        plainGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
                        plainGrid.ClearSelection();
                        plainGrid.Rows[0].Cells["cSecret"].Selected = true;
                        plainGrid.Rows[1].Cells["cVis1"].Selected = true;
                        string mixedCopy = Controls.DetailGridMenu.BuildSelectionCopyText(plainGrid);
                        bool noBlankLineForHiddenRow = mixedCopy == "VIS_A2";

                        // Chọn chỉ ô ẩn → trả null và CopySelection không ném ngoại lệ clipboard.
                        plainGrid.ClearSelection();
                        plainGrid.Rows[0].Cells["cSecret"].Selected = true;
                        string onlyHiddenCopy = Controls.DetailGridMenu.BuildSelectionCopyText(plainGrid);
                        bool copyHiddenNoThrow = true;
                        try { Controls.DetailGridMenu.CopySelection(plainGrid); }
                        catch (Exception) { copyHiddenNoThrow = false; }

                        // Kiểm CopyCell: ô hiện trả đúng giá trị; khi ẩn các cột hiện thì không còn ô hợp lệ để chép và CopyCell không ném lỗi.
                        plainGrid.CurrentCell = plainGrid.Rows[0].Cells["cVis1"];
                        string visCellText = Controls.DetailGridMenu.BuildCellCopyText(plainGrid);
                        plainGrid.Columns["cVis1"].Visible = false;
                        plainGrid.Columns["cVis2"].Visible = false;
                        string hiddenCurrentCellText = Controls.DetailGridMenu.BuildCellCopyText(plainGrid);
                        bool copyCellNoThrow = true;
                        try { Controls.DetailGridMenu.CopyCell(plainGrid); }
                        catch (Exception) { copyCellNoThrow = false; }

                        Check("DetailGridMenu (lưới có cột ẩn trên Form đã Show): sao chép vùng chọn và sao chép ô bỏ qua cột Visible=false, không sinh dòng trống, không lỗi clipboard khi rỗng",
                            fullRowOk && noBlankLineForHiddenRow && onlyHiddenCopy == null && copyHiddenNoThrow
                            && visCellText == "VIS_A1" && hiddenCurrentCellText == null && copyCellNoThrow,
                            "full=" + (fullRowCopy ?? "null") + ", mixed=" + (mixedCopy ?? "null") + ", hiddenCell=" + (hiddenCurrentCellText ?? "null"));
                    }
                    finally
                    {
                        hostGridTest.Close();
                    }
                }

                // 8. Preview forms --render-screen preview.itemsales và preview.itemsalesdetail.
                using (System.Windows.Forms.Form pvMainForm = Dialogs.ScreenFactory.Create("preview.itemsales", adminSession))
                {
                    var pvMain = pvMainForm as Dialogs.ItemSalesPreviewForm;
                    bool pvMainOk = false;
                    if (pvMain != null)
                    {
                        pvMain.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                        pvMain.ShowInTaskbar = false;
                        pvMain.Location = new System.Drawing.Point(-20000, -20000);
                        pvMain.Show();
                        System.Windows.Forms.Application.DoEvents();
                        try
                        {
                            pvMain.Preview("from=10/07/2002");
                            pvMain.Preview("to=11/07/2002");
                            pvMain.Preview("select=1");
                            pvMainOk = pvMain.ViewForTest.GridRowCount == 5
                                && pvMain.ViewForTest.GridForTest.SelectedRows.Count == 1
                                && pvMain.ViewForTest.GridForTest.SelectedRows[0].Index == 1;
                        }
                        finally { pvMain.Close(); }
                    }
                    Check("Thống kê MH bán (preview.itemsales): Show form chụp màn hình và from/to/select=1 hoạt động qua sự kiện thật", pvMainOk);
                }

                using (System.Windows.Forms.Form pvDetailForm = Dialogs.ScreenFactory.Create("preview.itemsalesdetail", adminSession))
                {
                    var pvDetail = pvDetailForm as Dialogs.ItemSalesDetailDialog;
                    bool pvDetailOk = false;
                    if (pvDetail != null)
                    {
                        pvDetail.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                        pvDetail.ShowInTaskbar = false;
                        pvDetail.Location = new System.Drawing.Point(-20000, -20000);
                        pvDetail.Show();
                        System.Windows.Forms.Application.DoEvents();
                        try
                        {
                            pvDetail.Preview("from=10/07/2002");
                            pvDetail.Preview("to=11/07/2002");
                            pvDetailOk = pvDetail.BillRowCount >= 1 && pvDetail.LineRowCount >= 1;
                        }
                        finally { pvDetail.Close(); }
                    }
                    Check("Thống kê MH bán (preview.itemsalesdetail): Show dialog chụp màn hình nạp được hóa đơn & chi tiết", pvDetailOk);
                }
            }
            finally
            {
                // Hoàn lại phiên, cấu hình và dọn con trước cha: dòng bill → bill → đặt hàng → khách → mặt hàng → nhóm → ĐVT → liên kết CH → user → cửa hàng.
                AppSession.Current = savedAppSession;
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ItemSalesConstants.ConfigSortOrderKey, DataType = 3, IntValue = origSortOrder },
                    new ConfigChange { Name = ItemSalesConstants.ConfigUseCodeKey, DataType = 3, IntValue = origUseCode }
                }, "selftest");
                SystemConfig.Reload(db);

                foreach (string lid in lineIds)
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE ID = @I", "@I", lid);
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID IN (@A, @B, @C, @D, @E, @F, @G)",
                    "@A", billA1Id, "@B", billA2Id, "@C", billB1Id,
                    "@D", billUnpaidId, "@E", billDeletedId, "@F", billImportId, "@G", billNextDayId);
                BookingExec(db, "DELETE FROM TDATHANG WHERE ID = @I", "@I", bookingId);
                BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", custId);
                BookingExec(db, "DELETE FROM DMATHANG WHERE ID IN (@A, @B, @C, @D, @E)",
                    "@A", prodAId, "@B", prodBId, "@C", prodCId, "@D", prodDId, "@E", prodMatId);
                BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", grpDrinkSubId);
                BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID IN (@A, @B, @C)", "@A", grpDrinkId, "@B", grpCakeId, "@C", grpMatId);
                BookingExec(db, "DELETE FROM DDONVITINH WHERE ID = @I", "@I", unitId);
                BookingExec(db, "DELETE FROM TNGUOIDUNGTHEOCUAHANG WHERE ID = @I", "@I", userStoreLinkId);
                BookingExec(db, "DELETE FROM SUSER WHERE ID IN (@A, @B)", "@A", userAId, "@B", userNoStoreId);
                BookingExec(db, "DELETE FROM DCUAHANG WHERE ID IN (@A, @B)", "@A", storeAId, "@B", storeBId);
            }
        }

        private static void RevenueReportTests(string db)
        {
            var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

            // 1. RevenueSummaryCalculator thuần decimal (§2.3, §6, §7 — ≥ 8 ca tính tay).
            // Ca đối chiếu nguyên văn: tiền mặt 100.000, thẻ 50.000, voucher 20.000, công nợ 30.000, thu 40.000, chi 15.000 → TỔNG THỰC THU = 175.000.
            RevenueSummary refSum = RevenueSummaryCalculator.Calculate(
                new[]
                {
                    new RevenueBillRow { TienMat = 100000m, The = 50000m, Voucher = 20000m, CongNo = 30000m }
                },
                40000m,
                15000m);
            Check("Thống kê doanh thu (Calculator ví dụ đối chiếu): TỔNG THỰC THU = 175.000, không cộng voucher/công nợ",
                refSum.Cash == 100000m && refSum.Card == 50000m && refSum.Voucher == 20000m && refSum.Debt == 30000m
                && refSum.OtherIncome == 40000m && refSum.OtherExpense == 15000m && refSum.NetActualReceipt == 175000m);

            // Các khoản không thực thu (voucher, trừ tích lũy, thẻ trả trước, công nợ) không cộng vào TỔNG THỰC THU.
            RevenueSummary nonActualSum = RevenueSummaryCalculator.Calculate(
                new[]
                {
                    new RevenueBillRow { Voucher = 20000m, TruTichLuy = 12000m, TheTraTruoc = 18000m, CongNo = 30000m }
                },
                0m,
                0m);
            Check("Thống kê doanh thu (Calculator không cộng khoản phi tiền mặt): VOUCHER/TRỪ TÍCH LŨY/THẺ TRẢ TRƯỚC/CÔNG NỢ không vào TỔNG THỰC THU",
                nonActualSum.Voucher == 20000m && nonActualSum.LoyaltyDeduction == 12000m
                && nonActualSum.PrepaidCard == 18000m && nonActualSum.Debt == 30000m
                && nonActualSum.NetActualReceipt == 0m);

            // 3 bill đủ trường hợp giảm giá hàng + mặt hàng + giảm giá giờ khác 0 + phí DV + thuế + các hình thức thanh toán + thu/chi.
            RevenueSummary threeBillSum = RevenueSummaryCalculator.Calculate(
                new[]
                {
                    new RevenueBillRow
                    {
                        TienHangChuaGiam = 120000m, TienGio = 10000m, TienGiamGia = 15000m, GiamGiaMatHang = 5000m,
                        TienGiamGiaGio = 0m, PhiDichVu = 5000m, TienThue = 8000m, TongCong = 100000m, TienMat = 100000m
                    },
                    new RevenueBillRow
                    {
                        TienHangChuaGiam = 80000m, TienGio = 0m, TienGiamGia = 7000m, GiamGiaMatHang = 3000m,
                        TienGiamGiaGio = 4000m, PhiDichVu = 2000m, TienThue = 6000m, TongCong = 70000m, The = 50000m, Voucher = 20000m
                    },
                    null,
                    new RevenueBillRow
                    {
                        TienHangChuaGiam = 95000m, TienGio = 0m, TienGiamGia = 6000m, GiamGiaMatHang = 0m,
                        TienGiamGiaGio = 0m, PhiDichVu = 3000m, TienThue = 4000m, TongCong = 85000m,
                        ChuyenKhoan = 25000m, TruTichLuy = 12000m, TheTraTruoc = 18000m, CongNo = 30000m
                    }
                },
                new RevenueCashFlowTotals { OtherIncome = 40000m, OtherExpense = 15000m });
            Check("Thống kê doanh thu (Calculator 3 bill đủ nhóm §2.3): khớp tay từng dòng kể cả TỔNG GIẢM GIÁ gồm TIENGIAMGIAGIO",
                threeBillSum.GoodsAmount == 295000m
                && threeBillSum.TimeCharge == 10000m
                && threeBillSum.GoodsDiscount == 36000m
                && threeBillSum.TimeDiscount == 4000m
                && threeBillSum.TotalDiscount == 40000m
                && threeBillSum.ServiceFee == 10000m
                && threeBillSum.Tax == 18000m
                && threeBillSum.TotalRevenue == 255000m
                && threeBillSum.Cash == 100000m
                && threeBillSum.Card == 50000m
                && threeBillSum.BankTransfer == 25000m
                && threeBillSum.Voucher == 20000m
                && threeBillSum.LoyaltyDeduction == 12000m
                && threeBillSum.PrepaidCard == 18000m
                && threeBillSum.Debt == 30000m
                && threeBillSum.OtherIncome == 40000m
                && threeBillSum.OtherExpense == 15000m
                && threeBillSum.NetActualReceipt == 200000m);

            RevenueSummary emptyBillsWithCashFlow = RevenueSummaryCalculator.Calculate(null, 40000m, 15000m);
            Check("Thống kê doanh thu (Calculator không bill nhưng có thu/chi): TỔNG THỰC THU = THU − CHI",
                emptyBillsWithCashFlow.TotalRevenue == 0m && emptyBillsWithCashFlow.NetActualReceipt == 25000m);

            RevenueSummary negNet = RevenueSummaryCalculator.Calculate(
                new[] { new RevenueBillRow { TienMat = 10000m } }, 5000m, 50000m);
            Check("Thống kê doanh thu (Calculator chi lớn hơn thu): TỔNG THỰC THU âm đúng",
                negNet.NetActualReceipt == -35000m);

            RevenueSummary nullCashFlow = RevenueSummaryCalculator.Calculate(
                new[] { new RevenueBillRow { ChuyenKhoan = 80000m, TienGiamGiaGio = 7000m } }, null);
            Check("Thống kê doanh thu (Calculator CashFlow null & giảm giá giờ đơn lẻ): không lỗi, TỔNG GIẢM GIÁ = GIẢM GIÁ GIỜ",
                nullCashFlow.OtherIncome == 0m && nullCashFlow.OtherExpense == 0m
                && nullCashFlow.GoodsDiscount == 0m && nullCashFlow.TimeDiscount == 7000m
                && nullCashFlow.TotalDiscount == 7000m && nullCashFlow.NetActualReceipt == 80000m);

            RevenueSummary allZero = RevenueSummaryCalculator.Calculate(new RevenueBillRow[0], 0m, 0m);
            Check("Thống kê doanh thu (Calculator rỗng hoàn toàn): mọi tổng = 0",
                allZero.GoodsAmount == 0m && allZero.TotalDiscount == 0m && allZero.TotalRevenue == 0m && allZero.NetActualReceipt == 0m);

            Check("Thống kê doanh thu (Calculator định dạng tiền & giảm giá có dấu -): n0 vi-VN",
                RevenueSummaryCalculator.FormatMoney(175000m) == 175000m.ToString("n0", vi)
                && RevenueSummaryCalculator.FormatDiscount(40000m) == "-" + 40000m.ToString("n0", vi)
                && RevenueSummaryCalculator.FormatDiscount(0m) == "-0");

            // 2. Route ScreenFactory (không vào Map hộp thoại, đi qua CreateView theo MenuId) & phân quyền menu.
            var adminSession = new UserSession
            {
                UserId = "selftest_rev_admin",
                UserName = "Admin",
                DisplayName = "Administrator",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            Check("Thống kê doanh thu (§6 route): MenuId không nằm trong Map hộp thoại",
                !Dialogs.ScreenFactory.Has(RevenueReportView.MenuId));
            var menuNode = new MenuNode
            {
                Id = RevenueReportView.MenuId,
                Title = RevenueReportConstants.FunctionName,
                Operation = 4,
                StableDescId = null
            };
            using (System.Windows.Forms.Control created = Dialogs.ScreenFactory.CreateView(menuNode, adminSession))
            {
                Check("Thống kê doanh thu (§6 route): ScreenFactory.CreateView dựng RevenueReportView dù Operation=4 và không có StableDescId",
                    created is RevenueReportView);
            }

            var permsViewOnly = new FunctionPermissions(false);
            permsViewOnly.Add(RevenueReportConstants.FunctionId, RevenueReportConstants.FunctionName, RoleModes.View);
            var permsNone = new FunctionPermissions(false);
            MenuModel menuHas = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_rev_allow", UserName = "u_rev_allow", DisplayName = "Cho xem", IsAdmin = false,
                DatabasePath = db, Permissions = permsViewOnly
            });
            MenuModel menuNo = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_rev_deny", UserName = "u_rev_deny", DisplayName = "Chặn", IsAdmin = false,
                DatabasePath = db, Permissions = permsNone
            });
            Check("Thống kê doanh thu (§2.6 phân quyền menu): MainMenuBuilder hiện khi có quyền SFUNCTION và ẩn khi không có quyền",
                MenuTreeContainsId(menuHas.Roots, RevenueReportView.MenuId)
                && !MenuTreeContainsId(menuNo.Roots, RevenueReportView.MenuId));

            // 3. Seed dữ liệu mẫu (tiền tố TKDT-, tháng 8/2003) trên bản sao DB; dọn con trước cha trong finally.
            string storeAId = DbHelper.NewId();
            string storeBId = DbHelper.NewId();
            string userAId = DbHelper.NewId();
            string userNoStoreId = DbHelper.NewId();
            string userStoreLinkId = DbHelper.NewId();
            string tableAId = DbHelper.NewId();
            string custAId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prodNormalId = DbHelper.NewId();
            string prodMatId = DbHelper.NewId();

            string billA1Id = DbHelper.NewId();
            string billA2Id = DbHelper.NewId();
            string billA3Id = DbHelper.NewId();
            string billB1Id = DbHelper.NewId();
            string billEarlyId = DbHelper.NewId();
            string billUnpaidId = DbHelper.NewId();
            string billDeletedId = DbHelper.NewId();
            string billImportId = DbHelper.NewId();
            string billNextDayId = DbHelper.NewId();
            string billBeforeFromId = DbHelper.NewId();

            var lineIds = new List<string>();
            var voucherIds = new List<string>();
            Theme.ThemeTokens savedTheme = Theme.ThemeManager.Current;

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeAId, "@N", "TKDT-CH-A");
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeBId, "@N", "TKDT-CH-B");
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", userAId, "@N", "TKDT Thu ngân A", "@U", "tkdt_user_a");
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", userNoStoreId, "@N", "TKDT Không CH", "@U", "tkdt_user_none");
                BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", userStoreLinkId, "@U", userAId, "@C", storeAId);

                BookingExec(db, "INSERT INTO DBAN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", tableAId, "@N", "TKDT-Bàn 01");
                BookingExec(db, "INSERT INTO DKHACHHANG (ID, NAME, MAKHACH, DIACHI, STATUS, USERCREATEDID) VALUES (@I, @N, @M, @DC, 30, 'selftest')",
                    "@I", custAId, "@N", "TKDT Khách A", "@M", "TKDT-KH01", "@DC", "88 Lê Lợi");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", unitId, "@N", "TKDT-Ly");
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @U, '0', 45000, 30, 'selftest')",
                    "@I", prodNormalId, "@C", "TKDT-MH01", "@N", "TKDT-Trà đào", "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @U, '2', 5000, 30, 'selftest')",
                    "@I", prodMatId, "@C", "TKDT-NL01", "@N", "TKDT-Đường cát NL", "@U", unitId);

                // 3 bill hợp lệ ở Store A (khác hình thức thanh toán, đủ giảm giá + giảm giá giờ + phí DV + thuế + NULL cột tiền + NULL khóa ngoại + 15:30 ngày Đến):
                // billA1: 10/08/2003 09:00, GIOTHANHTOAN 09:25, tiền mặt 100k.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, BATDAU, KETTHUC, GIOTHANHTOAN, " +
                    "DCUAHANGID, DBANID, DKHACHHANGID, USERTHANHTOANID, " +
                    "TIENHANGCHUAGIAM, TIENGIO, TIENGIAMGIA, GIAMGIAMATHANG, TIENGIAMGIAGIO, PHIDICHVU, TIENTHUE, TONGCONG, " +
                    "TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, CONGNO, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-A1', 'TKDT ghi chú 1', 0, 30, 30, @NGAY, @BD, @KT, @GTT, @CH, @BAN, @KH, @UTT, " +
                    "120000, 10000, 15000, 5000, 0, 5000, 8000, 100000, 100000, 0, 0, 0, 0, 0, 0, 'selftest')",
                    "@ID", billA1Id,
                    "@NGAY", new DateTime(2003, 8, 10, 9, 0, 0),
                    "@BD", new DateTime(2003, 8, 10, 8, 30, 0),
                    "@KT", new DateTime(2003, 8, 10, 9, 20, 0),
                    "@GTT", new DateTime(2003, 8, 10, 9, 25, 0),
                    "@CH", storeAId, "@BAN", tableAId, "@KH", custAId, "@UTT", userAId);

                // billA2: 10/08/2003 14:00, GIOTHANHTOAN 14:15, thẻ 50k + voucher 20k, TIENGIAMGIAGIO = 4000, NULL khóa ngoại và NULL các cột tiền còn lại.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, BATDAU, KETTHUC, GIOTHANHTOAN, " +
                    "DCUAHANGID, DBANID, DKHACHHANGID, USERTHANHTOANID, " +
                    "TIENHANGCHUAGIAM, TIENGIO, TIENGIAMGIA, GIAMGIAMATHANG, TIENGIAMGIAGIO, PHIDICHVU, TIENTHUE, TONGCONG, " +
                    "TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, CONGNO, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-A2', 'TKDT ghi chú 2', 0, 30, 30, @NGAY, @BD, @KT, @GTT, @CH, NULL, NULL, NULL, " +
                    "80000, 0, 7000, 3000, 4000, 2000, 6000, 70000, NULL, 20000, 50000, NULL, NULL, NULL, NULL, 'selftest')",
                    "@ID", billA2Id,
                    "@NGAY", new DateTime(2003, 8, 10, 14, 0, 0),
                    "@BD", new DateTime(2003, 8, 10, 13, 15, 0),
                    "@KT", new DateTime(2003, 8, 10, 14, 10, 0),
                    "@GTT", new DateTime(2003, 8, 10, 14, 15, 0),
                    "@CH", storeAId);

                // billA3: 11/08/2003 15:30 (ngày Đến lúc 15:30 → PHẢI được tính), công nợ 30k + trừ tích lũy 12k + thẻ trả trước 18k + CK 25k, NULL TIENGIO/GIAMGIAMATHANG/TIENGIAMGIAGIO.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, BATDAU, KETTHUC, GIOTHANHTOAN, " +
                    "DCUAHANGID, TIENHANGCHUAGIAM, TIENGIO, TIENGIAMGIA, GIAMGIAMATHANG, TIENGIAMGIAGIO, PHIDICHVU, TIENTHUE, TONGCONG, " +
                    "TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, CONGNO, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-A3', 'TKDT ghi chú 3', 0, 30, 30, @NGAY, @BD, @KT, @GTT, @CH, " +
                    "95000, NULL, 6000, NULL, NULL, 3000, 4000, 85000, NULL, NULL, NULL, 25000, 12000, 18000, 30000, 'selftest')",
                    "@ID", billA3Id,
                    "@NGAY", new DateTime(2003, 8, 11, 15, 30, 0),
                    "@BD", new DateTime(2003, 8, 11, 15, 0, 0),
                    "@KT", new DateTime(2003, 8, 11, 15, 40, 0),
                    "@GTT", new DateTime(2003, 8, 11, 15, 45, 0),
                    "@CH", storeAId);

                // billB1 (Store B): cùng NGAY 10/08/2003 09:00 như billA1 nhưng GIOTHANHTOAN 09:10 SỚM HƠN 09:25 → kiểm sắp NGAY, GIOTHANHTOAN tăng dần!
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, BATDAU, KETTHUC, GIOTHANHTOAN, " +
                    "DCUAHANGID, TIENHANGCHUAGIAM, TIENGIO, TIENGIAMGIA, GIAMGIAMATHANG, TIENGIAMGIAGIO, PHIDICHVU, TIENTHUE, TONGCONG, " +
                    "TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, CONGNO, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-B1', 'Bill CH B', 0, 30, 30, @NGAY, @BD, @KT, @GTT, @CH, " +
                    "80000, 0, 5000, 0, 0, 0, 0, 75000, 60000, 0, 0, 15000, 0, 0, 0, 'selftest')",
                    "@ID", billB1Id,
                    "@NGAY", new DateTime(2003, 8, 10, 9, 0, 0),
                    "@BD", new DateTime(2003, 8, 10, 8, 45, 0),
                    "@KT", new DateTime(2003, 8, 10, 9, 5, 0),
                    "@GTT", new DateTime(2003, 8, 10, 9, 10, 0),
                    "@CH", storeBId);

                // 5 bill BỊ LOẠI (cùng Store A, số tiền lớn 999k để lộ ngay nếu bị lọt vào lưới hoặc khối tổng):
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENHANGCHUAGIAM, TONGCONG, TIENMAT, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-UNPAID', 0, 0, 30, @NGAY, @NGAY, @CH, 999000, 999000, 999000, 'selftest')",
                    "@ID", billUnpaidId, "@NGAY", new DateTime(2003, 8, 10, 10, 0, 0), "@CH", storeAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENHANGCHUAGIAM, TONGCONG, TIENMAT, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-DEL', 0, 30, 0, @NGAY, @NGAY, @CH, 999000, 999000, 999000, 'selftest')",
                    "@ID", billDeletedId, "@NGAY", new DateTime(2003, 8, 10, 10, 15, 0), "@CH", storeAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENHANGCHUAGIAM, TONGCONG, TIENMAT, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-IMP', 1, 30, 30, @NGAY, @NGAY, @CH, 999000, 999000, 999000, 'selftest')",
                    "@ID", billImportId, "@NGAY", new DateTime(2003, 8, 10, 10, 30, 0), "@CH", storeAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENHANGCHUAGIAM, TONGCONG, TIENMAT, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-NEXT00', 0, 30, 30, @NGAY, @NGAY, @CH, 999000, 999000, 999000, 'selftest')",
                    "@ID", billNextDayId, "@NGAY", new DateTime(2003, 8, 12, 0, 0, 0), "@CH", storeAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, TIENHANGCHUAGIAM, TONGCONG, TIENMAT, USERCREATEDID) " +
                    "VALUES (@ID, 'TKDT-BEFORE', 0, 30, 30, @NGAY, @NGAY, @CH, 999000, 999000, 999000, 'selftest')",
                    "@ID", billBeforeFromId, "@NGAY", new DateTime(2003, 8, 9, 23, 59, 0), "@CH", storeAId);

                // Phiếu thu/chi TTHUCHI: 2 phiếu thu + 1 phiếu chi Store A (trong đó thuA2 có STATUS=0 và 15:30 ngày Đến → VẪN tính),
                // 1 phiếu Store B, 1 phiếu 00:00 hôm sau (loại), 1 phiếu trước ngày Từ (loại).
                Action<string, DateTime, decimal?, decimal?, string, int> addVoucher =
                    delegate(string name, DateTime ngay, decimal? thu, decimal? chi, string storeId, int status)
                    {
                        string vid = DbHelper.NewId();
                        voucherIds.Add(vid);
                        BookingExec(db,
                            "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, THU, CHI, DCUAHANGID, STATUS, USERCREATEDID) " +
                            "VALUES (@ID, @N, @NGAY, @NGAY, @THU, @CHI, @CH, @ST, 'selftest')",
                            "@ID", vid, "@N", name, "@NGAY", ngay,
                            "@THU", thu, "@CHI", chi, "@CH", storeId, "@ST", status);
                    };

                addVoucher("TKDT-PT-A1", new DateTime(2003, 8, 10, 10, 0, 0), 30000m, 0m, storeAId, 30);
                addVoucher("TKDT-PT-A2-ST0", new DateTime(2003, 8, 11, 15, 30, 0), 10000m, null, storeAId, 0);
                addVoucher("TKDT-PC-A1", new DateTime(2003, 8, 10, 16, 0, 0), null, 15000m, storeAId, 30);
                addVoucher("TKDT-PTPC-B1", new DateTime(2003, 8, 10, 12, 0, 0), 50000m, 20000m, storeBId, 30);
                addVoucher("TKDT-NEXT00", new DateTime(2003, 8, 12, 0, 0, 0), 777000m, 333000m, storeAId, 30);
                addVoucher("TKDT-BEFORE", new DateTime(2003, 8, 9, 23, 59, 0), 666000m, 0m, storeAId, 30);

                // Dòng chi tiết TDONHANGCHITIET:
                Action<string, string, string, string, decimal?, decimal?, decimal?, decimal?, int, string, string, DateTime> addLine =
                    delegate(string billId, string prodId, string tenHang, string dvtId, decimal? sl, decimal? dg, decimal? ck, decimal? tt,
                        int xvt, string comboParent, string note, DateTime tc)
                    {
                        string lid = DbHelper.NewId();
                        lineIds.Add(lid);
                        BookingExec(db,
                            "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, " +
                            "SLXUATCHUAQUYDOI, DONGIA, TILEGIAMGIA, THANHTIEN, XUATVATTU, COMBOPARENTID, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                            "VALUES (@ID, @BID, @PID, @TEN, @U, @SL, @DG, @CK, @TT, @XVT, @CP, @NOTE, 30, 'selftest', @TC)",
                            "@ID", lid, "@BID", billId, "@PID", prodId, "@TEN", tenHang, "@U", dvtId,
                            "@SL", sl, "@DG", dg, "@CK", ck, "@TT", tt,
                            "@XVT", xvt, "@CP", comboParent, "@NOTE", note, "@TC", tc);
                    };

                // billA1: 2 dòng hợp lệ (1 có MH thường, 1 KHÔNG có DMATHANGID vẫn giữ) + 3 dòng bị loại (XUATVATTU=30, nguyên liệu loại '2', combo con).
                addLine(billA1Id, prodNormalId, "TKDT-Trà đào", unitId, 2m, 45000m, 10m, 81000m, 0, "", "Ít đá", new DateTime(2003, 8, 10, 9, 1, 0));
                addLine(billA1Id, null, "TKDT-Phụ thu ngoài DM", null, 1.5m, 20000m, 0m, 30000m, 0, null, "Không có MH", new DateTime(2003, 8, 10, 9, 2, 0));
                addLine(billA1Id, prodNormalId, "TKDT-Xuất vật tư", unitId, 1m, 10000m, 0m, 10000m, 30, "", "Loại XVT", new DateTime(2003, 8, 10, 9, 3, 0));
                addLine(billA1Id, prodMatId, "TKDT-Đường cát NL", unitId, 2m, 5000m, 0m, 10000m, 0, "", "Loại NL", new DateTime(2003, 8, 10, 9, 4, 0));
                addLine(billA1Id, prodNormalId, "TKDT-Combo con", unitId, 1m, 0m, 0m, 0m, 0, "combo-parent-tkdt", "Loại combo", new DateTime(2003, 8, 10, 9, 5, 0));

                // billA2 và billB1: mỗi bill 1 dòng hợp lệ khác nhau để kiểm đổi CurrentCell tải đúng chi tiết.
                addLine(billA2Id, prodNormalId, "TKDT-Cà phê đen", unitId, 3m, 25000m, 0m, 75000m, 0, "", "Đậm", new DateTime(2003, 8, 10, 14, 1, 0));
                addLine(billB1Id, prodNormalId, "TKDT-Trà vải B", unitId, 1m, 75000m, 0m, 75000m, 0, "", "CH B", new DateTime(2003, 8, 10, 9, 1, 0));

                var repo = new RevenueReportRepository(db);
                DateTime from = new DateTime(2003, 8, 10);
                DateTime to = new DateTime(2003, 8, 11);

                // 4. Kiểm tầng Repository: phạm vi bill, sắp NGAY+GIOTHANHTOAN, lọc cửa hàng, TTHUCHI STATUS<>30, 15:30 vs 00:00, NULL tiền & khóa ngoại, loại chi tiết.
                var adminScope = new BillReportScope { FromDate = from, ToDate = to, StoreId = "", UserId = adminSession.UserId, IsAdmin = true };
                List<RevenueBillRow> adminBills = repo.LoadBills(adminScope);
                RevenueCashFlowTotals adminCf = repo.LoadOtherCashFlow(adminScope);
                RevenueSummary adminSum = RevenueSummaryCalculator.Calculate(adminBills, adminCf);

                Check("Thống kê doanh thu Repo (§6 phạm vi bill, sắp NGAY+GIOTHANHTOAN, loại bill sai/00:00 hôm sau, TTHUCHI gồm STATUS<>30)",
                    adminBills.Count == 4
                    && adminBills[0].Id == billB1Id
                    && adminBills[1].Id == billA1Id
                    && adminBills[2].Id == billA2Id
                    && adminBills[3].Id == billA3Id
                    && adminBills[1].TableName == "TKDT-Bàn 01" && adminBills[1].CustomerName == "TKDT Khách A"
                    && adminBills[1].CustomerAddress == "88 Lê Lợi" && adminBills[1].CustomerCode == "TKDT-KH01"
                    && adminBills[1].PaidBy == "TKDT Thu ngân A"
                    && adminBills[2].TableName == "" && adminBills[2].CustomerName == "" && adminBills[2].PaidBy == ""
                    && adminBills[2].TienMat == 0m && adminBills[2].ChuyenKhoan == 0m && adminBills[2].CongNo == 0m
                    && adminCf.OtherIncome == 90000m && adminCf.OtherExpense == 35000m
                    && adminSum.TotalDiscount == 45000m && adminSum.NetActualReceipt == 305000m,
                    "count=" + adminBills.Count + ", thu=" + adminCf.OtherIncome + ", chi=" + adminCf.OtherExpense + ", net=" + adminSum.NetActualReceipt);

                var storeAScope = new BillReportScope { FromDate = from, ToDate = to, StoreId = storeAId, UserId = adminSession.UserId, IsAdmin = true };
                List<RevenueBillRow> storeABills = repo.LoadBills(storeAScope);
                RevenueCashFlowTotals storeACf = repo.LoadOtherCashFlow(storeAScope);
                RevenueSummary storeASum = RevenueSummaryCalculator.Calculate(storeABills, storeACf);
                Check("Thống kê doanh thu Repo (§6/Q13a chọn Store A): loại bill & thu/chi Store B, khớp số tay",
                    storeABills.Count == 3
                    && storeABills[0].Id == billA1Id && storeABills[1].Id == billA2Id && storeABills[2].Id == billA3Id
                    && storeACf.OtherIncome == 40000m && storeACf.OtherExpense == 15000m
                    && storeASum.GoodsAmount == 295000m && storeASum.GoodsDiscount == 36000m
                    && storeASum.TimeDiscount == 4000m && storeASum.TotalDiscount == 40000m
                    && storeASum.TotalRevenue == 255000m && storeASum.NetActualReceipt == 200000m,
                    "count=" + storeABills.Count + ", thu=" + storeACf.OtherIncome + ", chi=" + storeACf.OtherExpense + ", net=" + storeASum.NetActualReceipt);

                var userAScope = new BillReportScope { FromDate = from, ToDate = to, StoreId = "", UserId = userAId, IsAdmin = false };
                List<RevenueBillRow> userABills = repo.LoadBills(userAScope);
                RevenueCashFlowTotals userACf = repo.LoadOtherCashFlow(userAScope);
                Check("Thống kê doanh thu Repo (§6 user thường không chọn CH): chỉ thấy Store A ở cả bill và TTHUCHI",
                    userABills.Count == 3 && userACf.OtherIncome == 40000m && userACf.OtherExpense == 15000m);

                var noStoreScope = new BillReportScope { FromDate = from, ToDate = to, StoreId = "", UserId = userNoStoreId, IsAdmin = false };
                List<RevenueBillRow> noStoreBills = repo.LoadBills(noStoreScope);
                RevenueCashFlowTotals noStoreCf = repo.LoadOtherCashFlow(noStoreScope);
                List<StoreRecord> noStoreLookup = repo.LoadAllowedStores(userNoStoreId, false);
                Check("Thống kê doanh thu Repo (§6 user không có cửa hàng): lưới rỗng, THU=0, CHI=0, lookup rỗng",
                    noStoreBills.Count == 0 && noStoreCf.OtherIncome == 0m && noStoreCf.OtherExpense == 0m && noStoreLookup.Count == 0);

                List<RevenueBillLine> billA1Lines = repo.LoadBillLines(billA1Id);
                List<RevenueBillLine> emptyLines = repo.LoadBillLines("");
                Check("Thống kê doanh thu Repo (§6/§2.2 chi tiết bill): loại xuất vật tư, nguyên liệu loại 2, combo con; giữ dòng không có mặt hàng",
                    billA1Lines.Count == 2
                    && billA1Lines[0].ItemName == "TKDT-Trà đào" && billA1Lines[0].UnitName == "TKDT-Ly"
                    && billA1Lines[0].Quantity == 2m && billA1Lines[0].UnitPrice == 45000m
                    && billA1Lines[0].DiscountRate == 10m && billA1Lines[0].Amount == 81000m
                    && billA1Lines[1].ItemName == "TKDT-Phụ thu ngoài DM" && billA1Lines[1].ProductId == "" && billA1Lines[1].Quantity == 1.5m
                    && emptyLines.Count == 0,
                    "lines=" + billA1Lines.Count);

                // 5. Test giao diện THẬT trên Form đã Show ngoài màn hình (sự kiện thật: DateBox Inner.Text+Commit, SelectedIndex, CurrentCell, ProcessCmdKey F5, PerformClick).
                DateTime dbToday = repo.GetServerDate();
                string[] expectedBillHeaders =
                {
                    "Số phiếu", "Ngày", "Bàn", "Giờ vào", "Giờ ra", "Giờ thanh toán", "Khách hàng",
                    "Tiền giảm giá", "Tổng cộng", "Thanh toán bởi", "Tiền hàng", "Ghi chú", "Địa chỉ", "Mã khách"
                };
                string[] expectedDetailHeaders =
                {
                    "Mặt hàng", "ĐVT", "S.lượng", "Đơn giá", "CK%", "Thành tiền", "Ghi chú"
                };

                using (var hostForm = new System.Windows.Forms.Form
                {
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new System.Drawing.Point(-20000, -20000),
                    ClientSize = new System.Drawing.Size(1400, 780)
                })
                {
                    var view = new RevenueReportView(adminSession)
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        SuppressErrorPopup = true
                    };
                    hostForm.Controls.Add(view);
                    hostForm.Show();
                    System.Windows.Forms.Application.DoEvents();
                    try
                    {
                        List<string> bh = view.BillColumnHeaders();
                        bool bhOk = bh.Count == expectedBillHeaders.Length;
                        for (int i = 0; bhOk && i < expectedBillHeaders.Length; i++)
                            if (bh[i] != expectedBillHeaders[i]) bhOk = false;

                        List<string> dh = view.DetailColumnHeaders();
                        bool dhOk = dh.Count == expectedDetailHeaders.Length;
                        for (int i = 0; dhOk && i < expectedDetailHeaders.Length; i++)
                            if (dh[i] != expectedDetailHeaders[i]) dhOk = false;

                        bool noHeaderClip = true;
                        foreach (System.Windows.Forms.DataGridViewColumn col in view.BillsGridForTest.Columns)
                        {
                            int needW = System.Windows.Forms.TextRenderer.MeasureText(col.HeaderText, view.HeadingFontForTest).Width;
                            if (col.Width < needW) noHeaderClip = false;
                        }
                        foreach (System.Windows.Forms.DataGridViewColumn col in view.DetailGridForTest.Columns)
                        {
                            int needW = System.Windows.Forms.TextRenderer.MeasureText(col.HeaderText, view.HeadingFontForTest).Width;
                            if (col.Width < needW) noHeaderClip = false;
                        }

                        bool captionsOk = view.GetSummaryCaptionText("goods") == "TIỀN HÀNG"
                            && view.GetSummaryCaptionText("time") == "TIỀN GIỜ"
                            && view.GetSummaryCaptionText("goodsDiscount") == "GIẢM GIÁ TIỀN HÀNG"
                            && view.GetSummaryCaptionText("timeDiscount") == "GIẢM GIÁ TIỀN GIỜ"
                            && view.GetSummaryCaptionText("totalDiscount") == "TỔNG GIẢM GIÁ"
                            && view.GetSummaryCaptionText("service") == "PHÍ DỊCH VỤ"
                            && view.GetSummaryCaptionText("tax") == "THUẾ"
                            && view.GetSummaryCaptionText("totalRevenue") == "TỔNG DOANH THU"
                            && view.GetSummaryCaptionText("cash") == "TIỀN MẶT"
                            && view.GetSummaryCaptionText("card") == "TIỀN THẺ"
                            && view.GetSummaryCaptionText("bankTransfer") == "CHUYỂN KHOẢN"
                            && view.GetSummaryCaptionText("voucher") == "VOUCHER"
                            && view.GetSummaryCaptionText("loyalty") == "TRỪ TÍCH LŨY"
                            && view.GetSummaryCaptionText("prepaid") == "THẺ TRẢ TRƯỚC"
                            && view.GetSummaryCaptionText("debt") == "CÔNG NỢ"
                            && view.GetSummaryCaptionText("otherIncome") == "THU KHÁC"
                            && view.GetSummaryCaptionText("otherExpense") == "CHI KHÁC"
                            && view.GetSummaryCaptionText("netReceipt") == RevenueReportConstants.NetActualReceiptCaption;

                        // Q7: hai dòng TIỀN GIỜ và GIẢM GIÁ TIỀN GIỜ thật sự ẩn (Visible == false trên Form đã Show).
                        bool timeRowsHidden = !view.TimeChargeCaptionLabelForTest.Visible
                            && !view.TimeChargeValueLabelForTest.Visible
                            && !view.TimeDiscountCaptionLabelForTest.Visible
                            && !view.TimeDiscountValueLabelForTest.Visible;

                        string tip = view.NetReceiptTooltipForTest;
                        bool tooltipOk = tip.IndexOf("TIỀN MẶT", StringComparison.OrdinalIgnoreCase) >= 0
                            && tip.IndexOf("VOUCHER", StringComparison.OrdinalIgnoreCase) >= 0
                            && tip.IndexOf("CÔNG NỢ", StringComparison.OrdinalIgnoreCase) >= 0;

                        bool storeBlankFirst = view.StoreComboForTest.Items.Count >= 3
                            && ((StoreRecord)view.StoreComboForTest.Items[0]).Id == ""
                            && ((StoreRecord)view.StoreComboForTest.Items[0]).Name == "";

                        bool themeColorOk = view.NetReceiptValueLabelForTest.ForeColor == Theme.ThemeManager.Current.Danger
                            && view.TotalRevenueValueLabelForTest.ForeColor == Theme.ThemeManager.Current.Primary;

                        Check("Thống kê doanh thu UI (§6 mở màn, Q7 ẩn dòng giờ, P2 tooltip, token màu): ngày DB, đúng 14+7 cột không cắt chữ, nhãn in hoa nguyên văn, 2 dòng giờ Visible=false",
                            view.FromDateValue.Date == dbToday && view.ToDateValue.Date == dbToday
                            && bhOk && dhOk && noHeaderClip && captionsOk && timeRowsHidden && tooltipOk && storeBlankFirst && themeColorOk
                            && view.BillsGridForTest.ReadOnly && view.DetailGridForTest.ReadOnly
                            && view.BillsGridForTest.ScrollBars == System.Windows.Forms.ScrollBars.Both
                            && view.BillsGridForTest.RowTemplate.Height >= 32
                            && view.DetailGridForTest.RowTemplate.Height >= 32,
                            "bh=" + bhOk + ", dh=" + dhOk + ", clip=" + noHeaderClip + ", cap=" + captionsOk + ", hid=" + timeRowsHidden + ", tip=" + tooltipOk);

                        // Đổi Từ/Đến qua sự kiện thật Inner.Text + Commit() → tải 4 bill theo thứ tự NGAY, GIOTHANHTOAN và đúng từng nhãn khối tổng Admin.
                        view.FromDateBoxForTest.Inner.Text = "10/08/2003";
                        view.FromDateBoxForTest.Commit();
                        view.ToDateBoxForTest.Inner.Text = "11/08/2003";
                        view.ToDateBoxForTest.Commit();

                        bool orderOk = view.BillRowCount == 4
                            && view.FindBillRowIndexById(billB1Id) == 0
                            && view.FindBillRowIndexById(billA1Id) == 1
                            && view.FindBillRowIndexById(billA2Id) == 2
                            && view.FindBillRowIndexById(billA3Id) == 3;

                        // Kiểm định dạng ô thật trên lưới bill (billA1 ở dòng 1, billA2 NULL FK ở dòng 2).
                        bool cellFormatOk = orderOk
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colSoPhieu"].FormattedValue) == "TKDT-A1"
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colNgay"].FormattedValue) == "10/08/2003"
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colBan"].FormattedValue) == "TKDT-Bàn 01"
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colGioVao"].FormattedValue) == "08:30"
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colGioRa"].FormattedValue) == "09:20"
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colGioThanhToan"].FormattedValue) == "09:25"
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colTienGiamGia"].FormattedValue) == 15000m.ToString("n0", vi)
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colTongCong"].FormattedValue) == 100000m.ToString("n0", vi)
                            && Convert.ToString(view.BillsGridForTest.Rows[1].Cells["colTienHang"].FormattedValue) == 120000m.ToString("n0", vi)
                            && Convert.ToString(view.BillsGridForTest.Rows[2].Cells["colBan"].FormattedValue) == ""
                            && Convert.ToString(view.BillsGridForTest.Rows[2].Cells["colKhachHang"].FormattedValue) == "";

                        bool adminLabelsOk =
                            view.GetSummaryValueText("goods") == 375000m.ToString("n0", vi)
                            && view.GetSummaryValueText("goodsDiscount") == "-" + 41000m.ToString("n0", vi)
                            && view.GetSummaryValueText("timeDiscount") == "-" + 4000m.ToString("n0", vi)
                            && view.GetSummaryValueText("totalDiscount") == "-" + 45000m.ToString("n0", vi)
                            && view.GetSummaryValueText("service") == 10000m.ToString("n0", vi)
                            && view.GetSummaryValueText("tax") == 18000m.ToString("n0", vi)
                            && view.GetSummaryValueText("totalRevenue") == 330000m.ToString("n0", vi)
                            && view.GetSummaryValueText("cash") == 160000m.ToString("n0", vi)
                            && view.GetSummaryValueText("card") == 50000m.ToString("n0", vi)
                            && view.GetSummaryValueText("bankTransfer") == 40000m.ToString("n0", vi)
                            && view.GetSummaryValueText("voucher") == 20000m.ToString("n0", vi)
                            && view.GetSummaryValueText("loyalty") == 12000m.ToString("n0", vi)
                            && view.GetSummaryValueText("prepaid") == 18000m.ToString("n0", vi)
                            && view.GetSummaryValueText("debt") == 30000m.ToString("n0", vi)
                            && view.GetSummaryValueText("otherIncome") == 90000m.ToString("n0", vi)
                            && view.GetSummaryValueText("otherExpense") == 35000m.ToString("n0", vi)
                            && view.GetSummaryValueText("netReceipt") == 305000m.ToString("n0", vi);

                        Check("Thống kê doanh thu UI (§6 đổi Từ/Đến qua DateBox, sắp thứ tự, từng nhãn khối tổng Admin + dấu -): khớp số tay",
                            orderOk && cellFormatOk && adminLabelsOk,
                            "ord=" + orderOk + ", fmt=" + cellFormatOk + ", totDisc=" + view.GetSummaryValueText("totalDiscount") + ", net=" + view.GetSummaryValueText("netReceipt"));

                        // Lưới chi tiết: đang chọn dòng 0 (billB1) → 1 dòng; đổi CurrentCell sang billA1 (dòng 1) → 2 dòng (gồm dòng không có MH, loại XVT/NL/combo); đổi sang billA2 (dòng 2) → 1 dòng; bỏ chọn → trống.
                        bool detailB1Ok = view.DetailRowCount == 1
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colMatHang"].FormattedValue) == "TKDT-Trà vải B";

                        view.BillsGridForTest.CurrentCell = view.BillsGridForTest.Rows[1].Cells[0];
                        bool detailA1Ok = view.DetailRowCount == 2
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colMatHang"].FormattedValue) == "TKDT-Trà đào"
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colSoLuong"].FormattedValue) == 2m.ToString("#,##0.##", vi)
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colCkPct"].FormattedValue) == 10m.ToString("0.##", vi)
                            && Convert.ToString(view.DetailGridForTest.Rows[1].Cells["colMatHang"].FormattedValue) == "TKDT-Phụ thu ngoài DM"
                            && Convert.ToString(view.DetailGridForTest.Rows[1].Cells["colSoLuong"].FormattedValue) == 1.5m.ToString("#,##0.##", vi);

                        view.BillsGridForTest.CurrentCell = view.BillsGridForTest.Rows[2].Cells[0];
                        bool detailA2Ok = view.DetailRowCount == 1
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colMatHang"].FormattedValue) == "TKDT-Cà phê đen";

                        view.BillsGridForTest.ClearSelection();
                        view.BillsGridForTest.CurrentCell = null;
                        bool detailEmptyWhenUnselected = view.DetailRowCount == 0;

                        Check("Thống kê doanh thu UI (§6/§2.2 lưới chi tiết qua CurrentCell): đổi bill nạp đúng chi tiết, loại XVT/NL/combo, giữ dòng không MH, bỏ chọn thì trống",
                            detailB1Ok && detailA1Ok && detailA2Ok && detailEmptyWhenUnselected,
                            "b1=" + detailB1Ok + ", a1=" + detailA1Ok + ", a2=" + detailA2Ok + ", empty=" + detailEmptyWhenUnselected);

                        // F5 qua ProcessCmdKey: chèn phiếu thu mới (25.000, Store A) trước khi nhấn F5 → THU KHÁC tăng 90k→115k, TỔNG THỰC THU tăng 305k→330k, giữ billA2 & bộ lọc.
                        view.BillsGridForTest.CurrentCell = view.BillsGridForTest.Rows[2].Cells[0];
                        addVoucher("TKDT-PT-F5", new DateTime(2003, 8, 10, 17, 0, 0), 25000m, 0m, storeAId, 30);
                        bool f5Handled = view.DispatchCmdKeyForTest(System.Windows.Forms.Keys.F5);
                        bool f5ReloadedAndKeptA2 = f5Handled
                            && view.GetSelectedBillId() == billA2Id
                            && view.FromDateValue == from && view.ToDateValue == to
                            && view.StoreComboForTest.SelectedIndex == 0
                            && view.DetailRowCount == 1
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colMatHang"].FormattedValue) == "TKDT-Cà phê đen"
                            && view.GetSummaryValueText("otherIncome") == 115000m.ToString("n0", vi)
                            && view.GetSummaryValueText("netReceipt") == 330000m.ToString("n0", vi);

                        // Nút Refresh (PerformClick): chèn phiếu chi mới (10.000, Store A) trước khi bấm → CHI KHÁC tăng 35k→45k, TỔNG THỰC THU giảm 330k→320k, giữ billA2 & bộ lọc.
                        addVoucher("TKDT-PC-REF", new DateTime(2003, 8, 10, 17, 30, 0), 0m, 10000m, storeAId, 30);
                        view.RefreshButtonForTest.PerformClick();
                        bool refreshReloadedAndKeptA2 = view.GetSelectedBillId() == billA2Id
                            && view.FromDateValue == from && view.ToDateValue == to
                            && view.StoreComboForTest.SelectedIndex == 0
                            && view.DetailRowCount == 1
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colMatHang"].FormattedValue) == "TKDT-Cà phê đen"
                            && view.GetSummaryValueText("otherIncome") == 115000m.ToString("n0", vi)
                            && view.GetSummaryValueText("otherExpense") == 45000m.ToString("n0", vi)
                            && view.GetSummaryValueText("netReceipt") == 320000m.ToString("n0", vi);

                        // Chèn bill mới sớm hơn (08:00, Store B) chen lên TRƯỚC billA2 rồi nhấn F5 → chỉ số dòng đang chọn dịch từ 2 sang 3 nhưng vẫn giữ đúng billA2 theo ID!
                        int idxBeforeShift = view.BillsGridForTest.CurrentRow != null ? view.BillsGridForTest.CurrentRow.Index : -1;
                        BookingExec(db,
                            "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, BATDAU, KETTHUC, GIOTHANHTOAN, " +
                            "DCUAHANGID, TIENHANGCHUAGIAM, TIENGIO, TIENGIAMGIA, GIAMGIAMATHANG, TIENGIAMGIAGIO, PHIDICHVU, TIENTHUE, TONGCONG, " +
                            "TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, CONGNO, USERCREATEDID) " +
                            "VALUES (@ID, 'TKDT-B0-EARLY', 'Bill sớm chen đầu', 0, 30, 30, @NGAY, @BD, @KT, @GTT, @CH, " +
                            "10000, 0, 0, 0, 0, 0, 0, 10000, 10000, 0, 0, 0, 0, 0, 0, 'selftest')",
                            "@ID", billEarlyId,
                            "@NGAY", new DateTime(2003, 8, 10, 8, 0, 0),
                            "@BD", new DateTime(2003, 8, 10, 7, 50, 0),
                            "@KT", new DateTime(2003, 8, 10, 8, 0, 0),
                            "@GTT", new DateTime(2003, 8, 10, 8, 5, 0),
                            "@CH", storeBId);
                        bool f5ShiftHandled = view.DispatchCmdKeyForTest(System.Windows.Forms.Keys.F5);
                        int idxAfterShift = view.BillsGridForTest.CurrentRow != null ? view.BillsGridForTest.CurrentRow.Index : -1;
                        Check("Thống kê doanh thu UI (§6 F5 giữ bill theo ID khi có bill mới chen trước): chỉ số đổi 2→3 nhưng vẫn giữ đúng billA2 và cập nhật tổng",
                            f5ShiftHandled && idxBeforeShift == 2 && idxAfterShift == 3
                            && view.BillRowCount == 5
                            && view.FindBillRowIndexById(billEarlyId) == 0
                            && view.GetSelectedBillId() == billA2Id
                            && view.DetailRowCount == 1
                            && Convert.ToString(view.DetailGridForTest.Rows[0].Cells["colMatHang"].FormattedValue) == "TKDT-Cà phê đen"
                            && view.GetSummaryValueText("totalRevenue") == 340000m.ToString("n0", vi)
                            && view.GetSummaryValueText("netReceipt") == 330000m.ToString("n0", vi),
                            "before=" + idxBeforeShift + ", after=" + idxAfterShift + ", sel=" + view.GetSelectedBillId() + ", net=" + view.GetSummaryValueText("netReceipt"));

                        // Chọn billEarly (của Store B), sau đó đổi combo sang Store A qua SelectedIndex:
                        // billEarly không còn → tự chọn dòng đầu (billA1), nạp chi tiết billA1 (2 dòng) và đổi toàn bộ nhãn tổng sang số tay Store A (gồm TKDT-PT-F5 +25k và TKDT-PC-REF +10k)!
                        view.BillsGridForTest.CurrentCell = view.BillsGridForTest.Rows[0].Cells[0];
                        int storeAIdx = -1;
                        for (int i = 0; i < view.StoreComboForTest.Items.Count; i++)
                        {
                            var sr = view.StoreComboForTest.Items[i] as StoreRecord;
                            if (sr != null && sr.Id == storeAId) { storeAIdx = i; break; }
                        }
                        if (storeAIdx > 0)
                            view.StoreComboForTest.SelectedIndex = storeAIdx;

                        bool storeASwitchOk = storeAIdx > 0
                            && view.BillRowCount == 3
                            && view.GetSelectedBillId() == billA1Id
                            && view.DetailRowCount == 2
                            && view.GetSummaryValueText("goods") == 295000m.ToString("n0", vi)
                            && view.GetSummaryValueText("goodsDiscount") == "-" + 36000m.ToString("n0", vi)
                            && view.GetSummaryValueText("totalDiscount") == "-" + 40000m.ToString("n0", vi)
                            && view.GetSummaryValueText("service") == 10000m.ToString("n0", vi)
                            && view.GetSummaryValueText("tax") == 18000m.ToString("n0", vi)
                            && view.GetSummaryValueText("totalRevenue") == 255000m.ToString("n0", vi)
                            && view.GetSummaryValueText("cash") == 100000m.ToString("n0", vi)
                            && view.GetSummaryValueText("card") == 50000m.ToString("n0", vi)
                            && view.GetSummaryValueText("bankTransfer") == 25000m.ToString("n0", vi)
                            && view.GetSummaryValueText("voucher") == 20000m.ToString("n0", vi)
                            && view.GetSummaryValueText("loyalty") == 12000m.ToString("n0", vi)
                            && view.GetSummaryValueText("prepaid") == 18000m.ToString("n0", vi)
                            && view.GetSummaryValueText("debt") == 30000m.ToString("n0", vi)
                            && view.GetSummaryValueText("otherIncome") == 65000m.ToString("n0", vi)
                            && view.GetSummaryValueText("otherExpense") == 25000m.ToString("n0", vi)
                            && view.GetSummaryValueText("netReceipt") == 215000m.ToString("n0", vi);

                        // Bấm nút In báo cáo bằng PerformClick() → gọi picker giả trả null (người dùng hủy), kiểm picker nhận đủ 4 mẫu.
                        int revPickerCalls = 0;
                        int revTemplateCount = 0;
                        view.PrintOutputHandlerForTest = new PrintOutputRecorder();
                        view.PrintTemplatePickerForTest = delegate (System.Windows.Forms.IWin32Window owner, IList<PrintTemplateRecord> list)
                        {
                            revPickerCalls++;
                            revTemplateCount = list == null ? 0 : list.Count;
                            return null;
                        };
                        view.PrintButtonForTest.PerformClick();
                        bool printMsgOk = revPickerCalls == 1 && revTemplateCount == 4;

                        Check("Thống kê doanh thu UI (§6 phím F5/Refresh tải lại số mới & giữ bill, đổi cửa hàng tải ngay + đổi tổng Store A, nút In báo chưa hỗ trợ)",
                            f5ReloadedAndKeptA2 && refreshReloadedAndKeptA2 && storeASwitchOk && printMsgOk,
                            "f5=" + f5ReloadedAndKeptA2 + ", ref=" + refreshReloadedAndKeptA2 + ", storeA=" + storeASwitchOk + ", print=" + printMsgOk);
                    }
                    finally
                    {
                        hostForm.Close();
                    }
                }

                // 6. User thường (chỉ gắn Store A) & User không có cửa hàng trên Form đã Show thật.
                var userASession = new UserSession
                {
                    UserId = userAId, UserName = "tkdt_user_a", DisplayName = "TKDT Thu ngân A",
                    IsAdmin = false, DatabasePath = db, Permissions = permsViewOnly
                };
                using (var hostUserA = new System.Windows.Forms.Form
                {
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new System.Drawing.Point(-20000, -20000),
                    ClientSize = new System.Drawing.Size(1400, 780)
                })
                {
                    var viewUserA = new RevenueReportView(userASession) { Dock = System.Windows.Forms.DockStyle.Fill, SuppressErrorPopup = true };
                    hostUserA.Controls.Add(viewUserA);
                    hostUserA.Show();
                    System.Windows.Forms.Application.DoEvents();
                    try
                    {
                        bool lookupOnlyA = viewUserA.StoreComboForTest.Items.Count == 2
                            && ((StoreRecord)viewUserA.StoreComboForTest.Items[0]).Id == ""
                            && ((StoreRecord)viewUserA.StoreComboForTest.Items[1]).Id == storeAId;

                        viewUserA.FromDateBoxForTest.Inner.Text = "10/08/2003";
                        viewUserA.FromDateBoxForTest.Commit();
                        viewUserA.ToDateBoxForTest.Inner.Text = "11/08/2003";
                        viewUserA.ToDateBoxForTest.Commit();

                        Check("Thống kê doanh thu UI (§6 user thường không chọn CH): lookup chỉ có Store A, chỉ thấy bill & thu/chi Store A",
                            lookupOnlyA && viewUserA.BillRowCount == 3
                            && viewUserA.GetSummaryValueText("otherIncome") == 65000m.ToString("n0", vi)
                            && viewUserA.GetSummaryValueText("otherExpense") == 25000m.ToString("n0", vi)
                            && viewUserA.GetSummaryValueText("netReceipt") == 215000m.ToString("n0", vi),
                            "stores=" + viewUserA.StoreComboForTest.Items.Count + ", bills=" + viewUserA.BillRowCount + ", net=" + viewUserA.GetSummaryValueText("netReceipt"));
                    }
                    finally { hostUserA.Close(); }
                }

                var noStoreSession = new UserSession
                {
                    UserId = userNoStoreId, UserName = "tkdt_user_none", DisplayName = "TKDT Không CH",
                    IsAdmin = false, DatabasePath = db, Permissions = permsViewOnly
                };
                using (var hostNoStore = new System.Windows.Forms.Form
                {
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new System.Drawing.Point(-20000, -20000),
                    ClientSize = new System.Drawing.Size(1400, 780)
                })
                {
                    var viewNoStore = new RevenueReportView(noStoreSession) { Dock = System.Windows.Forms.DockStyle.Fill, SuppressErrorPopup = true };
                    hostNoStore.Controls.Add(viewNoStore);
                    hostNoStore.Show();
                    System.Windows.Forms.Application.DoEvents();
                    try
                    {
                        viewNoStore.FromDateBoxForTest.Inner.Text = "10/08/2003";
                        viewNoStore.FromDateBoxForTest.Commit();
                        viewNoStore.ToDateBoxForTest.Inner.Text = "11/08/2003";
                        viewNoStore.ToDateBoxForTest.Commit();

                        Check("Thống kê doanh thu UI (§6 user không có cửa hàng): lưới rỗng, chi tiết rỗng, THU=0, CHI=0, TỔNG THỰC THU=0",
                            viewNoStore.StoreComboForTest.Items.Count == 1
                            && viewNoStore.BillRowCount == 0 && viewNoStore.DetailRowCount == 0
                            && viewNoStore.GetSummaryValueText("otherIncome") == "0"
                            && viewNoStore.GetSummaryValueText("otherExpense") == "0"
                            && viewNoStore.GetSummaryValueText("netReceipt") == "0");
                    }
                    finally { hostNoStore.Close(); }
                }

                // 7. Preview form --render-screen preview.revenue (from/to/select).
                using (System.Windows.Forms.Form pvForm = Dialogs.ScreenFactory.Create("preview.revenue", adminSession))
                {
                    var pv = pvForm as Dialogs.RevenueReportPreviewForm;
                    bool pvOk = false;
                    if (pv != null)
                    {
                        pv.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                        pv.ShowInTaskbar = false;
                        pv.Location = new System.Drawing.Point(-20000, -20000);
                        pv.Show();
                        System.Windows.Forms.Application.DoEvents();
                        try
                        {
                            pv.Preview("from=10/08/2003");
                            pv.Preview("to=11/08/2003");
                            pv.Preview("select=2");
                            pvOk = pv.ViewForTest.BillRowCount == 5
                                && pv.ViewForTest.GetSelectedBillId() == billA1Id
                                && pv.ViewForTest.DetailRowCount == 2;
                        }
                        finally { pv.Close(); }
                    }
                    Check("Thống kê doanh thu (preview.revenue): Show form chụp màn hình và from/to/select=2 hoạt động qua sự kiện thật", pvOk);
                }
            }
            finally
            {
                Theme.ThemeManager.Apply(savedTheme);
                // Dọn dữ liệu mẫu con trước cha: chi tiết bill → bill → phiếu thu chi → bàn → khách → mặt hàng → ĐVT → liên kết CH → user → cửa hàng.
                foreach (string lid in lineIds)
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE ID = @I", "@I", lid);
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID IN (@A, @B, @C, @D, @E, @F, @G, @H, @J, @K)",
                    "@A", billA1Id, "@B", billA2Id, "@C", billA3Id, "@D", billB1Id, "@E", billEarlyId,
                    "@F", billUnpaidId, "@G", billDeletedId, "@H", billImportId, "@J", billNextDayId, "@K", billBeforeFromId);
                foreach (string vid in voucherIds)
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", vid);
                BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tableAId);
                BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", custAId);
                BookingExec(db, "DELETE FROM DMATHANG WHERE ID IN (@A, @B)", "@A", prodNormalId, "@B", prodMatId);
                BookingExec(db, "DELETE FROM DDONVITINH WHERE ID = @I", "@I", unitId);
                BookingExec(db, "DELETE FROM TNGUOIDUNGTHEOCUAHANG WHERE ID = @I", "@I", userStoreLinkId);
                BookingExec(db, "DELETE FROM SUSER WHERE ID IN (@A, @B)", "@A", userAId, "@B", userNoStoreId);
                BookingExec(db, "DELETE FROM DCUAHANG WHERE ID IN (@A, @B)", "@A", storeAId, "@B", storeBId);
            }
        }
    }
}
