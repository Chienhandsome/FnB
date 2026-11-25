# Brief: Báo giá bán hàng

- Spec: `docs/specs/bao-gia-ban-hang.md`. Chiến chốt 25/11 "A, như app gốc" cho SL lẻ (§5.0.2).
- Code **xong, chưa push**, nằm ở worktree `fnb-agy-work\bao-gia-ban-hang` (base `b2df4cc`). Build 0 lỗi; selftest 772 đạt, 7 lỗi cũ. Báo cáo và bảng tiêu chí: issue #18.
- **Bị chặn: 2 câu ở #16** (Excel giá âm / % giảm ngoài 0..100; hộp 2 ĐVT khi cả lẻ và thùng bằng 0). Cả hai là luật Claude đặt, spec đã duyệt không có.
- Đã thử trên app gốc 25/11: SL lẻ lấy theo ô SL ngoài hộp; phiếu có sẵn dòng lẻ thì vẫn cộng ô SL dù lẻ trong hộp = 0. Ảnh `_tmp/_origin-shots/bao-gia-ban-hang/`.
- Chưa kiểm: 125% DPI; hộp nhập Excel và hộp số lượng chưa chụp; ca cả lẻ và thùng = 0 trên app gốc.
- Còn 2 câu không chặn ở #16: Khách đặt hàng có đổi theo A không; menu bị app gốc ẩn có ép hiện không.
