# POS TAP — nghiệp vụ `Dịch vụ máy tính bảng` (SFORM `Dịch vụ máy tính bảng__12b301d8`, class `DichVuMayTinhBang`)

Nguồn: `code.cs` 3863 dòng. Là **máy chủ HTTP nhúng** (cổng 6868) chạy trên máy quầy, phục vụ app Android (`Data\app.apk`, tải trực tiếp từ `GET /`) để gọi món, chuyển/gộp bàn, in bếp, thanh toán. Cũng là "Dịch vụ in chế biến" thu xuống khay hệ thống. Dùng chung logic tổng bill của form nhà hàng (xem [pos-tap-nghiep-vu-nha-hang.md](pos-tap-nghiep-vu-nha-hang.md)) nhưng **có vài chỗ lệch** (mục 8).

## 1. Hạ tầng

- `TcpListener` port 6868, mỗi kết nối xử lý trên một `BackgroundWorker`, giao thức HTTP/1.0, trả JSON `{status:"OK"|"ERROR", data, message}`. Mọi API là `GET /<tên>?param=...` (tên tham số không phân biệt hoa thường). `POST` chuyển cho `PrintProcessor.exe` (in) qua file XML tạm.
- Mỗi request mở một kết nối DB riêng (`Config.Db.Copy()`), đóng sau khi xong.
- Thiết bị: tự khởi động cùng Windows (khóa `HKCU\...\Run` tên `POS6`); thu nhỏ xuống khay khi đóng cửa sổ.
- Đăng ký bản quyền thiết bị `dang-ky` (`DeviceId`, bảng `DBANQUYEN`): thiết bị mới được dùng thử 20 ngày; hết hạn mà chưa có `MADANGKY` thì báo "ĐÃ HẾT HẠN DÙNG THỬ". Thêm hàng khi chưa có `Serial`: chặn nếu số ngày phân biệt có bill `LOAI=0` > 30.
- **Bảo mật (không nên sao chép):** đăng nhập `dang-nhap-new` truyền `taikhoan`/`matkhau` qua query string, so sánh mật khẩu thô trong SQL ghép chuỗi (`SUSER.USERNAME`/`PASSWORD`), có chế độ đăng nhập chỉ bằng mã (`SUSER.USERID`). Mọi API chạy theo `USERID` do client gửi, nhiều API không kiểm quyền phía server. Viết lại phải dùng tham số hóa SQL và kiểm quyền ở server.

## 2. Danh sách API (GET)

`ping`, `dang-nhap-new`, `dang-ky`, `kiem-tra-table-locked`, `lock-table`, `unlock-table`, `trang-thai-ban`, `danh-sach-ban`, `anh-khu-vuc`, `anh-mat-hang`, `danh-sach-khach-hang`, `danh-sach-ly-do-huy`, `danh-sach-don-vi-tinh`, `danh-sach-ghi-chu`, `tao-hoa-don-trang`, `them-mat-hang-hoa-don`, `load-hoa-don`, `load-chi-tiet-hoa-don`, `hoa-don-dang-mo`, `thong-ke-hoa-don`, `chon-khach-hang`, `doi-so-khach`, `doi-so-luong`, `xoa-mat-hang`, `dat-ghi-chu`, `giam-gia-nhom`, `chuc-nang-khac`, `chuyen-gop-ban`, `chuyen-mat-hang`, `huy-hoa-don`, `in-che-bien`, `in-tam-tinh`, `prepare-thanh-toan`, `thanh-toan`, `man-hinh-bep`, `che-bien`, `tra-mon`.

## 3. Đăng nhập và dữ liệu nạp về thiết bị

- Trả về cờ cấu hình: `CoTienGio`, `CoThueSuat`, `CoPhiDichVu`, `SuDungGiaoDienThietKe`, `NhapMatKhauGiamDo` (+ `PasswordGiamDo`), `ChoPhepKhachNo`, `SuDungChucNangInXuongBep`, `TuDongInPhaCheSauKhiGoiMon`, `BatBuocInKhiThanhToan`, `GopChungChietKhauLamMot`, `NhapSoLuongKhiChonMatHang`, `BatBuocNhapNhanVienBanHang`, `NhapSoKhachKhiMoHoaDon`, `SuDungGia2/3/4` + `DienGiaiGia2/3/4`.
- Quyền theo chức năng: admin (`SUSER.ISADMIN = 1`) có hết. Người thường: `SuDungManHinhBep`, `ChuyenMatHangSangHoaDonKhac`, `ThayDoiGiamGiaTongHoaDon`, `ChuyenBan`, `GopBan`, `HuyHoaDon`, `ThongKeTrongSuDungDichVu`, `XemDonGiaTrongHoaDonBanHang` (= quyền thanh toán), `GiamGiaMatHang` (= được giảm giá); in tạm tính theo `SystemConfig.ChoPhepInTamTinh`. Kiểm quyền: `Shared.GetRoleByUser(user, chức năng)` rồi `DbUtils.CanDo(role, RoleMode.ViewNew/RemoveNew)`.
- Khu vực: `DKHUVUC` `STATUS=30` theo `SORTORDER`; nếu `PhanQuyenTruyCapTheoKhuVuc=30` và không phải admin: chỉ khu vực có trong `DTKTHEOKHUVUC` của user.
- Nhóm mặt hàng: loại trừ `DLOAIDOID = NguyenLieu (4)`, thêm 2 nhóm ảo "TẤT CẢ" và "TÌM KIẾM".
- Mặt hàng: `STATUS=30`, loại trừ `DLOAIMATHANGID = NguyenLieu`; trường `MATHANGMO = 30 nếu DLOAIMATHANGID='3'`; **không có quyền xem đơn giá thì giá 1–4 trả về 0**; thêm cột tìm không dấu `NAMEKODAU = CODE + ' ' + tên bỏ dấu`.
- Mã loại mặt hàng suy ra từ các câu SQL (cần đối chiếu enum `LoaiMatHang` trong DLL): `0` thường, `1` định lượng, `2` nguyên liệu/vật tư (loại khỏi tổng bill và danh sách), `3` mặt hàng mở, `4` combo, `5` combo mở, `7` dịch vụ theo giờ. Loại đồ `DLOAIDOID`: `0` đồ ăn, `1` đồ uống, `2` dịch vụ, `3` đồ khác, `4` nguyên liệu.

## 4. Bàn, hóa đơn, khóa bàn

- Danh sách bàn: có 2 kiểu theo `SuDungGiaoDienThietKe` (giao diện thiết kế: vị trí, kích thước, ghế, hình dạng, màu, trạng thái, số bàn sửa chữa chưa xong `TSUACHUA`, nhóm hiển thị, có đặt hàng hôm nay `TDATHANG.TUNGAY = CURRENT_DATE`, ghi chú bill, số lần in tạm tính; hoặc kiểu đơn giản: đơn giá khu vực, ghi chú, có hóa đơn hay chưa, đã in tạm tính). Chỉ bàn/khu vực `STATUS=30`.
- Khóa bàn (nhiều máy trạm): `lock-table` ghi `DBAN.TAIKHOANGIU = tên user`, `GIULUC = now`; `unlock-table` xóa. Bàn coi là **đang bị giữ nếu `GIULUC` trong vòng 120 giây gần nhất**. `trang-thai-ban` trả lỗi tên người giữ nếu có, ngược lại trả ID bill chưa thanh toán trên bàn.
- Tạo bill trống (`TaoHoaDonTrang`): giống desktop (BATDAU = now − `TuDongDoiGioVaoVeTruoc` phút, cắt giây; `SOKHACH = 1`; số bill `TaoSoHoaDon`; KM giảm giá mặc định; phí dịch vụ/thuế mặc định) và thêm: `DNHANVIENXUATID = SUSER.DNHANVIENID` của người đăng nhập. Báo lỗi "Bàn này đã có hóa đơn" nếu `DBAN.TDONHANGID` có giá trị.
- Đổi số khách: `SOKHACH = max(1, n)`; chọn khách: gán `DKHACHHANGID`, không tính lại giảm giá theo nhóm khách ở API này. Hóa đơn đã thanh toán thì mọi API sửa đều báo lỗi.
- Bill đang mở: `LOAI=0 AND COALESCE(DATHANHTOAN,0)=0`; mỗi bill hiển thị món đầu tiên (không phải nguyên liệu `2`, không phải theo giờ `7`, không phải combo con); không có quyền xem giá thì `TONGCONG = 0`. Thống kê trong ngày theo `USERTHANHTOANID`, `NGAY = ngày DB`, bill đã thanh toán.

## 5. Thêm / sửa / xóa món

- `them-mat-hang-hoa-don`: nếu chưa có bill thì tạo bill trống. Đơn giá: mặt hàng mở hoặc `GIATHEOTHOIGIA=30` → giá client gửi; nếu bật giá 2/3/4: lấy giá theo khu vực của bàn + giá theo giờ (`LayGiaMatHangTheoGio`), nếu khu vực để `HIENTHILUACHON` thì lấy giá client gửi.
- Gộp dòng nếu `NhapMotMatHangNhieuLanTrongPhieu=0`, không phải mặt hàng mở / theo giờ và **cùng mặt hàng + cùng đơn giá + cùng ghi chú + cùng % giảm**: cộng `SLXUATCHUAQUYDOI`, `SLXUAT = SLXUATCHUAQUYDOI`, tính lại `THANHTIEN`, cập nhật SL con của combo.
- Dòng mới: `DTRANGTHAICHEBIENID = CHO_CHE_BIEN`; `GIAVON = mặt hàng.GIAVON`; `COMBOID = GUID`; kho = kho của khu vực; dịch vụ theo giờ: `TUGIO = now`, SL = 0; combo: sinh dòng con (đơn giá 0, `COMBOSL`, `COMBOPARENTID`) — **kho dòng con dùng `Shared.DKHOHANGID` (kho mặc định), không phải kho khu vực** (khác desktop).
- `%` giảm dòng khi thêm: `GetKhuyenMaiTheoMatHang(now, mặt hàng)` nếu `KichHoatKhuyenMaiTuDong=30`.
- `doi-so-luong`: không cho giảm khi bill đã in tạm tính (`SLXUAT > SL mới` và `KiemTraDaInTamTinh`); giảm xuống dưới số đã in xuống bếp bị chặn khi `KhongDuocGiamDoSauKhiInPhaChe=30` hoặc user thiếu quyền `XoaGiamMonSauKhiInCheBien`; sau đó `SLXUATCHUAQUYDOI = SL mới`, tính lại bill, cập nhật SL con combo. Mặt hàng dịch vụ theo giờ: bỏ qua (không làm gì).
- `xoa-mat-hang`: cùng điều kiện đã in bếp / in tạm tính; xóa cả dòng con combo; lưu lý do vào lưu vết.
- `dat-ghi-chu`: gán `NOTE` dòng. `giam-gia-nhom`: 4 tỉ lệ 0–100 (đồ ăn / uống / khác / dịch vụ) gán `TILEGIAMGIA` cho từng dòng theo `DNHOMMATHANG.DLOAIDOID`; **không so với KM tự động** như form desktop.
- `chuc-nang-khac` (`Field`): 1 = giảm % hàng (gộp: `TILEGIAMGIATONG`, đặt `GIAMTONGTHEOTIEN = 0`), 2 = giảm tiền hàng (gộp: `TIENGIAMGIATONG`, `GIAMTONGTHEOTIEN = 30`), 3 = giảm % giờ, 4 = giảm tiền giờ (**mã gốc ghi nhầm vào `TILEGIAMGIAGIO`**), 5 = % phí dịch vụ, 6 = % thuế. Không kiểm quyền ở server.

## 6. Tính tổng bill phía máy chủ (`CapNhatHoaDon`)

Chạy sau mỗi thao tác sửa; trả về cho app các giá trị tiền (số và chuỗi `#,#0.##`), cùng khách, bàn, giờ bắt đầu/kết thúc và danh sách dòng.
- Chỉ ghi DB khi bill chưa thanh toán. Dòng tính vào tiền hàng: không phải combo con, `DLOAIMATHANGID <> '2'`.
- Tiền giờ (khi `CoTienGio`): giống form nhà hàng — `KETTHUC = max(KETTHUC, now + TuDongDoiGioRaVeSau)` nếu `TUTHAYDOIGIO ≠ 30` (và không bị chặn bởi in tạm tính), `Shared.TinhGia(...)`, làm tròn nghìn `AwayFromZero`; dòng dịch vụ theo giờ: `SL = max(0, Round(giờ(TUGIO→DENGIO hoặc KETTHUC), 2))` — **không áp `LamTronMatHangDichVuTheoGio`** (khác desktop). Không có tiền giờ thì `KETTHUC = now`.
- Phần còn lại giống mục 6 của file nhà hàng (gộp / không gộp giảm giá, phí dịch vụ, thuế, `TONGCONG`), nhưng **ở nhánh không gộp, cả giảm giá hàng và giảm giá giờ đều dùng chung cờ `GIAMTHEOTIEN`** (desktop dùng thêm `GIAMGIAGIOTHEOTIEN`).

## 7. Chuyển / gộp bàn, chuyển món, hủy bill

- Quyền: `ChuyenBan` nếu bàn đích chưa có bill, `GopBan` nếu đã có bill. Bàn đích bị giữ (nhiều máy trạm) thì chặn.
- **Chuyển bàn**: nếu có tiền giờ: tạo dòng `TDONHANGGIO` cho bàn cũ (từ `BATDAUPHONGCUOI` đến now, đơn giá/bảng giá cũ, thành tiền `Shared.TinhGia`), rồi `TUTHAYDOIGIO = 0`, `BATDAUPHONGCUOI = now`, `KETTHUC = now` nếu nhỏ hơn. Cập nhật giá hàng theo khu vực mới (`CapNhatGia`: **chỉ nâng giá** — dòng nào `DONGIA < giá mới` thì đổi; khi giảm theo % mã gốc tính lại `THANHTIEN` bằng đơn giá cũ, có vẻ là lỗi). Cập nhật cách tính giờ / bảng giá / đơn giá theo bàn mới, `TIENMOBAN = max(mới, cũ)`. In phiếu thông báo chuyển bàn xuống bếp nếu `SuDungChucNangInXuongBep` và `InThongBaoKhiChuyenBan` (theo loại đồ đã in: ăn / uống / khác / dịch vụ, tùy `InDoAn/InDoUong/InDoKhac/InDichVu`). Bật đèn bàn mới, tắt đèn bàn cũ.
- **Gộp bàn** (chuyển vào bill khác): từ chối nếu bill nguồn có tiền giờ > 0 ("có tiền giờ"), nếu bill nguồn tạo từ đặt hàng (`TDATHANGID`), hoặc nếu `TILEGIAMGIA` / `TILEGIAMGIAGIO` hai bill khác nhau. Thực hiện: cập nhật giá theo khu vực đích, thêm dòng `TDONHANGGIO` vào bill đích (thành tiền = `TinhGia` từ BATDAU đến now, 0 phút KM), lưu bill nguồn vào bill hủy ("Gộp vào hóa đơn …"), chuyển toàn bộ `TDONHANGCHITIET` và `TINCHEBIEN` sang bill đích, tính lại bill đích, **xóa bill nguồn**.
- **Chuyển mặt hàng sang bill khác** (`chuyen-mat-hang`, quyền `ChuyenMatHangSangHoaDonKhac`): bàn đích phải có bill chưa thanh toán. `SOLUONG ≥ 0`: tách một phần — trừ SL dòng gốc, thêm dòng mới trên bill đích (cùng đơn giá, giá vốn, % giảm, không gộp với dòng có sẵn); `SOLUONG < 0`: chuyển nguyên dòng (và con của combo). Điều chỉnh lại bảng in chế biến (`ChuyenBan.GetChenhLechInCheBienChuyenDo`): trừ lượng đã in ở bill nguồn và cộng sang bill đích. Ghi lưu vết ở cả 2 bill.
- **Hủy hóa đơn** (`huy-hoa-don`, quyền `HuyHoaDon` mức `RemoveNew`): chỉ khi chưa thanh toán, `|now − BATDAU| ≤ SystemConfig.ThoiGianChoPhepHuyBill` phút, và không thuộc nhóm gộp (`NHOMGUID` rỗng). Thực hiện: lưu bill hủy (`QuanLyBanHang.LuuBillHuy(user, bill, lý do)`) rồi xóa `TDONHANG`, `TDONHANGCHITIET`, `TINCHEBIEN` của bill.

## 8. Thanh toán trên máy tính bảng (khác desktop)

- `prepare-thanh-toan`: cần quyền `XemDonGiaTrongHoaDonBanHang`; từ chối nếu đã thanh toán; tính lại toàn bộ khi nhiều máy trạm; trả khách, bàn, số bill, `TONGCONG`.
- `thanh-toan` (quyền `Functions.ThanhToan`): chỉ nhận `KHACHDUA` và cờ `PRINT`:
  - `KHACHDUA > TONGCONG`: `TIENMAT = TONGCONG`, `TRALAI = KHACHDUA − TONGCONG`.
  - Ngược lại: `TIENMAT = KHACHDUA`, `TRALAI = 0`, `CONLAI = TONGCONG − KHACHDUA`.
  - Đặt `PASSWIFI`, `DATHANHTOAN = 30`, `USERTHANHTOANID`, `TIENTHANHTOAN = TIENMAT`, `GIOTHANHTOAN = now`; xóa vật tư cũ rồi `XuatVatTu`; xóa `TINCHEBIEN`; in hóa đơn nếu `PRINT`.
- **Không làm**: chuyển khoản, thẻ, thẻ trả trước, voucher, trừ tích lũy, công nợ (`CONNO`, `CONGNO`), `NOCU`, `DATTRUOC`, `DIEM` / `DIEMGIAM`, `SOTT`, nâng nhóm khách, ghi `NGAY` giao dịch, bill gộp nhóm. App tablet chỉ thu tiền mặt đơn giản; cần chốt với Chiến có giữ nguyên hạn chế này không.

## 9. In bếp / pha chế (`in-che-bien`) và màn hình bếp

- `SuDungChucNangInXuongBep=30` mới dùng. Bill đã thanh toán thì chặn.
- Danh sách món cần in từ `InCheBienTs.GetSqlInCheBien(bill)` (SQL nằm trong DLL/form `In chế biến` — xem phần form `In chế biến`), tách theo `DLOAIDOID`: `0` ăn → máy in bếp, `1` uống → bar, `3` khác, `2` dịch vụ, mỗi loại chỉ khi `InDoAn / InDoUong / InDoKhac / InDichVu = 30`. Máy in theo khu vực từ `InCheBien.LayMayInTheoCauHinh(khuvực)`; có món cần in mà máy in chưa cấu hình ("(CHỌN MÁY IN)") thì báo "CHƯA CẤU HÌNH MÁY IN ĐỒ ĂN / UỐNG / KHÁC / DỊCH VỤ". `InRiengDoAnUong=30`: in riêng từng loại; ngược lại gộp các loại cùng máy in thành một phiếu (tiêu đề ghép "đồ ăn, đồ uống"). Không có món nào thì "KHÔNG CÓ MẶT HÀNG NÀO ĐỂ IN" (trừ khi `thoat=true` — tự động thì trả OK). Ghi lưu vết nếu `LuuVetInCheBien=30`.
- Màn hình bếp (`man-hinh-bep`): theo bill (`viewbyOrder=1`): mỗi bill kèm "Bàn: X - Tầng: Y", các dòng chưa trả món (`DTRANGTHAICHEBIENID` khác `'3'`) của mặt hàng loại 0, 1, 3, 4, 5; cờ `DANGCHEBIEN = 1` nếu trạng thái khác `'0'`. Theo món (`viewbyOrder=0`): gộp theo (tên, ghi chú, đơn vị) với `SOLUONG`, `SLCHEBIEN` (= SL dòng có trạng thái 1), `SLDANGCHO = SOLUONG − SLCHEBIEN`.
- `che-bien`: `DTRANGTHAICHEBIENID = DANG_THUC_HIEN`; `tra-mon`: `= DA_TRA_MON` (mã `'3'`). Trạng thái ban đầu `CHO_CHE_BIEN`.
- `in-tam-tinh`: quyền `Functions.InTamTinh`; dùng `TDONHANG0Ae.PrintInvoiceTablet(...)` (tăng `SOLANINTAMTINH`, `INTAMTINHLUC`, giới hạn `SoLanInTamTinhToiDa`).
