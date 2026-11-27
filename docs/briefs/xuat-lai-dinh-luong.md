# Brief: Xuất lại định lượng
- Spec: `docs/specs/xuat-lai-dinh-luong.md` (Claude tự duyệt 27/11).
- **27/11 đã code, push `bf9dd5a`** (#57): `MaterialRegen*`: bắt buộc Từ/Đến ngày, Xem trước (không ghi), hộp xác nhận gõ XACNHAN, chạy nền BackgroundWorker, một transaction xóa và sinh lại dòng `XUATVATTU = 30` của hóa đơn bán đã thanh toán có bàn bằng đúng hàm của luồng thanh toán, báo số mặt hàng tồn âm, lưu vết, tính lại giá vốn (`CostRecalcService.RunAfterRegenerate`, lỗi chỉ cảnh báo), quyền.
- **Chưa kiểm:** bấm thật, 125% DPI, hiệu năng với khoảng ngày rất dài (chưa đo). Chưa có khôi phục dòng cũ (T5): nên sao lưu trước khi chạy.
