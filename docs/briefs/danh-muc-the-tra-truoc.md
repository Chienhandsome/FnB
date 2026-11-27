# Brief: Danh mục thẻ trả trước
- Spec: `docs/specs/danh-muc-the-tra-truoc.md` (Claude tự duyệt 28/11; có tiền, Chiến xem lại T1–T8).
- **28/11 đã code, push `63df5fb`** (#66): `PrepaidCard*`: `CatalogView` cây nhóm `DNHOMTHETRATRUOC` + lưới (Khách đang giữ, Tổng nạp/Sử dụng/Còn lại), hộp thẻ hai tab lịch sử, Nạp tiền (phiếu thu) và Chi trả (phiếu chi) qua `PhieuThuDialog` chế độ thẻ + `VoucherRepository` (ghi thêm `DTHETRATRUOCID`), số dư dùng chung `GetPrepaidCardBalanceSummaryIn` với thanh toán, chặn chi trả quá số dư và sửa/xóa gây số dư âm, chặn xóa thẻ còn tham chiếu, menu lọc theo `SuDungTheTraTruoc`.
- **Chưa kiểm:** bấm thật, 125% DPI, ảnh app gốc; cờ `KHONGTHAYDOICONGNO` trên phiếu nạp/chi thẻ; quyền Nạp/Chi gắn Thêm/Sửa (T6).
