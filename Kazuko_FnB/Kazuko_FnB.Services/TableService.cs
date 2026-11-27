using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho màn "Sử dụng dịch vụ" (docs/specs/su-dung-dich-vu.md §3.1–§3.6, §4, §6.4–§6.6):
    /// tải khu vực/bàn với 7 trạng thái ưu tiên, khóa/nhả bàn nhiều máy trạm, mở bàn, và thao tác dòng món.
    /// Phần thêm/giảm/xóa/sửa dòng nằm ở TableService.Lines.cs.
    /// </summary>
    public sealed partial class TableService
    {
        private readonly UserSession _session;
        private readonly string _databasePath;
        private readonly ServiceOrderRepository _repo;
        private readonly CustomerRepository _customerRepo;
        private readonly ActivityLogger _activityLogger;

        public TableService(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "session");
            _session = session;
            _databasePath = session.DatabasePath;
            _repo = new ServiceOrderRepository(_databasePath);
            _customerRepo = new CustomerRepository(_databasePath);
            _activityLogger = new ActivityLogger(_databasePath, session.UserName, session.UserId);
        }

        public UserSession Session
        {
            get { return _session; }
        }

        // ------------------------------------------------------------------ Phân quyền (§4, §6.4 Q5)

        /// <summary>Kiểm quyền theo SFUNCTION.ID. Admin toàn quyền; tài khoản khác chưa cấu hình = không cấp (§6.4 Q5).</summary>
        public static bool HasFunction(UserSession session, string functionId)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null || string.IsNullOrEmpty(functionId))
                return false;
            return session.Permissions.ModeById(functionId) != RoleModes.Locked;
        }

        public bool CanViewPrice()
        {
            return HasFunction(_session, ServiceOrderConstants.FuncXemDonGia);
        }

        public bool CanEditUnitPrice()
        {
            return HasFunction(_session, ServiceOrderConstants.FuncSuaDonGia);
        }

        public bool CanEditLineDiscount()
        {
            return HasFunction(_session, ServiceOrderConstants.FuncGiamGiaMatHang);
        }

        public bool CanEditBillDiscount()
        {
            return SystemConfig.ChoPhepNhapGiamGia && HasFunction(_session, ServiceOrderConstants.FuncThayDoiGiamGiaTong);
        }

        public bool CanEditServiceFee()
        {
            return SystemConfig.CoPhiDichVu && HasFunction(_session, ServiceOrderConstants.FuncThayDoiPhiDichVu);
        }

        public bool CanReduceOrDelete(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncXoaGiamMon);
        }

        // ------------------------------------------------------------------ Đọc sơ đồ bàn & danh mục

        public List<ServiceAreaInfo> LoadAreas()
        {
            return _repo.LoadAreas(_session.UserId, _session.IsAdmin, SystemConfig.PhanQuyenTruyCapTheoKhuVuc);
        }

        public List<ServiceTableInfo> LoadTables(string areaId = null)
        {
            List<ServiceTableInfo> list = _repo.LoadTables(areaId, _session.UserName);
            foreach (ServiceTableInfo t in list)
            {
                t.Status = OrderCalculator.ResolveTableStatus(
                    !string.IsNullOrEmpty(t.ActiveBillId),
                    t.TempPrintCount,
                    t.TableFlag,
                    t.UnderRepairCount,
                    !string.IsNullOrEmpty(t.ReservationBookingId));
            }
            return list;
        }

        public List<ProductGroupRow> LoadMenuGroups()
        {
            return _repo.LoadMenuGroups();
        }

        public List<ServiceMenuItem> LoadMenuItems()
        {
            return _repo.LoadMenuItems(SystemConfig.SapXepThuTuTheo == 1);
        }

        public List<CustomerLookupItem> LoadCustomers()
        {
            return _customerRepo.LoadLookup();
        }

        public List<LookupItem> LoadUnits()
        {
            return _repo.LoadUnits();
        }

        public ServiceOrderHeader LoadBill(string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return null;
            decimal roundStep = SystemConfig.LamTronTien;
            return DbHelper.Read(_databasePath, conn =>
            {
                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, null, billId);
                PopulatePromotionLabelsIn(conn, null, bill, roundStep);
                return bill;
            });
        }

        // ------------------------------------------------------------------ Chọn bàn & khóa bàn (§3.2, §6.5)

        /// <summary>
        /// Chọn bàn trên sơ đồ: kiểm tra khóa nhiều máy trạm (120 giây); nếu bàn có bill chưa thanh toán thì
        /// khóa bàn cho tài khoản hiện tại và tải bill. Nếu bàn trống thì trả Ok với Bill = null.
        /// </summary>
        public ServiceOpResult SelectTable(string tableId, string previousTableId = null)
        {
            if (!string.IsNullOrEmpty(previousTableId) && !string.Equals(previousTableId, tableId, StringComparison.Ordinal))
                UnlockTable(previousTableId);

            if (string.IsNullOrEmpty(tableId))
                return ServiceOpResult.Success(null);

            ServiceTableInfo table = _repo.LoadTable(tableId, _session.UserName);
            if (table == null)
                return ServiceOpResult.Fail("Bàn không còn tồn tại.");

            if (SystemConfig.HeThongChayNhieuMayTram)
            {
                string lockedBy;
                if (_repo.IsTableLockedByOther(tableId, _session.UserName, out lockedBy))
                    return ServiceOpResult.Fail(ServiceOrderConstants.TableLockedByOther(table.Name, lockedBy));
            }

            ServiceOrderHeader bill = _repo.LoadActiveBillByTable(tableId);
            if (bill != null && SystemConfig.HeThongChayNhieuMayTram)
                _repo.LockTable(tableId, _session.UserName);

            return RefreshUnpaidBillOnSelect(bill, tableId);
        }

        public void RenewLock(string tableId)
        {
            if (SystemConfig.HeThongChayNhieuMayTram && !string.IsNullOrEmpty(tableId))
                _repo.LockTable(tableId, _session.UserName);
        }

        public void UnlockTable(string tableId)
        {
            if (!string.IsNullOrEmpty(tableId))
                _repo.UnlockTable(tableId, _session.UserName);
        }

        // ------------------------------------------------------------------ Mở bàn (§3.3, P3, P7, §6.5)

        /// <summary>
        /// Mở hóa đơn mới trên bàn trống:
        /// - Chưa chọn bàn -> "Mời bạn chọn bàn trước khi mở bàn".
        /// - Nhiều máy trạm bị giữ -> chặn.
        /// - Bàn đang sửa chữa (TSUACHUA chưa xong) và chưa xác nhận -> trả NeedsConfirmation (P7).
        /// - Bàn đã có bill -> "Bàn này đang có khách".
        /// - Sinh số hóa đơn qua InvoiceNumberRepository trong cùng transaction; KHÔNG ghi DBAN.TDONHANGID (P3, giữ trigger).
        /// - Thêm món mặc định khu vực nếu SuDungMatHangMacDinh = 30.
        /// - Ghi lưu vết MoBan khi KichHoatLuuVetHoatDong = 30.
        /// </summary>
        public ServiceOpResult OpenTable(string tableId, bool confirmUnderRepair = false, string bookingId = null)
        {
            if (string.IsNullOrEmpty(tableId))
                return ServiceOpResult.Fail(ServiceOrderConstants.SelectTableFirst);

            ServiceTableInfo table = _repo.LoadTable(tableId, _session.UserName);
            if (table == null)
                return ServiceOpResult.Fail("Bàn không còn tồn tại.");

            if (SystemConfig.HeThongChayNhieuMayTram)
            {
                string lockedBy;
                if (_repo.IsTableLockedByOther(tableId, _session.UserName, out lockedBy))
                    return ServiceOpResult.Fail(ServiceOrderConstants.TableLockedByOther(table.Name, lockedBy));
            }

            if (!string.IsNullOrEmpty(table.ActiveBillId))
                return ServiceOpResult.Fail(ServiceOrderConstants.TableOccupied);

            if (table.UnderRepairCount > 0 && !confirmUnderRepair)
                return ServiceOpResult.Confirm(ServiceOrderConstants.UnderRepairWarning(table.Name));

            ServiceOpResult outcome = null;
            decimal roundStep = SystemConfig.LamTronTien;
            InvoiceCycleMode cycleMode = InvoiceNumberFormat.NormalizeMode(SystemConfig.SoHoaDonQuayVongTheo);
            int shiftMinutes = SystemConfig.TuDongDoiGioVaoVeTruoc;
            decimal defaultFeeRate = SystemConfig.CoPhiDichVu ? SystemConfig.MacDinhPhiDichVu : 0m;
            decimal defaultTaxRate = SystemConfig.CoThueSuat ? SystemConfig.MacDinhThueSuat : 0m;
            decimal defaultDiscountRate = SystemConfig.MacDinhGiamGia;
            bool addDefaults = SystemConfig.SuDungMatHangMacDinh;
            bool allowNegativeStock = SystemConfig.ChoPhepAmKho;

            _repo.RunInTransaction((conn, tx) =>
            {
                if (SystemConfig.HeThongChayNhieuMayTram)
                {
                    string holder;
                    if (ServiceOrderRepository.IsTableLockedByOtherIn(conn, tx, tableId, _session.UserName, out holder))
                    {
                        outcome = ServiceOpResult.Fail(ServiceOrderConstants.TableLockedByOther(table.Name, holder));
                        return;
                    }
                }

                if (ServiceOrderRepository.HasActiveBillOnTableIn(conn, tx, tableId))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.TableOccupied);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                DateTime billDate = serverNow.Date;
                DateTime startTime = OrderCalculator.ComputeStartTime(serverNow, shiftMinutes);

                int soHd;
                string billName;
                InvoiceNumberRepository.AllocateNextInTransaction(conn, tx, cycleMode, billDate, _session.UserId, out soHd, out billName);

                string storeId = ServiceOrderRepository.ResolveDefaultStoreIdIn(conn, tx,
                    _session.CurrentStore != null ? _session.CurrentStore.Id : null);
                string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, table.AreaWarehouseId);

                var header = new ServiceOrderHeader
                {
                    Id = DbHelper.NewId(),
                    Name = billName,
                    SoHd = soHd,
                    Ngay = billDate,
                    BatDau = startTime,
                    TableId = table.Id,
                    TableName = table.Name,
                    AreaId = table.AreaId,
                    AreaName = table.AreaName,
                    WarehouseId = warehouseId,
                    PriceLevelId = table.AreaPriceLevelId,
                    StoreId = storeId,
                    GuestCount = 1,
                    BookingId = bookingId,
                    TileGiamGia = defaultDiscountRate,
                    TilePhiDichVu = defaultFeeRate,
                    TileThue = defaultTaxRate
                };

                ServiceOrderRepository.InsertBillHeaderIn(conn, tx, header, _session.UserId, serverNow);

                string promoWarning = null;
                // Thêm mặt hàng mặc định theo khu vực nếu bật cấu hình (§3.3).
                if (addDefaults && !string.IsNullOrEmpty(table.AreaId))
                {
                    List<AreaDefaultItem> defaults = ServiceOrderRepository.LoadAreaDefaultItemsIn(conn, tx, table.AreaId);
                    foreach (AreaDefaultItem def in defaults)
                    {
                        ServiceMenuItem prod = ServiceOrderRepository.LoadProductIn(conn, tx, def.ProductId);
                        if (prod == null || prod.Status != 30 || prod.TempLocked || prod.ProductTypeId == ProductRules.TypeHourlyService)
                            continue;
                        if (!allowNegativeStock)
                        {
                            string stockErr = CheckStockForAdditionIn(conn, tx, header, prod, def.Quantity, warehouseId, null);
                            if (stockErr != null)
                                continue;
                        }
                        decimal price = OrderCalculator.ResolveAreaPrice(prod, table.AreaPriceLevelId);
                        AddLineCoreIn(conn, tx, header, prod, def.Quantity, price, prod.Name, prod.UnitId, "", null, serverNow);
                    }
                    promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, serverNow, allowNegativeStock);
                }

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.MoBan,
                        BillId = header.Id,
                        TableName = table.Name,
                        Note = "Mở hóa đơn trên bàn " + table.Name,
                        Time = serverNow
                    }
                });

                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, header.Id);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });

            if (outcome != null && outcome.Ok && SystemConfig.HeThongChayNhieuMayTram)
                _repo.LockTable(tableId, _session.UserName);

            return outcome ?? ServiceOpResult.Fail("Không thể mở bàn.");
        }
    }
}
