# Spec: Danh mục voucher (DVOUCHER, DNHOMVOUCHER) và kiểm voucher khi thanh toán

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; Chiến chốt 27/11: sửa hai chỗ gốc bỏ sót khi kiểm voucher). Điểm **TẠM** ở §7.
- Ngày: 2026-11-27. Loại: **nặng** (voucher là tiền khi thanh toán). Nguồn: `docs/deep/khach-hang.md` §3 (voucher), B15; `su-dung-dich-vu.md` §3.7 (thanh toán bằng voucher đã làm).
- Menu: KHÁCH HÀNG ▸ Danh mục voucher (menu có sẵn trong `SMENU`).

## 1. Phạm vi

| Làm | Không làm |
|---|---|
| Danh mục voucher có cây nhóm voucher: xem, thêm, sửa, xóa voucher và nhóm; ô "Đã dùng"; xuất Excel; sửa kiểm voucher khi thanh toán | Phát hành hàng loạt, nhập Excel, in voucher, báo cáo voucher, khóa thanh toán theo khách của đối tác phân phối |

## 2. Màn Danh mục voucher

Khung `CatalogView` như Danh mục khách hàng, Danh mục nhà cung cấp: cây nhóm bên trái (`DNHOMVOUCHER`: cây `PARENTID`/`SORTORDER`, nhóm gốc là "Tất cả"), lưới bên phải. Chọn nhóm cha thì hiện cả voucher của nhóm con.
- Lưới: **Mã voucher** (`NAME`), **Giá trị** (`GIATRI`), **Ngày phát hành**, **Hạn sử dụng**, **Nhóm voucher**, **Đối tác phân phối** (khách), **Đã dùng** (có/không, và số hóa đơn đã dùng khi có), Ghi chú. Hàng tổng cộng cột Giá trị. Ô tìm không dấu (mã, nhóm, đối tác, ghi chú); lọc "Chỉ voucher còn dùng được" (chưa dùng, trong hạn).
- Hộp thêm/sửa voucher: Mã voucher (bắt buộc, không trùng, so UNICODE_CI_AI, tối đa 255), Giá trị (> 0), Ngày phát hành, Hạn sử dụng (không được trước ngày phát hành), Nhóm voucher (chọn), Đối tác phân phối (chọn khách, tùy chọn), Ghi chú. Người dùng chỉ có quyền Xem thì hộp chỉ đọc.
- Hộp nhóm voucher: Tên (bắt buộc, không trùng trong cùng cha), Ghi chú; thêm nhóm con.
- **Xóa:** voucher chưa được hóa đơn nào tham chiếu (`TDONHANG.DVOUCHERID`, mọi trạng thái) thì xóa cứng; ngược lại chặn, nêu số hóa đơn đã dùng ("Voucher đã được dùng ở hóa đơn X, không xóa được"). Xóa nhóm: chặn khi còn voucher hoặc nhóm con ("nhóm còn N voucher").
- Sửa giá trị, hạn, mã của voucher **đã dùng** thì chặn (giữ nguyên lịch sử); chỉ sửa được ghi chú.
- Ghi lưu vết mỗi thêm/sửa/xóa (chuỗi ghi vào cột có giới hạn phải cắt cho vừa, kể cả `Note` và `TableName` của lưu vết, 255).
- Quyền: chức năng "Danh mục voucher" (Xem, Thêm, Sửa, Xóa); xuất Excel qua `GridExcelExporter`.

## 3. Kiểm voucher khi thanh toán (sửa gốc, Chiến chốt)

Chỗ kiểm voucher hiện có trong thanh toán (Sử dụng dịch vụ, Quản lý bán hàng và Điều chỉnh hóa đơn; tìm trong `ServicePaymentRepository` và nơi gọi `DVOUCHER`): giữ thứ tự và sửa thành:
1. Trống: "Mời bạn nhập mã voucher trước".
2. Không có mã: "Mã voucher 'X' không tồn tại trong hệ thống".
3. **Chưa tới ngày phát hành** (`NGAYPHATHANH` sau hôm nay, so theo ngày): "Voucher 'X' chưa đến ngày phát hành (dd/MM/yyyy)" (gốc không chặn).
4. Quá hạn (`HANSUDUNG` trước hôm nay, so theo ngày): "Mã voucher đã quá hạn sử dụng".
5. **Đã dùng:** có hóa đơn khác `DVOUCHERID` = voucher đó **và đã thanh toán** (`DATHANHTOAN = 30`, `STATUS` còn hiệu lực): "Mã voucher đã được sử dụng". Bill đã hủy hoặc chưa thanh toán **không** làm voucher bị coi là đã dùng (gốc không lọc). Loại trừ chính hóa đơn đang thanh toán/điều chỉnh.
- Giá trị dùng là `GIATRI`; nếu lớn hơn số phải trả thì phần dư bỏ, không trả tiền mặt (quy tắc đã có ở `su-dung-dich-vu.md` §3.7).
- **Không** ràng buộc đối tác phân phối với khách mua (giống gốc; T2).

## 4. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Thêm voucher hợp lệ: có dòng, `STATUS = 30`, người và giờ tạo; mã trùng (khác hoa thường/dấu) bị chặn; giá trị ≤ 0, hạn trước ngày phát hành, thiếu mã: chặn có thông báo.
2. Cây nhóm: thêm nhóm cha và con, chọn cha hiện voucher của cả con; xóa nhóm còn voucher hoặc còn con bị chặn; xóa nhóm trống được.
3. Xóa voucher chưa dùng: mất dòng, có lưu vết; voucher đã gắn vào một hóa đơn thì chặn và nêu số hóa đơn; sửa mã hoặc giá trị voucher đã dùng bị chặn, sửa ghi chú được.
4. Ô "Đã dùng" đúng: chỉ tính hóa đơn đã thanh toán; hàng tổng cộng đúng; lọc "còn dùng được" đúng.
5. Thanh toán bằng voucher (Sử dụng dịch vụ): chưa tới ngày phát hành bị chặn đúng câu; quá hạn bị chặn; voucher của bill **đã hủy** hoặc **chưa thanh toán** dùng lại được; voucher của bill đã thanh toán thì "đã được sử dụng"; chính hóa đơn đang điều chỉnh không tự chặn mình.
6. Quyền: chỉ Xem thì nút và hộp chỉ đọc; chuỗi tên nhóm dài 255 ký tự không làm lưu vết tràn; xuất Excel đúng cột; menu mở màn thật; không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên (được sửa câu thông báo mong đợi nếu câu cũ đổi theo §3).

## 5. Giao diện

Cây nhóm và lưới như màn Danh mục khách hàng; tiêu đề cột đủ rộng không cắt chữ; hộp thoại gọn, hỗ trợ cảm ứng; màu qua `ThemeManager`.

## 6. Quyền và dữ liệu

Không đổi cấu trúc CSDL. Cột dùng: `DVOUCHER` (`NAME`, `NOTE`, `DNHOMVOUCHERID`, `GIATRI`, `NGAYPHATHANH`, `HANSUDUNG`, `DKHACHHANGID`, cột chuẩn), `DNHOMVOUCHER` (cột cây chuẩn), `TDONHANG.DVOUCHERID`.

## 7. Điểm TẠM

- **T1** Thêm các chặn sửa/xóa voucher đã dùng (gốc không nêu rõ cách xóa vì nằm trong khung No1Lib).
- **T2** Đối tác phân phối chỉ là thông tin, không ràng buộc khách dùng (như gốc); nếu Chiến muốn ràng buộc thì làm sau.
- **T3** Phát hành hàng loạt không làm (gốc không có, Q24).
