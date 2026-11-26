# Brief: Khách đặt hàng, chặn SL 0, gộp dòng, nhập số trên lưới

- Spec: `docs/specs/khach-dat-hang.md`, mục "đợt 2" (5 điểm).
- **Đã push `bf45f06` ngày 26/11.** Build 0 lỗi; selftest 814 đạt, 7 lỗi cũ (mốc 798). Báo cáo: issue #24.
- Agy 3 vòng; Claude sửa 1 lỗi vặt (biên dịch). Hàm đọc số dùng chung: `Services/NumberInput.cs`; ô lưới: `Dialogs/NumericEditGridCell.cs`.
- Đã bấm thử app gốc 26/11: bật cờ vẫn gộp 1 dòng; sửa lưới nhận SL 0 (bản mới chặn, Chiến chốt). Ảnh: `_tmp/_origin-shots/khach-dat-hang/`.
- Hai ca selftest cũ giữ tên nhưng tên đã sai nghĩa (script so ca theo tên).
- Còn: quy tắc nhập số cho MỌI ô số khác (Chiến chốt 26/11) chưa có spec, chưa code: lưới Báo giá, các ô `NumericUpDown` (đọc theo Windows; thử ngoài app trên en-US, `2,5` ra 25), `ProductEditDialog`, các màn khác chưa rà.
- Chưa kiểm: gõ bàn phím thật, Theo dõi đặt phòng, 125% DPI, bố cục (ảnh dựng ngoài màn không hiện đầu phiếu).
