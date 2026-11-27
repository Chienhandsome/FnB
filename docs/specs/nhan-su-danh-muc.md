# Spec: Nhân sự P1 (Danh mục nhân viên, Danh mục ca làm việc)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; Chiến chọn làm Nhân sự theo thứ tự đề xuất 27/11). Điểm **TẠM** ở §7.
- Ngày: 2026-11-27. Loại: **nặng vừa** (danh mục chứa số lương gốc dùng cho Tính lương, nhưng màn này chỉ lưu dữ liệu, không tính tiền). Nguồn: `docs/deep/nhan-su.md` §1.1, §1.5 (5.1, 5.2), §2.1, §2.8; mã form gốc `POS_TAP_SRC_EXPORT/STABLEDESC/DNHANVIEN__e5f5937a`, `DCALAMVIEC__4913b117`; bảng `SCOLUMN` trên DEMO.FDB.
- Menu: NHÂN SỰ ▸ Danh mục nhân viên (`SMENU.SORTORDER = ZZZY`), NHÂN SỰ ▸ Danh mục ca làm việc (`ZZZZ`). Nhận diện menu theo tên không dấu, không phân biệt hoa thường; quyền theo tên chức năng "Danh mục nhân viên", "Danh mục ca làm việc" (Xem, Thêm, Sửa, Xóa; admin luôn được).
- Làm trước phần danh mục để các patch sau (Chấm công, Tính lương, Tạm ứng, Thưởng phạt) có dữ liệu nền. **Không** làm ở đây: chấm công, tính lương, chi lương, tạm ứng, thưởng phạt, máy chấm công.

## 1. Khung chung

Hai màn dùng khung `CatalogView` như Danh mục kho hàng, nhà cung cấp, cửa hàng: lưới, hộp Thêm/Sửa, Xóa, ô tìm không dấu, xuất Excel qua `GridExcelExporter`, ghi lưu vết mỗi thêm/sửa/xóa (chuỗi ghi vào cột có giới hạn, kể cả `Note` và `TableName` của lưu vết, cắt cho vừa 255). Dòng mới: `ID` GUID, `STATUS = 30`, người và giờ tạo; sửa: giữ người/giờ tạo, ghi người/giờ sửa. Không đổi cấu trúc CSDL. Bản gốc là danh mục phẳng (không cây): bỏ qua `PARENTID`/`PARENTDIR`.
Quyền chỉ Xem: hộp thoại mở chỉ đọc, ẩn/mờ Thêm, Sửa, Xóa (cùng quy ước các danh mục khác).

## 2. Danh mục nhân viên (`DNHANVIEN`)

Lưới: **Mã** (`CODE`), **Tên nhân viên** (`NAME`, sắp tăng), **Điện thoại**, **Địa chỉ**, **Ca làm việc** (tên ca), **Cách tính lương** (chữ: "Theo ca", "Theo tháng", "Tháng theo ca"), **Lương ca**, **Lương tháng**, Ghi chú. Ô tìm không dấu (mã, tên, điện thoại, địa chỉ, ghi chú). Chỉ liệt kê nhân viên `STATUS = 30` (không có cờ "đã nghỉ" riêng).

Hộp thêm/sửa (thứ tự trường giống gốc):
- **Tên nhân viên** (`NAME`, bắt buộc, tối đa 255, **không trùng** so UNICODE_CI_AI: `SCOLUMN` ghi `ALLOWDUPLICATE = NO`).
- **Mã nhân viên** (`CODE`, tối đa 255): **chỉ hiện và bắt buộc khi cấu hình `TinhLuongSuDungMayChamCong = 30`** (mã khớp `USERID` máy chấm công); ngược lại ẩn và để trống; khi có giá trị thì **không trùng** (so không phân biệt hoa thường) giữa các nhân viên (T2).
- Điện thoại, Địa chỉ (≤ 255), Ghi chú (≤ 255).
- **Ca làm việc** (`DCALAMVIECID`, chọn từ `DCALAMVIEC` đang dùng): chỉ hiện khi `TinhLuongSuDungMayChamCong = 30` và khi đó **bắt buộc**; ngược lại ẩn (giữ nguyên giá trị cũ khi sửa).
- **Cách tính lương** (`CACHTINHLUONG`, nút chọn một): **Theo ca = 30** (nhập `LUONGCA`, ô Lương tháng bị khóa), **Theo tháng = 0** (nhập `LUONGTHANG`, hai ô tích **Nghỉ chủ nhật** `NGHICHUNHAT` và **Nghỉ thứ 7** `NGHITHU7` mở; Lương ca khóa), **Tháng theo ca = 60** (mở cả hai ô lương như gốc: gốc cho `rdLuongThangTheoCa` không khóa ô nào). Ô tích chỉ mở với "Theo tháng". Mặc định tạo mới: Theo ca, Nghỉ thứ 7 **có** (30), Nghỉ chủ nhật **không** (0).
- **Lương ca**, **Lương tháng**: số tiền ≥ 0, nhập bằng ô nhập số chung của dự án, tối đa 9 chữ số phần nguyên, 2 số lẻ khi lưu (cột `NUMERIC(*,2)`); hiện phân cách hàng nghìn. Khi khóa thì **giữ nguyên giá trị đã lưu** (không tự đặt 0).
- **Lương theo ca** (`LUONGTHEOCA`): cột chết trong gốc (không dùng ở đâu): **không có ô nhập**, giữ nguyên giá trị cũ khi sửa, dòng mới để trống. (T3)
- Ảnh (`SIMAGEID`): không làm ô nhập, giữ nguyên giá trị cũ.

**Xóa** (cứng): chặn khi nhân viên còn được tham chiếu ở `DKHACHHANG.DNHANVIENID`, `TTHUCHI.DNHANVIENID`, `TTHUONGPHAT.DNHANVIENID`, `TBANGLUONGCHITIET.DNHANVIENID`, `TBANGLUONGTONGHOP.DNHANVIENID`, `SUSER.DNHANVIENID`, `TSUACHUA.DNHANVIENID`, `TVAORA.DNHANVIENID`; nêu rõ nơi dùng và số lượng ("Nhân viên đang được dùng: 3 phiếu thu chi, 12 dòng chấm công..."); bảng nào không có trong CSDL thì bỏ qua (kiểm bằng `RDB$RELATION_FIELDS`, không đoán). Không có nơi dùng thì xóa được.

## 3. Danh mục ca làm việc (`DCALAMVIEC`)

Lưới: **Tên ca** (`NAME`, sắp tăng), **Từ giờ**, **Đến giờ** (hiện `HH:mm`), **Tỉ lệ lương (%)**, Ghi chú. Ô tìm không dấu.

Hộp thêm/sửa:
- **Tên ca làm việc** (`NAME`, bắt buộc, ≤ 255, **không trùng** UNICODE_CI_AI).
- **Từ giờ** (`TUGIO`), **Đến giờ** (`DENGIO`): kiểu `TIMESTAMP` nhưng chỉ giờ:phút có nghĩa; **chỉ mở nhập khi `TinhLuongSuDungMayChamCong = 30`** và khi đó **bắt buộc cả hai**; ngược lại khóa (như gốc), giữ nguyên giá trị cũ. Ghi giờ vào ngày cố định 01/01/2000 (chỉ phần giờ được dùng); cho phép Đến giờ nhỏ hơn Từ giờ (ca qua đêm, như gốc: công thức lương máy xử lý). Hai giờ bằng nhau: chặn khi bật máy chấm công ("ca làm việc không có thời lượng", T4).
- **Tỉ lệ lương** (`TILELUONG`, %): số 0 đến 1000, mặc định 100 khi tạo mới. Giá trị 0 vẫn lưu được; Tính lương coi 0 là 100 (làm ở patch Tính lương, ghi chú trong hộp "0 được hiểu là 100%").
- Ghi chú (≤ 255).
- Bản gốc lọc ca của màn lương theo `ITEMTYPE` rỗng/0; màn danh mục ghi `ITEMTYPE` để trống như các danh mục khác.

**Xóa** (cứng): chặn khi ca còn được tham chiếu ở `DNHANVIEN.DCALAMVIECID`, `TBANGLUONGCHITIET.DCALAMVIECID`; nêu số nhân viên và dòng chấm công đang dùng.

## 4. Cấu hình `TinhLuongSuDungMayChamCong`

Chỉ **đọc** (`SystemConfig`; thêm thuộc tính nếu chưa có, theo mẫu `SystemConfig.TuDongTinhGiaVon`; giá trị rỗng hoặc khác 30 là tắt). Không có màn bật tắt trong patch này (cấu hình đã có màn Cấu hình hệ thống nếu cấu hình hiển thị ở đó; không sửa). Mọi ô/ràng buộc "chỉ khi bật" tính lại mỗi lần mở hộp thoại.

## 5. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

**Nhân viên:**
1. Thêm hợp lệ chỉ Tên (tắt máy chấm công): có dòng, `STATUS = 30`, `CACHTINHLUONG = 30`, `NGHITHU7 = 30`, `NGHICHUNHAT = 0`, người và giờ tạo; mở lại các giá trị khớp. Thêm đủ trường (Theo tháng, lương tháng 4.000.000, Nghỉ CN và T7): lưu đúng `CACHTINHLUONG = 0`, `LUONGTHANG`, hai cờ; Tháng theo ca lưu `60`.
2. Trống Tên chặn; trùng Tên (khác hoa thường/dấu) chặn; sửa chính nó không báo trùng; lương âm hoặc quá 9 chữ số chặn; chuỗi 256 ký tự chặn; chuỗi 255 ký tự trong lưu vết không tràn.
3. Bật máy chấm công: ô Mã và Ca hiện và bắt buộc (thiếu thì chặn nêu tên trường), trùng Mã (khác hoa thường) chặn; tắt thì ẩn, và sửa nhân viên giữ nguyên `CODE`, `DCALAMVIECID`, `LUONGTHEOCA` cũ. Khóa ô lương theo cách tính: Theo ca khóa Lương tháng nhưng giá trị cũ giữ nguyên.
4. Xóa: nhân viên chưa dùng xóa được (có lưu vết); nhân viên đã có phiếu thu chi / thưởng phạt / chấm công / gắn khách / gắn tài khoản bị chặn và nêu đúng số lượng từng nơi (tạo dữ liệu thật trong selftest cho ít nhất 3 bảng).
5. Quyền: chỉ Xem thì hộp chỉ đọc và nút Thêm/Sửa/Xóa không dùng được; tìm không dấu; xuất Excel đúng cột; menu mở đúng màn; lưới không cắt chữ ở tiêu đề.

**Ca làm việc:**
6. Thêm ca (tắt máy chấm công): chỉ Tên và Tỉ lệ; `TILELUONG` mặc định 100; Từ/Đến giờ khóa. Bật máy chấm công: bắt buộc cả hai giờ; hai giờ bằng nhau chặn; ca qua đêm (22:00 đến 06:00) lưu được và đọc lại đúng giờ:phút.
7. Trùng Tên chặn; tỉ lệ ngoài 0..1000 chặn; xóa ca còn nhân viên hoặc dòng chấm công dùng thì chặn nêu số lượng; xóa ca trống được; lưu vết; quyền; xuất Excel; menu.
8. Không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 6. Giao diện

Như các danh mục đã có: hộp thoại gọn, không cuộn dọc ở 1366×768, tiêu đề cột đủ rộng không cắt chữ, màu qua `ThemeManager`, cảm ứng được; nhóm "Cách tính lương" là hộp nhóm với 3 nút chọn một.

## 7. Điểm TẠM

- **T1** Lưới và thứ tự cột là mặc định của dự án (chưa chụp app gốc).
- **T2** Chặn trùng `CODE` thêm so với gốc (gốc không ràng buộc); cần vì mã gắn với `USERID` máy chấm công.
- **T3** `LUONGTHEOCA` là cột chết, không có ô nhập.
- **T4** Chặn ca có thời lượng 0 khi bật máy chấm công là thêm so với gốc.
- **T5** "Tháng theo ca" mở cả hai ô lương như gốc; ý nghĩa tiền xử lý ở patch Tính lương.
- **T6** Chưa có màn bật tắt `TinhLuongSuDungMayChamCong`; cấu hình tắt mặc định (DEMO để trống), nên Mã/Ca/giờ của ca bị ẩn hoặc khóa trên DB demo.
