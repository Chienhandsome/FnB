# Spec: Nhân sự P2 (Thưởng phạt, Tạm ứng lương)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 28/11/2026 theo workflow-agy §10.1.1; Chiến chọn làm Nhân sự theo thứ tự đề xuất 27/11 và dặn làm liên tục đến 7h sáng 28/11). Điểm **TẠM** ở §6.
- Ngày: 2026-11-28. Loại: **nặng** (Tạm ứng lương là phiếu chi, đổi quỹ tiền; Thưởng phạt là đầu vào của Tính lương). Nguồn: `docs/deep/nhan-su.md` §1.1, §1.3 (3.3), §1.5 (5.4), §2.5, §2.8; mã form gốc `POS_TAP_SRC_EXPORT/STABLEDESC/TTHUONGPHAT__820569eb`, `TTHUCHI__97216923`; `docs/specs/phieu-thu-chi.md`, `quy-tien.md`.
- Menu: NHÂN SỰ ▸ Thưởng phạt (`SMENU.SORTORDER = ZZZa`, bảng `TTHUONGPHAT`); NHÂN SỰ ▸ Tạm ứng lương (`ZZZZn`, chức năng "Tạm ứng lương" `20027954-9b80-411b-a9f5-9e45cff99407`). Nhận diện menu theo tên không dấu, không phân biệt hoa thường; quyền theo tên chức năng (Xem, Thêm, Sửa, Xóa; admin luôn được).
- **Không** làm ở đây: Tính lương, Chấm công, Chi lương (patch sau), danh mục lý do thưởng phạt (`DLYDOTHUONGPHAT`, chỉ chọn từ danh sách có sẵn).

## 1. Khung chung

Hai màn lưới có thanh nút Thêm, Sửa, Xóa, Làm mới, ô tìm không dấu, xuất Excel qua `GridExcelExporter`, ghi lưu vết mỗi thêm/sửa/xóa (chuỗi ghi vào cột có giới hạn, kể cả `Note` và `TableName` của lưu vết, cắt cho vừa 255). Thưởng phạt dùng khung `CatalogView` như các danh mục nhân sự P1 (`EmployeeRepository`, `CatalogView.Employee`); Tạm ứng lương dùng khung lưới + hộp thoại của Danh mục phiếu chi (đọc `PhieuThu*`, `FundLedgerRules`, `FundRules`). Không đổi cấu trúc CSDL. Quyền chỉ Xem: hộp thoại chỉ đọc, nút Thêm/Sửa/Xóa không dùng được.

## 2. Thưởng phạt (`TTHUONGPHAT`)

Lưới: **Số phiếu** (`NAME`), **Ngày**, **Nhân viên**, **Thưởng**, **Phạt**, **Lý do** (`DLYDOTHUONGPHAT.NAME`), Ghi chú; hàng tổng cộng Thưởng và Phạt. Bộ lọc: **Từ ngày, Đến ngày** (mặc định tháng hiện tại), **Nhân viên** (tất cả hoặc một), ô tìm không dấu (số phiếu, nhân viên, lý do, ghi chú).

Hộp thêm/sửa (thứ tự trường như gốc):
- **Số phiếu** (`NAME`, bắt buộc, tối đa 255): tự sinh khi thêm `TP` + `yy` + `/` + 5 chữ số tăng dần (lớn nhất hiện có cùng tiền tố + 1; sửa tay được; **không trùng**, so không phân biệt hoa thường) (T1).
- **Ngày** (`NGAY`, mặc định hôm nay): bị khóa khi không có quyền "Thay đổi ngày trong các phiếu" (admin luôn được).
- **Nhân viên** (`DNHANVIENID`, bắt buộc, chọn từ nhân viên `STATUS = 30`).
- **Thưởng** và **Phạt** (`THUONG`, `PHAT`): số tiền ≥ 0, `NUMERIC(18,2)`, ô nhập số chung của dự án, tối đa 9 chữ số phần nguyên; **ít nhất một trong hai > 0** (cả hai bằng 0 hoặc để trống: chặn "Nhập số tiền thưởng hoặc phạt") (T2: gốc không chặn). Cho nhập cả hai trên một phiếu (như gốc).
- **Lý do** (`DLYDOTHUONGPHATID`): chọn từ `DLYDOTHUONGPHAT` đang dùng, tùy chọn.
- **Ghi chú** (`NOTE`, ≤ 255).
- **Giới hạn tháng:** khi mở hộp từ chi tiết bảng lương (patch Tính lương sẽ gọi) có thể đặt giới hạn tháng/năm: ngày ngoài tháng đó chặn "Ngày phải nằm trong tháng M năm Y". Màn Thưởng phạt tự mở không có giới hạn. Cung cấp tham số giới hạn này ngay ở hộp (kiểm tra trong selftest bằng gọi trực tiếp).
- Dòng mới: `ID` GUID, `STATUS = 30`, người và giờ tạo; sửa giữ người/giờ tạo, ghi người/giờ sửa.

**Xóa:** xóa cứng sau xác nhận, có lưu vết. Không có chặn tham chiếu (không bảng nào trỏ tới `TTHUONGPHAT`). Phiếu đã được một bảng lương tính (không có cột liên kết trong CSDL: `TTHUONGPHAT` chỉ lọc theo ngày và nhân viên) thì xóa/sửa vẫn được; Tính lương tính lại khi bấm Tải lại (T3).

## 3. Tạm ứng lương (`TTHUCHI` với `LATAMUNG = 30`)

Lưới các phiếu chi `LOAI = 1`, `LATAMUNG = 30`, `STATUS = 30`: **Số phiếu**, **Ngày**, **Nhân viên** (`DNHANVIENID` hiện tên; nếu rỗng thì `TENDOITUONG`), **Số tiền** (`CHI`), **Lý do** (`DLYDOTHUCHI.NAME`), **Diễn giải**, **Cửa hàng** (cột chỉ hiện khi có nhiều cửa hàng), Ghi chú; hàng tổng cộng Số tiền. Bộ lọc: Từ ngày, Đến ngày (mặc định tháng hiện tại), Nhân viên, ô tìm không dấu.

Hộp **Tạo/Sửa tạm ứng lương**: dùng đúng cách ghi phiếu chi của patch Phiếu thu chi (dùng lại hàm lưu, kiểm quỹ, số phiếu, lưu vết đã có; **không viết bản sao thứ hai** của luật quỹ):
- Phiếu mới: `LOAI = 1`, `LATAMUNG = 30`, `LOAIDOITUONG = 1` (nhân viên) và **khóa**, **Nhân viên bắt buộc** (`DNHANVIENID` và `TENDOITUONG` = tên nhân viên), **ẩn ô chuyển khoản** (tạm ứng luôn tiền mặt: `CHUYENKHOAN = 0`), Số phiếu tự sinh `PC` + yy + `/` + 5 chữ số như phiếu chi, Ngày (khóa nếu không có quyền đổi ngày), Cửa hàng (bắt buộc; người không phải admin chỉ chọn trong cửa hàng của mình), **Số tiền** (`CHI` > 0), **Lý do chi** (`DLYDOTHUCHI`, chỉ lý do chi `LALYDOTHU <> 30`; mặc định lý do tạm ứng lương gần nhất nếu có, như gốc "phiếu `LATAMUNG` gần nhất"), Diễn giải (mặc định "Tạm ứng lương"; cho sửa), Ghi chú.
- **Quỹ:** chi tiền mặt giảm quỹ tiền mặt ngay khi lưu theo `quy-tien.md`/`FundLedgerRules`; chi vượt quỹ (số dư tiền mặt sau chi < 0) xử lý đúng như phiếu chi thường của dự án (dùng cùng hàm, cùng thông báo).
- **Không** chặn số tiền theo lương đã tính hay theo bảng lương (như gốc: không có trần, T4).
- Sửa: chỉ phiếu `LATAMUNG = 30` và không có `TBANGLUONGID` (phiếu chi lương thật không sửa ở đây); sửa số tiền/ngày/lý do cập nhật quỹ như Phiếu thu chi; xóa: xóa cứng, quỹ trả lại như phiếu chi thường, lưu vết.
- Phiếu `LATAMUNG = 30` **hiện ở Danh mục phiếu chi như phiếu thường nhưng không sửa/xóa được ở đó** (đã đúng luật patch Phiếu thu chi, không đổi); màn Tạm ứng lương là nơi sửa/xóa.

## 4. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

**Thưởng phạt:**
1. Thêm phiếu thưởng 200.000 cho NV A: một dòng `TTHUONGPHAT`, `THUONG = 200000`, `PHAT = 0`, `STATUS = 30`, người và giờ tạo; số phiếu tự sinh `TP26/00001` rồi `TP26/00002` (lưu liên tiếp); thêm phiếu phạt 50.000 và phiếu có cả hai (30.000/10.000) lưu đúng.
2. Chặn: thiếu Số phiếu, trùng Số phiếu (khác hoa thường), thiếu Nhân viên, cả Thưởng và Phạt bằng 0, số âm, quá 9 chữ số, ghi chú 256 ký tự; chuỗi 255 ký tự không làm lưu vết tràn.
3. Giới hạn tháng: đặt giới hạn 03/2004, ngày 05/04/2004 bị chặn nêu tháng 3 năm 2004; ngày 15/03/2004 lưu được.
4. Lưới: lọc Từ/Đến ngày và Nhân viên đúng; hàng tổng Thưởng/Phạt đúng; tìm không dấu; xuất Excel đúng cột; ngày bị khóa khi không có quyền đổi ngày.
5. Sửa giữ người/giờ tạo, ghi người/giờ sửa; xóa có lưu vết; chỉ Xem thì hộp chỉ đọc và nút không dùng được; menu mở đúng màn.

**Tạm ứng lương:**
6. Thêm tạm ứng 500.000 cho NV A: một dòng `TTHUCHI` `LOAI = 1`, `LATAMUNG = 30`, `CHI = 500000`, `LOAIDOITUONG = 1`, `DNHANVIENID = A`, `CHUYENKHOAN = 0`, `STATUS = 30`; Tồn quỹ tiền mặt giảm đúng 500.000; số phiếu `PC` + yy theo chuỗi phiếu chi.
7. Chặn: thiếu Nhân viên, thiếu Cửa hàng, số tiền ≤ 0, lý do thu (`LALYDOTHU = 30`) không có trong danh sách; hộp không có ô chuyển khoản.
8. Sửa số tiền 500.000 → 300.000: quỹ tiền mặt tăng lại 200.000; xóa tạm ứng: quỹ trở về như trước khi tạo; chi vượt quỹ xử lý đúng như phiếu chi thường (cùng thông báo).
9. Phiếu `LATAMUNG = 30` không sửa/xóa được ở Danh mục phiếu chi (vẫn đúng); phiếu chi thường không hiện ở màn Tạm ứng lương; phiếu có `TBANGLUONGID` không hiện ở đây.
10. Lưới Tạm ứng lương: lọc, hàng tổng, tìm, xuất Excel; một transaction (hook lỗi giữa chừng thì không dòng phiếu, không đổi quỹ); quyền chỉ Xem; menu mở đúng màn.
11. Không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 5. Giao diện

Như các danh mục đã có: hộp thoại gọn, không cuộn dọc ở 1366×768, tiêu đề cột đủ rộng không cắt chữ, màu qua `ThemeManager`, hỗ trợ cảm ứng, ô nhập số dùng ô nhập số chung của dự án.

## 6. Điểm TẠM

- **T1** Số phiếu thưởng phạt `TP` + yy/00000 là suy đoán (gốc sinh số trong khung No1Lib).
- **T2** Chặn phiếu thưởng phạt có cả Thưởng và Phạt bằng 0 là thêm so với gốc.
- **T3** Sửa/xóa thưởng phạt sau khi đã tính lương không báo gì (Tính lương tính lại khi Tải lại, như gốc).
- **T4** Không có trần tạm ứng theo lương (như gốc).
- **T5** Tạm ứng luôn tiền mặt (gốc ẩn ô chuyển khoản khi `LATAMUNG = 30`).
- **T6** Công thức trừ tạm ứng ở Tính lương: Chiến chưa chốt R1 (xem `nhan-su.md` §2.5); Claude chọn đề xuất `SUM(CHI)` cho cả hai form khi làm patch Tính lương.
