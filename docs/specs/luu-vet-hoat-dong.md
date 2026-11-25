# Spec (delta): Lưu vết hoạt động (TLUUVET)

- Trạng thái: **ĐÃ DUYỆT** (Chiến duyệt 25/11/2026, Q1 giữ `9876`, Q9 hiện tên phân loại; các câu khác theo mặc định trong bảng §5). Chưa code. Rà lại 25/11 trên `main` @ `09d9ab6`: đã tìm ra giá trị enum `LoaiLuuVet`, 9 cột lưới phải và cách ghi từng cột.
- Ngày: 2026-11-24. Base: `main` @ `d0058a6` (đã `git fetch origin main`)
- Dự kiến là **chức năng thử đầu tiên** của [`workflow-agy.md`](../workflow-agy.md): phạm vi = `ActivityLogger` + màn xem + selftest; chưa có màn nào gọi `Track` (các màn bán hàng chưa code).
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md). Spec này **chỉ ghi phần khác khuôn**; chỗ không nói = như khuôn.
- Menu: **HOẠT ĐỘNG > Lưu vết hoạt động** (`SMENU` `b022d2c4-d61c-479a-9ebc-9c3eedfc18df`, `SORTORDER = ZZZ017`, `OPERATION = 4`, không `MENUCODE`, `SFORMID` `ac206bb6-0236-48a6-b558-b674e015c21c`, `SFUNCTIONID` `863ecff5-3010-400c-930e-549e4f8d1f96` "Lưu vết hoạt động", nhóm "Bán hàng", `NOTE = View`). Là **form tùy biến** `LuuVetHoatDong` (không có `STABLEDESC` riêng ở menu), màn **chỉ xem** + 1 nút xóa toàn bộ ẩn.
- Bảng `TLUUVET`; hiện **0 bản ghi** trong DB chép. Lưu vết do **các màn khác ghi** (`Track()`); spec này gồm màn xem **và** hợp đồng ghi lưu vết cho các spec khác.

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`): cấu trúc `TLUUVET`, `SCONFIG.KichHoatLuuVetHoatDong = 30`, `SFUNCTION`.
- **[CODE]** `POS_TAP_SRC_EXPORT/SFORM/Lưu vết hoạt động__ac206bb6/` (`code.cs`, `layout.xml`), `Xác nhận xóa lưu vết__c73a5edd/`; tài liệu sẵn có [`pos-tap-nghiep-vu-quan-tri-tien-ich.md`](../pos-tap-nghiep-vu-quan-tri-tien-ich.md) §5 và `docs/deep/quan-tri.md` (b.6) — đã đọc, không chép lại.
- **[APP]** đã chụp 24/11/2026 (bố cục + cột nhìn thấy của lưới phải); DB không có lưu vết nên chưa có dòng mẫu. Q8 giải quyết phần lớn.
- **[DB] đã kiểm 25/11:** enum `LoaiLuuVet` và các hàm `Track` nằm trong **mã dùng chung lưu trong `DATA.fdb`** (không có trong bản export `SFORM`); 9 cột lưới phải giải mã từ `ViewConfig` của `grMain` trong `layout.xml`, đổi `COLID` sang tên qua `SCOLUMN`. `TLUUVET` có 0 dòng.
- **[CHƯA XÁC MINH]**: cột Phân loại gốc hiện số hay tên (Q9).

## 1. Bố cục UI (khác khuôn)

Một tab "Lưu vết hoạt động" gồm **bộ lọc trên cùng** và **hai lưới**:

- **Bộ lọc:** *Từ ngày – Đến ngày* (`dtNgay`, mặc định **hôm nay → hôm nay** theo ngày DB), *Tài khoản* (`lueNhanVien`, lookup `SUSER`), *Số HĐ* (`txtSoHoaDon`, Enter = tải), nút **Tải dữ liệu** (`btnRefresh`), ô **Lọc dữ liệu** (`txtLoc`, lọc lưới phải theo chuỗi gõ), nút **Xóa lưu vết** (`btnXoaLuuVet`, **ẩn** — xem §3.4). Đổi ngày hoặc tài khoản **tải lại ngay**.
- **Lưới trái "HÓA ĐƠN"** (`grHoaDon`): cột **Hóa đơn** (`NAME`), **Bàn** (`BAN`), **Trạng thái** (`TRANGTHAI`). Dòng đỏ = bill đã **hủy** (`LOAI = 1`), xanh dương = bill **đang sử dụng** (`LOAI = 2`), mặc định = đã kết thúc (`LOAI = 0`).
- **Lưới phải "CHI TIẾT THAO TÁC"** (`grMain`, chỉ đọc, `GIO` hiển thị `HH:mm:ss`, mọi cột **không sắp xếp**). **Đúng 9 cột** [DB `ViewConfig` + `SCOLUMN`], theo thứ tự nhìn thấy trên ảnh 24/11: **Ngày** (`NGAY`), **Giờ** (`GIO`), **Số đơn hàng** (`SODONHANG`, hiện số phiếu, §2.2), **Nội dung** (`NOTE`, caption đổi từ "Ghi chú"; cột rộng nhất), **Tài khoản** (`TAIKHOAN`), **Thiết bị** (`THIETBI`), **Bàn** (`BAN`), **Chức năng** (`CHUCNANG`), **Phân loại** (`PHANLOAI`, cột bị khuất bên phải trên ảnh; cách hiện: Q9). **Không** hiện Tên hàng, Số lượng, Đơn giá, Thành tiền dù bảng có các cột đó.
- **[APP] vị trí điều khiển thật:** *Từ/Đến*, *Số HĐ* và nút **Tải dữ liệu** nằm ở khung **"HÓA ĐƠN"** bên trái (kèm mũi tên ▼ cạnh ô *Đến*); ô **Tài khoản** và **Lọc dữ liệu** nằm ở đầu khung **"CHI TIẾT THAO TÁC"** bên phải. Nút Xóa lưu vết không hiện (đúng ghi chú ẩn).
- Chưa chọn hóa đơn nào ở lưới trái → lưới phải **trống** (`0 = 1`).

## 2. Luật và luồng (khác khuôn)

### 2.1 Nạp danh sách hóa đơn [CODE `LoadData`]

`UNION ALL` hai nguồn, sắp theo `NAME`:
Khoảng ngày: `NGAY BETWEEN Từ AND Đến`, **Đến tính trọn ngày** (quy ước chung, như `quy-tien.md` §4.7).
1. `TDONHANG` có `LOAI = 0` (bill nhà hàng) và `NGAY` trong khoảng; `LOAI` hiển thị = **0** nếu `DATHANHTOAN = 30` (trạng thái "Kết thúc") ngược lại **2** ("Đang sử dụng"); `BAN` = tên bàn theo `DBANID`.
2. `TDONHANGHUY` (`TDONHANGID` làm khóa) với `NGAY` trong khoảng; `LOAI = 1`; **trạng thái hiển thị = `LYDOHUY`** (lý do hủy), `BAN` = cột `BAN` đã lưu.
- Nhập **Số HĐ**: thêm điều kiện `OR NAME = '<số>'` vào **cả hai** nhánh → hóa đơn đó hiện **dù ngoài khoảng ngày** (nhánh gốc dùng ghép chuỗi có escape `'` → `''`; bản mới dùng tham số, **không nối chuỗi** — P1).
- Không lọc theo tài khoản ở lưới trái (chỉ ảnh hưởng lưới phải).

### 2.2 Nạp lưu vết của hóa đơn đã chọn [CODE `grMain_CustomLoadData`, `GridView_OnCustomFilter`]

- Chọn một dòng ở lưới trái (đổi dòng = tải lại): lấy `TLUUVET` với `SODONHANG = <ID hóa đơn>` (`TDONHANGID`).
- Chọn tài khoản → thêm `TAIKHOAN = <tên>` (cột `TAIKHOAN` của `TLUUVET` là chuỗi tên, không phải ID). Gốc so với tên hiển thị của ô (`lueNhanVien.DisplayText`). **Chiến chốt 25/11 (A1):** v1 ghi và lọc theo **tên đăng nhập** (`SUSER.USERNAME`, ví dụ `Admin`), ô lọc hiện tên đăng nhập; khi có dữ liệu cũ do app gốc ghi thì lọc theo cả tên đăng nhập lẫn tên hiển thị.
- Cột "Số đơn hàng" hiển thị = `COALESCE(TDONHANG.NAME, TDONHANGHUY.NAME)` của `SODONHANG` (bill đã hủy vẫn ra tên số phiếu).
- Sắp theo **`CAST(NGAY AS DATE)`, `GIO`** (tăng dần, theo thời điểm xảy ra).
- Ô **Lọc dữ liệu** lọc thêm ở phía lưới (chuỗi gõ khớp các cột, chi tiết cách khớp do khung lưới).

### 2.3 Tô màu theo phân loại [CODE `GridView_CellFormatting`] (lỗi bị nuốt, im lặng)

| Phân loại (`PHANLOAI`) | Hiển thị |
|---|---|
| Hủy bill | **đỏ + đậm** |
| Chuyển bàn | xanh dương |
| Gộp bàn | **xanh lá + đậm** |
| Xóa món / Giảm món / Đổi giá bán / Đặt giảm giá mặt hàng | đỏ |
| Loại còn lại (mở bàn, thêm/tăng món, đặt SL, đóng bàn, in bill, in chế biến, chuyển mặt hàng, trả đồ, khác…) | mặc định |

Màu/đậm lấy từ **token** (đỏ cảnh báo, xanh thông tin, xanh lá thành công), không hard-code.

### 2.4 Cảnh báo khi tính năng tắt [CODE `Load`]

`KichHoatLuuVetHoatDong = 0` (hiện [DB] = **30**, bật): mở màn vẫn được nhưng hiện cảnh báo **"Chức năng lưu vết hoạt động hiện không được kích hoạt"** + dòng **"Bạn có thể kích hoạt trong menu 'Quản trị | Cấu hình toàn hệ thống'"** (hai dòng, nguyên văn).

### 2.5 Hợp đồng ghi lưu vết cho các màn khác [CODE, tổng hợp `deep/quan-tri.md` (b.6)]

Cách ghi từng cột, theo hàm `Track` của form Hóa đơn nhà hàng [DB, mã dùng chung]:

| Cột | Giá trị |
|---|---|
| `GIO` | thời điểm hiện tại của **máy chủ DB** |
| `NGAY` | phần ngày của `GIO` (không phải ngày của bill) |
| `SODONHANG` | ID hóa đơn (`TDONHANG.ID`) |
| `TAIKHOAN` | **tên đăng nhập** của người đang dùng (không phải ID) |
| `THIETBI` | tên máy (`Environment.MachineName`) |
| `PHANLOAI` | số theo bảng dưới |
| `BAN` | tên bàn đang hiện; trống thì đọc tên bàn của bill |
| `CHUCNANG` | "Sử dụng dịch vụ", "Quản lý bán hàng", "Điều chỉnh hóa đơn", "POS cảm ứng" (gốc còn "Máy tính bảng", v1 chưa làm) |
| `NOTE` | nội dung thao tác, chuỗi tự do |
| `TENHANG`, `SOLUONG`, `DONGIA`, `THANHTIEN` | khi thao tác gắn một mặt hàng; không thì `''` và 0 |

- Chỉ ghi khi `KichHoatLuuVetHoatDong = 30`.
- Chế độ Quản lý bán hàng / Điều chỉnh hóa đơn: gốc gom vào danh sách tạm rồi ghi khi cập nhật bill (spec đó quy định); `ActivityLogger` phải ghi được nhiều dòng trong một transaction.
- Gốc có **~101 điểm gọi** `Track()`. v1 chỉ cần dịch vụ ghi dùng chung `ActivityLogger.Track(chucNang, phanLoai, soDonHangId, ban, noiDung, [tenHang, sl, dongia, thanhtien])`; các spec nghiệp vụ gọi vào đó. Danh sách điểm gọi ở `deep/quan-tri.md` (b.6).

**Giá trị `PHANLOAI`** — dùng **đúng số của app gốc** (enum `LoaiLuuVet`), để dữ liệu cũ và mới đọc như nhau (thay cho Q2 cũ):

| Số | Tên trong code | Ý nghĩa | Số | Tên trong code | Ý nghĩa |
|---|---|---|---|---|---|
| 0 | `MoBan` | Mở bàn | 9 | `TangMon` | Tăng món |
| 1 | `DongBan` | Đóng bàn | 10 | `DatSoLuong` | Đặt số lượng |
| 2 | `InBill` | In bill | 11 | `DoiGiaBan` | Đổi giá bán |
| 3 | `InCheBien` | In chế biến | 12 | `DatGiamGiaMatHang` | Đặt giảm giá mặt hàng |
| 4 | `ThemMon` | Thêm món | 13 | `HuyBill` | Hủy bill |
| 5 | `XoaMon` | Xóa món | 14 | `Khac` | Khác |
| 6 | `GiamMon` | Giảm món | 15 | `ChuyenMatHang` | Chuyển mặt hàng |
| 7 | `ChuyenBan` | Chuyển bàn | 16 | `TraDo` | Trả đồ |
| 8 | `GopBan` | Gộp bàn | | | |

Số ngoài 0–16 khi đọc: coi như "Khác" (không màu), không báo lỗi. Lưu ý `PHANLOAI` mặc định 0 = **Mở bàn**: dòng ghi thiếu phân loại (form bán lẻ gốc không ghi cột này) sẽ hiện như Mở bàn.

## 3. Phần khác app gốc / chỗ dễ sai

### 3.1 Dữ liệu [DB]

`TLUUVET`: `ID`, `NOTE`, `STATUS`, `USERMODIFIEDID`, `TIMEMODIFIED`, `TIMECREATED`, `NGAY` (TIMESTAMP), `USERCREATEDID`, `GIO` (TIMESTAMP), `SODONHANG` (VARCHAR 36), `TAIKHOAN` (VARCHAR 36 — thực chất chứa tên), `THIETBI`, `PHANLOAI` (INTEGER, DEFAULT 0), `BAN`, `CHUCNANG` (VARCHAR 36 — thực chất chuỗi tên chức năng), `SOLUONG`, `DONGIA`, `THANHTIEN` (`DECIMAL(18,2)`), `TENHANG`. Không đổi cấu trúc DB. `TAIKHOAN`/`CHUCNANG` dài tối đa **36 ký tự** → khi ghi phải cắt ngắn tên dài hơn (gốc: lỗi/ cắt tùy driver; Q5).

### 3.2 Tên bàn hóa đơn đã hủy

Danh sách hóa đơn hủy lấy `BAN` **lưu sẵn** trong `TDONHANGHUY` (bản chụp tại lúc hủy), không join lại `DBAN`.

### 3.3 Quyền

Quyền xem màn = `SFUNCTION` `863ecff5-…` "Lưu vết hoạt động". Mặc định tạm cho tài khoản không phải admin: **không cấp** (Q5 spec Sử dụng dịch vụ). Không có quyền tách riêng cho nút xóa (§3.4).

### 3.4 Xóa toàn bộ lưu vết [CODE `btnXoaLuuVet_Click`, `XacNhanXoaLuuVet`]

- Nút `btnXoaLuuVet` **chỉ hiện khi**: `DbConfig.IsAdmin` **và** người dùng **giữ Ctrl+Shift lúc mở màn** (không phải lúc bấm). Cố tình ẩn.
- Bấm → hộp **"XÁC NHẬN XÓA LƯU VẾT"** (340×115, cố định): ô **Mật khẩu**, nút **Chấp nhận** (tắt cho tới khi nhập đúng), **Hủy bỏ**.
- **Mật khẩu cứng `"9876"` trong code** (so chuỗi thô). Đúng → `DELETE FROM TLUUVET` **toàn bảng, không điều kiện, không sao lưu** → tải lại.
- *(Gốc còn `DoAutoTest()` tự gọi xóa — thuộc chế độ tự kiểm thử, không đưa vào v1.)*

### 3.5 Xóa hóa đơn kèm lưu vết

Lưu vết **không bị xóa** khi bill bị hủy/xóa (khóa mềm bằng `SODONHANG`, không FK) → còn xem được qua nhánh `TDONHANGHUY`. Với bill xóa hẳn mà không qua hủy (hiếm), dòng lưu vết thành **mồ côi** (không thấy ở lưới trái, vẫn tồn tại trong DB).

## 4. Phần đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Truy vấn dùng **tham số** cho Số HĐ, không nối chuỗi.
- **P2** Mật khẩu xóa lưu vết: giữ `"9876"` cứng như gốc **hoặc** đưa vào cấu hình / yêu cầu mật khẩu admin hiện tại. *Mặc định tạm: giữ nguyên `9876` (đã ghi chú trong `quan-tri-tien-ich.md`: "không tự đổi lặng lẽ — Chiến chốt").*
- **P3** Tắt nút "Xóa lưu vết" ẩn bằng phím giữ lúc mở: giữ như gốc (admin + Ctrl+Shift).
- **P4** Phân trang / giới hạn dòng cho lưới phải (lưu vết một bill thường vài chục dòng, không cần); giữ như gốc.
- **P5** Tên cột/giờ: hiển thị `GIO` 24h `HH:mm:ss`; ngày theo `dd/MM/yyyy` như app.

## 5. Câu hỏi chờ Chiến quyết

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | Mật khẩu xóa lưu vết (P2) | **Đã chốt 25/11:** giữ `9876` |
| Q2 | ~~Enum `PHANLOAI` tự định nghĩa?~~ **Đã giải quyết 25/11:** dùng đúng số gốc (§2.5) | — |
| Q3 | Có đưa `DoAutoTest` (tự xóa lưu vết khi test) không? | Không |
| Q4 | Có giữ cảnh báo khi cờ tắt (vẫn mở màn)? | Giữ nguyên |
| Q5 | `TAIKHOAN`/`CHUCNANG` dài ≤ 36: cắt ngắn khi ghi hay báo lỗi? | Cắt ngắn, ghi cảnh báo trong log |
| Q6 | Lưu vết có cần xuất Excel / in (gốc không có nút)? | Không |
| Q7 | Dòng mồ côi (§3.5): có cần màn dọn? | Không |
| Q8 | ~~Cột lưới phải~~ **Đã giải quyết 25/11:** đúng 9 cột, cột thứ 9 là Phân loại (§1) | — |
| Q10 | (A1) Lọc tài khoản theo tên đăng nhập hay tên hiển thị? | **Đã chốt 25/11:** tên đăng nhập (§2.2) |
| Q11 | (A2) Cột cố định rộng để dòng chữ đậm không bị cắt, đổi lại Chức năng/Phân loại phải cuộn ngang, Nội dung hẹp đi | **Đã chốt 25/11:** giữ như vậy |
| Q9 | Cột Phân loại: gốc nhiều khả năng hiện **số** (0–16) vì không thấy chỗ đổi sang tên. Hiện tên ("Hủy bill", "Gộp bàn"…) cho dễ đọc? | **Đã chốt 25/11:** hiện tên theo cột "Ý nghĩa" bảng §2.5 (khác gốc, chỉ phần hiển thị; DB vẫn lưu số) |

## 6. Quyết định đã chốt (áp, không hỏi lại)

Xóa cứng cả app (không thùng rác; riêng "Xóa lưu vết" là xóa toàn bảng như gốc); tiền `NUMERIC(18,2)`/`decimal`; F&B thuần COFFEE + POS; không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB; không thêm tính năng ngoài gốc.

## 7. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest tự tạo bill, bill hủy và lưu vết mẫu)

- [ ] Mở màn: mặc định hôm nay → hôm nay; lưới trái liệt kê bill `LOAI = 0` trong ngày + bill hủy cùng khoảng, sắp theo số phiếu; dòng đỏ (hủy), xanh dương (đang dùng), mặc định (kết thúc).
- [ ] Nhập Số HĐ: hóa đơn ngoài khoảng ngày vẫn hiện (cả bill thường lẫn bill hủy); ký tự `'` trong số HĐ không gây lỗi (P1).
- [ ] Chọn bill: lưới phải chỉ có dòng `SODONHANG` = bill; sắp theo ngày/giờ; chưa chọn → trống; chọn tài khoản → chỉ thao tác của tài khoản đó; tên số phiếu hiện đúng cả với bill đã hủy.
- [ ] Tô màu đúng bảng §2.3 (6 loại đã kiểm); lỗi dữ liệu một dòng không làm sập lưới.
- [ ] `KichHoatLuuVetHoatDong = 0`: hiện đúng cảnh báo 2 dòng; vẫn mở được màn.
- [ ] Nút Xóa lưu vết ẩn mặc định; hiện chỉ khi admin + Ctrl+Shift lúc mở; sai mật khẩu → nút Chấp nhận tắt; đúng → xóa toàn bảng và tải lại; không admin không thể làm được dù giữ phím.
- [ ] `ActivityLogger.Track` ghi đúng các trường §2.5 (`NGAY` = ngày của giờ DB, `TAIKHOAN` = tên đăng nhập, `THIETBI` = tên máy), `PHANLOAI` đúng số gốc 0–16, **không ghi** khi cờ ≠ 30; tên dài > 36 xử lý theo Q5; ghi nhiều dòng trong một transaction được.
- [ ] Lưới phải đúng 9 cột theo thứ tự §1; không có cột Tên hàng/Số lượng/Đơn giá/Thành tiền; `PHANLOAI` ngoài 0–16 không làm lỗi.
- [ ] Không SQL trong View; màu từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc).

## 8. Gợi ý implement (không bắt buộc)

`ActivityLogRepository` (Data: truy vấn hai nhánh + lưu vết theo bill, `Insert`, `DeleteAll`), `ActivityLogger` (Services: kiểm cờ, cắt chuỗi, enum `ActivityKind` mang đúng số gốc §2.5), `ActivityLogView` (UI: bộ lọc + 2 lưới, màu theo token). **Làm `ActivityLogger` trước** để các spec Sử dụng dịch vụ / Quản lý bán hàng / Điều chỉnh hóa đơn gọi vào; màn xem làm sau.
