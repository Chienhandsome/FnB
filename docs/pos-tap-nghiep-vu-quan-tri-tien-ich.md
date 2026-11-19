# POS TAP — nghiệp vụ quản trị và tiện ích

Nguồn: SFORM `Nhập dữ liệu__2460ed2f` (942 dòng), `Đánh lại mã hàng__13880bab`, `Xuất lại vật tư__fca03f03`, `Import định lượng__c394a5ee`, `Lưu vết hoạt động__ac206bb6`, `Tài khoản người dùng__c21d52e6`. Đây là các chức năng quản trị / chuyển đổi dữ liệu, ít ảnh hưởng nghiệp vụ bán hàng nhưng cần biết khi làm phần quản trị và chuyển dữ liệu.

## 1. Nhập dữ liệu từ cơ sở dữ liệu phiên bản cũ (`NhapDuLieu`)

- Chọn file `.fdb` cũ + mật khẩu bảo vệ (hằng số `6789` trong mã). Kết nối embedded tài khoản mặc định `SYSDBA` (mật khẩu mặc định của Firebird). Các nhóm dữ liệu chọn nhập (đã có dữ liệu thì vô hiệu hóa): sao lưu trước (`FbBackup` ra `Data\Backup\bak_<ngày giờ>.gbk`), danh sách nhân viên + ca, mặt hàng + nhóm + định lượng + đơn vị tính, bàn khu vực + kho + bảng giá, khách hàng + nhóm, nhà cung cấp + nhóm, dữ liệu giao dịch (kho, bill, chi tiết bill, lý do thu chi, thu chi, tài khoản người dùng không phải admin), thông tin cửa hàng (`CompanyName/Phone/Address/Email/Fax`, `Logo` trong `SCONFIG`).
- Nhập từng bảng bằng cách ghép cột cùng tên (cột không có ở bảng đích bị bỏ), có bảng ánh xạ đổi tên cột cho cấu trúc cũ:
  - `TDONHANG`: `TIENNUOC→TIENHANG`, `TIENBAN→TIENGIO`, `SO→NAME`, `LANIN→SOLANINTAMTINH`, `LANINBILL→LANINHOADON`, `DONGIAGIO→DONGIA`, `TONGTHUE→TIENTHUE`, `DTHEID→DTHETRATRUOCID`, `PHIDVTHEOTIEN→PHIDICHVUTHEOTIEN`, `GIAMGIOTHEOTIEN→GIAMGIAGIOTHEOTIEN`, `TILEGIAMGIA→TILEGIAMGIAGIO`, `TONGGIAMGIA→TIENGIAMGIAGIO`, `TILEGIAMGIADOUONG→TILEGIAMGIA`, `TONGGIAMGIADOUONG→TIENGIAMGIA`. Phiếu nhập xuất cũ `TNHAPXUAT` gộp vào `TDONHANG` với `LOAI`: `LAPHIEUNHAP = 30 → 1` (nhập), `0 → 2` (xuất), `60 → 4` (kiểm kê), khác → 3 (chuyển kho); hình thức thanh toán cũ `LOAITHANHTOAN` 0/1/khác → `TIENMAT`/`CHUYENKHOAN`/`THE`.
  - Chi tiết cũ (`TDONHANGCHITIET`, `TNHAPXUATCHITIET`, `TXUATVATTU`) gộp vào một bảng: bán `SOLUONG→SLXUAT`; nhập xuất `SOLUONGXUAT`/`SOLUONG`; xuất vật tư cũ thành dòng `XUATVATTU=30`. Sau nhập: `TIENGIAMGIA = (SLXUAT + SLNHAP) × DONGIA − THANHTIEN`, `SLNHAP/SLXUAT` = số chưa quy đổi, `DDONVITINHID` = đơn vị cơ sở mặt hàng; bill: `TIENGIAMGIATONG = TIENGIAMGIA + TIENGIAMGIAGIO`, `TIENHANGCHUAGIAM = Σ (SLXUAT + SLNHAP) × DONGIA`, `GIAMGIAMATHANG = TIENHANGCHUAGIAM − TIENHANG`, `TILEGIAMGIATONG = TIENGIAMGIATONG × 100 / TONGCONG`.
  - `DMATHANG`: `LOAISANPHAM` cũ 0 → định lượng, 1 → kiêm vật tư, 2 → nguyên liệu, khác → dịch vụ; `DONGIA..DONGIA4 → GIABAN..GIABAN4`. `DKHUVUC`: `TABHIENTHI − 1`, `CACHTINHGIAGIO → CACHTINHGIO`, biểu tượng khớp theo tên ảnh. `DBANGGIA`: `COBANGGIANGAYLE→CONGAYLE`, `APDUNGCATHU7 > 0 → COCUOITUAN = 30`. `DKHACHHANG`: `CODE→MAKHACH`; `DNHACUNGCAP` (từ `CNHACUNGCAP`): `CODE→MANHACUNGCAP`; nhóm khách từ `DTHANHVIEN` với ánh xạ các cột giảm giá theo loại đồ. `TTHUCHI` cũ: `LAPHIEUTHU=30` → `LOAI 0` (thu) ngược lại `LOAI 1` (chi), `SOTIEN` chia vào `THU`/`CHI`, `LOAIDOITUONG` cũ 0/1/2/khác → 1/2/3/0.
  - Lỗi trùng lặp ghi vào `Data\import_log.txt`.
- Thứ tự nhập: nhân viên, mặt hàng, bàn khu vực (kho, khu vực, bảng giá, bàn), khách hàng, nhà cung cấp, dữ liệu giao dịch (xóa lý do thu chi và tài khoản người dùng không phải admin rồi nhập lại), thông tin cửa hàng.

## 2. Đánh lại mã hàng (`DanhLaiMaHang`)

- Mật khẩu bảo vệ cố định `4321`. Chiều dài mã = `SystemConfig.ChieuDaiMaVach`.
- Tùy chọn đánh lại mã nhóm: mỗi nhóm con của cùng một nhóm cha nhận mã `10, 11, 12…` (theo thứ tự cây); kiểm tra trùng `mã + nhóm cha` và cảnh báo.
- Mã hàng: `mã đầy đủ của nhóm = mã nhóm cha + mã nhóm` (viết hoa); các mặt hàng trong nhóm (sắp theo tên) nhận số thứ tự 1, 2, 3… đệm số 0 vào giữa cho đủ `ChieuDaiMaVach`: `CODE = mãNhóm + "0…0" + số`. (Số thứ tự bắt đầu lại từ 1 cho mỗi nhóm.)

## 3. Xuất lại vật tư (`XuatLaiVatTu`)

- Mật khẩu: mật khẩu của tài khoản admin (`SUSER.ISADMIN = 1`) nối thêm `6666`.
- Xóa mọi dòng `XUATVATTU = 30` (hoặc trong khoảng ngày nếu chọn) rồi chạy lại `TDONHANG0Ae.XuatVatTu(bill, kho của khu vực)` cho các bill có bàn (`TDONHANG` nối `DBAN` và `DKHUVUC` trong khoảng ngày), sau đó **tính lại giá vốn toàn hệ thống**. Dùng khi sửa công thức định lượng sau khi đã bán.

## 4. Import định lượng từ Excel (`ImportDinhLuong`)

- Nhận danh sách (mặt hàng, vật tư, số lượng) đã chuẩn hóa (sắp theo tên hàng). Với mỗi mặt hàng: xóa định lượng cũ (`DDINHLUONG`), đặt `DLOAIMATHANGID = '1'` (định lượng), ghi các dòng mới, cập nhật giá nhập / giá vốn của mặt hàng và của mọi mặt hàng chứa nó theo công thức ở [file danh mục](pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md) mục 2.

## 5. Lưu vết hoạt động (`LuuVetHoatDong`, bảng `TLUUVET`)

- Chỉ hoạt động khi `KichHoatLuuVetHoatDong=30`. Một dòng lưu vết gồm: `GIO`, `PHANLOAI` (`LoaiLuuVet`: mở bàn, thêm / tăng / giảm / xóa món, đặt số lượng, đổi giá bán, đặt giảm giá mặt hàng, đóng bàn, in bill, in chế biến, chuyển bàn, gộp bàn, chuyển mặt hàng, hủy bill, trả đồ, khác…), `BAN`, `SODONHANG`, `TAIKHOAN`, `THIETBI`, `NGAY`, `NOTE`, `SOLUONG`, `DONGIA`, `THANHTIEN`, `TENHANG`, `CHUCNANG` ("Sử dụng dịch vụ", "Máy tính bảng", "Quản lý bán hàng", "Điều chỉnh hóa đơn", "POS cảm ứng").
- Màn hình xem: danh sách bill (đã thanh toán, đang sử dụng, hoặc đã hủy — hủy lấy từ `TDONHANGHUY`, kèm lý do) theo khoảng ngày + số hóa đơn; chọn bill hiển thị các dòng lưu vết (lọc tài khoản), tô đỏ hủy bill / xóa / giảm món / đổi giá / giảm giá, xanh dương chuyển bàn, xanh lá gộp bàn. Xóa toàn bộ lưu vết cần admin giữ Ctrl+Shift và xác nhận (`XacNhanXoaLuuVet`).

## 6. Tài khoản người dùng (`SUSER`)

- Gắn tài khoản với nhân viên (`DNHANVIENID`): chọn nhân viên thì tên tài khoản lấy theo tên nhân viên và khóa ô nhập.
- Quyền vào cửa hàng: bảng `TNGUOIDUNGTHEOCUAHANG` (người dùng × cửa hàng); người dùng mới mặc định được vào mọi cửa hàng; lưu thì xóa và ghi lại; cảnh báo nếu không chọn cửa hàng nào. (Phân quyền chức năng theo nhóm nằm ở `SGROUPUSER`, `SGROUPROLE`, `SFUNCTION` — chưa có form đọc được trong export.)
