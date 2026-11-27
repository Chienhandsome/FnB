using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PromotionApplyUiTests(string db)
        {
            ThemeTokens savedTheme = ThemeManager.Current;
            var cfgRepo = new ConfigRepository(db);
            bool oldNegStock = SystemConfig.ChoPhepAmKho;
            bool oldDefItems = SystemConfig.SuDungMatHangMacDinh;
            decimal oldDefDisc = SystemConfig.MacDinhGiamGia;
            decimal oldDefFee = SystemConfig.MacDinhPhiDichVu;
            decimal oldDefTax = SystemConfig.MacDinhThueSuat;

            cfgRepo.Save(new[]
            {
                new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "SuDungMatHangMacDinh", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "MacDinhGiamGia", DataType = 3, IntValue = 0, DecimalValue = 0m },
                new ConfigChange { Name = "MacDinhPhiDichVu", DataType = 3, IntValue = 0, DecimalValue = 0m },
                new ConfigChange { Name = "MacDinhThueSuat", DataType = 3, IntValue = 0, DecimalValue = 0m }
            }, "selftest_km2_ui");
            SystemConfig.Reload(db);

            var session = new UserSession
            {
                UserId = "selftest-km2-ui",
                UserName = "selftest_km2_ui",
                DisplayName = "SelfTest KM2 UI",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };

            PromoApplyDbFixture fx = SeedPromoApplyFixture(db);
            try
            {
                var promoSvc = new PromotionService(session);
                DateTime today = promoSvc.GetServerDate();
                string tag = DbHelper.NewId().Substring(0, 6);

                string promoSpName = "KM SP 10% " + tag;
                string promoGiftName = "KM Mua 2 Tặng 1 " + tag;
                string promoBillName = "KM Bill 5% " + tag;

                SaveTempPromo(promoSvc, fx, promoSpName, PromotionKind.ProductPercent, today, today, 0, 0m,
                    new PromotionDetailRow { ProductId = fx.ProdAId, DiscountPercent = 10m });
                SaveTempPromo(promoSvc, fx, promoGiftName, PromotionKind.BuyXGetY, today, today, 0, 0m,
                    new PromotionDetailRow { ProductId = fx.ProdAId, BuyQuantity = 2m, GiftQuantity = 1m, GiftProductId = fx.ProdGift1Id });
                SaveTempPromo(promoSvc, fx, promoBillName, PromotionKind.TotalBillPercent, today, today, 0, 5m);

                ThemeManager.Apply(BuiltInThemes.Orange);
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var view = new ServiceOrderView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
                    view.TableMapForTest.StopAutoRefresh();
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        TableTileButton tile = view.TableMapForTest.FindTileForTest(fx.Table1Id);
                        tile.PerformClick();
                        Application.DoEvents();
                        view.StartButtonForTest.PerformClick();
                        Application.DoEvents();

                        if (view.CurrentBillForTest != null)
                            fx.CreatedBillIds.Add(view.CurrentBillForTest.Id);

                        view.MenuPickerForTest.SelectProductById(fx.ProdAId);
                        view.AddQtyBoxForTest.Value = 2m;
                        view.AddButtonForTest.PerformClick();
                        Application.DoEvents();

                        DataGridView g = view.DetailGridForTest;
                        int discRowIdx = -1;
                        int giftRowIdx = -1;
                        for (int i = 0; i < g.Rows.Count; i++)
                        {
                            var line = g.Rows[i].Tag as ServiceOrderLine;
                            if (line == null) continue;
                            if (line.IsPromoGift) giftRowIdx = i;
                            else if (line.IsPromoDiscounted) discRowIdx = i;
                        }

                        bool colsIntact = g.Columns.Count == 7 && view.DetailDataErrorCountForTest == 0;
                        bool discRowOk = discRowIdx >= 0
                            && Convert.ToString(g.Rows[discRowIdx].Cells["colNote"].Value).Contains("KM: " + promoSpName)
                            && g.Rows[discRowIdx].Cells["colItemName"].InheritedStyle.ForeColor == BuiltInThemes.Orange.Primary;
                        bool giftRowOk = giftRowIdx >= 0
                            && Convert.ToString(g.Rows[giftRowIdx].Cells["colItemName"].Value).StartsWith("[KM] ", StringComparison.Ordinal)
                            && Convert.ToString(g.Rows[giftRowIdx].Cells["colPrice"].Value) == "0"
                            && Convert.ToString(g.Rows[giftRowIdx].Cells["colAmount"].Value) == "0"
                            && Convert.ToString(g.Rows[giftRowIdx].Cells["colNote"].Value).Contains("KM: " + promoGiftName)
                            && g.Rows[giftRowIdx].Cells["colItemName"].InheritedStyle.ForeColor == BuiltInThemes.Orange.Success;
                        bool billPromoOk = view.BillPromoLabelForTest.Text.Contains("KM: " + promoBillName)
                            && view.BillPromoLabelForTest.ForeColor == BuiltInThemes.Orange.Success;

                        // Chọn dòng tặng trên lưới -> các nút sửa dòng bị khóa (không sửa tay dòng tặng tự sinh).
                        bool giftLockedOk = false;
                        if (giftRowIdx >= 0)
                        {
                            g.CurrentCell = g.Rows[giftRowIdx].Cells["colItemName"];
                            Application.DoEvents();
                            giftLockedOk = !view.PlusButtonForTest.Enabled
                                && !view.MinusButtonForTest.Enabled
                                && !view.DeleteButtonForTest.Enabled;
                        }

                        // Đổi bảng màu trực tiếp sang Ocean -> màu ô thật trên lưới và nhãn KM tổng đổi theo ThemeTokens.
                        ThemeManager.Apply(BuiltInThemes.Ocean);
                        Application.DoEvents();
                        bool themeSwitchOk = discRowIdx >= 0 && giftRowIdx >= 0
                            && g.Rows[discRowIdx].Cells["colItemName"].InheritedStyle.ForeColor == BuiltInThemes.Ocean.Primary
                            && g.Rows[giftRowIdx].Cells["colItemName"].InheritedStyle.ForeColor == BuiltInThemes.Ocean.Success
                            && view.BillPromoLabelForTest.ForeColor == BuiltInThemes.Ocean.Success;

                        // Kiểm tổng tiền trên UI khớp OrderCalculator: 50k*2 giảm 10% = 90k; giảm tổng 5% theo bước làm tròn hiện hành.
                        decimal expBillDisc = OrderCalculator.LamTron(90000m * 5m / 100m, SystemConfig.LamTronTien);
                        bool uiTotalsOk = view.TongCongBoxForTest.Value == (90000m - expBillDisc) && view.TileGiamBoxForTest.Value == 5m;

                        Check("KM P2 UI §4 & §7 (ServiceOrderView): nhãn KM dòng & tổng rõ không lệch 7 cột, màu ô thật theo ThemeTokens và đổi theo theme, khóa sửa dòng tặng",
                            colsIntact && discRowOk && giftRowOk && billPromoOk && giftLockedOk && themeSwitchOk && uiTotalsOk,
                            "cols=" + colsIntact + ", disc=" + discRowOk + ", gift=" + giftRowOk + ", bill=" + billPromoOk
                            + ", lock=" + giftLockedOk + ", theme=" + themeSwitchOk + ", tot=" + view.TongCongBoxForTest.Value);
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                CleanupPromoApplyFixture(db, fx);
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = oldNegStock ? 30 : 0 },
                    new ConfigChange { Name = "SuDungMatHangMacDinh", DataType = 3, IntValue = oldDefItems ? 30 : 0 },
                    new ConfigChange { Name = "MacDinhGiamGia", DataType = 3, IntValue = (int)oldDefDisc, DecimalValue = oldDefDisc },
                    new ConfigChange { Name = "MacDinhPhiDichVu", DataType = 3, IntValue = (int)oldDefFee, DecimalValue = oldDefFee },
                    new ConfigChange { Name = "MacDinhThueSuat", DataType = 3, IntValue = (int)oldDefTax, DecimalValue = oldDefTax }
                }, "selftest_km2_ui");
                SystemConfig.Reload(db);
            }
        }
    }
}
