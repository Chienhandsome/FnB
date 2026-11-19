# POS TAP — nghiệp vụ tính lương nhân viên

Nguồn: SFORM `Tính lương__a7e47b60` (`TinhLuong`, 708 dòng), `Chi lương__7adf38ef`, `Lương chi tiết__19eec0a3`, `Tạm ứng lương__b0e7f8d4`; liên quan `Phiếu chi` (`SetLuong`). ~~Chưa đọc: `Tính lương bằng máy`, `Lấy dữ liệu từ máy`, bảng mô tả `DNHANVIEN`, `DCALAMVIEC`, `TBANGLUONG`, `TTHUONGPHAT`.~~ **[Sửa v2]** Dòng "Chưa đọc" này là ghi chú cũ chưa cập nhật: §4 và §5 bên dưới mô tả chi tiết đúng các form/bảng đó và đã được vòng 2 đối chiếu với code (31/33 khẳng định đúng, 2 chỗ sửa ở mục 1 và mục 4). Các form đã có trong `POS_TAP_SRC_EXPORT` thật: `Tính lương__a7e47b60`, `Tính lương bằng máy__0046913e`, `Chi lương`, `Lương chi tiết`, `Tạm ứng lương`, `Lấy dữ liệu từ máy`.

## 1. Mô hình

- `TBANGLUONG`: một bảng lương cho mỗi (tháng `THANG`, năm `NAM`). `TBANGLUONGTONGHOP`: một dòng cho mỗi nhân viên trong bảng lương (`LUONGCA`, `LUONGTHANG`, `CACHTINHLUONG`, `TONGLUONG`, `PHAT`, `THUONG`, `TAMUNG`, `THUCNHAN`). `TBANGLUONGCHITIET`: chấm công theo (nhân viên, ca `DCALAMVIEC`, ngày) với `TRANGTHAI`. Xóa bảng lương thì xóa chi tiết chấm công. **[Sửa v2]** Chính xác hơn: **form tay** (`grMain_OnDeleted`) chỉ `DELETE FROM TBANGLUONGCHITIET` → `TBANGLUONGTONGHOP` thành **mồ côi** (DB không có FOREIGN KEY nào nên không có cascade); chỉ **form máy** mới xóa cả 3 bảng (`CHITIET`, `TONGHOP`, `TVAORA`). Spec phải ghi rõ 2 hành vi, không gộp chung. Không có check trùng (tháng, năm) khi tạo bảng lương (không `BeforeSave`, không UNIQUE trong DB) — chỉ còn khả năng framework check qua `AllowDuplicate = False` của cột `NAME`.
- Chấm công `DiLamMode`: `0` không có lịch, `1` đi làm, `2` nghỉ có phép, `3` nghỉ không phép (trong DB lưu 0/1/2/3; 0 không lưu dòng).
- Cách tính lương `CachTinhLuong` (cột `DNHANVIEN.CACHTINHLUONG`): `0` theo tháng, `30` theo ca, `60` tháng theo ca. Cờ `NGHICHUNHAT`, `NGHITHU7` (=30) cho cách tính theo tháng.
- Ca làm việc `DCALAMVIEC` (`STATUS=30`, `ITEMTYPE` rỗng/0): `TILELUONG` % lương của ca (0 coi như 100).
- Nhân viên đưa vào bảng lương: `DNHANVIEN` `STATUS=30`, `ITEMTYPE` rỗng/0 (chưa có trong bảng lương thì lấy từ danh mục, chưa lưu).

## 2. Công thức (`UpdateThucNhan`)

Khoảng tính: từ ngày 1 đến cuối tháng của bảng lương; `totalDays` = số ngày trong tháng.
```
soCaDiLam  = Σ (TILELUONG của ca / 100)  cho mỗi ô có trạng thái ĐI LÀM hoặc NGHỈ CÓ PHÉP
soNgayDiLam = số ngày có ít nhất một ca ĐI LÀM hoặc NGHỈ CÓ PHÉP
(nghỉ không phép: không cộng; phạt tự động đã bị comment trong mã gốc)

TheoCa (30)      : TONGLUONG = LUONGCA × soCaDiLam
ThangTheoCa (60) : TONGLUONG = soCaDiLam × LUONGTHANG / totalDays
TheoThang (0)    : soNgayTrongThang =
                     NGHICHUNHAT=30 & NGHITHU7=30 → số ngày trừ T7 và CN
                     NGHICHUNHAT=30               → số ngày trừ CN
                     NGHITHU7=30                  → số ngày trừ T7
                     ngược lại                    → totalDays
                   TONGLUONG = soNgayDiLam × LUONGTHANG / soNgayTrongThang
THUCNHAN = TONGLUONG + THUONG − PHAT − TAMUNG
```
- `PHAT` / `THUONG` = Σ cột tương ứng trong `TTHUONGPHAT` của nhân viên có `NGAY` trong tháng; `TAMUNG` = Σ `CHI` của `TTHUCHI` nhân viên đó có `LATAMUNG = 30` và `NGAY` trong tháng.
- Tính tại chỗ khi sửa ô chấm công; chỉ ghi vào DB khi bấm "Cập nhật" (ghi tổng hợp và chi tiết chấm công). "Tải lại" nạp lại lương cơ bản, cách tính, thưởng/phạt/tạm ứng hiện tại của nhân viên. **[Bổ sung v2]** "Tải lại" đọc từ `DNHANVIEN` (không phải `TBANGLUONGTONGHOP`) nên đè cả giá trị lương đã chốt ở bảng tổng hợp. Trong `workerSave_DoWork` có dòng gán nhầm `TBANGLUONGTONGHOPID = ID` dòng chi tiết (chỉ sai trong bộ nhớ, DB không sai). Khóa dictionary chi tiết dùng `ddMMyy` (năm 2 chữ số); dòng trùng (NV, ca, ngày) bị nuốt lặng trong `try/catch` rỗng.
- **[Bổ sung v2 — quyền]** Chế độ "Chấm công" chỉ ẩn cột/nút tiền (`showLuong`), **nhưng nút Cập nhật vẫn hiện và vẫn ghi `TONGLUONG`/`THUCNHAN`**; code không kiểm tra quyền nào khi lưu → user chỉ có quyền Chấm công vẫn lưu được tiền. Chỉ lộ khi cấp lệch 2 quyền (trong `SGROUPROLE` mẫu Quản lý/Kế toán = 240 cả 6, Phục vụ = 0, Thu ngân/Thủ kho khóa). Chiến chốt: giữ y hệt hay chặn bằng quyền Tính lương khi lưu.
- **[Bổ sung v2]** Chi lương: không kiểm tra mỗi NV chỉ 1 phiếu/bảng lương ở cả code lẫn DB (`TTHUCHI` chỉ có PK `ID`); tạo thủ công 2 phiếu → lưới Chi lương hiện 2 dòng. Không có luồng "chi nốt": sau khi lưu nút đổi thành "Xem phiếu"; muốn chi thêm phải sửa `CHI` trên phiếu cũ hoặc xóa tạo lại. Phiếu chi lương/tạm ứng **trừ quỹ ngay khi lưu** (tồn quỹ là truy vấn trực tiếp), không có bước duyệt. Không có trần tiền/số lần tạm ứng.
- **[Bổ sung v2]** Không có báo cáo lương nào trong `SREPORT` (0 báo cáo tên lương/nhân viên/chấm công; `LUONGTHEOCA`/`SOGIOLAM` 0 hit trong SQL báo cáo); chỉ tiêu doanh thu (`DCHITIEUDOANHTHU`) không báo cáo nào đọc.
- Giao diện: lưới nhân viên × ca × ngày, nhân viên bấm đúp vào cột Phạt / Thưởng / Tạm ứng để mở chi tiết (`LuongChiTiet`: danh sách thưởng, phạt, hoặc phiếu tạm ứng của tháng đó; ngày nhập phải nằm trong tháng bảng lương); bấm đúp vào Nhân viên / Lương cơ bản để sửa hồ sơ nhân viên. Có chế độ chỉ xem công (menu khác "Tính lương") không hiện lương và nút chi.

## 3. Chi lương và tạm ứng

- `Chi lương`: danh sách (nhân viên, thực nhận, số tiền đã chi `TTHUCHI.CHI` theo `TBANGLUONGID` + nhân viên, số phiếu). Tạo phiếu chi lương cho nhân viên bằng form `TTHUCHI1Ae.SetLuong` (xem file thu chi): diễn giải "Chi tiền lương tháng M năm Y", số tiền mặc định = `THUCNHAN`; nếu `THUCNHAN = 0` thì cảnh báo "chưa thiết lập cách tính lương".
- `Tạm ứng lương`: danh sách phiếu chi (`TTHUCHI` `LOAI=1`) với `LATAMUNG = 30`; phiếu mới tự đặt `LATAMUNG = 30` và đối tượng là nhân viên.

## 4. Tính lương bằng máy chấm công (`TinhLuongBangMay`, `LayDuLieuTuMay`)

- Dữ liệu vào/ra bảng `TVAORA` (`DNHANVIENID`, `NGAY`, `GIOVAO`, `GIORA`, `SOGIO`, `SOGIOTANGCA`, `NOTE`, `TBANGLUONGID`). Lấy từ máy chấm công ZK qua `zkemkeeper\zkemkeeper.exe <IP> <file> AttLog` (IP mặc định `192.168.1.201`, lưu `SystemConfig.IpMayChamCong`; chờ tối đa 2 phút). Với mỗi ngày trong tháng và mỗi nhân viên (khớp `USERID` của máy với `DNHANVIEN.CODE`): giờ vào = lần quét đầu, giờ ra = lần quét cuối (nếu có hơn một lần), `SOLAN` = số lần quét. Tùy chọn xóa dữ liệu cũ của tháng trước khi nạp và xóa log trên máy sau khi lấy (`ClearAttLog`). Chỉ lưu giờ:phút.
- Giờ làm của một ngày (`CapNhatGio`): ca của nhân viên (`DNHANVIEN.DCALAMVIECID`, `TUGIO`–`DENGIO`, ca qua đêm thì `từ − 1 ngày`): `soGioPhaiLam = Round(giờ ca, 2)`; `tongGioDaLam = Round(giờ ra − giờ vào, 2)` (giờ vào > giờ ra thì lùi 1 ngày); nếu `tongGioDaLam > soGioPhaiLam` thì `SOGIO = soGioPhaiLam`, `SOGIOTANGCA = chênh lệch`, ngược lại `SOGIO = tongGioDaLam`, tăng ca 0. Nhân viên chưa gán ca thì 0 giờ.
- Lương tháng: `TONGLUONG = Round((SOGIOLAM + SOGIOTANGCA × 1.5) × LUONGCA, 0)` (tăng ca hưởng 150%); `THUCNHAN = TONGLUONG + THUONG − TAMUNG − PHAT`. Trong bản này `TAMUNG = Σ(THU − CHI)` của phiếu có `LATAMUNG = 30` trong tháng (khác bản tính lương theo công, chỉ cộng `CHI`).
- Nạp ngày trống: mở một nhân viên sẽ tự thêm các ngày chưa có dòng `TVAORA` với 0 giờ. Xóa bảng lương xóa chi tiết, tổng hợp và `TVAORA` của bảng đó. Nút Chi lương như bản tính theo công.
- **[Bổ sung v2] Form lương máy hiện không có caller/menu nào trong export** (grep chỉ thấy trong chính nó; `SMENU` không có dòng; cờ `TinhLuongSuDungMayChamCong` chỉ được đọc ở `DNHANVIENAe` và `DCALAMVIECAe`, không dùng để chọn form lương). Vòng 2 (đọc DEMO) từng suy đoán đây là "form thừa của DEMO" (DEMO 112 form vs POS TAP 111) — **[Sửa v2] suy đoán bị bác theo dữ liệu thật:** `Tính lương bằng máy__0046913e` **có** trong `POS_TAP_SRC_EXPORT` (111 form). Form thừa của DEMO là form khác (chưa xác định). **Đo trên DB thật:** `Tính lương bằng máy` và `Lấy dữ liệu từ máy` không có menu `STATUS=30` nào trỏ tới (chỉ `Tính lương` có 2 menu); `TinhLuongSuDungMayChamCong` = NULL. Việc "luồng lương máy có dùng thật không" vẫn mở (checklist NS-R5/R6); gợi ý nghiêng về "không dùng": thư mục cài `POS TAP\` không có `zkemkeeper\zkemkeeper.exe` mà `LayDuLieuTuMay` cần.
- **[Bổ sung v2 — nghi bug nặng B1/B2 của form máy]** (a) `LayDuLieuTuMay.btnSave` không gán `TBANGLUONGID` cho dòng `TVAORA` import, mà `LoadDetail` tính `SOGIOLAM = SUM(SOGIO) WHERE TBANGLUONGID = SelID` → mở lại form hiện **0 giờ / lương 0 cho tới khi bấm Refresh**; bấm Cập nhật lúc đó **ghi đè lương đã lưu bằng 0**. (b) Nhân viên mới vào giữa tháng không bao giờ xuất hiện ở form máy (dòng tổng hợp chỉ tạo khi `dt.Rows.Count == 0`, cho mọi NV `STATUS=30`, không lọc `ITEMTYPE`). (c) NV chưa gán ca → 0 giờ dù đã có giờ vào/ra. (d) `btnRefresh` máy: `soGioTangCa` luôn = 0, đoạn kẹp giờ theo ca bị comment out. (e) Form máy lấy `LUONGCA` từ `DNHANVIEN`, bỏ qua `TONGHOP.LUONGCA` đã lưu. (f) Import lấy cả NV không còn hoạt động (không lọc `STATUS`). Chưa chạy app gốc để kiểm chứng (checklist NS-R5).
- **[Bổ sung v2] `TAMUNG` có 2 công thức khác nhau**: form tay `SUM(CHI)`, form máy `SUM(THU − CHI)` (phiếu `LATAMUNG = 30` trong tháng); lệch chỉ khi có phiếu thu `LATAMUNG = 30` (loại phiếu chỉ tạo thủ công nên hiếm). Đề xuất `SUM(CHI)` cho cả hai — Chiến chốt. `SOGIO`/`SOGIOTANGCA` là `BIGINT` ở DB nhưng code tính `Round(…, 2)`, lưới hiển thị `N2`; cơ chế quy đổi nằm trong No1Lib (**[Chưa chốt]**, cần xem giá trị thật sau khi chạy máy chấm công).
- `TVAORA.GIOBATDAU/GIOKETTHUC` và `TBANGLUONG.CHITIET` (BLOB) không ai ghi/đọc → cột chết (DEMO: 0 dòng).

## 5. Khai báo liên quan (STABLEDESC)

- **Nhân viên** (`DNHANVIENAe`): cách tính lương `CACHTINHLUONG`: theo ca (`30`, nhập `LUONGCA`), theo tháng (`0`, nhập `LUONGTHANG`, tùy chọn `NGHICHUNHAT`, `NGHITHU7`, mặc định tạo mới: theo ca, nghỉ T7 có, nghỉ CN không), tháng theo ca (`60`). Khi `TinhLuongSuDungMayChamCong=30`: ẩn nhóm thông tin lương theo công, bắt buộc chọn ca làm việc `DCALAMVIECID`, hiện `LUONGTHEOCA` và mã chấm công `CODE` (khớp với `USERID` trên máy chấm công); ngược lại ẩn các trường này. **[Bổ sung v2]** `LUONGTHEOCA` là ô nhập có thật trên form Nhân viên (`numLUONGTHEOCA`, `DataField = LUONGTHEOCA`; đã kiểm trên `POS_TAP_SRC_EXPORT/STABLEDESC/DNHANVIEN`), nhưng **không công thức/báo cáo nào tiêu thụ giá trị**: duy nhất 1 chỗ trong code form là `thRow.LUONGTHEOCA = rNhanVienRow.LUONGTHEOCA` (copy khi tạo dòng tổng hợp ở form máy). Tức "cột nhập được nhưng không có tác dụng" — giữ cột + ô nhập, không cần logic. **Không có "công thức lương mẫu"** (ExcelSalaryEngine/`Formula`/`Evaluate`) ở đâu: grep 0 kết quả trên toàn bộ code/AELAYOUT/DESIGNCODE/TSCODE/SCONFIG của DEMO (vòng 2); riêng tên `ExcelSalaryEngine` cũng 0 kết quả trong `POS_TAP_SRC_EXPORT`; mọi công thức lương hard-code C# (`UpdateThucNhan` tay, `TinhLuong` máy). Giá trị DEMO: `TinhLuongSuDungMayChamCong = NULL` (chế độ tay), `IpMayChamCong = ''` (mặc định `192.168.1.201`); giá trị POS TAP thật **chưa biết** (checklist NS-R5).
- **Bảng lương** (`TBANGLUONGAe`): tháng 1–12, năm 0–2999, tên tự sinh "Tháng m/yyyy"; mặc định tháng/năm hiện tại.
- **Thưởng / phạt** (`TTHUONGPHATAe`): ngày phải nằm trong tháng của bảng lương khi gọi từ chi tiết lương (`SetGioiHanNgay`).
- **Chỉ tiêu doanh thu** (`DCHITIEUDOANHTHUAe`): chỉ tiêu theo năm (tên = năm, số nguyên; mặc định năm lớn nhất đã có hoặc năm hiện tại) và theo tỉnh thành cha (`DTINHTHANH`, mỗi tỉnh cha một dòng chi tiết). Chưa thấy báo cáo nào đọc bảng này trong export.
