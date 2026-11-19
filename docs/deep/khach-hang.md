# Rà SÂU nghiệp vụ nhóm KHÁCH HÀNG — app POS TAP (vòng 2)

> Vòng 2, sâu hơn vòng 1. Đối chiếu từng khẳng định của Claude với CODE thật + AELAYOUT đầy đủ + dữ liệu trích trực tiếp từ `FNB_DEMO.fdb`.
> Nguồn chính: vòng 1 `../khach-hang.md` (Q1–Q39, E1–E20); Claude `~/workspace/fnb/repo/docs/pos-tap-nghiep-vu-khach-hang-the.md`, `pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md`, `pos-tap-nghiep-vu.md` §1.8; dữ liệu gốc `../raw/` (CODE/TSCODE/AELAYOUT/DESIGNCODE đầy đủ, `sgrid.txt`, `sgrouprole.txt`, `sreport_template/`); trích trực tiếp từ DB qua `fdb` (SCOLUMN đủ 26 trường, STABLEDESC.AELAYOUT/DESIGNCODE/FORMAT/SFUNCTIONID, SCONFIG, DLOAIHINHKHUYENMAI, DLOAIMATHANG, DGIAMATHANG, SGROUPUSER/SGROUPROLE, DNGANHANG).
> Chỉ đọc file, không sửa repo/DB, không commit/push. Ngày rà: 19/11/2026.

## 0. Phương pháp vòng 2 (khác vòng 1 ở đâu)

Vòng 1 bị giới hạn bởi dữ liệu xuất: `scolumn.txt` chỉ có 11/26 trường SCOLUMN (thiếu `SHOWONLOOKUP`, `ENABLECONFIGID`, `DEFAULTCONFIGID`, `FORMULA`, `MAXVALUE`/`MINVALUE`, `CONTROLTYPE`…), `stablesdesc.txt` không có `AELAYOUT`/`DESIGNCODE`/`FORMAT`/`SFUNCTIONID` của STABLEDESC, AELAYOUT của SFORM bị cắt ở 4000 ký tự. Vòng 2 khắc phục bằng cách trích trực tiếp từ `FNB_DEMO.fdb`:

- Đọc blob `STABLEDESC.AELAYOUT` / `DESIGNCODE` / `FORMAT` / `SFUNCTIONID` cho từng bảng (script `/tmp/extract_stable2.py` — lưu ý `/tmp` là ephemeral, script sẽ mất khi VM thay; phương pháp ghi lại ở đây để làm lại được).
- Đọc toàn bộ `SCOLUMN` đủ 26 trường cho các bảng liên quan (`/tmp/stable_out/scolumn_full.txt` + `scolumn_full2.txt`).
- Giải mã AELAYOUT: phần XML No1Lib chứa layout control + event; phần BinaryFormatter base64 chứa `ViewConfig` (`<DocumentElement>` với các cột `COLID`/`TABLEID`/`FORMULA`/`REPLACE`/`CAPTION`/`ALIAS`/`CUSTOMFIELD`) — giải mã bằng `/tmp/decode_ae2.py`. Tên cột dạng GUID được tra ngược qua SCOLUMN (map COLID → `TABLE.NAME`, `CAPTION`).
- Đọc `SGRID` (lưới mặc định theo `STABLEDESCID`), `SGROUPROLE` (269 dòng), `SREPORT_TEMPLATE` manifest (156 dòng, gồm `AUTOGENREPORT`, `TPL_BYTES`, `CFG_BYTES`) + file `.config` của từng báo cáo.

Kết quả: nhiều kết luận "không kiểm chứng được" của vòng 1 được giải quyết dứt điểm; 2 kết luận vòng 1 bị sửa (E10 bị bác, E13 đảo ngược); 1 kết luận vòng 1 bị sửa bảng chân lý (E19/TILEGIAMGIA); và phát hiện thêm 5 nghi bug mới chưa có ở vòng 1 lẫn tài liệu Claude.

---

## (a) Tóm tắt đối chiếu Claude

### a.1. Bảng đối chiếu từng khẳng định (file `pos-tap-nghiep-vu-khach-hang-the.md` + `pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md` + `pos-tap-nghiep-vu.md` §1.8)

| # | Khẳng định của Claude | Kết luận vòng 2 | Bằng chứng |
|---|---|---|---|
| C1 | §1.8: `GetDiemTichLuy` — nhánh `CachTinhDiem=0`: doanh số trừ bill hiện tại + `TTANGGIAMDIEM`, rồi trừ Σ`DIEMGIAM` không loại bill hiện tại; nhánh ≠0: Σ(`DIEM`−`DIEMGIAM`) trừ bill hiện tại + điểm ban đầu + `TTANGGIAMDIEM` | **ĐÚNG nguyên văn** | `HoaDonBanHang.cs` dòng 865–896, đọc verbatim, khớp cả vòng 1 §11.2 |
| C2 | Thẻ trả trước: số dư = nạp − sử dụng; nạp/rút tiền; khóa thẻ; hết hạn | **ĐÚNG, có bổ sung** | `NhapTheTraTruoc.GetSoTienConLai` (CODE + TSCODE) + 5 kiểm tra ở `btnOK_Click`. Bổ sung: desktop trừ `CHI`, cảm ứng không trừ (xem Q30); thông báo trống gõ nhầm chữ "voucher" (xem §f) |
| C3 | Voucher: 3 kiểm tra (tồn tại, hạn dùng, đã dùng 1 lần qua `TDONHANG.DVOUCHERID`) | **ĐÚNG, có bổ sung** | `NhapCoupon.btnOK_Click` (CODE = TSCODE, chỉ khác `Form1`/`No1Form1` + `SelectAll`). Bổ sung: không kiểm tra `NGAYPHATHANH`, không kiểm tra đối tác phân phối, `COUNT(*)` không lọc `DATHANHTOAN` |
| C4 | Điểm: 1 điểm = 1.000đ (`QuyDoi1DiemSangTien`); trừ tích lũy desktop/cảm ứng | **ĐÚNG, có bổ sung lớn** | `TruTichLuy` CODE (1118 byte) vs `TruTichLuyTs` TSCODE (2908 byte): desktop KHÔNG đồng bộ điểm↔tiền, KHÔNG validation; cảm ứng đồng bộ 2 chiều + validation + nút "Sử dụng hết" (xem Q33) |
| C5 | Nhóm khách: `mapper_OnLoad` điều khiển hiện/ẩn ô giảm theo `CoTienGio`/`GopChung` | **ĐÚNG, trừ bảng chân lý** | Layout `DNHOMKHACHHANG`: `lbl/numTILEGIAMGIA` có `Visible=False` mặc định; code chỉ bật khi `CoTienGio && GopChung`. Vòng 1 §3.2 sai ở trường hợp còn lại — xem §(e) E19 |
| C6 | KM: 8 loại, mọi giảm giá lấy Max không cộng dồn | **ĐÚNG** | 8 hằng `LOAIHINHKHUYENMAI.*` trong `DDOTKHUYENMAI.cs` + `HoaDonBanHang`/`HoaDonNhaHang` dùng `Max(...)` |
| C7 | MUA1TANG1: BH và NH tính khác nhau | **ĐÚNG, đào sâu hơn** | BH: `FLOOR(SL/SOLUONGMUA)×SOLUONGTANG`, tặng dòng 0 đồng. NH: chia cho `(MUA+TẶNG)`, tặng dòng âm `DMATHANGID='KM'`, mua 1 tặng 1 thì mua 1 được 0; `slXuat` tính rồi không dùng; `GetSoLuongTra` đọc nguyên văn (xem Q19/Q20) |
| C8 | Kiểm tra giờ KM chỉ ở form nhà hàng, không ở bán hàng | **ĐÚNG** | NH kiểm tra `TUGIO`/`DENGIO`; BH không có nhánh giờ |
| C9 | `DLOAIMATHANG` mã 6 "dự đoán là Dịch vụ" | **XÁC NHẬN** | Tra DB: 0 kiêm vật tư, 1 pha chế, 2 NVL, 3 mở, 4 Combo, 5 Combo mở, **6 Dịch vụ**, 7 dịch vụ theo giờ |
| C10 | `NhapCoupon` desktop và cảm ứng "giống nhau" | **ĐÚNG** | So sánh verbatim CODE vs TSCODE: cùng 3 kiểm tra + cùng thông báo; khác duy nhất `Form1` vs `No1Form1` và TSCODE không gọi `txtMa.SelectAll()` |
| C11 | Giá theo khách (`GIABAN` 0–4, `SetLoaiGia`) | **ĐÚNG phần lõi, thiếu 1 lệch** | `SetLoaiGia` 0→GIABAN, 1→GIABAN2, 2→GIABAN3, 3→GIABAN4, 4→giá gần nhất. Nhưng form `ChonLoaiGia` desktop lệch 1 so với `SetLoaiGia` (xem Q7 + §f) — Claude không nhắc |
| C12 | Công thức điểm hiển thị / xét nâng nhóm | **ĐÚNG, thiếu 1 công thức thứ ba** | Ngoài 2 công thức đã biết còn công thức thứ ba trên lưới Khách hàng thân thiết (lọc `DATHANHTOAN=30 AND LOAI=0`, trừ `SUM(DIEMGIAM)` cả hai nhánh) — xem §(b) 1.3 |
| C13 | Báo cáo `BÁO_CÁO_CHI_TIẾT_HÀNG_KHUYẾN_MẠI` lấy số liệu từ dòng `KHUYENMAI=30` / `DMATHANGID='KM'` | **KHÔNG XÁC MINH ĐƯỢC theo hướng đó — đáp án khác** | SQL của báo cáo rỗng vì đây là **báo cáo tự sinh** (`AUTOGENREPORT=30`); số liệu lấy từ `.config` 12 cột (xem Q27). Không có bằng chứng nó lọc theo `KHUYENMAI`/`'KM'` |
| C14 | SMS/tin nhắn: chỉ còn menu + quyền, không có code gửi | **ĐÚNG, xác nhận lại lần nữa** | Grep toàn bộ CODE/TSCODE/DESIGNCODE/AELAYOUT: `TinNhan`/`SMS`/`SendSMS` = 0; `SSMSTEMPLATE` 0 dòng; 2 config `CanhBaoChuongTrinhKhuyenMai` + `ThongBaoKhachDenNgaySinhNhat` chỉ có trong `sconfig.txt`, không code nào đọc |

### a.2. Những gì Claude chưa đọc (vòng 2 bổ sung)

- Hộp thoại nhỏ: `TruTichLuy`, `NhapCoupon`, `NhapTheTraTruoc`, `ChonLoaiGia`, `CanhBaoKhuyenMai` (chỉ có file của chính nó).
- `SGRID` (lưới mặc định), `SGROUPROLE` (269 dòng), `SCOLUMN` đủ trường, `STABLEDESC.FORMAT`/`SFUNCTIONID`, `SREPORT_TEMPLATE` (báo cáo tự sinh).
- Các phát hiện mới không có trong tài liệu Claude: lệch 1 của `ChonLoaiGia` desktop; công thức điểm thứ ba; `GetSoLuongTra` trả `Math.Min(sum, 0)`; số dư thẻ desktop/cảm ứng lệch nhau ở `CHI`; `DienGiaiGia3` = "Giá 2" (dữ liệu DEMO); loại KM `8be106e5` không có nhánh xử lý.

### a.3. Đánh giá chung

Claude đúng ở phần lõi (điểm, thẻ, voucher, KM, nhóm khách) vì đã đọc kỹ form chính + 36 báo cáo + danh mục chính. Vòng 2 đúng hơn ở: (1) độ phủ — đọc cả hộp thoại nhỏ, TSCODE, AELAYOUT đầy đủ, SGRID, SGROUPROLE, template báo cáo; (2) độ chính xác từng công thức — đọc verbatim và đối chiếu 3 công thức điểm thay vì 2; (3) phát hiện lệch desktop/cảm ứng ở 3 chỗ (số dư thẻ, trừ tích lũy, chọn loại giá) mà Claude không nhắc. Điểm Claude hơn vòng 2: Claude đọc dữ liệu POS TAP thật (Claude đọc từ `POS_TAP_SRC_EXPORT/` trên máy Chiến), còn vòng 2 vẫn dùng `FNB_DEMO.fdb` — các dòng seed (`DLOAIHINHKHUYENMAI` 5 dòng, `DGIAMATHANG`, `DNGANHANG` 58 dòng…) là dữ liệu DEMO, DB POS TAP thật có thể khác (đặc biệt lưu ý ở Q16, Q23, Q35, Q39).

---

## (b) Công thức + luồng đào sâu theo chủ đề

### 1. Điểm tích lũy

#### 1.1. Điểm của từng hóa đơn — `TDONHANG.DIEM`

`DIEM = (int)(TONGCONG / diemQuyDoi)` với `diemQuyDoi = DoanhSoTuongUngVoi1Diem` (DEMO = 20000). Ghi ở `HoaDonBanHang` và `HoaDonNhaHang` khi thanh toán (`row.DIEM = (int)(numTONGCONG.Value / diemQuyDoi)`, NH dòng ~2898). Lưu ý: `TONGCONG` ở đây là tổng sau giảm giá/tiền giờ/phí — điểm tính trên tổng đã giảm, không phải `TIENHANG` chưa giảm.

#### 1.2. Điểm hiện tại của khách — `HoaDonBanHang.GetDiemTichLuy(DKHACHHANGID, TDONHANGID)` (dòng 865–896, nguyên văn đã đối chiếu)

Khớp verbatim cả vòng 1 §11.2 và Claude §1.8. Tóm tắt hành vi:

- Nhánh `CachTinhDiem = 0` (DEMO đang = 1, nên nhánh này không chạy với DEMO): điểm = (doanh số các bill đã thanh toán, trừ bill hiện tại) / `QuyDoi` + điểm từ `TTANGGIAMDIEM`, rồi trừ Σ`DIEMGIAM` của **mọi** bill kể cả bill hiện tại. Comment trong code ghi "tính theo tổng doanh số" — comment gây hiểu lầm vì hàm chỉ là wrapper gọi `GetDiemTichLuy` với `""`.
- Nhánh `CachTinhDiem ≠ 0` (DEMO): điểm = Σ(`DIEM` − `DIEMGIAM`) trên các bill đã thanh toán, trừ bill hiện tại, + `DIEMTICHLUYBANDAU` + Σ(`DIEMTANG` − `DIEMGIAM`) từ `TTANGGIAMDIEM`.
- Điểm khác biệt mấu chốt giữa hai nhánh: nhánh 0 lấy doanh số trừ bill hiện tại (không lọc `DATHANHTOAN`/`LOAI` ở phần này), rồi trừ Σ`DIEMGIAM` của **mọi** bill kể cả bill hiện tại; nhánh ≠0 loại bill hiện tại ra khỏi cả hai phía và cộng `DIEMTICHLUYBANDAU`.

#### 1.3. Công thức điểm THỨ BA — lưới Khách hàng thân thiết (phát hiện mới vòng 2, Q32)

`grKhachHang` (form `KhachHangThanThiet`) có 11 cột, trong đó 3 cột tính sẵn bằng SQL trong `ViewConfig` (giải mã từ `SFORM.AELAYOUT` đầy đủ):

| Cột | Công thức (verbatim, rút gọn) |
|---|---|
| "Doanh số" (`DOANHSO`) | `(SELECT COALESCE(SUM(TONGCONG),0) FROM TDONHANG WHERE DATHANHTOAN=30 AND LOAI=0 AND DKHACHHANGID=DKHACHHANG.ID)` |
| "Số hóa đơn" (`SODONHANG`) | `COUNT(*)` cùng điều kiện trên |
| "Điểm tích lũy" (`DIEM`) | `COALESCE(DIEMTICHLUYBANDAU,0) + (SELECT COALESCE(CASE WHEN @CACHTINH=1 THEN SUM(DIEM−DIEMGIAM) ELSE SUM(TONGCONG)/CAST(@DIEM AS DECIMAL(18,2)) − SUM(DIEMGIAM) END,0) FROM TDONHANG WHERE DATHANHTOAN=30 AND LOAI=0 AND DKHACHHANGID=DKHACHHANG.ID) + (SELECT COALESCE(SUM(DIEMTANG−DIEMGIAM),0) FROM TTANGGIAMDIEM …)` |

Đây là công thức điểm thứ ba, khác cả hai công thức đã biết:

- Khác `GetDiemTichLuy` nhánh ≠0: lưới này **lọc** `DATHANHTOAN=30 AND LOAI=0` và **trừ `SUM(DIEMGIAM)` trong cả hai nhánh** của `CASE` (kể cả nhánh `CACHTINH=1` vốn là Σ(`DIEM`−`DIEMGIAM`)); `GetDiemTichLuy` nhánh ≠0 không lọc `DATHANHTOAN`/`LOAI` ở phần Σ(`DIEM`−`DIEMGIAM`) và loại bill hiện tại ra (lưới không có khái niệm bill hiện tại).
- Khác công thức xét tự nâng nhóm: công thức nâng nhóm **không trừ `DIEMGIAM`**; lưới này trừ.
- Hệ quả thực tế: cùng một khách, số điểm hiển thị trên lưới Thân thiết có thể khác số điểm mà `GetDiemTichLuy` trả về khi trừ tích lũy (đặc biệt khi khách có bill chưa thanh toán, bill `LOAI≠0`, hoặc đã tiêu điểm). Bản clone phải quyết định dùng **một** công thức chuẩn cho mọi nơi, hoặc giữ nguyên 3 công thức như app gốc (và ghi rõ sự khác nhau trong spec).

Các cột còn lại của `grKhachHang`: `NOTE`, `NAME` (SORT ASC), `DNHOMKHACHHANGID` (khóa nối, không tạo cột riêng), `NAME` nhóm (qua `DNHOMKHACHHANGID`), `DIACHI`, `DIENTHOAI`, `EMAIL`, `DIEMTICHLUYBANDAU`.

`grTangDiem` 6 cột: `NAME` Số phiếu, `NOTE`, `NGAY`, `DIEMTANG`, `DIEMGIAM`, `LYDO`; tab tên "Tặng điểm - tặng quà". `grHoaDon` 5 cột: `DIEM` Điểm, `NAME` Số phiếu, `NGAY`, `TONGCONG` Tổng cộng, `DIEMGIAM` "Điểm sử dụng".

#### 1.4. Tự nâng nhóm theo điểm

`TuDongNangCapThanhVienKhiDatHanMuc` (DEMO = 30, bật) + ngưỡng `DNHOMKHACHHANG.DIEMTICHLUY`. Công thức xét nâng nhóm **không trừ `DIEMGIAM`** (khác công thức hiển thị) — xem Q35 (còn mở: có hạ nhóm khi điểm tụt không; ngưỡng từng nhóm trong DEMO = 0 nhóm nên chưa có số thật).

#### 1.5. Trừ điểm khi thanh toán — desktop vs cảm ứng (Q33, đầy đủ)

**Desktop `TruTichLuy`** (form 384×219, `TruTichLuy.cs` 1118 byte, CODE nguyên văn):

```csharp
public void btnOK_Click(object sender, EventArgs e)
{
    No1Form1.DialogResult = DialogResult.OK;   // KHÔNG có validation nào
}
internal void LoadData(string DKHACHHANGID, string TDONHANGID)
{
    DKHACHHANGRow khRow = new DKHACHHANGRow(DKHACHHANGID);
    lblKhachHang.Text = lblKhachHang.Text + ": " + khRow.NAME;
    decimal soDiem = HoaDonBanHang.GetDiemTichLuy(DKHACHHANGID, TDONHANGID);
    numDiem.Value = soDiem;
    decimal giaTri = soDiem * SystemConfig.QuyDoi1DiemSangTien;
    numGiaTri.Value = giaTri;
}
```

- AELAYOUT chỉ có 1 event: `btnOK_Click`. **Không có handler đồng bộ** `numDiem` ↔ `numGiaTri`: người dùng sửa ô nào thì ô kia đứng yên.
- Form gọi (`XacNhanThanhToan` desktop) đọc `numGiaTri` (số tiền) làm `TruTichLuy`. Vì không có validation, người dùng có thể gõ `numGiaTri` vượt quá giá trị điểm hiện có (và vượt tổng tiền bill) — `DIEMGIAM = TruTichLuy / QuyDoi` sau đó có thể đẩy điểm khách xuống âm (xem §f).
- Không có nút "Sử dụng hết", không có tham số `TongTien` (khác bản cảm ứng).

**Cảm ứng `TruTichLuyTs`** (TSCODE 2908 byte, đầy đủ):

```csharp
internal void LoadData(string DKHACHHANGID, string TDONHANGID, decimal TongTien) // CÓ TongTien
{
    this.TongTien = TongTien;
    decimal soDiem = HoaDonBanHang.GetDiemTichLuy(DKHACHHANGID, TDONHANGID);
    numDiem.Value = soDiem;
    numGiaTri.Value = soDiem * SystemConfig.QuyDoi1DiemSangTien;
}
public void btnSuDungHet_Click(object sender, EventArgs e)
{
    decimal quyDoi = SystemConfig.QuyDoi1DiemSangTien;
    if (quyDoi <= 0) { Msg.ShowWarning("VUI LÒNG THIẾT LẬP QUY ĐỔI ĐIỂM SANG TIỀN TRƯỚC"); return; }
    numDiemSuDung.Value = Math.Min(numDiem.Value, Math.Round(TongTien / quyDoi, 2));
    btnOK_Click(null, null);
}
public void btnOK_Click(object sender, EventArgs e)
{
    if (numDiemSuDung.Value <= 0 || numDiemSuDung.Value > numDiem.Value)
    {
        Msg.ShowWarning("VUI LÒNG NHẬP ĐIỂM SỬ DỤNG TRONG KHOẢNG TỪ 0 TỚI " + numDiem.Value.ToString());
        return;
    }
    Form1.DialogResult = DialogResult.OK;
}
public void numDiemSuDung_OnEditValueChanged(object sender, object value)
{
    numGiaTriSuDung.Value = numDiemSuDung.Value * SystemConfig.QuyDoi1DiemSangTien;
}
public void numGiaTriSuDung_OnEditValueChanged(object sender, object value)
{
    decimal quyDoi = SystemConfig.QuyDoi1DiemSangTien;
    if (quyDoi == 0) numDiemSuDung.Value = 0;
    else numDiemSuDung.Value = Math.Round(numGiaTriSuDung.Value / SystemConfig.QuyDoi1DiemSangTien, 2);
}
```

- Đồng bộ 2 chiều điểm ↔ tiền (nhân/chia `QuyDoi`, guard `quyDoi == 0`).
- Validation: `0 < điểm sử dụng ≤ điểm hiện có`, không thì cảnh báo. **Nhưng không kiểm tra so với tổng tiền bill**: nhập tay vẫn có thể vượt `TongTien` (chỉ nút "Sử dụng hết" mới cap bằng `Min(điểm hiện có, Round(TongTien/quyDoi, 2))`).
- Form gọi (XNTT cảm ứng, dòng 374–388): `numTichLuy.Value = form.numDiemSuDung.Value * SystemConfig.QuyDoi1DiemSangTien`.

**Kết luận cho spec clone:** giữ hành vi cảm ứng (đồng bộ + validation + "Sử dụng hết") làm chuẩn cho cả hai bản; desktop gốc thiếu validation là điểm yếu cần sửa, không phải sao chép y nguyên.

#### 1.6. Thanh toán gộp nhiều hóa đơn — `NHOMGUID` (Q34, đầy đủ, sửa kết luận vòng 1)

Nhánh gộp trong `HoaDonNhaHang` (CODE dòng ~2798–2852; bản cảm ứng TSCODE cùng cấu trúc, khác `LOAITHANHTOAN = 0` hardcode thay vì `form.LoaiThanhToan`):

```csharp
// Lấy các bill cùng nhóm
DataTable dtNhom = Config.Db.GetTable(
  "SELECT ID, TONGCONG, TDATHANGID, (SELECT SOTIEN FROM TTHUCHI WHERE TTHUCHI.TDATHANGID = TDONHANG.TDATHANGID) AS DATTRUOC FROM TDONHANG WHERE NHOMGUID = '" + GUID + "'");
decimal tienThanhToan = form.TienThanhToan;
foreach (DataRow r in dtNhom.Rows)
{
    TDONHANGRow uRow = new TDONHANGRow(r["ID"].ToString());
    uRow.USERTHANHTOANID = DbConfig.UserID;
    uRow.DATHANHTOAN = 30;
    uRow.NHOMGUID = GUID;
    uRow.GIOTHANHTOAN = gioThanhToan;
    uRow.NGAY = ngayGiaoDich;
    uRow.LOAITHANHTOAN = form.LoaiThanhToan;
    uRow.TRALAI = form.TraLai;
    uRow.KHACHDUA = form.KhachDua;
    decimal datTruoc = ...;               // từ cột DATTRUOC
    tienThanhToan += datTruoc;
    uRow.TIENTHANHTOAN = Math.Min(tienThanhToan, Math.Max(0, uRow.TONGCONG - datTruoc));
    tienThanhToan -= uRow.TONGCONG;        // trừ NGUYÊN TONGCONG, không phải số vừa trả
    uRow.Update();
}
// Sau vòng lặp: if (tienThanhToan > 0) { /* TraLaiKhach(tienThanhToan); — đã comment */ }
```

- Mỗi bill trong nhóm chỉ được ghi: `USERTHANHTOANID`, `DATHANHTOAN=30`, `NHOMGUID`, `GIOTHANHTOAN`, `NGAY`, `LOAITHANHTOAN`, `TRALAI`, `KHACHDUA`, `TIENTHANHTOAN` (thác nước: `Min(còn lại, Max(0, TONGCONG − đặt trước))`).
- **Không ghi** vào các bill trong nhóm: `THETRATRUOC`, `TRUTICHLUY`, `DIEM`, `DIEMGIAM`, `VOUCHER`, `DVOUCHERID`, `DTHETRATRUOCID`.
- Các trường đó chỉ ghi trên **bill hiện tại** (`row` — nhánh thanh toán đơn ngay sau đó, dòng ~2858–2920): `row.THETRATRUOC = form.TheTraTruoc`, `row.TRUTICHLUY = form.TruTichLuy`, `row.DIEMGIAM = form.TruTichLuy / quyDoi`, `row.VOUCHER = form.Voucher`, `row.DVOUCHERID`, `row.DIEM = (int)(numTONGCONG.Value / diemQuyDoi)`, `row.DTHETRATRUOCID`, cộng `CONLAI`, `PASSWIFI = GetPasswordWifi()`, `DATTRUOC`, `CONGNO`, `TIENMAT`, `CHUYENKHOAN`, `SOTT`, `THE`, `NOCU`, `DTAIKHOANNGANHANGID`.
- **Sửa kết luận vòng 1:** vòng 1 viết "số dư thẻ sẽ không trừ, điểm tiêu không ghi" — **SAI**. Vì `THETRATRUOC`/`DIEMGIAM` được ghi 1 lần trên bill hiện tại, và `GetSoTienConLai`/`GetDiemTichLuy` cộng dồn theo `DTHETRATRUOCID`/khách (không theo từng bill), nên số dư thẻ **vẫn trừ đúng 1 lần** và điểm tiêu **vẫn ghi đúng 1 lần**. Voucher cũng vậy: `DVOUCHERID` ghi 1 lần trên bill hiện tại nên kiểm tra "đã dùng" vẫn chặn dùng lại.
- **Cái thiếu thật:** `DIEM` (điểm tích lũy kiếm được) chỉ ghi trên bill hiện tại — các bill còn lại trong nhóm **không được cộng điểm** dù tiền của chúng đã thanh toán. Khách gộp 3 bill 300k + 200k + 100k thì chỉ nhận điểm trên 1 bill (bill hiện tại), mất điểm của 2 bill kia. Đây mới là lệch hành vi thật của nhánh gộp (xem §f).
- Lưu ý phụ: thác nước trừ `tienThanhToan -= uRow.TONGCONG` (nguyên tổng) trong khi `TIENTHANHTOAN` chỉ trả `Min(còn lại, TONGCONG − datTruoc)` — khi bill có đặt trước (`datTruoc > 0`), số còn lại giảm nhiều hơn số thực trả (lệch đúng bằng `datTruoc` mỗi bill). Và `TraLaiKhach` sau vòng lặp đã bị comment — tiền thừa sau khi trả hết nhóm không được xử lý ở đây.
- Lưu ý chéo nhóm Quản trị: `row.PASSWIFI = GetPasswordWifi()` — mật khẩu wifi được ghi lên bill lúc thanh toán (NH); `GetPasswordWifi` lấy 1 mật khẩu rồi DELETE khỏi bảng (theo rà nhóm Quản trị) — tức wifi dùng 1 lần/bill.

### 2. Thẻ trả trước

#### 2.1. Số dư — desktop vs cảm ứng (Q30, công thức nguyên văn)

**Desktop** `NhapTheTraTruoc.GetSoTienConLai` (CODE):

```sql
nap    = SELECT SUM(COALESCE(THU, 0) - COALESCE(CHI, 0)) FROM TTHUCHI WHERE DTHETRATRUOCID = '{id}'
suDung = SELECT SUM(COALESCE(THETRATRUOC, 0)) FROM TDONHANG WHERE DTHETRATRUOCID = '{id}' AND ID <> '{TDONHANGID}'
conLai = nap - suDung
```

**Cảm ứng** `NhapTheTraTruocTs.GetSoTienConLai` (TSCODE):

```sql
nap    = SELECT SUM(THU) FROM TTHUCHI WHERE DTHETRATRUOCID = '{id}'   -- KHÔNG trừ CHI, không COALESCE
suDung = SELECT SUM(COALESCE(THETRATRUOC, 0)) FROM TDONHANG WHERE DTHETRATRUOCID = '{id}' AND ID <> '{TDONHANGID}'
conLai = nap - suDung
```

- Chênh lệch thật: sau một lần "Chi trả thẻ" (`TTHUCHI.CHI > 0`), bản cảm ứng hiển thị số dư **cao hơn** bản desktop đúng bằng tổng `CHI` (vì không trừ). **Bản desktop đúng** (chi trả là tiền rút khỏi thẻ, phải trừ); bản cảm ứng sai.
- Cả hai đều loại bill hiện tại (`ID <> TDONHANGID`) khỏi `suDung` — đúng, vì tiền thẻ của bill hiện tại chưa chốt lúc nhập thẻ.
- Công thức này cũng là công thức hiển thị "Còn lại" trên form Thẻ trả trước (3 ô Tổng nạp / Tổng sử dụng / Còn lại).

#### 2.2. Nhập thẻ trả trước — 5 kiểm tra (CODE = TSCODE, chỉ khác `No1Form1`/`Form1` + `SelectAll`)

`NhapTheTraTruoc.btnOK_Click` (form 384×145, ô `txtMa` "MÃ THẺ", nút "Chấp nhận"/"Hủy bỏ"):

1. Trống → cảnh báo `"Mời bạn nhập voucher trước"` — **sai chữ**: form thẻ mà báo "voucher" (copy-paste từ `NhapCoupon`), cả desktop lẫn cảm ứng đều sai giống nhau (xem §f).
2. Không tìm thấy `DTHETRATRUOC` theo `NAME` → `"Mã thẻ trả trước 'X' không tồn tại trong hệ thống"`.
3. `NGAYHETHAN < DbDate` (nếu có) → `"Mã thẻ này đã quá hạn sử dụng"`.
4. `KHOA == 30` → `"Thẻ này đã bị khóa, không thể sử dụng"`.
5. `GetSoTienConLai(...) <= 0` → `"Thẻ này không còn tiền để sử dụng"`.

Sau đó: `TheTraTruocID = row.ID`, `DialogResult = OK`; form gọi cap tiền dùng = `Min(SoTienConLai, TONGTIEN)`.

#### 2.3. Form Thẻ trả trước — đầy đủ (Q28)

Dialog 493×439: Mã thẻ, Nhóm thẻ, Khóa thẻ, Ngày hết hạn, Ghi chú; 2 tab "Lịch sử nạp tiền" / "Lịch sử sử dụng"; 3 ô Tổng nạp / Tổng sử dụng / Còn lại; nút Nạp tiền / Chi trả tiền.

- `grNapTien` = bảng **TTHUCHI**, 4 cột (giải mã từ `ViewConfig`, tra ngược SCOLUMN): `NAME` "Số phiếu", `NGAY` "Ngày", `THU` caption "Số tiền nạp", `CHI` caption "Chi trả".
- `grThanhToan` = bảng **TDONHANG**, 3 cột: `NAME` "Số phiếu", `NGAY` "Ngày", `THETRATRUOC` caption "Sử dụng".
- Ghi chú chéo: `DTHETRATRUOCID` trên dialog khách là `No1GridLookup`, chỉ bật khi `SuDungTheTraTruoc = 30` (`ENABLECONFIGID = 740dbdf2-…`); lookup không lọc trong layout — việc lọc thẻ khóa/hết hạn/đã gắn khách khác (nếu có) nằm trong framework, chưa xác minh được (Q10 một phần).

### 3. Voucher

#### 3.1. Nhập coupon — 3 kiểm tra (CODE = TSCODE, verbatim)

`NhapCoupon.btnOK_Click` (form 382×141, ô `txtMa` "MÃ VOUCHER", nút "Chấp nhận"/"Hủy bỏ"):

1. Trống → `"Mời bạn nhập voucher trước"`.
2. `SELECT * FROM DVOUCHER WHERE NAME = @NAME` không có dòng → `"Mã voucher 'X' không tồn tại trong hệ thống"` (+ `txtMa.SelectAll()` ở desktop; TSCODE không có dòng này).
3. `HANSUDUNG < Config.Db.DbDate` (nếu có) → `"Mã thẻ này đã quá hạn sử dụng"` (lại chữ "Mã thẻ").
4. `SELECT COUNT(*) FROM TDONHANG WHERE DVOUCHERID = '{id}' > 0` → `"Mã thẻ này đã được sử dụng"` (lại chữ "Mã thẻ").

Sau đó: `VoucherID = row.ID`, `GiaTriVoucher = row.GIATRI`, `DialogResult = OK`. Form gọi ghi `row.VOUCHER = form.Voucher`, `row.DVOUCHERID` lên bill.

Những gì **không** kiểm tra (bổ sung vòng 2):

- Không kiểm tra `NGAYPHATHANH` (ngày bắt đầu hiệu lực) — voucher chưa tới ngày phát hành vẫn dùng được.
- Không kiểm tra `DKHACHHANGID` "Đối tác phân phối" — voucher của đối tác A dùng cho khách nào cũng được.
- `COUNT(*)` **không lọc `DATHANHTOAN`** — một bill đã hủy/đổi trả mà từng gắn voucher thì voucher đó bị khóa vĩnh viễn (không dùng lại được) dù tiền chưa từng thanh toán thật. Bản clone nên lọc `DATHANHTOAN = 30` (hoặc trạng thái hủy tương ứng) khi kiểm tra đã dùng.
- Không kiểm tra `GIATRI` so với tổng bill (voucher lớn hơn bill thì sao — code gọi không cap ở đây; việc cap nằm ở XNTT, chưa đào sâu trong vòng này).

#### 3.2. Phát hành + danh mục voucher

- Dialog voucher 421×179: `NAME` "Mã voucher" (bắt buộc + không trùng), `GIATRI`, `NGAYPHATHANH` + `HANSUDUNG` (cùng hàng), `DNHOMVOUCHERID` (lookup), `DKHACHHANGID` "Đối tác phân phối" (grid lookup), `NOTE`.
- Không tìm thấy form phát hành/import voucher hàng loạt (Q24 còn mở).
- `LuaChonVoucherTuDanhSach`: giá trị dùng là `INTVALUE = 0` → **gõ tay mã voucher** (không chọn từ danh sách). Cột thứ 11 trong file xuất (= 30 với 11 dòng, trong đó có dòng này) chưa xác định được là cột nào trong 29 cột `SCONFIG` (nghi là `OTHERCONFIG`) — ghi chú, không dùng làm đáp án chính (Q23 một phần).

### 4. Khuyến mại

#### 4.1. Bảng loại KM trong DEMO — 5 dòng, không phải 8 (Q16)

| ID (GUID) | Tên trong DEMO | Ứng với hằng code |
|---|---|---|
| `8a96fc6b-d34c-4386-b421-400372a0b536` | Giảm giá tổng bill | `GIAMGIATONGBILL` (map theo tên — suy luận mạnh) |
| `7c566d6f-6386-4e93-a01c-3645e1aba5e7` | Giảm giá theo nhóm hàng | `GIAMGIATHEONHOM` (map theo tên) |
| `8402c931-8a13-4d19-a693-53f9f683647e` | Giảm giá % theo sản phẩm | `GIAMGIATHEOSANPHAM` (map theo tên) |
| `8be106e5-37b5-4523-8eec-a7c6ea0d40f9` | Giảm giá tiền theo sản phẩm | **Không có nhánh xử lý nào trong code** (grep toàn bộ CODE/TSCODE/DESIGNCODE/AELAYOUT = 0; code chỉ dùng đúng 8 hằng) — đợt loại này sẽ không áp dụng khi bán (xem §f) |
| `e5d39bb6-41a4-43ff-bc4d-e170ae4aeba2` | Mua x sản phẩm tặng y sản phẩm | `MUA1TANG1` (khớp GUID hardcode trong code nhà hàng) |

4 loại trong code **không có dòng seed** trong DEMO: `GIAMTHEOGIATRIDONHANG`, `GIAMTHEOSOLUONGMATHANG`, `MATHANGDONGGIA`, `TANGTHEOTRIGIA`. Lưu ý: 5 dòng trên là dữ liệu DEMO; DB POS TAP thật có thể có đủ 8 dòng — phải kiểm tra trên `DATA.fdb` thật trước khi kết luận "thiếu dòng seed" trong spec (Q39).

#### 4.2. `TANGTHEOTRIGIA` — kết luận chắc chắn (Q17)

- **Có tab nhập liệu đầy đủ**: `pageTangTheoTriGia` ("Tặng theo trị giá") + `grTangTheoTriGia` với 6 cột (giải mã từ AELAYOUT `DDOTKHUYENMAI`, 100.791 byte): `NOTE`, `GIATRIDONHANG` (SORT ASC — ngưỡng trị giá), `SOLUONGTANG` (số lượng tặng), `DMATHANGTANGID` caption "Mã hàng tặng", `DMATHANG.NAME` caption "Tên hàng tặng", `DMATHANG.GIABAN` caption "Giá bán" (hiển thị tham chiếu).
- **Không có code áp dụng nào**: grep toàn bộ CODE/TSCODE/DESIGNCODE/AELAYOUT — loại này chỉ xuất hiện trong `stablesdesc.txt` ở nhánh `LoadPage` + `LoadTangTheoTriGia()` (chỉ gán `grTangTheoTriGia.MasterTable = mapper; LoadData()`); `CanhBaoKhuyenMai` không có nhánh hiển thị cho loại này; cả 2 form hóa đơn không có nhánh nào.
- **Kết luận: nhập được, không chạy.** Đợt loại này lưu được dữ liệu nhưng không tặng gì khi bán. Bản clone phải tự viết luật áp dụng (ngưỡng `GIATRIDONHANG` + tặng `SOLUONGTANG` món `DMATHANGTANGID`, lấy món tặng từ cột đã có) hoặc bỏ loại này khỏi danh mục loại KM. Đây là quyết định nghiệp vụ cần Chiến chốt, không phải lỗi đọc thiếu.

#### 4.3. Lưới các tab đợt KM (giải mã từ AELAYOUT `DDOTKHUYENMAI`)

| Tab | Lưới | Cột (caption) |
|---|---|---|
| Giảm tổng bill | `pnlGiamTongBill` (không phải tab) | `TILEGIAMGIA` (tỉ lệ % tổng bill) |
| Theo nhóm | `pageTheoNhom` | cây nhóm (`LoadNhom`, không phải ViewConfig) |
| Theo giá trị đơn hàng | `grTheoGiaTri` | `TILEGIAMGIA` "Tỉ lệ giảm (%)", `GIATRIDONHANG` "Giá trị đơn hàng từ" |
| Theo số lượng | `grTheoSoLuong` | `TILEGIAMGIA` "Tỉ lệ giảm (%)", `SOLUONGMATHANG` "Tổng số lượng từ" |
| Mua 1 tặng 1 | `grMua1Tang1` | "Mặt hàng mua" (+ ĐVT mua), `SOLUONGMUA`, `SOLUONGTANG`, "Mặt hàng tặng" (+ ĐVT tặng) |
| Đồng giá | `grDongGia` | "Mặt hàng" (+ ĐVT), `GIABAN` caption "Giá khuyến mại" |
| Theo sản phẩm | `grTheoSanPham` | `TILEGIAMGIA`, "Mặt hàng" (+ ĐVT) |
| Tặng theo trị giá | `grTangTheoTriGia` | xem §4.2 |

Chữ "từ" trong caption ("Giá trị đơn hàng từ", "Tổng số lượng từ") xác nhận ngữ nghĩa ngưỡng: giảm khi đơn **đạt từ** mức đó trở lên. Có đúng 7 tab + 1 panel = 8 khu vực UI cho 8 loại code; loại DB `8be106e5` ("Giảm giá tiền theo sản phẩm") **không có tab nào** — khớp với việc nó không có nhánh xử lý.

#### 4.4. Mua 1 tặng 1 — BH vs NH + `GetSoLuongTra` (Q19/Q20, đầy đủ)

**BH** (`HoaDonBanHang`): số tặng = `FLOOR(SL / SOLUONGMUA) × SOLUONGTANG`; tặng bằng dòng mới 0 đồng cùng món (hoặc món tặng khác).

**NH** (`HoaDonNhaHang`, dòng ~3117–3202): tặng cùng món:

```csharp
int slTang = (int)(SoLuongTang * (int)(SoLuong / (SoLuongMua + SoLuongTang)));
//trường hợp đặc biệt
//mua 3 tặng 2, mua 4 được tặng 1
//              mua 14 tặng 5
int tiLe = (int)((SoLuong - slTang) / SoLuongMua);
int slTangKhaDung = (int)(SoLuong - tiLe * SoLuongMua);
if (slTangKhaDung < tiLe * SoLuongTang)
{
    slTang = slTangKhaDung;
}
if (slTang <= 0) continue;
//trừ số lượng ... (tạo dòng tặng âm DMATHANGID='KM')
```

- Công thức nền: chia cho `(MUA + TẶNG)` thay vì `MUA` — mua 1 tặng 1 thì mua 1 được **0** (vì `1/(1+1) = 0`), mua 2 tặng 1 mới được 1. Khác hẳn BH.
- Hiệu chỉnh lẻ: comment trong code ghi rõ "mua 3 tặng 2, mua 4 được tặng 1, mua 14 tặng 5" — logic `slTangKhaDung` xử lý phần lẻ.
- Tặng bằng dòng âm `DMATHANGID = 'KM'` (khác BH tặng dòng 0 đồng).

**Tặng khác món (NH, dòng ~3177):** tính `slXuat = Min(SoLuong, SL món tặng trên bill)` rồi **không dùng** — dòng ghi vẫn dùng `SoLuong` (số lượng món mua) làm số lượng tặng. Vòng 1 nghi bug — **vòng 2 xác nhận**: biến `slXuat` được gán nhưng không có dòng code nào đọc nó sau đó. Hệ quả: tặng vượt quá số lượng món tặng thực có trên bill (vd mua 5 món A, bill chỉ có 2 món B, vẫn tặng 5 món B). Xem §f.

**`GetSoLuongTra`** (`HoaDonNhaHang`, dòng 3217, nguyên văn):

```csharp
public static decimal GetSoLuongTra(string DMATHANGID, DataTable dtHoaDon, DateTime denGio)
{
    decimal SoLuong = 0;
    foreach (DataRow r in dtHoaDon.Rows)
    {
        if (r["DMATHANGID"].ToString() == DMATHANGID)
        {
            DateTime gioThem = ConvertTo.Date(r["TIMECREATED"]);
            if (gioThem > denGio)
            {
                SoLuong += ConvertTo.Decimal(r["SLXUATCHUAQUYDOI"]);
            }
        }
    }
    return Math.Min(SoLuong, 0);
}
```

- `denGio` = `ConvertTo.Date(r["DENGIO"])` — `DENGIO` chỉ là **giờ** (vd 22:00), còn `TIMECREATED` là **ngày+giờ đầy đủ**. Phép so sánh `gioThem > denGio` so nguyên datetime: kết quả phụ thuộc hoàn toàn vào phần ngày mà `ConvertTo.Date` gán cho giá trị giờ (ngày gốc 1899-12-30 hay ngày hiện tại) — code không chuẩn hóa về cùng một ngày. Nếu phần ngày là ngày gốc thì điều kiện **luôn đúng** với mọi dòng (mọi dòng đều sau năm 1899); nếu là hôm nay thì chỉ tính dòng thêm sau giờ đó trong ngày.
- `return Math.Min(SoLuong, 0)` — hàm chỉ trả về ≤ 0, tức chỉ trừ, không bao giờ cộng. Vì dòng bán thường có `SLXUAT` dương, tổng sẽ dương → `Min` → **0** — hàm **không có tác dụng gì** trừ khi bill chứa dòng trả có số lượng âm. Dòng trả có tồn tại trong `dtHoaDon` của NH hay không thì code đã đọc không cho biết (NH không có tab đổi trả như BH) — cần kiểm chứng bằng app (Q20 trả lời được phần code, phần hành vi còn mở).
- Nơi gọi: `SoLuong += GetSoLuongTra(r["DMATHANGID"], dtHoaDon, denGio)` (dòng 3177) rồi `SoLuong = (int)(SoLuong * SoLuongTang / SoLuongMua)` — số lượng mua dùng tính tặng bị trừ đi lượng trả sau `DENGIO` (nếu hàm trả về âm).

#### 4.5. Báo cáo KM chi tiết — báo cáo tự sinh, SQL rỗng là bình thường (Q27)

- File `sreport_sql/BÁO_CÁO_CHI_TIẾT_HÀNG_KHUYẾN_MẠI.txt`: SQL rỗng (`b''`), `PARAMS = <DocumentElement />`, `HASDETAIL = 0`, `SQLMODE = 0`.
- Nhưng manifest `sreport_template_manifest.txt` có dòng: `59731503-… | BÁO CÁO HÀNG KHUYẾN MẠI | 07005fb8-6d57-41ed-be32-a360d8dd7416 | … | AUTOGENREPORT=30 | TPL_BYTES=0 | CFG_BYTES=4715 | (không có file .frx)`.
- File `.config` (`07005fb8-….config`, 4715 byte) định nghĩa 12 cột: Số phiếu (`TDONHANG_NAME`), Ngày (`TDONHANG_NGAY`), Tên khách hàng (`DKHACHHANG_NAME`), Địa chỉ (ẩn), Nhân viên xuất (ẩn), Thu ngân (ẩn), Đã thanh toán (`NotShow`), Mặt hàng (`DMATHANG_NAME`), ĐVT (ẩn), Mã hàng (ẩn), Giảm giá % (`TILEGIAMGIA`, ẩn), Số lượng (`SLXUAT`).
- **Kết luận:** đây là báo cáo **tự sinh** (`AUTOGENREPORT = 30`) — dữ liệu sinh lúc chạy từ `.config`, không từ SQL và không từ `.frx`. SQL rỗng là **bình thường**, không phải báo cáo hỏng. Bối cảnh rộng hơn: **118/156** template trong manifest là `AUTOGENREPORT = 30` và 118 template có `TPL_BYTES = 0` — tức phần lớn báo cáo "không có SQL" ở vòng 1 (73/128) thực ra là báo cáo tự sinh chạy bằng config, không phải lấy số liệu từ mẫu `.frx`. (Ghi chú chéo cho nhóm Báo cáo: cần rà lại kết luận "73 báo cáo không có SQL" theo hướng này.)
- Lưu ý: tên báo cáo trong manifest là "BÁO CÁO HÀNG KHUYẾN MẠI" (không có chữ "CHI TIẾT"), ID `07005fb8-…` trùng với file SQL — cùng một báo cáo, tên hiển thị khác nhau giữa 2 nguồn.

#### 4.6. Kiểm tra giờ KM + đợt trùng nhau

- Kiểm tra giờ (`TUGIO`/`DENGIO`) chỉ có ở NH, không ở BH (C8 đúng). Nhánh tiền giờ sẽ bỏ ở bản clone nên không đào sâu thêm (Q25 còn mở nhưng đã hạ ưu tiên theo quyết định chốt).
- Kiểm tra trùng thời gian đợt KM bị comment trong `mapper_BeforeSave` — app gốc cho 2 đợt cùng loại trùng ngày; khi trùng thì hóa đơn lấy Max — có phải hành vi được chấp nhận không thì cần Chiến chốt (Q26 còn mở).

### 5. Danh mục khách hàng — đầy đủ theo schema POS TAP (26 cột)

#### 5.1. Dialog khách — 466×513, 1 cột, 20 trường, KHÔNG có group box (E18 sửa, Q9/Q10/Q11)

Giải mã từ `STABLEDESC.AELAYOUT` của `DKHACHHANG` (layout đầy đủ, không còn cắt 4000 ký tự). Thứ tự theo Y:

| Y | Trường | Control | Ghi chú |
|---|---|---|---|
| 5 | `MAKHACH` | textbox | Mẫu mặc định `{KH(*****)}` (xem Q1) |
| 31 | `NAME` | textbox | Bắt buộc + không trùng (mapper) |
| 57 | `DIACHI` | textbox | |
| 84 | `DIENTHOAI` | textbox | |
| 111 | `EMAIL` | textbox | |
| 139 | `NGAYSINH` + `DIEMTICHLUYBANDAU` | cùng hàng | `DIEMTICHLUYBANDAU` `DecimalPlaces = 2` |
| 165 | `GIABAN` | `cboGIABAN` ComboBox 316px | `ENABLECONFIGID = SuDungGiaTheoKhach` |
| 192 | `DNHANVIENID` | lookup | |
| 219 | `DNHOMKHACHHANGID` | lookup | |
| 246 | `DTINHTHANHID` | lookup | |
| 273 | `FACEBOOK` | textbox | |
| 299 | `DTHETRATRUOCID` | `No1GridLookup` | Chỉ bật khi `SuDungTheTraTruoc = 30` |
| 326 | `CANCUOCCONGDAN` | textbox thường | Không nhóm "pháp lý", không validation trong layout |
| 353 | `NGUOIMUAHANG` | textbox thường | |
| 379 | `MASOTHUE` | textbox thường | |
| 405 | `PHUONGTHUCTT` | textbox thường | |
| 431 | `SOTAIKHOAN` | textbox thường | |
| 457 | `NGANHANG` | textbox thường | **Không** phải lookup tới `DNGANHANG` (58 dòng đã tra: ABBANK, ACB, Agribank, …, Vietcombank, VietinBank, VPBank…) |
| 483 | `NOTE` | textbox | |

- Bản nháp ghi 466×410 — **rộng đúng, cao sai**: thực tế 466×**513**.
- Không có group box/phân nhóm nào — bản nháp chia nhóm "Pháp lý"/"Hóa đơn điện tử" là **sai với layout thật** (E3 sửa theo hướng này).
- Mapper Maps xác nhận: `NAME` `AllowEmpty = False` + `AllowDuplicate = False`; `MAKHACH` không có `AllowEmpty`/`AllowDuplicate` trong mapper (ràng buộc bắt buộc/không trùng của nó chỉ có ở SCOLUMN).
- `GIABAN` trên dialog là `cboGIABAN` — xem §5.2.

#### 5.2. Giá theo khách — `GIABAN` / `LOAIGIA` / `ChonLoaiGia` (Q7, đầy đủ + 1 nghi bug mới)

`SetLoaiGia` (Hóa đơn bán hàng):

| `LOAIGIA` | Giá áp dụng |
|---|---|
| 0 / mặc định | `GIABAN` |
| 1 | `GIABAN2` |
| 2 | `GIABAN3` |
| 3 | `GIABAN4` |
| 4 | Giá bán gần nhất của khách: `… DATHANHTOAN=30 ORDER BY NGAY DESC, TIMECREATED DESC` |

Khi chọn khách và `SuDungGiaTheoKhach = 30`: `LoaiGia = SELECT GIABAN FROM DKHACHHANG` rồi `SetLoaiGia` lại toàn bộ dòng. `GetDienGiai`: 1 → `DienGiaiGia2`, 2 → `DienGiaiGia3`, 3 → `DienGiaiGia4`, còn lại "Giá bán".

Form `ChonLoaiGia` ("Chọn loại giá", 324×261): 4 nút Tag **1, 2, 3, 4** với nhãn "Giá bán" / "Giá bán 2" / "Giá bán 3" / "Giá bán 4" (`form_Load` gắn thêm giá `GIABAN`..`GIABAN4` vào nhãn). **Lệch 1 so với `SetLoaiGia`**: bấm "Giá bán" (hiện giá `GIABAN`) lại áp `GIABAN2`; bấm "Giá bán 4" lại áp giá gần nhất; **không có nút nào chọn được `LOAIGIA = 0` (`GIABAN`)**. Bản cảm ứng `ChonLoaiGiaTs` gán `DonGia` trực tiếp theo nút (`GIABAN`..`GIABAN4`) nên đúng — **desktop và cảm ứng mâu thuẫn nhau ở nút giá** (xem §f).

Dữ liệu DEMO liên quan: `DienGiaiGia2/3/4` (TEXTVALUE) = "Giá 2", **"Giá 2"**, "Giá 4" — `DienGiaiGia3` ghi "Giá 2" là sai chữ trong dữ liệu seed (đáng lẽ "Giá 3"); `SuDungGia2/3/4` đều = 0 (tắt) → trong DEMO, dialog `ChonLoaiGia` chỉ bật nút "Giá bán".

**Đừng nhầm hai thang 0–4:** `DGIAMATHANG` (5 dòng tra từ DB: 0 "Hiển thị để lựa chọn", 1 "Giá chung", 2 "Giá bán t2", 3 "Giá bán t3", 4 "Giá bán cafe") là loại giá theo **khu vực** (`DKHUVUC.DGIAMATHANGID`, enum `GiaMatHang` trong code nhà hàng), khác với `LOAIGIA` của hóa đơn.

#### 5.3. Lưới danh mục khách + lookup chọn khách (E8/E9)

`SGRID` có dòng cho `DKHACHHANG` (ID `5fcc571a-…`, key theo `STABLEDESCID`): `NAME` 97, `DNHOMKHACHHANGID` 130, `MAKHACH` 68, `DIACHI` 186, `DIENTHOAI` 139, `EMAIL` 139, `MASOTHUE` 139, `DNHANVIENID` 78, `DIEMTICHLUYBANDAU` 105, `NGAYSINH` 80 (hiện), `NOTE` 150 (ẩn). Khớp đúng bảng lưới trong bản nháp — E8 giải quyết. (Dòng `SGRID` còn lại là `DMATHANG`.)

`SHOWONLOOKUP = 30` đúng cho 4 cột khách: `NAME`, `MAKHACH`, `DIACHI`, `DIENTHOAI` — danh sách chọn khách hiện 4 cột này (E9 giải quyết). So sánh: thẻ chỉ `NAME`; đợt KM / voucher / tăng-giảm điểm chỉ `NAME`.

#### 5.4. Dialog nhóm khách — đầy đủ + bảng chân lý sửa (E19)

Trường theo Y: `NAME` (bắt buộc + không trùng), `SIMAGEID` ("Ảnh:", `lueSIMAGEID` tại (400,12) — E7 giải quyết: có control nhập dù SCOLUMN không có dòng cho nó), `DIEMTICHLUY` + gợi ý "(Khi khách hàng đạt tới số điểm này sẽ tự động được chuyển sang nhóm)", tiêu đề "Giảm giá tổng hóa đơn", `TILEGIAMGIATIENHANG` (y=105) + `TILEGIAMGIATIENGIO` (y=105), tiêu đề "Giảm giá trên từng mặt hàng", `TILEGIAMGIA` (ẩn mặc định, y=138), `TILEGIAMDOAN` (163) / `DOUONG` (190) / `DICHVU` (217) / `DOKHAC` (244), `NOTE` (270).

**Bảng chân lý đúng** (sửa vòng 1 §3.2): `lbl/numTILEGIAMGIA` (giảm tổng) có `Visible = False` mặc định trong layout; code `mapper_OnLoad` chỉ bật nó khi `CoTienGio && GopChung`. Các trường hợp còn lại đều ẩn. Với POS TAP (`CoTienGio = 0`) thì ô giảm tổng của nhóm **không bao giờ hiện trên dialog** — nhưng `HoaDonBanHang.GetTiLeGiamTheoKhach` vẫn đọc `nhomRow.TILEGIAMGIA`, nên giá trị này kẹt ở dữ liệu DB (mặc định 0) và không sửa được qua UI. Ghi vào spec clone: hoặc luôn hiện ô này, hoặc bỏ cột.

#### 5.5. Dialog tăng/giảm điểm — 370×233 (Q31 liên quan)

`NGAY` (mặc định Today, bắt buộc), `NAME` "Số phiếu" (bắt buộc + không trùng), `DKHACHHANGID` (grid lookup, bắt buộc), `DIEMTANG`, `DIEMGIAM`, `LYDO` (**bắt buộc** — `AllowEmpty = False` trong mapper, vòng 1 chưa ghi), `NOTE` (multiline 106px).

#### 5.6. Số thập phân trên UI số nguyên (Q8 một phần)

`DecimalPlaces = 2` trên `numDIEMTICHLUYBANDAU` và cả 8 ô số của dialog nhóm (7 tỉ lệ + `DIEMTICHLUY`) — UI cho nhập 2 số thập phân dù cột DB là `INTEGER`/`BIGINT`; `MAXVALUE`/`MINVALUE` trong SCOLUMN đều = 0 (không giới hạn cấu hình). Nhập 12,5 vào thì làm tròn/cắt/báo lỗi vẫn chưa rõ (framework) — còn mở.

### 6. SMS / tin nhắn (Q37, xác nhận lại lần nữa)

- Menu "Gửi tin nhắn tới khách hàng" có `OPERATION = 7` duy nhất + quyền `531fb633`; grep `TinNhan`/`SMS`/`SendSMS` toàn bộ CODE/TSCODE/DESIGNCODE/AELAYOUT = 0; `SSMSTEMPLATE` 0 dòng; không có form/cấu hình SMS nào.
- 2 config liên quan chỉ xuất hiện trong `sconfig.txt`, không code nào đọc: `CanhBaoChuongTrinhKhuyenMai` và `ThongBaoKhachDenNgaySinhNhat`. Không biết bấm menu thì mở form gì — còn mở (Q37). Giả thuyết mạnh nhất vẫn là **menu chết / chức năng chưa triển khai**; bản clone không làm SMS trừ khi Chiến yêu cầu riêng.

### 7. Quyền nhóm Khách hàng (Q12/Q38)

`SGROUPUSER` 5 nhóm: Thu ngân `e6b89ecd`, Thủ kho `bdf36303`, Kế toán `4195e059`, Quản lý `c75f95fd`, Phục vụ `7d908baf`. `SGROUPROLE.MODE`: 240 (126 dòng), 0 (104 dòng), 16 (28 dòng), 80 (8 dòng), 112 (2 dòng). 240 = 0xF0 = Xem(16) + Sửa(32) + Thêm(64) + Xóa(128) — khớp bản nháp.

Quyền 6 chức năng nhóm Khách hàng (`Danh mục khách hàng 36c3bad1`, `Đợt KM e73d65a7`, `Voucher cacb3963`, `Thẻ trả trước 09f35712`, `Khách hàng thân thiết 43e435f8`, `Gửi tin nhắn 531fb633`):

| Nhóm người dùng | 6 quyền trên |
|---|---|
| Kế toán, Quản lý | 240 (đủ 4 quyền) cho cả 6 |
| Thu ngân, Phục vụ | 0 cho cả 6 |
| Thủ kho | chỉ có 1 dòng cho Khách hàng thân thiết (= 0); tổng 12 dòng SGROUPROLE (có 240 cho Danh mục mặt hàng và Tồn kho) |

`STABLEDESC.SFUNCTIONID` tồn tại (E12 giải quyết): `DKHACHHANG → 36c3bad1`, `DTHETRATRUOC → 09f35712`, `DDOTKHUYENMAI → e73d65a7`, `DVOUCHER → cacb3963`; `DNHOMKHACHHANG`, `DDOTKHUYENMAICHITIET`, `DNHOMVOUCHER`, `DLOAIHINHKHUYENMAI`, `TTANGGIAMDIEM`, `DNHOMTHETRATRUOC` không có `SFUNCTIONID`. Chưa xác minh: khi thiếu quyền thì nút bị ẩn hay mờ (framework); menu "Danh mục thẻ trả trước" vào bằng đường nào khi không có dòng menu trỏ tới quyền `09f35712` (Q38 một phần).

---

## (c) Đáp án cho các câu hỏi vòng 1 đã trả lời được

### c.1. Trả lời đầy đủ (15 câu)

| # | Đáp án (tóm tắt) | Bằng chứng |
|---|---|---|
| Q6 | Bảng chân lý đúng: `TILEGIAMGIA` chỉ hiện khi `CoTienGio` ĐÚNG **và** `GopChung = 30`; các trường hợp còn lại đều ẩn. Với POS TAP (`CoTienGio = 0`) ô này không bao giờ hiện. | Layout `DNHOMKHACHHANG` (`Visible=False` mặc định) + `mapper_OnLoad` |
| Q7 | `GIABAN` là `cboGIABAN` trên dialog khách, `ENABLECONFIGID = SuDungGiaTheoKhach`; `SetLoaiGia` 0–4 như bảng §5.2; `ChonLoaiGia` desktop **lệch 1** (không chọn được `LOAIGIA = 0`), cảm ứng đúng. | CODE `HoaDonBanHang` + AELAYOUT `ChonLoaiGia`/`ChonLoaiGiaTs` + SCOLUMN đủ trường |
| Q9 | `CANCUOCCONGDAN` là textbox thường tại y=326 (giữa Thẻ trả trước và Người mua hàng), không nhóm "pháp lý", không validation trong layout; không xuất hiện trên hóa đơn/báo cáo nào trong code đã xuất. | AELAYOUT `DKHACHHANG` + grep toàn `raw/` |
| Q10 | `DTHETRATRUOCID` là `No1GridLookup` trên dialog khách, chỉ bật khi `SuDungTheTraTruoc = 30`. Lọc thẻ khóa/hết hạn/đã gắn khách khác chưa xác minh được (nằm trong framework). | AELAYOUT `DKHACHHANG` + SCOLUMN (`ENABLECONFIGID = 740dbdf2`) |
| Q11 | Cả 4 cột e-invoice đều là textbox thường; `NGANHANG` **không** phải lookup tới `DNGANHANG` (58 tên ngân hàng đã tra: ABBANK, ACB, Agribank, …, Vietcombank, VietinBank, VPBank…); không có validation `SOTAIKHOAN` là số trong layout; `NGUOIMUAHANG` không có giá trị mặc định trong layout/mapper. | AELAYOUT `DKHACHHANG` + tra DB `DNGANHANG` |
| Q13 | Lưới mặc định: `NAME` 97, `DNHOMKHACHHANGID` 130, `MAKHACH` 68, `DIACHI` 186, `DIENTHOAI` 139, `EMAIL` 139, `MASOTHUE` 139, `DNHANVIENID` 78, `DIEMTICHLUYBANDAU` 105, `NGAYSINH` 80 (hiện), `NOTE` 150 (ẩn). `FORMAT` rỗng → không có cơ sở cho "5 cặp mẫu in" (xem E10). | `SGRID` dòng `DKHACHHANG` + `STABLEDESC.FORMAT` = 0 byte |
| Q16 | 5 dòng `DLOAIHINHKHUYENMAI` (bảng §4.1): 3 dòng đầu map theo tên, `MUA1TANG1 = e5d39bb6-…` khớp GUID hardcode; dòng `8be106e5` "Giảm giá tiền theo sản phẩm" **không có nhánh xử lý**; 4 loại code không có dòng seed trong DEMO. | Tra trực tiếp DB + grep toàn `raw/` |
| Q17 | `TANGTHEOTRIGIA`: **chỉ là tab nhập liệu, không chạy khi bán** (chắc chắn). Lưới có đủ cột ngưỡng + món tặng (§4.2); không có code áp dụng nào trong toàn bộ CODE/TSCODE/DESIGNCODE/AELAYOUT. | AELAYOUT `DDOTKHUYENMAI` + grep toàn `raw/` |
| Q19 | BH: `FLOOR(SL/SOLUONGMUA)×SOLUONGTANG`, tặng dòng 0 đồng. NH: chia `(MUA+TẶNG)`, tặng dòng âm `DMATHANGID='KM'`, mua 1 tặng 1 thì mua 1 được 0; tặng khác món tính `slXuat` rồi **không dùng** (xác nhận bug). | CODE `HoaDonBanHang` + `HoaDonNhaHang` dòng ~3117–3202 |
| Q20 | `GetSoLuongTra` đọc nguyên văn (§4.4): so `TIMECREATED` (ngày+giờ) với `ConvertTo.Date(DENGIO)` (chỉ giờ) — phần ngày quyết định kết quả; `return Math.Min(SoLuong, 0)` nên chỉ trừ, và bằng 0 nếu bill không có dòng trả âm. | `HoaDonNhaHang` dòng 3217 |
| Q27 | Báo cáo KM chi tiết là **báo cáo tự sinh** (`AUTOGENREPORT = 30`): SQL rỗng là bình thường; dữ liệu từ `.config` 12 cột (§4.5); không có file `.frx`. 118/156 template là tự sinh. | `sreport_sql/…`, manifest + file `.config 07005fb8-…` |
| Q28 | `grNapTien` = `TTHUCHI` (Số phiếu `NAME`, Ngày `NGAY`, "Số tiền nạp" `THU`, "Chi trả" `CHI`); `grThanhToan` = `TDONHANG` (Số phiếu `NAME`, Ngày `NGAY`, "Sử dụng" `THETRATRUOC`). | `ViewConfig` trong AELAYOUT `DTHETRATRUOC` + tra ngược SCOLUMN |
| Q30 | Số dư thẻ: desktop `SUM(THU−CHI) − SUM(THETRATRUOC)`; cảm ứng `SUM(THU) − SUM(THETRATRUOC)` — cảm ứng **không trừ CHI** nên hiển thị cao hơn sau khi chi trả thẻ; **desktop đúng**. Cả hai đều loại bill hiện tại. | CODE + TSCODE `NhapTheTraTruoc.GetSoTienConLai` |
| Q32 | Lưới Khách hàng thân thiết: 11 cột `grKhachHang` + công thức điểm **thứ ba** (§1.3); `grTangDiem` 6 cột; `grHoaDon` 5 cột. | `ViewConfig` trong AELAYOUT `KhachHangThanThiet` |
| Q33 | Desktop `TruTichLuy`: **không** đồng bộ điểm↔tiền, **không** validation, không nút "Sử dụng hết", không tham số `TongTien`. Cảm ứng: đồng bộ 2 chiều + validation `0 < dùng ≤ hiện có` + nút "Sử dụng hết" (cap theo tổng tiền). Nhập tay ở cảm ứng vẫn có thể vượt tổng tiền (chỉ kiểm tra ≤ điểm hiện có). | CODE `TruTichLuy` (1118 byte) + TSCODE `TruTichLuyTs` (2908 byte) + AELAYOUT (chỉ 1 event `btnOK_Click` ở desktop) |
| Q34 | Thanh toán gộp (`NHOMGUID`): nhánh gộp chỉ ghi `TIENTHANHTOAN`/`TRALAI`/`KHACHDUA`/trạng thái lên từng bill; tiền thẻ, trừ tích lũy, voucher, `DIEMGIAM` chỉ ghi trên **bill hiện tại** — nhưng vì cộng dồn theo thẻ/khách nên số dư thẻ **vẫn trừ đúng 1 lần**, điểm tiêu **vẫn ghi đúng 1 lần** (sửa kết luận vòng 1). **Cái thiếu thật: `DIEM` chỉ cộng cho bill hiện tại**, các bill khác trong nhóm không được cộng điểm. Cảm ứng cùng cấu trúc (`LOAITHANHTOAN = 0` hardcode). | CODE `HoaDonNhaHang` dòng ~2798–2920 + TSCODE tương ứng |

### c.2. Trả lời một phần (6 câu)

| # | Phần trả lời được | Phần còn mở |
|---|---|---|
| Q1 | Mẫu mã tồn tại: `MAKHACH.DEFAULTCONFIGID = {KH(*****)}` trong SCOLUMN đủ trường → bản nháp đúng về mẫu (E2 giải quyết). | Số kế tiếp = max+1 hay đếm dòng? Mã nhập tay có làm số nhảy không? Mã sửa được sau khi lưu không? (tăng số nằm trong framework) |
| Q8 | `DecimalPlaces = 2` trên `numDIEMTICHLUYBANDAU` + cả 8 ô số dialog nhóm; `MAXVALUE`/`MINVALUE` = 0 (không giới hạn cấu hình) → khoảng "±10¹²/±10¹⁶" trong bản nháp không có cơ sở. | Nhập 12,5 thì làm tròn/cắt/báo lỗi? Nhập âm hoặc > 100% có được không? (framework) |
| Q10 | Xem Q10 ở c.1 (nơi gán + cách chọn + điều kiện bật đã rõ). | Lookup có lọc thẻ khóa/hết hạn/đã gắn khách khác không? Đổi thẻ có ảnh hưởng đơn cũ không? |
| Q12 | Giá trị `MODE` thực tế: Kế toán + Quản lý = 240 cho cả 6 quyền nhóm Khách hàng; Thu ngân + Phục vụ = 0; Thủ kho chỉ 1 dòng (= 0) cho Thân thiết (xem §7). Ý nghĩa bit 0x10/0x20/0x40/0x80 đúng (240 = 0xF0). | Khi thiếu quyền thì nút bị ẩn hay mờ? (framework) |
| Q23 | Giá trị dùng là `INTVALUE = 0` → voucher ở chế độ **gõ tay mã** (không chọn từ danh sách). `INTVALUE` là trường giá trị dùng — khớp mọi config khác đã kiểm (vd `QuyDoi = 1000`, `CachTinhDiem = 1`, `TuDongNangCap = 30`). | Cột thứ 11 trong file xuất (= 30 với 11 dòng) chưa xác định được là cột nào trong 29 cột `SCONFIG` (nghi `OTHERCONFIG`). |
| Q38 | `SGROUPROLE` đã xuất: bảng quyền như Q12 (§7). | Menu "Danh mục thẻ trả trước" vào bằng đường nào khi không có dòng menu trỏ tới quyền `09f35712`? (chưa kiểm tra đường menu) |

---

## (d) Câu hỏi còn lại sau vòng 2 (18 câu)

| # | Câu hỏi | Vì sao vẫn chưa trả lời được |
|---|---|---|
| Q2 | Thông báo lỗi nguyên văn (trống Tên/Mã, trùng Mã/Tên/nhóm…), thứ tự kiểm tra khi vi phạm nhiều lỗi cùng lúc | `CODE_LEN = 0` (`DKHACHHANG`), logic trong framework |
| Q3 | So sánh trùng: hoa/thường, có dấu/không dấu, khoảng trắng đầu/cuối; sửa chính khách đó có bị báo trùng không | SCOLUMN chỉ cho biết cột nào bị kiểm trùng, không cho biết cách so sánh |
| Q4 | Xóa khách/nhóm: hỏi gì, xóa hẳn hay đổi `STATUS`, khách đã phát sinh thì chặn hay xóa, xóa nhiều dòng? | Logic xóa trong framework |
| Q5 | Ô Tìm lọc theo cột nào, có bỏ dấu/không phân biệt hoa-thường không; chọn nhóm cha có hiện khách nhóm con không; cây nhóm thêm/sửa/xóa ở đâu (chuột phải)? | Không có layout màn danh mục trong dữ liệu (chỉ có SGRID) |
| Q14 | Lưu vết: `STATUS` = 30 ngay khi thêm? `USERCREATEDID`/`TIMECREATED` điền gì; sửa có cập nhật `USERMODIFIEDID`/`TIMEMODIFIED`, `TIMECREATED` có giữ nguyên? | Quy ước engine, chưa đối chiếu hành vi thật |
| Q15 | Nhập Tên/Mã dài 255 ký tự có lưu được không, 256 ký tự thì bị chặn gõ hay báo lỗi? Tiếng Việt có dấu lưu/đọc đúng không? | Chỉ suy từ kích thước cột |
| Q18 | Form `CanhBaoKhuyenMai` mở từ đâu? Config `CanhBaoChuongTrinhKhuyenMai` (= 0) có tác dụng gì, hiện khi nào? | Không có lời gọi nào trong toàn bộ CODE/TSCODE/`smenu`/`sfunction`; config là khóa chết trong code đã xuất |
| Q21 | Giảm tay có bị KM tự động ghi đè không (`mapper_OnCalculation` gán `Max(khách, KM)` mỗi lần tính lại)? | Hành vi ghi đè trong code — cần chạy app thật để xem giảm tay có giữ được không |
| Q22 | Ngưỡng `GIAMTHEOGIATRIDONHANG` so với số nào (`TONGCONG` sau giảm hay `TIENHANG` chưa giảm)? 2 loại mốc có áp ở form nhà hàng không? | Khác biệt BH vs NH; cần Chiến chốt chủ đích (code NH không có nhánh này — vòng 1) |
| Q24 | Phát hành voucher: có sinh/import hàng loạt không, hay chỉ nhập tay từng mã? `DKHACHHANGID` "Đối tác phân phối" có ràng buộc voucher chỉ dùng cho khách của đối tác đó không? | Không tìm thấy form phát hành; code nhập không kiểm tra đối tác |
| Q25 | Công thức tách tiền giảm gộp ở NH (`TIENGIO` ở mẫu số có phải lỗi đánh máy?); `Shared.TinhGia` tính thế nào? | Nhánh tiền giờ — sẽ bỏ ở bản clone; chỉ cần xác nhận không còn dùng, không đào sâu |
| Q26 | App gốc có cho 2 đợt cùng loại trùng ngày không (kiểm tra trùng bị comment trong `mapper_BeforeSave`)? Khi trùng thì Max ở hóa đơn có phải hành vi được chấp nhận? | Code comment vs code chạy — cần Chiến chốt |
| Q29 | Phiếu nạp có bắt buộc chọn khách không? Tiền nạp có cộng vào công nợ/quỹ như phiếu thu thường không (`KHONGTHAYDOICONGNO` được tick sẵn nghĩa là gì)? | Hành vi phiếu nạp — cần app thật |
| Q31 | Validation tăng/giảm điểm: nhập chỉ `DIEMTANG` có thật sự bị chặn như `mapper_BeforeSave` viết không (nghi bug viết nhầm `numDIEMGIAM` 2 lần — vòng 1)? Có cho nhập đồng thời cả tăng lẫn giảm trên 1 phiếu không? | Đọc trực tiếp từ code (vòng 1 §10.3) nhưng chưa kiểm chứng bằng app |
| Q35 | Tự nâng nhóm: điểm xét nâng nhóm không trừ điểm đã tiêu — có phải chủ đích? Có hạ nhóm khi điểm tụt không? Ngưỡng `DIEMTICHLUY` từng nhóm trong dữ liệu thật là bao nhiêu (DEMO có 0 nhóm khách)? | Cần dữ liệu thật (`DATA.fdb` POS TAP) |
| Q36 | `CachChonKhachHang = 0` ứng với chế độ nào (enum trong `No1Lib.Sys`)? "Mã thẻ" quẹt thực tế là `MAKHACH`? Có đầu đọc nào khác không? | Enum trong `No1Lib` (DLL, không đọc được) |
| Q37 | Bấm menu "Gửi tin nhắn tới khách hàng" thì mở form gì / làm gì? `SSMSTEMPLATE` lưu nội dung ở đâu khi bảng không có cột nội dung? Gửi qua COM modem / API SMS / hay menu chết? `ThongBaoKhachDenNgaySinhNhat` có kích hoạt gì không? | Không có code/form/cấu hình SMS nào; 2 config là khóa chết |
| Q39 | Cần dữ liệu `DATA.fdb` của POS TAP (hoặc tạo dữ liệu thử) để kiểm chứng mọi công thức với dữ liệu thật, đặc biệt trường hợp biên: thẻ quá hạn/khóa/hết tiền, `QuyDoi1DiemSangTien = 0`, hóa đơn chưa thanh toán có cộng điểm không (nhánh `CachTinhDiem = 0`). | DEMO rỗng (0 khách / 0 thẻ / 0 điểm / 0 nhóm khách) |

---

## (e) Bản sửa bảng E1–E20 (vòng 2)

"Vòng 1" = kết luận cũ; "Vòng 2" = kết luận sau khi kiểm chứng bằng AELAYOUT đầy đủ + SCOLUMN đủ trường + SGRID + SGROUPROLE + STABLEDESC.

| # | Vòng 1 | Vòng 2 |
|---|---|---|
| E1 | Sai: thiếu 6 cột, thừa 3 cột → 26 cột DB, 20 trường SCOLUMN | **Giữ nguyên.** Bổ sung: dialog xác nhận đúng 20 trường, 1 cột, 466×513 (xem E18) |
| E2 | Không kiểm chứng được (grep 0 kết quả) → bỏ hoặc giữ làm câu hỏi mở | **GIẢI QUYẾT — bản nháp ĐÚNG về mẫu mã.** `MAKHACH.DEFAULTCONFIGID = {KH(*****)}` tồn tại trong SCOLUMN đủ trường. Vòng 1 grep 0 vì file xuất thiếu cột này. Cách tăng số kế tiếp vẫn mở (Q1 một phần) |
| E3 | Lỗi thời: nhóm "Pháp lý" chỉ còn `MASOTHUE` + `CANCUOCCONGDAN`; thêm nhóm "Hóa đơn điện tử" + ô "Thẻ trả trước" | **SỬA THÊM: dialog không có group box/phân nhóm nào.** Các trường e-invoice nằm rải trong 1 cột duy nhất theo thứ tự Y: `NGUOIMUAHANG` 353, `MASOTHUE` 379, `PHUONGTHUCTT` 405, `SOTAIKHOAN` 431, `NGANHANG` 457; `CANCUOCCONGDAN` 326; `DTHETRATRUOCID` 299. Bản nháp chia nhóm là sai với layout thật |
| E4 | Sai: `SOCMND` không tồn tại; hóa đơn NH điền `TENDONVI/NGUOIMUAHANG/MASOTHUE/DIACHIKHACH/DIENTHOAI/NGANHANG/SOTAIKHOAN/PHUONGTHUCTT` (trống → `"TM"`); BH không điền | **Giữ nguyên** (vòng 2 không đào sâu lại) |
| E5 | Sai: 8 cột tỉ lệ + `DIEMTICHLUY` là `BIGINT`, không phải `NUMERIC(18,2)` | **Giữ nguyên.** Bổ sung Q8: UI `DecimalPlaces = 2` đã xác nhận; `MAXVALUE`/`MINVALUE` = 0 |
| E6 | Sai: POS TAP `DNHANVIEN: 0` (số 1 là của TEA) | **Giữ nguyên** |
| E7 | Thiếu bằng chứng: `SIMAGEID` có trong DB nhưng không có dòng SCOLUMN → chưa rõ có nhập được trên dialog nhóm không | **GIẢI QUYẾT — bản nháp ĐÚNG khi đưa `SIMAGEID` vào bảng trường.** Dialog nhóm có control `lueSIMAGEID`, nhãn "Ảnh:" tại (400,12), dù SCOLUMN không có dòng cho nó |
| E8 | Không kiểm chứng được (không có file `sgrid`) → giữ làm giả định | **GIẢI QUYẾT — bản nháp ĐÚNG.** `SGRID` dòng `DKHACHHANG`: `NAME` 97, `DNHOMKHACHHANGID` 130, `MAKHACH` 68, `DIACHI` 186, `DIENTHOAI` 139, `EMAIL` 139, `MASOTHUE` 139, `DNHANVIENID` 78, `DIEMTICHLUYBANDAU` 105, `NGAYSINH` 80 (hiện), `NOTE` 150 (ẩn) |
| E9 | Không kiểm chứng được (`SHOWONLOOKUP` 0 lần) → giữ làm giả định | **GIẢI QUYẾT — bản nháp ĐÚNG.** `SHOWONLOOKUP = 30` đúng cho 4 cột: `NAME`, `MAKHACH`, `DIACHI`, `DIENTHOAI` |
| E10 | Không kiểm chứng được (`FORMAT` 0 lần) → giữ làm giả định | **BỊ BÁC — không giữ làm giả định nữa.** `STABLEDESC.FORMAT` rỗng (0 byte) với **mọi** bảng đã trích → khẳng định "FORMAT chứa 5 cặp (ID mẫu, LOAI 1–4 và −1)" **không có cơ sở trong DEMO**. Xóa khỏi spec; "Nút In danh sách" cần cơ sở khác hoặc bỏ |
| E11 | Không kiểm chứng được (`SGROUPROLE` không xuất) → chỉ xác nhận `SFUNCTION 36c3bad1` | **GIẢI QUYẾT (phần giá trị).** Kế toán + Quản lý = 240 cho cả 6 quyền nhóm Khách hàng; Thu ngân + Phục vụ = 0; Thủ kho chỉ 1 dòng (= 0) cho Thân thiết. Bit 0x10/0x20/0x40/0x80 đúng (240 = 0xF0). Phần "nút ẩn hay mờ khi thiếu quyền" vẫn mở |
| E12 | Không kiểm chứng được (file xuất không có cột `SFUNCTIONID`) | **GIẢI QUYẾT.** `STABLEDESC.SFUNCTIONID` tồn tại: `DKHACHHANG → 36c3bad1`, `DTHETRATRUOC → 09f35712`, `DDOTKHUYENMAI → e73d65a7`, `DVOUCHER → cacb3963` |
| E13 | Không kiểm chứng được (`ENABLECONFIGID` 0 lần) → kết luận `SuDungGiaTheoKhach` chỉ điều khiển hóa đơn lấy `GIABAN` làm `LOAIGIA`, không bật/tắt ô dialog | **ĐẢO NGƯỢC một phần — vòng 1 sai về "ENABLECONFIGID 0 lần".** `GIABAN.ENABLECONFIGID = 34913ae0` = `SuDungGiaTheoKhach`; `DTHETRATRUOCID.ENABLECONFIGID = 740dbdf2` = `SuDungTheTraTruoc` (ô "Thẻ trả trước" trên dialog khách chỉ bật khi config này = 30). Vòng 1 grep 0 vì file xuất thiếu cột. Config **vừa** gating ô dialog **vừa** điều khiển hành vi hóa đơn |
| E14 | Suy luận, chưa có bằng chứng (`DIEMTICHLUYBANDAU = 0`, `GIABAN = 0` mặc định 0) | **Giữ nguyên** (không có bằng chứng mới) |
| E15 | Không kiểm chứng được; mâu thuẫn tiềm ẩn DB số nguyên vs UI thập phân | **Một phần giải quyết:** `DecimalPlaces = 2` xác nhận trên cả 8 ô số dialog nhóm + `DIEMTICHLUYBANDAU`; `MAXVALUE`/`MINVALUE` = 0 → khoảng "±10¹²/±10¹⁶" trong bản nháp **không có cơ sở cấu hình**. Có chặn > 100% hay không vẫn mở |
| E16 | Cách diễn đạt không khớp dữ liệu xuất | **Giữ nguyên** |
| E17 | Chưa chính xác: yếu tố tạo cây là `CATEGORYCOL = DNHOMKHACHHANGID`, không phải `TABLETYPE = 2` một mình | **Giữ nguyên** |
| E18 | Không kiểm chứng được → giữ làm giả định (466×410, 1 cột, 18 trường) | **SỬA: dialog 466×513 (rộng đúng, cao sai), 1 cột, 20 trường** theo thứ tự Y đã liệt kê ở §5.1 |
| E19 | Đúng về logic, thiếu 1 trường hợp + thiếu giá trị `CoTienGio` | **SỬA BẢNG CHÂN LÝ: `TILEGIAMGIA` chỉ hiện khi `CoTienGio` ĐÚNG và `GopChung = 30`; các trường hợp còn lại đều ẩn.** Với POS TAP (`CoTienGio = 0`) ô này không bao giờ hiện trên dialog nhưng `GetTiLeGiamTheoKhach` vẫn đọc cột → giá trị kẹt ở DB (mặc định 0), không sửa được qua UI. Spec clone: hoặc luôn hiện ô này, hoặc bỏ cột |
| E20 | Không có bằng chứng (cách so sánh trùng; kiểm trùng Điện thoại/MST/CCCD) | **Giữ nguyên** |

**Tổng kết E1–E20 sau vòng 2:** giải quyết dứt điểm 7 điểm (E2, E7, E8, E9, E11-phần-giá-trị, E12, E13), sửa 3 điểm (E3, E18, E19), bác 1 điểm (E10), làm rõ một phần 1 điểm (E15), giữ nguyên 8 điểm (E1, E4, E5, E6, E14, E16, E17, E20).

---

## (f) Nghi vấn giống bug — cập nhật sau vòng 2

### f.1. MỚI trong vòng 2 (chưa có ở vòng 1/Claude)

| # | Nghi vấn | Bằng chứng | Mức độ |
|---|---|---|---|
| B1 | **Form `ChonLoaiGia` desktop lệch 1 so với `SetLoaiGia`**: nút "Giá bán" (hiện giá `GIABAN`) áp `GIABAN2`; nút "Giá bán 4" áp giá gần nhất; không có nút nào chọn được `LOAIGIA = 0` (`GIABAN`). Bản cảm ứng `ChonLoaiGiaTs` gán trực tiếp nên đúng. | `ChonLoaiGia` Tag 1–4 + `SetLoaiGia` 0–4 (§5.2) | Cao — sai giá bán trực tiếp; cần Chiến chốt bản clone theo phía nào (khuyên theo cảm ứng: nút nào giá đó) |
| B2 | **`GetSoLuongTra` gần như không bao giờ có tác dụng**: trả `Math.Min(tổng, 0)` — tổng dương (dòng bán thường) → 0; chỉ trừ khi bill có dòng trả âm. Thêm nữa so sánh `TIMECREATED` (ngày+giờ) với `ConvertTo.Date(DENGIO)` (chỉ giờ) — phần ngày không được chuẩn hóa. | `HoaDonNhaHang` dòng 3217 (§4.4) | Trung bình — cần app thật xem dòng trả có tồn tại trong `dtHoaDon` NH không |
| B3 | **`NhapTheTraTruoc` báo sai chữ khi để trống**: `"Mời bạn nhập voucher trước"` trong form nhập thẻ (copy-paste từ `NhapCoupon`), cả desktop lẫn cảm ứng. | `NhapTheTraTruoc.btnOK_Click` cả CODE + TSCODE | Thấp — chỉ sai text, không sai tiền |
| B4 | **Số dư thẻ cảm ứng không trừ `CHI`**: sau "Chi trả thẻ", cảm ứng hiển thị số dư cao hơn desktop đúng bằng tổng `CHI` — có thể cho dùng vượt số dư thật (desktop cap `Min(SoTienConLai, TONGTIEN)` bằng số dư đã trừ CHI; cảm ứng dùng số dư chưa trừ). | `GetSoTienConLai` desktop vs TSCODE (§2.1) | Cao — sai tiền; bản clone lấy công thức desktop làm chuẩn |
| B5 | **`DienGiaiGia3` = "Giá 2" trong dữ liệu DEMO** (đáng lẽ "Giá 3") — sai chữ trong seed data; nếu clone bê nguyên dữ liệu DEMO thì nhãn giá 3 sẽ hiện "Giá 2". | Tra `SCONFIG` trực tiếp | Thấp — dữ liệu, sửa 1 dòng |
| B6 | **Loại KM `8be106e5` "Giảm giá tiền theo sản phẩm" không có nhánh xử lý**: đợt loại này lưu được nhưng không áp dụng khi bán; người dùng tưởng đã tạo KM nhưng thực ra không chạy. | 5 dòng `DLOAIHINHKHUYENMAI` + grep 0 kết quả (§4.1) | Trung bình — cần Chiến chốt: viết nhánh xử lý hay ẩn loại này khỏi danh mục |
| B7 | **`TANGTHEOTRIGIA` nhập được nhưng không chạy** (xem Q17): tab + lưới đầy đủ, không có code áp dụng. Cùng họ với B6 nhưng khác: loại này có đủ cột dữ liệu (ngưỡng + món tặng) chỉ thiếu luật áp dụng. | AELAYOUT `DDOTKHUYENMAI` + grep toàn `raw/` (§4.2) | Trung bình — cần Chiến chốt luật áp dụng hay bỏ loại |

### f.2. Đã có ở vòng 1 — vòng 2 xác nhận / sửa

| # | Nghi vấn | Kết luận vòng 2 |
|---|---|---|
| B8 | `TTANGGIAMDIEM.mapper_BeforeSave` viết `numDIEMGIAM` 2 lần | **Giữ nguyên nghi vấn** (vòng 1 §10.3; vòng 2 không đào sâu lại — Q31 còn mở, cần app thật) |
| B9 | MUA1TANG1 BH vs NH khác nhau | **Xác nhận, đào sâu hơn**: BH `FLOOR(SL/MUA)×TẶNG` dòng 0 đồng; NH chia `(MUA+TẶNG)` dòng âm `'KM'`; mua 1 tặng 1 thì NH mua 1 được 0 |
| B10 | Nhánh tặng-khác-món của NH tính `slXuat` rồi không dùng | **Xác nhận là bug thật trong code đã đọc**: `slXuat = Min(SoLuong, SL món tặng trên bill)` được gán nhưng không dòng nào đọc sau đó; dòng ghi dùng `SoLuong` → tặng vượt số lượng món tặng trên bill |
| B11 | Công thức điểm nâng nhóm vs hiển thị khác nhau (nâng nhóm không trừ `DIEMGIAM`) | **Xác nhận + thêm công thức thứ ba**: lưới Thân thiết trừ `SUM(DIEMGIAM)` cả hai nhánh và lọc `DATHANHTOAN=30 AND LOAI=0` — 3 công thức cùng tồn tại, số điểm có thể khác nhau tùy nơi xem |
| B12 | Thanh toán gộp `NHOMGUID` không ghi tiền thẻ/điểm vào từng bill | **Sửa kết luận**: số dư thẻ vẫn trừ đúng 1 lần, điểm tiêu vẫn ghi đúng 1 lần (ghi trên bill hiện tại, cộng dồn theo thẻ/khách). **Cái thiếu thật là `DIEM`**: các bill khác trong nhóm không được cộng điểm |
| B13 | `TILEGIAMGIA` của nhóm không hiện trên dialog POS TAP nhưng vẫn đọc khi bán | **Xác nhận**: layout `Visible=False` mặc định + chỉ bật khi `CoTienGio && GopChung`; POS TAP (`CoTienGio=0`) không bao giờ hiện; giá trị kẹt ở DB (mặc định 0) |
| B14 | Desktop `TruTichLuy` không validation | **Xác nhận + mở rộng**: desktop không đồng bộ điểm↔tiền, không validation, không nút "Sử dụng hết" — có thể nhập vượt điểm hiện có / vượt tổng tiền. Cảm ứng có validation nhưng nhập tay vẫn vượt được tổng tiền (chỉ kiểm tra ≤ điểm hiện có) |
| B15 | Voucher kiểm tra "đã dùng" bằng `COUNT(*)` không lọc trạng thái bill | **Xác nhận**: bill đã hủy/đổi trả từng gắn voucher sẽ khóa voucher vĩnh viễn. Bản clone nên lọc `DATHANHTOAN = 30` |

### f.3. Lưu ý cho spec clone (không phải bug, nhưng phải quyết)

1. **Một công thức điểm chuẩn**: app gốc có 3 công thức điểm (`GetDiemTichLuy` 2 nhánh + lưới Thân thiết) + 1 công thức xét nâng nhóm không trừ `DIEMGIAM`. Clone nên thống nhất 1 công thức (khuyên: công thức lưới Thân thiết — lọc bill đã thanh toán + trừ điểm đã tiêu — vì nó là số khách nhìn thấy), hoặc giữ nguyên 3 công thức và ghi rõ khác biệt.
2. **`ChonLoaiGia`**: lấy hành vi cảm ứng (nút nào giá đó) làm chuẩn; sửa lệch 1 của desktop, không sao chép.
3. **Số dư thẻ**: lấy công thức desktop (`THU − CHI`) làm chuẩn cho cả hai bản.
4. **`TruTichLuy`**: lấy hành vi cảm ứng (đồng bộ 2 chiều + validation + "Sử dụng hết") làm chuẩn; bổ sung thêm kiểm tra ≤ tổng tiền bill cho cả nhập tay.
5. **Thanh toán gộp**: cộng `DIEM` cho từng bill trong nhóm (theo `TONGCONG` từng bill), không chỉ bill hiện tại; ghi `THETRATRUOC`/`TRUTICHLUY`/`VOUCHER` phân bổ theo từng bill hoặc ghi rõ "chỉ ghi trên bill đại diện" trong spec.
6. **`TANGTHEOTRIGIA` + loại `8be106e5`**: Chiến chốt — viết luật áp dụng hay ẩn khỏi danh mục loại KM.
7. **`TILEGIAMGIA` nhóm**: Chiến chốt — luôn hiện ô giảm tổng trên dialog nhóm hay bỏ cột.
8. **Voucher**: kiểm tra "đã dùng" nên lọc `DATHANHTOAN = 30`; bổ sung kiểm tra `NGAYPHATHANH` (chưa tới ngày thì chặn) nếu nghiệp vụ yêu cầu; quyết định `DKHACHHANGID` "Đối tác phân phối" có ràng buộc gì không (hiện không kiểm tra).
9. **Báo cáo KM chi tiết**: là báo cáo tự sinh — bản clone làm theo cơ chế "báo cáo tự sinh từ config cột", không cần viết SQL; ghi chú chéo cho nhóm Báo cáo rà lại kết luận "73 báo cáo không có SQL".

---

## Tổng kết vòng 2

- **Trả lời đầy đủ: 15/39 câu** (Q6, Q7, Q9, Q11, Q13, Q16, Q17, Q19, Q20, Q27, Q28, Q30, Q32, Q33, Q34).
- **Trả lời một phần: 6/39 câu** (Q1, Q8, Q10, Q12, Q23, Q38).
- **Còn lại: 18/39 câu** (Q2, Q3, Q4, Q5, Q14, Q15, Q18, Q21, Q22, Q24, Q25, Q26, Q29, Q31, Q35, Q36, Q37, Q39) — phần lớn cần chạy app gốc hoặc dữ liệu `DATA.fdb` thật (đặc biệt Q39: DEMO rỗng nên mọi công thức vẫn chưa đối chiếu với dữ liệu thật nào).
- **Bảng E1–E20:** giải quyết dứt điểm 7 điểm, sửa 3 điểm, bác 1 điểm (E10), làm rõ một phần 1 điểm, giữ nguyên 8 điểm.
- **Nghi bug:** 7 mới (B1–B7, trong đó B1 lệch giá `ChonLoaiGia` và B4 số dư thẻ cảm ứng là nặng nhất) + 8 cũ đã xác nhận/sửa (B8–B15).
- **Việc cần Chiến chốt trước khi viết spec clone:** luật `TANGTHEOTRIGIA`, loại KM `8be106e5`, ô `TILEGIAMGIA` nhóm, 1 công thức điểm chuẩn, cộng điểm khi thanh toán gộp, kiểm tra voucher (lọc bill đã thanh toán + `NGAYPHATHANH` + đối tác phân phối).
