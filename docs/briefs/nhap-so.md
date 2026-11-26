# Brief: Nhập số dùng chung

- Spec: `docs/specs/nhap-so/index.md` (Chiến duyệt 26/11). Chia 2 patch, **đã code cả hai**.
- Patch 1 `969285d` (ô ngoài lưới, `Controls/NumberBox.cs`), issue #25.
- Patch 2 `054d7c7` (4 lưới sửa được qua `Dialogs/NumericEditGridCell.cs`, Excel Báo giá theo N7), issue #26. Build 0 lỗi; selftest 857 đạt, 7 lỗi cũ (mốc 845).
- Sửa kèm: mở mặt hàng có định lượng từng phát sinh `DataError` ngầm (cột Vật tư gắn danh sách rỗng).
- Chưa kiểm: bàn phím thật, Ctrl+V thật, gõ dấu cách cột Ghi chú Báo giá, chuột/chạm, 125% DPI. A4 chỉ có ca riêng cho lưới Báo giá.
- Còn thấy, chưa sửa: cột Số lượng lưới định lượng hẹp (`1.600,1234` bị cắt, độ rộng có từ trước); `QuickAddRoomDialog` có 2 cột số nguyên ngoài danh sách spec.
- Chưa rà dữ liệu thật đã lưu theo cách đọc cũ.
- `tools/agy/Test-AgyScope.ps1` còn lỗi chặn nhầm khi git in cảnh báo xuống dòng (xem `kinh-nghiem.md` §4), chờ Chiến cho sửa.
