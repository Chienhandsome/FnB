# POS TAP — nghiệp vụ rút từ mã nguồn export (`POS_TAP_SRC_EXPORT`)

Ghi chú làm việc, cập nhật dần khi đọc từng form. Nguồn: `code.cs` / `ts_code.cs` / `query.sql` trong `POS_TAP_SRC_EXPORT/{SFORM,SREPORT,STABLEDESC}`. Chưa phải spec đã duyệt; dùng làm đầu vào cho `/spec`.

## 0. Quy ước chung

- Cờ bật/tắt: **30 = bật/true, 0 = tắt**. Áp dụng cho `SystemConfig.*`, `DATHANHTOAN`, `STATUS`, `KHUYENMAI`, `GIAMTHEOTIEN`, `CONNO`, `XUATVATTU`, `COHANSUDUNG`, `THEOCAN`...
- `SystemConfig.*` là cấu hình toàn hệ thống (Quản trị | Cấu hình toàn hệ thống). Các khóa gặp ở form bán hàng: `LamTronTien`, `KichHoatLuuVetHoatDong`, `SuDung2DonViTinh`, `HeThongChayNhieuMayTram`, `MatHangCoHanSuDung`, `MatHangCoKichThuoc`, `CoThueSuat`, `CoPhiVanChuyen`, `CoGiaoHang`, `BatBuocNhapKhachHang`, `BatBuocNhapNhanVienBanHang`, `KichHoatKhuyenMaiTuDong`, `HienThiMatHangKhuyenMai`, `ChoPhepThayDoiNgayTrenHoaDon`, `ChoPhepNhapGiamGia`, `MacDinhGiamGia`, `GiamGiaMacDinh`, `SuDungGiaTheoKhach`, `SuDungGia2/3/4`, `HienThiDeLuaChonNeuCoNhieuGia`, `NhapMotMatHangNhieuLanTrongPhieu`, `TuDongNangCapThanhVienKhiDatHanMuc`, `CachTinhDiem`, `DoanhSoTuongUngVoi1Diem`, `QuyDoi1DiemSangTien`, `HienThiDiemCuaKhachHangTrenHoaDon`, `KichHoatSuDungCanDienTu`/`SuDungCanDienTu`, `CoKyTuKiemTra`, `CachTim`, `TimTheo`, `SapXepThuTuTheo`.
- Tên hóa đơn tự sinh: `DbUtils.GenSoHoaDon(ngay, mẫu NOTEMPLATE của SFORM, bảng, cột tên, cột ngày, điều kiện)`; mẫu lấy từ `SFORM.NOTEMPLATE` (FORMTYPE=0, LOAI=0, theo STABLEDESCID).
- Dòng chi tiết đơn: `TDONHANGCHITIET` có cặp số lượng **chưa quy đổi** (`SLXUATCHUAQUYDOI`, `SLNHAPCHUAQUYDOI`, theo đơn vị người dùng chọn) và **đã quy đổi** (`SLXUAT`, `SLNHAP`, theo đơn vị cơ sở). Bán = XUAT, trả = NHAP trên cùng bảng.
- Lưu vết hoạt động (`TLUUVET`) chỉ ghi khi `KichHoatLuuVetHoatDong = 30`.

## 1. Form `Hóa đơn bán hàng` (SFORM `Hóa đơn bán hàng__5f029a5b`, 2401 dòng)

### 1.1 Tính tiền dòng hàng (`grMua_OnRowCalculate`)
- `quyDoi = DMATHANG.QUYDOI` nếu bật 2 đơn vị tính, mặt hàng có 2 đơn vị và đơn vị dòng ≠ đơn vị cơ sở; ngược lại 1. `SLXUAT = quyDoi × SLXUATCHUAQUYDOI`.
- Sửa `TILEGIAMGIA` → `GIAMTHEOTIEN = 0`; sửa `TIENGIAMGIA` → `GIAMTHEOTIEN = 30`.
- `GIAMTHEOTIEN = 30`: `THANHTIEN = sl × donGia − tienGiam`, `TILEGIAMGIA = round(tienGiam × 100 / (donGia × sl), 2)` (0 nếu mẫu = 0).
- Ngược lại: `TIENGIAMGIA = sl × donGia × TL / 100`, `THANHTIEN = sl × donGia × (1 − TL/100)`.
- Dòng trả (`grTraLai_OnRowCalculate`): `SLNHAP = quyDoi × SLNHAPCHUAQUYDOI`; `THANHTIENDUONG = sl × donGia × (1 − TL/100)`; `THANHTIEN = −THANHTIENDUONG`.
- Đơn giá dòng trả theo đơn gốc: `DONGIA = donGiaGoc × (1 + TILETHUE_donGoc/100)`; `TILEGIAMGIA = 100 − (100 − TL_dòng)/100 × (100 − TL_bill_gốc)` (gộp chiết khấu dòng và chiết khấu bill của đơn gốc). Trả không theo đơn: lấy `GIABAN`, SL 1.

### 1.2 Tổng bill (`UpdateTotalWithoutSave`)
```
TIENHANG   = Σ THANHTIEN (tab mua)
DOITRA     = −Σ THANHTIEN (tab trả)            // số dương
Giảm theo tiền (GIAMTHEOTIEN=30):  TILEGIAMGIA = 100 × TIENGIAMGIA / TIENHANG (0 nếu TIENHANG=0)
Giảm theo %   :  TIENGIAMGIA = 100 × Round( TIENHANG × TILEGIAMGIA / 10000 )   // làm tròn tới trăm
TIENTHUE   = TILETHUE × (TIENHANG − TIENGIAMGIA) / 100
TONGCONG   = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN − DOITRA
nếu LamTronTien > 0:  TONGCONG = LamTronTien × Round(TONGCONG / LamTronTien)
```
Lưu ý: `Math.Round` của C# mặc định làm tròn về số chẵn (banker's) — cần giữ y hệt khi viết lại.
Thuế tính **sau** giảm giá, trước phí vận chuyển và trừ hàng trả. Trường thuế/phí vận chuyển chỉ hiện khi `CoThueSuat`/`CoPhiVanChuyen` = 30.

### 1.3 Chọn đơn giá khi thêm hàng
- Loại giá bill `LOAIGIA`: 0 = `GIABAN`, 1 = `GIABAN2`, 2 = `GIABAN3`, 3 = `GIABAN4`, 4 = **giá bán gần nhất cho khách** (`TDONHANG` đã thanh toán, cùng khách + mặt hàng + đơn vị, `ORDER BY NGAY DESC, TIMECREATED DESC`). Nhãn hiển thị lấy từ `SystemConfig.DienGiaiGia2/3/4`, mặc định "Giá bán".
- Hỏi chọn loại giá khi `HienThiDeLuaChonNeuCoNhieuGia=30` và có bật ít nhất một giá 2/3/4; bỏ qua nếu `SuDungGiaTheoKhach=30` và đã chọn khách. Khi chọn khách mà `SuDungGiaTheoKhach=30` thì loại giá lấy từ `DKHACHHANG.GIABAN` và áp cho cả bill.
- Đơn vị chẵn (đơn vị 2): giá `GIABANCHAN`, quy đổi `DMATHANG.QUYDOI`; thêm riêng một dòng đơn vị chẵn, cộng dồn nếu đã có cùng mặt hàng + đơn vị chẵn (trừ khi `NhapMotMatHangNhieuLanTrongPhieu=30`).
- Quét lại cùng mặt hàng: cộng dồn số lượng vào dòng cũ nếu cùng mặt hàng + đơn vị + chiết khấu (và cùng kích thước / hạn dùng khi bật), trừ khi `NhapMotMatHangNhieuLanTrongPhieu=30`.
- Giảm giá mặc định theo mặt hàng (`GiamGiaMacDinh=30`): `MACDINHGIAMGIA > 0` → dùng làm %; ngược lại dùng `MACDINHGIAMTIEN` với `GIAMTHEOTIEN=30`.
- Cân điện tử (`KichHoatSuDungCanDienTu=30`): mặt hàng `THEOCAN=30` lấy số lượng từ cân (`numCan`, gram/1000, 3 số lẻ). Mã vạch cân: bắt đầu bằng `ChuoiBatDau`, dài > 6; số lượng = 5 ký tự trước 1 ký tự cuối chia 1000. Cổng COM, baud (0→1200, 1→2400, 2→4800, khác→9600), vị trí/độ dài cắt chuỗi cân lấy từ cấu hình. Màn hình hiển thị cho khách: cổng `CongSuDung`, 9600 8N1, dòng 1 bỏ dấu, hoa, tối đa 19 ký tự.

### 1.4 Khuyến mại (đợt `DDOTKHUYENMAI`)
Chỉ áp khi `KichHoatKhuyenMaiTuDong=30`. Đợt đang dùng: `CURRENT_DATE BETWEEN TUNGAY AND DENNGAY AND STATUS=30 AND COALESCE(NGUNGAPDUNG,0)=0 AND DLOAIHINHKHUYENMAIID = <loại>`. Các loại (`LOAIHINHKHUYENMAI`):
- `GIAMGIATHEOSANPHAM`: chiết khấu dòng = `MAX(TILEGIAMGIA)` trong `DDOTKHUYENMAICHITIET` theo mặt hàng.
- `GIAMGIATHEONHOM`: `MAX(TILEGIAMGIA)` theo nhóm mặt hàng; lấy giá trị lớn hơn giữa theo sản phẩm và theo nhóm.
- `MATHANGDONGGIA`: đơn giá = `MIN(GIABAN)` trong chi tiết (nếu > 0).
- `GIAMGIATONGBILL`: % giảm cả bill = `TILEGIAMGIA` của đợt (lấy lớn nhất).
- `GIAMTHEOGIATRIDONHANG`: bậc theo `GIATRIDONHANG ≤ (int)TONGCONG`, lấy dòng bậc cao nhất; % giảm cả bill.
- `GIAMTHEOSOLUONGMATHANG`: bậc theo `SOLUONGMATHANG ≤ (int)(Σ SLXUAT − Σ SLNHAP)`; % giảm cả bill.
- `MUA1TANG1`: lúc thanh toán, tặng `SOLUONGTANG × FLOOR(SLXUAT / SOLUONGMUA)` (SOLUONGMUA=0 coi như 1) mặt hàng `DMATHANGTANGID`, gộp theo mặt hàng tặng; hiện form xác nhận nếu `HienThiMatHangKhuyenMai=30` (Hủy thì dừng thanh toán). Lưu thành dòng mới `DONGIA=0, THANHTIEN=0, KHUYENMAI=30`, SL xuất = số tặng.
- % giảm bill cuối cùng = `MAX(giảm theo nhóm khách hàng, giảm theo KM)`. Giảm theo nhóm khách: `DNHOMKHACHHANG.TILEGIAMGIA` của nhóm khách; mặc định `SystemConfig.MacDinhGiamGia`. Chọn khách / thêm hàng thì tính lại (trừ khi người dùng đang gõ ô giảm giá); các KM bill cũng đặt `GIAMTHEOTIEN=0`.

### 1.5 Kiểm tra trước thanh toán (`IsValidForThanhToan`)
1. Bản dùng thử (`No1System.IsTrial`): chặn khi số ngày phân biệt có hóa đơn `COUNT(DISTINCT NGAY) > 30`.
2. Một máy trạm: tải lại, nếu `DATHANHTOAN=30` thì báo "Hóa đơn này đã thanh toán"; sinh lại số hóa đơn.
3. Kho: `DKHOHANG.CHOPHEPAMKHO = 0` → không cho âm. Cộng dồn SL xuất theo mặt hàng (trừ SL trả) rồi so với `TonKhoHandler.GetTonKho(mặtHàng, kho, đơnHiệnTại)`; thiếu thì báo "trong kho chỉ còn tồn ..., không đủ xuất ...". Đồng bộ `DKHOHANGID` của mọi dòng về kho chọn.
4. Dòng trả có tham chiếu `TDONHANGTRAID`: SL trả ≤ SL bán − SL đã trả ở các đơn khác. (Quy đổi hiển thị ngược theo `QUYDOI` khi có 2 đơn vị.)
5. Phải có ít nhất 1 dòng (mua hoặc trả).
6. `BatBuocNhapKhachHang=30` bắt buộc chọn khách; `BatBuocNhapNhanVienBanHang=30` bắt buộc chọn nhân viên xuất; bắt buộc chọn kho.

### 1.6 Thanh toán (`btnThanhToan_Click`)
- Khi `SuDung2DonViTinh = 0` và một máy trạm: `SLNHAP := SLNHAPCHUAQUYDOI`, `SLXUAT := SLXUATCHUAQUYDOI` cho cả đơn. Gán kho cho mọi dòng.
- Nợ cũ khách: `GetNoCu(khách, đơnHiện)` =
  `Σ(TONGCONG) − Σ(COALESCE(TIENTHANHTOAN,0))` trên `TDONHANG` của khách `DATHANHTOAN=30` (trừ đơn hiện tại)
  `− Σ( LOAI=0 ? THU : −CHI )` trên `TTHUCHI` của khách với `COALESCE(KHONGTHAYDOICONGNO,0)=0` và `TDATHANGID IS NULL`.
- Đặt trước: `datTruoc = TTHUCHI.THU` của phiếu đặt hàng gắn đơn (`TDATHANGID`). Form xác nhận nhận `(tổng cộng, đặt trước, nợ cũ + đặt trước, khách, đơn)`; "Nợ cũ đã bao gồm đặt trước".
- Kết quả form xác nhận `OK`/`Retry` (Retry = không in) → ghi đơn:
  - `TRALAI = max(0, form.TraLai)`; `TIENTHANHTOAN = form.TienThanhToan − form.DatTruoc`; `CONLAI = max(0, TONGCONG − form.TienThanhToan)`.
  - `TIENMAT = (TraLai < 0 ? TraLai : TienMat)`; `CHUYENKHOAN`, `THE`, `THETRATRUOC` (+ `DTHETRATRUOCID` nếu > 0), `TRUTICHLUY`, `VOUCHER` (+`DVOUCHERID`), `DTAIKHOANNGANHANGID`, `LOAITHANHTOAN`, `KHACHDUA`, `NOCU`.
  - `DIEMGIAM = TRUTICHLUY / QuyDoi1DiemSangTien` (nếu quy đổi ≠ 0). `DIEM = (int)(TONGCONG / max(DoanhSoTuongUngVoi1Diem, 1))`.
  - `GIOTHANHTOAN = now`; `NGAY = now.Date` nếu không cho đổi ngày; `USERTHANHTOANID`; `DATHANHTOAN = 30`; `CONNO = (KhachDua < TONGCONG) ? 30 : 0`.
  - Sau đó: `XuatVatTu`, in hóa đơn (nếu OK), nâng nhóm khách, tạo hóa đơn mới.
- `Ignore` = chỉ xem trước hóa đơn.

### 1.7 Xuất vật tư theo định lượng (`XuatVatTu`)
- Xóa các dòng `XUATVATTU=30` cũ của đơn, sinh lại. Nguồn: `DDINHLUONG` (mặt hàng → vật tư, `SOLUONG`) cho mặt hàng có `LOAIDINHLUONG` = 1 hoặc 2; SL vật tư = `Σ (SLXUAT − SLNHAP) × DDINHLUONG.SOLUONG`.
- **Đệ quy**: vật tư lại có định lượng riêng thì thay bằng vật tư con (nhân số lượng) cho tới khi hết cấp.
- Mỗi vật tư thành một dòng `TDONHANGCHITIET`: `XUATVATTU=30`, `DONGIA=0`, `THANHTIEN=0`, đơn vị = đơn vị cơ sở vật tư, kho = kho xuất.
- Danh sách mặt hàng bán loại bỏ nguyên liệu: `COALESCE(LOAIDINHLUONG,0) <> LoaiMatHang.NguyenLieu`.
- **[Bổ sung v2 — chỗ DEMO khác POS TAP thật]** Cột `DMATHANG.LOAIDINHLUONG` **không có** trong schema DEMO/TEMPLATE nhưng code bán lẻ dùng (2 chỗ: SQL `XuatVatTu` ở trên và danh sách mặt hàng bán; đã kiểm trên `POS_TAP_SRC_EXPORT`: 4 hit trong `Hóa đơn bán hàng` + `Quản lý bán hàng`). `XuatVatTu` chạy không try/catch ngay sau lưu thanh toán → cột phải tồn tại trong `DATA.fdb` POS TAP thật; **lấy schema POS TAP thật làm chuẩn**, đừng tin schema DEMO ở cột này. Trừ vật tư bán lẻ **bung đệ quy không giới hạn độ sâu** (`GetVatTu`, không kiểm tra vòng tròn) — công thức A⊃B⊃A treo lúc thanh toán; nhà hàng chỉ bung 1 cấp thêm qua `InsertVatTu`.
- **[Bổ sung v2]** `GetNoCu` đã đối chiếu nguyên văn trên `POS_TAP_SRC_EXPORT` (xem công thức ở mục 1.6): phiếu thu công nợ `LOAI=2` đóng góp 0 (nhánh `-CHI`); phiếu đặt trước (`TDATHANGID IS NOT NULL`) bị loại. Hai câu hỏi lớn của bán hàng vẫn mở: **trừ kho thực sự xảy ra ở đâu** (0 `UPDATE` tồn trong form) và **transaction khi thanh toán** (0 hit) → test app gốc (checklist BH-Q13).

### 1.8 Điểm tích lũy và nhóm khách hàng
- `GetDiemTichLuy(khách, đơn)`:
  - `CachTinhDiem = 0` (theo tổng doanh số): `DIEMTICHLUYBANDAU + Σ TONGCONG(trừ đơn hiện tại) / max(1, DoanhSoTuongUngVoi1Diem) + Σ(DIEMTANG − DIEMGIAM)(TTANGGIAMDIEM)` rồi trừ `Σ DIEMGIAM` trên `TDONHANG`.
  - `CachTinhDiem ≠ 0` (theo từng hóa đơn): `DIEMTICHLUYBANDAU + Σ(DIEM − DIEMGIAM)(TDONHANG, trừ đơn hiện tại) + Σ(DIEMTANG − DIEMGIAM)(TTANGGIAMDIEM)`.
  - Hiện điểm trên hóa đơn chỉ khi `HienThiDiemCuaKhachHangTrenHoaDon=30`.
- Tự nâng nhóm (`TuDongNangCapThanhVienKhiDatHanMuc=30`) sau thanh toán: tính điểm (đơn đã thanh toán, `LOAI=0`), chọn nhóm `DNHOMKHACHHANG` có `0 < DIEMTICHLUY ≤ điểm` cao nhất; khác nhóm hiện tại thì cập nhật và thông báo.
- In hóa đơn thêm tham số: `Nợ mới = TONGCONG + NOCU − TIENTHANHTOAN`, `Đặt trước`, `Điểm tích lũy`.

### 1.9 Lưu tạm / mở lại, các thao tác khác
- Lưu tạm: chỉ khi đơn chưa thanh toán và có hàng; ghi `TLUUTAM` + `TLUUTAMCHITIET` (SL = xuất − nhập, trả lưu số âm), rồi xóa chi tiết đơn đang mở (`EmptyDonHang` reset mọi tổng về 0, bỏ `TDATHANGID`). Mở lại: nạp ngược (SL > 0 → mua, ngược lại → trả), xóa bản tạm. Lưu ý: dòng `row.TILEGIAMGIA = row.TILEGIAMGIA` (gán tự thân) là lỗi gốc, không có tác dụng.
- Mở từ phiếu đặt hàng chưa có đơn (`NOT EXISTS (TDONHANG ... TDATHANGID)`): nạp mặt hàng + giảm giá + khách từ `TDATHANG`.
- Duyệt đơn trước/sau: theo `USERCREATEDID` hiện tại, `LOAI=0`, `STATUS=30`, sắp theo `TIMECREATED`.
- Một máy trạm: đơn chưa thanh toán gần nhất của user được tải lại (`DATHANHTOAN IS NULL OR 0`), tự lưu mỗi lần tính lại (`mapper.Update`); nhiều máy trạm: chỉ lưu khi thanh toán.
- Sửa đơn giá cần quyền `Functions.SuaDonGia`; sửa dòng bị khóa khi đã thanh toán hoặc đang in.
- Phím tắt: F3 mã vạch, F6 khách, F7 loại giá, F9 lưu tạm, F10 thống kê, F11 mở lại, F12 thanh toán.
- Tìm mặt hàng: `CachTim` 0 chứa / 1 bắt đầu / khác chính xác; `TimTheo` 0 mã+tên / 1 tên / 2 mã; có `MASANCO` khi `SuDungMaSanCo=30`; `CoKyTuKiemTra=30` bỏ ký tự cuối mã vạch; tìm không dấu.
- Hiển thị tồn dưới ảnh mặt hàng: `TonKhoHandler.GetTonKho`.
- Đơn vị chẵn khi hết hàng: hiển thị "tồn lẻ + tồn chẵn" với `tonChan = (int)(tonKho / quyDoi)`.


## 2. Hộp thoại nhỏ dùng chung khi bán hàng

- **Chọn mặt hàng trả** (`ChonMatHangTra`): `BatBuocDoiTraHangTheoDon=30` bắt buộc trả theo hóa đơn gốc: nhập số hóa đơn (`TDONHANG.NAME`), chỉ trả trong `SoNgayChoPhepDoiTra` ngày kể từ `NGAY` hóa đơn ("quá số ngày có thể đổi trả"); liệt kê các dòng bán (`SLXUAT > 0`) để tick, mặc định số trả 1; số trả phải > 0 và ≤ `SLXUATCHUAQUYDOI` của dòng gốc (khi thanh toán còn kiểm tra thêm số đã trả ở các đơn khác). Không bắt buộc: chọn mặt hàng bất kỳ, SL 1, giá bán hiện tại.
- **Nhập số lượng** (`NhapSoLuong`): nhập SL lẻ + SL chẵn (nếu có 2 đơn vị) + chiết khấu %; khi có kho: hiện tồn theo kích thước (`MatHangCoKichThuoc`, bắt buộc chọn một) và tồn theo hạn dùng (`MatHangCoHanSuDung` và mặt hàng `COHANSUDUNG=30`, bắt buộc chọn một hạn).
- **Đặt giảm giá một dòng** (`DatGiamGia`): nhập % (0–100) hoặc tiền (0 – giá gốc); `tiền = Round(giáGốc × % / 100, 0)`, `% = Round(tiền × 100 / giáGốc, 2)` (0 nếu giá gốc 0).
- **Đổi giá bán** (`DoiGiaBan`): đơn giá ≥ 0. **Thêm mặt hàng mở** (`ThemMatHangMo`): bắt buộc nhập tên (khác tên mặc định của mặt hàng mở), đơn giá ≥ 0 (0 chữ số lẻ), đơn vị tính.
- **Chọn khách hàng qua thẻ** (`ChonKhachHangQuaThe`): đầu đọc thẻ gõ mã vào bàn phím — chuỗi ký tự kết thúc bằng Enter trong vòng ≤ 2 giây được coi là mã quẹt; tìm `DKHACHHANG.MAKHACH`; sai thì "Mã thẻ khách hàng không đúng".
- **Danh sách bill hủy**: xem `TDONHANGHUY` và chi tiết (`TDONHANGHUYCHITIET`). **Hàng tồn hết hạn dùng**: tồn `> 0` theo `HANSUDUNG` của mặt hàng `COHANSUDUNG=30`; "sắp hết hạn" = hạn trong `[hôm nay, hôm nay + ThongBaoTruocNgay ngày]`, "đã quá hạn" = hạn `< hôm nay`; số ngày còn / quá hạn = chênh lệch ngày.
