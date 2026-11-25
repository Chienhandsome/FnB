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
    public static class SelfTest
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
                string schemaBefore = Schema(databasePath);

                ConfigTests(databasePath);
                RunExtra(databasePath);

                string schemaAfter = Schema(databasePath);
                Check("Không có DDL (schema trước = sau)", schemaBefore == schemaAfter);
            }
            catch (Exception ex)
            {
                Check("Ngoại lệ không mong đợi", false, ex.GetType().Name + ": " + ex.Message);
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
                List<BookingMethodRow> baseline = methods.LoadAll();
                Check("Phương thức đặt: có sẵn các thư mục tạo sẵn (Công văn, Điện thoại, Email, Tin nhắn, Trực tiếp)",
                    baseline.Exists(r => r.Name == "Công văn") && baseline.Exists(r => r.Name == "Trực tiếp"), "n=" + baseline.Count);
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
                Check("Lịch tải lưới: tổng số phòng = 4 (kể cả phòng không loại)",
                    loaded.TotalRooms == 4, loaded.TotalRooms.ToString());
                Check("Lịch tải lưới: 4 thanh (B3 bao trùm không hiện)", loaded.Bars.Count == 4,
                    loaded.Bars.Count.ToString());
                Check("Lịch tải lưới: B3 không có trong danh sách", !loaded.Bars.Exists(b => b.Id == b3Id));
                Check("Lịch tải lưới: B4 có (DENNGAY trong cửa sổ)", loaded.Bars.Exists(b => b.Id == b4Id));
                CalendarData vipOnly = calRepo.LoadCalendar(from, days, typeVip);
                Check("Lịch lọc loại VIP: 1 loại, 2 phòng, 3 thanh (B1+B2 bàn 2+B4)",
                    vipOnly.Types.Count == 1 && vipOnly.Rooms.Count == 2 && vipOnly.Bars.Count == 3,
                    vipOnly.Types.Count + "/" + vipOnly.Rooms.Count + "/" + vipOnly.Bars.Count);
                Check("Lịch lọc loại VIP: tổng số phòng vẫn 4", vipOnly.TotalRooms == 4);

                BookingCalendarRules.CalendarTotals lt = BookingCalendarRules.BuildTotals(loaded, from, days);
                Check("Lịch tổng trên DB: Đã bán [1,2,3,1,0,0,0]",
                    lt.SoldPerDay[0] == 1 && lt.SoldPerDay[1] == 2 && lt.SoldPerDay[2] == 3 &&
                    lt.SoldPerDay[3] == 1 && lt.SoldPerDay[4] == 0,
                    string.Join(",", Array.ConvertAll(lt.SoldPerDay, i => i.ToString())));
                Check("Lịch tổng trên DB: Còn lại [3,2,1,3,4,4,4]",
                    lt.RemainingPerDay[0] == 3 && lt.RemainingPerDay[2] == 1 && lt.RemainingPerDay[4] == 4);
                Check("Lịch tổng trên DB: Công suất ngày 2 = 75.0%", lt.CapacityPerDay[2] == 75.0);
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
                int n = groups.CountReferences(thuNgan.Id);
                Check("Không xóa được nhóm còn tài khoản: đếm (1) như app gốc", n == 1, "n=" + n);
                Check("Thông báo xóa nhóm đúng chữ gốc",
                    ReferenceChecker.BlockedMessage("nhóm người dùng", "Thu ngân", n) ==
                    "Bạn không thể xóa nhóm người dùng 'Thu ngân' vì còn (1) dữ liệu khác liên kết tới 'Thu ngân'");
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

                // Bật lại cờ = 30 và kiểm UI: đúng 9 cột theo thứ tự §1, nút Xóa ẩn/hiện đúng điều kiện.
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 } }, "selftest");
                SystemConfig.Reload(db);

                string[] expectedHeaders =
                {
                    "Ngày", "Giờ", "Số đơn hàng", "Nội dung", "Tài khoản", "Thiết bị", "Bàn", "Chức năng", "Phân loại"
                };
                using (var viewAdminNormal = new ActivityLogView(adminSession, false) { SuppressWarningPopup = true })
                {
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
                // Hoàn lại cấu hình và dọn toàn bộ dữ liệu seed.
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = origFlag } }, "selftest");
                SystemConfig.Reload(db);
                foreach (string id in seededLogIds)
                    BookingExec(db, "DELETE FROM TLUUVET WHERE ID = @I", "@I", id);
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG IN (@A, @B)", "@A", billDoneId, "@B", billCancelId);
                BookingExec(db, "DELETE FROM TDONHANGHUY WHERE ID IN (@A, @B)", "@A", billCancelRowId, "@B", billCancelOutRowId);
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID IN (@A, @B, @C, @D)", "@A", billDoneId, "@B", billInUseId, "@C", billOutId, "@D", billStockId);
                BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tableId);
            }
        }
    }
}
