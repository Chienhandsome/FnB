# Spec (delta): Danh mục hóa đơn hủy (TDONHANGHUY)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 25/11/2026 theo [`../workflow-agy.md`](../workflow-agy.md) §10.1, loại nhẹ; Chiến yêu cầu chạy workflow từng spec). Mọi mặc định ở §4 được áp. Đã đối chiếu lại `code.cs`, cấu trúc 2 bảng và quyền trên bản sao `DATA.fdb` ngày 25/11.
- Ngày: 2026-11-24; sửa theo review [issue #5](https://github.com/Chienhandsome/FnB/issues/5) ngày 25/11/2026 (đối chiếu lại tại `main` @ `954f133`)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md) (lọc + lưới + chi tiết, thanh nút trên). Spec này **chỉ ghi phần khác**; chỗ không nói = như khuôn.
- Menu: **HOẠT ĐỘNG > Danh mục hóa đơn hủy** (`SMENU` `7d169f76-9897-4111-ba6d-f3cbd4c4e3f7`, `SORTORDER = ZZZ024`, `OPERATION = 4`, `MENUCODE = DanhMucHoaDonHuy`, `SFORMID` `a52478cf-2d23-4ff0-8b63-e098e5ce6829`, `SFUNCTIONID` `f1a22c9e-774f-4dce-ad95-d1c392a9ba51` "Danh mục hóa đơn hủy", nhóm "Bán hàng", `NOTE = View`). Là **form tùy biến** `DanhSachBillHuy` (hai lưới master–detail **chỉ đọc**, không thêm/sửa/xóa).
- Bảng chính `TDONHANGHUY` (`STABLEDESC` `ee531e78-c1b2-4801-ba74-5e43a630fd65`, `TABLETYPE = 3`), chi tiết `TDONHANGHUYCHITIET` (`STABLEDESC` `9dfabf38-202e-435d-a87a-33e551fd2e4d`, `TABLETYPE = 4`; khóa ngoại mềm `TDONHANGHUYID`). Hiện **0 bản ghi** cả hai trong DB chép.
- Dữ liệu **do màn khác ghi** khi hủy bill (Sử dụng dịch vụ — hủy bill trên bàn; Quản lý bán hàng — hủy phiếu): ghi `TDONHANGHUY` + `TDONHANGHUYCHITIET` rồi xóa cứng bill gốc. Spec này chỉ **xem lại** bản lưu đó.

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`): cấu trúc 2 bảng, `STABLEDESC`, `SMENU`, `SFUNCTION`.
- **[CODE]** `POS_TAP_SRC_EXPORT/SFORM/Danh sách bill hủy__a52478cf/` (`code.cs`; `layout.xml` = nội dung `SFORM.AELAYOUT`); cách ghi bản lưu hủy: [`quan-ly-ban-hang-dieu-chinh-hoa-don.md`](quan-ly-ban-hang-dieu-chinh-hoa-don.md) §3.2 (đã sửa theo review) và [`su-dung-dich-vu.md`](su-dung-dich-vu.md) §3.8.
- **[LAYOUT]** `layout.xml` cho đủ **22 cột lưới master** và `FilterManager`; `ViewConfig` nhị phân của lưới chi tiết giải mã ra **7 cột** (đối chiếu tên với `SCOLUMN`).
- **[APP]** ảnh 24/11 (DB không có bill hủy) có thanh trên và phần đầu lưới master. **Ảnh lệch `layout.xml` ở 2 điểm:** 3 cột đầu là Ngày, Số phiếu, Ghi chú; và giữa "Tiền giảm giá" với "Thanh toán bởi" **không thấy "Phí vận chuyển"**. Nguyên nhân [CHƯA XÁC MINH] (có thể khung lưới áp cấu hình cột đã lưu riêng). Xem Q8.
- **[CHƯA XÁC MINH]**: lưới chi tiết khi có dòng; mũi tên ▼ cạnh ô ngày; cách khung lưới hiểu chuỗi lọc và vị ngữ ngày.

## 1. Bố cục UI (khác khuôn)

Một tab "Danh mục hóa đơn hủy":
- **Thanh trên** (trái sang phải): **Từ** và **Đến** (`dtNgay`, để trống mặc định, định dạng `__/__/____`, nên mở màn là tải toàn bộ), mũi tên ▼ (Q12), ô **Lọc** (`txtLoc`, lọc **chỉ lưới chi tiết**), nút **Refresh** (tải lại lưới master).
- **Bộ lọc lưới master chỉ có ngày hủy:** `FilterManager1` (`AutoLoad = False`) có đúng một ánh xạ `dtNgay → NGAYHUY`. Không lọc theo số phiếu hay khách hàng.
- **Lưới trên (master)** `grMain` = `TDONHANGHUY`, chỉ đọc, **22 cột** theo thứ tự `layout.xml` (mặc định tạm Q8; ảnh còn có cột chọn dòng ở đầu):

| # | Cột | Nhãn | Kiểu | # | Cột | Nhãn | Kiểu |
|---|---|---|---|---|---|---|---|
| 1 | `NAME` | Số phiếu | chữ | 12 | `TRALAI` | Trả lại | số |
| 2 | `NOTE` | Ghi chú | chữ | 13 | `TIENHANG` | Tiền hàng | số |
| 3 | `NGAY` | Ngày | ngày | 14 | `TILETHUE` | Tỉ lệ thuế | số |
| 4 | `KHACHHANG` | Khách hàng | chữ | 15 | `TIENTHUE` | Tiền thuế | số |
| 5 | `NHANVEN` | Nhân viên | chữ | 16 | `TILEGIAMGIA` | Tỉ lệ giảm giá (%) | số |
| 6 | `THUNGAN` | Thu ngân hủy | chữ | 17 | `TIENGIAMGIA` | Tiền giảm giá | số |
| 7 | `GIOHUY` | Giờ hủy | giờ | 18 | `PHIVANCHUYEN` | Phí vận chuyển | số |
| 8 | `NGAYHUY` | Ngày hủy | ngày | 19 | `THANHTOANBOI` | Thanh toán bởi | chữ |
| 9 | `DOITRA` | Đổi trả | số | 20 | `LYDOHUY` | Lý do hủy | chữ |
| 10 | `DATHANHTOAN` | Đã thanh toán | ô đánh dấu | 21 | `TIENGIO` | Tiền giờ | số |
| 11 | `GIOTHANHTOAN` | Giờ thanh toán | ngày giờ | 22 | `PHIDICHVU` | Phí dịch vụ | số |

  - Định dạng: `GIOHUY` = **`HH:mm`**, `GIOTHANHTOAN` = **`dd/MM HH:mm`** (code đặt, thay cho `FORMAT = TIME` trong `SCOLUMN`). `SCOLUMN.FORMAT = n0` cho `DOITRA`, `TRALAI`, `TIENHANG`, `TILETHUE`, `TIENTHUE`, `TIENGIAMGIA`, `PHIVANCHUYEN`; `TILEGIAMGIA` không có định dạng.
  - **Không có cột Bàn:** `BAN` có trong bảng, được ghi lúc hủy và có dòng `SCOLUMN` nhãn "Bàn", nhưng **không nằm trong cột của `grMain`**.
- **Lưới dưới (detail)** `grDetail` = `TDONHANGHUYCHITIET` của dòng master đang chọn, **7 cột** theo thứ tự `ViewConfig` (mặc định tạm Q9): Ghi chú (`NOTE`), Mã hàng (`MAHANG`), Tên hàng (`TENHANG`), ĐVT (`DVT`), Số lượng (`SOLUONG`), Đơn giá (`DONGIA`), Thành tiền (`THANHTIEN`).
- Không có nút Thêm/Sửa/Xóa/Excel/In. `grMain` đặt `ReadOnly = True` nhưng cũng đặt `AllowAdding`/`AllowDeleting = True` (menu chuột phải gốc có thể cho thêm/xóa, chưa thử): v1 **không** cho (Q15).

## 2. Luật và luồng (khác khuôn)

### 2.1 Nạp dữ liệu [CODE]

- Mở tab → `grMain.LoadData("")` (hai ô ngày trống nên tải toàn bộ).
- **Từ/Đến** lọc lưới master theo `NGAYHUY` (`TIMESTAMP`). Vị ngữ do khung quyết định, không đọc được từ code form (`AfterFiltered` rỗng). Mặc định tạm Q10: tính **trọn ngày "Đến"** (`NGAYHUY < Đến + 1 ngày`); một đầu để trống thì không giới hạn đầu đó.
- **Đổi dòng master** (focus row) → `grDetail.LoadData()` với `TDONHANGHUYID = <ID dòng master>`. Chưa chọn dòng → `TDONHANGHUYID = ''` (lưới trống).
- **Refresh** → chỉ gọi `grMain.LoadData("")`; **không xóa ô Lọc**, nên lưới chi tiết vẫn chịu chuỗi lọc đang gõ (Q11). Không giữ vị trí dòng (P2). Việc lời gọi này có giữ bộ lọc ngày hay không do khung quyết định; v1 giữ bộ lọc ngày đang chọn.

### 2.2 Dữ liệu `TDONHANGHUY` [DB]

`ID`, `NAME` (số phiếu), `NOTE`, `NGAY` (ngày bill), `KHACHHANG`, `NHANVEN`, `THUNGAN`, `THANHTOANBOI` (tên, không phải ID), `DOITRA`, `DATHANHTOAN`, `GIOTHANHTOAN`, `TRALAI`, `TIENHANG`, `TILETHUE`, `TIENTHUE`, `TILEGIAMGIA`, `TIENGIAMGIA` (**giá trị lưu sai** theo app gốc: bằng `TILEGIAMGIA`, xem Q3), `PHIVANCHUYEN`, `PHIDICHVU`, `TIENGIO`, `NGAYHUY`, `GIOHUY`, `TDONHANGID` (ID bill gốc đã xóa), `LYDOHUY`, `BAN` (tên bàn chụp lúc hủy), cột chuẩn. **Không có cột `TONGCONG`** — màn không hiển thị tổng cộng; nếu cần, tính `TIENHANG − TIENGIAMGIA + TIENTHUE + PHIDICHVU + PHIVANCHUYEN + TIENGIO` (Q4, **chỉ khi** `TIENGIAMGIA` lưu đúng là tiền).
`TDONHANGHUYCHITIET`: `ID`, `TDONHANGHUYID`, `MAHANG`, `TENHANG`, `DVT`, `SOLUONG` (= `SLXUAT − SLNHAP` lúc hủy), `DONGIA`, `THANHTIEN`, `NOTE`, cột chuẩn. Tiền `DECIMAL(18,2)`. Không đổi cấu trúc DB.

### 2.3 Chuỗi lọc `txtLoc`

Gõ chuỗi → code gán nguyên văn `grDetail.GridView.Filter = txtLoc.Text` (lọc **chỉ lưới chi tiết**); xóa chuỗi để bỏ lọc. Khung lưới hiểu chuỗi này thế nào (chứa chuỗi, hay phải đúng cú pháp biểu thức; cột nào) **[CHƯA XÁC MINH]**. v1: lọc "chứa chuỗi" trên `TENHANG` và `MAHANG`, không phân biệt hoa thường (Q2/Q13).

### 2.4 Quyền

Quyền xem màn = `SFUNCTION` `f1a22c9e-…` "Danh mục hóa đơn hủy" (`NOTE = View`, `STATUS = 30`, nhóm "Bán hàng"). `SGROUPROLE` trên bản sao `DATA.fdb`: **Quản lý 240, Kế toán 240**; nhóm khác không có dòng. v1 theo mapping này qua cơ chế phân quyền chung. Không có quyền riêng cho từng thao tác (màn chỉ xem).

### 2.5 Liên hệ với màn khác

- **Lưu vết hoạt động** hiển thị bill đã hủy qua `TDONHANGHUY` (số phiếu + lý do) và nối `TLUUVET.SODONHANG = TDONHANGHUY.TDONHANGID`; **không** có liên kết ngược từ màn này sang lưu vết (v1: không thêm).
- Nội dung bản lưu do spec **Sử dụng dịch vụ** (`HuyBill` ở bàn) và **Quản lý bán hàng** (hủy phiếu) quy định; hai chỗ có điểm lệch đã nêu trong spec Quản lý bán hàng §3.2 (`TIENGIAMGIA` ghi sai, `THANHTOANBOI` rỗng với bill nhóm). Màn này chỉ hiển thị nguyên trạng.

## 3. Chỗ đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Giữ y hệt gốc: chỉ xem, không sửa/xóa/in.
- **P2** Giữ vị trí dòng đang chọn khi Refresh.
- **P3** Hiển thị thêm cột "Tổng cộng" tính được (Q4/Q14). *Mặc định tạm: không thêm*: bảng không lưu tổng, công thức ở §2.2 chỉ là phỏng đoán (chưa tính tới `DOITRA`) và `TIENGIAMGIA` đang lưu sai.
- **P4** Truy vấn dùng tham số, không nối chuỗi `TDONHANGHUYID`.

## 4. Câu hỏi và mặc định đã áp

Cột "Mặc định" là quyết định cho v1. Đề xuất P1, P4 được áp; P2, P3 không làm. Q7, Q15: màn **chỉ xem**, không có đường nào thêm, sửa, xóa dữ liệu.

| # | Câu hỏi | Mặc định |
|---|---|---|
| Q1 | ~~Bộ lọc mặc định của lưới master~~ **Đã xác định từ `layout.xml`: chỉ ngày hủy `NGAYHUY`** | — |
| Q2 | Cách khớp ô Lọc (cột nào, hoa/thường) | `TENHANG`/`MAHANG`, không phân biệt hoa thường |
| Q3 | `TIENGIAMGIA` của bản lưu cũ có thể sai (= `TILEGIAMGIA`) nên cột #16 và #17 hiện cùng một số. Nếu P4 spec Quản lý bán hàng được duyệt, bản hủy mới ghi tiền, cùng một cột sẽ mang 2 nghĩa. Hiển thị nguyên trạng hay xử lý khác? | Hiển thị nguyên trạng, ghi chú trên màn |
| Q4 | Thêm cột "Tổng cộng" (P3)? | Không |
| Q5 | Thêm xuất Excel / in danh sách hủy (gốc không có)? | Không |
| Q6 | Chụp thêm app gốc. Cột đã lấy được từ `layout.xml`. **Còn thiếu:** lưới chi tiết có dòng, phần lưới master cuộn sang phải, mũi tên ▼ | Có, sau |
| Q7 | Có cần xóa bản hủy cũ (gốc không có)? | Không |
| Q8 | Cột lưới master: theo `layout.xml` (Số phiếu, Ghi chú, Ngày…, có Phí vận chuyển ở #18) hay theo ảnh app (Ngày, Số phiếu, Ghi chú…, không thấy Phí vận chuyển trước "Thanh toán bởi")? | Theo `layout.xml`, đủ 22 cột |
| Q9 | Cột Ghi chú của lưới chi tiết đứng đầu (theo `ViewConfig`) hay cuối? | Theo `ViewConfig` (đứng đầu) |
| Q10 | Lọc ngày trên `NGAYHUY`: tính trọn ngày "Đến" hay đến 00:00? | Trọn ngày |
| Q11 | Refresh có xóa ô Lọc không? (gốc không xóa) | Không xóa |
| Q12 | Mũi tên ▼ cạnh Từ/Đến mở ra gì (khoảng ngày chọn nhanh)? | v1 chỉ làm 2 ô ngày |
| Q13 | Cách khớp ô Lọc (gộp với Q2): "chứa chuỗi" hay cú pháp biểu thức? | Chứa chuỗi trên `TENHANG`/`MAHANG` |
| Q14 | Nếu thêm "Tổng cộng" (P3) thì công thức nào, `DOITRA` có tham gia không? | Không thêm cột |
| Q15 | Menu chuột phải hai lưới có cho Thêm/Xóa không (gốc đặt `AllowAdding`/`AllowDeleting = True`)? | Không (màn chỉ xem) |
| Q16 | `TDONHANGHUY.STATUS`: có lọc `STATUS = 30` hay hiện mọi bản lưu? (code form không lọc) | Hiện tất cả |

## 5. Quyết định đã chốt (áp, không hỏi lại)

Xóa cứng cả app (không thùng rác); tiền `NUMERIC(18,2)`/`decimal`; chỉ VNĐ; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; bỏ karaoke + tính giờ (cột `TIENGIO` chỉ hiển thị nguyên trạng); không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB; màn chỉ xem.

## 6. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest chèn thẳng ≥ 3 bản hủy kèm chi tiết vào `TDONHANGHUY` / `TDONHANGHUYCHITIET`, dọn sau khi chạy)

Hai tiêu chí cần luồng hủy bill thật (chưa có code) đã chuyển sang [`su-dung-dich-vu.md`](su-dung-dich-vu.md) §7.

- [ ] Mở màn: hai ô ngày trống, lưới master liệt kê đủ `TDONHANGHUY`; đủ **22 cột** đúng nhãn, thứ tự theo §1 (Q8); giờ hủy `HH:mm`, giờ thanh toán `dd/MM HH:mm`.
- [ ] Lọc Từ/Đến theo `NGAYHUY`: bản hủy lúc giữa ngày "Đến" được tính (Q10); để trống một đầu thì không giới hạn đầu đó.
- [ ] Bản có `TILEGIAMGIA ≠ 0` ghi theo cách của app gốc: cột #16, #17 hiển thị đúng giá trị trong DB, có ghi chú (Q3).
- [ ] Chọn một dòng master: lưới chi tiết hiện đúng 7 cột (§1) và các dòng `TDONHANGHUYCHITIET` của bill đó (SL, đơn giá, thành tiền khớp lúc hủy); đổi dòng cập nhật ngay; không chọn dòng → trống.
- [ ] Ô Lọc chỉ lọc lưới chi tiết; xóa chuỗi trả đủ dòng; Refresh tải lại lưới master, **không xóa** ô Lọc (Q11).
- [ ] Quyền theo mapping §2.4; truy vấn dùng tham số (P4).
- [ ] Không có nút Thêm/Sửa/Xóa/In, menu chuột phải không cho thêm/xóa (Q15); lưới chỉ đọc; không SQL trong View; màu/cỡ chữ từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc).

## 7. Gợi ý implement (không bắt buộc)

`CancelledBillRepository` (Data: danh sách master lọc theo ngày hủy, chi tiết theo `TDONHANGHUYID`), `CancelledBillView` (UI: bộ lọc + 2 lưới master–detail). `main` @ `954f133` **chưa có component lưới master–detail dùng chung**; `BookingListView` (Khách đặt hàng) tự dựng `SplitContainer` + lưới, có thể tham khảo cách làm và dùng lại `DetailGridMenu` cho phần sao chép/dãn cột. Thêm nhánh route theo `node.Id = 7d169f76-9897-4111-ba6d-f3cbd4c4e3f7` trong `ScreenFactory.CreateView`, đặt trước nhánh kiểm `Operation`. Làm trước luồng hủy bill: selftest chèn dữ liệu trực tiếp trên bản sao DB.
