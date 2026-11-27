# Brief: Tính lại giá vốn
- Spec: `docs/specs/tinh-lai-gia-von.md` (Claude tự duyệt 27/11).
- **27/11 đã code, push `3f5c6ce`** (#49), **do Claude viết trực tiếp** (agy hết hạn mức, Chiến đồng ý): `CostRecalcEngine` (thuần bộ nhớ, bước A nguyên liệu/kiêm vật tư, bước B món 2 cấp), `CostRecalcRepository`, `CostRecalcService` (một transaction, lưu vết), màn `CostRecalcView` (chạy nền, tiến độ theo tháng, quyền), tự chạy sau lưu/sửa/xóa phiếu nhập khi `TuDongTinhGiaVon` bật (lỗi chỉ cảnh báo). Menu theo tên "Tính lại giá vốn".
- **Chưa kiểm:** bấm thật, 125% DPI, hiệu năng DB lớn (T5), đối chiếu số với app gốc. Chưa có Xuất lại định lượng và Tồn kho ban đầu.
