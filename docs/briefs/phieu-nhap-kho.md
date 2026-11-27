# Brief: Nhập hàng vào kho
- Spec: `docs/specs/phieu-nhap-kho.md` (Claude tự duyệt 27/11; Chiến chốt số phiếu PN, chặn sửa/xóa gây âm kho).
- **27/11 đã code, push `cedccd2`** (#43): lưới Quản lý nhập kho, hộp phiếu nhập (quy đổi 2 ĐVT, công thức dòng và tổng, một transaction), chặn sửa/xóa làm tồn âm, lưu vết, xuất Excel, menu. Hàm tồn của bán hàng (`GetStockBalanceIn`) nay lọc thêm `STATUS = 30` cho thống nhất công thức tồn.
- **Chưa kiểm:** bấm thật, 125% DPI. Chưa tính lại giá vốn sau nhập (T2).
