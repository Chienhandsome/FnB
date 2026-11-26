# Brief: Nhập số dùng chung

- Spec: `docs/specs/nhap-so/index.md` (Chiến duyệt 26/11). Chia 2 patch.
- **Patch 1 đã push `969285d`** (ô ngoài lưới, `Controls/NumberBox.cs`). Build 0 lỗi; selftest 845 đạt, 7 lỗi cũ (mốc 814). Báo cáo, bảng tiêu chí: issue #25. Số đo: `docs/hieu-qua.md`.
- **Còn patch 2:** lưới Báo giá, định lượng mặt hàng (4 số lẻ), món mặc định khu vực; chặn chữ số lẻ thừa khi gõ ở mọi lưới (gồm Đặt hàng `bf45f06`); Excel theo N7.
- Cho tới khi có patch 2: ô định lượng gõ `0.125` vẫn đọc thành 125, món mặc định khu vực gõ `0,125` cũng vậy (thử ngoài app).
- Đã thử app gốc 26/11: lưới không nhận chữ số lẻ thứ 3; ô Số lượng ngoài lưới làm tròn. Ảnh `_tmp/_origin-shots/nhap-so/`.
- Chưa kiểm: chuột/chạm vào ô đang hiện số có ngăn nghìn, bàn phím thật, 125% DPI; mới chụp Báo giá (có dữ liệu) và Phiếu thu (trống).
- Chưa rà dữ liệu thật đã lưu theo cách đọc cũ.
