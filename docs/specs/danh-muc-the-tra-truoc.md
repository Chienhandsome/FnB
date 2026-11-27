# Spec: Danh mục thẻ trả trước (DTHETRATRUOC, DNHOMTHETRATRUOC), nạp tiền và chi trả thẻ

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 28/11/2026 theo workflow-agy §10.1.1; Chiến dặn làm liên tục đến 7h sáng 28/11 và thẻ trả trước nằm trong danh sách "làm sau"). Điểm **TẠM** ở §7, **Chiến xem lại khi dậy** vì có tiền.
- Ngày: 2026-11-28. Loại: **nặng** (nạp tiền thẻ là phiếu thu, chi trả là phiếu chi: đổi quỹ). Nguồn: `docs/deep/khach-hang.md` §2 (2.1 công thức số dư, 2.2 kiểm tra khi dùng thẻ, 2.3 form Thẻ trả trước, Q28, Q30); `docs/specs/phieu-thu-chi.md`, `quy-tien.md`; thanh toán bằng thẻ trả trước đã làm ở `su-dung-dich-vu.md` §3.7 (`ServicePaymentRepository`, hàm số dư thẻ).
- Menu: KHÁCH HÀNG ▸ Danh mục thẻ trả trước (`SMENU.SORTORDER = ZZZ007`, chức năng "Danh mục thẻ trả trước" `09f35712-d2af-489c-ba39-e21f3bb3bf20`); nhận diện theo tên không dấu, không phân biệt hoa thường. Menu chỉ hiện khi cấu hình `SuDungTheTraTruoc = 30` (DB demo đang 30; nếu menu có `VISIBLECONFIGID` thì lọc **chỉ cho menu này**, như đã làm cho Danh mục cửa hàng).
- Quyền: "Danh mục thẻ trả trước" (Xem, Thêm, Sửa, Xóa; admin luôn được); nút **Nạp tiền** cần quyền Thêm, **Chi trả tiền** cần quyền Sửa (T6).

## 1. Phạm vi

| Làm | Không làm |
|---|---|
| Danh mục thẻ có cây nhóm thẻ; hộp thẻ có hai tab lịch sử và ba ô Tổng nạp / Tổng sử dụng / Còn lại; Nạp tiền, Chi trả tiền (ghi phiếu thu/chi gắn thẻ); khóa thẻ; ngày hết hạn; xóa; xuất Excel; lưu vết | Gán thẻ vào khách (đã có ở Danh mục khách hàng, ô `DTHETRATRUOCID`); thanh toán bằng thẻ (đã làm); in thẻ; phát hành thẻ hàng loạt; nhập Excel; hoàn tiền từ bill đã hủy về thẻ |

## 2. Màn Danh mục thẻ trả trước

`CatalogView` có cây nhóm bên trái (`DNHOMTHETRATRUOC`: `PARENTID`/`SORTORDER`/`PARENTDIR`/`ITEMTYPE`/`AUTOID`, nhóm gốc "Tất cả"; chọn nhóm cha hiện cả thẻ của nhóm con), lưới bên phải; cách làm như Danh mục voucher (đọc `Voucher*`, `CatalogView.Voucher*`).
- Lưới: **Mã thẻ** (`NAME`), **Nhóm thẻ**, **Khách đang giữ thẻ** (khách có `DKHACHHANG.DTHETRATRUOCID` = thẻ; nhiều khách thì nối tên), **Ngày hết hạn**, **Khóa** (Có/Không), **Tổng nạp**, **Tổng sử dụng**, **Còn lại**, Ghi chú; hàng tổng cộng 3 cột tiền. Ô tìm không dấu (mã, nhóm, khách, ghi chú); lọc "Chỉ thẻ còn tiền", "Chỉ thẻ chưa khóa". Số dư tính **đúng một hàm** đã có ở thanh toán (`ServicePaymentRepository`: nạp = Σ(`THU` − `CHI`) của `TTHUCHI` có `DTHETRATRUOCID`, trừ các dòng có `STATUS = -30`; sử dụng = Σ `THETRATRUOC` của `TDONHANG` có `DTHETRATRUOCID`, trừ `STATUS = -30`; còn lại = nạp − sử dụng; hàm lấy trừ bill hiện tại, ở đây không trừ bill nào). Không viết công thức thứ hai (Q30: bản desktop đúng, trừ cả `CHI`).
- **Hộp thẻ** (thêm/sửa; gốc 493×439): **Mã thẻ** (`NAME`, bắt buộc, ≤ 255, **không trùng** UNICODE_CI_AI), **Nhóm thẻ trả trước** (chọn, tùy chọn), **Khóa thẻ** (`KHOA`: 30 khi tích, 0 khi không), **Ngày hết hạn** (`NGAYHETHAN`, tùy chọn; bỏ trống = không hết hạn), Ghi chú (≤ 255); hai tab **Lịch sử nạp tiền** (phiếu `TTHUCHI` của thẻ: Số phiếu, Ngày, Số tiền nạp = `THU`, Chi trả = `CHI`; sắp ngày giảm dần) và **Lịch sử sử dụng** (hóa đơn có thẻ: Số phiếu = `NAME`, Ngày, Sử dụng = `THETRATRUOC`); ba ô chỉ đọc **Tổng nạp**, **Tổng sử dụng**, **Còn lại**; nút **Nạp tiền**, **Chi trả tiền** (chỉ khi thẻ đã lưu; thẻ mới phải lưu trước).
- Thẻ **đã khóa** hoặc **hết hạn**: vẫn xem được lịch sử; **Nạp tiền** vẫn cho (nạp vào thẻ khóa/hết hạn có cảnh báo xác nhận, T4); **Chi trả** vẫn cho (rút tiền thừa trả khách).
- **Xóa thẻ:** chặn khi còn dòng `TTHUCHI` (`STATUS <> -30`) hoặc `TDONHANG` tham chiếu, hoặc còn khách đang giữ thẻ (`DKHACHHANG.DTHETRATRUOCID`): nêu số phiếu thu chi, số hóa đơn, số khách ("Thẻ đã có 3 phiếu thu chi, 2 hóa đơn, 1 khách đang giữ, không xóa được"). Thẻ không dùng thì xóa cứng, lưu vết. Xóa nhóm thẻ: chặn khi còn thẻ hoặc nhóm con.
- **Sửa mã thẻ** khi thẻ đã có giao dịch được phép (khách giữ thẻ theo ID, không theo mã); khóa/mở khóa tự do.
- Lưu vết mỗi thêm/sửa/xóa/khóa/nạp/chi (chuỗi ghi vào cột có giới hạn, kể cả `Note`, `TableName`, cắt cho vừa 255). Xuất Excel qua `GridExcelExporter` (đúng cột đang hiện). Chỉ có quyền Xem: hộp chỉ đọc, nút Nạp/Chi không dùng được.

## 3. Nạp tiền, Chi trả tiền (phiếu thu/chi gắn thẻ)

Dùng đúng đường ghi phiếu của patch Phiếu thu chi (`VoucherRepository`, `PhieuThuDialog` ở chế độ mới như đã làm cho Tạm ứng lương và Chi lương; luật quỹ, chi vượt quỹ, sinh số phiếu `PT` + yy/00000 cho thu và `PC` + yy/00000 cho chi; **không viết bản sao thứ hai**):
- **Nạp tiền** = phiếu thu: `LOAI = 0`, `THU` = số tiền > 0, `DTHETRATRUOCID` = thẻ, đối tượng = khách đang giữ thẻ nếu có đúng một khách (nếu không: loại đối tượng khác, tên đối tượng nhập tay, T5), diễn giải mặc định "Nạp tiền thẻ trả trước {mã thẻ}", lý do thu chọn từ `DLYDOTHUCHI` (`LALYDOTHU = 30`; mặc định lý do của phiếu nạp thẻ gần nhất nếu có), cho chuyển khoản (chọn tài khoản như phiếu thu thường), Cửa hàng bắt buộc như phiếu thu thường, Ngày mặc định hôm nay. Tồn quỹ tăng như phiếu thu thường.
- **Chi trả tiền** = phiếu chi: `LOAI = 1`, `CHI` = số tiền > 0, `DTHETRATRUOCID`, **không được lớn hơn số còn lại của thẻ** ("Số tiền chi trả không được lớn hơn số dư thẻ (còn X)"; gốc không chặn nhưng số dư thẻ âm vô nghĩa, T3), diễn giải mặc định "Chi trả tiền thẻ trả trước {mã thẻ}", lý do chi chọn từ lý do chi, chi vượt quỹ xử lý như phiếu chi thường. Quỹ giảm như phiếu chi thường.
- Sửa/xóa phiếu nạp/chi của thẻ: làm ngay trong hai tab lịch sử (nút Sửa, Xóa dòng đang chọn trong tab Lịch sử nạp tiền; mở lại hộp phiếu); khi sửa/xóa mà làm số dư thẻ **âm** (nạp bị giảm/xóa nhưng đã dùng) thì chặn ("Không thể sửa/xóa: số dư thẻ sẽ âm (còn X)"). Các phiếu có `DTHETRATRUOCID` hiện ở Danh mục phiếu thu/chi như phiếu thường nhưng không sửa/xóa được ở đó (đã đúng luật patch Phiếu thu chi, không đổi; thêm kiểm `DTHETRATRUOCID` nếu luật hiện tại chưa chặn, **không đổi hành vi các phiếu khác**).
- Một transaction mỗi lần ghi (hook lỗi giữa chừng thì không dòng phiếu, không đổi quỹ).

## 4. Thanh toán bằng thẻ (đã có, kiểm lại, không đổi)

Spec này **không đổi** luật thanh toán bằng thẻ trả trước (`su-dung-dich-vu.md` §3.7, `ServicePaymentRepository`: kiểm mã, hết hạn, khóa, số dư > 0, cap `Min(còn lại, tổng tiền)`). Chỉ **dùng lại** hàm số dư cho cột/ô Còn lại. Selftest có một ca xác nhận thẻ vừa nạp trong màn này dùng được ở thanh toán (số dư khớp) và thẻ khóa/hết hạn bị thanh toán chặn (nếu ca cũ đã phủ thì chỉ gọi lại hàm).

## 5. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu, số cụ thể)

1. Thêm thẻ hợp lệ (Mã, không nhóm): có dòng, `STATUS = 30`, `KHOA = 0`, người và giờ tạo; trùng Mã (khác hoa thường/dấu) chặn; trống Mã chặn; Mã/Ghi chú 256 ký tự chặn; chuỗi 255 ký tự trong lưu vết không tràn; sửa chính nó không báo trùng; khóa/mở khóa và đặt ngày hết hạn lưu đúng.
2. Cây nhóm: thêm nhóm cha/con, chọn cha hiện thẻ của con; xóa nhóm còn thẻ hoặc còn con chặn; xóa nhóm trống được.
3. Nạp 1.000.000 tiền mặt: `TTHUCHI` `LOAI = 0`, `THU = 1000000`, `DTHETRATRUOCID`, quỹ tiền mặt tăng đúng 1.000.000 (kiểm bằng hàm tồn quỹ chung như ca Phiếu thu chi), Tổng nạp 1.000.000, Còn lại 1.000.000; nạp chuyển khoản 500.000 qua tài khoản A tăng quỹ tài khoản A, thiếu tài khoản bị chặn.
4. Dùng thẻ: hóa đơn `THETRATRUOC = 300.000` gắn thẻ → Tổng sử dụng 300.000, Còn lại 1.200.000 (sau nạp thêm 500.000 → 1.500.000 − 300.000); bill có `STATUS = -30` không tính.
5. Chi trả 200.000 tiền mặt: `LOAI = 1`, `CHI = 200000`, quỹ giảm 200.000, Tổng nạp = Σ(THU − CHI), Còn lại giảm đúng; chi trả lớn hơn còn lại bị chặn; chi vượt quỹ xử lý như phiếu chi thường.
6. Sửa/xóa phiếu nạp: giảm nạp xuống làm số dư âm bị chặn nêu số còn lại; xóa phiếu nạp 1.000.000 khi đã dùng 300.000 bị chặn; xóa phiếu chi trả trả lại số dư; quỹ cập nhật đúng khi sửa/xóa.
7. Xóa thẻ: thẻ có phiếu, có hóa đơn, hoặc có khách giữ đều bị chặn và nêu đúng số lượng từng loại; thẻ trống xóa được, có lưu vết.
8. Lưới: cột Khách đang giữ thẻ, Tổng nạp/Sử dụng/Còn lại và hàng tổng đúng; lọc "Chỉ thẻ còn tiền", "Chỉ thẻ chưa khóa", tìm không dấu; xuất Excel đúng cột.
9. Thẻ khóa/hết hạn: Nạp có cảnh báo xác nhận; thanh toán bằng thẻ khóa/hết hạn vẫn bị chặn (gọi lại hàm đã có); số dư thanh toán khớp ô Còn lại.
10. Quyền: chỉ Xem thì hộp chỉ đọc, nút Nạp/Chi/Sửa/Xóa không dùng được; menu chỉ hiện khi `SuDungTheTraTruoc = 30` và mở đúng màn; một transaction (hook lỗi thì không đổi dòng phiếu, không đổi quỹ); không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 6. Giao diện

Cây nhóm và lưới như Danh mục voucher; hộp thẻ gọn (hai tab, ba ô tổng, nút Nạp/Chi), không cuộn dọc ở 1366×768 (150% DPI), tiêu đề cột đủ rộng không cắt chữ, màu qua `ThemeManager`, cảm ứng được, số hiển thị không phụ thuộc culture máy.

## 7. Điểm TẠM (Chiến xem lại)

- **T1** Số phiếu nạp/chi dùng chuỗi `PT`/`PC` của Phiếu thu chi (gốc có thể dùng chuỗi riêng trong `TTHUCHI2Ae`, chưa kiểm).
- **T2** Cột "Khách đang giữ thẻ" là suy ra từ `DKHACHHANG.DTHETRATRUOCID` (gốc không hiển thị).
- **T3** Chặn chi trả lớn hơn số dư thẻ và chặn sửa/xóa gây số dư âm là thêm so với gốc.
- **T4** Nạp tiền vào thẻ đã khóa/hết hạn cho phép sau xác nhận (gốc không rõ).
- **T5** Đối tượng của phiếu nạp là khách giữ thẻ nếu có đúng một khách, ngược lại nhập tay.
- **T6** Quyền nút Nạp/Chi gắn với Thêm/Sửa của "Danh mục thẻ trả trước" (gốc kiểm quyền theo `TTHUCHI2`, chưa kiểm).
- **T7** Menu chỉ hiện khi `SuDungTheTraTruoc = 30` (theo cấu hình của gốc).
- **T8** Chưa có ảnh app gốc để đối chiếu bố cục hộp thẻ (dùng mặc định dự án).
