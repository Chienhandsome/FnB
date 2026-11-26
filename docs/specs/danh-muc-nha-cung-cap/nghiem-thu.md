# Nghiệm thu, kế hoạch, câu hỏi

## Acceptance criteria (trên bản sao CSDL)

- [ ] Thêm nhà cung cấp chỉ có Mã và Tên: lưu được, `STATUS = 30`, cột người và giờ tạo đúng.
- [ ] Mã mặc định theo mẫu `NCC` + 5 số; ca bảng trống, đã có mã, có mã tay nhập.
- [ ] Trống Mã hoặc Tên bị chặn; trùng bị chặn theo Q2; sửa chính nó không báo trùng.
- [ ] Nhập đủ trường, lưu, mở lại: khớp.
- [ ] Chọn nhóm trên cây lọc đúng; "Tất cả" hiện hết.
- [ ] Xóa nhà cung cấp và nhóm theo Q1; không đổi cấu trúc CSDL.
- [ ] Quyền: mức 80 thêm được, không sửa, không xóa; mức 0 không thấy menu.
- [ ] Không cắt chữ, không cuộn dọc ở 1366×768; đổi bảng màu thì màn đổi theo; không SQL trong View.

## Kế hoạch implement

Làm **sau** Danh mục khách hàng để dùng lại khung cây nhóm và lưới. `Data` (repository nhà cung cấp, nhóm), `Services` (luật, quyền, sinh mã), `UI` (màn, hộp thoại, route), selftest.

## Nguồn đã đọc (26/11/2026)

`STABLEDESC/DNHACUNGCAP__1a86ea05` (`layout.xml`); `SharedCode`; trên bản chép `DATA.fdb`: `SMENU`, `STABLEDESC`, `SCOLUMN`, `SGROUPROLE`, cấu trúc và collation, `RDB$TRIGGERS`. Chưa chụp màn này trên app gốc.

## Câu hỏi mở

| # | Câu hỏi | Loại | Đề xuất |
|---|---|---|---|
| Q1 | Xóa nhà cung cấp đã có phiếu; xóa nhóm còn nhà cung cấp hoặc còn nhóm con | xóa dữ liệu, công nợ | Như khách hàng: chặn khi còn phát sinh, chặn nhóm còn thành viên; nhóm còn nhóm con theo câu 2 của #17 |
| Q2 | So trùng và sinh mã giống màn khách hàng? | dữ liệu | Giống (theo câu 3, 4 của #17) |
| Q3 | Thứ tự cột lưới của app gốc? | hiển thị | Như mục Giao diện |
| Q4 | Chỉ có quyền Xem: hộp thoại mở chỉ đọc không? | quyền | Câu 1 của #17 |
| Q5 | Tên nhóm có bắt buộc và không trùng? | dữ liệu | Bắt buộc, không trùng (như nhóm khách) |

**Phụ thuộc:** dùng khung do Danh mục khách hàng tạo ra, nên theo `workflow-agy.md` §10.2.1 chưa code khi chức năng đó còn bị chặn.

## Chiến chốt 26/11/2026 (thắng bảng trên): "Áp luôn" luật của Danh mục khách hàng

- Xóa nhà cung cấp đã có phiếu nhập hoặc phiếu thu chi: chặn; chưa phát sinh: xóa cứng. Xóa nhóm còn nhà cung cấp hoặc còn nhóm con: chặn.
- So trùng và sinh mã như [`danh-muc-khach-hang.md`](../danh-muc-khach-hang.md) §9.0.2 (tiền tố `NCC`).
- Chỉ có quyền Xem: mở hộp thoại chỉ đọc.
