# Spec (delta): Sử dụng dịch vụ (màn bán hàng tại bàn)

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-23. Base: `main` @ `13d90f8` (đã `git fetch origin main`, working tree sạch). Đã sửa theo review Muse (issue #1) ngày 23/11 + Chiến chốt Q1/P3.
- Rà lại ngày 25/11 trên `main` @ `cb8b5b7`, đối chiếu [`quy-tien.md`](quy-tien.md) (đã duyệt). **Quỹ nào tăng giảm theo `quy-tien.md`**; spec này quy định màn bán hàng ghi gì vào bill. **Q14–Q19 Chiến chốt 25/11** (§6.3). Q2–Q13 còn dùng mặc định tạm, chưa chốt.
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md). Spec này **chỉ ghi phần khác khuôn**; chỗ không nói = như khuôn.
- Menu: **HOẠT ĐỘNG > Sử dụng dịch vụ** (`SMENU` `8e360da4-b18e-43ba-a0fe-aac67914aabf`, `SORTORDER = ZZZ013`, `MENUCODE = SuDungDichVu`, `OPERATION = 4`, `SFORMID` `141ca9a1-6819-49e2-b8a6-c1ac806ef0a9`, `SFUNCTIONID` `db591d0e-d671-42d0-b3b1-f705345cc641`). Không phải màn danh mục (form `141ca9a1` có `STABLEDESCID` rỗng; form hóa đơn `f3f7bb77` có `STABLEDESCID = 721de885-…`, chưa rõ desc bảng nào): là **form tùy biến** `SuDungDichVu` nhúng form hóa đơn `TDONHANG0Ae` ("Hóa đơn nhà hàng") ở chế độ `HoaDonMode.SuDungDichVu`.
- Cùng lớp `TDONHANG0Ae` còn phục vụ **Điều chỉnh hóa đơn** (ZZZ014) và **Quản lý bán hàng** (ZZZ015) — hai màn đó sẽ có spec riêng, chỉ ghi phần khác chế độ. Spec này **là spec gốc của bộ máy hóa đơn nhà hàng**.

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** đo trên bản chép `DATA.fdb` (không mở file gốc). Bản DEMO gần như trống (`DKHUVUC: 0, DBAN: 0, DMATHANG: 1, TDONHANG: 0`) nên không dùng để đo; selftest §7 phải tự tạo khu vực/bàn/bill/mặt hàng mẫu (seed), không trông vào dữ liệu có sẵn. **[CODE]** `POS_TAP_SRC_EXPORT/SFORM/`: `Sử dụng dịch vụ__141ca9a1`, `Hóa đơn nhà hàng__f3f7bb77`, `Khu vực control__f842ff6f`, `Chuyển bàn__0001db41`, `Xác nhận thanh toán__96f6da47`. **[APP]** một ảnh màn chính.
- **Đã có tài liệu nghiệp vụ chi tiết** (đọc rồi, KHÔNG chép lại ở đây; spec này trỏ tới): [`pos-tap-nghiep-vu-nha-hang.md`](../pos-tap-nghiep-vu-nha-hang.md) (tạo bill, thêm dòng, giá, công thức tổng bill, thanh toán, xuất vật tư), [`pos-tap-nghiep-vu-ban-khu-vuc.md`](../pos-tap-nghiep-vu-ban-khu-vuc.md) (chuyển/gộp bàn, trạng thái bàn, hủy bill), [`pos-tap-nghiep-vu-dat-phong-in-bep.md`](../pos-tap-nghiep-vu-dat-phong-in-bep.md) (in xuống bếp). Mục dưới ghi: **phạm vi v1, bố cục, luồng, danh sách luật được giữ/cắt, cấu hình, quyền, đề xuất, câu hỏi, nghiệm thu**.
- **Giới hạn quan sát [APP]:** lần chụp này công cụ giả lập chuột/bàn phím của máy không tác động được lên app (Windows từ chối `SetCursorPos`), nên chỉ có **ảnh màn chính lúc mở** (1 ảnh). Các màn mở bàn, thêm món, thanh toán, chuyển bàn **chưa chụp**: bố cục lấy từ `layout.xml`/code, đánh dấu **[CHƯA XÁC MINH BẰNG APP]**. Cần chụp bổ sung nếu Chiến muốn (Q13).

## 1. Phạm vi v1 (đề xuất, Chiến chốt)

Theo ghi nhớ dự án "bỏ karaoke + tính giờ, tablet làm sau":

| Nhóm | Quyết định đề xuất |
|---|---|
| Sơ đồ bàn theo khu vực, mở bàn, gọi món, giảm/xóa, đổi giá, ghi chú, giảm giá dòng/tổng, phí dịch vụ, thuế, thanh toán (tiền mặt/thẻ/chuyển khoản/voucher/thẻ trả trước/điểm/nợ), chuyển bàn, gộp bàn, hủy bill, in tạm tính/hóa đơn/chế biến (nút có, in = "chưa hỗ trợ" nếu chưa có máy in) | **Làm** |
| Tiền giờ (`TIENGIO`, `TDONHANGGIO`, bảng giá giờ, `UpdateGio`, mặt hàng `DichVuTheoGio`, ngắt giờ, đổi giờ vào/ra, `Shared.CoTienGio`) | **Cắt** (không code; giữ cột DB, ghi 0) |
| Cân điện tử (cổng COM), màn hình khách (cây hiển thị giá), điều khiển đèn bàn, đồng bộ `webservice.exe`, `HttpProcessor` (máy tính bảng) | **Cắt v1** |
| Quẹt thẻ từ chọn khách (`CachChonKhachHang`≠0), SPA chọn nhân viên từng dòng | **Cắt v1** |
| Chuyển mặt hàng sang bill khác (`ChuyenMatHang`) | **Làm** (cùng form chuyển bàn) — đề xuất, xem Q9 |
| Kiểm đồ / trả đồ (`KichHoatChucNangKiemDo`, `tsbKiemDo`) | **Để sau** cùng "Điều chỉnh hóa đơn" |
| Mua 1 tặng 1 và KM tự động (`KichHoatKhuyenMaiTuDong`) | **Để sau** cùng spec khuyến mãi (cờ hiện = 0, [DB]) |

Rule cố định áp dụng: không thùng rác (hủy bill = xóa cứng sau khi ghi `TDONHANGHUY`, như app gốc); tiền `decimal`/`NUMERIC(18,2)`; làm tròn dùng `LamTron` bội `LamTronTien` (hiện 1000, [DB]) kiểu AwayFromZero, **chỉ khi tính tiền**; không đọc cột `LOAIDINHLUONG` (loại mặt hàng lấy từ `DLOAIMATHANGID`).

## 2. Bố cục UI (khác khuôn)

### 2.1 Màn chính [APP] (ảnh 01)

Một tab "Sử dụng dịch vụ" (mở từ menu hoặc nút toolbar đầu tiên). `splitMain` ngang, tỉ lệ lưu theo độ rộng màn hình trong `Data\app.dat` mục `LAYOUT` (`splitMain<Rộng>`, `splitKhuVuc<Rộng>`, `splitHoaDon<Rộng>`; chỉ nhận giá trị 10–1000):
- **Trái — sơ đồ bàn** (`splitKhuVuc`, 2 hàng): `tabKhuVuc` (khu vực `TABHIENTHI=0`) trên, `tabKhuVuc2` (`TABHIENTHI=1`) dưới (ẩn nếu rỗng). Mỗi khu vực là một tab (tên khu vực), bên trong là `ListView` LargeIcon các bàn (biểu tượng `DBIEUTUONG.ANH` của khu vực, xám khi bàn không dùng), nhóm theo `DNHOMHIENTHI`.
- **Phải — hóa đơn** (`TDONHANG0Ae`): hàng đầu: ô giờ vào, nút **Bắt đầu**, ô giờ ra (chỉ liên quan tiền giờ — cắt), `Ngày`, `Số phiếu`, `Số khách`, `Khách hàng (F12)`; lưới chi tiết cột **Tên hàng | ĐVT | SL | Đ giá | CK% | T tiền | Ghi chú**; thanh công cụ trên lưới: `+`, `−`, **Đặt sl, Đổi giá, Ghi chú, CK%**; cột nút: **Chuyển bàn, Gộp bàn, Số lượng [combo], << Thêm, Giảm >>, Xóa, % theo nhóm, In chế biến (F10)**; cột phải: ô **Tìm (F3)**, cây nhóm mặt hàng ("Tất cả" + nhóm), lưới mặt hàng (Tên hàng, giá theo khu vực). Dưới: tab tổng cộng (tiền hàng, giảm giá, phí dịch vụ, thuế, **Tổng cộng**), nút **Thanh toán (F11)**, **Thống kê (F9)**, **In lại bill (F8)**, **Tạm ứng** (khi `SuDungChucNangTamUngTrongDonHang=30`) **[CHƯA XÁC MINH BẰNG APP phần dưới]**.
- Trạng thái khi chưa chọn bàn / bàn trống: các ô giờ trống, nút **Bắt đầu** bật, lưới trống, các nút thao tác dòng mờ. [APP]

### 2.2 Hộp thoại phụ [CODE]

`Chọn bàn` (F5 bàn trống / F6 bàn đang dùng / F7 tất cả; kết quả chọn nhảy tới bàn đó, nếu bàn trống thì hỏi "Bạn có muốn thực hiện mở bàn không?" hoặc, khi `BatBuocNhapNhanVienBanHang=30`, mở hộp "Chọn nhân viên" rồi mở bàn); `Chuyển bàn` (bản đồ khu vực dùng chung, double-click = OK); `Xác nhận thanh toán`; `Đặt số lượng`; `Đổi giá bán`; `Ghi chú`; `Đặt giảm giá`; `Giảm giá theo nhóm`; `Nhập số lượng nhà hàng`; `Nhập mật khẩu giảm đồ`; `Nhập lý do`; `Thêm mặt hàng mở`; `Combo mở`; `Thống kê`; `In chế biến`.

### 2.3 Component map (đề xuất WinForms)

`TableMapView` (tab khu vực + ô bàn, tự vẽ theo token trạng thái) · `OrderView` (header + lưới + tổng + nút) · `MenuPickerView` (cây nhóm + lưới mặt hàng + ô tìm) · `PaymentDialog` · `MoveMergeTableDialog`. Logic: `OrderCalculator` (thuần, có selftest), `TableService` (mở/đóng/chuyển/gộp/hủy), `InvoiceNumberService`. Không SQL trong View (theo CLAUDE.md).

## 3. Luật và luồng (khác khuôn)

### 3.1 Trạng thái bàn [CODE `KhuVucControl.SetItemInfo`, `LayDanhSachBan`]

Thứ tự ưu tiên: (1) `DBAN.TDONHANGID` trỏ bill `DATHANHTOAN=0` → **Đã in tạm tính** nếu `SOLANINTAMTINH>0`, ngược lại **Đang sử dụng**; (2) `DBAN.TRANGTHAI=1` **Báo dọn**; `=2` **Dọn dẹp**; (3) có `TSUACHUA` chưa xong **Sửa chữa**; (4) có `TDATHANG` hôm nay `TUGIO` ±30 phút **Đặt** (hiện "tên khách [SĐT]-giờ"); (5) **Trống**. Chỉ lấy `DBAN.STATUS=30` và `DKHUVUC.STATUS=30`, sắp `DBAN.NAME`. Ô hiển thị tên bàn, ghi chú bill (khi `HienThiGhiChuTrenGiaoDienBan=30`), giờ bắt đầu; cỡ ô theo `KichThuocHienThiOGiaoDienDichVu` (0=64px, 1=48, 2=32, 3=24). Làm tươi: nền mỗi 20 giây (`refreshTimer.Interval = 20000`; `onlineTimer` 5 phút cho việc khác) + sau mỗi thao tác (tên timer cũ `tmrAutoRefresh` và điều kiện `HeThongChayNhieuMayTram=30` chưa xác minh); dùng kết nối riêng, lỗi bỏ qua.
Menu chuột phải: Mở bàn / Mở bàn từ đặt (bàn không dùng), Thanh toán / Hủy hóa đơn / Chuyển / Gộp (bàn đang dùng hoặc đã in tạm tính). Double-click bàn = mở bàn/mở bill. Kéo thả bàn sang bàn khác = chuyển (đích trống) hoặc gộp (đích có bill).
Phân quyền khu vực: `PhanQuyenTruyCapTheoKhuVuc=30` và không phải admin → chỉ khu vực có trong `DTKTHEOKHUVUC` của user (hiện = 0, [DB]).

### 3.2 Chọn bàn → tải bill [CODE `LoadByTable`]

- Một máy: `TDONHANGID = DBAN.TDONHANGID`; có thì nạp bill, không thì để màn trống, nút Bắt đầu bật. Đổi bàn là tải lại nếu bill khác.
- Nhiều máy trạm (`HeThongChayNhieuMayTram=30`, hiện = 30 [DB]): bàn bị giữ khi `DBAN.TAIKHOANGIU` không rỗng và `GIULUC` trong 120 giây gần đây → báo **"<BÀN> NÀY ĐANG ĐƯỢC THAO TÁC BỞI: <tài khoản>"** và bỏ chọn. Máy chọn bàn có bill thì **giữ bàn** (`TableLocker`: ghi `TAIKHOANGIU`, `GIULUC`, `IPCONGTAC` vào `DBAN`, gia hạn định kỳ); nhả khi chọn bàn khác, thanh toán, đóng tab. Cơ chế gia hạn chính xác nằm trong DLL (**[CHƯA XÁC MINH]**, Q7).
- Giá hiển thị trong lưới mặt hàng theo `DKHUVUC.DGIAMATHANGID` (`GIABAN`/`GIABAN2/3/4`, 0 = giá thường).

### 3.3 Mở bàn [CODE `btnBatDau_Click`, `TaoHoaDonTrang`] — chi tiết xem `nha-hang.md` §1

Kiểm tra: đã chọn bàn (câu thông báo **chưa tìm thấy nguyên văn** trong code export — khi implement dùng câu tương đương, ví dụ "Mời bạn chọn bàn trước khi mở bàn"; không coi là nguyên văn gốc); bản dùng thử ≥ 30 ngày có bill → chặn; bàn bị giữ (nhiều máy); bàn đã có bill → "Bàn này đang có khách". Tạo `TDONHANG` (`LOAI=0`, `DATHANHTOAN=0`, `SOKHACH=1`, `NGAY = ngày giao dịch`, `BATDAU = now − TuDongDoiGioVaoVeTruoc phút` bỏ giây, `DCUAHANGID` hiện tại, `TILEPHIDICHVU`/`TILETHUE` mặc định theo cấu hình, giảm giá khởi tạo = `MacDinhGiamGia`) và **liên kết bàn–bill**. Thêm món mặc định theo khu vực (`SuDungMatHangMacDinh=30`, `DMATHANGMACDINH`). Ghi lưu vết "Mở hóa đơn trên bàn". **`DBAN.TDONHANGID` không có chỗ ghi trong code export** (chỉ trigger `TDONHANG_TRIGGER`; xem `ban-khu-vuc.md` §6) → đề xuất P3.
Số hóa đơn: `TSOHOADON` + quay vòng ngày/tháng/năm/không (`ddMM`+4 số, `MMyy`+5, `yy`+6, 7 số), `SOHD` = lớn nhất + 1, thử lại khi lỗi. `Shared.SoHDQuayVong`, `Shared.NgayGiaoDich` nằm trong DLL, giá trị hiện hành **[CHƯA XÁC MINH]** (Q6).

### 3.4 Gọi món [CODE `btnThem_Click`, `DoAddItemToGridWithoutTrack`]

- Chỉ chạy khi bill chưa thanh toán (`IsDaThanhToan`: bill mất, bàn không còn trỏ bill, hoặc `DATHANHTOAN=30` → "Hóa đơn này đã thanh toán, không thể thay đổi").
- Chưa chọn mặt hàng → "Mời bạn chọn mặt hàng trước!"; SL ≤ 0 → "Mời bạn nhập số lượng!"; mặt hàng đã xóa → "Mặt hàng này đã bị xóa ra khỏi danh mục"; `TAMKHOA=30` → "Mặt hàng này tạm khóa, không thể sử dụng".
- Cột SL của dòng là **`SLXUAT`** (`TDONHANGCHITIET` không có cột `SOLUONG`; `SOLUONG` trong code chỉ là bí danh lưới).
- Chọn giá theo khu vực; `HIENTHILUACHON` (biến/thuộc tính trong code, **không phải cột `DMATHANG`**; chưa rõ lấy từ đâu — xác minh khi implement) hỏi loại giá (trừ giá thời giá và mặt hàng mở); nhập SL qua hộp khi `HienThiCuaSoNhapSoLuongKhiQuetMaVach=30`.
- Gộp dòng khi `NhapMotMatHangNhieuLanTrongPhieu=0` (hiện = 0 → **gộp**): cùng mặt hàng + cùng `TILEGIAMGIA` + không ghi chú + (`GIATHEOTHOIGIA=30` hoặc cùng đơn giá) → cộng SL. Mặt hàng mở / thời giá hỏi tên-giá. Combo, combo mở: dòng cha + dòng con nằm ở **`TDONHANGCHITIET`** (`COMBOID`/`COMBOPARENTID`), đơn giá con 0. `DDINHLUONG` chỉ là bảng định lượng nguyên liệu (10 cột, không có giá), không dùng cho combo. Chi tiết `nha-hang.md` §2.
- Dòng mới: `DKHOHANGID = DKHUVUC.DKHOHANGID`, `GIAVON = DMATHANG.GIAVON`, `COMBOID = GUID mới`. **Mỗi thay đổi ghi vào DB ngay** (`mapper.Update` trong tính tổng, chỉ ở chế độ này) — v1 giữ hành vi: ghi sau mỗi thao tác chứ không có nút Lưu.
- Sau khi gọi: tự đưa trỏ về ô Tìm; `StartAutoPrintTimer` (in chế biến tự động sau `ThoiGianTuDongIn` giây nếu bật).

### 3.5 Giảm / xóa / sửa dòng [CODE] — thông báo nguyên văn

| Thao tác | Điều kiện / thông báo |
|---|---|
| Giảm, xóa | cần quyền `XoaGiamMon` (đăng nhập lại), mật khẩu giảm đồ khi `NhapMatKhauGiamDo=30` |
| Bill đã in tạm tính | "Không thể giảm vì phiếu đã in tạm tính" / "Không thể xóa vì phiếu đã in tạm tính" / đặt SL: "Phiếu đã in tạm tính, mời bạn sử dụng chức năng thêm hoặc click nút dấu '+' phía bên trái"; bỏ qua nếu `ChoPhepThayDoiHoaDonSauKhiInTamTinh=30` (hiện = 30 → **không chặn**) hoặc có quyền `DuocGiamDoSauKhiInTamTinh` |
| Đã in xuống bếp | `KhongDuocGiamDoSauKhiInPhaChe=30` (hiện = 30) hoặc thiếu `XoaGiamMonSauKhiInCheBien`: không giảm dưới SL đã in: "Bạn đã in xuống bếp số lượng N. Không thể giảm." / "Không được xóa sau khi in xuống bếp!" |
| Nút `−` | giữ SL ≥ 1 |
| Nút Giảm >> | "Mời bạn chọn mặt hàng trong hóa đơn để giảm số lượng"; chỉ giảm khi SL ≥ giá trị+1, ngược lại "Không thể giảm 'x' vì số lượng hiện tại chỉ có 'y'" |
| Xóa | bắt buộc nhập lý do khi bật lưu vết **và** `NhapLyDoKhiXoaMon=30`, ngược lại hỏi "Bạn có muốn xóa mặt hàng đang chọn ra khỏi hóa đơn không?"; xóa cha thì xóa con combo |
| Đổi giá | quyền `SuaDonGia`; Giảm giá dòng/nhóm: quyền `GiamGiaMatHang`; `ChoPhepNhapGiamGia` (hiện 30) và quyền `ThayDoiGiamGiaTongHoaDon` / `ThayDoiPhiDichVu` cho ô tổng |
| Dịch vụ theo giờ | **cắt** |

### 3.6 Tính tổng bill [CODE `mapper_OnCalculation`]

Công thức đầy đủ ở `nha-hang.md` §6; v1 bỏ nhánh tiền giờ (`TIENGIO = 0`) nên chỉ còn nhánh **không gộp** giảm giá:
```
TIENHANG = LamTron(Σ THANHTIEN dòng);  TIENHANGCHUAGIAM = LamTron(Σ SL×DONGIA)
giảm tiền ↔ %: tiền→% = Round(tiền×100/TIENHANG, 2, AwayFromZero); %→tiền = LamTron(% × TIENHANG / 100)
sau giảm = TIENHANG − TIENGIAMGIA
phí dịch vụ: theo tiền → TILEPHIDICHVU = Round(PHIDICHVU×100/sau giảm, 2); theo % → LamTron(TILE × sau giảm / 100)
TIENTHUE = LamTron((sau giảm + PHIDICHVU) × TILETHUE / 100);  TONGCONG = LamTron(sau giảm + PHIDICHVU + TIENTHUE)
TIENPHAITRA = TONGCONG + BILLKHAC   (BILLKHAC = Σ TONGCONG các bill khác cùng NHOMGUID)
-- TIENPHAITRA và BILLKHAC là biến tính trong code, KHÔNG phải cột TDONHANG
```
Dòng: `GIAMTHEOTIEN=30` → `THANHTIEN = DONGIA×SL − TIENGIAMGIA`; ngược lại `DONGIA×SL×(1−TL/100)`, `TIENGIAMGIA = DONGIA×SL − THANHTIEN`. **Đã chốt (Chiến 23/11, Q1):** chỉ VNĐ, `LamTron` 1000 AwayFromZero; bỏ nhánh USD 2 số lẻ của `GiaBanLeUsd` (DB đang để 30, v1 bỏ qua cờ này).

### 3.7 Thanh toán [CODE `btnThanhToan_Click`, `XacNhanThanhToan`]

Thứ tự kiểm tra: bill đã thanh toán → chặn; không có dòng (và tiền giờ 0) → "Mời bạn thêm mặt hàng vào phiếu"; nhiều máy → tải lại bill; bắt buộc chọn nhân viên nếu ô nhân viên hiện ("Mời bạn chọn nhân viên trước"); **kiểm soát order** (`BatChucNangKiemSoatOrder=30`, hiện 0): "Mời bạn nhập số order" / "Mời bạn nhập đúng định dạng số order, ví dụ: 12, 16, 17,19"; tính lại mọi dòng; in chế biến nốt; cảnh báo nếu bill từ ngày cũ ("…Hệ thống sẽ chuyển hóa đơn này sang ngày hiện tại …").
**Hộp Xác nhận thanh toán**: nhận `TIENPHAITRA`, tiền đặt trước (`GetDatTruoc`), nợ cũ (`GetNoCu`), khách. `TONGTIEN = TIENPHAITRA + NợCũ − ĐặtTrước` (đây là "số phải trả" của `quy-tien.md` §4.3). Ô: Khách đưa (mặc định = còn lại), Thẻ ATM (`CoThanhToanThe`, hiện 30), Chuyển khoản (`CoThanhToanChuyenKhoan`, hiện 0), **Tài khoản (hiện khi có ô thẻ hoặc ô chuyển khoản** — khác gốc, gốc chỉ hiện theo cờ chuyển khoản), Voucher (`CoThanhToanVoucher`, 0), Thẻ trả trước (`SuDungTheTraTruoc`, 0), Trừ tích lũy (`SuDungDiemTichLuyDeThanhToan`, 0), Trả lại. **Tính trả lại và chặn nhập theo `FundRules.SettlePayment`** (§3.7.1), không theo công thức gốc `max(0, Σ đã trả − TONGTIEN)`. `TienTra = KhachDua+Voucher+TheTraTruoc+TruTichLuy+The+ChuyenKhoan`. Nút: **OK** (đóng + in), **Đóng bill không in** (F9; tắt nếu `BatBuocInKhiThanhToan=30`), **In thử bill** (F8, `ChoPhepInTamTinh=30`, quyền `InTamTinh`, kết quả Ignore = in tạm tính, bill giữ nguyên).
Chặn khi bấm OK: thiếu quyền `ThanhToan`; `TONGTIEN > TienTra` và `ChoPhepKhachNo≠30` → "Chức năng này hiện tại chưa được kích hoạt. Bạn có thể kích hoạt trong menu 'Quản trị'|'Cấu hình toàn hệ thống'"; chưa chọn khách → "Bạn chưa chọn khách hàng, mời bạn chọn khách hàng trước!"; hỏi "Bạn có muốn cho khách hàng 'X' nợ số tiền 'N' không?"; thẻ trả trước vượt số dư; **chuyển khoản hoặc quẹt thẻ** mà chưa chọn tài khoản → "Mời bạn chọn tài khoản" (gốc chỉ bắt với chuyển khoản); **quẹt thẻ quá số phải trả** → chặn (§3.7.1); `Shared.DuLieuHoaDonOk` (trong DLL, **[CHƯA XÁC MINH]**).
Ghi DB khi OK/Retry: xem `nha-hang.md` §7 cho các cột không liên quan tiền; **các cột tiền theo §3.7.2 và §3.7.3**; toàn bộ phần đóng bill chạy trong **một transaction**; xóa dòng `TDONHANGCHITIET` có `XUATVATTU=30` cũ (`XUATVATTU` là cột của `TDONHANGCHITIET`, không phải `TDONHANG`) rồi `XuatVatTu`; xóa `TINCHEBIEN` của bill; nếu khách đã cọc dư thì tạo phiếu chi trả lại; nâng nhóm khách theo điểm (`TuDongNangCapThanhVienKhiDatHanMuc=30`, hiện 30); tạo bill trống cho bàn (`Reload("")`), nhả giữ bàn, làm tươi trạng thái bàn.

#### 3.7.1 Trả lại và chặn nhập (theo `quy-tien.md` §4.3, Q2 của spec Quỹ)

- **Không quẹt thẻ quá số phải trả:** `Thẻ ≤ max(0, TONGTIEN − voucher − thẻ trả trước − tích lũy − chuyển khoản)`.
- Tiền trả lại **chỉ** phát sinh từ tiền mặt khách đưa và chuyển khoản. Phần dư của voucher / thẻ trả trước / tích lũy bỏ, không trả tiền mặt.
- Chuyển khoản dư, trả lại tiền mặt: `CHUYENKHOAN` ghi đủ số khách chuyển, `TIENMAT = khách đưa − trả lại` (**âm được**), `TRALAI = trả lại`. Quỹ Tài khoản cộng đủ, **quỹ Tiền mặt bị trừ** phần trả lại.
- `TONGTIEN ≤ 0` (đặt trước đủ hoặc dư): không nhận thêm tiền mặt, chuyển khoản, thẻ.
- Khách đang dư tiền (nợ cũ âm) đã được trừ vào `TONGTIEN`; trả thừa thì trả lại như thường (gốc đặt trả lại = 0 khi nợ cũ âm).
- Ví dụ: bill 480.000, chuyển khoản 500.000 → trả lại 20.000; Tài khoản +500.000, Tiền mặt −20.000. Bill 480.000, chuyển khoản 300.000, quẹt thẻ 200.000 → **chặn** (thẻ tối đa 180.000).

#### 3.7.2 Cột tiền của bill đơn

Nợ khách tính bằng `Σ(TONGCONG − TIENTHANHTOAN)` trừ phiếu thu/chi [CODE `GetNoCu`], nên `TIENTHANHTOAN` là cột quyết định công nợ.

| Cột | Giá trị |
|---|---|
| `TIENMAT` | khách đưa − trả lại (âm được) |
| `CHUYENKHOAN`, `THE` | số nhập ở hộp thanh toán |
| `DTAIKHOANNGANHANGID` | bắt buộc khi `CHUYENKHOAN` hoặc `THE` ≠ 0 |
| `TIENTHANHTOAN` | đặt trước dùng cho bill + `max(0, min(TienTra, TONGTIEN))`. **Đặt trước dư (Q16, chốt 25/11):** = phần cọc thực dùng (tiền bill, cộng nợ cũ được cọc trả nếu có), **không** ghi 0 như gốc. Ví dụ bill 300.000, cọc 400.000 → `TIENTHANHTOAN = 300.000`, `CONGNO = 0`, phiếu chi trả lại 100.000 |
| `CONGNO` | `TONGCONG − TIENTHANHTOAN`; **âm** khi thu kèm nợ cũ |
| `DATTRUOC`, `NOCU`, `TRALAI`, `KHACHDUA` | như gốc; `TRALAI` theo §3.7.1 |

- **Đặt trước** = Σ `THU` của phiếu thu gắn `TDONHANGID` = bill hoặc `TDATHANGID` = phiếu đặt của bill, **không tính phiếu `STATUS = -30`** (gốc không lọc). Bill không có phiếu đặt chỉ tính tạm ứng khi `SuDungChucNangTamUngTrongDonHang = 30`. Tiền này đã vào quỹ lúc lập phiếu thu, **không cộng lại** khi đóng bill.
- **Đặt trước dư** (`TONGTIEN < 0`): sau khi đóng bill, mở phiếu chi "Trả lại tiền đặt trước hóa đơn số …" (`KHONGTHAYDOICONGNO` tích, số tiền = phần dư, chọn tiền mặt hoặc chuyển khoản + tài khoản). Đóng phiếu chi mà không lưu → **cảnh báo rõ và hỏi xác nhận**. Bản viết lại **chưa có phiếu chi ghi thật**, nên phải làm trong phạm vi spec này.
- **Tạm ứng** (khi bật): phiếu thu chuyển khoản bắt buộc chọn tài khoản.

#### 3.7.3 Thanh toán gộp nhiều bill (`NHOMGUID`)

"Gộp bàn" ở §3.8 chuyển hết món sang **một** bill, nên sau đó thanh toán như bill đơn. Mục này chỉ áp cho **nhiều bill cùng `NHOMGUID`, trả một lần**.

**Cách tạo nhóm trong v1 (Q14, Chiến chốt 25/11): chức năng mới "Thanh toán chung"** (app gốc không có):
- Mở từ menu chuột phải của bàn đang có khách, hoặc nút cạnh **Thanh toán**. Quyền `ThanhToan`.
- Hộp chọn bàn cho chọn **nhiều** bàn đang có khách (bill chưa thanh toán, không bị máy khác giữ), bàn đang chọn được tick sẵn; cần ít nhất 2 bàn.
- Xác nhận → các bill được gán chung một `NHOMGUID` mới (bill đã thuộc nhóm thì cả nhóm cũ vào nhóm mới), rồi mở **Xác nhận thanh toán** với tổng của nhóm.
- Đóng hộp thanh toán mà không trả → gỡ `NHOMGUID` vừa gán, các bàn trở lại như trước.
- Câu chữ, bố cục hộp chọn bàn: đề xuất, theo hộp chọn bàn hiện có.
- Tách bill bằng "Ngắt giờ" không làm ở v1.

**Hiện trạng app gốc:**
- [CODE `Sử dụng dịch vụ` `lst_OnNgatGioRequest`] chỗ tạo `NHOMGUID` đọc được trong bản export là **"Ngắt giờ"** ở bản không phải karaoke: thực chất là **tách bill** (tạo bill mới cùng bàn, cùng nhóm). Menu này chỉ hiện khi `Shared.CoNgatGio` (trong DLL, **[CHƯA XÁC MINH]** có bật cho bản F&B không).
- [DB] bản chép `POS TAP/Database/DATA.fdb` có 0 bill thuộc nhóm.
- [DB] câu SQL của nhánh thanh toán nhóm đọc `TTHUCHI.SOTIEN`, **cột không tồn tại**; chạy thử trên bản chép báo `Column unknown SOTIEN`. Trên schema này nhánh đó không chạy được.
- [CODE] nhánh nhóm còn không ghi `TIENMAT`/`CHUYENKHOAN`/`THE`/tài khoản/`CONGNO`; chỉ xuất vật tư và xóa `TINCHEBIEN` cho **bill đang mở**; không ghi `SOTT`, `DIEM`; bỏ lệnh trả lại đặt trước dư; chặn `TIENTHANHTOAN` ở `TONGCONG − đặt trước` nên theo `GetNoCu` phần đặt trước thành nợ và nợ cũ thu kèm không được ghi.

**v1:**
- Chia tiền, nợ, người đứng tên nợ, nợ cũ thu kèm: **y hệt `quy-tien.md` §4.8** (thứ tự giờ vào rồi số phiếu; thẻ → chuyển khoản → tiền mặt; chuyển khoản dư và trả lại ở bill cuối; thiếu nằm ở bill cuối rồi lùi; thiếu thì bắt chọn một khách, gán cho mọi bill mang nợ).
- **Đặt trước dư trong nhóm (Q15, chốt 25/11):** tính chung cả nhóm. Cọc dư của bill trước bù sang bill sau theo thứ tự nhóm; chỉ khi tổng cọc lớn hơn tổng tiền cả nhóm mới lập phiếu chi trả lại. Ví dụ bill 1 = 300.000 (cọc 400.000), bill 2 = 200.000 → chỉ thu thêm 100.000; bill 1 dùng 300.000 cọc, bill 2 dùng 100.000 cọc còn dư + 100.000 tiền mới.
- **Nợ cũ thu kèm khi các bill khác khách (Q17, chốt 25/11):** nợ cũ theo khách chọn ở hộp thanh toán; bill cuối được gán khách đó. Nhóm đồng thời trả thiếu thì người đứng tên nợ là chính khách đó.
- **Voucher, thẻ trả trước, điểm tích lũy (Q18, chốt 25/11):** lấp từ bill đầu theo thứ tự voucher → thẻ trả trước → tích lũy, **trước** thẻ ngân hàng, chuyển khoản, tiền mặt. Phần dư bỏ, không trả lại tiền mặt.
- Mỗi bill ghi **đủ** `TIENMAT`, `CHUYENKHOAN`, `THE`, `DTAIKHOANNGANHANGID` của mình.
- Mỗi bill ghi `TIENTHANHTOAN` = đặt trước dùng cho bill đó + phần phân bổ; `CONGNO = TONGCONG − TIENTHANHTOAN` (cùng nghĩa với bill đơn). Σ `CONGNO` = số thiếu khi trả thiếu; bill cuối `CONGNO` âm khi thu kèm nợ cũ. `TRALAI` ghi ở bill cuối, bill khác ghi 0 (gốc lặp cùng một số ở mọi bill, cộng lại bị nhân lên).
- Điều kiện chặn như bill đơn, gồm `ChoPhepKhachNo` (hiện 0 → **nhóm trả thiếu cũng bị chặn**) và quy tắc thẻ, tài khoản ở §3.7.1.
- Mọi bill trong nhóm đều được xuất vật tư, xóa `TINCHEBIEN`, cấp `SOTT`, tính `DIEM`; bàn của mọi bill được làm tươi. Một transaction cho cả nhóm.

### 3.8 Chuyển / gộp bàn, hủy bill

Chi tiết `ban-khu-vuc.md` §2–3. Thông báo nguyên văn quan trọng: "Bàn này đã in tạm tính, không thể thay đổi" (chặn khi `ChoPhepThayDoiHoaDonSauKhiInTamTinh=0`); "Bàn này đã thanh toán, không thể thay đổi"; "Mời bạn chọn bàn khác"; "<Bàn> <tên> đang bị giữ bởi <tài khoản>"; chuyển cần quyền `ChuyenBan`, gộp `GopBan`; hỏi "Bạn có muốn chuyển hóa đơn từ bàn 'A' sang bàn 'B' không?" / "Bạn có chắc chắn muốn gộp bàn 'A' và bàn 'B' không? Sau khi gộp, chỉ còn hóa đơn trên bàn 'B', các mặt hàng sẽ được chuyển sang bàn này." Gộp bị từ chối nếu bill nguồn tạo từ đặt hàng, hoặc tỉ lệ giảm giá hai bill khác nhau. Hộp chọn bàn: đích có bill mà chế độ "Chuyển" → "Bàn này đang sử dụng, mời bạn chọn bàn khác(bàn trống) để chuyển!"; đích trống mà chế độ "Gộp" → "Bàn này không sử dụng, mời bạn chọn bàn khác (bàn đang có khách) để gộp!". Hủy bill: quyền `HuyHoaDon`, trong `ThoiGianChoPhepHuyBill` (hiện 50 phút) từ lúc `BATDAU`, bill không thuộc nhóm gộp; `NhapLyDoKhiHuyHoaDon=30` (hiện 30) bắt nhập lý do, ngược lại hỏi "Bạn có muỗn hủy hóa đơn trên bàn '<tên>' không?" (**nguyên văn lỗi chính tả "muỗn"** — Q12); hủy = `HuyBill` (ghi `TDONHANGHUY`, xóa bill, chi tiết, `TINCHEBIEN`).
Hủy bill chưa thanh toán không đổi quỹ; phiếu thu cọc/tạm ứng liên quan **không** tự xóa (`quy-tien.md` §4.2). **Gộp bàn khi bàn nguồn có tạm ứng (Q19, Chiến chốt 25/11): không cho gộp.** Bill nguồn có phiếu thu tạm ứng còn hiệu lực (`TTHUCHI.TDONHANGID` = bill, `STATUS ≠ -30`) thì từ chối gộp, vì gộp sẽ xóa bill nguồn và làm phiếu tạm ứng mất chỗ trừ. Câu báo (đề xuất): "Bàn '<A>' đã có tiền tạm ứng, không thể gộp. Mời bạn dùng Thanh toán chung hoặc thanh toán riêng." Muốn trả chung thì dùng Thanh toán chung (§3.7.3). Hủy bill có tạm ứng: chưa chốt, xem Q20.
**Nút "Tách bàn" của app gốc là stub**, không thêm. Tách bill qua "Ngắt giờ" là chuyện khác, xem §3.7.3.

### 3.9 Phím tắt (màn Sử dụng dịch vụ) [CODE `mapper_OnKeyDown`]

F2 ghi chú dòng · F3 tìm · F4 chọn nhân viên · F5/F6/F7 chọn bàn trống/đang dùng/tất cả · F8 in lại bill · F9 thống kê · F10 in chế biến · F11 thanh toán · F12 chọn khách. Enter trong ô Tìm: thêm món (hoặc nhảy sang ô SL nếu `ToiUuDungBanPhim`); Esc xóa ô Tìm.

### 3.10 Cấu hình `SCONFIG` hiện hành [DB] (những khóa quyết định hành vi)

| Khóa | Giá trị | Khóa | Giá trị |
|---|---|---|---|
| `LamTronTien` | 1000 | `GiaBanLeUsd` | 30 |
| `HeThongChayNhieuMayTram` | 30 | `ChoPhepThayDoiHoaDonSauKhiInTamTinh` | 30 |
| `KhongDuocGiamDoSauKhiInPhaChe` | 30 | `SuDungChucNangInXuongBep` | 30 |
| `TuDongInPhaCheSauKhiGoiMon` | 0 | `ThoiGianTuDongIn` | 10 |
| `KichHoatLuuVetHoatDong` | 30 | `NhapLyDoKhiHuyHoaDon` | 30 |
| `ThoiGianChoPhepHuyBill` | 50 | `NhapMotMatHangNhieuLanTrongPhieu` | 0 |
| `CoPhiDichVu`, `CoThueSuat` | 0, 0 | `ChoPhepNhapGiamGia` | 30 |
| `CoThanhToanThe` | 30 | `ChoPhepKhachNo` | 0 |
| `DoanhSoTuongUngVoi1Diem` | 20000 | `QuyDoi1DiemSangTien` | 1000 |
| `CachTinhDiem` | 1 | `TuDongNangCapThanhVienKhiDatHanMuc` | 30 |
| `KichHoatKhuyenMaiTuDong` | 0 | `BatChucNangKiemSoatOrder` | 0 |

Các khóa còn lại ở mục 3 đều = 0 trừ ghi chú. Chỉ ghi giá trị trong bản chép DB hiện có (bản demo có 1 khu vực "Lầu 1", 3 bàn, 2 mặt hàng, 0 bill).

## 4. Phân quyền

Quyền chức năng dùng trong màn (bảng `SFUNCTION`; nhóm "Hóa đơn bán hàng" trừ khi ghi khác; `NAME` là tên hiển thị tiếng Việt, mã kiểu `ChuyenBan` là mã trong code, cột `ID` là `SFUNCTION.ID`):

| Mã trong code | Tên hiển thị [DB] | `SFUNCTION.ID` |
|---|---|---|
| `ChuyenBan` | Chuyển bàn | `f3401c18-c9ce-4d97-92e3-ba1542671edb` |
| `GopBan` | Gộp bàn | `7a46b358-71e7-4400-b480-e9f47f580f93` |
| `HuyHoaDon` | Hủy hóa đơn | `912818cc-88a5-4b26-8aa3-8a9bb4a7c65b` |
| `ThanhToan` | Thanh toán | `4ceaa330-c51a-41e9-80ae-a28ad69433b8` |
| `InTamTinh` | In tạm tính | `d057c064-cedc-4429-bc41-e4e8e2472994` |
| `InLaiBill` | In lại bill | `679ea71e-9921-4c39-9df3-74d292594de5` |
| `XoaGiamMon` | Xóa giảm món | `83a0fd04-067c-46fd-857e-01cc70af7a23` |
| `XoaGiamMonSauKhiInCheBien` | Xóa giảm món sau khi in chế biến | `d6579615-3967-4a7f-888e-2678f1abac4d` |
| `DuocGiamDoSauKhiInTamTinh` | Được giảm đồ sau khi in tạm tính (nhóm **"Quản trị"**) | `af0cd926-4f9e-4e96-9049-a07716a1e038` |
| `SuaDonGia` | Sửa đơn giá | `0f06705f-f59a-4252-893b-680a2baa3e27` |
| `GiamGiaMatHang` | Giảm giá mặt hàng | `267fdc55-2f47-42d1-8ca7-4a6d92e73b15` |
| `ThayDoiGiamGiaTongHoaDon` | Thay đổi giảm giá tổng hóa đơn | `e5f93aa1-50ff-4ea3-ab8b-117254c3c70e` |
| `ThayDoiPhiDichVu` | Thay đổi phí dịch vụ | `326162e4-b204-4bb5-a8c5-c782c8cdc462` |
| `XemDonGiaTrongHoaDonBanHang` | Xem đơn giá trong hóa đơn bán hàng | `8f35f8c3-f141-4099-a2d2-a3328d3ec949` |
| `ChuyenMatHangSangHoaDonKhac` | Chuyển mặt hàng sang hóa đơn khác | `e35fe6b9-64b9-4461-87be-17a0565e914a` |
| `ThongKeTrongSuDungDichVu` | Thống kê trong sử dụng dịch vụ | `43402cbf-c1b8-4a96-b10f-31b233925801` |
| *(thêm, mã chưa rõ)* | Thu tạm ứng khách hàng (chỉ cần khi bật tạm ứng) | `8cbfb281-68ab-46df-b84d-ff8b530dbb07` |

(Còn có quyền "Xem thống kê của các cài khoản khác", `6baaab2b-8d9f-4a7c-8644-d50651829421`, dùng ở màn Thống kê.) `XemDonGiaTrongHoaDonBanHang`: không có quyền → ẩn giá, tổng, thanh toán (dành cho nhân viên chạy bàn). Kiểu kiểm: `CanView` (xem, im lặng) hay `CanLogin` (yêu cầu đăng nhập lại, hiện hộp). Giữ nguyên hai kiểu. Mặc định tạm: admin có tất cả; các quyền chưa cấu hình trong DB bản chép coi như **không cấp** (Q5).

## 5. Chỗ khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Mỗi thao tác ghi DB ngay (như gốc) nhưng gom trong một transaction ngắn và có kiểm tra `DATHANHTOAN` trong cùng câu lệnh để tránh ghi đè bill vừa thanh toán ở máy khác.
- **P2** Tính tiền bằng `OrderCalculator` thuần `decimal`, **một bản duy nhất** dùng cho cả màn này, Điều chỉnh hóa đơn, Quản lý bán hàng (và tablet sau) — app gốc có bản sao lệch.
- **P3** *(ĐÃ CHỐT 23/11: GIỮ trigger gốc, không ghi `DBAN.TDONHANGID` trực tiếp)*. **Đã đọc thân trigger ngày 25/11** trên bản chép DB (chạy trước insert/update/delete): (a) bill chuyển sang đã thanh toán → `DBAN.TDONHANGID` của bàn trỏ sang một bill chưa thanh toán khác cùng bàn, không có thì `NULL`; (b) bill chưa thanh toán có `DBANID` đổi (gồm **tạo mới**, chuyển bàn, xóa) → bàn cũ `NULL`, bàn mới trỏ bill này. Lưu ý: xóa một trong nhiều bill chưa thanh toán cùng bàn sẽ làm bàn thành trống dù còn bill khác. Trigger không ghi cột tiền.
- **P4** Giữ bàn: dùng cùng các cột `TAIKHOANGIU`/`GIULUC` nhưng gia hạn mỗi 30 giây và nhả khi đóng tab; hết hạn 120 giây như gốc.
- **P5** Sửa lỗi chính tả "muỗn" → "muốn".
- **P6** `GiaBanLeUsd`: một chế độ tiền duy nhất ở v1 (VNĐ, bội `LamTronTien`); bỏ nhánh 2 số lẻ. ĐÃ CHỐT (Q1).
- **P7** Trạng thái bàn **Sửa chữa** không chặn mở bill (như gốc) nhưng hiển thị cảnh báo trước khi mở.
- **P8** Làm tươi sơ đồ bàn bằng poll nền 5–30 giây thay vì tải lại toàn bộ lưới.
- **P9** Mật khẩu giảm đồ: kiểm với mật khẩu quản trị đã băm, không so chuỗi thô (cách gốc nằm trong DLL, chưa biết). **Lưu ý:** đây là thay đổi hành vi bảo mật so với gốc, cần Chiến duyệt rõ, không phải cải tiến nhỏ.

## 6. Câu hỏi

### 6.1 Đã trả lời bằng đọc code/DB
Nhiều máy trạm khóa bàn bằng `DBAN.TAIKHOANGIU/GIULUC` (120 giây) ✔ · gộp dòng cùng mặt hàng khi cờ = 0 ✔ · hủy bill bị chặn khi `NHOMGUID` khác rỗng ✔ · tách bàn là stub ✔ · tiền giờ không dùng ở v1 ✔.

### 6.2 Còn mở — kèm "mặc định tạm"
| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | ~~`GiaBanLeUsd=30` nhưng `LamTronTien=1000`: v1 chỉ VNĐ?~~ **ĐÃ CHỐT 23/11** | VNĐ, `LamTron` 1000 AwayFromZero |
| Q2 | Có giữ ghi DB tức thì mỗi thao tác (khác "Lưu" cuối)? | Giữ như gốc |
| Q3 | `Shared.NgayGiaoDich` (theo ngày mở bàn hay theo giờ chốt)? Hiện chưa rõ giá trị | Ngày hiện tại của DB |
| Q4 | Có cần màn hình hiển thị giá cho khách, cân điện tử, đèn bàn không? | Không (v1 cắt) |
| Q5 | Quyền mặc định cho tài khoản không phải admin? | Không cấp, admin toàn quyền |
| Q6 | `Shared.SoHDQuayVong` hiện hành (ngày/tháng/năm/không)? | Theo ngày: `ddMM` + 4 số |
| Q7 | Cơ chế gia hạn `GIULUC` (chu kỳ, điều kiện nhả)? | P4 |
| Q8 | Nội dung `Shared.DuLieuHoaDonOk` (kiểm hợp lệ bill trước thanh toán)? | Chỉ kiểm: có dòng, tổng khớp tổng dòng |
| Q9 | Làm "Chuyển mặt hàng sang bill khác" trong v1? | Làm, cùng hộp chuyển bàn |
| Q10 | In (tạm tính, hóa đơn, chế biến) thật hay "chưa hỗ trợ"? | Nút có, báo "chưa hỗ trợ" |
| Q11 | Mở bàn từ phiếu đặt (`Mở bàn từ đặt`) làm trong spec này hay gộp với Khách đặt hàng? | Làm ở đây (liên kết `TDONHANG.TDATHANGID`, như `khach-dat-hang.md` §3.6) |
| Q12 | Sửa câu hỏi hủy bill có lỗi chính tả? | Sửa (P5) |
| Q13 | Có cần chụp bổ sung (mở bàn, thanh toán, chuyển bàn) bằng app gốc khi máy cho phép? | Có, sau |

### 6.3 Câu về tiền, chưa chốt (không dùng mặc định tạm để code)

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q14 | ~~Cách tạo nhóm trả chung~~ | **Chiến chốt 25/11: (a) Thanh toán chung** (§3.7.3) |
| Q15 | ~~Nhóm có bill đặt trước dư~~ | **Chiến chốt 25/11:** tính chung, cọc dư bù sang bill sau; thu 100.000 trong ví dụ (§3.7.3, `quy-tien.md` §4.8) |
| Q16 | ~~Bill đơn đặt trước dư~~ | **Chiến chốt 25/11:** `TIENTHANHTOAN` = phần cọc dùng, phiếu chi trả lại phần dư (§3.7.2) |
| Q17 | ~~Nợ cũ khi các bill khác khách~~ | **Chiến chốt 25/11:** theo khách ở hộp thanh toán (§3.7.3) |
| Q18 | ~~Voucher, thẻ trả trước, tích lũy trong nhóm~~ | **Chiến chốt 25/11:** lấp từ bill đầu, trước thẻ ngân hàng; dư bỏ (§3.7.3) |
| Q19 | ~~Gộp bàn khi bill nguồn có tạm ứng~~ | **Chiến chốt 25/11: không cho gộp** (§3.8). Khác đề xuất cũ (chuyển phiếu tạm ứng) |
| Q20 | Hủy bill (trong 50 phút) khi bill đã có tạm ứng: gốc vẫn cho hủy, phiếu tạm ứng còn lại nhưng trỏ bill đã xóa (tiền đã vào quỹ, không trừ được vào đâu). Chặn hủy như gộp bàn? | Chặn, cùng lý do Q19; muốn hủy thì lập phiếu chi trả lại tạm ứng trước |

## 7. Tiêu chí nghiệm thu (kiểm trên bản sao DB)

- [ ] Quẹt thẻ quá số phải trả bị chặn (gồm ca chuyển khoản 300.000 + thẻ 200.000 cho bill 480.000); voucher lớn hơn bill không sinh tiền trả lại.
- [ ] Bill 480.000 chuyển khoản 500.000: `CHUYENKHOAN = 500.000`, `TIENMAT = −20.000`, `TRALAI = 20.000`; màn Tồn quỹ: Tài khoản +500.000, Tiền mặt −20.000.
- [ ] Chỉ quẹt thẻ, `CoThanhToanChuyenKhoan = 0`: ô tài khoản vẫn hiện và bắt chọn.
- [ ] Bill đơn: `TIENTHANHTOAN`, `CONGNO` đúng §3.7.2 ở các ca đủ, thiếu, thu kèm nợ cũ, có đặt trước; `GetNoCu` sau đó ra đúng nợ còn lại.
- [ ] Đặt trước dư: lưu phiếu chi thì quỹ trừ một lần; đóng không lưu thì có cảnh báo, hỏi xác nhận.
- [ ] Thanh toán chung: chọn ≥ 2 bàn đang có khách, các bill chung một `NHOMGUID`; đóng hộp thanh toán không trả thì gỡ nhóm, các bàn như cũ. Đạt mọi tiêu chí gộp bàn của `quy-tien.md` §7; từng bill có đủ cột tiền; vật tư xuất cho mọi bill; lỗi giữa chừng không bill nào đổi.
- [ ] Nhóm có cọc dư (Q15): bill 1 = 300.000 cọc 400.000, bill 2 = 200.000 → chỉ thu 100.000, không phiếu chi; quỹ tăng đúng 100.000.
- [ ] Bill đơn cọc dư (Q16): bill 300.000 cọc 400.000 → `TIENTHANHTOAN = 300.000`, `GetNoCu` của khách không tăng, phiếu chi 100.000.
- [ ] Gộp bàn khi bàn nguồn có tạm ứng (Q19): bị từ chối, không bill nào đổi; phiếu tạm ứng đã xóa mềm thì cho gộp.
- [ ] Nhóm trả thiếu: `ChoPhepKhachNo ≠ 30` thì chặn; bật thì chưa chọn khách đứng tên nợ không đóng được, chọn rồi mọi bill mang nợ gắn khách đó, bill không nợ giữ khách cũ.

- [ ] (Seed trong selftest: ≥ 2 khu vực, vài bàn, vài mặt hàng, vài bill; không dựa vào DEMO trống.) Mở màn: tab khu vực đúng số `DKHUVUC.STATUS=30`; bàn `DBAN.STATUS=30` sắp theo tên; khu vực `TABHIENTHI=1` ở hàng dưới.
- [ ] Trạng thái ô bàn đúng thứ tự ưu tiên 3.1 (dựng đủ 6 trạng thái bằng dữ liệu mẫu).
- [ ] Mở bàn trống tạo đúng một `TDONHANG` (`DATHANHTOAN=0`, `SOHD`/`NAME` theo quy tắc quay vòng, `BATDAU` làm tròn phút); mở lần hai trên cùng bàn báo "Bàn này đang có khách".
- [ ] Gọi món: gộp dòng theo luật 3.4; mặt hàng `TAMKHOA=30` bị chặn; combo sinh dòng con đơn giá 0 và đổi SL cha cập nhật con.
- [ ] `OrderCalculator`: selftest với ≥ 15 ca (giảm theo %/theo tiền, phí dịch vụ, thuế, làm tròn 1000, biên 500 → 1000) khớp công thức 3.6; `decimal`, không `double`.
- [ ] Giảm/xóa/đặt SL: đủ thông báo ở 3.5; không giảm dưới SL đã in khi cờ bật.
- [ ] Thanh toán: `TIENTHANHTOAN`, `CONLAI`, `CONGNO`, `TRALAI`, `TIENMAT`, `DATHANHTOAN=30`, `SOTT = MAX+1` trong ngày; chặn nợ khi `ChoPhepKhachNo≠30`; bàn về trống, bill mới trắng.
- [ ] Chuyển bàn (đích trống) và gộp bàn (đích có bill) đúng điều kiện + thông báo 3.8; sau gộp bill nguồn bị xóa, dòng chuyển sang bill đích, ghi `TDONHANGHUY`.
- [ ] Hủy bill: chặn quá `ThoiGianChoPhepHuyBill`, chặn nhóm gộp; hủy xong bàn trống.
- [ ] Hai phiên cùng mở một bàn: phiên thứ hai bị chặn bằng "ĐANG ĐƯỢC THAO TÁC BỞI".
- [ ] Không SQL trong View; màu/cỡ chữ lấy từ token; hoạt động ở 125% DPI; lưới nút đủ lớn cho cảm ứng (≥ 44 px) — *yêu cầu v1, không phải hành vi app gốc (gốc là WinForms layout cố định).*
- [ ] Nút In/Excel hiện, báo "chưa hỗ trợ"; không có thùng rác; không đọc `LOAIDINHLUONG`.

## 8. Gợi ý implement (không bắt buộc)

Thứ tự: (1) `OrderCalculator` + selftest; (2) `TableService`/`InvoiceNumberService` + kiểm thử trên bản sao DB; (3) `TableMapView` chỉ đọc; (4) mở bàn + gọi món; (5) thanh toán (là bước 3 của `quy-tien.md` §8 phần bán hàng: dùng lại `FundRules.SettlePayment`, thêm phiếu chi trả cọc dư; sửa luôn phiếu thu đang coi chuyển khoản là `CHUYENKHOAN = 30` còn quỹ coi là khác 0; chia nhóm là hàm thuần có selftest); (6) chuyển/gộp/hủy; (7) giữ bàn nhiều máy. Tham chiếu repo hiện có: `BookingGroupPlanner` (mẫu logic thuần đặt ở Data), phân tầng UI → Services → Data → Domain một chiều.

## Phụ lục A — Ảnh [APP]

1. `img/su-dung-dich-vu/01-man-hinh-chinh.png` — màn lúc mở (khu vực "Lầu 1", 3 bàn demo, hóa đơn trống).

Ghi chú: đường dẫn code `POS_TAP_SRC_EXPORT/SFORM/…` không có trên máy cloud (bản tương đương: `pos-tap-review/raw/sform_code/*.txt`). `OrderCalculator`: mặc định chỉ phục vụ 3 chế độ nhà hàng; form bán lẻ quyết khi có spec bán lẻ. Chỉ có 1 ảnh [APP]; cần chụp bổ sung phần dưới màn và các dialog (Q13).
