# Brief: Danh mục cửa hàng
- Spec: `docs/specs/danh-muc-cua-hang/` (Claude tự duyệt 27/11, Chiến chốt 26/11 ở #19).
- **27/11 đã code, push `37211bd`** (#58): `Store*` + `CatalogView.Store`: thêm/sửa/xóa, trùng Mã/Tên theo collation, chặn xóa khi còn tham chiếu (kho, hóa đơn, phiếu thu chi, người dùng) và cửa hàng cuối, nạp lại cửa hàng/kho của phiên sau lưu, lưu vết, quyền (chỉ Xem thì hộp chỉ đọc), xuất Excel. Menu ẩn/hiện theo `NhieuCuaHang`, bộ lọc `VISIBLECONFIGID` chỉ áp cho menu này.
- **Chưa kiểm:** bấm thật, 125% DPI. Chưa có ảnh app gốc (Q3 thứ tự cột là mặc định). Chưa làm gán người dùng vào cửa hàng.
