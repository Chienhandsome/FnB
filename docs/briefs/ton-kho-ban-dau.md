# Brief: Tồn kho ban đầu (P1 của Số dư đầu kỳ)
- Spec: `docs/specs/so-du-dau-ky.md` (Claude tự duyệt 27/11). P2 Công nợ ban đầu chưa làm.
- **27/11 đã code, push `1b6998a`** (#55): `OpeningStock*`: màn chọn kho, ngày 31/12 năm trước, lưới Tồn đầu, Giá vốn, Thành tiền, hàng tổng cộng, ghi một phiếu `LOAI = 97` `NAME = TONDAU` mỗi kho, xóa dòng khi về 0, chặn âm, chặn giảm gây âm kho (hàm tồn chung và `ChoPhepAmKho`), tự tính lại giá vốn khi cấu hình bật, quyền, lưu vết, xuất Excel, menu.
- **Chưa kiểm:** bấm thật, 125% DPI. Chưa có nhập Excel (P5).
