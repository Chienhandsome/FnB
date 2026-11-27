# Brief: Kiểm kê kho
- Spec: `docs/specs/kiem-ke-kho.md` (Claude tự duyệt 27/11).
- **27/11 đã code, push `dd98a2f`** (#48): lưới Quản lý kiểm kê và hộp phiếu (`StockTake*`), tồn hệ thống tính lại lúc lưu (báo và không lưu nếu khác số đã thấy), `SLXUAT`/`SLNHAP` = max(0, chênh lệch) theo ĐVT cơ sở (sửa lỗi gốc ghi thiếu khi bật 2 ĐVT), chặn sửa/xóa gây âm kho, nút Mặt hàng chưa kiểm kê, cột Kho thêm vào lưới, lưu vết, xuất Excel, menu.
- **Chưa kiểm:** bấm thật, 125% DPI. Chưa làm: nhập từ máy kiểm kho (.txt), nhập Excel.
