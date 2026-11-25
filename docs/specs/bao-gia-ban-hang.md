# Spec (delta): Báo giá bán hàng (TBAOGIA)

- Trạng thái: **ĐÃ DUYỆT** (Chiến duyệt 25/11/2026 trong phiên Claude Code; chưa code)
- **Đọc trước:** §5.0 và §5.0.1 **thắng mọi chỗ khác** trong spec. Câu cũ trong thân spec (nhất là "nằm trong DLL") đã lỗi thời: mã dùng chung ở `SCONFIG` bản ghi `SharedCode`.
- **Điểm còn chưa xác minh:** khi làm tới thì tự xác minh từ mã nguồn hoặc CSDL. Không chứng minh được: phần chỉ hiển thị thì theo mặc định tạm của spec; phần **tiền, giá, kho, công nợ, quyền, xóa dữ liệu** thì không đoán, mở issue `cho-chien` và xử lý theo `workflow-agy.md` §10.2.1. Q2 (số phiếu): áp mặc định tạm, quay vòng theo năm, nếu không tìm được bằng chứng khác.
- Ngày: 2026-11-24. Base: `main` @ `9e48bc8` (đã `git fetch origin main`, working tree chỉ lệch file prompt không thuộc spec)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md) và, vì cùng kiểu màn chứng từ (`TABLETYPE = 3`) + chi tiết (`TABLETYPE = 4`), [`khach-dat-hang.md`](khach-dat-hang.md). Spec này **chỉ ghi phần khác**; chỗ không nói = như khuôn / như Khách đặt hàng.
- Menu: **HOẠT ĐỘNG > Báo giá bán hàng** (`SMENU` `bd2b1cfd-0503-40d1-8f2f-39bb1c7817af`, `SORTORDER = ZZZ012`, `OPERATION = 0`, không `MENUCODE`, `SMENU.SFORMID = NULL`, `SMENU.SFUNCTIONID = NULL`, `STABLEDESCID` = bảng mô tả bên dưới). Không phải form tùy biến: là màn danh sách chứng từ chuẩn của khung `STABLEDESC`.
- Bảng chính `TBAOGIA` (`STABLEDESC` `b2d3ffd4-9037-4dd1-b255-ec16dd122e66`, `TABLETYPE = 3`), chi tiết `TBAOGIACHITIET` (`STABLEDESC` `fc920f2a-158a-4acf-97d8-701bf00d9812`, `TABLETYPE = 4`).
- Phiếu báo giá cho khách: **không** tác động kho, công nợ, quỹ, điểm. Là tài liệu in/gửi, không thanh toán.

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** đo trên **bản chép** `POS TAP\Database\DATA.fdb` (không dùng `DEMO.FDB`; không mở file gốc). `TBAOGIA` hiện **0 bản ghi**, `TBAOGIACHITIET` **0 bản ghi** → không có dữ liệu mẫu thật; selftest §7 phải tự tạo.
- **[CODE]** `POS_TAP_SRC_EXPORT/STABLEDESC/TBAOGIA__b2d3ffd4/` (`code.cs` lớp `TBAOGIAAe`, `layout.xml`); tài liệu nghiệp vụ sẵn có [`pos-tap-nghiep-vu-khu-vuc-ban-bao-gia.md`](../pos-tap-nghiep-vu-khu-vuc-ban-bao-gia.md) §3 (đã đọc, không chép lại).
- **[APP]** Chưa chụp app gốc cho màn này (DB đủ để suy ra, không cần). Các phần bố cục dialog lấy từ `layout.xml` ⇒ đánh dấu **[CHƯA XÁC MINH BẰNG APP]**; cần 1–2 ảnh khi Chiến muốn (Q8).
- Quyền: `SFUNCTION` `7be6722c-aeed-486a-85ef-607b8b4c7407` "Báo giá" (nhóm "Bán hàng", `NOTE = View`).

## 1. Bố cục UI (khác khuôn)

### 1.1 Màn danh sách [DB + CODE]

- Giống Khách đặt hàng: tab chứng từ, panel lọc trái, lưới phải, thanh nút trên (**Thêm mới, Chỉnh sửa, Xóa, Xuất excel, In** + ô Lọc F3).
- **Bộ lọc** (từ `STABLEDESC.FILTERCONFIG`): **Ngày** (mặc định "30 ngày gần nhất"), **Số phiếu**, **Khách hàng** (`DKHACHHANGID`). Chỉ 3 lọc (không có "Phương thức" như Đặt hàng).
- **Số phiếu tự sinh** theo mẫu `NOTEMPLATE` = **`BG(yy)/(*****)`**, `GENNO = 30` ⇒ dạng `BG26/00001` (tiền tố `BG`, 2 số năm, `/`, 5 số tăng dần; quay vòng theo năm). Cơ chế sinh/đánh lại số như các chứng từ khác dùng `GENNO` (xem khuôn Khách đặt hàng §3.1).
- Cột lưới: cấu hình trong `AELAYOUT` (blob nhị phân, **chưa giải mã**) → **[CHƯA XÁC MINH]**; mặc định tạm theo trường bảng: Ngày, Số phiếu, Tên khách, Địa chỉ, Điện thoại, Email, Tiền hàng, Giảm giá, Thuế, Phí vận chuyển, Tổng cộng, Ghi chú.
- Có nút **In** (`INVOICEPRINT = 30`): mẫu in "Báo giá" trong `SREPORT` — **không tìm thấy** mẫu tên "báo giá" trong DB chép; v1 nút In báo "chưa hỗ trợ" nếu chưa có mẫu (Q4).

### 1.2 Dialog "BÁO GIÁ - THÊM MỚI / SỬA" (`TBAOGIAAe`) [CODE + layout, CHƯA XÁC MINH BẰNG APP]

Mở **tối đa hóa** (`Maximize`, `AllowMaxWindows`), **không** lưu bằng Enter (`SetSaveByEnterKey(false)`), phím **F3** nhảy vào ô "Tìm". Bố cục như `TDATHANGAe` nhưng bỏ phần đặt tiền trước/phương thức:

| Control (nhãn) | Trường DB | Ghi chú |
|---|---|---|
| Ngày | `NGAY` | mặc định hôm nay |
| Số phiếu | `NAME` | tự sinh (§1.1), bắt buộc, không trùng |
| Khách hàng (lookup) | `DKHACHHANGID` | chọn → tự điền 4 ô dưới và **khóa** chúng; bỏ chọn → mở khóa, xóa trống (`EnableCustomerCtrl`) |
| Tên khách / Địa chỉ / Điện thoại / Email | `TENKHACH`, `DIACHI`, `DIENTHOAI`, `EMAIL` | nhập tay khi không chọn khách (khách vãng lai) |
| Ghi chú | `NOTE` | |
| Tiền hàng | `TIENHANG` | tính |
| Giảm giá: % và tiền | `TILEGIAMGIA`, `TIENGIAMGIA`, cờ `GIAMTHEOTIEN` | sửa ô % → `GIAMTHEOTIEN=0`; sửa ô tiền → `GIAMTHEOTIEN=30` |
| Thuế: % và tiền | `TILETHUE`, `TIENTHUE` | |
| Phí vận chuyển | `PHIVANCHUYEN` | |
| Tổng cộng | `TONGCONG` | tính |
| *(không hiện)* | `LOAI` | cột có trong bảng, code không dùng; v1 ghi `0`/NULL (Q6) |

Phần dưới: ô **Tìm (F3)**, lưới mặt hàng (cột: Mã hàng, Tên hàng, ĐVT, Giá bán lẻ, Giá bán buôn, Bảo hành), ô **Số lượng**, nút **Thêm** (Enter trong ô SL = Thêm); lưới chi tiết gồm Mặt hàng (mã, tên), ĐVT, SL, Đơn giá, % giảm, Thành tiền, Ghi chú; có nút Xóa dòng, **Nhập Excel**.

## 2. Luật và luồng (khác khuôn)

### 2.1 Thêm mặt hàng vào chi tiết [CODE `grMatHang_OnAddingRowToGrid`, `OnAddSameItem`]

- SL = 0 → **"Mời bạn nhập số lượng"**, chọn lại ô SL (không áp khi bật 2 đơn vị tính, vì SL lẻ có thể 0 còn SL chẵn > 0).
- Dòng mới lấy: `DMATHANGID`, `DDONVITINHID` (đơn vị **lẻ** = `DMATHANG.DDONVITINHID`), `DONGIA = giá bán`, `SOLUONG`. Tên/mã/ĐVT chỉ để hiển thị.
- **Gộp dòng** khi cùng mặt hàng **và** cùng đơn vị tính → cộng SL. Quyết định gộp **luôn** (code `OnAddSameItem` không đọc `NhapMotMatHangNhieuLanTrongPhieu` cho SL lẻ); riêng nhánh thêm SL chẵn (2 đơn vị) có đọc cờ này (≠30 → gộp; hiện cờ = 0 → gộp). v1 gộp theo cờ này cho cả hai nhánh (khác gốc ở dòng SL lẻ khi cờ = 30; đề xuất P2).
- Giá lấy từ `selRow["GIABAN"]` (đơn giá lẻ) và `GIABANCHAN` (đơn giá chẵn) — lưới mặt hàng hiển thị **"Giá bán lẻ"/"Giá bán buôn"**; ánh xạ chính xác `GIABAN ↔ GIABANLE` chưa xác minh (Q5).
- **2 đơn vị tính** (`SuDung2DonViTinh = 30` và mặt hàng có 2 ĐVT): bấm Thêm mở hộp `NhapSoLuong` (SL lẻ + SL chẵn, ô chiết khấu bị **khóa**); SL chẵn ghi dòng thứ hai với `DDONVITINHCHANID`, đơn giá `GIABANCHAN`, `TILEGIAMGIA = 0`; `QUYDOI` lấy từ mặt hàng. **[DB] `SuDung2DonViTinh = 30` (đang BẬT)** → v1 phải làm nhánh 2 đơn vị tính. Cấu hình hiện hành khác: `SuDungMaHang = 0` (Excel dùng cột **Tên hàng**, khớp theo tên), `NhapMotMatHangNhieuLanTrongPhieu = 0` (**gộp** dòng), `CoThueSuat = 0`, `KichHoatLuuVetHoatDong = 30`.

### 2.2 Tính tổng [CODE `mapper_OnCalculation`]

```
TIENHANG = Σ THANHTIEN dòng
giảm theo tiền (GIAMTHEOTIEN=30): TILEGIAMGIA = TIENHANG = 0 ? 0 : 100 × TIENGIAMGIA / TIENHANG
giảm theo %   (GIAMTHEOTIEN≠30): TIENGIAMGIA = 100 × Round(TIENHANG × TILEGIAMGIA / 10000)
TIENTHUE = TILETHUE × (TIENHANG − TIENGIAMGIA) / 100
TONGCONG = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN
```
`THANHTIEN` dòng tính bởi lưới (`detail.CalculateRow`): `SOLUONG × DONGIA × (1 − TILEGIAMGIA/100)` — **công thức dòng chưa đọc được trực tiếp** (nằm trong thư viện), suy từ tài liệu nghiệp vụ → [CHƯA XÁC MINH] (Q9).

**Điểm lệch với quyết định chốt:** gốc làm tròn `TIENGIAMGIA` theo **bội 100** (`100 × Round(...)` = làm tròn tới trăm), **không** áp `LamTron` 1000; `TIENTHUE` và `TONGCONG` **không** làm tròn. Quyết định chốt của dự án là `LamTron` 1000 AwayFromZero → **P1**.

### 2.3 Nhập từ Excel [CODE `OnImpExcel*`]

Cột Excel: **Mã hàng** (nếu `SuDungMaHang = 30`) hoặc **Tên hàng** (ngược lại); **Số lượng** (bắt buộc); **ĐVT** (chỉ khi `SuDung2DonViTinh = 30`); **Đơn giá**; **% giảm**.
- Bắt buộc chọn cột: "Mời bạn chọn cột 'Mã hàng'" / "'Tên hàng'" / "'Số lượng'".
- Bỏ qua dòng không có mã hoặc SL = 0; mặt hàng lấy từ `DMATHANG` (`STATUS = 30`), khớp theo **mã hoặc tên, không phân biệt hoa thường**.
- Lỗi gom lại rồi hiện một lần, hủy toàn bộ nhập: "Mã hàng 'x' không tồn tại trong hệ thống"; "Đơn vị tính 'y' ở mặt hàng 'z' không tồn tại trong hệ thống"; "Mặt hàng 'z' không có đơn vị tính 'y'".
- Đơn giá nhập từ Excel **ghi đè** giá bán; không nhập giá thì **0** (không tự lấy giá bán) — (code: `row["DONGIA"]` mặc định 0). Q10.

### 2.4 Lưu / xóa / quyền

- Không có trạng thái "đã duyệt/đã chuyển thành đơn"; sửa/xóa tự do (có quyền).
- **Xóa cứng** (không thùng rác, như quyết định chốt): xóa `TBAOGIA` + `TBAOGIACHITIET`. Không có ràng buộc ngược (không bảng nào trỏ về `TBAOGIA` trong code đọc được).
- Không có "chuyển báo giá thành hóa đơn/đặt hàng" trong code → **không làm** (Q3 nếu Chiến muốn).
- Lưu vết `TLUUVET` khi `KichHoatLuuVetHoatDong = 30`: như khuôn chứng từ (thêm/sửa/xóa phiếu). Chi tiết thông điệp theo khuôn Khách đặt hàng.

## 3. Dữ liệu [DB]

`TBAOGIA`: `ID`, `STATUS`, `NAME` (số phiếu), `NOTE`, `NGAY`, `TENKHACH`, `DIACHI`, `DIENTHOAI`, `EMAIL`, `DKHACHHANGID`, `TIENHANG`, `TILETHUE`, `TIENTHUE`, `TILEGIAMGIA`, `TIENGIAMGIA`, `PHIVANCHUYEN`, `TONGCONG`, `LOAI`, `GIAMTHEOTIEN`, cột chuẩn (`USERCREATEDID`, `TIMECREATED`, `USERMODIFIEDID`, `TIMEMODIFIED`). Tiền: `DECIMAL(18,2)` (đã khớp quyết định chốt).
`TBAOGIACHITIET`: `ID`, `STATUS`, `TBAOGIAID`, `DMATHANGID`, `DDONVITINHID`, `SOLUONG`, `DONGIA`, `THANHTIEN`, `TILEGIAMGIA`, `NOTE` + cột chuẩn. **Không có `TIENGIAMGIA`, `QUYDOI` ở chi tiết** → dòng chẵn lưu bằng `DDONVITINHID` ≠ đơn vị lẻ, quy đổi suy từ mặt hàng khi cần.
Không đổi cấu trúc DB.

## 4. Phân quyền

Một quyền: "Báo giá" (`SFUNCTION` `7be6722c-…`, nhóm "Bán hàng", kiểu View). Mặc định tạm: admin toàn quyền; tài khoản khác **không cấp** (Q5 spec Sử dụng dịch vụ). Không có quyền riêng cho In/Xóa.

## 5. Câu hỏi chờ Chiến quyết

### 5.0 Chiến chốt 25/11/2026 (hỏi trong phiên). Mục này thắng §2, §5.1, §5.2 nếu khác

- **Q1/P1:** `LamTron` 1000 AwayFromZero cho `TIENGIAMGIA`, `TIENTHUE`, `TONGCONG`, tiền `decimal`. Ca số: tiền hàng 235.000, giảm 7%, thuế 8%, phí 0 → giảm 16.000, thuế 18.000, tổng 237.000.
- **P2:** giữ như app gốc (`code.cs:75-83`, `:181-198`): dòng đơn vị lẻ luôn gộp; dòng đơn vị chẵn gộp khi `NhapMotMatHangNhieuLanTrongPhieu` khác 30. Thay đề xuất ở §2.1 và §5.1.
- **P3:** trước mắt chặn số lượng <= 0. Sau này bổ sung cấu hình toàn hệ thống cho phép số âm (chưa thuộc spec này).
- **Q4:** nút In báo "chưa hỗ trợ".
- Còn phải xác minh trước khi duyệt: Q2, Q5, Q7 (phần hiện ô thuế), Q9.

### 5.0.2 Số lượng lẻ khi mặt hàng 2 đơn vị tính (Chiến chốt 25/11/2026, issue #16). Thắng §2.1 nếu khác

Chiến trả lời trong phiên Claude Code: **"A, như app gốc"**. "Ô SL" là ô số lượng cạnh nút Thêm; "số trong hộp" là SL lẻ ở hộp "Nhập số lượng". Đã bấm thử trên app gốc 25/11 (bản chép), lẻ 15.000, thùng 340.000:

| Phiếu đang có dòng lẻ? | Ô SL | Lẻ trong hộp | Kết quả dòng lẻ |
|---|---|---|---|
| chưa | 2 | 5 | tạo dòng **2** |
| rồi | 0 | 3 | **không đổi** |
| rồi | 2 | **0** | **cộng thêm 2** (2 thành 4) |
| chưa | 0 | 3 | gốc tạo dòng số lượng 0; bản mới **không tạo** (P3) |

Luật cho bản mới:
- Đã có dòng lẻ cùng mặt hàng: cộng **ô SL** vào dòng đó, không xét số lẻ trong hộp.
- Chưa có dòng lẻ: lẻ trong hộp = 0 thì không tạo dòng; khác 0 thì tạo dòng với số lượng = **ô SL** (ca này theo mã gốc `code.cs:21`, `:38`).
- P3: ô SL <= 0 thì không tạo và không cộng dòng lẻ. Khi lẻ trong hộp > 0 mà ô SL <= 0, báo "Mời bạn nhập số lượng" (câu của gốc; gốc không báo ở ca này).
- Dòng chẵn lấy số **chẵn trong hộp** như §2.1, kể cả khi dòng lẻ không được tạo. Lẻ và chẵn trong hộp đều 0 thì không thêm gì, báo "Mời bạn nhập số lượng". Hộp bấm Thoát thì không thêm gì.
- Khách đặt hàng bản mới giữ nguyên (đang lấy số trong hộp); chưa có quyết định đổi.

Menu: app gốc đang ẩn "Báo giá bán hàng" (`SMENU.STATUS = 0`), bản mới chỉ hiện dòng `STATUS = 30`. Vẫn làm màn và route theo spec; **không** sửa CSDL và không ép hiện menu khi chưa có quyết định.

### 5.0.1 Xác minh 25/11/2026

- **Q9, đã xác minh:** `ViewConfig` của lưới chi tiết `detail` (`layout.xml`, giải base64) lưu công thức `SOLUONG*DONGIA*(1-TILEGIAMGIA/100)`. Thay ghi chú [CHƯA XÁC MINH] ở §2.2.
- **Q7, đã xác minh:** `SCOLUMN.ENABLECONFIGID` của `TILETHUE`, `TIENTHUE` = `CoThueSuat` → cấu hình đang 0 thì ô bị khóa.
- **Q5, một phần:** `DMATHANG` không có cột `GIABANLE`, `GIABANBUON` (chỉ `GIABAN`, `GIABAN2`–`4`, `GIABANCHAN`). Giá ghi vào phiếu: lẻ = `GIABAN`, chẵn = `GIABANCHAN` (`code.cs`). Hai cột lưới "Giá bán lẻ/buôn" của gốc gắn với tên không có trong bảng; gốc hiển thị gì chưa xác minh. Bản mới hiển thị `GIABAN`, `GIABANCHAN` (chỉ là hiển thị).
- **Q3, Q6, Q10, giống app gốc:** `code.cs` của form không có chức năng chuyển báo giá, không đọc hay gán `LOAI`; đơn giá Excel trống thành 0 (`detail_OnImpExcelRowAdding`). Mới rà form này.
- **Q2, chưa xác minh:** `STABLEDESC` có `GENNO = 30`, mẫu `BG(yy)/(*****)`; chưa tìm thấy mã quay vòng theo năm.

### 5.1 Chỗ đề xuất khác app gốc (CHỈ đề xuất)

- **P1** Làm tròn: `TIENGIAMGIA`, `TIENTHUE`, `TONGCONG` dùng `LamTron` 1000 AwayFromZero, tiền `decimal` (gốc: bội 100 cho giảm giá, thuế/tổng không làm tròn). *Mặc định tạm: áp P1, nhưng đây là phiếu báo giá, không phải tiền thật ⇒ có thể giữ nguyên gốc.*
- **P2** Gộp dòng: gộp theo `NhapMotMatHangNhieuLanTrongPhieu` cho cả SL lẻ và chẵn.
- **P3** Ghi `SOLUONG` ≤ 0 bị chặn (gốc chỉ chặn = 0 ở nhánh một ĐVT).

### 5.2 Câu hỏi còn mở (kèm mặc định tạm)

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | Áp P1 hay giữ làm tròn gốc? | Áp `LamTron` 1000 |
| Q2 | Số phiếu `BG26/00001`: quay vòng theo năm đúng không (chưa có dữ liệu)? | Có |
| Q3 | Có cần "chuyển báo giá thành hóa đơn / đặt hàng" không (gốc không có)? | Không |
| Q4 | Mẫu in Báo giá: tạo mới (FastReport) hay "chưa hỗ trợ"? | "Chưa hỗ trợ" |
| Q5 | Ánh xạ `GIABAN ↔ GIABANLE`, `GIABANCHAN ↔ GIABANBUON` ở lưới chọn mặt hàng | Lẻ→`GIABAN`, buôn→`GIABANCHAN` |
| Q6 | Cột `LOAI` dùng cho gì? | Không dùng, ghi 0 |
| Q7 | ~~Giá trị cấu hình hiện hành~~ **Đã đo [DB]**: `SuDung2DonViTinh=30`, `SuDungMaHang=0`, `NhapMotMatHangNhieuLanTrongPhieu=0`, `CoThueSuat=0`. Thuế có hiện trên dialog khi `CoThueSuat=0` không (chưa rõ) | Ô thuế vẫn hiện, mặc định 0 |
| Q8 | Chụp bổ sung app gốc (danh sách + dialog) khi máy cho phép? | Có, sau |
| Q9 | Công thức `THANHTIEN` dòng (trong DLL) | `SL × ĐG × (1 − %/100)` |
| Q10 | Đơn giá Excel trống: 0 hay lấy giá bán? | 0 như gốc |

## 6. Quyết định đã chốt (áp, không hỏi lại)

Xóa cứng; tiền `NUMERIC(18,2)`/`decimal`; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; không đọc `LOAIDINHLUONG`; không thêm/sửa file DB gốc; lưu vết theo cờ hệ thống như các spec trước.

## 7. Tiêu chí nghiệm thu (kiểm trên bản sao DB; seed dữ liệu trong selftest)

- [ ] Menu "Báo giá bán hàng" mở màn danh sách; lọc Ngày (mặc định 30 ngày), Số phiếu, Khách hàng.
- [ ] Thêm mới tự sinh số `BGyy/#####`, không trùng; sửa/xóa phiếu đúng quyền "Báo giá".
- [ ] Chọn khách tự điền & khóa 4 ô thông tin; bỏ chọn mở khóa và xóa trống; nhập tay được khi không chọn.
- [ ] Thêm mặt hàng: SL = 0 báo "Mời bạn nhập số lượng"; cùng mặt hàng + ĐVT gộp SL; tên/mã chỉ để hiển thị.
- [ ] `QuoteCalculator` (thuần `decimal`, selftest ≥ 12 ca): giảm theo %/theo tiền, chuyển qua lại cờ `GIAMTHEOTIEN`, thuế, phí vận chuyển, TIENHANG = 0, biên làm tròn; khớp §2.2.
- [ ] Nhập Excel: đủ 3 thông báo lỗi gốc, bỏ dòng thiếu SL, mã/tên không phân biệt hoa thường.
- [ ] Xóa phiếu xóa cả chi tiết; không dư bản ghi `TBAOGIACHITIET` mồ côi.
- [ ] Nút In/Excel hiện, báo "chưa hỗ trợ" nếu chưa có mẫu; không SQL trong View; màu/cỡ chữ từ token; 125% DPI; nút đủ lớn cảm ứng (yêu cầu v1, không phải hành vi gốc).
- [ ] (§5.0) Tiền hàng 235.000, giảm 7%, thuế 8% (bật `CoThueSuat` trên bản chép), phí 0 → giảm 16.000, thuế 18.000, tổng 237.000. Thay yêu cầu "khớp §2.2" ở phần làm tròn.
- [ ] (§5.0) Số lượng <= 0 bị chặn ở mọi đường nhập, gồm nhập Excel và sửa trên lưới.
- [ ] (§5.0.1) `CoThueSuat = 0` thì hai ô thuế bị khóa.

## 8. Gợi ý implement (không bắt buộc)

Tái dùng dialog chứng từ + `DocumentNumberService` của Khách đặt hàng; thêm `QuoteCalculator` (Services, thuần) + `QuoteRepository` (Data: `TBAOGIA`/`TBAOGIACHITIET`, một transaction cho lưu phiếu + chi tiết); `QuoteListView`/`QuoteEditDialog` (UI). Thứ tự: (1) `QuoteCalculator` + selftest; (2) repository + số phiếu; (3) danh sách + lọc; (4) dialog thêm/sửa/xóa; (5) nhập Excel.
