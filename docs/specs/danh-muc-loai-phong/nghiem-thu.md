# Nghiệm thu, kế hoạch, câu hỏi

## Acceptance criteria (trên bản sao CSDL)

- [ ] Thêm loại chỉ có Tên: lưu được, `STATUS = 30`, `MAUNEN` là trắng, `MAUCHU` là đen, cột người và giờ tạo đúng.
- [ ] Tên trống bị chặn; tên trùng loại khác bị chặn theo collation của cột (`vip` trùng `VIP`); sửa chính nó không báo trùng.
- [ ] Đổi màu chữ, màu nền hoặc gõ tên thì ô xem trước đổi ngay (kiểm màu thật của control).
- [ ] Lưu, mở lại: hai màu và các trường khớp; màn Theo dõi đặt phòng đọc đúng màu đó.
- [ ] Xóa theo quyết định Q1; không đổi cấu trúc CSDL.
- [ ] Quyền theo quyết định Q2.
- [ ] Không cuộn dọc ở 1366×768; đổi bảng màu thì khung đổi theo, màu của loại giữ nguyên; không SQL trong View.

## Kế hoạch implement

1. `Data`: repository loại phòng (đọc, thêm, sửa, xóa, kiểm trùng, đếm tham chiếu), SQL tham số hóa.
2. `Services`: luật kiểm tra.
3. `UI`: màn lưới, hộp thoại, route trong `ScreenFactory`.
4. Selftest theo tiêu chí trên.

## Nguồn đã đọc (26/11/2026)

`STABLEDESC/DLOAIPHONG__4b737682` (`code.cs`, `layout.xml`); trên bản chép `DATA.fdb`: `SMENU`, `STABLEDESC`, `SCOLUMN`, `SFUNCTION`, `SGROUPROLE`, collation cột, `RDB$TRIGGERS`; mã bản mới đọc `DLOAIPHONG`. Chưa chụp màn này trên app gốc.

## Câu hỏi mở

| # | Câu hỏi | Loại | Đề xuất |
|---|---|---|---|
| Q1 | Xóa loại đang gắn cho bàn hoặc phiếu sửa chữa thì sao? | xóa dữ liệu | Chặn khi còn tham chiếu (chưa có bằng chứng app gốc) |
| Q2 | Mục này không gắn chức năng quyền nào. Ai được xem, thêm, sửa, xóa? | quyền | Dùng quyền "Danh mục bàn khu vực" (khác dữ liệu gốc, cần Chiến chốt) |
| Q3 | Cột trên lưới của app gốc? | hiển thị | Như mục Giao diện |
| Q4 | ~~Menu không hiện dù `SMENU` đang bật~~ **Đã rõ 26/11:** mã dùng chung ẩn khi không phải bản HOTEL | phạm vi | Tạm bỏ (xem [`index.md`](index.md)) |
