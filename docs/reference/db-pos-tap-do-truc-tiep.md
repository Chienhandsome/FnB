# Số đo trực tiếp trên DB POS TAP thật (`POS TAP\Database\DATA.fdb`)

- **Ngày đo:** 19/11/2026. Đây là **chuẩn** cho mọi số liệu metadata/cấu hình/schema trong `docs/`; chỗ nào bản rà vòng 2 (đọc `FNB_DEMO.fdb`) hoặc spec cũ (đọc bản TEA/bản sao cũ) khác số này thì sửa theo số này.
- **Cách đo (chỉ đọc):** chép `POS TAP\Database\DATA.fdb` sang thư mục tạm, biên dịch một công cụ console x86 nhỏ (`csc.exe`, tham chiếu `FirebirdSql.Data.FirebirdClient.DLL` + `fbembed.dll` của chính POS TAP, `ServerType=1` embedded, `SYSDBA/masterkey`), chạy `SELECT` trên **bản chép**. Không mở file gốc, không ghi gì. (Python trên máy là 64-bit nên không nạp được `fbembed.dll` 32-bit.)
- **Lưu ý trạng thái DB:** file `DATA.fdb` gốc có thời điểm sửa 19/11 15:58 (cùng kích thước 19.505.152 byte với `Data\TEMPLATE.FDB`). Dữ liệu nghiệp vụ gần như trống: `TDONHANG`, `TDONHANGCHITIET`, `DKHACHHANG`, `DNHANVIEN`, `TTHUCHI`, `DNHOMKHACHHANG` = 0 dòng; `DMATHANG` 1, `DKHOHANG` 1, `DBAN` 1, `DKHUVUC` 1, `DCUAHANG` 1, `SUSER` 1 (`Admin`, tên hiển thị "Administrator", mật khẩu rỗng, `ISADMIN = 1`, nhóm trỏ tới ID không có trong `SGROUPUSER`). Bản sao `DEMO.FDB` cùng thư mục có dữ liệu (581 mặt hàng / 573 `STATUS=30`, 13 nhân viên, 19 bàn, 3 khách, 4 đơn, 3 phiếu thu chi, thêm user `phucvu1`) — **không phải** `FNB_DEMO.fdb` mà bản rà vòng 2 đọc (khác số form/khóa; xem mục 6).

## 1. Quy mô

| Mục | Số đo |
|---|---|
| Bảng người dùng | **100** (`D*` 54, `T*` 26, `S*` 20); không view, không stored procedure |
| `SFORM` | 111 (26 form có `TSCODE`); form có menu trỏ tới: xem mục 4 |
| `SREPORT` | **128 dòng, 127 `STATUS = 30` + 1 `STATUS = 0`** — dòng tắt là "BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN" |
| `SREPORTTEMPLATE` | 156 |
| `SREPORTROLE` | 196 (cột `SGROUPUSERID`, `SREPORTID`, `MODE`, `STATUS`) |
| `SMENU` | 78 dòng, **77 `STATUS = 30`**; 11 mục `TOOLBAR = 30` |
| `STABLEDESC` | 94 (`TABLETYPE`: NULL 9, `0` 40, `2` 12, `3` 13, `4` 20) |
| `SCOLUMN` | 752 |
| `SGRID` | **2 dòng**: `SGRID.ID` = ID `STABLEDESC` của `DMATHANG` và `DKHACHHANG` (nối bằng `SGRID.ID`, **không** phải `GRIDID`; `GRIDID` rỗng ở cả 2 dòng) |
| `SFUNCTION` | **86** (Bán hàng 20, Báo cáo 1, Cơ sở dữ liệu 3, Công nợ 2, Danh mục 11, Hệ thống 7, Hóa đơn bán hàng 18, Kho hàng 9, Nhân sự 5, Quản trị 5, Quỹ 5); `NOTE = 'View'`: Bán hàng 11, Báo cáo 1, CSDL 3, Công nợ 2, Hệ thống 7, Hóa đơn bán hàng 17, Kho 4, Quản trị 4, Quỹ 2 |
| `SGROUPROLE` | **268** dòng: Quản lý 81, **Kế toán 73**, Phục vụ 67, **Thu ngân 35**, Thủ kho 12; `MODE` ∈ {0: 104, 16: 28, 80: 8, 112: 2, 240: 126} |
| `SGROUPUSER` | 5: Thu ngân, Kế toán, Thủ kho, Quản lý, Phục vụ |
| `SIMAGE` | 272 |
| `SCONFIG` | **205 khóa** (`DATATYPE` 1/3/5; `CONTROLTYPE` 3 ô số 30, 5 ô chữ 16+2, 6 nhiều dòng 1, 7 ảnh 2, 8 danh sách bảng 5, 9 ô tích 118, 10 combo 20; `TAB = 30`: 11 khóa) |
| `SCONFIGGROUP` | **18 nhóm**: Thông tin chung (7 khóa), Máy in mặc định (0), In HĐ và in chế biến (22), Mẫu hóa đơn (0), Số phiếu (0), Bán hàng (26), Giờ trên bill (14), Thanh toán (11), Tích điểm (4), Kho hàng (10), Mặt hàng (14), Thiết bị khác (14), Cảnh báo (4), Quản trị (21), Cảm ứng (5), Tùy chọn khác (13), Ứng dụng (3), Ẩn (28); **9 khóa không thuộc nhóm nào**; **không có nhóm "Khách sạn"** |
| `DNGANHANG` | **không tồn tại**; `DTAIKHOANNGANHANG` 3, `DLOAIQUY` 3 |
| `DUNGDUNG` | 9 (COFFEE 0, POS 1, KARAOKE, BIDA, HOTEL, MASSAGE, HOTEL-KARAOKE, PS3, SPA); `SCONFIG.UngDung`: `INTVALUE = 0`, `TEXTVALUE = '1'` (POS) |
| `SSMSTEMPLATE` | 0 dòng |

Khóa `SCONFIG` không có `CAPTION` (nội bộ, không hiển thị): `DatabaseVersion`, `Application`, `CompliedCode`, `SharedCode`, `Profile`, `AppAutoVersion`, `ChonNgayLamViecKhiDangNhap`, `BATCH_ID`, `LAST_EXPORT`, `TraDoThucHienAm`, `TinhLuongSuDungMayChamCong`, và hai khóa số dài `140242-47212-…-31490` / `140243-47212-…-4488`.

## 2. Schema các bảng hay bị nhầm

- **`DKHACHHANG` — 21 cột:** `ID, NAME, NOTE, DNHOMKHACHHANGID, STATUS, USERMODIFIEDID, TIMEMODIFIED, TIMECREATED, USERCREATEDID, MAKHACH, DIACHI, DIENTHOAI, EMAIL, MASOTHUE, DNHANVIENID, NGAYSINH, DIEMTICHLUYBANDAU (INTEGER), GIABAN (INTEGER), DTINHTHANHID, FACEBOOK, DTHETRATRUOCID`. **Không có** `SOCMND`, `NGAYCAP`, `NOICAP`, `CANCUOCCONGDAN`, `TENDONVI`, `NGUOIMUAHANG`, `PHUONGTHUCTT`, `SOTAIKHOAN`, `NGANHANG`.
- **`DNHOMKHACHHANG` — 22 cột:** `ID, NAME, NOTE, STATUS, USERMODIFIEDID, TIMEMODIFIED, TIMECREATED, SORTORDER, USERCREATEDID, PARENTID, PARENTDIR, ITEMTYPE, AUTOID, SIMAGEID`, và 8 cột `NUMERIC(18,2)`: `TILEGIAMGIA, DIEMTICHLUY, TILEGIAMGIATIENHANG, TILEGIAMGIATIENGIO, TILEGIAMDOAN, TILEGIAMDOUONG, TILEGIAMDICHVU, TILEGIAMDOKHAC`.
- **`DMATHANG` — 39 cột, KHÔNG có `LOAIDINHLUONG`** (có `DLOAIMATHANGID`, `QUYDOI`, `DDONVITINHCHANID`, `GIABANCHAN`, `COHANSUDUNG`, `SIZE`, `GIOHATCOMBO`…). Code bán lẻ (`Hóa đơn bán hàng`, `Quản lý bán hàng`) có 4 chỗ dùng `LOAIDINHLUONG` → xem mục 5.
- Có trong POS TAP: `DNHANVIEN.LUONGTHEOCA/CODE/DCALAMVIECID`, `DCALAMVIEC.TUGIO/DENGIO`, `DMATHANG.GIOHATCOMBO`, `TDONHANG.GIOTRADO/PASSWIFI`, `TDONHANGCHITIET.GIOTINHLUONG`, `TINCHEBIEN.DONGIA`, `DBANQUYEN.MADANGKY/KETNOILUC`, `TBANGLUONGTONGHOP.SOGIOLAM`, bảng `DMATKHAUWIFI`, `DNHOMWIFI`, `TVAORA`. Không có: `DBANGGIAGIO`, `DBANGGIANGAYDEM`, `DBAN.ROOMADDRESS`, `TSUACHUA.NGAYSUAXONG`.
- **`SCOLUMN` của `DKHACHHANG` (15 dòng):** `NAME` (`ALLOWEMPTY=NO`, `ALLOWDUPLICATE` = cấu hình `ChoPhepTrungTenKhachHang`), `MAKHACH` (`ALLOWEMPTY=NO`, `ALLOWDUPLICATE=NO`, `DEFAULTCONFIGID` = `{KH(*****)}`, `SHOWONLOOKUP=30`), `DIACHI`/`DIENTHOAI` (`SHOWONLOOKUP=30`), `GIABAN` (`ENABLECONFIGID` = `SuDungGiaTheoKhach`), `DTHETRATRUOCID` (`ENABLECONFIGID` = `SuDungTheTraTruoc`), còn lại không ràng buộc. `SCOLUMN` của `DNHOMKHACHHANG`: `NAME` `ALLOWEMPTY=NO` + `ALLOWDUPLICATE=NO`; 8 cột còn lại kiểu Number.
- **`STABLEDESC` `DKHACHHANG`:** `TABLETYPE = 2`, `CATEGORYCOL = DNHOMKHACHHANGID`, `SFUNCTIONID = 36c3bad1-9d2e-4916-98e0-efb6c0e681e2`, **`FORMAT = NULL`**, `GENNO = 0`. Menu "Danh mục khách hàng": `SHORTCUT = 131147` (Ctrl+K), `STABLEDESCID` = `5fcc571a-662d-4953-a83f-6004c732f439`. Quyền `36c3bad1…`: Quản lý 240, Kế toán 240, Thu ngân 0, Phục vụ 0.
- **Dialog khách (layout.xml thật):** 466×383, một cột, **15 trường**: `NAME, NOTE, DNHOMKHACHHANGID, MAKHACH, DIACHI, DIENTHOAI, EMAIL, MASOTHUE, DNHANVIENID, NGAYSINH, DIEMTICHLUYBANDAU, GIABAN, DTINHTHANHID, FACEBOOK, DTHETRATRUOCID`.
- **`SGRID` `DKHACHHANG`:** `NAME 97, DNHOMKHACHHANGID 130, MAKHACH 68, DIACHI 186, DIENTHOAI 139, EMAIL 139, MASOTHUE 139, DNHANVIENID 78, DIEMTICHLUYBANDAU 105, NGAYSINH 80` (hiện) và `NOTE 150` (ẩn). `SGRID` `DMATHANG`: `CODE 63, NAME 217, DDONVITINHID 107, GIANHAP 99, MASANCO 70, GIABAN 69, DDOITACKYGUIID 150, HOAHONG 87` (hiện); `TONTOIDA, TONTOITHIEU, GIABAN2, NOTE, GIAVON, DNHOMMATHANGID` (ẩn).

## 3. Giá trị cấu hình (`SCONFIG`) quan trọng

| Khóa | Giá trị | Khóa | Giá trị |
|---|---|---|---|
| `UngDung` | `TEXTVALUE='1'` (POS) | `CoTienGio` | 0 |
| `ChoPhepKhachNo` | 0 | `LamTronTien` | 1000 |
| `ThoiGianChoPhepHuyBill` | 50 (phút) | `GiaMatHangKhiChuyenBan` | 0 |
| `SuDung2DonViTinh` | 30 | `TuDongTinhGiaVon` | 30 |
| `GiaSuDungKhiChuyenXuatKho` | **1** (giá nhập) | `MatHangCoHanSuDung` | 0 |
| `MatHangCoKichThuoc` | 0 | `SuDungNhieuKho` | 0 |
| `NhapMotMatHangNhieuLanTrongPhieu` | 0 | `ThongBaoTruocNgay` | 0 |
| `ChieuDaiMaVach` | 7 | `TuDongSinhMaVach` | 0 |
| `CachTinhDiem` | **1** (cộng `DIEM` từng bill) | `QuyDoi1DiemSangTien` | 1000 |
| `DoanhSoTuongUngVoi1Diem` | 20000 | `TuDongNangCapThanhVienKhiDatHanMuc` | 30 |
| `HienThiDiemCuaKhachHangTrenHoaDon` | 0 | `ChoPhepTrungTenKhachHang` | 0 |
| `GopChungGiamGiaTienGioVaTienHangLamMot` | 0 | `CachChonKhachHang` | 0 |
| `BatBuocNhapKhachHang` | 0 | `SoHoaDonQuayVongTheo` | 1 |
| `CoThueSuat` / `CoPhiDichVu` | 0 / 0 | `MacDinhThueSuat` | 0 |
| `KichHoatLuuVetHoatDong` | 30 | `LuuVetTrong` / `SoNgayLuuVet` | 20 / 30 |
| `PhanQuyenTruyCapTheoKhuVuc` | 0 | `InHoaDonTheoKhuVuc` | 0 |
| `TinhLuongSuDungMayChamCong` | NULL (chưa đặt) | `IpMayChamCong` | rỗng (mặc định `192.168.1.201`) |
| `KichHoatDongBo` | 0 | `CanhBaoChuongTrinhKhuyenMai` / `ThongBaoKhachDenNgaySinhNhat` | 0 / 0 |
| `SoFileSaoLuu` / `TuDongSaoLuuDuLieu` / `SoNgaySaoLuu` | 10 / 30 / 1 | `SuDungGiaTheoKhach` / `SuDungTheTraTruoc` / `SuDungGia2` | 0 / 0 / 0 |
| `DienGiaiGia2/3/4` | "Giá 2" / **"Giá 2"** (sai chữ, seed thật) / "Giá 4" | `GiaoDienChuongTrinh` / `DoToChuHienThi` | 0 / 0 |

## 4. Menu ↔ form

- Form **không có menu `STATUS=30` nào trỏ tới**: `Hóa đơn bán hàng` (bán lẻ), `Quản lý bán hàng`, `Quản lý hóa đơn bán hàng`, `Hóa đơn nhà hàng`, `Sinh dữ liệu`, `Tính lương bằng máy`, `Lấy dữ liệu từ máy` (và 81/111 form tổng cộng — phần lớn là hộp thoại mở từ form khác). Có menu: `Quản lý bán hàng nhà hàng` (2), `Tính lương` (2).
- 8 `SFUNCTION` **không nhóm nào được cấp quyền** (`MODE > 0`): "Điều chỉnh giờ tính lương dịch vụ theo giờ", "Sử dụng màn hình bếp", "Thay đổi khách hàng trong điều chỉnh hóa đơn", "Danh mục mật khẩu wifi", "In lại bill", "Xóa giảm món", "Xóa giảm món sau khi in chế biến", "Bản quyền máy tính bảng". **Không có** hai chức năng e-invoice `9998/9999` trong DB thật.

## 5. Hệ quả đối với kết luận cũ

- **Báo cáo "thừa" 128 vs 127:** là một báo cáo **bị tắt** (`STATUS = 0`), không phải báo cáo chỉ có ở bản DEMO.
- **`LOAIDINHLUONG` không tồn tại trong `DATA.fdb` thật**, đồng thời form bán lẻ dùng nó **không có menu** trong POS TAP → suy luận vòng 2 "cột phải tồn tại trong DB thật" là **sai**; đúng hơn: luồng bán lẻ `Hóa đơn bán hàng` (đặc biệt `XuatVatTu` bán lẻ) không phải đường chạy chính ở bản POS TAP này (đường chính là `Hóa đơn nhà hàng` qua `Quản lý bán hàng nhà hàng`, dùng `DLOAIMATHANGID='1'`). Chạy nhánh bán lẻ trên DB này sẽ lỗi SQL. Chiến chốt có đưa bán lẻ vào phạm vi không.
- **`DLOAIHINHKHUYENMAI` chỉ có 5 loại** (Giảm giá tổng bill, theo nhóm hàng, % theo sản phẩm, tiền theo sản phẩm, "Mua x tặng y"): các loại "giảm theo giá trị đơn hàng", "giảm theo số lượng", "đồng giá", "tặng theo trị giá" **không chọn được** trong app; loại "tiền theo sản phẩm" (`8be106e5…`) chọn được nhưng không có nhánh xử lý.
- **`DLOAIDO`:** 0 Đồ ăn, 1 Đồ uống, 2 Dịch vụ, 3 Đồ khác, 4 Nguyên liệu. **`DGIAMATHANG`:** 0 Hiển thị để lựa chọn, 1 Giá chung, 2 Giá bán t2, 3 Giá bán t3, 4 Giá bán cafe. **`DLOAIMATHANG`:** 8 dòng (0 kiêm vật tư … 7 dịch vụ theo giờ).

## 6. Vì sao khác bản rà vòng 2

Bản rà vòng 2 đọc `FNB_DEMO.fdb` (112 form, 212 khóa `SCONFIG`, `DKHACHHANG` 26 cột, có `DNGANHANG`, 2 chức năng e-invoice, `LOAIDINHLUONG` thiếu). `DATA.fdb` thật: 111 form, 205 khóa, `DKHACHHANG` 21 cột, không `DNGANHANG`, không e-invoice. Mọi điểm của bản rà vòng 2 dựa vào các thứ DEMO có mà DB thật không có (cột 26 của khách, bảng ngân hàng, e-invoice, `UngDung` 2 dòng…) không áp dụng cho POS TAP.
