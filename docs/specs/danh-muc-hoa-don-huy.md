# Spec (delta): Danh mục hóa đơn hủy (TDONHANGHUY)

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-24. Base: `main` @ `acfcbca` (đã `git fetch origin main`)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md) (lọc + lưới + chi tiết, thanh nút trên). Spec này **chỉ ghi phần khác**; chỗ không nói = như khuôn.
- Menu: **HOẠT ĐỘNG > Danh mục hóa đơn hủy** (`SMENU` `7d169f76-9897-4111-ba6d-f3cbd4c4e3f7`, `SORTORDER = ZZZ024`, `OPERATION = 4`, `MENUCODE = DanhMucHoaDonHuy`, `SFORMID` `a52478cf-2d23-4ff0-8b63-e098e5ce6829`, `SFUNCTIONID` `f1a22c9e-774f-4dce-ad95-d1c392a9ba51` "Danh mục hóa đơn hủy", nhóm "Bán hàng", `NOTE = View`). Là **form tùy biến** `DanhSachBillHuy` (hai lưới master–detail **chỉ đọc**, không thêm/sửa/xóa).
- Bảng chính `TDONHANGHUY` (`STABLEDESC` `ee531e78-c1b2-4801-ba74-5e43a630fd65`, `TABLETYPE = 3`), chi tiết `TDONHANGHUYCHITIET` (`STABLEDESC` `9dfabf38-202e-435d-a87a-33e551fd2e4d`, `TABLETYPE = 4`; khóa ngoại mềm `TDONHANGHUYID`). Hiện **0 bản ghi** cả hai trong DB chép.
- Dữ liệu **do màn khác ghi** khi hủy bill (Sử dụng dịch vụ — hủy bill trên bàn; Quản lý bán hàng — hủy phiếu): ghi `TDONHANGHUY` + `TDONHANGHUYCHITIET` rồi xóa cứng bill gốc. Spec này chỉ **xem lại** bản lưu đó.

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`): cấu trúc 2 bảng, `STABLEDESC`, `SMENU`, `SFUNCTION`.
- **[CODE]** `POS_TAP_SRC_EXPORT/SFORM/Danh sách bill hủy__a52478cf/` (`code.cs` ngắn, `layout.xml`); cách ghi bản lưu hủy: [`quan-ly-ban-hang-dieu-chinh-hoa-don.md`](quan-ly-ban-hang-dieu-chinh-hoa-don.md) §3.2 (đã sửa theo review) và [`su-dung-dich-vu.md`](su-dung-dich-vu.md) §3.8.
- **[CHƯA XÁC MINH]**: danh sách cột và bộ lọc của hai lưới (cấu hình trong `ViewConfig`/`FILTERCONFIG` nhị phân, `STABLEDESC.FILTERCONFIG` = NULL cho cả hai bảng, chưa giải mã `SFORM.AELAYOUT`). **[APP]** chưa chụp (Q6).

## 1. Bố cục UI (khác khuôn)

Một tab "Danh mục hóa đơn hủy":
- **Thanh trên:** ô **Lọc** (`txtLoc`, lọc **lưới chi tiết** phía dưới theo chuỗi gõ — không lọc lưới master), nút **Refresh** (tải lại lưới master, chuỗi lọc rỗng). Có `FilterManager` cho lưới master (panel lọc theo cột như khuôn; **mặc định tạm: Ngày hủy `NGAYHUY` (khoảng), Số phiếu `NAME`, Khách hàng** — Q1).
- **Lưới trên (master)** `grMain` = `TDONHANGHUY`: cột giờ hủy `GIOHUY` định dạng **`HH:mm`**, giờ thanh toán `GIOTHANHTOAN` định dạng **`dd/MM HH:mm`**. Cột đầy đủ **[CHƯA XÁC MINH]**; mặc định tạm theo bảng: Số phiếu (`NAME`), Ngày hủy (`NGAYHUY`), Giờ hủy, Bàn (`BAN`), Khách hàng (`KHACHHANG`), Nhân viên (`NHANVEN`), Thu ngân (`THUNGAN`), Người thanh toán (`THANHTOANBOI`), Tiền hàng, Giảm giá, Thuế, Phí dịch vụ, Phí vận chuyển, Tiền giờ, **Lý do hủy** (`LYDOHUY`), Ghi chú.
- **Lưới dưới (detail)** `grDetail` = `TDONHANGHUYCHITIET` của dòng master đang chọn (`TDONHANGHUYID = <ID master>`): Mã hàng (`MAHANG`), Tên hàng (`TENHANG`), ĐVT (`DVT`), Số lượng (`SOLUONG`), Đơn giá (`DONGIA`), Thành tiền (`THANHTIEN`), Ghi chú (`NOTE`).
- Không có nút Thêm/Sửa/Xóa/Excel/In (gốc chỉ có Lọc + Refresh). **Không** có thùng rác (xóa cứng cả app; riêng danh mục này không xóa được qua giao diện).

## 2. Luật và luồng (khác khuôn)

### 2.1 Nạp dữ liệu [CODE]

- Mở tab → `grMain.LoadData("")` (toàn bộ, lọc theo `FilterManager` nếu có mặc định).
- **Đổi dòng master** (focus row) → `grDetail.LoadData()` với `TDONHANGHUYID = <ID dòng master>`. Chưa chọn dòng → `TDONHANGHUYID = ''` (lưới trống).
- **Refresh** → tải lại lưới master (không giữ vị trí dòng — P2).

### 2.2 Dữ liệu `TDONHANGHUY` [DB]

`ID`, `NAME` (số phiếu), `NOTE`, `NGAY` (ngày bill), `KHACHHANG`, `NHANVEN`, `THUNGAN`, `THANHTOANBOI` (tên, không phải ID), `DOITRA`, `DATHANHTOAN`, `GIOTHANHTOAN`, `TRALAI`, `TIENHANG`, `TILETHUE`, `TIENTHUE`, `TILEGIAMGIA`, `TIENGIAMGIA` (**giá trị lưu sai** theo app gốc: bằng `TILEGIAMGIA`, xem Q3), `PHIVANCHUYEN`, `PHIDICHVU`, `TIENGIO`, `NGAYHUY`, `GIOHUY`, `TDONHANGID` (ID bill gốc đã xóa), `LYDOHUY`, `BAN` (tên bàn chụp lúc hủy), cột chuẩn. **Không có cột `TONGCONG`** — màn không hiển thị tổng cộng; nếu cần, tính `TIENHANG − TIENGIAMGIA + TIENTHUE + PHIDICHVU + PHIVANCHUYEN + TIENGIO` (Q4, **chỉ khi** `TIENGIAMGIA` lưu đúng là tiền).
`TDONHANGHUYCHITIET`: `ID`, `TDONHANGHUYID`, `MAHANG`, `TENHANG`, `DVT`, `SOLUONG` (= `SLXUAT − SLNHAP` lúc hủy), `DONGIA`, `THANHTIEN`, `NOTE`, cột chuẩn. Tiền `DECIMAL(18,2)`. Không đổi cấu trúc DB.

### 2.3 Chuỗi lọc `txtLoc`

Gõ chuỗi → gán `grDetail.GridView.Filter = <chuỗi>` (lọc **chỉ lưới chi tiết**, theo cú pháp lọc của khung lưới); xóa chuỗi để bỏ lọc. Cách khớp cột (tên hàng/mã?) do khung lưới quyết định — v1: lọc theo `TENHANG` và `MAHANG`, không phân biệt hoa thường, chứa chuỗi (Q2).

### 2.4 Quyền

Quyền xem màn = `SFUNCTION` `f1a22c9e-…` "Danh mục hóa đơn hủy". Mặc định tạm tài khoản không phải admin: **không cấp** (Q5 spec Sử dụng dịch vụ). Không có quyền riêng cho từng thao tác (màn chỉ xem).

### 2.5 Liên hệ với màn khác

- **Lưu vết hoạt động** hiển thị bill đã hủy qua `TDONHANGHUY` (số phiếu + lý do) và nối `TLUUVET.SODONHANG = TDONHANGHUY.TDONHANGID`; **không** có liên kết ngược từ màn này sang lưu vết (v1: không thêm).
- Nội dung bản lưu do spec **Sử dụng dịch vụ** (`HuyBill` ở bàn) và **Quản lý bán hàng** (hủy phiếu) quy định; hai chỗ có điểm lệch đã nêu trong spec Quản lý bán hàng §3.2 (`TIENGIAMGIA` ghi sai, `THANHTOANBOI` rỗng với bill nhóm). Màn này chỉ hiển thị nguyên trạng.

## 3. Chỗ đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Giữ y hệt gốc: chỉ xem, không sửa/xóa/in.
- **P2** Giữ vị trí dòng đang chọn khi Refresh.
- **P3** Hiển thị thêm cột "Tổng cộng" tính được (Q4) — *mặc định tạm: không thêm*, vì `TIENGIAMGIA` hiện lưu sai nên tổng tính ra sẽ sai.
- **P4** Truy vấn dùng tham số, không nối chuỗi `TDONHANGHUYID`.

## 4. Câu hỏi chờ Chiến quyết

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | Bộ lọc mặc định của lưới master (gốc chưa xác định) | Ngày hủy, Số phiếu, Khách hàng |
| Q2 | Cách khớp ô Lọc (cột nào, hoa/thường) | `TENHANG`/`MAHANG`, không phân biệt hoa thường |
| Q3 | `TIENGIAMGIA` của bản lưu cũ có thể sai (= `TILEGIAMGIA`): hiển thị nguyên trạng hay chú thích? Khi hủy mới sẽ ghi đúng tiền (P4 spec Quản lý bán hàng nếu duyệt) | Hiển thị nguyên trạng, ghi chú trên màn |
| Q4 | Thêm cột "Tổng cộng" (P3)? | Không |
| Q5 | Thêm xuất Excel / in danh sách hủy (gốc không có)? | Không |
| Q6 | Chụp app gốc để lấy đủ cột lưới master/detail + bộ lọc | Có, sau |
| Q7 | Có cần xóa bản hủy cũ (gốc không có)? | Không |

## 5. Quyết định đã chốt (áp, không hỏi lại)

Xóa cứng cả app (không thùng rác); tiền `NUMERIC(18,2)`/`decimal`; chỉ VNĐ; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; bỏ karaoke + tính giờ (cột `TIENGIO` chỉ hiển thị nguyên trạng); không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB; màn chỉ xem.

## 6. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest tự tạo ≥ 3 bản hủy kèm chi tiết, qua đúng luồng hủy của spec Sử dụng dịch vụ / Quản lý bán hàng)

- [ ] Mở màn: lưới master liệt kê đủ `TDONHANGHUY` (theo bộ lọc mặc định); giờ hủy `HH:mm`, giờ thanh toán `dd/MM HH:mm`.
- [ ] Chọn một dòng master: lưới chi tiết hiện đúng các dòng `TDONHANGHUYCHITIET` của bill đó (SL, đơn giá, thành tiền khớp lúc hủy); đổi dòng cập nhật ngay; không chọn dòng → trống.
- [ ] Ô Lọc chỉ lọc lưới chi tiết; xóa chuỗi trả đủ dòng; Refresh tải lại lưới master.
- [ ] Hủy một bill thật bằng màn Sử dụng dịch vụ rồi mở màn này: bản hủy xuất hiện với đúng số phiếu, bàn, lý do hủy, người thanh toán; chi tiết không dính dòng xuất vật tư/combo con (theo cách spec hủy quy định).
- [ ] Không có nút Thêm/Sửa/Xóa/In; lưới chỉ đọc; không SQL trong View; màu/cỡ chữ từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc).

## 7. Gợi ý implement (không bắt buộc)

`CancelledBillRepository` (Data: danh sách master có lọc, chi tiết theo `TDONHANGHUYID`), `CancelledBillView` (UI: bộ lọc + 2 lưới master–detail, dùng chung component lưới master–detail của Khách đặt hàng/Theo dõi đặt phòng). Làm **sau** khi spec hủy bill ở Sử dụng dịch vụ / Quản lý bán hàng đã implement để có dữ liệu thật.
