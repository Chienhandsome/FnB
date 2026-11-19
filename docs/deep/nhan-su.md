# Rà soát SÂU nghiệp vụ nhóm NHÂN SỰ — vòng 2 (đối chiếu tài liệu Claude)

- Ngày rà soát: 19/11/2026. Ngôn ngữ: tiếng Việt.
- App đích: **POS TAP** — chỉ giữ loại hình COFFEE (0) + POS (1), bỏ tính tiền theo giờ. Phần `GIOTINHLUONG`/dịch vụ theo giờ chỉ ghi tham khảo.
- Tài liệu Claude đối chiếu: `docs/pos-tap-nghiep-vu-luong.md` + `docs/pos-tap-nghiep-vu-muc-luc.md` (repo HEAD `4fdfe48`).
- Vòng 1: `pos-tap-review/nhan-su.md` (484 dòng, 16 mục, 22 câu hỏi) — vòng 2 trả lời lại từng câu bằng code + dữ liệu mới.
- **Nguồn mới của vòng 2** (vòng 1 chưa có): `raw/sform_aelayout/` (111 file AELAYOUT **đầy đủ**, không còn cắt 4000 ký tự), `raw/sform_tscode/` (26 file code cảm ứng), `raw/sform_designcode/` (99 file), `raw/sgrouprole.txt`, `raw/sgrid.txt`, `raw/sreport_template/`; đọc trực tiếp DB `FNB_DEMO.fdb` + `FNB_TEMPLATE.FDB` bằng Firebird 2.5 embedded (kiểm tra số dòng, BLOB, index/constraint/trigger). Toàn bộ file export được giải mã từ dạng bytes-literal `b'...'` trước khi đọc (CODE/AELAYOUT/DESIGNCODE/TSCODE trong file export là 1 dòng dài duy nhất chứa chuỗi escape — grep thô trên file chưa giải mã vẫn khớp từ khóa ASCII nhưng không đọc được ngữ cảnh).
- Chỉ đọc file, KHÔNG sửa repo/DB, không commit/push.

## 0. Kết quả tóm tắt

- **Đối chiếu Claude: 31/33 khẳng định của Claude được xác nhận đúng** (bảng §1); **2 điểm cần đính chính** (C1: đầu file ghi "Chưa đọc" các form mà §4–§5 lại mô tả chi tiết — mâu thuẫn nội bộ; C2: §1 viết "Xóa bảng lương thì xóa chi tiết chấm công" — thiếu: form tay chỉ xóa `TBANGLUONGCHITIET`, để lại `TBANGLUONGTONGHOP` mồ côi; chỉ form máy mới xóa cả 3 bảng).
- **Không có "công thức lương mẫu" (ExcelSalaryEngine) ở đâu cả** — grep 0 kết quả cho `ExcelSalaryEngine|CongThuc|Formula|Evaluate|SalaryEngine` trên toàn bộ CODE + AELAYOUT + DESIGNCODE + TSCODE + SCONFIG. Mọi công thức lương đều hard-code C# trong `UpdateThucNhan` (tay) và `TinhLuong` (máy). Các hit "Excel" duy nhất là xuất/nhập Excel (chọn cột, import định lượng), không liên quan lương.
- **`LUONGTHEOCA` là cột chết** — đúng 1 lần xuất hiện trong toàn bộ CODE (copy `DNHANVIEN → TBANGLUONGTONGHOP` khi tạo mới ở form máy), không dùng trong công thức nào, không hiện trên form nào, không có trong SQL báo cáo nào, không có báo cáo lương nào.
- **Phát hiện mới quan trọng nhất (B1):** form máy sau khi mở lại sẽ hiện **0 giờ / lương 0** cho tới khi bấm Refresh — vì `LayDuLieuTuMay.btnSave` không gán `TBANGLUONGID` cho dòng `TVAORA` import, mà `LoadDetail` lại tính `SOGIOLAM = SUM(SOGIO) WHERE TBANGLUONGID = SelID`. Bấm Cập nhật trong trạng thái đó sẽ **ghi đè lương đã lưu bằng 0**.
- **22 câu hỏi vòng 1: trả lời được hoàn toàn 16 câu, trả lời một phần 5 câu, còn 1 câu chưa đụng tới** (Q18 — ngoài phạm vi tính giờ). Chi tiết ở §3, danh sách còn lại ở §4.

## 1. Đối chiếu từng khẳng định của Claude

Quy ước cột Kết quả: **✓ đúng** | **△ đúng nhưng thiếu** | **✗ sai** | **? không xác minh được từ code**.

### 1.1 Mô hình (§1 tài liệu Claude)

| # | Khẳng định của Claude | Kết quả | Bằng chứng |
|---|---|---|---|
| 1.1 | `TBANGLUONG`: 1 bảng lương cho mỗi (tháng, năm) | ✓ | `TBANGLUONGAe`: `NAME = "Tháng " + THANG + "/" + NAM`; không có validate trùng tháng (xem §3-Q10) |
| 1.2 | `TBANGLUONGTONGHOP`: 1 dòng/NV/bảng lương (`LUONGCA, LUONGTHANG, CACHTINHLUONG, TONGLUONG, PHAT, THUONG, TAMUNG, THUCNHAN`) | ✓ | Schema dump: 20 cột, đúng danh sách; thêm `SOGIOLAM, SOGIOTANGCA, LUONGTHEOCA` (chỉ form máy dùng) |
| 1.3 | `TBANGLUONGCHITIET`: chấm công theo (NV, ca, ngày) với `TRANGTHAI` | ✓ | Schema + `TinhLuong.LoadDetail`: key `DNHANVIENID + DCALAMVIECID + NGAY(ddMMyy)` |
| 1.4 | "Xóa bảng lương thì xóa chi tiết chấm công" | △ **C2 — thiếu** | Form tay `grMain_OnDeleted` chỉ `DELETE FROM TBANGLUONGCHITIET` — **`TBANGLUONGTONGHOP` thành mồ côi**; chỉ form máy mới xóa cả 3 bảng (`CHITIET`, `TONGHOP`, `TVAORA`). DB không có FK/cascade nào (0 FOREIGN KEY trong toàn DB) nên không có cơ chế nào khác xóa hộ |
| 1.5 | `DiLamMode`: 0 không lịch, 1 đi làm, 2 nghỉ có phép, 3 nghỉ không phép; 0 không lưu dòng | ✓ | `enum DiLamMode` cuối file `Tính_lương.txt` (duy nhất 1 định nghĩa trong export); `workerSave_DoWork`: `val != 0` mới insert |
| 1.6 | `CachTinhLuong`: 0 theo tháng, 30 theo ca, 60 tháng theo ca; cờ `NGHICHUNHAT`, `NGHITHU7` (=30) cho cách theo tháng | ✓ | `enum CachTinhLuong { TheoThang = 0, TheoCa = 30, ThangTheoCa = 60 }`; `DNHANVIENAe.mapper_BeforeSave`: `rdTheoCa ? 30 : (rdTheoThang ? 0 : 60)` |
| 1.7 | `DCALAMVIEC`: `TILELUONG` % lương của ca (0 coi như 100) | ✓ | `MixTable`: `TILELUONG == 0 ? 100 : TILELUONG`; `mapper_AfterFillData` ca mới: `TILELUONG = 100` |
| 1.8 | NV đưa vào bảng lương: `DNHANVIEN STATUS=30`, `ITEMTYPE` rỗng/0; chưa có trong bảng lương thì lấy từ danh mục, chưa lưu | ✓ (form tay) / △ (form máy) | Form tay: UNION ALL nhánh 2 đúng như mô tả. Form máy: **khác** — chỉ tạo dòng tổng hợp khi `dt.Rows.Count == 0` (tạo cho **mọi** NV `STATUS=30`, **không lọc ITEMTYPE**); NV mới vào giữa tháng **không bao giờ xuất hiện** ở form máy (xem §2.3, B2) |

### 1.2 Công thức lương tay (§2 tài liệu Claude)

| # | Khẳng định của Claude | Kết quả | Bằng chứng |
|---|---|---|---|
| 2.1 | `soCaDiLam = Σ (TILELUONG/100)` cho mỗi ô Đi làm hoặc Nghỉ có phép | ✓ | `UpdateThucNhan`: 2 nhánh `DiLam`/`NghiCoPhep` đều `+= tiLeLuong/100` |
| 2.2 | `soNgayDiLam` = số ngày có ít nhất 1 ca Đi làm/Nghỉ có phép | ✓ | Cờ `hasDiLam` theo từng cột ngày |
| 2.3 | Nghỉ không phép: không cộng; phạt tự động đã bị comment | ✓ | Nhánh `NghiKhongPhep`: chỉ có `//phat += luongCa;` |
| 2.4 | TheoCa (30): `TONGLUONG = LUONGCA × soCaDiLam` | ✓ | `tongLuong = luongCa * soCaDiLam` |
| 2.5 | ThangTheoCa (60): `TONGLUONG = soCaDiLam × LUONGTHANG / totalDays` | ✓ | `tongLuong = soCaDiLam * luongThang / totalDays` |
| 2.6 | TheoThang (0): `TONGLUONG = soNgayDiLam × LUONGTHANG / soNgayTrongThang`, với 4 trường hợp trừ T7/CN theo cờ | ✓ | Đúng cả 4 nhánh (`tongNgayTruT7CN` / `tongNgayTruCN` / `tongNgayTruT7` / `totalDays`); `AddColumns` đếm đúng: trừ CN, trừ T7, trừ cả hai |
| 2.7 | `THUCNHAN = TONGLUONG + THUONG − PHAT − TAMUNG` | ✓ | `thucNhan = tongLuong + thuong - phat - rTongHop.TAMUNG` |
| 2.8 | `PHAT`/`THUONG` = Σ trong `TTHUONGPHAT` theo tháng; `TAMUNG` = Σ `CHI` của `TTHUCHI` có `LATAMUNG = 30` trong tháng | ✓ (form tay) | Đúng 3 subquery trong SQL `LoadDetail`/`RefreshData` của form tay. **Lưu ý:** form máy tính `TAMUNG` khác (`SUM(THU − CHI)`) — Claude không nhắc tới mâu thuẫn này (xem §2.5, B4) |
| 2.9 | Tính tại chỗ khi sửa ô chấm công; chỉ ghi DB khi bấm "Cập nhật" | ✓ | `SetSelectionColor` → `UpdateThucNhan` (chỉ bộ nhớ); `btnCapNhat_Click` → `workerSave_DoWork` mới ghi |
| 2.10 | "Tải lại" nạp lại lương cơ bản, cách tính, thưởng/phạt/tạm ứng hiện tại | ✓ | `RefreshData`: đọc lại từ `DNHANVIEN` (`LUONGCA, LUONGTHANG, CACHTINHLUONG, NGHICHUNHAT, NGHITHU7` + 3 subquery) rồi `UpdateThucNhan` toàn bộ. **Mới (B10):** đọc từ `DNHANVIEN` chứ không phải `TONGHOP` — đè cả giá trị đã chốt ở bảng tổng hợp |
| 2.11 | Chế độ chỉ xem công (menu "Chấm công") không hiện lương và nút chi | ✓ | `showLuong = CallerMenuID.NAME == "Tính lương"`; 6 cột tiền `.Visible = showLuong`; `btnChiLuong`/`btnRefresh` ẩn khi `!showLuong`. **Nhưng `btnCapNhat` vẫn hiện** (xem §2.7, B7) |

### 1.3 Chi lương và tạm ứng (§3 tài liệu Claude)

| # | Khẳng định của Claude | Kết quả | Bằng chứng |
|---|---|---|---|
| 3.1 | `Chi lương`: danh sách (NV, thực nhận, số tiền đã chi `TTHUCHI.CHI` theo `TBANGLUONGID` + NV, số phiếu) | ✓ | SQL `ChiLuong.LoadData` đúng như mô tả (LEFT JOIN `TTHUCHI` theo `TBANGLUONGID + DNHANVIENID`) |
| 3.2 | Tạo phiếu chi lương bằng `TTHUCHI1Ae.SetLuong`: diễn giải "Chi tiền lương tháng M năm Y", số tiền mặc định = `THUCNHAN`; `THUCNHAN = 0` thì cảnh báo | ✓ | `SetLuong`: `txtDIENGIAI = "Chi tiền lương tháng " + THANG + " năm " + NAM`, `numCHI.Value = SoTien`; `ChiLuong.tsbPhieuChi_Click` hỏi khi `thucNhan == 0` |
| 3.3 | `Tạm ứng lương`: danh sách phiếu chi (`TTHUCHI LOAI=1`) với `LATAMUNG = 30`; phiếu mới tự đặt `LATAMUNG = 30`, đối tượng là NV | ✓ | `TamUngLuong`: `e.Where += " AND LATAMUNG = 30"`, `AfterCreatedEditForm` → `SetValue("LATAMUNG", 30)`, `CustomAeForm: loai = 1` |

### 1.4 Tính lương bằng máy (§4 tài liệu Claude)

| # | Khẳng định của Claude | Kết quả | Bằng chứng |
|---|---|---|---|
| 4.1 | Dữ liệu vào/ra bảng `TVAORA` (`DNHANVIENID, NGAY, GIOVAO, GIORA, SOGIO, SOGIOTANGCA, NOTE, TBANGLUONGID`); lấy từ máy ZK qua `zkemkeeper\zkemkeeper.exe <IP> <file> AttLog`, IP mặc định `192.168.1.201` (config `IpMayChamCong`), chờ tối đa 2 phút | ✓ | `LayDuLieuTuMay`: đúng đường dẫn, tham số, `WaitForExit(2*60*1000)`, `SetData` lấy IP từ config, trống → `192.168.1.201` |
| 4.2 | Khớp `USERID` máy với `DNHANVIEN.CODE`; giờ vào = lần quét đầu, giờ ra = lần quét cuối (>1 lần), `SOLAN` = số lần quét; chỉ lưu giờ:phút | ✓ | `dtGet.Select("DATESTR='ddMMyyyy' AND USERID='CODE'", "TIME")`; `GIOVAO = rows[0]`, `GIORA = rows[last]` khi `> 1`; `new DateTime(2000,1,1,H,m,0)` |
| 4.3 | Tùy chọn xóa dữ liệu cũ của tháng trước khi nạp và xóa log trên máy sau khi lấy (`ClearAttLog`) | ✓ | `chkXoaDuLieuCu` → `DELETE FROM TVAORA WHERE NGAY BETWEEN đầu–cuối tháng`; `chkXoaSauKhiThucHien` → chạy lại exe với `noFile ClearAttLog` |
| 4.4 | `CapNhatGio`: `soGioPhaiLam = Round(giờ ca, 2)`; `tongGioDaLam = Round(giờ ra − giờ vào, 2)` (vào > ra thì lùi 1 ngày); vượt thì `SOGIO = soGioPhaiLam`, `SOGIOTANGCA = chênh lệch`, ngược lại `SOGIO = tongGioDaLam`, tăng ca 0; NV chưa gán ca thì 0 giờ | ✓ | Đúng từng dòng code. **Bổ sung (B3):** "NV chưa gán ca thì 0 giờ" đúng cả khi đã có `GIOVAO`/`GIORA` — vì `soGio` chỉ được gán trong nhánh `DCALAMVIECID.Length > 0` |
| 4.5 | Lương tháng: `TONGLUONG = Round((SOGIOLAM + SOGIOTANGCA × 1.5) × LUONGCA, 0)`; `THUCNHAN = TONGLUONG + THUONG − TAMUNG − PHAT`; `TAMUNG = Σ(THU − CHI)` phiếu `LATAMUNG = 30` trong tháng (khác bản tay chỉ cộng `CHI`) | ✓ | `TinhLuong` + `UpdateThucNhan` + subquery `TAMUNG` trong `LoadDetail` đúng như mô tả. Mâu thuẫn với form tay được Claude ghi nhận nhưng **không đánh dấu là nghi vấn cần chốt** (xem §2.5, B4) |
| 4.6 | Nạp ngày trống: mở một NV sẽ tự thêm các ngày chưa có dòng `TVAORA` với 0 giờ | ✓ | `grTongHop_SelectionChanged`: tạo dòng thiếu `SOGIO=0, SOGIOTANGCA=0, TBANGLUONGID=SelID` cho mọi ngày trong tháng |
| 4.7 | Xóa bảng lương xóa chi tiết, tổng hợp và `TVAORA` của bảng đó; nút Chi lương như bản tay | ✓ (form máy) | `grMain_OnDeleted` (máy): 3 câu `DELETE` đúng như mô tả; `btnChiLuong_Click` gọi `ChiLuong.SetData(SelID)` như bản tay |

### 1.5 Khai báo liên quan (§5 tài liệu Claude)

| # | Khẳng định của Claude | Kết quả | Bằng chứng |
|---|---|---|---|
| 5.1 | NV: `CACHTINHLUONG` 30 theo ca (nhập `LUONGCA`), 0 theo tháng (nhập `LUONGTHANG` + `NGHICHUNHAT`/`NGHITHU7`), 60 tháng theo ca; mặc định tạo mới: theo ca, nghỉ T7 có, nghỉ CN không | ✓ | `DNHANVIENAe.mapper_BeforeSave`/`mapper_AfterFillData` đúng như mô tả |
| 5.2 | Khi `TinhLuongSuDungMayChamCong=30`: ẩn nhóm lương theo công, bắt buộc chọn ca `DCALAMVIECID`, hiện `LUONGTHEOCA` và mã `CODE`; ngược lại ẩn các trường này | ✓ | `DNHANVIENAe.mapper_OnLoad` đúng từng dòng. **Mới:** cờ này chỉ được đọc ở đúng 2 chỗ (`DNHANVIENAe`, `DCALAMVIECAe`) + định nghĩa ở `SCONFIG` — không chỗ nào dùng nó để chọn form lương (xem §2.9) |
| 5.3 | Bảng lương (`TBANGLUONGAe`): tháng 1–12, năm 0–2999, tên tự sinh "Tháng m/yyyy"; mặc định tháng/năm hiện tại | ✓ | `mapper_OnLoad` (min/max) + `numTHANG_OnEditValueChanged` + `mapper_AfterFillData` (tháng/năm = `DbDate`) |
| 5.4 | Thưởng/phạt (`TTHUONGPHATAe`): ngày phải nằm trong tháng của bảng lương khi gọi từ chi tiết lương (`SetGioiHanNgay`) | ✓ | `mapper_BeforeSave`: ngoài tháng/năm giới hạn → cảnh báo + `e.Cancel = true` |
| 5.5 | Chỉ tiêu doanh thu (`DCHITIEUDOANHTHUAe`): chỉ tiêu theo năm (tên = năm, số nguyên; mặc định năm lớn nhất đã có hoặc năm hiện tại) và theo tỉnh thành cha (`DTINHTHANH`, mỗi tỉnh cha một dòng chi tiết); chưa thấy báo cáo nào đọc bảng này | ✓ | `mapper_AfterFillData`: `NAME = Max(Now.Year, MAX(NAM))`; tự thêm dòng chi tiết cho tỉnh cha; `mapper_BeforeSave` parse năm. **Đã kiểm chứng thêm:** 0 hit `DCHITIEUDOANHTHU` trong toàn bộ CODE + SQL báo cáo; DB DEMO có đúng 1 dòng (năm 2015, 100.000.000 × 12 tháng) |

### 1.6 Đính chính tài liệu Claude

- **C1 — Mâu thuẫn nội bộ trong đầu file Claude:** phần Nguồn ghi "Chưa đọc: `Tính lương bằng máy` (chấm công từ máy), `Lấy dữ liệu từ máy`, bảng mô tả `DNHANVIEN`, `DCALAMVIEC`, `TBANGLUONG`, `TTHUONGPHAT`" — nhưng §4 mô tả chi tiết `TinhLuongBangMay`/`LayDuLieuTuMay` và §5 mô tả chi tiết 4 bảng trên, nội dung đều khớp code đã kiểm chứng (§1.4–§1.5). Nhiều khả năng dòng "Chưa đọc" là ghi chú cũ chưa cập nhật. Nội dung §4–§5 vẫn dùng được; chỉ cần sửa dòng mở đầu khi đưa vào spec.
- **C2 — §1 thiếu hệ quả xóa bảng lương form tay:** Claude viết "Xóa bảng lương thì xóa chi tiết chấm công" mà không nói `TBANGLUONGTONGHOP` bị bỏ lại thành mồ côi ở form tay (chỉ form máy mới xóa cả 3 bảng). Khi viết spec phải ghi rõ 2 hành vi khác nhau này, không gộp chung.

## 2. Công thức + luồng đào sâu

### 2.1 Lương tay đầy đủ — `TinhLuong` (form `a7e47b60`, CODE 33.022 ký tự, TSCODE_LEN = 0: không có bản cảm ứng)

**Khung thời gian:** `startDate = 01/THANG/NAM`, `stopDate` = cuối tháng; `totalDays` = số ngày trong tháng. Lưới chấm công là `DataGridView` tạo động trong `Panel2` (không phải grid metadata): 1 dòng cho mỗi (NV × ca), cột ngày `ddMMyy` rộng 27px, header "ngày\nT2…CN", 6 cột tiền merge theo chiều dọc NV.

**SQL nạp NV (UNION ALL, `ORDER BY NHANVIEN`):**
- Nhánh 1: NV đã có `TBANGLUONGTONGHOP` của bảng lương này (INNER JOIN `DNHANVIEN`).
- Nhánh 2: NV `DNHANVIEN.STATUS = 30 AND COALESCE(ITEMTYPE,0) = 0` chưa có trong tổng hợp (để chấm công cho cả NV mới; `TBANGLUONGTONGHOPID = ''`, tiền = 0).
- Cột tính: `LUONGCOBAN = CASE WHEN CACHTINHLUONG = 30 THEN LUONGCA ELSE LUONGTHANG END`; 3 subquery `PHAT`/`THUONG`/`TAMUNG` theo tháng (xem §2.5).
- Ca: `SELECT ID, NAME, TILELUONG FROM DCALAMVIEC WHERE STATUS = 30 AND (ITEMTYPE IS NULL OR ITEMTYPE = 0)`.
- Chi tiết chấm công nạp vào `dic` với key `DNHANVIENID + DCALAMVIECID + NGAY("ddMMyy")` — **năm chỉ lấy 2 chữ số**; trùng key (2 dòng cùng NV/ca/ngày) bị nuốt lặng (`try { dic.Add(...) } catch { }`, dòng đầu thắng; khi lưu chỉ update dòng đầu — B9).

**`UpdateThucNhan(RowIndex)` — công thức đầy đủ, từng bước:**
```
soCaDiLam   = Σ qua từng ngày, từng ca: ô = 1 (Đi làm) hoặc 2 (Nghỉ có phép) → += TILELUONG/100
soNgayDiLam = số ngày có ít nhất 1 ca = 1 hoặc 2
// ô = 3 (Nghỉ không phép): không cộng gì; dòng "//phat += luongCa;" đã bị comment
// TILELUONG = 0 được quy về 100 khi nạp (MixTable); TILELUONG âm không được chặn
CACHTINHLUONG = 30 → TONGLUONG = LUONGCA × soCaDiLam
                = 60 → TONGLUONG = soCaDiLam × LUONGTHANG / totalDays
                = 0 (mọi giá trị khác cũng rơi vào nhánh else) → TONGLUONG = soNgayDiLam × LUONGTHANG / soNgayTrongThang
    soNgayTrongThang: CN nghỉ (30) + T7 nghỉ (30) → tongNgayTruT7CN
                      chỉ CN nghỉ → tongNgayTruCN; chỉ T7 nghỉ → tongNgayTruT7; không nghỉ → totalDays
THUCNHAN = TONGLUONG + THUONG − PHAT − TAMUNG   (TAMUNG lấy từ dòng tổng hợp đã nạp, không tính lại từ DB)
```
- Không làm tròn ở bước nào trong `UpdateThucNhan` (kết quả `decimal` giữ nguyên; làm tròn chỉ xảy ra khi ghi `BIGINT` ở tầng No1Lib — không thấy trong export).
- `THUONG`/`PHAT`/`TAMUNG` hiển thị là **snapshot lúc nạp**; muốn cập nhật phải bấm "Tải lại" (`btnRefresh`) hoặc sửa qua popup Lương chi tiết (sau khi đóng popup, form cha gọi `RefreshData(DNHANVIENID)`).
- **B10 — `RefreshData` đọc từ `DNHANVIEN`, không phải `TONGHOP`:** sau khi bấm Tải lại, `LUONGCA`/`LUONGTHANG`/`CACHTINHLUONG`/`NGHICHUNHAT`/`NGHITHU7` bị đè bằng giá trị hiện tại của danh mục NV — mọi chỉnh sửa lương đã lưu ở `TONGHOP` (nhưng chưa sửa ở `DNHANVIEN`) đều mất. Chấm công (`TBANGLUONGCHITIET`) không bị đụng tới.

**Lưu — `btnCapNhat_Click` → `workerSave_DoWork` (không có nút "Tính lương" riêng):**
- Mỗi NV: upsert `TBANGLUONGTONGHOP` theo `TBANGLUONGTONGHOPID` (rỗng → tạo mới): gán `DNHANVIENID, LUONGCA, THUCNHAN, TONGLUONG, TAMUNG, LUONGTHANG, CACHTINHLUONG, PHAT, THUONG, TBANGLUONGID`. **Không gán `SOGIOLAM`/`SOGIOTANGCA`/`LUONGTHEOCA`.**
- Mỗi (ca × ngày): `1→1, 2→2, 3→3, 0→0`; có trong `dic` → update `TRANGTHAI`; chưa có và `val != 0` → insert `TBANGLUONGCHITIET`; ô = 0 không sinh dòng.
- **B8 — quirk trong nhánh insert:** `rNhanVien["TBANGLUONGTONGHOPID"] = upRow.ID;` — gán nhầm ID của dòng *chi tiết* vào cột *tổng hợp* trong bộ nhớ (TONGHOP đã lưu trước đó nên DB không sai; chỉ sai dữ liệu hiển thị trong phiên làm việc).
- **Không có chốt/khóa:** grep `KHOA/CHOT/DACHI/THANHTOAN` trong code lương = 0 kết quả liên quan bảng lương; `TBANGLUONG.STATUS` tồn tại nhưng code lương không đọc/ghi. Bảng lương đã tính vẫn sửa/xóa/chi lại tự do.

### 2.2 "Công thức lương mẫu" (ExcelSalaryEngine) — kết luận dứt khoát: KHÔNG TỒN TẠI

- Grep toàn bộ export đã giải mã: `ExcelSalaryEngine|CongThuc|Formula|Evaluate|SalaryEngine` = **0 kết quả** trên `sform_code/` (112 file), `sform_aelayout/`, `sform_designcode/`, `sform_tscode/`, `sconfig.txt`, `stablesdesc.txt`.
- Các hit "Excel" duy nhất: `Chọn_cột_excel` (xuất Excel), `Import_định_lượng`/`Lựa_chọn_import_định_lượng` (nhập Excel), và vài chỗ xuất file — không liên quan lương.
- Không có cột công thức nào trong DB (771 cột đã rà: không cột nào tên `CONGTHUC`/`FORMULA`).
- **Kết luận:** mọi công thức lương đều hard-code C# (`TinhLuong.UpdateThucNhan`, `TinhLuongBangMay.TinhLuong`/`CapNhatGio`). Bản clone không cần engine công thức; chỉ cần cài đúng 2 hàm trên + 3 subquery tiền. (Từ "công thức lương mẫu" trong câu hỏi vòng 1 có thể xuất phát từ tên gợi ý của người khác — không có thực trong code.)

### 2.3 Lương máy đầy đủ — `TinhLuongBangMay` (form `0046913e`, CODE 18.017 ký tự, TSCODE_LEN = 0)

**`LoadDetail()` — SQL nạp (khác hẳn form tay):**
```sql
SELECT TBANGLUONGTONGHOP.ID, DNHANVIEN.LUONGCA, DNHANVIEN.ID AS DNHANVIENID, DNHANVIEN.NAME AS NHANVIEN,
  DCALAMVIEC.NAME AS CA, THUCNHAN, TONGLUONG,
  (SELECT COALESCE(SUM(PHAT),0) FROM TTHUONGPHAT WHERE ... NGAY BETWEEN @FromDate AND @ToDate) AS PHAT,
  (SELECT COALESCE(SUM(THUONG),0) FROM TTHUONGPHAT WHERE ... ) AS THUONG,
  (SELECT CASE WHEN SUM(THU-CHI) IS NULL THEN 0 ELSE SUM(THU-CHI) END FROM TTHUCHI
     WHERE DNHANVIENID = DNHANVIEN.ID AND NGAY BETWEEN @FromDate AND @ToDate AND LATAMUNG = 30) AS TAMUNG,
  (SELECT COALESCE(SUM(SOGIO),0) FROM TVAORA WHERE DNHANVIENID = DNHANVIEN.ID AND TBANGLUONGID = '<SelID>') AS SOGIOLAM,
  (SELECT COALESCE(SUM(SOGIOTANGCA),0) FROM TVAORA WHERE DNHANVIENID = DNHANVIEN.ID AND TBANGLUONGID = '<SelID>') AS SOGIOTANGCA
FROM DNHANVIEN LEFT JOIN DCALAMVIEC ON DNHANVIEN.DCALAMVIECID = DCALAMVIEC.ID
LEFT JOIN TBANGLUONGTONGHOP ON DNHANVIEN.ID = TBANGLUONGTONGHOP.DNHANVIENID
WHERE DNHANVIEN.STATUS = 30 AND TBANGLUONGID = '<SelID>' ORDER BY DNHANVIEN.NAME
```
- **B2 — NV mới trong tháng vô hình:** `WHERE TBANGLUONGID = SelID` loại mọi NV chưa có dòng `TONGHOP` của tháng này (LEFT JOIN cho `NULL` → bị lọc). Nhánh tạo mới chỉ chạy khi `dt.Rows.Count == 0` (tạo cho **mọi** NV `STATUS = 30`, **không lọc `ITEMTYPE`** — khác form tay). NV vào làm giữa tháng (sau lần mở đầu tiên) không bao giờ xuất hiện ở form máy trong tháng đó.
- **B11 — `LUONGCA` lấy từ `DNHANVIEN`, không phải `TONGHOP`:** cột "Lương/giờ" trên lưới và công thức `TinhLuong` đều dùng `DNHANVIEN.LUONGCA` hiện tại; giá trị `TONGHOP.LUONGCA` đã lưu bị bỏ qua.
- Sau khi nạp, `TinhLuong()` chạy trên từng dòng (chỉ bộ nhớ): `TONGLUONG = Math.Round((SOGIOLAM + SOGIOTANGCA × 1.5) × LUONGCA, 0)`; `THUCNHAN = TONGLUONG + THUONG − TAMUNG − PHAT`. `LUONGCA` ở đây là **lương/giờ** (header cột lưới ghi rõ "Lương/giờ"); `LUONGTHANG`/`CACHTINHLUONG`/`TILELUONG` không dùng.
- Lưới `grTongHop` (AELAYOUT đầy đủ): 9 cột `NHANVIEN, SOGIOLAM ("Số giờ", N2), SOGIOTANGCA ("Số giờ tăng ca", N2), LUONGCA ("Lương/giờ", N2), THUONG, PHAT, TAMUNG, TONGLUONG, THUCNHAN` — tất cả `ReadOnly = True`, **không có cột `LUONGTHEOCA` hay `CACHTINHLUONG`**. Lưới `grChiTiet`: `NGAY, GIOVAO, GIORA, SOGIO (N2, ReadOnly), SOGIOTANGCA (N2, ReadOnly), NOTE`.

**`btnRefresh_Click` — import + tính lại từ máy (B6):**
1. Mở `LayDuLieuTuMay.SetData(THANG, NAM)`; chỉ tính tiếp khi `DialogResult.OK`.
2. SQL: `SELECT DNHANVIENID, NGAY, GIOVAO, GIORA, GIOBATDAU, GIOKETTHUC FROM TVAORA INNER JOIN DNHANVIEN … LEFT JOIN DCALAMVIEC … WHERE NGAY BETWEEN đầu–cuối tháng` (lọc theo **ngày**, không theo `TBANGLUONGID`).
3. Mỗi NV: `soGioLam = Σ Round((GIORA − GIOVAO).TotalHours, 2)` — chỉ ngày có đủ cả hai giờ; chỉ lấy giờ:phút, bỏ giây (chuẩn hóa về `2000-01-01 HH:mm`). **`soGioTangCa` luôn = 0** (khai báo rồi không gán). **Đoạn kẹp giờ vào/ra theo ca (`GIOBATDAU`/`GIOKETTHUC`) đã bị comment out** (`//if (gioVao < gioBatDau) gioVao = gioBatDau;` …) — làm bao nhiêu tính bấy nhiêu, kể cả đi sớm/về muộn.
4. Gán `SOGIOLAM`/`SOGIOTANGCA` (chỉ bộ nhớ) → `TinhLuong()` → refresh lưới. **Không ghi `TVAORA.SOGIO`** — giờ đã tính chỉ nằm ở `TONGHOP` sau khi bấm Cập nhật.

**`grChiTiet_CellValueChanged` → `CapNhatGio` (sửa tay giờ vào/ra — chỗ DUY NHẤT tách tăng ca):**
- Chỉ xử lý cột Ghi chú / Giờ vào / Giờ ra (giờ có `Year < 2000` thì set null).
- Lấy ca của NV (`DNHANVIEN.DCALAMVIECID → DCALAMVIEC.TUGIO/DENGIO`); `soGioPhaiLam = Round((DENGIO−TUGIO).TotalHours, 2)` (ca qua đêm: `tuGio > denGio → tuGio.AddDays(-1)`; vào > ra → `gioVao.AddDays(-1)`); `tongGioDaLam = Round((GIORA−GIOVAO).TotalHours, 2)`; vượt → `SOGIO = soGioPhaiLam, SOGIOTANGCA = phần vượt`, không vượt → `SOGIO = tongGioDaLam, SOGIOTANGCA = 0`.
- **B3:** NV chưa gán ca (`DCALAMVIECID` rỗng) → `SOGIO = 0, SOGIOTANGCA = 0` dù đã có đủ `GIOVAO`/`GIORA`. Thiếu 1 trong 2 giờ → cũng 0.
- Sau đó `CapNhatTongGio()` cộng `SUM(SOGIO)`/`SUM(SOGIOTANGCA)` từ lưới chi tiết lên dòng tổng hợp và tính lại lương.

**`grTongHop_SelectionChanged` — nạp ngày trống:** load `TVAORA` theo NV + khoảng tháng; tự tạo dòng thiếu cho mọi ngày trong tháng (`SOGIO=0, SOGIOTANGCA=0, TBANGLUONGID=SelID`). Đây là chỗ duy nhất trong export gán `TBANGLUONGID` cho dòng `TVAORA` — và chỉ cho ngày **chưa có dòng**.

**`btnCapNhat_Click` (máy):** lưu `TBANGLUONGTONGHOP` bằng `DbUtils.UpdateByFields` với các field `DNHANVIENID, LUONGCA, PHAT, TAMUNG, THUONG, SOGIOLAM, SOGIOTANGCA, TONGLUONG, THUCNHAN, TBANGLUONGID = SelID`. **Không lưu `LUONGTHANG`/`CACHTINHLUONG`/`LUONGTHEOCA`.**

**`LayDuLieuTuMay` — import máy chấm công (đầy đủ):**
- `SetData(month, year)`: `txtIP = SystemConfig.IpMayChamCong`, trống → `192.168.1.201`.
- `btnThucHien_Click`: lưu IP vào config → chạy `Application.StartupPath + "\zkemkeeper\zkemkeeper.exe"` với tham số `"<IP> <guid> AttLog"`, chờ tối đa 2 phút; exe không tồn tại → báo "File 'zkemkeeper.exe' không tồn tại trong thư mục chương trình".
- Output là file XML tạm `zkemkeeper\temp\<guid>` (không đuôi); nội dung bắt đầu bằng `"MESSAGE: "` → hiện cảnh báo đó; ngược lại `DataTable.ReadXml`, rồi `File.Delete` file tạm.
- Ghép dữ liệu: với mỗi ngày trong tháng × mỗi NV (`SELECT ID, CODE, NAME FROM DNHANVIEN` — **B12: không lọc `STATUS`** — cả NV đã nghỉ cũng vào lưới import), lọc `dtGet.Select("DATESTR='ddMMyyyy' AND USERID='CODE'", "TIME")`; có ≥ 1 lần quẹt → `GIOVAO` = lần sớm nhất, `GIORA` = lần muộn nhất (chỉ khi > 1 lần); 1 lần duy nhất → chỉ có `GIOVAO`, `GIORA` null (ngày đó tính 0 giờ). `SOLAN` = số lần quẹt (chỉ hiển thị, không lưu DB). Lưới import có cây lọc theo NV (`tvMain_Load` → `bs.Filter`).
- `btnSave_Click`: `grMain.Rows.Count == 0` → cảnh báo "Không có dữ liệu nào để cập nhật". Nếu `chkXoaDuLieuCu` tick → `DELETE FROM TVAORA WHERE NGAY BETWEEN đầu–cuối tháng` (xóa **toàn bộ** tháng, mọi NV); nếu không, từng dòng mới: tìm dòng cũ cùng key `NGAY(ddMMyyyy)+DNHANVIENID` → `DELETE` dòng cũ (chỉ `rows[0]` nếu trùng nhiều) rồi insert mới. Insert `TVAORARow`: chỉ `NGAY, DNHANVIENID, GIOVAO/GIORA (2000-01-01 HH:mm)` — **không set `TBANGLUONGID, GIOBATDAU, GIOKETTHUC, SOGIO, SOGIOTANGCA`**. Nếu `chkXoaSauKhiThucHien` tick → chạy lại exe với `"<IP> noFile ClearAttLog"`. Trả `DialogResult.OK`.

### 2.4 `LUONGTHEOCA` — kết luận dứt khoát: cột chết

- Toàn bộ export (CODE + AELAYOUT + DESIGNCODE + TSCODE + SQL báo cáo + `stablesdesc`/`scolumn`): đúng **1** lần xuất hiện trong logic — `TinhLuongBangMay.LoadDetail`: `thRow.LUONGTHEOCA = rNhanVienRow.LUONGTHEOCA;` (copy khi tạo dòng tổng hợp mới).
- Không dùng trong công thức nào (cả 2 form lương), không có cột nào trên lưới `grTongHop`/`grChiTiet`, không có trong SQL báo cáo nào (0 báo cáo lương trong `SREPORT`), không có control nào đọc nó ngoài `DNHANVIENAe.mapper_OnLoad` (chỉ ẩn/hiện ô nhập `numLUONGTHEOCA` theo cờ máy chấm công).
- Schema: `BIGINT` ở cả `DNHANVIEN` và `TBANGLUONGTONGHOP`; nhập được ở form NV khi bật máy chấm công.
- **Kết luận:** giữ cột trong schema cho tương thích, không cần logic nào cho nó ở bản clone. Khả năng duy nhất còn lại (không kiểm chứng được): dùng trong mẫu in `.frx` chưa xuất hoặc bản build khác.

### 2.5 Tạm ứng / chi lương — đào sâu

**Hai công thức `TAMUNG` (B4 — mâu thuẫn đã xác nhận, cần Chiến chốt):**
| | Form tay (`TinhLuong`) | Form máy (`TinhLuongBangMay`) |
|---|---|---|
| SQL | `(SELECT CASE WHEN SUM(CHI) IS NULL THEN 0 ELSE SUM(CHI) END FROM TTHUCHI WHERE DNHANVIENID = … AND NGAY BETWEEN … AND LATAMUNG = 30)` | `(SELECT CASE WHEN SUM(THU − CHI) IS NULL THEN 0 ELSE SUM(THU − CHI) END FROM TTHUCHI WHERE DNHANVIENID = … AND NGAY BETWEEN … AND LATAMUNG = 30)` |
| Ý nghĩa | Chỉ cộng tiền chi (tạm ứng đã chi) | Trừ cả tiền thu mang cờ `LATAMUNG = 30` |
| Khi nào lệch | Phiếu **thu** (`LOAI=0`, `THU > 0`) có `LATAMUNG = 30` tồn tại trong tháng | — |

- Phiếu thu mang `LATAMUNG = 30` chỉ tạo được thủ công qua Danh mục phiếu thu/chi (set cờ tay) — các luồng UI chuẩn không tạo ra nó (`TamUngLuong` tạo phiếu **chi** `loai=1`; `SetTamUng` của đơn hàng không set `LATAMUNG`). Nên trong vận hành bình thường 2 công thức cho cùng kết quả; lệch chỉ xảy ra khi có dữ liệu lạ. **Đề xuất cho Chiến: giữ công thức tay `SUM(CHI)` cho cả hai form** (tạm ứng lương bản chất là chi), và coi `SUM(THU−CHI)` ở form máy là bug — nhưng đây là quyết định của Chiến, không tự sửa.

**`TTHUCHI1Ae.SetLuong` (phiếu chi lương) — đầy đủ:**
- `txtDIENGIAI = "Chi tiền lương tháng {THANG} năm {NAM}"`; `numCHI.Value = THUCNHAN` (**không disable** — sửa được); `cboLOAIDOITUONG = 1` (NV) + disable; `TENDOITUONG` = tên NV; `TBANGLUONGID` = bảng lương; lý do chi = `SELECT FIRST 1 DLYDOTHUCHIID FROM TTHUCHI WHERE TBANGLUONGID IS NOT NULL ORDER BY TIMECREATED DESC` (lý do của phiếu chi lương gần nhất).
- **Không set `LATAMUNG`** — phiếu chi lương là phiếu chi thường, chỉ phân biệt bằng `TBANGLUONGID`. `chkCHUYENKHOAN` vẫn hiện (được chuyển khoản) — khác phiếu tạm ứng (ẩn checkbox chuyển khoản khi `LATAMUNG = 30`).
- `mapper_BeforeSave` (`TTHUCHI1Ae`): chỉ 2 check — (1) nếu đã `SetGioiHanNgay` thì `NGAY` phải trong tháng đó; (2) nếu chuyển khoản thì bắt buộc chọn tài khoản ngân hàng. **Không có check `CHI ≤ THUCNHAN`, không có trần tiền, không có check trùng.**
- `mapper_AfterFillData`: phiếu mới → `LOAI = 1`, `DCUAHANGID = Shared.DCUAHANGID`; `LATAMUNG = 30` → khóa `LOAIDOITUONG` = NV + ẩn chuyển khoản; không có quyền `ThayDoiNgayTrongPhieuThuChi` → khóa `dtNGAY`. Không phải admin → `DCUAHANGID` chỉ được chọn trong cửa hàng của user (`TNGUOIDUNGTHEOCUAHANG`).

**Chi một phần (Q9 — cơ chế đầy đủ):** `ChiLuong` chỉ mở phiếu theo `TTHUCHI.ID` đã join được; `ID` rỗng → "Tạo phiếu…", có `ID` → "Xem phiếu…" (mở lại phiếu cũ để sửa). Không có cột còn thiếu/còn lại, không có đối soát `CHI` vs `THUCNHAN`, không có luồng "chi nốt". Muốn chi thêm chỉ có 2 cách thủ công: (a) sửa `CHI` trên phiếu cũ cho đủ; (b) xóa phiếu cũ (qua Danh mục phiếu chi) rồi tạo lại. **Hệ quả:** nếu `CHI < THUCNHAN` thì tháng đó hệ thống vẫn coi là "đã chi" (vì chỉ kiểm tra `ID` rỗng) — tiền thiếu không được nhắc ở đâu.

**`TTHUCHI0Ae.SetTamUng` (tạm ứng đơn hàng) — đầy đủ:**
- Chỉ chạy khi phiếu mới (`mapper.ID` rỗng): `TDONHANGID` = bill; `LOAIDOITUONG = 0` + disable; `DIENGIAI = "Tạm ứng cho " + GetTenPhongBan(true) + " " + tên bàn`; lý do = `SELECT FIRST 1 DLYDOTHUCHIID FROM TTHUCHI WHERE TDONHANGID IS NOT NULL ORDER BY TIMECREATED DESC`, fallback `SELECT ID FROM DLYDOTHUCHI WHERE NAME = 'Thu tạm ứng' AND LALYDOTHU = 30`; `KHONGTHAYDOICONGNO` tích sẵn + disable; focus vào `TENDOITUONG`.
- **Không gán:** số tiền `THU` (người dùng nhập, không có trần so với tổng đơn), `LATAMUNG`, `TDATHANGID` (dù form `TamUngDonHang` lọc theo cả 2 ID).
- Caller duy nhất: `HoaDonNhaHang.btnTamUng_Click` — chặn khi `DaThanhToan(true)`; truyền `mapper.ID` (bill), `TDONHANG.TDATHANGID`, `lblBan.Text` (tên bàn). Không có check quyền nào trên nút này trong code export (SFUNCTION "Thu tạm ứng khách hàng" `8cbfb281` tồn tại nhưng **không code nào tham chiếu** — B14).

### 2.6 `TBANGLUONGCHITIET` (phân ca) và `TBANGLUONG.CHITIET` (BLOB)

- `TBANGLUONGCHITIET`: đúng như vòng 1 — bảng phân ca theo ngày, **không phải** thành phần lương. Cột `TRANGTHAI` kiểu hiển thị **Number** (metadata `scolumn.txt`); giá trị chỉ 0–3 theo `enum DiLamMode` (định nghĩa duy nhất trong export, cuối file `Tính_lương.txt`). Không có mã trạng thái nào khác.
- `TBANGLUONG.CHITIET` (BLOB): **0 dòng** ở cả `FNB_DEMO.fdb` và `FNB_TEMPLATE.FDB`; **không code nào trong toàn bộ export đọc hoặc ghi cột này** (grep `\.CHITIET` chỉ trúng `ChiTietCombo` và tên bảng `TBANGLUONGCHITIET`); chỉ có 1 định nghĩa cột duy nhất trong `scolumn.txt` (`NotShow`). Không thể biết nội dung. Khả năng: cột chết, hoặc No1Lib tự ghi (không có trong export). Khi clone: giữ cột, không cần logic.

### 2.7 Chấm công — 2 menu, 1 form, và lỗ hổng quyền (B7)

- `showLuong = No1UserControl1.CallerMenuID.NAME == "Tính lương"`. Menu "Chấm công" → `showLuong = false`: ẩn 6 cột tiền (`LUONGCOBAN, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN`) và 2 nút (`btnChiLuong`, `btnRefresh`).
- **Nút `btnCapNhat` (lưu) vẫn hiện và vẫn lưu được cả tiền** — `workerSave_DoWork` ghi `TONGLUONG`/`THUCNHAN` không phân biệt chế độ. Các nút chấm công (`btnDiLam`/`btnNghiCoPhep`/…) vẫn hiện ở cả 2 chế độ (đúng — đó là chức năng chấm công).
- Text nút lưu trong AELAYOUT gốc là **"Cập nhât"** (thiếu dấu ậ) — typo của app gốc (B15, chỉ ghi chú, clone thì sửa thành "Cập nhật" hoặc giữ y hệt tùy Chiến).
- `kryptonPanel1` (panel chứa các nút) có `Enabled = False` trong layout, được bật lại ở `tmrLoadDetail_Tick` khi đã chọn 1 bảng lương.

### 2.8 Quyền — đã xác minh tới cùng bằng `SGROUPROLE` + `SFUNCTION` + cột DB thật

- **Cột trong `sfunction.txt` (Q15 — đã giải quyết dứt khoát):** `ID | NAME | GROUPNAME | NOTE | (trống) | SORTORDER`. Cột "View" = **`NOTE = 'View'`** (chức năng chỉ-xem: ở màn phân quyền chỉ có ô Khóa/Xem, không có Thêm/Sửa/Xóa — theo `he-thong.md` §4.1). Cột số cuối = **`SORTORDER`** (thứ tự hiển thị trong cây phân quyền — **không phải** mã enum: trùng giá trị ở 4 cặp (27, 70, 71, 78), NULL ở 27 dòng gồm toàn bộ "Danh mục…" + `Tính lương`, `Quản lý thưởng phạt`, `Báo giá`, `Đặt hàng`, `Thu chi`, 2 function e-invoice).
- **`SGROUPROLE.MODE` — cờ bit** (theo `he-thong.md` §4.1, khớp dữ liệu): Xem `0x10` (16), Sửa `0x20` (32), Thêm `0x40` (64), Xóa `0x80` (128); 0 = khóa; không có dòng = khóa. Giá trị đang dùng: 0, 16, 80 (= Xem+Thêm), 112 (= Xem+Sửa+Thêm), 240 (= tất cả).
- **Nhóm Nhân sự trong DEMO:**

| Nhóm | DM nhân viên | Thưởng phạt | Tính lương | Chấm công | Tạm ứng lương | DM ca làm việc |
|---|---|---|---|---|---|---|
| Quản lý | 240 | 240 | 240 | 240 | 240 | 240 |
| Kế toán | 240 | 240 | 240 | 240 | 240 | 240 |
| Phục vụ | 0 | 0 | 0 | 0 | 0 | 0 |
| Thu ngân | — | — | — | — | — | 0 |
| Thủ kho | — | — | — | — | — | — |

  (— = không có dòng = khóa. Tổng `SGROUPROLE` DEMO: 269 dòng; `he-thong.md` ghi 267 — lệch 2 dòng, xem lưu ý cuối §2.8.)
- **Kết luận quyền:** không nhóm nào trong DEMO tách "Chấm công" khỏi "Tính lương" (luôn cùng 240 hoặc cùng 0/khóa) — nên lỗ hổng B7 chỉ lộ ra nếu Chiến (hoặc quán) cấp lệch 2 quyền này. Trong code lương **không có `CanView`/`CanLogin` nào** cho Tính/Chi/Refresh — chỉ có: (1) quyền menu ở khung chính, (2) `showLuong` theo tên menu, (3) `CanRemove` (framework cấp) ở form máy để bật/tắt `pnlButton`/`tsbXoa`, (4) `DbUtils.CanView(Functions.ThayDoiNgayTrongPhieuThuChi)` khóa ô ngày trên phiếu thu/chi, (5) `DbConfig.IsAdmin` giới hạn cửa hàng. Thuộc tính `sFunctionID` trên các grid lương trong AELAYOUT đều **trống**.
- Lưu ý nhỏ: tổng dòng `SGROUPROLE` đếm được trong DEMO là **269**, trong khi `he-thong.md` §4.1 ghi 267 (Quản lý 81, Kế toán 71, Phục vụ 67, Thu ngân 36, Thủ kho 12 = 267). Lệch 2 dòng — có thể do DEMO khác bản POS TAP (DEMO thừa 3 function) hoặc đếm ở thời điểm khác. Không ảnh hưởng kết luận quyền nhóm Nhân sự (đã đối chiếu từng dòng).

### 2.9 Form `TinhLuongBangMay` không có caller — kết luận dứt khoát + giả thuyết mới

- Grep toàn bộ export đã giải mã (`sform_code/`, `sform_aelayout/`, `sform_designcode/`, `sform_tscode/`, `stablesdesc.txt`, `smenu.txt`, `sfunction.txt`, `sreport_sql/`): `TinhLuongBangMay` chỉ xuất hiện **2 lần, đều trong chính file của nó** (header `CLASSNAME` + khai báo class). **Không có dòng menu nào** trong `SMENU` trỏ tới nó (truy vấn DB trực tiếp: 0 dòng; không có menu tên "lương bằng máy"/"máy chấm công").
- Cờ `TinhLuongSuDungMayChamCong == 30` **không chọn form**: chỉ được đọc ở đúng 2 chỗ — `DNHANVIENAe.mapper_OnLoad` và `DCALAMVIECAe.mapper_OnLoad` (đổi giao diện form NV/ca) + định nghĩa ở `SCONFIG`. Không có `if` nào rẽ sang `TinhLuongBangMay`.
- **Giả thuyết mới (cần đối chiếu DATA.fdb thật):** DEMO có 112 form, POS TAP có 111 form — `TinhLuongBangMay` có thể chính là form thừa của DEMO, tức **POS TAP thật không có form này**. Nếu đúng thì toàn bộ luồng lương máy (import ZK, `TVAORA`, `SOGIOLAM`) chỉ tồn tại trong DEMO, và bản clone chỉ cần làm lương tay. Cách xác minh dứt khoát: `SELECT NAME FROM SFORM` trên DATA.fdb POS TAP thật.
- Cho tới khi xác minh được, bản clone nên làm lương tay trước; lương máy để sau và chỉ làm nếu Chiến xác nhận quán đang dùng máy chấm công (xem R5–R6).

### 2.10 Tồn quỹ — phiếu lương/tạm ứng trừ quỹ ngay khi lưu (Q7)

- Tồn quỹ là truy vấn trực tiếp (live query), không qua bước duyệt/chốt: `SELECT … FROM TTHUCHI … UNION ALL SELECT … FROM TDONHANG WHERE TIENTHANHTOAN <> 0` (câu SQL gốc nằm trong class `TonQuy` của No1Lib — không có trong export; cấu trúc suy ra từ `TonQuyHandler.tonQuy_CustomLoadData` chèn `WHERE` trước `" UNION ALL"` và sau `"(TDONHANG.TIENTHANHTOAN <>0)"`).
- **Không có bước duyệt/chốt nào:** trong toàn bộ code export không có cột trạng thái duyệt cho `TTHUCHI`; trigger duy nhất trong DB là `TDONHANG_TRIGGER` (trên `TDONHANG`, không phải `TTHUCHI`); DB không có FK/UNIQUE/CHECK nào (0 FOREIGN KEY, 0 UNIQUE, 0 CHECK). Phiếu chi lương/tạm ứng làm giảm tồn quỹ **ngay khi lưu**; xóa phiếu (grid `RealDelete = True`) thì quỹ tăng lại ngay.
- Lọc theo cây: Tiền mặt (`CHUYENKHOAN = 0`), Quẹt thẻ (phía phiếu thu/chi bị loại hẳn bằng `0 = 1` — chỉ tính `TDONHANG.THE`), Ngân hàng (`CHUYENKHOAN = 30`), từng tài khoản.

### 2.11 Báo cáo lương — không có báo cáo nào (Q17)

- Trong 128 báo cáo (`sreport_index.txt` + `sreport_sql/`): **không có báo cáo nào tên lương/nhân viên/chấm công**; grep `TBANGLUONG|TVAORA|DNHANVIEN` trong SQL báo cáo chỉ trúng 2 báo cáo bán hàng, trong đó `DNHANVIEN` là **người xuất hóa đơn** (`TDONHANG.DNHANVIENXUATID`): `BIỂU_ĐỒ_THEO_NHÂN_VIÊN_KINH_DOANH` (doanh số theo NV kinh doanh) và `CHI_TIẾT_BÁN_HÀNG_THEO_HÓA_ĐƠN` (cột NV = người xuất, thu ngân = `USERTHANHTOANID`).
- `LUONGTHEOCA`/`SOGIOLAM` không xuất hiện trong SQL báo cáo nào. In bảng lương (nếu có) chỉ nằm trong mẫu `.frx` chưa xuất — chưa xác minh được.

### 2.12 Cấu hình — giá trị thật trong DB (Q16, phần DEMO)

- Truy vấn trực tiếp `SCONFIG` trên cả 2 DB: `TinhLuongSuDungMayChamCong` = **NULL/NULL** (dòng tồn tại nhưng không có giá trị — chưa set) → theo quy ước engine (`ConvertTo.Long` trả -1 khi null) thì `== 30` là sai → **DEMO chạy chế độ lương tay**. `IpMayChamCong` = `''` (text rỗng, INTVALUE 0) → mặc định `192.168.1.201`.
- Giá trị trên DATA.fdb POS TAP thật vẫn chưa biết (xem R5).

## 3. Đáp án cho 22 câu hỏi vòng 1

Ký hiệu: **✅ trả lời được hoàn toàn** · **◐ trả lời một phần** · **❌ chưa trả lời được**.

| # | Câu hỏi (tóm tắt) | Kết quả | Đáp án + bằng chứng |
|---|---|---|---|
| 1 | Ai/cái gì mở `TinhLuongBangMay`? | ✅ | **Không có caller/menu nào trong toàn bộ export** (grep 2 hit, đều trong chính file nó; `SMENU` không có dòng nào cho form này). Cờ `TinhLuongSuDungMayChamCong` không chọn form (chỉ đọc ở 2 form danh mục NV/ca). **Giả thuyết mới:** đây có thể chính là form thừa của DEMO (DEMO 112 form vs POS TAP 111) — đối chiếu `SFORM` trên DATA.fdb thật (xem R6). Trong phạm vi export, câu hỏi đã được trả lời dứt khoát; phần còn lại nằm ngoài export. |
| 2 | `LUONGTHEOCA` dùng để làm gì? | ✅ | **Cột chết.** Chỉ 1 lần xuất hiện trong CODE: copy `DNHANVIEN → TBANGLUONGTONGHOP` khi tạo mới ở form máy. Không công thức, không form, không báo cáo nào dùng. |
| 3 | `TVAORA.GIOBATDAU`/`GIOKETTHUC` dùng để làm gì? | ✅ | **Cột chết.** Không chỗ nào ghi 2 cột này; chỉ đọc 1 lần ở `btnRefresh` form máy rồi bỏ — đoạn kẹp giờ đã bị comment out (`//if (gioVao < gioBatDau) …`). |
| 4 | `TAMUNG` tay (`SUM(CHI)`) vs máy (`SUM(THU−CHI)`) — đâu là đúng? | ◐ | **Mâu thuẫn được xác nhận + trích đúng cả 2 SQL** (xem §2.5). "Đâu là đúng" cần Chiến chốt (đề xuất: `SUM(CHI)` — tạm ứng bản chất là chi; máy `SUM(THU−CHI)` chỉ lệch khi có phiếu thu `LATAMUNG=30`, loại phiếu chỉ tạo được thủ công nên lệch thực tế hiếm). |
| 5 | Form máy: dòng `TVAORA` import có `TBANGLUONGID` null → `LoadDetail` hiện lương 0 cho tới khi Refresh? | ✅ | **Đúng 100%, đã phân tích đầy đủ cơ chế (B1):** `LayDuLieuTuMay.btnSave` không set `TBANGLUONGID`/`SOGIO`/`SOGIOTANGCA`/`GIOBATDAU`/`GIOKETTHUC`; `LoadDetail` tính `SOGIOLAM = SUM(SOGIO) WHERE TBANGLUONGID = SelID` → 0; `TinhLuong()` tính lại `TONGLUONG`/`THUCNHAN` = 0 trên bộ nhớ (hiển thị 0) cho tới khi bấm Refresh (tính lại từ `GIOVAO`/`GIORA` rồi Cập nhật mới lưu đúng). **Nguy hiểm:** bấm Cập nhật mà không Refresh sẽ lưu 0 đè lên giá trị đúng đã lưu trước đó. Chưa chạy app gốc để kiểm chứng hành vi thực tế. |
| 6 | Xóa bảng lương tay có để lại `TBANGLUONGTONGHOP` mồ côi không? | ✅ | **Có, đã xác nhận dứt khoát:** form tay `grMain_OnDeleted` chỉ `DELETE FROM TBANGLUONGCHITIET`; DB có **0 FOREIGN KEY** nên không có cascade nào. Form máy mới xóa cả 3 bảng. |
| 7 | Chi lương/tạm ứng trừ quỹ lúc nào? Có bước duyệt/chốt không? | ✅ | **Trừ ngay khi lưu phiếu.** Tồn quỹ là live query `TTHUCHI UNION ALL TDONHANG` (xem §2.10); không có cột trạng thái duyệt nào trên `TTHUCHI`; trigger duy nhất trong DB là `TDONHANG_TRIGGER` trên `TDONHANG`. |
| 8 | Có kiểm tra 1 NV chỉ được chi lương 1 phiếu/bảng lương không? | ✅ | **Không, đã xác nhận ở cả 3 lớp:** code `ChiLuong` không check (chỉ mở phiếu theo `ID` join được, `ID` rỗng → tạo mới); `TTHUCHI1Ae.mapper_BeforeSave` không check; **DB chỉ có PK trên `ID`**, không unique constraint/index nào trên `(TBANGLUONGID, DNHANVIENID)`. Tạo thủ công qua Danh mục phiếu chi thì được 2 phiếu; lưới Chi lương sẽ hiện 2 dòng (join không `DISTINCT`). UI chuẩn thì vẫn 1 phiếu (vì sau khi tạo, nút đổi thành "Xem phiếu"). |
| 9 | Lương tháng chi 1 phần rồi chi nốt phần còn lại thế nào? | ◐ | **Cơ chế đầy đủ đã rõ:** không có cột còn thiếu, `CHI` sửa được trên phiếu, sau khi lưu nút đổi thành "Xem phiếu" → **không có luồng chi nốt**. Muốn chi thêm chỉ có 2 cách thủ công: sửa `CHI` trên phiếu cũ cho đủ, hoặc xóa phiếu cũ rồi tạo lại. Thực tế quán làm thế nào vẫn cần app gốc. |
| 10 | Có check trùng tháng/năm khi tạo bảng lương không? | ✅ | **Không, đã xác nhận ở cả 2 lớp:** `TBANGLUONGAe` không có `mapper_BeforeSave` (chỉ `numTHANG_OnEditValueChanged` tự sinh `NAME`); DB không có unique constraint nào trên `(THANG, NAM)` (0 UNIQUE trong toàn DB). Chỉ còn khả năng: framework check trùng qua `AllowDuplicate = False` trên cột `NAME` của `grMain` — không xác minh được cơ chế này từ export. |
| 11 | `SOGIO`/`SOGIOTANGCA` decimal → `BIGINT` quy đổi ra sao? | ◐ | **Đã xác nhận:** DB `BIGINT` (`TVAORA`, `TBANGLUONGTONGHOP`); lưới hiển thị `N2`; code tính `Round(..., 2)` (giờ, 2 số thập phân) và `Math.Round(..., 0)` (tiền). **Không có cơ chế quy đổi nào trong export** — nằm trong No1Lib row mapping (không có trong export). Cần xem giá trị thật sau khi chạy máy chấm công (xem R3). |
| 12 | Nội dung `TBANGLUONG.CHITIET` BLOB là gì? | ◐ | **Đã xác nhận tới cùng bằng code + DB:** 0 dòng ở cả DEMO lẫn TEMPLATE; **không code nào trong toàn bộ export đọc/ghi cột này**; chỉ 1 định nghĩa cột duy nhất trong `scolumn.txt`. Nội dung cụ thể vẫn không biết — chỉ còn khả năng No1Lib tự ghi hoặc cột chết. |
| 13 | Mapping lưới → DB của 5 màn chi tiết (Lương chi tiết, Tạm ứng lương, Tạm ứng đơn hàng, Tính lương tay, Tính lương bằng máy) có đúng 100%? | ✅ | **Đúng 100%, đã xác minh đủ 5 màn** (xem §2.1, §2.3, §2.5 + `LuongChiTiet` §3-Q13 chi tiết dưới đây). |
| 14 | Quyền nhóm Nhân sự: user chỉ có quyền Chấm công vẫn lưu được tiền qua nút Cập nhật? | ✅ | **Đã xác minh đầy đủ (xem §2.7–§2.8):** code `TinhLuong` không kiểm tra quyền nào khi lưu; `btnCapNhat` hiện ở cả 2 chế độ; `showLuong` chỉ ẩn cột/nút hiển thị. Trong DEMO không nhóm nào tách 2 quyền (Quản lý/Kế toán = 240 cả 6; Phục vụ = 0 cả 6; Thu ngân/Thủ kho = khóa). Lỗ hổng chỉ lộ ra khi cấp lệch 2 quyền. |
| 15 | `sfunction.txt` cột 4 (View) và cột 6 (số cuối) là gì? | ✅ | **Đã xác minh bằng cột DB thật:** cột 4 = `NOTE` (`'View'` = chức năng chỉ-xem); cột 6 = `SORTORDER` (thứ tự hiển thị cây phân quyền; trùng ở 27/70/71/78; NULL ở 27 dòng). |
| 16 | Giá trị `TinhLuongSuDungMayChamCong` + `IpMayChamCong` trong `SCONFIG`? | ◐ | **Phần DEMO đã trả lời:** `TinhLuongSuDungMayChamCong` = NULL (chưa set → chế độ tay), `IpMayChamCong` = `''` → mặc định `192.168.1.201`. **Giá trị trên DATA.fdb POS TAP thật vẫn chưa biết** (xem R5). |
| 17 | Có báo cáo lương nào trong `SREPORT` không? | ✅ | **Không có.** 0 báo cáo tên lương/nhân viên/chấm công trong 128 báo cáo; `DNHANVIEN` chỉ xuất hiện trong 2 báo cáo bán hàng với vai trò người xuất hóa đơn. `LUONGTHEOCA`/`SOGIOLAM` = 0 hit trong SQL báo cáo. |
| 18 | Bug `GioTinhLuong` (getter trả về `dtBATDAU`) có cần giữ lại không? | ❌ | **Giữ nguyên, ngoài phạm vi** — thuộc tính giờ đã bỏ theo quyết định loại hình COFFEE + POS. Chỉ xác minh nếu Chiến muốn giữ hành vi này. |
| 19 | `TTHUCHI0Ae.SetTamUng` gán gì? Caller là ai? | ✅ | **Đầy đủ (xem §2.5):** set `TDONHANGID, LOAIDOITUONG=0, DIENGIAI, KHONGTHAYDOICONGNO=30` + lý do (phiếu gần nhất có `TDONHANGID`, fallback 'Thu tạm ứng'); **không gán `THU`, `LATAMUNG`, `TDATHANGID`**; caller duy nhất `HoaDonNhaHang.btnTamUng_Click` (chặn khi đã thanh toán). |
| 20 | Có trần tiền/số lần tạm ứng không? Có check ngày? | ✅ | **Không có trần tiền/số lần trong code.** `mapper_BeforeSave` của `TTHUCHI1Ae` chỉ check (1) ngày trong tháng khi có `SetGioiHanNgay` (gọi từ Lương chi tiết), (2) tài khoản ngân hàng khi chuyển khoản. Tạo từ màn Tạm ứng lương độc lập thì không có cả giới hạn ngày. |
| 21 | `CACHTINHLUONG`/`TRANGTHAI` là kiểu gì? Ý nghĩa tick? | ✅ | **Đã xác minh metadata:** `TBANGLUONGTONGHOP.CACHTINHLUONG` = **CheckBox** trong metadata, `DNHANVIEN.CACHTINHLUONG` = **NotShow**, `TBANGLUONGCHITIET.TRANGTHAI` = **Number** (0–3 theo `DiLamMode`). Nhưng **không form nào hiển thị cột `CACHTINHLUONG` của TONGHOP** nên ý nghĩa tick không quan sát được — giá trị lưu vẫn 0/30/60. |
| 22 | "Thay đổi giờ vào" (`ThayDoiGioVao`): giao diện có những trường nào? | ✅ | **Trả lời được từ AELAYOUT đầy đủ:** `lblTime` = **"Mở hóa đơn lúc:"** (code cộng thêm giờ tạo bill `HH:mm`); `lblVeTruoc` = **"Số phút điều chỉnh về trước:"**, `lblVeSau` = **"Số phút điều chỉnh về sau:"**; `numSoPhut`/`numSoPhutVeSau` Maximum = **180**, DecimalPlaces = **2**. Form này thuộc tính giờ nên ngoài phạm vi clone (chỉ ghi tham khảo). |

**Chi tiết Q13 — mapping lưới → DB (đã xác minh 100%):**
- **Lương chi tiết (`LuongChiTiet`, form `19eec0a3`, FORMTYPE 6):** `SetData(TBANGLUONGRow)` → tháng/năm + `lblThang.Text = row.NAME`, `lblNhanVien.Text = "Nhân viên: " + NAME`. Filter tạm ứng: `DNHANVIENID = … AND EXTRACT(MONTH FROM NGAY) = … AND EXTRACT(YEAR FROM NGAY) = … AND LATAMUNG = 30`; filter thưởng/phạt: `THUONG > 0` / `PHAT > 0` (theo `LuongChiTietMode { TamUng = 0, Thuong = 1, Phat = 2 }`). Grid tạm ứng → `TTHUCHI` (cột `NAME/NGAY/CHI`, `DNHANVIENID`, `LATAMUNG = 30`); grid thưởng/phạt → `TTHUONGPHAT` (cột `NAME/NGAY/THUONG/PHAT`, `DLYDOTHUONGPHATID` → `DLYDOTHUONGPHAT`, `DNHANVIENID`); `grTamUng_CustomAeForm: loai = 1`; `AfterCreatedEditForm` gán `DNHANVIENID` + `LATAMUNG = 30` + `SetGioiHanNgay(tháng, năm)`; `Changed` set true khi sửa.
- **Tạm ứng lương (`TamUngLuong`, form `b0e7f8d4`, FORMTYPE 5):** chỉ 3 handler — `CustomLoadData` thêm `AND LATAMUNG = 30`, `AfterCreatedEditForm` set `LATAMUNG = 30`, `CustomAeForm` set `loai = 1`; grid có `dtFilter` (FilterDateRange).
- **Tạm ứng đơn hàng (`TamUngDonHang`, FORMTYPE 6):** grid → `TTHUCHI` (lọc `TDONHANGID` + `TDATHANGID`); nút "Tạo phiếu thu" gọi `TTHUCHI0Ae.SetTamUng` (xem Q19); `RealDelete = True` trên cả 3 grid Lương chi tiết / Tạm ứng.
- **Tính lương tay (`TinhLuong`):** lưu `TBANGLUONGTONGHOP` + `TBANGLUONGCHITIET` như §2.1.
- **Tính lương bằng máy (`TinhLuongBangMay`):** lưu `TBANGLUONGTONGHOP` qua `DbUtils.UpdateByFields` (xem §2.3); `TVAORA` do `LayDuLieuTuMay` ghi (xem §2.3).

## 4. Câu hỏi còn lại sau vòng 2

| # | Câu hỏi | Nguồn | Cần gì để trả lời |
|---|---|---|---|
| R1 | `TAMUNG`: công thức tay `SUM(CHI)` hay máy `SUM(THU−CHI)` mới đúng? | Q4 | Chiến chốt (đề xuất: `SUM(CHI)` cho cả hai form) |
| R2 | Thực tế quán có chi lương thiếu rồi chi nốt không? Nếu có, cần luồng chi nốt không? | Q9 | Chiến hỏi quán / chạy app gốc |
| R3 | Giá trị `TVAORA.SOGIO` thật trong DB sau khi chạy máy chấm công (số giờ × 100? làm tròn số nguyên?) | Q11 | Chạy app gốc + đọc DB thật (hoặc DB có dữ liệu thật) |
| R4 | Nội dung `TBANGLUONG.CHITIET` BLOB là gì? Nếu DB thật cũng trống thì bỏ cột khi clone | Q12 | DB thật có dữ liệu / hỏi nhà cung cấp |
| R5 | Giá trị `TinhLuongSuDungMayChamCong` + `IpMayChamCong` trên DATA.fdb POS TAP thật — quán đang chạy tay hay máy? IP máy chấm công? | Q16 | Đọc `SCONFIG` trên DATA.fdb thật |
| R6 | `TinhLuongBangMay` có phải form thừa của DEMO không (DEMO 112 form vs POS TAP 111)? Nếu POS TAP không có thì bỏ toàn bộ luồng lương máy khỏi phạm vi | Q1 | `SELECT NAME FROM SFORM` trên DATA.fdb thật |
| R7 | Bug `GioTinhLuong` (getter trả `dtBATDAU`) — có giữ lại hành vi này không? | Q18 | Chiến quyết (ngoài phạm vi tính giờ) |
| R8 | Lỗ hổng quyền Chấm công (user chỉ có quyền Chấm công vẫn lưu được tiền qua nút Cập nhật) — giữ y hệt hay chặn bằng quyền Tính lương khi lưu? | Mới (B7) | Chiến chốt |

## 5. Nghi vấn giống bug — cập nhật sau vòng 2

Mức: **🔴 nặng** · **🟡 vừa** · **🟢 nhẹ**. "Đã biết vòng 1" = đã có trong `nhan-su.md` vòng 1; "Mới" = vòng 2 phát hiện.

| # | Nghi vấn | Mức | Trạng thái | Bằng chứng | Đề xuất cho Chiến |
|---|---|---|---|---|---|
| B1 | Form máy mở lại hiện lương 0 cho tới khi bấm Refresh; bấm Cập nhật trong lúc đó ghi đè lương đã lưu bằng 0 | 🔴 | **Mới** | `LayDuLieuTuMay.btnSave` không set `TBANGLUONGID` cho `TVAORA` import; `LoadDetail` tính `SOGIOLAM = SUM(SOGIO) WHERE TBANGLUONGID = SelID` → 0 | Nếu làm lương máy: set `TBANGLUONGID = SelID` khi import, hoặc tính `SOGIOLAM` theo (NV, tháng) thay vì theo `TBANGLUONGID` |
| B2 | NV mới vào giữa tháng vô hình ở form máy; insert tạo mới không lọc `ITEMTYPE` | 🔴 | **Mới** | `LoadDetail` máy: `WHERE TBANGLUONGID = SelID` lọc mất NV chưa có dòng tổng hợp; tạo mới chỉ khi `dt.Rows.Count == 0`, `SELECT * FROM DNHANVIEN WHERE STATUS = 30` | Nếu làm lương máy: dùng UNION ALL như form tay + lọc `ITEMTYPE` |
| B3 | NV chưa gán ca → 0 giờ dù đã có đủ giờ vào/ra (cả khi sửa tay `CapNhatGio`) | 🟡 | **Mới** | `soGio` chỉ gán trong nhánh `DCALAMVIECID.Length > 0` | Nếu làm lương máy: tính giờ theo ca mặc định khi chưa gán, hoặc báo rõ NV chưa gán ca |
| B4 | `TAMUNG` 2 công thức (tay `SUM(CHI)` vs máy `SUM(THU−CHI)`) | 🟡 | Đã biết, nay xác nhận + phân tích | 2 SQL khác nhau (xem §2.5); lệch chỉ khi có phiếu thu `LATAMUNG = 30` | Chiến chốt 1 công thức (đề xuất `SUM(CHI)`) |
| B5 | Xóa bảng lương form tay để lại `TBANGLUONGTONGHOP` mồ côi | 🟡 | Đã biết, nay xác nhận DB không có FK/cascade | `grMain_OnDeleted` tay chỉ xóa `CHITIET`; 0 FOREIGN KEY trong toàn DB | Bản clone: xóa cả 2 bảng như form máy, hoặc chặn xóa khi đã có tổng hợp |
| B6 | `btnRefresh` máy: `soGioTangCa` luôn = 0; kẹp giờ vào/ra theo ca bị comment out (làm bao nhiêu tính bấy nhiêu, kể cả đi sớm/về muộn) | 🟡 | **Mới** | `btnRefresh_Click` không gán `soGioTangCa`; 2 dòng kẹp giờ trong code đã comment | Chiến chốt: có tính tăng ca tự động khi import không, hay chỉ tính khi sửa tay (`CapNhatGio`) |
| B7 | User chỉ có quyền Chấm công vẫn lưu được tiền (nút Cập nhật không kiểm tra quyền, vẫn ghi `TONGLUONG`/`THUCNHAN`) | 🟡 | Đã biết, nay xác nhận thêm | `workerSave_DoWork` không phân biệt chế độ; trong DEMO không nhóm nào tách 2 quyền nên lỗi chỉ lộ khi cấp lệch | Chiến chốt: giữ y hệt hay chặn bằng quyền Tính lương khi lưu (xem R8) |
| B8 | `workerSave_DoWork` gán nhầm `TBANGLUONGTONGHOPID = ID` dòng chi tiết (chỉ sai bộ nhớ, DB không sai) | 🟢 | Đã biết | Nhánh insert `TBANGLUONGCHITIET` trong `workerSave_DoWork` | Sửa khi clone (gán đúng hoặc bỏ dòng) |
| B9 | Khóa `dic` dùng `ddMMyy` (năm 2 chữ số); dòng trùng (NV, ca, ngày) bị nuốt lặng, chỉ update dòng đầu | 🟢 | **Mới** | `dic.Add` trong `try/catch` rỗng; key `NGAY.ToString("ddMMyy")` | Bản clone: dùng `yyyyMMdd` + chặn trùng bằng unique index |
| B10 | "Tải lại" (`RefreshData`) đọc từ `DNHANVIEN`, đè mất giá trị lương đã chốt ở `TONGHOP` | 🟡 | **Mới** | `RefreshData` không đọc `TBANGLUONGTONGHOP` | Bản clone: đọc từ `TONGHOP` nếu đã có, chỉ lấy từ `DNHANVIEN` khi chưa có |
| B11 | Form máy lấy `LUONGCA` từ `DNHANVIEN`, bỏ qua `TONGHOP.LUONGCA` đã lưu | 🟡 | **Mới** | SQL `LoadDetail` máy: `DNHANVIEN.LUONGCA` | Bản clone: ưu tiên `TONGHOP.LUONGCA` nếu đã có |
| B12 | Import máy chấm công lấy cả NV không còn hoạt động (không lọc `STATUS`) | 🟢 | **Mới** | `SELECT ID, CODE, NAME FROM DNHANVIEN` trong `LayDuLieuTuMay` | Bản clone: lọc `STATUS = 30` |
| B13 | `SOGIO`/`SOGIOTANGCA` decimal → `BIGINT`: cơ chế quy đổi nằm trong No1Lib, không thấy trong export | 🟡 | Đã biết, nay xác nhận thêm | DB `BIGINT`, lưới `N2`, code `Round(..., 2)`; không quy đổi trong code export | Cần xem giá trị thật (xem R3) |
| B14 | SFUNCTION "Thu tạm ứng khách hàng" (`8cbfb281`) tồn tại nhưng không code nào kiểm tra | 🟢 | **Mới** | 0 hit ngoài `sfunction.txt`; nút `btnTamUng` chỉ bị chặn bởi `DaThanhToan` | Bản clone: hoặc gắn quyền vào nút, hoặc bỏ function thừa |
| B15 | Nút lưu ghi "Cập nhât" (thiếu dấu ậ) | 🟢 | **Mới** | AELAYOUT form `Tính lương` (gốc) | Clone thì sửa thành "Cập nhật" (hoặc giữ y hệt tùy Chiến) |
| B16 | `TVAORA.GIOBATDAU`/`GIOKETTHUC` là cột chết | 🟢 | Đã biết (vòng 1: không ai ghi), nay xác nhận thêm | Chỉ đọc 1 lần ở `btnRefresh` rồi bỏ (kẹp giờ bị comment) | Bỏ 2 cột khi clone (hoặc giữ cho tương thích schema) |
| B17 | `LUONGTHEOCA` là cột chết | 🟢 | Đã biết (vòng 1: không dùng trong công thức), nay xác nhận toàn diện | 1 hit copy duy nhất; không form/báo cáo nào dùng | Giữ cột, không cần logic |
| B18 | 2 phiếu chi lương trùng (NV, bảng lương) → lưới Chi lương hiện 2 dòng (không `DISTINCT`, không unique) | 🟢 | Đã biết (Q8), nay xác nhận DB | `TTHUCHI` chỉ có PK `ID`; 0 UNIQUE/CHECK trong toàn DB | Bản clone: unique index `(TBANGLUONGID, DNHANVIENID)` hoặc check khi tạo |

**Lưu ý về số liệu DEMO (đã kiểm chứng trực tiếp):** `DNHANVIEN/DCALAMVIEC/TBANGLUONG/TBANGLUONGCHITIET/TBANGLUONGTONGHOP/TTHUONGPHAT/TVAORA/TTHUCHI` = **0 dòng** ở cả DEMO lẫn TEMPLATE; `DLYDOTHUONGPHAT` = 2 dòng; `DCHITIEUDOANHTHU` = 1 dòng (năm 2015, 100.000.000 × 12 tháng); `TBANGLUONG.CHITIET` BLOB = 0 dòng ở cả 2 DB. `SGROUPROLE` DEMO đếm được **269 dòng** (khác số 267 ghi trong `he-thong.md` §4.1 — lệch 2 dòng, không ảnh hưởng kết luận).

---

## Kết quả vòng 2

- **22 câu hỏi vòng 1: trả lời được hoàn toàn 16/22** (Q1, Q2, Q3, Q5, Q6, Q7, Q8, Q10, Q13, Q14, Q15, Q17, Q19, Q20, Q21, Q22 — trong đó Q1, Q10 có lưu ý giới hạn trong phạm vi export), **trả lời một phần 5/22** (Q4, Q9, Q11, Q12, Q16), **chưa trả lời được 1/22** (Q18 — ngoài phạm vi tính giờ, giữ nguyên).
- **Đối chiếu Claude: 31/33 khẳng định đúng; 2 đính chính** (C1: dòng "Chưa đọc" mâu thuẫn nội bộ; C2: xóa bảng lương form tay để lại `TONGHOP` mồ côi).
- **18 nghi vấn giống bug** (B1–B18): 10 mới (B1, B2, B3, B6, B9, B10, B11, B12, B14, B15), 6 đã biết vòng 1 nay có thêm bằng chứng (B4, B5, B7, B8, B13, B18), 2 đã biết vòng 1 nay được xác nhận thêm (B16, B17). Nặng nhất: **B1** (ghi đè lương bằng 0), **B2** (NV mới vô hình ở form máy).
- **8 câu hỏi còn lại** (R1–R8) — cần Chiến chốt hoặc DATA.fdb POS TAP thật: R1 (công thức `TAMUNG`), R5–R6 (quán chạy tay hay máy + form máy có phải form thừa DEMO), R3–R4 (giá trị `SOGIO` thật + nội dung BLOB), R2 (chi lương thiếu), R7 (bug `GioTinhLuong`), R8 (lỗ hổng quyền Chấm công).
