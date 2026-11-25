# Nghiệm thu, kế hoạch, câu hỏi

## Acceptance criteria (trên bản sao CSDL)

- [ ] Thêm kho có Tên và Cửa hàng: lưu được, `STATUS = 30`, cột người và giờ tạo đúng.
- [ ] Trống Tên hoặc Cửa hàng bị chặn; tên trùng theo collation cột bị chặn; sửa chính nó không báo trùng.
- [ ] Cờ âm kho lưu và đọc lại đúng giá trị theo Q2; không đổi luật kiểm tồn ở màn khác.
- [ ] Xóa theo quyết định Q1; không đổi cấu trúc CSDL.
- [ ] Quyền theo `SGROUPROLE`: tài khoản mức 112 thêm, sửa được nhưng không xóa được; mức 0 không thấy menu.
- [ ] Không cắt chữ, không cuộn dọc ở 1366×768; đổi bảng màu thì màn đổi theo; không SQL trong View.

## Kế hoạch implement

1. `Data`: repository kho (đọc, thêm, sửa, xóa, kiểm trùng, đếm tham chiếu), SQL tham số hóa.
2. `Services`: luật kiểm tra, quyền.
3. `UI`: màn lưới, hộp thoại, route trong `ScreenFactory`.
4. Selftest theo tiêu chí trên.

## Nguồn đã đọc (26/11/2026)

`STABLEDESC/DKHOHANG__221299bf` (`layout.xml`); các form dùng `CHOPHEPAMKHO`; `SharedCode`; trên bản chép `DATA.fdb`: `SMENU`, `STABLEDESC`, `SCOLUMN`, `SFUNCTION`, `SGROUPROLE`, cấu trúc và collation `DKHOHANG`, `RDB$TRIGGERS`. Chưa chụp màn này trên app gốc.

## Câu hỏi mở

| # | Câu hỏi | Loại | Đề xuất |
|---|---|---|---|
| Q1 | Xóa kho đang được cửa hàng, khu vực, hóa đơn hoặc phiếu kho tham chiếu thì sao? Xóa kho cuối cùng được không? | xóa dữ liệu, kho | Chặn khi còn tham chiếu; chặn xóa kho cuối cùng (chưa có bằng chứng app gốc) |
| Q2 | Cờ âm kho: ghi 30 khi tích, 0 khi bỏ; kho mới mặc định gì? | kho | 30 / 0; mặc định không tích (0). Cần xác minh trên app gốc |
| Q3 | Cột và thứ tự cột trên lưới của app gốc? | hiển thị | Như mục Giao diện |
| Q4 | Chỉ có quyền Xem: hộp thoại mở chỉ đọc không? | quyền | Cùng câu trả lời với câu 1 của issue #17 |
