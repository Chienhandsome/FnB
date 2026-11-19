# POS TAP — nghiệp vụ bàn / khu vực, chuyển–gộp bàn, màn hình sử dụng dịch vụ

Nguồn: SFORM `Chuyển bàn__0001db41` (`ChuyenBan`, 692 dòng), `Sử dụng dịch vụ__141ca9a1` (`SuDungDichVu`, 625 dòng). Xem thêm [nhà hàng](pos-tap-nghiep-vu-nha-hang.md) và [máy tính bảng](pos-tap-nghiep-vu-may-tinh-bang.md) (cùng logic ở phía API).

## 1. Màn hình `Sử dụng dịch vụ` (bán tại bàn)

- Bố cục: tab khu vực phía trên (`DKHUVUC.TABHIENTHI = 0`) và phía dưới (`= 1`) + hóa đơn nhà hàng (`TDONHANG0Ae`, chế độ `SuDungDichVu`) bên phải. Khu vực: `DKHUVUC.STATUS=30`, sắp theo `SORTORDER`; không phải admin và `PhanQuyenTruyCapTheoKhuVuc=30` thì chỉ khu vực có trong `DTKTHEOKHUVUC`. Mỗi khu vực một `KhuVucControl` (danh sách bàn, hình nền `DBIEUTUONG`, giá khu vực `DGIAMATHANGID`). Vị trí các thanh chia được lưu trong `Data\app.dat` mục `LAYOUT` theo độ rộng màn hình.
- Chọn bàn trống từ phím tắt: nếu `BatBuocNhapNhanVienBanHang=30` thì hỏi chọn nhân viên rồi mở bàn và gán nhân viên; ngược lại hỏi "có muốn mở bàn không".
- **Một máy trạm vs nhiều máy trạm** (`HeThongChayNhieuMayTram=30`): chọn bàn → kiểm tra bàn bị giữ (`GIULUC` trong 120 giây gần nhất và có `TAIKHOANGIU`), nếu bàn có bill thì khóa bàn cho tài khoản hiện tại (`TableLocker`) và tải bill; bàn đang bị người khác thao tác thì báo "ĐANG ĐƯỢC THAO TÁC BỞI: <tài khoản>" và bỏ chọn. Danh sách bàn tự làm tươi theo timer. Mỗi lần đổi bàn: in chế biến nốt phần chưa in (`ThucHienInCheBien(true)`).
- **Ngắt giờ** (chỉ khi có tiền giờ): karaoke — tạo dòng `TDONHANGGIO` cho khoảng giờ vừa dùng (đơn giá/bảng giá hiện tại, thành tiền `Shared.TinhGia`), `TUTHAYDOIGIO = 0`, `BATDAUPHONGCUOI = now`, `KETTHUC = now` nếu cũ hơn. Ứng dụng khác: tách bill — đặt `NHOMGUID` (tạo mới nếu chưa có) cho bill hiện tại, `USERTHANHTOANID = user`, rồi tạo bill mới cùng bàn và cùng `NHOMGUID` (các bill cùng nhóm sẽ được thanh toán chung, xem file nhà hàng mục 7), số bill mới `TaoSoHoaDon`.
- Tự động khởi động `webservice.exe` (đồng bộ online) mỗi 5 phút nếu `KichHoatDongBo=30` và cơ sở dữ liệu là embedded / localhost.
- Tự động tải lại khi `DBAN`, `DKHUVUC`, `DMATHANG` hoặc nhóm mặt hàng thay đổi.
- Đóng tab: in chế biến nốt, nhả khóa bàn.

## 2. Chuyển bàn / gộp bàn / chuyển mặt hàng trên desktop (`ChuyenBan`)

Chế độ `ChuyenGopBanMode`: `ChuyenBan`, `GopBan`, `ChuyenMatHang`. Người dùng chọn bàn đích trên bản đồ khu vực.
- Bàn đích có bill (đang mở) → với `ChuyenBan` báo "bàn này đang sử dụng, chọn bàn trống"; với `GopBan` thực hiện gộp; với `ChuyenMatHang` thực hiện chuyển mặt hàng. Bàn đích không có bill → với `GopBan` / `ChuyenMatHang` báo phải chọn bàn đang có khách; với `ChuyenBan` thực hiện chuyển.
- Điều kiện chung (`ChuyenGopBan`): bill nguồn chưa thanh toán (`DATHANHTOAN = 0`); chưa in tạm tính, trừ khi `ChoPhepThayDoiHoaDonSauKhiInTamTinh=30`; bàn đích khác bàn hiện tại; không bị giữ bởi người khác (nhiều máy trạm). Quyền: `Functions.ChuyenBan` (chuyển) hoặc `Functions.GopBan` (gộp).
- **Chuyển bàn** (`ThucHienChuyenBan`): hỏi xác nhận; nếu có tiền giờ (hoặc `TIENGIO ≠ 0`): ghi dòng `TDONHANGGIO` cho bàn cũ (từ `BATDAUPHONGCUOI` đến now, `Shared.TinhGia` với `PHUTKHUYENMAI`, `TILEKHUYENMAIPHUTDAU`), đặt `TUTHAYDOIGIO = 0`, `BATDAUPHONGCUOI = now`, `KETTHUC = max(KETTHUC, now)`. Cập nhật giá hàng theo khu vực mới (`CapNhatGia`), cập nhật cách tính giờ / bảng giá / đơn giá theo bàn mới, `TIENMOBAN = max(mới, cũ)`, `DBANID = bàn mới`. Ghi lưu vết "Chuyển … từ A sang B". In phiếu thông báo chuyển bàn xuống bếp nếu `SuDungChucNangInXuongBep=30` và `InThongBaoKhiChuyenBan=30` (mỗi loại đồ đã in theo `TINCHEBIEN.DLOAIDOID` và `InDoAn/InDoUong/InDoKhac/InDichVu`, mỗi máy in một lần, mẫu `MauInChuyenBan`). Bật/tắt đèn.
- **Gộp bàn** (`ThucHienGopBan`): bill đích cũng không được in tạm tính (trừ khi cho phép); từ chối nếu bill nguồn có `TIENGIO > 0`, tạo từ đặt hàng (`TDATHANGID`), hoặc `TILEGIAMGIA` / `TILEGIAMGIAGIO` hai bill khác nhau; hỏi xác nhận; cập nhật giá theo khu vực đích; thêm dòng `TDONHANGGIO` vào bill đích (thành tiền tính lại bằng `TinhGia` từ `BATDAU` đến now với 0 phút KM); lưu lưu vết ở cả hai bill; lưu bill nguồn vào bill hủy ("Gộp vào hóa đơn …"); chuyển `TDONHANGCHITIET` và `TINCHEBIEN` sang bill đích; tính lại bill đích (`CapNhatHoaDon` của máy chủ tablet); xóa bill nguồn; tắt đèn bàn nguồn.
- **Chuyển mặt hàng** (`ChuyenMatHang(bàn đích, dòng, SL)`): giống API tablet: `SL ≥ 0` tách một phần (trừ ở dòng gốc, dòng mới trên bill đích với cùng đơn giá, đơn vị, ghi chú, giá vốn, % giảm); `SL < 0` chuyển nguyên dòng (và con combo). Điều chỉnh bảng in bếp bằng `GetChenhLechInCheBienChuyenDo`: nếu mặt hàng đã in ≥ số còn lại sau chuyển thì chênh lệch = `đã in − số còn lại` (chỉ khi `SuDungChucNangInXuongBep=30` và loại đồ tương ứng bật in), ghi lại hai dòng `TINCHEBIEN` ngược dấu ở hai bill.
- **Cập nhật giá hàng khi chuyển bàn** (`CapNhatGia`), theo `SystemConfig.GiaMatHangKhiChuyenBan`: `0` chỉ nâng (dòng nào `DONGIA < giá mới` thì đổi), `1` luôn cập nhật theo giá khu vực mới, `2` không cập nhật. Giá khu vực mới: `DKHUVUC.DGIAMATHANGID` → `GIABAN` / `GIABAN2` / `GIABAN3` / `GIABAN4`. Dòng đổi giá: `THANHTIEN = giaMoi × SL − TIENGIAMGIA` (giảm theo tiền) hoặc `giaMoi × SL × (1 − TL/100)`; rồi tính lại cả bill. **Bản API tablet chỉ có cách "chỉ nâng" (bỏ qua cấu hình) và có lỗi dùng đơn giá cũ khi tính thành tiền** — khi viết lại nên dùng đúng cách của bản desktop.
- Phát hiện "cùng cách tính giờ" giữa hai bàn (`CungCachTinhGio`) có sẵn nhưng form hiện tại không dùng để chặn.

## 3. Trạng thái bàn trên bản đồ khu vực (`KhuVucControl`)

Trạng thái (`TrangThaiBan`) tính theo thứ tự ưu tiên:
1. Bàn có bill chưa thanh toán (`DBAN.TDONHANGID` trỏ tới bill `DATHANHTOAN = 0`): **Đã in tạm tính** nếu `SOLANINTAMTINH > 0`, ngược lại **Đang sử dụng**.
2. Ngược lại: `DBAN.TRANGTHAI = 1` → **Báo dọn**; `= 2` → **Dọn dẹp**; có phiếu sửa chữa chưa xong (`TSUACHUA.DASUAXONG = 0`) → **Sửa chữa**; có phiếu đặt trong ngày hôm nay với `TUGIO` nằm trong ±30 phút quanh hiện tại → **Đặt** (hiển thị "tên khách [điện thoại]-giờ" và ghi chú); còn lại → **Trống**.
- Hiển thị: nhóm bàn theo `DNHOMHIENTHI`; kích cỡ ô theo `KichThuocHienThiOGiaoDienDichVu` (0 = 64px kèm số phút, 1 = 48, 2 = 32, 3 = 24); ghi chú bill hiện trên ô khi `HienThiGhiChuTrenGiaoDienBan=30`; giờ bắt đầu / kết thúc (kết thúc không nhỏ hơn giờ hiện tại). Tải lại bằng luồng nền theo timer.
- Menu chuột phải: Mở bàn (khi bàn không ở trạng thái đang sử dụng / in tạm tính), Mở bàn từ đặt, Thanh toán / Hủy hóa đơn / Chuyển / Gộp / Ngắt giờ (khi bàn đang sử dụng hoặc đã in tạm tính; "Ngắt giờ" chỉ khi `Shared.CoNgatGio`). Kéo thả bàn sang bàn khác = chuyển hoặc gộp (`ChuyenGopBan`).
- **Hủy hóa đơn trên bàn**: quyền `HuyHoaDon`; chỉ khi bill chưa thanh toán, `|now − BATDAU| ≤ ThoiGianChoPhepHuyBill` phút và không thuộc nhóm gộp (`NHOMGUID` rỗng); `NhapLyDoKhiHuyHoaDon=30` thì bắt nhập lý do, không thì lý do mặc định "Hủy hóa đơn"; dùng `QuanLyBanHang.HuyBill` (lưu `TDONHANGHUY`, xóa bill, chi tiết, bảng in bếp).

## 4. Kiểm soát order (`KiemSoatOrder`)

Báo cáo kiểm tra số order viết tay trên các bill (ô `SOORDER`, danh sách số nguyên cách nhau dấu phẩy) theo khoảng ngày (mặc định ngày hiện tại):
- Với mọi số order của mọi bill trong khoảng: ghi nhớ bill chứa số đó. Số không phải số nguyên → lỗi "Lỗi số order … ở bill …". Một số order xuất hiện ở nhiều bill → "Trùng số order N, các bill: …".
- Sắp xếp các số và gộp thành các dải liên tục ("Tồn tại": `a-b`); các khoảng giữa hai dải là "Thiếu order a-b" (hoặc một số).
- Tóm tắt: số order lớn nhất, nhỏ nhất, tổng số order (không tính trùng), số order trùng.

## 5. Các hộp thoại bán hàng nhỏ

- **Đặt giờ cho mặt hàng theo giờ** (`DatGioChoMatHang`): sửa `TUGIO`, `DENGIO`, `GIOTINHLUONG` của dòng dịch vụ theo giờ; chỉ sửa được khi có quyền `DoiGioMatHangDichVuTheoGio`. Hợp lệ khi: có giờ kết thúc; kết thúc ≥ bắt đầu; bắt đầu ≥ giờ bắt đầu hóa đơn; kết thúc ≤ giờ kết thúc hóa đơn (nếu chưa có giờ kết thúc thì dùng giờ hiện tại). (Hàm lấy `GioTinhLuong` của mã gốc trả `dtBATDAU` — có vẻ là lỗi.)
- **Trả đồ** (`TraDo`, bill nhà hàng): liệt kê các mặt hàng đã gọi (loại trừ KM tự động, dòng xuất vật tư, dịch vụ theo giờ, dòng con combo), gộp theo (mặt hàng, đơn giá, % giảm, đơn vị) với `SLORDER = ΣSLXUATCHUAQUYDOI > 0`; người dùng nhập `SLTRALAI` hoặc `SLSUDUNG` (hai ô cân bằng: `SLTRALAI + SLSUDUNG = SLORDER`); nhập vượt thì đặt 0; xác nhận khi mọi `SLTRALAI ≤ SLORDER`.
- **Combo mở** (`ComboMo`): chọn mặt hàng thành phần của combo mở (mặc định lấy từ `DDINHLUONG` của combo, đơn giá bán hiện tại, giá vốn), không trùng mặt hàng, mỗi mặt hàng `SOLUONG > 0`, phải có ít nhất một dòng; kết quả được dùng làm dòng con của bill thay cho định lượng mặc định.
