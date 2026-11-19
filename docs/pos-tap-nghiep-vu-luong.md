# POS TAP — nghiệp vụ tính lương nhân viên

Nguồn: SFORM `Tính lương__a7e47b60` (`TinhLuong`, 708 dòng), `Chi lương__7adf38ef`, `Lương chi tiết__19eec0a3`, `Tạm ứng lương__b0e7f8d4`; liên quan `Phiếu chi` (`SetLuong`). Chưa đọc: `Tính lương bằng máy` (chấm công từ máy), `Lấy dữ liệu từ máy`, bảng mô tả `DNHANVIEN`, `DCALAMVIEC`, `TBANGLUONG`, `TTHUONGPHAT`.

## 1. Mô hình

- `TBANGLUONG`: một bảng lương cho mỗi (tháng `THANG`, năm `NAM`). `TBANGLUONGTONGHOP`: một dòng cho mỗi nhân viên trong bảng lương (`LUONGCA`, `LUONGTHANG`, `CACHTINHLUONG`, `TONGLUONG`, `PHAT`, `THUONG`, `TAMUNG`, `THUCNHAN`). `TBANGLUONGCHITIET`: chấm công theo (nhân viên, ca `DCALAMVIEC`, ngày) với `TRANGTHAI`. Xóa bảng lương thì xóa chi tiết chấm công.
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
- Tính tại chỗ khi sửa ô chấm công; chỉ ghi vào DB khi bấm "Cập nhật" (ghi tổng hợp và chi tiết chấm công). "Tải lại" nạp lại lương cơ bản, cách tính, thưởng/phạt/tạm ứng hiện tại của nhân viên.
- Giao diện: lưới nhân viên × ca × ngày, nhân viên bấm đúp vào cột Phạt / Thưởng / Tạm ứng để mở chi tiết (`LuongChiTiet`: danh sách thưởng, phạt, hoặc phiếu tạm ứng của tháng đó; ngày nhập phải nằm trong tháng bảng lương); bấm đúp vào Nhân viên / Lương cơ bản để sửa hồ sơ nhân viên. Có chế độ chỉ xem công (menu khác "Tính lương") không hiện lương và nút chi.

## 3. Chi lương và tạm ứng

- `Chi lương`: danh sách (nhân viên, thực nhận, số tiền đã chi `TTHUCHI.CHI` theo `TBANGLUONGID` + nhân viên, số phiếu). Tạo phiếu chi lương cho nhân viên bằng form `TTHUCHI1Ae.SetLuong` (xem file thu chi): diễn giải "Chi tiền lương tháng M năm Y", số tiền mặc định = `THUCNHAN`; nếu `THUCNHAN = 0` thì cảnh báo "chưa thiết lập cách tính lương".
- `Tạm ứng lương`: danh sách phiếu chi (`TTHUCHI` `LOAI=1`) với `LATAMUNG = 30`; phiếu mới tự đặt `LATAMUNG = 30` và đối tượng là nhân viên.

## 4. Tính lương bằng máy chấm công (`TinhLuongBangMay`, `LayDuLieuTuMay`)

- Dữ liệu vào/ra bảng `TVAORA` (`DNHANVIENID`, `NGAY`, `GIOVAO`, `GIORA`, `SOGIO`, `SOGIOTANGCA`, `NOTE`, `TBANGLUONGID`). Lấy từ máy chấm công ZK qua `zkemkeeper\zkemkeeper.exe <IP> <file> AttLog` (IP mặc định `192.168.1.201`, lưu `SystemConfig.IpMayChamCong`; chờ tối đa 2 phút). Với mỗi ngày trong tháng và mỗi nhân viên (khớp `USERID` của máy với `DNHANVIEN.CODE`): giờ vào = lần quét đầu, giờ ra = lần quét cuối (nếu có hơn một lần), `SOLAN` = số lần quét. Tùy chọn xóa dữ liệu cũ của tháng trước khi nạp và xóa log trên máy sau khi lấy (`ClearAttLog`). Chỉ lưu giờ:phút.
- Giờ làm của một ngày (`CapNhatGio`): ca của nhân viên (`DNHANVIEN.DCALAMVIECID`, `TUGIO`–`DENGIO`, ca qua đêm thì `từ − 1 ngày`): `soGioPhaiLam = Round(giờ ca, 2)`; `tongGioDaLam = Round(giờ ra − giờ vào, 2)` (giờ vào > giờ ra thì lùi 1 ngày); nếu `tongGioDaLam > soGioPhaiLam` thì `SOGIO = soGioPhaiLam`, `SOGIOTANGCA = chênh lệch`, ngược lại `SOGIO = tongGioDaLam`, tăng ca 0. Nhân viên chưa gán ca thì 0 giờ.
- Lương tháng: `TONGLUONG = Round((SOGIOLAM + SOGIOTANGCA × 1.5) × LUONGCA, 0)` (tăng ca hưởng 150%); `THUCNHAN = TONGLUONG + THUONG − TAMUNG − PHAT`. Trong bản này `TAMUNG = Σ(THU − CHI)` của phiếu có `LATAMUNG = 30` trong tháng (khác bản tính lương theo công, chỉ cộng `CHI`).
- Nạp ngày trống: mở một nhân viên sẽ tự thêm các ngày chưa có dòng `TVAORA` với 0 giờ. Xóa bảng lương xóa chi tiết, tổng hợp và `TVAORA` của bảng đó. Nút Chi lương như bản tính theo công.

## 5. Khai báo liên quan (STABLEDESC)

- **Nhân viên** (`DNHANVIENAe`): cách tính lương `CACHTINHLUONG`: theo ca (`30`, nhập `LUONGCA`), theo tháng (`0`, nhập `LUONGTHANG`, tùy chọn `NGHICHUNHAT`, `NGHITHU7`, mặc định tạo mới: theo ca, nghỉ T7 có, nghỉ CN không), tháng theo ca (`60`). Khi `TinhLuongSuDungMayChamCong=30`: ẩn nhóm thông tin lương theo công, bắt buộc chọn ca làm việc `DCALAMVIECID`, hiện `LUONGTHEOCA` và mã chấm công `CODE` (khớp với `USERID` trên máy chấm công); ngược lại ẩn các trường này.
- **Bảng lương** (`TBANGLUONGAe`): tháng 1–12, năm 0–2999, tên tự sinh "Tháng m/yyyy"; mặc định tháng/năm hiện tại.
- **Thưởng / phạt** (`TTHUONGPHATAe`): ngày phải nằm trong tháng của bảng lương khi gọi từ chi tiết lương (`SetGioiHanNgay`).
- **Chỉ tiêu doanh thu** (`DCHITIEUDOANHTHUAe`): chỉ tiêu theo năm (tên = năm, số nguyên; mặc định năm lớn nhất đã có hoặc năm hiện tại) và theo tỉnh thành cha (`DTINHTHANH`, mỗi tỉnh cha một dòng chi tiết). Chưa thấy báo cáo nào đọc bảng này trong export.
