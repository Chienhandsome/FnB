# POS TAP — nghiệp vụ đặt phòng / đặt hàng, in xuống bếp

Nguồn: SFORM `Theo dõi đặt phòng__11f4bf28` (972 dòng), STABLEDESC `TDATHANG__780a9daf` (`TDATHANGAe`, 786 dòng), SFORM `In chế biến__c4a05471` (`code.cs` 423 + `ts_code.cs` 476), `In xuống bếp control__d4139d75` (`InXuongBepControl`, 197).

## 1. Phiếu đặt phòng / đặt hàng (`TDATHANG`, chi tiết `TDATHANGCHITIET`)

- Một phiếu đặt cho một bàn/phòng, các phiếu của cùng một **đoàn** chia sẻ `GUID` (mỗi phòng một dòng `TDATHANG`, dùng chung thông tin khách / thời gian / giảm giá / mặt hàng). Khi lưu: các phòng được chọn mới thì tạo phiếu mới (số phiếu `DbUtils.GenSoHoaDon` theo mẫu `NOTEMPLATE` của `TDATHANG`) hoặc tái sử dụng phiếu của phòng đã bị bỏ; phiếu của phòng bị bỏ thì xóa; sau đó **sao chép toàn bộ thông tin chung và chi tiết mặt hàng** sang các phiếu còn lại trong đoàn (xóa chi tiết cũ, chép lại).
- Ứng dụng khách sạn (`UngDung.HOTEL`): dùng `TUNGAY`/`DENNGAY` (ngày); ứng dụng khác dùng `TUGIO`/`DENGIO` (giờ), và `TUNGAY`/`DENNGAY` suy ra từ phần ngày của giờ (00:00). Kiểm tra: chọn ít nhất một phòng; `DENNGAY ≥ TUNGAY`.
- Màu hiển thị `MAUSAC`, giờ đặt `GIODAT`, `TENKHACH`, `DIENTHOAI`, `DIACHI`, `NOTE`, mục đích đặt `DMUCDICHDAT`.
- Chi tiết: thêm hàng (đơn giá `GIABAN`, hoặc theo loại giá khách `DKHACHHANG.GIABAN` = 0/1/2/3 → `GIABAN`/`GIABAN2`/`GIABAN3`/`GIABAN4` khi `SuDungGiaTheoKhach=30`, đổi khách thì tính lại giá cả phiếu); gộp dòng cùng mặt hàng + đơn vị; hỗ trợ 2 đơn vị (đơn vị chẵn có giá `GIABANCHAN`); nhập từ Excel.
- Tổng tiền (`mapper_OnCalculation`) giống phiếu nhập kho: `TIENHANG = ΣTHANHTIEN`; giảm giá %/tiền (`TIENGIAMGIA = 100 × Round(TIENHANG × TL / 10000)`); `TIENTHUE = TILETHUE × (TIENHANG − TIENGIAMGIA)/100`; `TONGCONG = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN` (không làm tròn).
- **Đặt tiền trước**: tạo phiếu thu `TTHUCHI` với `TDATHANGID` (mặc định `THU = TONGCONG` của phiếu đặt, đối tượng là khách, diễn giải "Đặt tiền trước số phiếu đặt hàng: <số>", lý do lấy theo lần đặt tiền trước gần nhất). Hiển thị số phiếu thu và số tiền. Khi thanh toán bill tạo từ đặt hàng, `datTruoc` = `THU` của phiếu này (xem file thanh toán). Xóa phiếu đặt không được nếu đã có tiền đặt hoặc đã tạo hóa đơn (`DbUtils.CountRef`).
- Mở bàn từ đặt phòng (`MoBanTuDatPhong`): chọn phiếu đặt (`TimKiemDatTruoc`), tạo bill gắn `TDATHANGID`, `DKHACHHANGID`, và nạp mặt hàng đã đặt vào bill (giá đặt nếu cùng phòng, ngược lại giá khu vực).
- Bill nhà hàng chỉ dùng được phiếu đặt chưa có bill (`NOT EXISTS TDONHANG WHERE TDATHANGID`).

## 2. Màn hình `Theo dõi đặt phòng`

Lưới lịch dạng Gantt: dòng = phòng (nhóm theo loại phòng `DLOAIPHONG`, thứ tự `SORTORDER`, `STATUS=30`; có thể lọc 1 loại), cột = ngày (7 / 15 / 30 / 60 / 90 ngày, bội 2 / 4 hoặc chia 2 / 4, tối thiểu 7 tối đa 90; có chế độ tháng này / trước / sau). Mỗi đặt phòng là một ô gộp từ `TUNGAY` đến `DENNGAY` (màu `MAUSAC`).
- Thao tác: kéo thả / co giãn ô để đổi phòng và khoảng ngày (hỏi xác nhận; đổi ngày được áp cho cả đoàn cùng `GUID`; chỉ cho phép kéo trong vùng hiển thị, nếu ô vượt vùng hiển thị thì mở cửa sổ sửa); tạo đặt phòng từ vùng ô chọn (các phòng và khoảng ngày chọn); sửa; xóa (xóa cả đoàn cùng `GUID`, chặn nếu có liên kết). Phím: F5 làm tươi, Insert tạo, Delete xóa, F4 sửa.
- Các dòng tổng cuối: **Đã bán** = số phòng có đặt trong ngày; **Còn lại** = tổng số phòng `STATUS=30` − đã bán; **Tổng cộng** = tổng số phòng; **Công suất** = `Round(100 × đã bán / tổng số phòng, 1)%`; cột "Tổng" theo phòng = số ngày đặt (cột cuối − cột đầu, tính chênh lệch ngày chứ không +1). Dòng nhóm theo loại phòng cũng có số phòng đã đặt mỗi ngày.

## 3. In xuống bếp / pha chế

- **Lấy danh sách món cần in** (`InCheBienTs.GetSqlInCheBien`): từ chi tiết bill (`SLXUATCHUAQUYDOI`, `TENHANG`, `NOTE`, đơn vị, `DLOAIDOID` của nhóm mặt hàng, mặc định `'1'` nếu rỗng, loại **combo `4`, combo mở `5` và dịch vụ theo giờ `7`**), trừ đi những gì đã in trong `TINCHEBIEN` (cùng bill): `SUM(SOLUONG)` nhóm theo (ghi chú, tên hàng, loại đồ, đơn vị [, đơn giá nếu `InDonGiaTrenMauInBep=30`]), chỉ giữ nhóm có `|100 × tổng| > 1` (lệch hơn 0,01). Số lượng dương: in thêm; âm (đã in nhiều hơn số hiện có, do giảm / xóa / chuyển) cũng được hiển thị.
- **Chia theo loại đồ**: `0` = đồ ăn (máy in bếp), `1` = đồ uống (bar), `3` = đồ khác, `2` = dịch vụ; chỉ loại có `InDoAn/InDoUong/InDoKhac/InDichVu = 30`. Máy in: cấu hình trong registry (`EasyReg`): chung (`MAY_IN_DO_AN`, `MAY_IN_DO_UONG`, `MAY_IN_KHAC`, `MAY_IN_DICH_VU`) hoặc theo khu vực khi `InPhaCheTheoKhuVuc=30` (khóa `<ID khu vực>_AN / _UONG / _KHAC / _DICHVU`); chỉ chấp nhận tên máy in đang cài trong Windows (không thì coi như chưa cấu hình).
- `InRiengDoAnUong=30`: in riêng mỗi loại một phiếu; ngược lại gộp các loại cùng máy in thành một phiếu (tiêu đề ghép). Chưa cấu hình máy in cho loại có món → mở bảng thiết lập, không in. In xong mới ghi nhận.
- **Ghi nhận đã in**: mỗi dòng đã in lưu vào `TINCHEBIEN` (`TDONHANGID`, `TENHANG`, `SOLUONG` (có thể âm), `NOTE`, `DDONVITINHID`, `DLOAIDOID`, `LANSO`, `DONGIA`). Bảng này bị xóa khi bill thanh toán hoặc bị hủy.
- **Lần số in**: `TSOINCHEBIEN` (ngày giao dịch + tên máy in → `SOLANIN`), tăng 1 mỗi lần in trên máy đó trong ngày; in ra phiếu "LẦN IN".
- Số liên `SoLienInCheBien` (1–5); mẫu `MauInCheBien` (rỗng thì hiện chọn mẫu / xem trước); `InMoiDoRa1To=30`: mỗi món in thành một phiếu riêng; `InThem1LienTaiQuay=30`: thêm một liên ra máy in mặc định tại quầy. Thông tin phiếu: tên bàn, khu vực, số hóa đơn, lần in, "In bởi" (người dùng).
- Có thể ghi lưu vết quá trình in (`LuuVetInCheBien=30`). Hóa đơn đã thanh toán thì không in được.
- Phiếu bản cảm ứng (`InCheBienTs`) có cùng SQL nhưng hiển thị dạng thẻ lớn và chọn máy in bằng form `ChonMayInTs`.
