# Chốt 28/11 đợt B và cây nhân viên (brief)

Spec: `docs/specs/chot-2811-b.md`, `docs/specs/nhan-su-cay.md`, `docs/specs/chien-chot-28-11.md`.

## Đã làm (đều trên main)
- `5e3a858`: Nhân sự: tên 3 cách tính lương như gốc ("Lương theo ca", "Lương tháng theo ngày", "Lương tháng theo ca"), khóa Lương ca ở hai kiểu tháng. Claude tự viết.
- `539f7a8`: menu Voucher, Khuyến mại, Thẻ trả trước, Kiểm soát order, Mật khẩu wifi, Cửa hàng hiện/ẩn theo cờ `SCONFIG` (`VISIBLECONFIGID`). Claude tự viết; ca selftest cũ cần menu hiện nên bọc `WithMenuFlagsOn`.
- `6a2d6e7` (agy, 2 vòng): Tạm ứng lọc Từ/Đến để trống (ô tích); Tồn đầu thêm cột "Mã sẵn có" và hộp "Tổng giá trị"; Bảng lương sửa Số phiếu, Lưu & Mới, Lưu & In, hỏi khi thoát có thay đổi. Selftest 1558 → 1570.
- `b71c2fd` (agy, 2 vòng): Danh mục nhân viên dạng cây (thư mục ITEMTYPE 1, phân cách ITEMTYPE 2, Thùng rác STATUS 0, khôi phục, xóa hẳn); mọi ô chọn nhân viên lọc bỏ thư mục/phân cách/Thùng rác. Selftest → 1578.
- "Phân loại" của phiếu chi = Lý do chi (đã có ở bản mới).

## Điều chưa kiểm / cần Chiến thử
- Cây nhân viên: bấm thử trong app (Thêm mới ▾, Thùng rác, Shift+Del). Định dạng `PARENTDIR` theo `AUTOID` của cha như gốc.
- Ctrl+S ở hộp Thêm bảng lương nay là "Lưu & Mới".
- Lưu & In mở màn xem bảng lương hiện có, chưa có báo cáo in riêng.
- Cờ chập chờn: khi chạy selftest bản mới, có lúc hiện hộp "Cảnh báo: Chưa có ca làm việc nào." chặn selftest (chưa tìm ra ca nào gây ra; đóng hộp thì selftest tiếp tục và đạt).
