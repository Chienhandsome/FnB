# Spec: Danh mục bàn khu vực (DBAN + DKHUVUC) — DELTA theo khuôn Danh mục mặt hàng

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-21. Base: `main` @ `6a57cd3` (đã `git fetch origin main`, commit mới nhất là `6a57cd3 add spec DANHMUCMATHANG`)
- Khuôn: [danh-muc-mat-hang.md](danh-muc-mat-hang.md). Spec này **chỉ ghi phần khác**; phần giống hệt ghi "**theo khuôn Danh mục mặt hàng**" (bố cục cây + lưới, flow thêm/sửa/xóa, phân quyền theo bit, kiểu kiểm tra, thông báo, thanh nút, phím tắt).
- Phạm vi: màn **Danh mục bàn khu vực** (quản lý cây khu vực + lưới bàn). **Không** gồm:
  - "Thiết kế giao diện bàn" (`SFORM` 82055203) và "Thiết kế giao diện khu vực" (`SFORM` e58e61ee) — trình thiết kế sơ đồ, chức năng riêng.
  - Màn Table Screen / "Chọn bàn" (`SFORM` c52e7ec2) / "Chuyển bàn" / "Hóa đơn nhà hàng" — sơ đồ bàn lúc bán hàng, engine tự dựng. Chỉ đọc để biết **luật nào dùng cấu hình bàn/khu vực**.

## 0. Nguồn, cách đọc, giới hạn

Nhãn nguồn trong spec:

- **[DB]** đọc trực tiếp từ bản chép chỉ đọc `DATA.fdb` trong `POS TAP\Database\` (copy vào thư mục tạm, **không** sửa bản gốc, không dùng `DEMO.FDB`). Đọc qua FirebirdSql.Data + `fbembed.dll` bằng PowerShell 32-bit. Chắc chắn về **giá trị và cấu trúc hiện có**.
- **[CODE]** đọc từ mã đã xuất trong `POS_TAP_SRC_EXPORT/STABLEDESC/DBAN__f2288345/`, `STABLEDESC/DKHUVUC__9e2afcb8/`, `SFORM/Thêm nhanh bàn__ff770199/`, `SFORM/Chuyển bàn__0001db41/`, `SFORM/Hóa đơn nhà hàng__f3f7bb77/`, `SFORM/Chọn bàn__c52e7ec2/`, `SFORM/Dịch vụ máy tính bảng__12b301d8/`. Chắc chắn về **code có tồn tại**; hành vi chạy thật chưa thấy.
- **[APP]** **chưa có** trong vòng này: không chạy bản sao app gốc, không chụp ảnh. Mọi chỗ cần app gốc nằm ở mục 6.2 kèm kịch bản test. Thư mục `docs/specs/img/danh-muc-ban-khu-vuc/` **chưa tạo**.
- **[CHƯA XÁC MINH]** không có trong DB/mã đã đọc; ghi ở mục 6.2.

Giới hạn: DB hiện có **1 khu vực ("Lầu 1") và 1 bàn ("bàn 1")**, không có hóa đơn nào gắn bàn (`TDONHANG.DBANID` không có giá trị), không có bảng giá theo bàn/khu vực. Nên mọi quy tắc "chặn xóa khi tham chiếu" chỉ đọc từ mã, **chưa kiểm được**.

---

## 1. Bố cục UI

### 1.1 Màn danh mục — theo khuôn, khác ở cây và lưới

- **Cây bên trái** = **khu vực**: `DBAN` có `TABLETYPE = 2` (cây + lưới) và `CATEGORYCOL = DKHUVUCID` [DB]. Cây là danh sách `DKHUVUC` **đang dùng** (`STATUS = 30`) [CODE: Chọn bàn dùng cùng điều kiện]. Nút **+** / **✎** ở panel như khuôn.
  - **Khác khuôn:** `DKHUVUC` có `TABLETYPE = 0` [DB] = danh sách phẳng, không cây. Nên khu vực **không có nhóm con**. Mục "Chưa thiết lập" của khuôn: **mặc định không có** (bàn bắt buộc có khu vực, xem 3.1). Xác nhận ở Q2.
- **Lưới bên phải** = bàn trong khu vực đang chọn (cột ở mục 1.1a). Thanh nút: **Lọc (F3), Thêm mới (Insert), Chỉnh sửa (F4), Xóa (Del), Thêm nhanh (mới — xem 2.2), In**. "Thêm excel / Xuất excel / Import" **không** áp dụng cho bàn: chưa có chỗ trong DB (không có `SREPORT`/định lượng), xem Q16.
- **Dưới:** panel "Thông tin" (Khởi tạo · Khởi tạo bởi · Sửa đổi gần nhất · Sửa đổi bởi) như khuôn.

### 1.1a Cột lưới bàn — **chưa có cấu hình trong DB**

- `SGRID` không có dòng nào gắn `DBAN` hay `DKHUVUC` [DB: `SGRID` có 2 dòng, không dòng nào trỏ `GRIDID` tới hai bảng này]. Nghĩa là **không có cấu hình cột lưới** để copy như khuôn ("SGRID của DMATHANG" không áp dụng).
- Cột đề xuất (chờ app gốc xác nhận ở Q1), lấy từ `SCOLUMN` [DB] và `designcode` [CODE]:

| Cột | Nguồn | Ghi chú |
|---|---|---|
| Tên bàn | `DBAN.NAME` | bắt buộc, duy nhất (3.6) |
| Khu vực | `DBAN.DKHUVUCID` → `DKHUVUC.NAME` | theo lọc cây |
| Nhóm hiển thị | `DBAN.DNHOMHIENTHIID` → `DNHOMHIENTHI` | caption "Nhóm hiển thị" [DB] |
| Loại phòng | `DBAN.DLOAIPHONGID` → `DLOAIPHONG` | caption "Loại phòng" [DB] |
| Cách tính giờ / Đơn giá / Bảng giá / Tiền mở bàn | `CACHTINHGIO`, `DONGIA`, `DBANGGIAID`, `TIENMOBAN` | **chỉ hiện khi `CoTienGio` bật** (3.10) |
| Ghi chú | `DBAN.NOTE` | |
| IP công tắc điện | `DBAN.IPCONGTAC` | chỉ hiện khi điều kiện 3.10 thỏa |

- **Không** đưa vào lưới: `TRANGTHAI`, `TDONHANGID`, `TAIKHOANGIU`, `GIULUC` (trạng thái runtime do engine bán hàng ghi, xem 3.2) và toàn bộ cột thiết kế (3.2).
- Lưới chỉ đọc (theo khuôn) — sửa qua dialog.

### 1.2 Dialog Thêm/Sửa BÀN (`DBAN`) — khác khuôn

Dialog gồm trang "Thông tin bàn" và trang "Bảng giá áp dụng" (`pageBangGia`, chỉ hiện khi `CoTienGio` và `ChoPhepDungNhieuBangGiaTrenBill`). Bố cục theo `designcode` [CODE]:

| Control (app gốc) | Nhãn / trường DB | Điều kiện hiện | Ghi chú |
|---|---|---|---|
| `txtNAME` | **Tên bàn** — `NAME` | luôn | bắt buộc, không trùng (3.6) |
| `lueDKHUVUCID` | **Khu vực** — `DKHUVUCID` | luôn | bắt buộc; `MUSTSELECTCATEGORY = 30` [DB] |
| `lueDBANGGIAID` | Bảng giá — `DBANGGIAID` | `rdTheoBangGia` được chọn | ẩn khi không chọn theo bảng giá |
| `numDONGIA` | Đơn giá — `DONGIA` | `rdTheoGio` được chọn | |
| `lueDNHOMHIENTHIID` | Nhóm hiển thị — `DNHOMHIENTHIID` | [CHƯA XÁC MINH] có luôn không | |
| `numTIENMOBAN` | Tiền mở bàn — `TIENMOBAN` | `CoTienGio` | ẩn khi không bật giá theo giờ [CODE] |
| `txtNOTE` | Ghi chú — `NOTE` | luôn | |
| `grCachTinhGio`: `rdTheoKhuVuc` / `rdTheoGio` / `rdTheoBangGia` | Cách tính giờ — `CACHTINHGIO` | `CoTienGio` | Thêm mới: mặc định **theo khu vực** [CODE] |
| `lueDLOAIPHONGID` | Loại phòng — `DLOAIPHONGID` | [CHƯA XÁC MINH] | |
| `txtIPCONGTAC`, `btnBat`, `btnTat` | Ip công tắc điện — `IPCONGTAC` | điều kiện 3.10 | Bật/Tắt đèn bàn qua IP |
| `grBangGia` (trang Bảng giá áp dụng) | bảng `DBANGGIATHEOBAN` | điều kiện trang | danh sách bảng giá với cột chọn |

- Không có trên dialog nhưng có trong bảng (xem 3.2): các trường runtime và thiết kế.
- **Khác khuôn:** Dialog bàn **không** có ô mã, không có ảnh, không có tab định lượng, không có link lịch sử.

### 1.3 Dialog Thêm/Sửa KHU VỰC (`DKHUVUC`) — khác khuôn

Dialog có trang chính và tối đa 4 trang phụ, theo `designcode` và `code.cs` [CODE]:

| Trang | Control | Nhãn / trường DB | Điều kiện hiện |
|---|---|---|---|
| Khu vực | `txtNAME` | **Tên khu vực** — `NAME` | luôn; bắt buộc, không trùng (3.6) |
| Khu vực | `lueSIMAGEID` | Ảnh — `SIMAGEID` | [CHƯA XÁC MINH] |
| Khu vực | `lueSTEMPLATEID` | **Mẫu hóa đơn** — `STEMPLATEID` (→ bảng `STEMPLATE`) | luôn; danh sách lọc `SFORMID = 5f029a5b…` [CODE] — xem 3.4, Q11 |
| Khu vực | `lueDKHOHANGID` | Kho hàng — `DKHOHANGID` | [CHƯA XÁC MINH] theo `SuDungNhieuKho` |
| Khu vực | `lueDGIAMATHANGID` | **Giá mặt hàng** — `DGIAMATHANGID` | luôn; danh sách lọc theo `SuDungGia2/3/4` (3.5) |
| Khu vực | `lueDBIEUTUONGID` + `ptLogo` | Biểu tượng — `DBIEUTUONGID` | ảnh xem trước hiện theo lựa chọn [CODE] |
| Khu vực | `numTIENMOBAN` | Tiền mở bàn — `TIENMOBAN` | `CoTienGio` |
| Khu vực | `rdTheoGio` / `rdTheoBangGia` | Cách tính giờ — `CACHTINHGIO` (chỉ hai giá trị) | `CoTienGio` |
| Khu vực | `numDONGIA`, `lueDBANGGIAID`, `lueDGIATHEOGIOID`, `dtTUGIO`, `dtDENGIO` | Đơn giá, Bảng giá, Giá theo giờ, Từ giờ, Đến giờ | theo cách tính giờ |
| Khu vực | `rdTABHIENTHI` | Tab hiển thị — `TABHIENTHI` | [CHƯA XÁC MINH] ý nghĩa giá trị |
| Khu vực | `txtNOTE` | Ghi chú — `NOTE` | luôn |
| Trang "Mặt hàng theo giờ" | (`pageMatHangTheoGio`) | `DGIATHEOGIO` | `SuDungGiaTheoGio` |
| Trang "Mặt hàng mặc định" | `grMatHangMacDinh` (`GridMapper`, bảng `DMATHANGMACDINH`) | mặt hàng + số lượng | `SuDungMatHangMacDinh` |
| Trang "Bảng giá áp dụng" | `grBangGia` | `DBANGGIATHEOKHUVUC` | `CoTienGio` và `ChoPhepDungNhieuBangGiaTrenBill` |
| Trang "Truy cập" | `grTruyCap` | `DTKTHEOKHUVUC` | `PhanQuyenTruyCapTheoKhuVuc` |

- Trang "Truy cập": lưới liệt kê người dùng `SUSER` có `ISADMIN` khác 30, cột tick → ghi/xóa `DTKTHEOKHUVUC` [CODE].
- Cách tính giờ của khu vực: `CACHTINHGIO` có hai giá trị trên dialog, `THEOGIO` và `THEOBANGGIA` [CODE: `mapper_BeforeSave`].

### 1.4 Hộp thoại "Thêm nhanh bàn" (`SFORM` ff770199) — **chức năng mới so với khuôn**

Mở từ màn danh mục bàn [CODE: form riêng; điểm mở chưa xác minh, Q9]. Bố cục [CODE]:

- Lưới `grMain`: mỗi dòng là một khu vực đang dùng (`ID`, `KHUVUC`) với 4 cột nhập: **BATDAU** (tiền tố), **TUSO**, **DENSO** (số đầu, số cuối), và cột **DULIEU** chỉ đọc (ví dụ `B01 ... B12`).
- Ô **Chiều dài số** (`numChieuDai`): tự cập nhật bằng số chữ số của `DENSO` lớn nhất; số được đệm 0 theo độ dài này.
- Nút **Thực hiện** (`btnThucHien`).

### 1.5 Component dùng trong bản mới

Theo khuôn mục 1.5 của Danh mục mặt hàng (`FlatButton`, `ThemedDialog`, `ThemedContextMenu`, `ThemeApplier`, `LookupCombo`, `NumericUpDown` với `decimal`, `DataGridView`). Bố cục giữ y hệt; màu/cỡ chữ do theme. Không SQL trong View/ViewModel (CLAUDE.md).

---

## 2. Flow thao tác

### 2.1 Thêm/sửa/xóa bàn và khu vực — theo khuôn

- **Thêm bàn** (Insert hoặc menu chuột phải): theo khuôn. Khi đang ở "Tất cả" → hộp chọn **khu vực** (tương đương "Mời bạn chọn Nhóm mặt hàng") vì `MUSTSELECTCATEGORY = 30`. Khi đang chọn một khu vực → dialog mở với **Khu vực = khu vực đang chọn**.
- **Mặc định khi thêm bàn** [CODE `DBANAe.mapper_AfterFillData`]: `CACHTINHGIO` = theo khu vực (radio), `DONGIA`/`TIENMOBAN` = 0.
- **Sửa bàn**: F4 hoặc nhấp đôi dòng; dialog nạp bản ghi và chọn lại radio cách tính giờ theo `CACHTINHGIO`.
- **Thêm/sửa khu vực**: nút **+** / **✎** ở panel khu vực [chưa xác minh điểm mở — Q2]. Mặc định: theo flow của nhóm trong khuôn.
- **Xóa bàn / xóa khu vực**: hộp xác nhận theo khuôn, **chặn** theo 3.9. Chờ quyết định (Q3, Q4).

### 2.2 Thêm nhanh bàn — flow

1. Chọn các khu vực trên lưới, nhập BATDAU / TUSO / DENSO (có thể nhiều khu vực một lần).
2. Chỉnh chiều dài số nếu cần; DULIEU cập nhật ví dụ.
3. Bấm **Thực hiện**. Hệ thống **kiểm tra toàn bộ trước khi ghi** [CODE]:
   - Tên đã tồn tại trong `DBAN` (so sánh không phân biệt hoa/thường, toàn bảng, không theo khu vực) → cảnh báo `"<Bàn> '<tên>' đã tồn tại trong dữ liệu"`, dừng.
   - Tên trùng trong chính lần sinh → cảnh báo `"<Bàn> '<tên>' bị trùng lặp"`, dừng.
   - Ghi `DBAN` với `CACHTINHGIO = THEOKHUVUC`, `DONGIA = 0`, `TIENMOBAN = 0`; sau đó `Config.SetLastUpdate(Tables.DBAN)` [CODE].
4. Đóng hộp thoại với kết quả OK.
- Từ bằng số: bàn được sinh cho các số `TUSO ... DENSO` **khi `TUSO < DENSO`** (so sánh nghiêm ngặt, xem 5.1).

### 2.3 Lưu

Theo khuôn 2.1 (`mapper_BeforeSave` → framework → `mapper_AfterSave`). **Khác khuôn:** lưu bảng giá, truy cập và "mặt hàng mặc định" của khu vực nằm **sau** lưu bản ghi chính, mỗi bảng một lần ghi riêng, **không** trong cùng transaction [CODE: `DKHUVUCAe.mapper_AfterSave`]. Xem 5.2.

---

## 3. Luật nghiệp vụ

### 3.1 Cột `DBAN` — dialog, lưới và phần chỉ có trong DB

`DBAN` có 54 cột [DB]. Phân loại:

| Nhóm | Cột | Hiện ở đâu |
|---|---|---|
| **Nghiệp vụ, trên dialog** | `ID`, `NAME`, `DKHUVUCID`, `DBANGGIAID`, `DONGIA`, `CACHTINHGIO`, `DNHOMHIENTHIID`, `TIENMOBAN`, `DLOAIPHONGID`, `NOTE`, `IPCONGTAC` | 1.2 |
| **Trạng thái (`STATUS` và runtime)** | `STATUS` (30 = đang dùng, 0 = đã xóa mềm, xem 3.9); `TRANGTHAI`, `TDONHANGID`, `TAIKHOANGIU`, `GIULUC` | `STATUS` ẩn; các cột runtime **không** sửa ở danh mục (xem 3.2) |
| **Kiểm toán** | `USERCREATEDID`, `TIMECREATED`, `USERMODIFIEDID`, `TIMEMODIFIED` | panel "Thông tin" |
| **Thiết kế sơ đồ (không thuộc Danh mục)** | `POSLEFT`, `POSTOP`, `WIDTH`, `HEIGHT`, `ZINDEX`, `ROTATE`, `BUTTONSHAPE`, `FLAT`, `HASBORDER`, `BORDERWIDTH`, `BACKCOLOR`, `FONTNAME`, `FONTSIZE`, `FONTCOLOR`, `BOLD`, `ITALIC`, `UNDERLINE`, `TEXTALIGN`, `ICON`, `ICONALIGN`, `IMAGEZOOM`, `TRANSPARENT`, `VISIBLE`, `CONTROLNAME`, `CONTROLTYPE`, `CONTROLLEVEL`, `PARENTCONTROLNAME`, `CUSTOMDATA1/2/3`, `REF1ID`, `REF2ID`, `NUMCHAIRS`, `ANH` | **không** có trên dialog hay lưới của Danh mục; thuộc "Thiết kế giao diện bàn" (ngoài phạm vi) |

- Đếm: 20 cột nghiệp vụ/kiểm toán/trạng thái + 34 cột thiết kế = 54 ✔ [DB].
- Quy tắc: khi **sửa** bàn từ danh mục, **giữ nguyên** mọi cột thiết kế và runtime (không ghi đè về 0/NULL). Đây là quy tắc chốt ở spec này, không phải câu hỏi.
- `ANH` có caption "Anh" trong `SCOLUMN` [DB] nhưng **không** có ô trên dialog bàn [CODE]. Ghi nhận; xem Q12.

### 3.2 Trạng thái runtime — không sửa ở danh mục

- `TRANGTHAI`, `TDONHANGID`, `TAIKHOANGIU`, `GIULUC`: do engine bán hàng ghi. Đơn hàng gắn bàn qua `TDONHANG.DBANID` [CODE: nhiều chỗ, ví dụ `Dịch vụ máy tính bảng` dòng 953].
- Có **1 trigger người dùng** trong DB: `TDONHANG_TRIGGER` trên bảng `TDONHANG` [DB]. Trigger này đọc/ghi `DBAN.TDONHANGID` khi đổi `DATHANHTOAN` giữa 0 và >0 (SQL trong `RDB$TRIGGER_SOURCE`, đã đọc tên bảng) [DB]. Ảnh hưởng tới xóa bàn (3.9).

### 3.3 Cột `DKHUVUC` — dialog, lưới và phần thiết kế

`DKHUVUC` có 33 cột [DB]:

| Nhóm | Cột |
|---|---|
| Nghiệp vụ, trên dialog | `ID`, `NAME`, `STEMPLATEID`, `DKHOHANGID`, `DGIAMATHANGID`, `DGIATHEOGIOID`, `TUGIO`, `DENGIO`, `TIENMOBAN`, `DONGIA`, `DBANGGIAID`, `CACHTINHGIO`, `DBIEUTUONGID`, `SIMAGEID`, `TABHIENTHI`, `NOTE`, `STATUS` |
| Cây / khung (không dùng ở UI) | `PARENTID`, `PARENTDIR`, `ITEMTYPE`, `AUTOID`, `SORTORDER` (DB: `SORTORDER = 'ZZZZ'`, `PARENTID = ''`) — `TABLETYPE = 0` nên không phải cây |
| Thiết kế khu vực (ngoài phạm vi) | `GRIDSIZE`, `WIDTH`, `HEIGHT`, `BACKCOLOR`, `BACKGROUNDIMAGE`, `MAUSAC`, `ANH` |
| Kiểm toán | `USERCREATEDID`, `TIMECREATED`, `USERMODIFIEDID`, `TIMEMODIFIED` |

- Giá trị DB của khu vực duy nhất: `STATUS = 30`, `STEMPLATEID = NULL`, `DGIAMATHANGID = 0`, `CACHTINHGIO = 1`, `DONGIA = 0`, `TIENMOBAN = 0`, `DKHOHANGID` và `DBIEUTUONGID` có giá trị [DB].

### 3.4 Mẫu hóa đơn theo khu vực — luật và fallback [CODE]

- Hóa đơn nhà hàng (`Hóa đơn nhà hàng__f3f7bb77`, dòng 3277 và 3460) lấy mẫu:
  ```
  SELECT STEMPLATEID FROM DKHUVUC WHERE ID = (SELECT DKHUVUCID FROM DBAN WHERE ID = <bàn>)
  ```
  Nếu rỗng → `SystemConfig.MauHoaDon`.
- `DKHUVUC.STEMPLATEID` trỏ bảng **`STEMPLATE`** (mẫu in) [DB: cột tham chiếu `REFTABLEID` = `STEMPLATE`], **không** trỏ `SFORM`.
- `SCONFIG.MauHoaDon` = `190263eb-…` = **"Mẫu in bill 80mm"** [DB].
- Hóa đơn bán hàng (`Hóa đơn bán hàng__5f029a5b`, dòng 803) dùng luật khác: `DCUAHANG.STEMPLATEID` qua kho → fallback `SystemConfig.MauHoaDon` — **không** theo khu vực.
- Combo `lueSTEMPLATEID` của khu vực chỉ liệt kê mẫu có `SFORMID = 5f029a5b…` (= "Hóa đơn bán hàng") [CODE]. Nghi ngờ: nếu bill bàn dùng `SFORM` "Hóa đơn nhà hàng" thì combo không hiện đúng mẫu. Xem Q11.
- `SCONFIG.MauInChuyenBan` = `ef280054-…` = **"Mẫu 80mm"** [DB] — dùng khi chuyển bàn [CODE].

### 3.5 Giá theo khu vực — `DKHUVUC.DGIAMATHANGID`

- Giá trị **lưu** trong `DKHUVUC.DGIAMATHANGID`/`DGIATHEOGIOID` là ID của bảng **`DGIAMATHANG`** [DB]:

| ID | Tên (DB) | Ý nghĩa đọc từ mã [CODE] |
|---|---|---|
| `1` | Giá chung | `GIABAN` |
| `2` | Giá bán t2 | `GIABAN2` |
| `3` | Giá bán t3 | `GIABAN3` |
| `4` | Giá bán cafe | `GIABAN4` |
| `0` | Hiển thị để lựa chọn | Chuyển bàn: giá trị 0 → `GIABAN` (mặc định) [CODE]. Ý nghĩa trên bill chưa rõ — Q9 |

- Giá trị khu vực mặc định của DB = `0`.
- Combo `lueDGIAMATHANGID` ẩn các ID `2`, `3`, `4` khi `SuDungGia2/3/4` tắt [CODE: `lueDGIAMATHANGID_CustomLoadData`]. **DB hiện tắt cả ba** → combo chỉ còn `0` và `1`.
- `SCONFIG.DienGiaiGia3` = **"Giá 2"** (trùng với Giá 2) [DB] — có vẻ là dữ liệu sai; xem Q10.
- Dùng khi lên bill: `Hóa đơn nhà hàng` dòng 1190 và 3721, `Dịch vụ máy tính bảng` dòng 2640 — chọn `GIABAN*` theo `DGIAMATHANGID` của khu vực; nếu tính theo giờ thì dùng `DGIATHEOGIOID` (dòng 3733) [CODE].

### 3.6 Trùng tên và mã

- **Mã**: `DBAN` và `DKHUVUC` **không có cột `CODE`** [DB]. Không có sinh mã. Khóa là `ID` (GUID) và tên. Xác nhận ở Q7.
- **Tên bắt buộc, không trùng**: `SCOLUMN.NAME` có `ALLOWEMPTY = NO`, `ALLOWDUPLICATE = NO` cho cả bàn và khu vực [DB]. Phạm vi kiểm trùng (toàn bảng hay trong khu vực) **chưa xác minh** (Q5).
- Thêm nhanh bàn kiểm trùng **toàn bảng**, không phân biệt hoa thường [CODE]. Đây là nguồn mặc định tạm cho Q5.

### 3.7 Cách tính giờ — `CACHTINHGIO` [DB + CODE]

- `CACHTINHGIO` là số nguyên ở cả `DBAN` và `DKHUVUC`. Dialog bàn có **3** lựa chọn (`THEOKHUVUC`, `THEOGIO`, `THEOBANGGIA`); dialog khu vực chỉ có **2** (`THEOGIO`, `THEOBANGGIA`) [CODE].
- Giá trị số của enum **không có trong mã đã xuất** (enum `CachTinhGiaGio` nằm ngoài export). DB: bàn `CACHTINHGIO = 0`, khu vực `= 1`. **Chưa suy ra được mã số** → Q8. Không đoán.

### 3.8 Chuyển bàn — cập nhật giá theo `SCONFIG.GiaMatHangKhiChuyenBan` [CODE + DB]

Mã `Chuyển bàn` (`CapNhatGia`, dòng ~636): đọc `DGIAMATHANGID` của khu vực **đích**, rồi cập nhật đơn giá của từng dòng hàng theo cột giá tương ứng, theo cấu hình:

| Giá trị | Ý nghĩa (theo mã) |
|---|---|
| `0` (DB hiện tại) | cập nhật khi **đơn giá hiện tại < giá mới** |
| `1` | luôn cập nhật |
| `2` | không cập nhật (thoát ngay) |

Bảng này thuộc luật bán hàng; spec chỉ ghi để biết cấu hình nào ảnh hưởng tới danh mục.

### 3.9 Xóa và tham chiếu

- **DB không có khóa ngoại** nào trên `DBAN` hay `DKHUVUC` (chỉ `NOT NULL` và `PRIMARY KEY`) [DB]. Chặn tham chiếu **nếu có** phải nằm ở mã.
- Mã đã xuất **không có** hàm chặn xóa trong `DBANAe` hay `DKHUVUCAe` [CODE].
- Bảng có cột `DKHUVUCID` hoặc `DBANID` (đếm từ `RDB$RELATION_FIELDS` [DB]): `DANH`, `DBAN`, `DBANGGIATHEOBAN`, `DBANGGIATHEOKHUVUC`, `DMATHANGMACDINH`, `DTKTHEOKHUVUC`, `TDATHANG`, `TDONHANG`, `TDONHANGGIO`, `TSUACHUA`. Chưa xác minh từng cột trỏ bàn hay khu vực (Q4).
- Trạng thái xóa mềm trong app: `STATUS = 0` bị lọc khỏi "Chọn bàn" và danh sách khu vực (`STATUS = 30`) [CODE]. Đây là **quy ước có sẵn**; chưa biết xóa bàn có đi qua `STATUS = 0` hay xóa cứng (Q3).
- Xóa khu vực: `DBAN.DKHUVUCID` tham chiếu khu vực; xóa khu vực đang có bàn là rủi ro lớn. Mặc định tạm ở 6.2 (Q4).

### 3.10 Cấu hình `SCONFIG` ảnh hưởng (giá trị DB thật [DB])

| Cấu hình | Giá trị DB | Ảnh hưởng | Nguồn |
|---|---|---|---|
| `CoTienGio` | 0 | Ẩn cách tính giờ, tiền mở bàn, trang bảng giá; ẩn IP công tắc (`Shared.CoTienGio`) | [CODE] |
| `ChoPhepDungNhieuBangGiaTrenBill` | 0 | Hiện trang "Bảng giá áp dụng" (cùng `CoTienGio`) | [CODE] |
| `SuDungGiaTheoGio` | 0 | Hiện trang "Mặt hàng theo giờ" của khu vực | [CODE] |
| `SuDungMatHangMacDinh` | 0 | Hiện trang "Mặt hàng mặc định" | [CODE] |
| `PhanQuyenTruyCapTheoKhuVuc` | 0 | Hiện trang "Truy cập" | [CODE] |
| `CoSuDungCongTacDienTu` | 0 | Điều kiện hiện IP công tắc (cùng `CoTienGio` và quyền admin) | [CODE] |
| `SuDungGia2` / `SuDungGia3` / `SuDungGia4` | 0 / 0 / 0 | Lọc combo "Giá mặt hàng" của khu vực | [CODE] |
| `DienGiaiGia2` / `3` / `4` | "Giá 2" / **"Giá 2"** / "Giá 4" | Nhãn giá. `DienGiaiGia3` trùng nhãn (Q10) | [DB] |
| `MauHoaDon` | `190263eb-…` ("Mẫu in bill 80mm") | Fallback mẫu hóa đơn khu vực | [DB + CODE] |
| `MauInChuyenBan` | `ef280054-…` ("Mẫu 80mm") | Mẫu khi chuyển bàn | [DB + CODE] |
| `GiaMatHangKhiChuyenBan` | 0 | Cách cập nhật giá khi chuyển bàn (3.8) | [DB + CODE] |
| `InHoaDonTheoKhuVuc`, `InPhaCheTheoKhuVuc` | 0 | In theo khu vực (ngoài phạm vi) | [DB] |
| `SuDungNhieuKho` | 0 | Có hiện "Kho hàng" của khu vực hay không [CHƯA XÁC MINH] | [DB] |
| `TuDongSinhMaVach`, `SuDungMaHang` | 0 | Không áp dụng cho bàn | [DB] |

### 3.11 Thêm nhanh bàn — luật riêng

- Chỉ sinh bàn cho khu vực có `STATUS = 30`.
- Số sinh ra: `BATDAU + số đệm 0 đến chiều dài`, với `TUSO .. DENSO`.
- Điều kiện **chạy**: `TUSO < DENSO` (xem 5.1, Q6).
- Toàn bộ kiểm tra trùng (trong DB và trong lần sinh) xảy ra **trước khi** ghi dòng đầu tiên. Nếu có lỗi → không ghi gì [CODE].
- Mỗi bàn được tạo `CACHTINHGIO = THEOKHUVUC`, `DONGIA = 0`, `TIENMOBAN = 0`, không có bảng giá, không có ghi chú.

---

## 4. Phân quyền

Chức năng: `SFUNCTION` **"Danh mục bàn khu vực"** — `194c969a-962f-47c8-b808-0330e2a60350`, nhóm **"Danh mục"** [DB]. `STABLEDESC` `DBAN` có `SFUNCTIONID` trỏ tới chức năng này [DB]; `DKHUVUC` **không có** `SFUNCTIONID` (`NULL`) [DB] → xem Q13.

Bit quyền theo khuôn (§2 của Danh mục mặt hàng): **Xem 0x10 (16)**, **Sửa 0x20 (32)**, **Thêm 0x40 (64)**, **Xóa 0x80 (128)**. Giá trị dưới đây đọc từ `SGROUPROLE` [DB] (`MODE`):

| Nhóm | `MODE` | Xem | Sửa | Thêm | Xóa | Ghi chú |
|---|---|---|---|---|---|---|
| Quản lý | 240 | ✔ | ✔ | ✔ | ✔ | |
| Kế toán | 240 | ✔ | ✔ | ✔ | ✔ | |
| Thu ngân | 0 | — | — | — | — | không thấy màn (có dòng nhưng `MODE = 0`) |
| Phục vụ | 0 | — | — | — | — | không thấy màn |
| Thủ kho | (không có dòng) | — | — | — | — | không có quyền [DB] |

- Chưa biết thu ngân/phục vụ có thấy màn ở mức chỉ-xem hay không: `MODE = 0` nghĩa là không có bit nào. Giữ nguyên.
- **Hai hành động riêng:** "Thêm nhanh bàn" và "Thiết kế giao diện" **không có** `SFUNCTION` riêng trong DB (tên tìm theo "bàn" và "khu vực" chỉ ra một chức năng) [DB]. Mặc định tạm: gắn vào quyền **Thêm** của "Danh mục bàn khu vực" (Q13).
- Quyền theo khu vực: `PhanQuyenTruyCapTheoKhuVuc` tắt (DB) → không lọc theo người dùng. Khi bật: trang "Truy cập" ghi `DTKTHEOKHUVUC` (3.3).

---

## 5. Chỗ khác app gốc — **chỉ đề xuất, KHÔNG tự quyết** (Chiến quyết)

| # | Điểm | Đề xuất | Căn cứ |
|---|---|---|---|
| 5.1 | `Thêm nhanh bàn` dùng `TUSO < DENSO` nên `TUSO = DENSO` **không sinh bàn nào và không báo lỗi** | Báo lỗi "Khoảng số không hợp lệ" hoặc sinh đúng 1 bàn. Chọn một | [CODE] dòng `if (TuSo < DenSo)` |
| 5.2 | Lưu bàn/khu vực và các bảng liên quan (bảng giá, truy cập, mặt hàng mặc định) không cùng transaction | Gom vào một transaction để tránh ghi dở | [CODE] `mapper_AfterSave` |
| 5.3 | `DBANAe` **không** kiểm bảng giá bắt buộc khi chọn "theo bảng giá" (`DKHUVUCAe` có kiểm) | Thêm kiểm như khu vực | [CODE] |
| 5.4 | `grTruyCap_CellMouseClick` của khu vực gọi `RaiseChangedEvent(grBangGia)` (copy-paste, lưới sai) | Sửa thành `grTruyCap` | [CODE] |
| 5.5 | Combo mẫu hóa đơn lọc theo `SFORM` "Hóa đơn bán hàng", còn bill bàn là "Hóa đơn nhà hàng" | Xác nhận đúng `SFORM` (Q11) | [CODE] |
| 5.6 | Thùng rác cho bàn / khu vực | Theo quyết định ở Q3 — không mặc định | [CODE] `STATUS = 0` |
| 5.7 | Dữ liệu `DienGiaiGia3 = "Giá 2"` | Không tự sửa DB; Chiến xác nhận giá trị đúng | [DB] |
| 5.8 | Tách "Thiết kế" (34 cột sơ đồ của bàn, 7 cột thiết kế của khu vực) khỏi danh mục | Đã chọn ở phạm vi: **không** sửa các cột này từ danh mục | [DB] |

---

## 6. Câu hỏi còn mở

### 6.1 Đã trả lời bằng app gốc [APP]

Chưa có: vòng này **không chạy** app gốc. Mục này để trống cho đến khi có kết quả thử nghiệm.

### 6.2 Còn mở — kèm mặc định tạm (Muse dùng mặc định cho đến khi Chiến chốt)

| # | Câu hỏi | Mặc định tạm | Cách kiểm |
|---|---|---|---|
| Q1 | Cột lưới bàn thật là gì? `SGRID` không có cấu hình cho `DBAN` | Dùng cột đề xuất ở 1.1a | Chụp màn hình app gốc (1 ảnh) |
| Q2 | Cây khu vực: có "Tất cả" và "Chưa thiết lập" không? Điểm mở **+ / ✎** nằm ở đâu? (`DKHUVUC` là `TABLETYPE = 0`, phẳng) | Không có "Chưa thiết lập" (bàn bắt buộc có khu vực); có "Tất cả" như khuôn | Mở app gốc, xem panel trái |
| Q3 | Xóa bàn / khu vực: xóa cứng hay xóa mềm `STATUS = 0`? Có thùng rác không? | **Xóa cứng** khi không tham chiếu; **chặn** khi tham chiếu (Q4). Không thùng rác (theo quyết định cho mặt hàng) | Xóa bàn trống và bàn đã có đơn trên bản sao |
| Q4 | Chặn tham chiếu: bảng nào, điều kiện nào? (`TDONHANG.DBANID` đã thanh toán có chặn không? `DBANGGIATHEOBAN`, `DTKTHEOKHUVUC` xóa theo bàn/khu vực?) | Chặn khi còn `TDONHANG` **chưa thanh toán** (`DATHANHTOAN = 0`) trỏ tới bàn, hoặc bàn đang có `TDONHANGID`. Khu vực chặn khi còn bàn. Bảng giá/truy cập gắn kèm bị xóa theo | Xóa bàn có đơn chưa thanh toán và đã thanh toán trên bản sao |
| Q5 | Trùng tên: phạm vi (toàn bảng hay trong khu vực)? Hoa thường? Khoảng trắng? | Toàn bảng, không phân biệt hoa thường, cắt khoảng trắng đầu cuối (như Thêm nhanh) | Thêm "B01" ở khu vực khác; "b01"; "B01 " |
| Q6 | `Thêm nhanh`: `TUSO = DENSO` làm gì? (5.1) | Báo lỗi "Khoảng số không hợp lệ" | Thử TUSO = DENSO trên bản sao |
| Q7 | Có mã bàn/khu vực không? (DB không có cột `CODE`) | **Không có mã**; khóa theo tên | — |
| Q8 | Mã số của `CachTinhGiaGio` (`THEOGIO`, `THEOBANGGIA`, `THEOKHUVUC`) | **Không đoán.** Giữ đúng giá trị DB hiện có; Muse không ghi số mới cho đến khi có enum | Đọc enum từ mã nguồn `No1Lib` (ngoài export) hoặc đối chiếu bàn tạo bằng Thêm nhanh (`THEOKHUVUC`) |
| Q9 | Giá `DGIAMATHANG = 0` ("Hiển thị để lựa chọn") trên bill nghĩa là gì? | Coi như `GIABAN` (như Chuyển bàn) | Xem `Hóa đơn nhà hàng` dòng 1190 / 3721 và thử trên bản sao |
| Q10 | `DienGiaiGia3 = "Giá 2"` là lỗi dữ liệu hay cố ý? | Giữ nguyên DB; hiển thị đúng như DB | Chiến xác nhận |
| Q11 | Mẫu hóa đơn theo khu vực: combo nên lọc theo `SFORM` nào? (5.5) | Giữ đúng như app (lọc `5f029a5b`) | So sánh với bill bàn trên bản sao |
| Q12 | Các cột `ANH`, `NUMCHAIRS`, `ICON`… của bàn: có phải là dữ liệu của "Thiết kế giao diện bàn"? Có cần hiển thị? | Không hiển thị; giữ giá trị khi lưu (3.1) | Đối chiếu với trình thiết kế |
| Q13 | Quyền của "Thêm nhanh bàn" và "Thiết kế giao diện" (không có `SFUNCTION` riêng); quyền của `DKHUVUC` (không có `SFUNCTIONID`) | Gắn vào quyền Thêm/Sửa của "Danh mục bàn khu vực" | Đăng nhập tài khoản Kế toán/Thu ngân trên bản sao |
| Q14 | Thu ngân và Phục vụ có `MODE = 0`: có thấy menu không? Thủ kho không có dòng | Giữ nguyên như DB | Đăng nhập từng nhóm trên bản sao |
| Q15 | Trang "Bảng giá áp dụng" và "Truy cập" hiện đúng điều kiện không? Thay đổi `SCONFIG` có hiện ngay không? | Hiện theo điều kiện 1.3 / 3.10 | Bật từng cấu hình trên bản sao |
| Q16 | "Thêm excel", "Xuất excel", "Import", "In" cho bàn có cần không? | Không làm ở phiên đầu; nút ẩn | Chiến quyết |
| Q17 | Điều kiện hiện IP công tắc: `Shared.CoTienGio` có phải đọc trực tiếp `SCONFIG.CoTienGio` không? | Theo `CoTienGio` và `CoSuDungCongTacDienTu` và admin (3.10) | Xác minh nguồn `Shared.CoTienGio` |
| Q18 | Điểm mở "Thêm nhanh bàn" nằm ở đâu trên màn? | Nút trên thanh của màn danh mục bàn | Mở app gốc |
| Q19 | Thứ tự menu: "Danh mục bàn khu vực" có **ngay sau** "Danh mục mặt hàng" trong HOẠT ĐỘNG không? | Theo đề bài: ngay sau | Đọc `SMENU` (chưa đọc trong vòng này) |

---

## 7. Tiêu chí nghiệm thu (rút gọn — Muse kiểm trên bản sao DB)

1. Mở màn từ menu HOẠT ĐỘNG; cây trái hiện khu vực `STATUS = 30`; lưới phải hiện bàn của khu vực đang chọn. Có DB bản sao với 1 khu vực, 1 bàn → thấy "bàn 1" dưới "Lầu 1".
2. Thêm bàn: bắt buộc chọn khu vực; tên trống → không lưu, báo lỗi theo khuôn; tên trùng (không phân biệt hoa thường) → không lưu.
3. Sửa bàn: các cột thiết kế và runtime (`POSLEFT`, `WIDTH`, `TRANGTHAI`, `TDONHANGID`…) **không đổi** sau khi lưu (so sánh bản ghi trước và sau).
4. Thêm khu vực: tên trùng → không lưu. `STEMPLATEID` để trống → khi lên bill dùng `MauHoaDon` (kiểm đường dẫn 3.4).
5. Thêm nhanh: `BATDAU="B", TUSO=1, DENSO=3`, chiều dài 2 → sinh `B01, B02, B03`, `CACHTINHGIO = THEOKHUVUC`; có một tên đã tồn tại → không sinh dòng nào.
6. Thêm nhanh với `TUSO = DENSO` → hành vi theo Q6.
7. Quyền: Quản lý và Kế toán thấy và sửa được; Phục vụ và Thu ngân không thấy menu (`MODE = 0`); Thủ kho không thấy.
8. Cấu hình `CoTienGio = 0` (DB hiện tại) → không hiện cách tính giờ, tiền mở bàn, trang bảng giá; lưu bàn không ghi `TIENMOBAN` khác 0.
9. Xóa bàn đang có đơn chưa thanh toán → bị chặn (theo Q4 mặc định); thông báo chưa cố định (Q4).
10. Không có SQL trong form/View; màu lấy từ `ThemeManager`; cỡ chữ không cố định.

## 8. Gợi ý implement cho Muse (không bắt buộc)

- Repository mới `RoomRepository`/`AreaRepository` trong `Kazuko_FnB.Data`; luật kiểm trong `Kazuko_FnB.Services`; dialog trong `Kazuko_FnB.UI/Dialogs` theo mẫu `ProductEditDialog`.
- Đọc danh sách: `WHERE STATUS = 30` cho cả hai bảng (theo `Chọn bàn`).
- Ghi: một transaction cho bàn + bảng giá + truy cập (5.2).
- Cột `CACHTINHGIO`: đọc/ghi số nguyên qua một hằng số, **không** hard-code số cho đến khi có enum (Q8).
- Thêm nhanh: tách phần sinh tên và kiểm tra (thuần, có test) khỏi phần ghi DB.
- Sau khi ghi bàn: gọi tương đương `Config.SetLastUpdate(Tables.DBAN)` để màn bán hàng nạp lại.

---

## Phụ lục A — Ảnh chụp app gốc

Chưa có. Vòng này không chạy app gốc và không chụp ảnh. Khi làm Q1, Q2, Q4, Q6, Q9, Q13 và Q18, nên chụp tối đa ~8 ảnh vào `docs/specs/img/danh-muc-ban-khu-vuc/`.
