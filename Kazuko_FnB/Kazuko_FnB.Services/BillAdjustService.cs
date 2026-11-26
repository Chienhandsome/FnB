using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho "Quản lý bán hàng" và "Điều chỉnh hóa đơn"
    /// (docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md §2–§7, §6.3, §6.4).
    /// Phần cập nhật trong một transaction nằm ở BillAdjustService.Update.cs;
    /// phần hủy phiếu đã thanh toán đặt ở BillAdjustService.Cancel.cs.
    /// </summary>
    public sealed partial class BillAdjustService
    {
        private sealed class BillUpdateRollbackException : Exception
        {
            public BillUpdateRollbackException(string message) : base(message) { }
        }

        private readonly UserSession _session;
        private readonly string _databasePath;
        private readonly SalesManageRepository _repo;
        private readonly ServiceOrderRepository _orderRepo;
        private readonly ServicePaymentRepository _payRepo;
        private readonly CustomerRepository _customerRepo;
        private readonly ActivityLogger _activityLogger;

        public BillAdjustService(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "session");
            _session = session;
            _databasePath = session.DatabasePath;
            _repo = new SalesManageRepository(_databasePath);
            _orderRepo = new ServiceOrderRepository(_databasePath);
            _payRepo = new ServicePaymentRepository(_databasePath);
            _customerRepo = new CustomerRepository(_databasePath);
            _activityLogger = new ActivityLogger(_databasePath, session.UserName, session.UserId);
        }

        public UserSession Session { get { return _session; } }

        // ------------------------------------------------------------------ Phân quyền (§1.1, §2.2, §3.4, §4)

        /// <summary>Quyền SỬA (CanEdit = RoleModes.Edit) trên chức năng "Quản lý bán hàng" (§2.2, §4).</summary>
        public static bool CanEditManage(UserSession session)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            int mode = session.Permissions.ModeById(SalesManageConstants.FuncQuanLyBanHang);
            return (mode & RoleModes.Edit) == RoleModes.Edit;
        }

        /// <summary>Quyền xem màn "Quản lý bán hàng" (CanView).</summary>
        public static bool CanViewManage(UserSession session)
        {
            return TableService.HasFunction(session, SalesManageConstants.FuncQuanLyBanHang);
        }

        /// <summary>Quyền xem màn "Điều chỉnh hóa đơn" (CanView).</summary>
        public static bool CanViewAdjust(UserSession session)
        {
            return TableService.HasFunction(session, SalesManageConstants.FuncDieuChinhHoaDon);
        }

        /// <summary>Quyền đổi khách hàng trong chế độ Điều chỉnh hóa đơn (§1.1, §3.4).</summary>
        public static bool CanChangeCustomerInAdjust(UserSession session)
        {
            return TableService.HasFunction(session, SalesManageConstants.FuncThayDoiKhachHangDieuChinh);
        }

        // ------------------------------------------------------------------ Đọc danh sách & chi tiết bill

        /// <summary>Ngày giao dịch hiện hành (chế độ 0, 1 là ngày máy chủ DB theo §6.4).</summary>
        public DateTime GetBusinessDate()
        {
            return _repo.GetServerDate();
        }

        public List<PaidBillListItem> LoadPaidBills(DateTime fromDate, DateTime toDate)
        {
            return _repo.LoadPaidBills(fromDate, toDate);
        }

        public PaidBillDetail LoadPaidBillDetail(string billId)
        {
            return _repo.LoadPaidBillDetail(billId);
        }

        public CustomerGroupRow LoadCustomerGroup(string customerId)
        {
            return _repo.LoadCustomerGroupByCustomer(customerId);
        }

        public List<ServiceRecipeComponent> LoadRecipe(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return new List<ServiceRecipeComponent>();
            return _repo.Read(conn => ServiceOrderRepository.LoadRecipeIn(conn, null, productId));
        }

        /// <summary>Tạo bản sao độc lập của bill để chỉnh sửa hoàn toàn trong bộ nhớ (§2.2).</summary>
        public static ServiceOrderHeader CloneWorkingBill(ServiceOrderHeader src)
        {
            if (src == null)
                return null;
            var copy = new ServiceOrderHeader
            {
                Id = src.Id,
                Name = src.Name,
                SoHd = src.SoHd,
                Ngay = src.Ngay,
                BatDau = src.BatDau,
                TableId = src.TableId,
                TableName = src.TableName,
                AreaId = src.AreaId,
                AreaName = src.AreaName,
                WarehouseId = src.WarehouseId,
                PriceLevelId = src.PriceLevelId,
                CustomerId = src.CustomerId,
                CustomerName = src.CustomerName,
                StoreId = src.StoreId,
                GuestCount = src.GuestCount,
                PaidFlag = src.PaidFlag,
                TempPrintCount = src.TempPrintCount,
                GroupGuid = src.GroupGuid,
                BookingId = src.BookingId,
                Note = src.Note,
                TienHang = src.TienHang,
                TienHangChuaGiam = src.TienHangChuaGiam,
                GiamGiaMatHang = src.GiamGiaMatHang,
                TileGiamGia = src.TileGiamGia,
                TienGiamGia = src.TienGiamGia,
                GiamTheoTien = src.GiamTheoTien,
                TilePhiDichVu = src.TilePhiDichVu,
                PhiDichVu = src.PhiDichVu,
                PhiDichVuTheoTien = src.PhiDichVuTheoTien,
                TileThue = src.TileThue,
                TienThue = src.TienThue,
                TongCong = src.TongCong,
                BillKhac = src.BillKhac,
                TienPhaiTra = src.TienPhaiTra,
                Lines = new List<ServiceOrderLine>(src.Lines != null ? src.Lines.Count : 0)
            };
            if (src.Lines != null)
            {
                foreach (ServiceOrderLine l in src.Lines)
                {
                    if (l == null)
                        continue;
                    copy.Lines.Add(new ServiceOrderLine
                    {
                        Id = l.Id,
                        OrderId = l.OrderId,
                        ProductId = l.ProductId,
                        ProductTypeId = l.ProductTypeId,
                        DrinkTypeId = l.DrinkTypeId,
                        ItemName = l.ItemName,
                        UnitId = l.UnitId,
                        UnitName = l.UnitName,
                        WarehouseId = l.WarehouseId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        CostPrice = l.CostPrice,
                        DiscountRate = l.DiscountRate,
                        DiscountAmount = l.DiscountAmount,
                        DiscountByMoney = l.DiscountByMoney,
                        Amount = l.Amount,
                        Note = l.Note,
                        ComboId = l.ComboId,
                        ComboParentId = l.ComboParentId,
                        ComboQty = l.ComboQty,
                        XuatVatTu = l.XuatVatTu,
                        TimeCreated = l.TimeCreated
                    });
                }
            }
            return copy;
        }

        /// <summary>Đổi khách trên bill trong bộ nhớ và áp lại giảm giá theo nhóm khách (Q4, §6.3).</summary>
        public void ApplyCustomerChangeInMemory(ServiceOrderHeader workingBill, string customerId, string customerName)
        {
            if (workingBill == null)
                return;
            workingBill.CustomerId = string.IsNullOrEmpty(customerId) ? null : customerId;
            workingBill.CustomerName = customerName ?? "";
            CustomerGroupRow group = !string.IsNullOrEmpty(workingBill.CustomerId)
                ? LoadCustomerGroup(workingBill.CustomerId)
                : null;
            OrderModePolicy.ApplyCustomerGroupDiscount(
                workingBill,
                group,
                SystemConfig.GopChungGiamGiaTienGioVaTienHang,
                SystemConfig.LamTronTien);
        }

        // ------------------------------------------------------------------ Trả đồ (§2.3)

        /// <summary>
        /// Ghi ngay TDONHANG.GIOTRADO = giờ máy chủ khi người dùng xác nhận Trả đồ (§2.3).
        /// Chỉ cần quyền XoaGiamMon (CanLogin); KHÔNG hỏi mật khẩu giảm đồ (NhapMatKhauGiamDo).
        /// Chặn bill thuộc nhóm (§6.3).
        /// </summary>
        public ServiceOpResult RecordReturnTime(string billId, UserSession overrideSession = null)
        {
            if (string.IsNullOrEmpty(billId))
                return ServiceOpResult.Fail(SalesManageConstants.SelectBillFirst);

            UserSession actor = overrideSession ?? _session;
            if (!TableService.HasFunction(actor, ServiceOrderConstants.FuncXoaGiamMon))
                return ServiceOpResult.ReLogin(ServiceOrderConstants.FuncXoaGiamMon);

            ServiceOpResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                ServiceOrderHeader dbBill = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                if (dbBill == null || dbBill.PaidFlag != 30)
                {
                    outcome = ServiceOpResult.Fail(SalesManageConstants.BillNotPaidOrMissing);
                    return;
                }
                if (!string.IsNullOrEmpty(dbBill.GroupGuid))
                {
                    outcome = ServiceOpResult.Fail(SalesManageConstants.GroupBillReturnBlocked);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                SalesManageRepository.UpdateReturnTimeIn(conn, tx, billId, actor.UserId, serverNow);
                outcome = ServiceOpResult.Success(dbBill);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể ghi nhận thời điểm trả đồ.");
        }

        // ------------------------------------------------------------------ Chuẩn bị hộp thanh toán khi cập nhật (§2.2)

        public PaymentDialogContext PrepareUpdatePayment(ServiceOrderHeader workingBill, BillManageMode mode)
        {
            var ctx = new PaymentDialogContext { IsEditPaidBill = true };
            if (workingBill == null || string.IsNullOrEmpty(workingBill.Id))
            {
                ctx.ErrorMessage = SalesManageConstants.SelectBillFirst;
                return ctx;
            }
            if (mode == BillManageMode.Manage && !CanEditManage(_session))
            {
                ctx.ErrorMessage = ServiceOrderConstants.NoPermission;
                return ctx;
            }
            if (!string.IsNullOrEmpty(workingBill.GroupGuid))
            {
                ctx.ErrorMessage = SalesManageConstants.GroupBillEditBlocked;
                return ctx;
            }

            bool hasItem = workingBill.Lines != null
                && workingBill.Lines.Exists(l => l != null && !l.IsMaterialExport && !l.IsComboChild);
            if (!hasItem)
            {
                ctx.ErrorMessage = ServicePaymentConstants.EmptyBillError;
                return ctx;
            }

            decimal roundStep = SystemConfig.LamTronTien;
            OrderModePolicy.RecalcBillTotalsInPlace(workingBill, roundStep);
            ServiceOrderValidationResult val = OrderCalculator.ValidateInvoice(workingBill, workingBill.Lines, roundStep);
            if (!val.IsValid)
            {
                ctx.ErrorMessage = val.ErrorMessage;
                return ctx;
            }

            bool includeAdvance = SystemConfig.SuDungChucNangTamUngTrongDonHang;
            decimal pointRate = SystemConfig.QuyDoi1DiemSangTien;
            int pointMode = SystemConfig.CachTinhDiem;
            int revPerPoint = SystemConfig.DoanhSoTuongUngVoi1Diem;

            _repo.Read(conn =>
            {
                PaidBillDetail dbDetail = SalesManageRepository.LoadPaidBillDetailIn(conn, null, workingBill.Id);
                if (dbDetail == null || dbDetail.Header == null || dbDetail.Header.PaidFlag != 30)
                {
                    ctx.ErrorMessage = SalesManageConstants.BillNotPaidOrMissing;
                    return 0;
                }
                if (dbDetail.IsGroupBill)
                {
                    ctx.ErrorMessage = SalesManageConstants.GroupBillEditBlocked;
                    return 0;
                }

                decimal dep = ServicePaymentRepository.GetDepositIn(conn, null, workingBill.Id, dbDetail.Header.BookingId, includeAdvance);
                var excludeIds = new List<string> { workingBill.Id };
                string custId = workingBill.CustomerId ?? "";

                ctx.PrimaryBillId = workingBill.Id;
                ctx.PrimaryTableId = workingBill.TableId ?? "";
                ctx.GroupGuid = "";
                ctx.TotalBills = workingBill.TongCong;
                ctx.TotalDeposit = dep;
                ctx.InitialCustomerId = custId;
                ctx.ExistingPayment = dbDetail.Payment;
                ctx.Bills.Add(new PaymentBillSlice
                {
                    BillId = workingBill.Id,
                    BillName = workingBill.Name,
                    TableId = workingBill.TableId,
                    TableName = workingBill.TableName,
                    BatDau = workingBill.BatDau,
                    OpenDate = workingBill.Ngay,
                    OriginalCustomerId = workingBill.CustomerId,
                    TongCong = workingBill.TongCong,
                    Deposit = dep
                });

                ctx.InitialCustomerState = BuildCustomerPaymentStateIn(conn, custId, excludeIds, pointMode, revPerPoint, pointRate);
                ctx.Ok = true;
                return 0;
            });

            if (!ctx.Ok)
                return ctx;

            ctx.Customers = _customerRepo.LoadLookup();
            ctx.BankAccounts = _payRepo.LoadBankAccounts();
            if (SystemConfig.BatBuocNhapNhanVienBanHang || (ctx.ExistingPayment != null && !string.IsNullOrEmpty(ctx.ExistingPayment.EmployeeId)))
                ctx.Employees = _payRepo.LoadEmployees();
            return ctx;
        }

        private static CustomerPaymentState BuildCustomerPaymentStateIn(
            FirebirdSql.Data.FirebirdClient.FbConnection conn,
            string customerId,
            IList<string> excludeBillIds,
            int pointCalcMode,
            int revenuePerPoint,
            decimal pointRate)
        {
            var state = new CustomerPaymentState { PointRate = pointRate };
            if (string.IsNullOrEmpty(customerId))
                return state;

            string name, addr, cardId, groupId;
            if (!ServicePaymentRepository.CustomerExistsIn(conn, null, customerId, out name, out addr, out cardId, out groupId))
                return state;

            DateTime serverDate = InvoiceNumberRepository.GetServerTimestamp(conn, null).Date;
            state.CustomerId = customerId;
            state.CustomerName = name;
            state.OldDebt = ServicePaymentRepository.GetOldDebtIn(conn, null, customerId, excludeBillIds);
            state.AvailablePoints = ServicePaymentRepository.GetCustomerPointsIn(
                conn, null, customerId, pointCalcMode, revenuePerPoint, excludeBillIds);

            if (!string.IsNullOrEmpty(cardId))
            {
                PrepaidCardInfo card = ServicePaymentRepository.ValidatePrepaidCardByIdIn(
                    conn, null, cardId, serverDate, excludeBillIds);
                if (card.IsValid && card.Balance > 0m)
                    state.LinkedPrepaidCard = card;
            }
            return state;
        }
    }
}
