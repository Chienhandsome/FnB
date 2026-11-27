using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ Danh mục đợt khuyến mại (DDOTKHUYENMAI + DDOTKHUYENMAICHITIET,
    /// spec docs/specs/danh-muc-khuyen-mai.md P1 §1–§3, §5, §8):
    /// kiểm quyền, kiểm nhập liệu theo 9 loại hình, chặn đợt trùng khoảng ngày cùng loại,
    /// khóa đổi loại khi đã có chi tiết, ghi đầu đợt + chi tiết + lưu vết TLUUVET trong một transaction,
    /// xóa cứng đợt + chi tiết + lưu vết trong một transaction.
    /// </summary>
    public sealed class PromotionService
    {
        private readonly UserSession _session;
        private readonly PromotionRepository _repo;
        private readonly ProductGroupRepository _groupRepo;
        private readonly ActivityLogger _activityLogger;

        public PromotionService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new PromotionRepository(session.DatabasePath);
            _groupRepo = new ProductGroupRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return PromotionRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return PromotionRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return PromotionRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return PromotionRules.Can(_session, RoleModes.Remove); } }

        public DateTime GetServerDate()
        {
            return _repo.GetServerDate();
        }

        public List<PromotionTypeItem> LoadTypes()
        {
            return _repo.LoadTypes();
        }

        public List<PromotionRow> LoadAll()
        {
            if (!CanView)
                return new List<PromotionRow>();
            return _repo.LoadAll();
        }

        public PromotionRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<PromotionDetailRow> LoadDetails(string promotionId)
        {
            if (!CanView)
                return new List<PromotionDetailRow>();
            return _repo.LoadDetails(promotionId);
        }

        public List<PromotionProductItem> LoadProductsForPicker()
        {
            return _repo.LoadProductsForPicker();
        }

        public List<ProductGroupRow> LoadProductGroups()
        {
            return _groupRepo.LoadAll();
        }

        public PromotionSaveResult Save(
            PromotionRow row,
            IList<PromotionDetailRow> details,
            Action testHookBeforeCommit = null)
        {
            if (row == null)
                return PromotionSaveResult.Fail("Không có dữ liệu đợt khuyến mại.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return PromotionSaveResult.Fail(PromotionText.NoPermission);
            if (!isNew && !CanEdit)
                return PromotionSaveResult.Fail(PromotionText.NoPermission);

            PromotionSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    PromotionTypeItem typeItem = PromotionRepository.LoadTypeByIdIn(conn, tx, row.TypeId);
                    PromotionKind kind = typeItem == null ? PromotionKind.Unknown : typeItem.Kind;
                    if (typeItem != null)
                        row.TypeName = typeItem.Name;

                    if (!isNew)
                    {
                        PromotionRow existing = PromotionRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = PromotionSaveResult.Fail(PromotionText.NotFound);
                            return;
                        }
                        int existingDetails = PromotionRepository.CountDetailsIn(conn, tx, row.Id);
                        if (existingDetails > 0
                            && !string.Equals(existing.TypeId ?? "", row.TypeId ?? "", StringComparison.Ordinal))
                        {
                            outcome = PromotionSaveResult.Fail(PromotionText.TypeLockedWhenHasDetails, hasTypeError: true);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    bool dupName = PromotionRepository.IsNameTakenIn(conn, tx, row.Name, excludeId);

                    PromotionRow overlap = null;
                    if (!row.IsStopped && !string.IsNullOrEmpty(row.TypeId))
                    {
                        List<PromotionRow> sameTypeActive = PromotionRepository.LoadActiveByTypeIn(conn, tx, row.TypeId);
                        overlap = PromotionRules.FindOverlapping(row, sameTypeActive);
                    }

                    IList<PromotionDetailRow> normalizedDetails = NormalizeDetailsForKind(kind, details);
                    PromotionSaveResult valid = PromotionRules.Validate(row, kind, normalizedDetails, dupName, overlap);
                    if (!valid.Ok)
                    {
                        outcome = valid;
                        return;
                    }

                    if (kind != PromotionKind.TotalBillPercent)
                        row.DiscountPercent = 0m;

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    string actorId = _session.UserId ?? "";

                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = PromotionRepository.ActiveStatus;
                        row.CreatedById = actorId;
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        PromotionRepository.InsertHeaderIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = actorId;
                        row.ModifiedAt = serverNow;
                        if (PromotionRepository.UpdateHeaderIn(conn, tx, row) == 0)
                        {
                            outcome = PromotionSaveResult.Fail(PromotionText.NotFound);
                            return;
                        }
                    }

                    PromotionRepository.DeleteDetailsIn(conn, tx, row.Id);
                    if (kind != PromotionKind.TotalBillPercent)
                        PromotionRepository.InsertDetailsIn(conn, tx, row.Id, normalizedDetails, actorId, serverNow);
                    row.DetailCount = kind == PromotionKind.TotalBillPercent ? 0 : normalizedDetails.Count;

                    string note = (isNew ? "Thêm mới đợt khuyến mại: " : "Cập nhật đợt khuyến mại: ") + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = PromotionText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = PromotionRules.ClipLogText(row.Name),
                            Note = PromotionRules.ClipLogText(note),
                            Account = ResolveAccount(),
                            UserId = actorId,
                            Time = serverNow
                        }
                    });

                    if (testHookBeforeCommit != null)
                        testHookBeforeCommit();

                    outcome = new PromotionSaveResult
                    {
                        Ok = true,
                        SavedId = row.Id
                    };
                });
            }
            catch
            {
                if (assignedNewId)
                    ResetNew(row);
                throw;
            }

            if ((outcome == null || !outcome.Ok) && assignedNewId)
                ResetNew(row);
            return outcome ?? PromotionSaveResult.Fail("Không thể lưu đợt khuyến mại.");
        }

        public PromotionDeleteResult Delete(string id, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(id))
                return PromotionDeleteResult.Fail(PromotionText.NotFound);
            if (!CanRemove)
                return PromotionDeleteResult.Fail(PromotionText.NoPermission);

            PromotionDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                PromotionRow existing = PromotionRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = PromotionDeleteResult.Fail(PromotionText.NotFound);
                    return;
                }

                PromotionRepository.DeleteDetailsIn(conn, tx, id);
                if (PromotionRepository.DeleteHeaderIn(conn, tx, id) == 0)
                {
                    outcome = PromotionDeleteResult.Fail(PromotionText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = PromotionText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = PromotionRules.ClipLogText(existing.Name),
                        Note = PromotionRules.ClipLogText("Xóa đợt khuyến mại: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new PromotionDeleteResult { Ok = true };
            });

            return outcome ?? PromotionDeleteResult.Fail("Không thể xóa đợt khuyến mại.");
        }

        private static List<PromotionDetailRow> NormalizeDetailsForKind(PromotionKind kind, IList<PromotionDetailRow> source)
        {
            var list = new List<PromotionDetailRow>();
            if (kind == PromotionKind.TotalBillPercent || source == null)
                return list;
            for (int i = 0; i < source.Count; i++)
            {
                PromotionDetailRow d = source[i];
                if (d == null)
                {
                    list.Add(null);
                    continue;
                }
                list.Add(new PromotionDetailRow
                {
                    ProductGroupId = kind == PromotionKind.ProductGroupPercent ? d.ProductGroupId : null,
                    DiscountPercent = UsesLinePercent(kind) ? d.DiscountPercent : 0m,
                    ProductId = UsesLineProduct(kind) ? d.ProductId : null,
                    SalePrice = (kind == PromotionKind.FixedPrice || kind == PromotionKind.ProductAmount) ? d.SalePrice : 0m,
                    OrderValue = (kind == PromotionKind.OrderValuePercent || kind == PromotionKind.GiftByOrderValue) ? d.OrderValue : 0m,
                    ItemQuantity = kind == PromotionKind.QuantityPercent ? d.ItemQuantity : 0m,
                    BuyQuantity = kind == PromotionKind.BuyXGetY ? d.BuyQuantity : 0m,
                    GiftQuantity = (kind == PromotionKind.BuyXGetY || kind == PromotionKind.GiftByOrderValue) ? d.GiftQuantity : 0m,
                    GiftProductId = (kind == PromotionKind.BuyXGetY || kind == PromotionKind.GiftByOrderValue) ? d.GiftProductId : null,
                    Note = d.Note ?? ""
                });
            }
            return list;
        }

        private static bool UsesLinePercent(PromotionKind kind)
        {
            return kind == PromotionKind.ProductGroupPercent
                || kind == PromotionKind.OrderValuePercent
                || kind == PromotionKind.QuantityPercent
                || kind == PromotionKind.ProductPercent;
        }

        private static bool UsesLineProduct(PromotionKind kind)
        {
            return kind == PromotionKind.BuyXGetY
                || kind == PromotionKind.FixedPrice
                || kind == PromotionKind.ProductPercent
                || kind == PromotionKind.ProductAmount;
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(PromotionRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
