# Nghiệm thu, kế hoạch, câu hỏi

## Acceptance criteria (trên bản sao CSDL)

- [ ] `NhieuCuaHang = 30` thì có menu "Danh mục cửa hàng"; khác 30 thì ẩn.
- [ ] Thêm cửa hàng chỉ có Mã và Tên: lưu được, `STATUS = 30`, cột người và giờ tạo đúng.
- [ ] Trống Mã hoặc Tên bị chặn; trùng Mã hoặc Tên với cửa hàng khác bị chặn; sửa chính nó không bị báo trùng (cách so: Q2).
- [ ] Danh sách Mẫu hóa đơn chỉ gồm mẫu có `SFORMID` nêu ở nghiệp vụ §1.
- [ ] Nhập đủ trường, lưu, mở lại: mọi giá trị khớp. Mã quá 36 hoặc trường chữ quá 255 ký tự không lưu được.
- [ ] Lưu xong, cửa hàng và kho của phiên được nạp lại theo nghiệp vụ §3.
- [ ] Xóa theo quyết định Q1; không đổi cấu trúc CSDL.
- [ ] Thiếu quyền Thêm, Sửa, Xóa thì không làm được thao tác đó (ẩn hay mờ, chỉ-xem: Q4).
- [ ] Không cuộn dọc ở 1366×768; đổi bảng màu thì màn đổi theo; không SQL trong View.

## Kế hoạch implement

1. `Data`: repository cửa hàng (đọc, thêm, sửa, xóa, kiểm trùng, đếm tham chiếu), SQL tham số hóa.
2. `Services`: luật kiểm tra, quyền.
3. `UI`: màn lưới, hộp thoại, route trong `ScreenFactory`, ẩn/hiện menu theo cấu hình.
4. Selftest theo tiêu chí trên.

## Nguồn đã đọc (25/11/2026)

`STABLEDESC/DCUAHANG__71442d6c` (`code.cs`, `layout.xml`); trên bản chép `DATA.fdb`: `SMENU`, `STABLEDESC`, `SCOLUMN`, `SCONFIG` (`NhieuCuaHang`, `SharedCode`), `SFUNCTION`, `SGROUPROLE`, cấu trúc `DCUAHANG`, `RDB$TRIGGERS`. **Chưa chụp app gốc** (phiên chưa được cấp quyền điều khiển app).

## Câu hỏi mở

| # | Câu hỏi | Loại | Đề xuất |
|---|---|---|---|
| Q1 | Xóa cửa hàng đang được kho, hóa đơn, phiếu thu chi hoặc người dùng tham chiếu thì sao? Xóa cửa hàng cuối cùng được không? | xóa dữ liệu | Chặn khi còn tham chiếu; chặn xóa cửa hàng cuối cùng (chưa có bằng chứng app gốc) |
| Q2 | So trùng Mã, Tên: hoa/thường, dấu, khoảng trắng đầu/cuối? | dữ liệu | Cùng câu trả lời với câu 3 của issue #17 |
| Q3 | Cột và thứ tự cột trên lưới của app gốc? | hiển thị | Như mục Giao diện |
| Q4 | Chỉ có quyền Xem: hộp thoại mở chỉ đọc không? | quyền | Cùng câu trả lời với câu 1 của issue #17 |
