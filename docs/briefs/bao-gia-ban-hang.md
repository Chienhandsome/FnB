# Brief: Báo giá bán hàng

- Spec: `docs/specs/bao-gia-ban-hang.md` (đã duyệt 25/11).
- **Chưa có code. Bị chặn: issue #16** (cho-chien).
- Đã làm 25/11 (vòng lặp): tạo worktree trên base `2afe509`; mốc selftest 711 đạt, 7 lỗi cũ; đọc `TBAOGIA__b2d3ffd4/code.cs`. Chưa giao agy.
- Lý do chặn: mã gốc ghi số lượng lẻ theo ô SL ngoài form (`code.cs:38`, `:79`), số lẻ sửa trong hộp "Nhập số lượng" chỉ dùng để kiểm bằng 0 (`:157`). Làm như vậy có trường hợp ra dòng số lượng 0, trái P3. Khách đặt hàng bản mới lấy số trong hộp.
- Đã thử trên app gốc 25/11 (comment ở #16): đúng như mã, gồm ca mất số lẻ và ca dòng số lượng 0. App gốc đang ẩn menu này (`SMENU.STATUS = 0`). Ảnh: `_tmp/_origin-shots/bao-gia-ban-hang/`.
- Còn: chờ trả lời, ghi vào spec, rồi làm §4. Ghi chú viết prompt ở `_tmp\bao-gia-ban-hang\notes.md`.
