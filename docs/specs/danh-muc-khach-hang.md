# Spec: Danh mục khách hàng (G1)

- Trạng thái: **ĐÃ DUYỆT** (Chiến duyệt 25/11/2026 trong phiên Claude Code; chưa code)
- **Đọc trước:** §9.0 và §9.0.1 **thắng mọi chỗ khác** trong spec. Câu cũ trong thân spec (nhất là "nằm trong DLL") đã lỗi thời: mã dùng chung ở `SCONFIG` bản ghi `SharedCode`.
- **Điểm còn chưa xác minh:** khi làm tới thì tự xác minh từ mã nguồn hoặc CSDL. Không chứng minh được: phần chỉ hiển thị thì theo mặc định tạm của spec; phần **tiền, giá, kho, công nợ, quyền, xóa dữ liệu** thì không đoán, mở issue `cho-chien` và xử lý theo `workflow-agy.md` §10.2.1.
- Ngày: 2026-11-19
- Base: `origin/main` @ `b0d15db` (đã `git fetch origin main`; nhánh `main` local đang ở `7ced0a8`, đi trước origin 1 commit)
- Spec tổng: `docs/specs/kazuko-tea-clone.md` (mục 4.2 Khách hàng, mục 6 tiêu chí G1)
- **Cập nhật theo DB thật (2026-11-19):** bản nháp này viết theo schema TEA; đã đo lại trên `POS TAP\Database\DATA.fdb` (số đo ở `docs/reference/db-pos-tap-do-truc-tiep.md`) và sửa: `DKHACHHANG` thật có **21 cột / 15 trường trên dialog** (không có `SOCMND`, `NGAYCAP`, `NOICAP`; có `DTHETRATRUOCID`), dialog thật **466×383**, `STABLEDESC.FORMAT` rỗng. Chỗ đã sửa đánh dấu **[Sửa theo DB thật]**. Spec vẫn ở trạng thái NHÁP chờ Chiến duyệt.
- Nguồn đã đọc: `STABLEDESC/DKHACHHANG`, `STABLEDESC/DNHOMKHACHHANG`, `SFORM/Khách hàng thân thiết`, `SFORM/Hóa đơn bán hàng` + `Hóa đơn nhà hàng` (phần gắn khách), `SFORM/Nhập dữ liệu`; metadata `SMENU`, `STABLEDESC`, `SCOLUMN`, `SGRID`, `SCONFIG`, `SGROUPROLE` và cấu trúc bảng đọc từ **bản sao** `DATA.FDB` (chỉ đọc, không sửa file nào).

## 1. Bối cảnh

Menu **Khách hàng > Danh mục khách hàng** (Ctrl+K, `SMENU.SHORTCUT = 131147` = Ctrl + phím K) mở màn quản lý danh mục khách hàng. Trong app gốc đây **không có form riêng**: nó là màn "bảng nhập liệu" chung của framework `No1Lib`, dựng từ metadata của bảng `DKHACHHANG` (`STABLEDESC`, `TABLETYPE = 2` = cây nhóm bên trái + lưới bên phải, nhóm theo cột `DNHOMKHACHHANGID`). Hành vi thêm/sửa/xóa/tìm/in nằm trong framework (`No1Lib.*`), **không đọc được từ code xuất** → phần đó phải đối chiếu bằng ảnh app gốc và câu trả lời của Chiến (mục 8, 9).

Đây là chức năng mẫu: các danh mục `D*` khác sẽ theo cùng khung (lưới, tìm kiếm, thêm/sửa/xóa, kiểm tra dữ liệu).

## 2. Mục tiêu

1. Quản lý (xem, thêm, sửa, xóa) khách hàng trên bản sao `DATA.FDB`, **cùng trường, ràng buộc, thông báo lỗi** như app gốc.
2. Dữ liệu ghi ra tương thích 100% với app gốc (cùng bảng/cột, cùng giá trị mặc định, cùng cột lưu vết).
3. Dựng khung danh mục tái dùng được (lưới + tìm kiếm + dialog thêm/sửa + kiểm tra) cho các danh mục sau.

## 3. Phạm vi

**Làm**
- Màn "Danh mục khách hàng": cây nhóm khách bên trái (có mục "Tất cả"), lưới khách bên phải, ô tìm kiếm (F3 nhảy vào ô tìm), thanh nút Thêm / Sửa / Xóa / Làm mới.
- Dialog thêm/sửa khách hàng (đủ **15 trường** của mục 4.1 — **[Sửa theo DB thật]** trước ghi 18 —, có chọn Nhóm, Nhân viên, Tỉnh thành, Thẻ trả trước).
- Quản lý **Nhóm khách hàng** (`DNHOMKHACHHANG`) ngay trên cây nhóm: thêm/sửa/xóa nhóm với các trường ở mục 4.2. *(Giả định — cần Chiến xác nhận ở câu hỏi A1: menu không có mục "Nhóm khách hàng" riêng.)*
- Quyền theo `SGROUPROLE` cho chức năng "Danh mục khách hàng": Xem / Thêm / Sửa / Xóa.
- Cung cấp lớp truy vấn (`Data`) cho khách hàng + nhóm, để các màn sau (hóa đơn, công nợ, thân thiết) dùng lại.

**KHÔNG làm (để spec/giai đoạn khác)**
- **Khách hàng thân thiết** (menu riêng, `SFORM/Khách hàng thân thiết`, bảng `TTANGGIAMDIEM`) — spec riêng.
- Gửi tin nhắn tới khách hàng (menu "Gửi tin nhắn tới khách hàng", `OPERATION = 7` trên cùng bảng).
- Nhập khách hàng từ hệ thống cũ (`Nhập dữ liệu > ImportKhachHang`) — thuộc nhóm Quản trị.
- Chọn khách khi bán hàng, giảm giá theo nhóm khi tính hóa đơn, tự nâng nhóm theo điểm — thuộc G2 (nhưng spec này ghi lại quy tắc để G2 dùng, mục 4.4).
- Công nợ khách hàng, thu tạm ứng khách hàng, báo cáo sinh nhật khách — G3/G4.
- Đổi cấu trúc DB, thêm cột, thêm ràng buộc FK.

## 4. Nghiệp vụ chi tiết

### 4.1 Khách hàng — bảng `DKHACHHANG`

Kiểu cột đọc từ DB (VARCHAR đo theo ký tự, charset UTF8). Nhãn và thứ tự theo `SCOLUMN`/layout app gốc.

| Cột | Nhãn trên UI | Kiểu / độ dài | Bắt buộc | Duy nhất | Ghi chú |
| --- | --- | --- | --- | --- | --- |
| `ID` | (ẩn) | VARCHAR(36), khóa chính | có | có | GUID chuỗi, sinh khi thêm |
| `MAKHACH` | Mã khách | VARCHAR(255) | **có** (`SCOLUMN.ALLOWEMPTY='NO'`) | **có** (`ALLOWDUPLICATE='NO'`) | Giá trị mặc định khi thêm: mẫu `{KH(*****)}` = tự sinh mã bắt đầu "KH" + dãy số 5 chữ số (xem Q3) |
| `NAME` | Tên khách hàng | VARCHAR(255) | **có** | theo cấu hình: `ChoPhepTrungTenKhachHang` (hiện = 0 → **không cho trùng tên**) | `SCOLUMN.ALLOWDUPLICATE` của cột này trỏ tới cấu hình đó |
| `DNHOMKHACHHANGID` | Nhóm khách hàng | VARCHAR(36) → `DNHOMKHACHHANG.ID` | không | — | Chọn từ danh sách nhóm; khi thêm từ một nhóm đang chọn trên cây thì gán sẵn nhóm đó (Q-xác nhận ở ảnh) |
| `DIACHI` | Địa chỉ | VARCHAR(255) | không | không | |
| `DIENTHOAI` | Điện thoại | VARCHAR(255) | không | không | |
| `EMAIL` | Email | VARCHAR(255) | không | không | |
| `MASOTHUE` | Mã số thuế | VARCHAR(255) | không | không | |
| `DNHANVIENID` | Nhân viên | VARCHAR(36) → `DNHANVIEN.ID` | không | — | Nhân viên phụ trách (NVKD) |
| `NGAYSINH` | Ngày thành lập/sinh nhật | TIMESTAMP | không | — | |
| `DIEMTICHLUYBANDAU` | Điểm tích lũy ban đầu | INTEGER, mặc định 0 | không | — | UI gốc cho nhập 2 số thập phân, khoảng ±10¹² nhưng cột là INTEGER (Q5) |
| `GIABAN` | Giá bán | INTEGER, mặc định 0 | không | — | Combo 5 mục: 0 "Giá theo danh mục", 1 "Giá bán 2", 2 "Giá bán 3", 3 "Giá bán 4", 4 "Giá gần nhất". Chỉ bật khi cấu hình tương ứng — **[Sửa theo DB thật]** `SCOLUMN.ENABLECONFIGID` = `SuDungGiaTheoKhach` (xác nhận; hiện = 0, nên ô bị khóa). Config này vừa điều khiển ô trên dialog vừa điều khiển việc hóa đơn lấy `GIABAN` làm loại giá (Q6 đã trả lời). Không hiện trên lưới |
| `DTINHTHANHID` | Tỉnh thành | VARCHAR(36) → `DTINHTHANH.ID` | không | — | Bảng `DTINHTHANH` trong bản sao đang trống |
| `FACEBOOK` | Facebook | VARCHAR(255) | không | không | |
| `DTHETRATRUOCID` | Thẻ trả trước | VARCHAR(36) → `DTHETRATRUOC.ID` | không | — | **[Sửa theo DB thật]** cột có ở POS TAP (không có ở TEA). Ô chỉ bật khi cấu hình `SuDungTheTraTruoc` = 30 (`SCOLUMN.ENABLECONFIGID`; hiện = 0). Khách gắn sẵn thẻ thì thanh toán tự dùng thẻ đó nếu còn tiền |
| ~~`SOCMND`~~ / ~~`NGAYCAP`~~ / ~~`NOICAP`~~ | — | — | — | — | **[Sửa theo DB thật]** **không tồn tại ở POS TAP** (chỉ có ở TEA). Hóa đơn nhà hàng điền thông tin khách (tên đơn vị, người mua, MST, địa chỉ, điện thoại…) vào cột của `TDONHANG`, không dùng CMND |
| `NOTE` | Ghi chú | VARCHAR(255) | không | không | |
| `STATUS` | (ẩn) | INTEGER NOT NULL | có | — | Trạng thái dòng: **30 = đang dùng** (theo mọi dòng `SMENU/SFUNCTION` trong DB). Cách "xóa" xem Q2 |
| `USERCREATEDID`, `TIMECREATED` | (ẩn) | VARCHAR(36) NOT NULL / TIMESTAMP | có / — | — | Người tạo (user đăng nhập), thời điểm tạo |
| `USERMODIFIEDID`, `TIMEMODIFIED` | (ẩn) | VARCHAR(36) / TIMESTAMP | — | — | Cập nhật mỗi lần sửa |

Ràng buộc ở mức DB: chỉ có khóa chính `ID` và NOT NULL (`ID`, `NAME`, `STATUS`, `USERCREATEDID`); **không có FK, không trigger, không index duy nhất** → mọi kiểm tra trùng/liên kết phải do chương trình làm (giống app gốc).

Kiểm tra khi lưu (suy từ metadata; **thông báo lỗi nguyên văn chưa biết**, chờ ảnh):
1. `NAME` trống → lỗi bắt buộc.
2. `MAKHACH` trống → lỗi bắt buộc.
3. `MAKHACH` đã có ở khách khác → lỗi trùng.
4. `NAME` đã có ở khách khác và `ChoPhepTrungTenKhachHang` ≠ 30 → lỗi trùng.
5. Độ dài vượt 255 ký tự → chặn nhập hoặc báo lỗi (Q4).

Lưới (theo `SGRID` của `DKHACHHANG` — thứ tự, độ rộng px, hiện/ẩn của app gốc):

| Thứ tự | Cột | Rộng | Hiện |
| --- | --- | --- | --- |
| 1 | Tên khách hàng | 97 | có |
| 2 | Nhóm khách hàng | 130 | có |
| 3 | Mã khách | 68 | có |
| 4 | Địa chỉ | 186 | có |
| 5 | Điện thoại | 139 | có |
| 6 | Email | 139 | có |
| 7 | Mã số thuế | 139 | có |
| 8 | Nhân viên | 78 | có |
| 9 | Điểm tích lũy ban đầu | 105 | có |
| 10 | Ngày thành lập/sinh nhật | 80 | có |
| 11 | Ghi chú | 150 | **ẩn** (người dùng bật được) |

Bản viết lại dùng thứ tự cột này làm mặc định, độ rộng co giãn theo DPI/cửa sổ (không phải giữ đúng px).

Tìm kiếm: ô "Tìm" lọc lưới trực tiếp trên các cột đang hiện (hành vi chi tiết — theo ảnh/Q7). F3 đưa con trỏ vào ô tìm. Chọn một nhóm trên cây thì lọc lưới theo `DNHOMKHACHHANGID` = nhóm đó; chọn "Tất cả" thì không lọc. *(Quy tắc lọc nhóm lấy từ `Khách hàng thân thiết`, dùng cùng khung; khi chọn nhóm cha có lấy luôn khách nhóm con không — Q8.)*

Danh sách chọn khách (dropdown ở hóa đơn, phiếu thu/chi…) hiển thị các cột có `SHOWONLOOKUP = 30`: **Mã khách, Tên khách hàng, Địa chỉ, Điện thoại**. Spec này chỉ cung cấp truy vấn dùng chung.

### 4.2 Nhóm khách hàng — bảng `DNHOMKHACHHANG`

Kiểu cây (`PARENTID`, `PARENTDIR`, `ITEMTYPE`, `SORTORDER`, `AUTOID`) như các danh mục dạng cây khác.

| Cột | Nhãn | Kiểu | Bắt buộc / duy nhất |
| --- | --- | --- | --- |
| `NAME` | Tên nhóm khách hàng | VARCHAR(255) | **bắt buộc, không trùng** |
| `SIMAGEID` | Ảnh | VARCHAR(36) → `SIMAGE.ID` (thư viện ảnh) | không |
| `DIEMTICHLUY` | Điểm tích lũy | NUMERIC(18,2) | không (ngưỡng điểm để tự nâng nhóm; 0 = không dùng) |
| `TILEGIAMGIA` | Tỉ lệ giảm giá (giảm tổng, %) | NUMERIC(18,2) | không |
| `TILEGIAMGIATIENHANG` | Tỉ lệ giảm giá tiền hàng (%) | NUMERIC(18,2) | không |
| `TILEGIAMGIATIENGIO` | Tỉ lệ giảm giá tiền giờ (%) | NUMERIC(18,2) | không |
| `TILEGIAMDOAN` / `TILEGIAMDOUONG` / `TILEGIAMDICHVU` / `TILEGIAMDOKHAC` | Tỉ lệ giảm đồ ăn / đồ uống / dịch vụ / đồ khác (%) | NUMERIC(18,2) | không |
| `NOTE` | Ghi chú | VARCHAR(255) | không |

Hiển thị ô tỉ lệ giảm trên dialog nhóm (từ `DNHOMKHACHHANG/code.cs`, `mapper_OnLoad`), phụ thuộc cấu hình:
- Cửa hàng **không có tiền giờ** (`Shared.CoTienGio` = sai) → ẩn ô "Tỉ lệ giảm giá tiền giờ".
- Cấu hình `GopChungGiamGiaTienGioVaTienHangLamMot` = 30 **và** có tiền giờ → ẩn "tiền giờ" **và** "tiền hàng", chỉ còn ô "Tỉ lệ giảm giá" (giảm tổng). Hiện cấu hình này = 0.
- **[Sửa theo DB thật]** Ô "Tỉ lệ giảm giá" (giảm tổng, `TILEGIAMGIA`) **mặc định ẩn** (`Visible=False` trong layout) và code `mapper_OnLoad` (đã đọc trên `POS_TAP_SRC_EXPORT/STABLEDESC/DNHOMKHACHHANG`) chỉ bật nó khi `Shared.CoTienGio` **và** `GopChung = 30`. POS TAP có `CoTienGio = 0` → ô này **không bao giờ hiện**, nhưng `GetTiLeGiamTheoKhach` vẫn đọc cột khi bán → giá trị kẹt ở DB (mặc định 0), không sửa được qua UI. Bản mới: **Chiến chốt** luôn hiện ô này hay bỏ cột (câu hỏi mới A2).
- Mọi ô có đơn vị "%". Khoảng nhập của các ô giảm: ±10¹⁶, 2 số thập phân (app gốc không chặn >100% — Q9).

### 4.3 Quyền (`SGROUPROLE`)

Chức năng: `SFUNCTION` "Danh mục khách hàng" (`36c3bad1-9d2e-4916-98e0-efb6c0e681e2`, gắn với `STABLEDESC.SFUNCTIONID` của `DKHACHHANG`). Cờ `MODE`: Xem = 0x10, Sửa = 0x20, Thêm = 0x40, Xóa = 0x80; 0 = khóa (menu ẩn — đã làm ở khung chính). Admin có mọi quyền.
Dữ liệu hiện có: *Quản lý* và *Kế toán* = 240 (0xF0, đủ bốn quyền); *Thu ngân*, *Phục vụ* = 0 (khóa).
Quy tắc: không có quyền Thêm → ẩn/vô hiệu nút Thêm; Sửa/Xóa tương tự; chỉ có Xem → mở được màn nhưng không sửa được. (Cách app gốc thể hiện — ẩn hay mờ nút — chờ ảnh, Q10.)

### 4.4 Quy tắc liên quan, để các giai đoạn sau dùng (không làm ở spec này)

- Hóa đơn chọn khách → lấy nhóm của khách → giảm giá theo nhóm: giảm đồ ăn/uống/khác/dịch vụ theo từng loại; nếu `GopChungGiamGiaTienGioVaTienHangLamMot` = 30 thì giảm tổng = max(KM tự động, `TILEGIAMGIA` của nhóm), ngược lại giảm tiền hàng và tiền giờ mỗi cái = max(KM tự động, tỉ lệ nhóm).
- Hóa đơn tự điền `SOCMND`, tên, địa chỉ, điện thoại từ khách (ô nhập khóa lại khi đã chọn khách).
- Điểm tích lũy = `DIEMTICHLUYBANDAU` + điểm từ hóa đơn (`CachTinhDiem` = 1: cộng cột `DIEM` từng hóa đơn đã thanh toán; =0: tổng tiền ÷ `DoanhSoTuongUngVoi1Diem`, hiện 20 000) + (`DIEMTANG` − `DIEMGIAM`) trong `TTANGGIAMDIEM`. Khi `TuDongNangCapThanhVienKhiDatHanMuc` = 30 (hiện = 30), sau mỗi hóa đơn nếu điểm đạt ngưỡng `DNHOMKHACHHANG.DIEMTICHLUY` thì chuyển khách sang nhóm có ngưỡng cao nhất ≤ điểm. Nghĩa là `DIEMTICHLUYBANDAU` và ngưỡng điểm của nhóm ảnh hưởng tới tiền giảm giá — là dữ liệu nghiệp vụ, không chỉ là nhãn.
- **[Bổ sung theo vòng 2 + DB thật]** Giá trị hiện hành trên DB thật: `CachTinhDiem = 1`, `QuyDoi1DiemSangTien = 1000`, `DoanhSoTuongUngVoi1Diem = 20000`, `TuDongNangCapThanhVienKhiDatHanMuc = 30`, `HienThiDiemCuaKhachHangTrenHoaDon = 0`. App gốc có **ba công thức điểm** + một công thức xét nâng nhóm (không trừ điểm đã tiêu) cho kết quả khác nhau; chi tiết và đề xuất một công thức chuẩn ở `docs/pos-tap-nghiep-vu-khach-hang-the.md` mục 6.1. Spec G2 phải chốt trước khi code.
- Bảng tham chiếu `DKHACHHANGID`: `TDONHANG`, `TDATHANG`, `TBAOGIA`, `TTHUCHI`, `TTANGGIAMDIEM`, `TLUUTAM`, `DVOUCHER`. `DATA.FDB` thật hiện **0 khách, 0 nhóm, 0 tỉnh thành, 0 nhân viên** (**[Sửa theo DB thật]** trước ghi 1 nhân viên; `DEMO.FDB` có 3 khách, 13 nhân viên, 0 nhóm khách).

## 5. Bố cục UI

Theo CLAUDE.md: dùng design token + `ThemeManager`, không hard-code màu/cỡ chữ; high-DPI; hạn chế cuộn dọc; cảm ứng; không SQL trong View/ViewModel.

- **Màn danh mục** (mở thành tab tài liệu trong khung chính, thay tab "sẽ được bổ sung"): trên cùng thanh tiêu đề + ô tìm (có F3) + nút Thêm/Sửa/Xóa/Làm mới; trái: cây nhóm (có "Tất cả", đếm số khách mỗi nhóm là tùy chọn, không có trong app gốc → **không làm**); phải: lưới. Nhấp đôi một dòng = Sửa. Phím: Ctrl+K mở màn; Insert/Enter/Delete theo app gốc (chốt theo ảnh).
- **Dialog Thêm/Sửa khách hàng**: **[Sửa theo DB thật]** app gốc **466×383**, một cột, **15 trường** (`layout.xml` thật; trước ghi 466×410 và 18 trường). Dialog gốc **không có group box**. Bản mới gọn hơn: **hai cột**, nhóm theo ý nghĩa (nhóm chỉ là cách xếp của bản mới) — *Định danh* (Mã khách, Tên, Nhóm, Nhân viên phụ trách), *Liên hệ* (Điện thoại, Email, Địa chỉ, Tỉnh thành, Facebook), *Pháp lý* (Mã số thuế), *Khác* (Ngày thành lập/sinh nhật, Điểm tích lũy ban đầu, Giá bán, Thẻ trả trước, Ghi chú). Không cuộn dọc ở độ phân giải 1366×768 @100–150% DPI. Nhãn lỗi hiện ngay cạnh ô sai, thông báo giữ nguyên chữ app gốc. Nút Lưu / Lưu & thêm tiếp (nếu app gốc có) / Hủy; Enter/Esc.
- **Dialog Nhóm khách hàng**: 2 cột (Tên, Ảnh, Điểm tích lũy, Ghi chú | các ô tỉ lệ giảm theo mục 4.2), gọn.
- Chạm: hàng lưới và nút đủ lớn (token cỡ hàng cảm ứng của khung chính).

## 6. Acceptance criteria

Kiểm trên **bản sao** `DATA.FDB` (bản sao trống khách hàng, nên mỗi ca tự tạo dữ liệu). So sánh với app gốc bằng ảnh Chiến chụp (mục 8) hoặc bằng chạy cùng thao tác trên một bản sao khác.

**Dữ liệu & kiểm tra**
- [ ] Thêm khách chỉ có Mã + Tên → lưu thành công; dòng ghi có `STATUS=30`, `USERCREATEDID`/`TIMECREATED` điền đúng, `ID` là GUID hợp lệ, `DIEMTICHLUYBANDAU=0`, `GIABAN=0`.
- [ ] Mã khách mặc định khi bấm Thêm theo đúng mẫu `KH` + 5 số của app gốc và tiếp nối đúng sau khi đã có khách (ca: bảng trống, có KH00001, có mã tay nhập).
- [ ] Để trống Tên → báo lỗi bắt buộc, không ghi dòng; để trống Mã → tương tự.
- [ ] Trùng Mã với khách khác → báo lỗi trùng; trùng Tên khi `ChoPhepTrungTenKhachHang`=0 → báo lỗi; đổi cấu hình thành 30 → cho trùng tên. Sửa chính khách đó (giữ nguyên Mã/Tên) không bị báo trùng.
- [ ] Nhập đủ 15 trường, lưu rồi mở lại: mọi giá trị khớp, kể cả ngày, nhóm, nhân viên, tỉnh thành, giá bán, thẻ trả trước (hai ô cuối chỉ nhập được khi bật `SuDungGiaTheoKhach` / `SuDungTheTraTruoc`).
- [ ] Tên dài 255 ký tự lưu được; 256 ký tự bị chặn/báo lỗi theo app gốc; tiếng Việt có dấu (UTF8) lưu và đọc đúng.
- [ ] Sửa khách: `TIMEMODIFIED`/`USERMODIFIEDID` cập nhật, `TIMECREATED`/`USERCREATEDID` giữ nguyên.
- [ ] Xóa khách chưa phát sinh → biến mất khỏi lưới (cách xóa — cứng hay `STATUS` — đúng như Q2).
- [ ] Xóa khách đã có hóa đơn/đặt hàng/thu chi/điểm/voucher → hành vi và thông báo đúng như app gốc (Q2); không có dữ liệu mồ côi ngoài ý muốn.
- [ ] Danh sách đối chiếu bảng/cột trước và sau: **không có DDL**; chỉ các dòng `DKHACHHANG`/`DNHOMKHACHHANG` thay đổi.

**Nhóm**
- [ ] Thêm/sửa/xóa nhóm đúng mục 4.2; tên nhóm trống hoặc trùng bị chặn; ô "tiền giờ"/"tiền hàng"/"giảm tổng" hiện/ẩn đúng theo `Shared.CoTienGio` và `GopChungGiamGiaTienGioVaTienHangLamMot` (với `CoTienGio = 0`: chỉ hiện "tiền hàng" và 4 ô theo loại đồ; "giảm tổng" ẩn — hoặc theo quyết định A2).
- [ ] Xóa nhóm đang có khách → hành vi đúng app gốc (Q2).
- [ ] Chọn nhóm trên cây lọc đúng lưới; "Tất cả" hiện hết; (con/cháu theo Q8).

**Lưới & tìm kiếm**
- [ ] Cột, thứ tự, hiện/ẩn mặc định đúng bảng mục 4.1; cột Ghi chú bật lên được.
- [ ] Gõ vào ô tìm lọc đúng theo quy tắc app gốc (không phân biệt hoa/thường; có/không bỏ dấu theo Q7); F3 vào ô tìm; xóa chữ thì hiện lại đủ.
- [ ] 1 000 khách (tạo bằng script trên bản sao) mở màn < 1 giây, cuộn/lọc mượt.

**Quyền**
- [ ] Đăng nhập admin: đủ nút. Nhóm *Quản lý*/*Kế toán* (240): đủ nút. Tạo bản sao với mode 0x10 (chỉ xem): mở được, không thêm/sửa/xóa được. Mode 0: không thấy menu và Ctrl+K không mở được (đã đúng ở khung chính).

**UI**
- [ ] Dialog khách và nhóm không cuộn dọc ở 1366×768; đúng ở 100%, 125%, 150% DPI.
- [ ] Đổi bảng màu khi màn đang mở: lưới, cây, dialog đổi ngay; không có màu hard-code (kiểm bằng grep và một bảng màu khác).
- [ ] Thao tác chính (chọn nhóm, thêm, sửa, xóa, tìm) dùng được bằng cảm ứng.
- [ ] Không có SQL trong View/ViewModel (kiểm bằng grep).

**Theo §9.0**
- [ ] Xóa khách đã có phát sinh ở bất kỳ bảng nào tại §4.4 bị chặn; khách chưa phát sinh xóa cứng được. Xóa nhóm còn khách bị chặn.
- [ ] Điểm tích lũy ban đầu không nhận số âm, số thập phân. Tỉ lệ giảm ngoài 0–100 bị chặn.
- [ ] Ô "Tỉ lệ giảm giá" luôn hiện trên dialog nhóm. "Giá bán", "Thẻ trả trước" bị khóa khi cấu hình tắt. Thiếu quyền nào thì ẩn nút đó.

## 7. Kế hoạch implement (chỉ chạy sau khi Chiến duyệt)

1. **Data**: `CustomerRepository` (tìm/lọc, theo ID, thêm, sửa, xóa, kiểm tra trùng Mã/Tên, sinh mã tiếp theo), `CustomerGroupRepository`, `LookupRepository` (nhân viên, tỉnh thành), đọc cấu hình `SCONFIG` cần dùng (`ChoPhepTrungTenKhachHang`, `GopChungGiamGiaTienGioVaTienHangLamMot`, `SuDungGiaTheoKhach`, cờ có tiền giờ). Tham số hóa SQL, transaction cho xóa/chuyển nhóm.
2. **Khung danh mục tái dùng** (`UI/Catalog/…`): lưới + cây + ô tìm + thanh nút, dialog thêm/sửa dựng từ mô tả trường; đọc theme từ token.
3. **ViewModel/Presenter**: `CustomerCatalogViewModel`, `CustomerEditViewModel`, `CustomerGroupEditViewModel` (kiểm tra, trạng thái nút theo quyền).
4. **Màn + dialog** WinForms cho khách và nhóm; nối vào khung chính: thay tab tạm của menu "Danh mục khách hàng" (và Ctrl+K) bằng màn thật; thêm file vào `Kazuko_FnB.csproj` thủ công (csproj kiểu cũ).
5. **Kiểm thử** theo mục 6 trên bản sao DB; ảnh chụp UI (`--render-*`) để đối chiếu với ảnh app gốc.

File/vùng chạm dự kiến: `Kazuko_FnB/Data/` (repository mới), `Kazuko_FnB/UI/` (màn + dialog + khung danh mục), `Kazuko_FnB/Services/MainMenuBuilder.cs` (hoặc nơi mở tab theo menu), `Kazuko_FnB/Kazuko_FnB.csproj`. **Không có migration DB.** Một patch/task theo skill `kazuko-patch`; không commit/push khi chưa được Chiến bảo.

## 8. Ảnh cần chụp từ app gốc (Chiến chụp, dữ liệu thử trên **bản sao**)

Chụp toàn cửa sổ, ghi chú dưới mỗi ảnh nếu có thao tác đặc biệt.
1. **Màn danh mục lúc mở** (Khách hàng > Danh mục khách hàng): thấy cả thanh nút, ô tìm, cây nhóm, lưới (cả khi chưa có khách và khi có vài khách).
2. Cây nhóm: bấm **chuột phải** vào cây/nhóm (menu ngữ cảnh), và cách **thêm / sửa / xóa nhóm** (nút nào, ở đâu).
3. Lưới: bấm **chuột phải** vào lưới (menu ngữ cảnh, cột nào bật/tắt được), tiêu đề cột khi **sắp xếp**, dòng tổng/trạng thái dưới lưới nếu có.
4. **Dialog Thêm khách hàng** vừa mở (thấy Mã khách đã tự điền, nhóm chọn sẵn hay không); thêm lần nữa sau khi đã có KH00001 để thấy mã kế tiếp.
5. Dialog **Sửa khách hàng** có dữ liệu đầy đủ; thêm ảnh khi mở các ô **Nhóm / Nhân viên / Tỉnh thành** (danh sách thả xuống có nút thêm nhanh không); và combo **Giá bán** (nếu ô bị mờ, chụp thêm cấu hình `Sử dụng giá theo khách` đang bật/tắt).
6. **Lỗi kiểm tra** (mỗi lỗi một ảnh): để trống Tên; để trống Mã; trùng Mã; trùng Tên (khi cấu hình "Cho phép trùng tên khách hàng" = tắt); bật cấu hình đó lên rồi trùng Tên (không báo lỗi); nhập quá độ dài (nếu gõ được); nhập chữ vào ô Điểm tích lũy/Điện thoại/Email sai dạng.
7. **Xóa**: (a) xóa khách chưa phát sinh — hộp xác nhận; (b) xóa khách **đã có hóa đơn** (hoặc đặt hàng/thu chi/điểm tăng giảm) — thông báo gì hoặc có xóa được không; (c) xóa **nhóm có khách**; (d) chọn nhiều dòng rồi xóa (nếu cho phép).
8. **Tìm kiếm**: gõ không dấu / có dấu / viết hoa vào ô tìm; ô tìm khi có kết quả và khi không có; bấm F3.
9. **Chọn nhóm cha và nhóm con** trên cây (nếu có cây nhiều cấp): lưới hiện khách của cấp nào.
10. **Dialog Nhóm khách hàng** (thêm và sửa), ở 3 trạng thái cấu hình: bình thường; cửa hàng không dùng tiền giờ; "Gộp chung giảm giá tiền giờ và tiền hàng" bật.
11. Các nút trên thanh công cụ của màn (di chuột để thấy tên): **In / Xuất Excel / Nhập Excel / Gửi tin nhắn** nếu có, và hộp thoại sau khi bấm (không cần chạy tới cùng).
12. Tài khoản **quyền hạn chế** (nhóm Quản lý/Kế toán đang 240; tạo thử mode chỉ-xem 0x10 hoặc không-có-quyền-Xóa trên bản sao): màn danh mục hiện ra thế nào (nút mờ hay ẩn).
13. (Nếu có) Mở lại sau khi tắt app: màn có nhớ độ rộng/ẩn hiện cột không.

## 9. Rủi ro / câu hỏi mở

### 9.0 Chiến chốt 25/11/2026 (hỏi trong phiên). Mục này thắng phần khác của spec nếu khác

- **Xóa khách (Q2):** khách đã có phát sinh thì **chặn xóa**; khách chưa phát sinh thì xóa cứng. Bảng cần kiểm: các bảng tham chiếu ở §4.4.
- **Xóa nhóm (Q2):** nhóm còn khách thì **chặn xóa**.
- **Điểm tích lũy ban đầu (Q5):** chỉ nhận số nguyên từ 0.
- **Tỉ lệ giảm của nhóm (Q9):** chặn ngoài 0–100.
- **Ô "Tỉ lệ giảm giá" / giảm tổng (A2):** **luôn hiện** (khác app gốc). Ảnh hưởng lên tiền hóa đơn phải đối chiếu ở spec bán hàng.
- **"Giá bán", "Thẻ trả trước" khi cấu hình tắt (Q6, A3):** làm mờ, khóa.
- **Thiếu quyền Thêm/Sửa/Xóa (Q10):** **ẩn nút**. Chiến chưa chốt riêng việc chỉ có quyền Xem thì hộp thoại có mở ở chế độ chỉ đọc không.
- **Phạm vi (A1, Q11):** quản lý nhóm nằm trong spec này; Khách hàng thân thiết, Gửi tin nhắn, Nhập từ hệ thống cũ, Công nợ làm spec sau.
- Còn phải xác minh trước khi duyệt: Q3, Q4, Q7, Q8, Q12 và phần chỉ đọc của Q10.

### 9.0.1 Xác minh 25/11/2026

- **A2:** `Hóa đơn nhà hàng` chỉ dùng `DNHOMKHACHHANG.TILEGIAMGIA` khi `GopChungGiamGiaTienGioVaTienHangLamMot = 30` (đang 0; khi đó dùng `TILEGIAMGIATIENHANG`). Nhưng `Hóa đơn bán hàng` (bán lẻ), hàm `GetTiLeGiamTheoKhach`, lấy `TILEGIAMGIA` khi khách có nhóm tồn tại và thay cho `MacDinhGiamGia`. Vì vậy sửa ô này ảnh hưởng tiền ở bán lẻ.
- **Q12, một phần:** `STABLEDESC` của `DKHACHHANG` và `DNHOMKHACHHANG` có `INVOICEPRINT = 0`, `BARCODEPRINT = 0`. Nút xuất Excel chưa xác minh.
- **Q7, một phần:** `SCOLUMN.FORMAT` của Điện thoại, Email, Mã số thuế rỗng, nên không thấy luật định dạng trong metadata này. Cách ô tìm lọc và bỏ dấu chưa xác minh.
- **Q8, một phần:** màn `Khách hàng thân thiết` chỉ lọc đúng nhóm đang chọn (`DNHOMKHACHHANGID = SelectedID`), không gồm nhóm con. Màn danh mục do khung dựng, chưa xác minh.
- **Chưa xác minh:** Q3 (sinh mã), Q4 (so trùng hoa/thường, dấu, khoảng trắng; metadata chỉ cho biết cột nào cấm trùng), chế độ chỉ đọc ở Q10. Nhiều khả năng thuộc khung `No1Lib`; cần thử trên app gốc.

**Rủi ro**
- Hành vi thêm/sửa/xóa/tìm nằm trong framework `No1Lib.*` (không có mã) → chỉ chắc khi có ảnh/đối chiếu; phần "nguyên văn thông báo lỗi" và "xóa khi có phát sinh" là nơi dễ lệch nhất.
- DB **không có FK/unique** → nếu bản clone và app gốc cùng ghi vào một DB (không nên, dùng bản sao riêng) có thể sinh trùng mã; kiểm trùng phải làm trong transaction.
- `DIEMTICHLUYBANDAU` và ngưỡng điểm nhóm ảnh hưởng giảm giá/nâng nhóm ở hóa đơn: sai ở đây là sai tiền → cần đối chiếu số liệu ở G2.
- Bản sao DB không có khách/nhóm/tỉnh thành → ca kiểm thử phải tự dựng; cần dữ liệu thật (đã che thông tin cá nhân) nếu muốn kiểm tải.
- **[Sửa theo DB thật]** `STABLEDESC.FORMAT` của `DKHACHHANG` **rỗng (NULL)** trên DB thật — nhận định cũ "chứa 5 cặp (ID mẫu, `LOAI` 1–4 và −1)" đến từ bản sao khác và không áp dụng. Nút In danh sách (nếu có) cần cơ sở khác hoặc bỏ (ảnh #11).

**Câu hỏi cho Chiến** (chưa rõ từ code; không đoán)
- **A1.** Nhóm khách hàng quản lý ngay trên cây của màn này (không có menu riêng), đúng không? Cho gộp vào spec này luôn?
- **Q2.** Xóa khách: xóa hẳn dòng hay chuyển `STATUS`? Khách đã có hóa đơn/đặt hàng/thu chi/điểm/voucher thì bị chặn (thông báo gì) hay vẫn xóa? Xóa nhóm còn khách thì sao?
- **Q3.** Mã tự sinh `{KH(*****)}`: ra "KH00001"? Số kế tiếp = lớn nhất+1 hay đếm số dòng? Mã có sửa tay được không, và mã nhập tay có làm số kế tiếp nhảy không?
- **Q4.** So sánh trùng Mã/Tên có phân biệt hoa/thường, dấu, khoảng trắng đầu/cuối không (ví dụ "An" và "an")? Có kiểm trùng Điện thoại/MST/CMND không (code không thấy)?
- **Q5.** Điểm tích lũy ban đầu: cột là số nguyên, UI cho 2 số thập phân và âm. Cho nhập âm/thập phân không? Nếu nhập 12,5 thì làm tròn hay cắt?
- **Q6.** Ô "Giá bán" bật/tắt theo cấu hình nào (nghi `Sử dụng giá theo khách`)? Tắt thì ẩn hay mờ, và lưu mặc định 0?
- **Q7.** Email, Điện thoại, MST, CMND có kiểm định dạng không? Ô tìm lọc theo cột nào, có bỏ dấu tiếng Việt không?
- **Q8.** Chọn nhóm cha có hiện khách của nhóm con không? Nhóm có nhiều cấp thật hay chỉ một cấp?
- **Q9.** Tỉ lệ giảm có cho >100% hoặc âm không?
- **Q10.** Khi thiếu quyền Thêm/Sửa/Xóa: ẩn nút hay làm mờ? Chỉ có quyền Xem thì dialog mở ở chế độ chỉ đọc?
- **Q11.** Xác nhận ngoài phạm vi spec này: Khách hàng thân thiết, Gửi tin nhắn, Nhập từ hệ thống cũ, công nợ — làm spec riêng sau. Được không?
- **Q12.** Cần "Lưu & thêm tiếp", In/Xuất Excel danh sách khách trong bản mới không (nếu app gốc có thì giữ y hệt)?
- **A2 (mới).** Ô "Tỉ lệ giảm giá" (giảm tổng) của nhóm khách không bao giờ hiện ở POS TAP (`CoTienGio = 0`) nhưng cột vẫn được đọc khi bán: luôn hiện ô này trong bản mới, hay bỏ cột khỏi dialog?
- **A3 (mới).** Ô "Thẻ trả trước" trên dialog khách chỉ bật khi `SuDungTheTraTruoc = 30` (hiện 0): ẩn hay mờ khi tắt? (Cùng câu với Q6 cho "Giá bán".)
