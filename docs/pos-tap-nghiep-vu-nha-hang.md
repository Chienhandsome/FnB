# POS TAP — nghiệp vụ form `Hóa đơn nhà hàng` (SFORM `Hóa đơn nhà hàng__f3f7bb77`, class `TDONHANG0Ae`)

Tiếp theo của [pos-tap-nghiep-vu.md](pos-tap-nghiep-vu.md). Nguồn: `code.cs` 4982 dòng (bản desktop). Bản cảm ứng `ts_code.cs` (2252 dòng) ghi riêng ở phần cuối khi đã đọc.

Dùng cho bàn/phòng (quán ăn, karaoke, spa). Ba chế độ `HoaDonMode`: `SuDungDichVu` (bán tại bàn), `QuanLyBanHang` (sửa bill cũ, cho đổi giờ), `DieuChinhHoaDon` (chỉ đổi khách, trả đồ). Thuật ngữ bàn/phòng lấy từ `Shared.GetTenPhongBan`.

## 1. Tạo hóa đơn trên bàn (`TaoHoaDonTrang`)

- Bàn đã có hóa đơn (`DBAN.TDONHANGID`) thì báo "đang có khách". Nhiều máy trạm: bàn bị giữ bởi tài khoản khác thì chặn (`SuDungDichVu.BanDangBiGiu`). Bản dùng thử: ≥ 30 ngày phân biệt có bill thì chặn.
- `NGAY = Shared.GetNgayGiaoDich` (có chế độ `NgayGiaoDichMode.TheoNgayMoBan`); `BATDAU = BATDAUPHONGCUOI = now − TuDongDoiGioVaoVeTruoc phút`, bỏ giây; `SOKHACH = 1`; `DATHANHTOAN = 0`; `LOAI = 0`.
- Tiền giờ (`Shared.CoTienGio`): cách tính + đơn giá lấy từ bàn: `DBAN.CACHTINHGIO` = THEOKHUVUC → lấy từ `DKHUVUC` (bảng giá `DBANGGIAID` hoặc `DONGIA`); THEOBANGGIA → `DBAN.DBANGGIAID`; còn lại `DBAN.DONGIA`. `TIENMOBAN = DBAN.TIENMOBAN`.
- Số hóa đơn (`TaoSoHoaDon`, bảng `TSOHOADON`, cột `SOHD`): quay vòng theo `Shared.SoHDQuayVong`: Ngày → `ddMM` + 4 số; Tháng → `MMyy` + 5 số; Năm → `yy` + 6 số; Không quay vòng → 7 số. Số mới = số lớn nhất hiện có + 1 (khóa `TSOHOADON` theo ngày / đầu tháng / đầu năm / 2000-01-01; lần đầu lấy `MAX(SOHD)` của `TDONHANG LOAI=0` trong kỳ). `SOTT` (thứ tự khi thanh toán) = `MAX(SOTT)+1` trong ngày, điều kiện `LOAI=0 AND DATHANHTOAN=30`.
- Mặc định: `TILEPHIDICHVU = MacDinhPhiDichVu` nếu `CoPhiDichVu=30`; `TILETHUE = MacDinhThueSuat` nếu `CoThueSuat=30`; giảm giá khởi tạo từ KM đang chạy (mục 5).
- Hàng mặc định theo khu vực (`SuDungMatHangMacDinh=30`, bảng `DMATHANGMACDINH`); mở từ phiếu đặt phòng (`TDATHANG`): thêm hàng đã đặt (giá đặt nếu cùng bàn, không thì giá khu vực), gán khách.

## 2. Thêm / sửa dòng hàng

- Mặt hàng `TAMKHOA=30` không dùng được. Loại mặt hàng `DLOAIMATHANGID`: 1 = định lượng; 4 = Combo (chữ đỏ); 5 = Combo mở (chữ xanh); 7 = dịch vụ theo giờ; còn có Mặt hàng mở, Dịch vụ, Mặt hàng kiêm vật tư. Dòng `DMATHANGID='KM'` là dòng khuyến mại tự sinh (nền vàng nhạt, không sửa được).
- Gộp dòng nếu `NhapMotMatHangNhieuLanTrongPhieu=0`, trừ mặt hàng mở, dịch vụ theo giờ, SL âm (trả): cùng mặt hàng, cùng `TILEGIAMGIA`, không ghi chú, và (`GIATHEOTHOIGIA=30` hoặc cùng đơn giá) → cộng số lượng.
- Mặt hàng mở: hỏi tên, đơn vị, đơn giá. `GIATHEOTHOIGIA=30`: hỏi đơn giá mỗi lần. Combo mở: chọn thành phần. Ứng dụng SPA: hỏi nhân viên theo từng dòng (`DNHANVIEN1ID`).
- Combo / combo mở: dòng cha (có `COMBOID` = GUID) + dòng con từ `DDINHLUONG` (`DVATTUID`, `SOLUONG`): đơn giá 0, giá vốn 0, `COMBOSL = SOLUONG`, `SL = SL cha × COMBOSL`, `COMBOPARENTID = COMBOID cha`. Đổi SL cha thì cập nhật SL con (`SL = COMBOSL × SL cha`). Xóa cha xóa luôn con. Không chuyển combo sang bill khác.
- Mặt hàng theo giờ (`THEOGIO=30`): `TUGIO = GIOTINHLUONG = now`; số lượng là số giờ, không tăng/giảm bằng nút +/−; sửa bằng form chọn từ giờ/đến giờ (`TUGIO`, `DENGIO`, `GIOTINHLUONG`).
- Tính dòng: `SLXUAT = SLXUATCHUAQUYDOI` (form này không dùng 2 đơn vị).
  - `GIAMTHEOTIEN=30`: `THANHTIEN = DONGIA × SL − TIENGIAMGIA`.
  - Ngược lại: `THANHTIEN = DONGIA × SL × (1 − TL/100)`; `TIENGIAMGIA = DONGIA × SL − THANHTIEN`.
- Kho của dòng = `DKHUVUC.DKHOHANGID` của khu vực chứa bàn.
- Trả đồ (`TraDo`): thêm dòng SL âm, giữ đơn giá và % giảm của dòng gốc, đặt `GIOTRADO = now`.
- Cân điện tử: mặt hàng `THEOCAN=30` lấy SL từ cân; = 0 thì "Mời bạn đặt đồ lên cân".

## 3. Chọn giá bán theo khu vực và theo giờ

- `DKHUVUC.DGIAMATHANGID`: GIABAN, GIABAN2/3/4, hoặc `HIENTHILUACHON` (hỏi từng lần, trừ mặt hàng giá theo thời giá và mặt hàng mở).
- Giá theo khung giờ (`SuDungGiaTheoGio=30`): trong khoảng `DKHUVUC.TUGIO..DENGIO` dùng `DGIATHEOGIOID` thay `DGIAMATHANGID` (trừ khi = `HIENTHILUACHON`). Khoảng giờ qua đêm: nếu `từ ≥ đến` thì hợp lệ khi `now ≥ từ` hoặc `now ≤ đến` (`IsTimeInRange`). Thời điểm xét theo `CachChonGioTinhGia`: giờ gọi đồ (now), giờ vào bàn (`BATDAU` bill) hoặc **giờ thanh toán** (khi thanh toán duyệt lại giá mọi dòng không phải combo con / KM / mặt hàng mở).

## 4. Tính tiền giờ (`UpdateGio`, `UpdateTongThoiGian`)

- Cập nhật định kỳ (timer) khi chưa thanh toán. Nếu `DungThoiGianKhiInTamTinh=30` và đã in tạm tính: không cập nhật thời gian trong `SoPhutToiDaChoPhepDung` phút kể từ `INTAMTINHLUC`.
- Chưa đổi giờ thủ công (`TUTHAYDOIGIO ≠ 30`): `KETTHUC = max(KETTHUC, now + TuDongDoiGioRaVeSau phút)`.
- Tiền giờ = `Shared.TinhGia(cachTinh, bảng giá, đơn giá, bắt đầu, kết thúc, BATDAU bill, phút KM, % KM phút đầu, …, TIENMOBAN)`; nếu `ketThuc < batDau` cộng 1 ngày. **Mã `Shared.TinhGia` không có trong export** (nằm trong DLL), chỉ biết đầu vào; kết quả được làm tròn nghìn: `max(0, 1000 × Round(val/1000, AwayFromZero))` (không làm tròn khi `GiaBanLeUsd=30`).
- Phút khuyến mại giờ: từ bill (`PHUTKHUYENMAI`, `TILEKHUYENMAIPHUTDAU`); nếu bill có combo hát (`DMATHANG.GIOHATCOMBO` giờ × 60 trừ số phút đã dùng trong `TDONHANGGIO`) thì dùng số phút combo còn lại với tỉ lệ 100%.
- Mặt hàng dịch vụ theo giờ: `SL = max(0, Round(số giờ giữa TUGIO và DENGIO, 2))` (`DENGIO` rỗng = giờ kết thúc bill). Nếu `LamTronMatHangDichVuTheoGio = n phút > 0`: `heSo = phút / n` rồi `Floor` (xuống) / `Ceiling` (lên) / `Round AwayFromZero` (giữa) theo `CachLamTronDichVuTheoGio` (0/1/2); `SL = Round(heSo × n / 60, 2)`.
- Bảng `TDONHANGGIO` lưu các khoảng giờ khi chuyển phòng (mỗi khoảng có đơn giá/bảng giá riêng). `TIENGIO = TIENGIOPHONGCUOI + Σ THANHTIEN(TDONHANGGIO)`.

## 5. Khuyến mại và giảm giá theo nhóm khách

- Đợt KM ở form này lọc thêm khung giờ trong ngày: `CAST(t AS DATE) BETWEEN TUNGAY AND DENNGAY AND CAST(t AS TIME) BETWEEN TUGIO AND DENGIO`, cùng điều kiện `STATUS=30, NGUNGAPDUNG=0, KichHoatKhuyenMaiTuDong=30`.
- Giảm dòng theo KM: giống form bán lẻ (theo sản phẩm, theo nhóm lấy max; đồng giá lấy min).
- KM giảm tổng bill (`GetTiLeGiamTheoKM`): từ các đợt `GIAMGIATONGBILL` lấy max `TILEGIAMGIA` (tiền hàng), max `TILEGIAMGIATONG`, max `TILEGIAMGIATIENGIO`, và `KHUYENMAIGIOHAT` (phút) lớn nhất cùng `TILEGIAMGIAGIODAU` (% giảm giờ đầu).
  - Gộp chung giảm giá giờ và hàng (`GopChungGiamGiaTienGioVaTienHangLamMot=30`, chỉ khi có tiền giờ): `giamTong = KM.TILEGIAMGIATONG`, nếu không có KM = `min(MacDinhGiamGiaTienGio, MacDinhGiamGia)`; giờ = hàng = tổng.
  - Không gộp: `giamGio = max(KM giờ, MacDinhGiamGiaTienGio)`; `giamHang = max(KM hàng, MacDinhGiamGia)`.
- Chọn khách: `DNHOMKHACHHANG.TILEGIAMDOAN / DOUONG / DOKHAC / DICHVU` áp lên từng dòng theo `DNHOMMATHANG.DLOAIDOID` (LoaiDo: đồ ăn / đồ uống / dịch vụ / đồ khác); dòng lấy `max(KM theo sản phẩm tại thời điểm TIMECREATED của dòng, % nhóm khách)`. Giảm cả bill: gộp thì `giamTong = max(KM, nhom.TILEGIAMGIA)`; không gộp thì `giamHang = max(KM, nhom.TILEGIAMGIATIENHANG)` và `giamGio = max(KM, nhom.TILEGIAMGIATIENGIO)`. Bỏ chọn khách thì đặt giảm theo nhóm = 0. Nút "Giảm theo nhóm" cho nhập tay 4 tỉ lệ, cần quyền `GiamGiaMatHang`.
- **Mua 1 tặng 1** (`MUA1TANG1`, id loại `e5d39bb6-41a4-43ff-bc4d-e170ae4aeba2`) tính lại lúc thanh toán: xóa mọi dòng `DMATHANGID='KM'` rồi sinh lại. Chỉ tính dòng có `TIMECREATED` trong ngày và khung giờ của đợt. SL trả (SL âm) được thêm sau `DENGIO` của đợt thì trừ vào số bán (`GetSoLuongTra = min(Σ SL các dòng thêm sau DENGIO, 0)`). **[Sửa v2]** Hàm này gần như không bao giờ có tác dụng: `Math.Min(tổng, 0)` → tổng dương (dòng bán thường) cho 0, chỉ trừ khi bill có dòng trả âm; còn so sánh `TIMECREATED` (ngày+giờ) với `Date(DENGIO)` (chỉ giờ) nên phần ngày không được chuẩn hóa. Mua 1 tặng 1 ở nhà hàng chia cho `(MUA+TẶNG)` và tặng bằng dòng âm `DMATHANGID='KM'` (mua 1 tặng 1 → mua 1 được 0), khác bán lẻ (`FLOOR(SL/SOLUONGMUA)×SOLUONGTANG`, dòng 0 đồng). Nhánh "tặng khác món" tính `slXuat` rồi không dùng → tặng vượt số lượng món tặng trên bill (xem [file khách hàng](pos-tap-nghiep-vu-khach-hang-the.md) mục 6.3). **Bản cảm ứng** (`TDONHANG0AeTs`) dùng chung các hàm *static* với desktop (`TaoSoHoaDon`, `LamTron`, `XuatVatTu`, `GetDatTruoc`…) nhưng `mapper_OnCalculation`, `btnThanhToan_Click`, `DoSave`, `UpdateTongThoiGian`, `HuyBill` là **bản sao viết lại** (công thức giống, kể cả lỗi chia cho `TIENGIO`; lệch ở `UpdateTongThoiGian` thiếu làm tròn phút); clone màn cảm ứng nên viết **một bộ tính toán chung**.
  - Tặng cùng mặt hàng (mua M tặng T): `slTang = T × floor(SL / (M+T))`; trường hợp lẻ: `tiLe = (int)((SL − slTang)/M)`, `slTangKhaDung = (int)(SL − tiLe×M)`; nếu `slTangKhaDung < tiLe × T` thì `slTang = slTangKhaDung`. Dòng KM: tên "<tên> (Khuyến mại)", `DONGIA = −đơn giá`, `THANHTIEN = −slTang × đơn giá × (1 − TL/100)`.
  - Tặng hàng khác: `SL tặng = (int)(SL × SoLuongTang / SoLuongMua)`, chỉ tặng khi mặt hàng tặng có trong bill, không quá SL mặt hàng đó; cùng dạng dòng KM âm.

## 6. Tổng bill nhà hàng (`mapper_OnCalculation`)

Làm tròn ở đây là `LamTron(x) = LamTronTien × Round(x / LamTronTien, 0, AwayFromZero)` (khác form bán lẻ dùng làm tròn mặc định của C#), bỏ qua nếu `LamTronTien ≤ 0`.

```
TIENHANG          = LamTron( Σ THANHTIEN )
TIENHANGCHUAGIAM  = LamTron( Σ SL × DONGIA );  GIAMGIAMATHANG = TIENHANGCHUAGIAM − TIENHANG
TIENGIO           = TIENGIOPHONGCUOI + Σ THANHTIEN(TDONHANGGIO)

GỘP chung giảm giá (Tong = TIENGIO + TIENHANG):
  - giảm theo tiền (GIAMTONGTHEOTIEN=30):
      TILEGIAMGIATONG = Round(TIENGIAMGIATONG×100/Tong, 2, AwayFromZero)
      TIENGIAMGIA     = (TIENGIO=0) ? TIENGIAMGIATONG : LamTron(TIENHANG × TIENGIAMGIATONG / TIENGIO)   // đúng theo mã gốc
      TIENGIAMGIAGIO  = TIENGIAMGIATONG − TIENGIAMGIA
  - giảm theo %:
      TIENGIAMGIATONG = LamTron(TILEGIAMGIATONG × Tong / 100)
      TIENGIAMGIA     = LamTron(TIENHANG × TILEGIAMGIA / 100); TIENGIAMGIAGIO = TIENGIAMGIATONG − TIENGIAMGIA
  - TienHangSauGiam = TIENHANG + TIENGIO − TIENGIAMGIATONG
KHÔNG gộp: giảm hàng và giảm giờ tính riêng, mỗi bên có cờ theo tiền / theo % (GIAMTHEOTIEN, GIAMGIAGIOTHEOTIEN):
  tiền → %: Round(tien×100/Tong, 2, AwayFromZero); % → tiền: LamTron(% × Tong / 100)
  TienHangSauGiam = TIENHANG + TIENGIO − TIENGIAMGIA − TIENGIAMGIAGIO; giảm tổng suy ngược lại

PHIDICHVUTHEOTIEN=30 → TILEPHIDICHVU = Round(PHIDICHVU×100/TienHangSauGiam, 2)
          ngược lại   → PHIDICHVU    = LamTron(TILEPHIDICHVU × TienHangSauGiam / 100)
TienHangSauGiam += PHIDICHVU
TIENTHUE    = LamTron( TienHangSauGiam × TILETHUE / 100 )      // thuế tính sau giảm và sau phí dịch vụ
TONGCONG    = LamTron( TienHangSauGiam + TIENTHUE )
TIENPHAITRA = TONGCONG + BILLKHAC    // BILLKHAC = Σ TONGCONG các bill khác cùng NHOMGUID (gộp phòng)
```

Ghi vào DB ngay trong lúc nhập (chỉ ở chế độ SuDungDichVu và bill chưa thanh toán). Phần chia tiền giảm tổng khi giảm theo tiền và có tiền giờ dùng `TIENGIO` làm mẫu số đúng như mã gốc, nhìn có vẻ lệch với kỳ vọng "chia theo tỉ trọng" — cần hỏi Chiến trước khi chép lại.

## 7. Thanh toán (`btnThanhToan_Click`)

- Không có dòng hàng và `TIENGIO = 0` → "Mời bạn thêm mặt hàng vào phiếu". Chế độ QuanLyBanHang cần quyền sửa `Functions.QuanLyBanHang`.
- Trước khi thanh toán: tính lại KM mua 1 tặng 1; bắt buộc chọn nhân viên nếu ô đang hiện; `UpdateGio()`; kiểm soát order (`BatChucNangKiemSoatOrder=30`): `SOORDER` phải là danh sách số nguyên cách nhau dấu phẩy ("12, 16, 17,19"); tính lại giá theo giờ thanh toán nếu cần; `CalculateAllRows`; in chế biến nốt phần chưa in; kiểm tra còn mật khẩu wifi (`InMatKhauWifiTrenBill`).
- Bill khác ngày giao dịch hiện tại (và không phải `TheoNgayMoBan`): cảnh báo và chuyển bill sang ngày hiện tại.
- Số tiền cần thu = `TIENPHAITRA` (đã gồm bill khác cùng nhóm); form xác nhận nhận `(mode, TIENPHAITRA, đặt trước, nợ cũ, khách, đơn)`; `BatBuocInKhiThanhToan=30` thì tắt nút "đóng bill không in".
- Khi OK / Retry:
  - Xóa dòng vật tư cũ rồi `XuatVatTu` (mục 8).
  - **Bill gộp nhóm (`NHOMGUID`)**: duyệt từng bill trong nhóm: `tienThanhToan += đặtTrước(bill)`; `TIENTHANHTOAN = min(tienThanhToan, max(0, TONGCONG − đặtTrước))`; `tienThanhToan −= TONGCONG`; ghi chung `LOAITHANHTOAN`, `TRALAI`, `KHACHDUA`, `NGAY`, `GIOTHANHTOAN`, `DATHANHTOAN=30`, `USERTHANHTOANID`.
  - **Bill đơn**: `TRALAI = max(0, form.TraLai)`; `tienTraLai = (form.TienThanhToan < 0) ? −form.TienThanhToan : 0` (khách cọc dư); `TIENTHANHTOAN = max(form.TienThanhToan, 0)`; `CONLAI = max(0, TONGCONG − form.TienThanhToan)`; `THETRATRUOC`, `TRUTICHLUY`, `DTHETRATRUOCID`, `DATTRUOC = đặt trước`, **`CONGNO = form.TienNo`**, `TIENMAT = (TraLai < 0 ? TraLai : TienMat)`, `CHUYENKHOAN`, `THE`, `SOTT`, `DIEMGIAM = TRUTICHLUY / QuyDoi1DiemSangTien`, `KHACHDUA`, `NOCU`, `DTAIKHOANNGANHANGID`, `VOUCHER` / `DVOUCHERID`, `LOAITHANHTOAN`, `DIEM = (int)(TONGCONG / max(1, DoanhSoTuongUngVoi1Diem))`, `PASSWIFI` (lấy 1 mật khẩu từ `DMATKHAUWIFI` rồi xóa nó), `USERTHANHTOANID`, `GIOTHANHTOAN`, `DATHANHTOAN = 30`, `CONNO = (KhachDua < TONGCONG) ? 30 : 0`.
  - In hóa đơn nếu OK. Nếu `tienTraLai > 0`: mở phiếu chi tự động "Trả lại tiền đặt trước hóa đơn số …" (`KHONGTHAYDOICONGNO` tích) cho khách.
  - Nâng nhóm khách theo điểm (giống form bán lẻ). Xóa chi tiết in chế biến (`TINCHEBIEN`), tắt đèn bàn (`Shared.TatDen`), tạo bill trống, làm tươi trạng thái bàn.
  - Chế độ `QuanLyBanHang` / `DieuChinhHoaDon`: không đóng bàn; lưu vết được gom (`lstLuuVet` → `TLUUVET`) rồi ghi khi lưu.
- `Ignore` = in tạm tính (tăng `SOLANINTAMTINH`, ghi `INTAMTINHLUC`).
- **Đặt trước / tạm ứng** (`GetDatTruoc`): bill thường: `SUM(THU)` của `TTHUCHI` với `TDONHANGID = bill` hoặc `TDATHANGID = phiếu đặt` (chỉ khi `SuDungChucNangTamUngTrongDonHang=30` hoặc có phiếu đặt). Bill nhóm: tổng `THU` của mọi bill trong nhóm (theo `TDATHANGID` hoặc `TDONHANGID`).
- Nợ cũ khách dùng chung `HoaDonBanHang.GetNoCu` (công thức ở file chính, mục 1.6).

## 8. Xuất vật tư (`XuatVatTu` ở form nhà hàng)

- Với các dòng thuộc mặt hàng `DLOAIMATHANGID = '1'` (định lượng): vật tư = `DDINHLUONG.DVATTUID`, `SL vật tư = Σ SLXUATCHUAQUYDOI × DDINHLUONG.SOLUONG`, nhóm theo vật tư, bỏ các SL = 0. Nếu vật tư lại là mặt hàng định lượng thì mở rộng thêm **đúng một tầng** (`InsertVatTu`, cộng dồn nếu trùng) rồi chỉ lấy những vật tư **không** phải định lượng. Mỗi vật tư thành một dòng `TDONHANGCHITIET`: `XUATVATTU=30`, `SLXUAT = SLXUATCHUAQUYDOI = SL`, `GIAVON` = giá vốn vật tư, kho = kho của khu vực, đơn vị = đơn vị cơ sở.
- Khác form bán lẻ (đệ quy nhiều tầng, SL đã quy đổi, giá vốn không gán).

## 9. Điều kiện thay đổi / xóa / giảm hàng

- Hóa đơn đã thanh toán: không sửa (`IsDaThanhToan`: bill không còn, hoặc bàn không còn trỏ vào bill, hoặc `DATHANHTOAN=30`).
- Giảm / xóa / đặt SL thấp hơn: cần quyền `Functions.XoaGiamMon` (đăng nhập lại), mật khẩu giảm đồ nếu `NhapMatKhauGiamDo=30`; không giảm được khi bill đã in tạm tính (trừ khi `ChoPhepThayDoiHoaDonSauKhiInTamTinh=30` hoặc có quyền `DuocGiamDoSauKhiInTamTinh`); khi `KhongDuocGiamDoSauKhiInPhaChe=30` hoặc thiếu quyền `XoaGiamMonSauKhiInCheBien`: không giảm xuống dưới SL đã in xuống bếp (`TINCHEBIEN.SOLUONG` cộng theo `TENHANG`; combo thì tính theo các món con và suy `soLuongSau` theo tỉ lệ).
- Nút `−1` giữ SL ≥ 1 (`max(SL − 1, 1)`); nút Giảm (SL tùy chọn) chỉ cho giảm khi `SL ≥ val + 1`.
- Xóa mặt hàng: nếu `CoLuuVet && NhapLyDoKhiXoaMon=30` bắt buộc nhập lý do, ngược lại hỏi xác nhận.
- Quyền khác: đổi đơn giá `SuaDonGia`; giảm giá dòng / nhóm `GiamGiaMatHang`; sửa giảm giá tổng `ThayDoiGiamGiaTongHoaDon`; sửa phí dịch vụ `ThayDoiPhiDichVu`; xem đơn giá `XemDonGiaTrongHoaDonBanHang` (không có quyền: ẩn giá, tổng và thanh toán, dành cho nhân viên chạy bàn); chuyển hàng sang bill khác `ChuyenMatHangSangHoaDonKhac`; đổi khách ở chế độ điều chỉnh `ThayDoiKhachHangTrongDieuChinhHoaDon`.
- Chuyển bàn / gộp bàn / chuyển hàng sang bill khác (`ChuyenBan` với `ChuyenGopBanMode`): phải in chế biến trước; chuyển hàng chỉ cho dịch vụ, hàng kiêm vật tư, mặt hàng mở, định lượng; không chuyển combo; không chuyển khi đã in tạm tính.

## 10. In ấn

- In tạm tính tối đa `SoLanInTamTinhToiDa` lần; in hóa đơn tối đa `SoLanInToiDa` lần (0 = không giới hạn). Số liên in `SoLanIn` (1–5; = 1 nếu xem trước).
- Mẫu in: `DKHUVUC.STEMPLATEID`, mặc định `SystemConfig.MauHoaDon`. Máy in: mặc định của hệ điều hành; `InHoaDonTheoKhuVuc`: in theo khu vực (`Data\app.dat`, mục `INKHUVUC`), hoặc tạm tính theo khu vực + hóa đơn tại quầy, hoặc 1 liên tại quầy + 1 liên theo khu vực.
- Tham số in thêm: `Trả lại tiền cọc dư`: nếu `DATTRUOC > 0` thì `TongKhachDua = VOUCHER + DATTRUOC + CHUYENKHOAN + THE + THETRATRUOC + TRUTICHLUY`; nếu `VOUCHER > TONGCONG` bằng `DATTRUOC`, ngược lại `max(0, TongKhachDua − TONGCONG)`. `Nợ mới = TONGCONG + NOCU − TIENTHANHTOAN` (0 nếu tạm tính). Tiêu đề "PHIẾU TẠM TÍNH" / "HÓA ĐƠN BÁN HÀNG"; số ngày/giờ/phút sử dụng; chi tiết giờ (`ChiTietGio`, theo từng khoảng giờ nếu `InChiTietTheoTungKhoangGio=30`, thành tiền làm tròn nghìn); combo in dạng cha–con; in cộng gộp mặt hàng (`InHoaDonCongGopMatHang`). Số lượng dịch vụ theo giờ in dạng "{giờ}h {phút}'".
- In xuống bếp / pha chế (`InCheBien`, bảng `TINCHEBIEN`): tự động sau khi gọi món nếu `TuDongInPhaCheSauKhiGoiMon=30` và `SuDungChucNangInXuongBep=30` (chờ `ThoiGianTuDongIn` giây, 0 = in ngay); bắt buộc in nốt trước khi chuyển bàn, thống kê và thanh toán.

## 11. Các điểm khác

- Hiển thị 4 tab Đồ ăn / Đồ uống / Dịch vụ / Đồ khác (`HienThi3NhomOGiaoDienBanHang`); nhóm có `DLOAIDOID = '4'` bị ẩn khỏi cây nhóm bán hàng.
- Màn hình khách (cây hiển thị giá): 1 dòng (2400 baud, tổng 8 ký tự, `ESC @`) khi `CayHienThiGia1Dong=30`, ngược lại 2 dòng (9600 baud, tên bỏ dấu ≤ 19 ký tự).
- Phím tắt: F2 ghi chú, F3 tìm, F4 chọn nhân viên, F5/F6/F7 chọn bàn trống / đang dùng / tất cả, F8 in lại, F9 thống kê, F10 in chế biến, F11 thanh toán, F12 khách hàng.
- Bật / tắt đèn theo bàn (`Shared.BatDen / TatDen`) nếu có thiết bị. Chọn khách theo `CachChonKhachHang` (chuột + bàn phím hoặc quẹt thẻ).
- Chế độ `≠ SuDungDichVu`: dữ liệu lưu vết được gom và ghi khi lưu, không ghi ngay.
