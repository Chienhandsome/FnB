# POS TAP — nghiệp vụ kho

Nguồn: SFORM `Phiếu nhập kho__24399cd6` (821 dòng), `Phiếu xuất kho__1f13b546` (552), `Phiếu chuyển kho__cc92bb29` (609), `Phiếu kiểm kê__93d9d7d4` (544), `Tồn kho__f0cc94d9` (303, chứa `TonKhoHandler`), `Tồn kho ban đầu__cb34a58c` (340), `Tính lại giá vốn__12ab9341` (512). Chưa đọc: `Phân tích kiểm kê`, `Quản lý nhập/xuất/chuyển kho/kiểm kê` (form danh sách, 28–35 dòng), `Lấy dữ liệu từ máy`, `Đánh lại mã hàng`, `Xuất lại vật tư`, `Hàng tồn hết hạn dùng`, `Nhập hạn sử dụng`, `Chọn kích thước nhập kho`, `Nhập dữ liệu`.

## 1. Mô hình dữ liệu kho

Mọi chứng từ kho và bán hàng dùng chung hai bảng `TDONHANG` / `TDONHANGCHITIET`; cột `TDONHANG.LOAI` phân loại (enum `LoaiPhieu` trong `Tính lại giá vốn`):

| LOAI | Ý nghĩa | Lớp form |
|---|---|---|
| 0 | Bán hàng (cả bán lẻ và nhà hàng) | `HoaDonBanHang` / `TDONHANG0Ae` |
| 1 | Nhập hàng | `TDONHANG1Ae` |
| 2 | Xuất khác | `TDONHANG2Ae` |
| 3 | Chuyển kho | `TDONHANG3Ae` |
| 4 | Kiểm kê | `TDONHANG4Ae` |
| 97 | Tồn đầu kỳ (tồn kho ban đầu, `NAME='TONDAU'`, một phiếu cho mỗi kho, `DKHONHAPID` = kho) | `TonKhoBanDau` |

(Còn các mã khác chưa gặp ở form đã đọc, ví dụ công nợ ban đầu — xem phần công nợ.)

Mỗi dòng chi tiết: `SLNHAP` (cộng kho) và `SLXUAT` (trừ kho), cả hai bằng **đơn vị cơ sở** (`DMATHANG.DDONVITINHID`); `SLNHAPCHUAQUYDOI` / `SLXUATCHUAQUYDOI` là số lượng theo đơn vị người dùng chọn (đơn vị cơ sở hoặc đơn vị chẵn `DDONVITINHCHANID`, quy đổi `DMATHANG.QUYDOI`). Mặt hàng có 2 đơn vị khi `DDONVITINHCHANID` có giá trị và khác `DDONVITINHID` (`TonKhoHandler.Has2DonViTinh`). Khi `SuDung2DonViTinh = 0`, sau khi lưu mọi chứng từ đều chạy `SLNHAP := SLNHAPCHUAQUYDOI`, `SLXUAT := SLXUATCHUAQUYDOI`.

## 2. Tồn kho (`TonKhoHandler`)

- `GetTonKho(mặtHàng, kho, trừChứngTừ) = COALESCE(SUM(COALESCE(SLNHAP,0) − COALESCE(SLXUAT,0)), 0)` trên chi tiết có `DKHOHANGID = kho`, loại chứng từ `trừChứngTừ` ra, và chỉ tính **chứng từ `LOAI <> 0` hoặc bill bán hàng đã thanh toán (`LOAI = 0 AND DATHANHTOAN = 30`)**. Bill bán chưa thanh toán không trừ tồn.
- `GetTonKhoEx` thêm điều kiện `KICHTHUOC = …` và `HANSUDUNG = …` khi có.
- Tồn theo kích thước / hạn dùng (khi `MatHangCoKichThuoc` / `MatHangCoHanSuDung`): tổng `SLNHAP − SLXUAT` nhóm theo `KICHTHUOC` hoặc `HANSUDUNG`, bỏ nhóm bằng 0.
- Hiển thị tồn 2 đơn vị: `tonChan = (int)(tồn / QUYDOI)`, `tonLe = tồn − tonChan × QUYDOI`; chuỗi "N <ĐVT chẵn> M <ĐVT lẻ>".
- Nhắc tồn tối thiểu (`Reminder`): giữ lại các mặt hàng có `TONTOITHIEU > 0` và `tồn ≤ TONTOITHIEU`.
- Kho: `DKHOHANG.CHOPHEPAMKHO` (0 = không cho âm → kiểm tồn khi bán / xuất / chuyển). Không phải admin chỉ thấy kho của cửa hàng trong `TNGUOIDUNGTHEOCUAHANG`.
- Không có quyền `XemGiaNhap`: ẩn đơn giá, thành tiền, giá vốn, giá trị vốn / bán, không in.

## 3. Nhập kho (`TDONHANG1Ae`)

- Thêm hàng: bắt buộc SL > 0; đơn giá mặc định `GIANHAP` của mặt hàng (nhập tay được); nếu có 2 đơn vị và mặt hàng có đơn vị chẵn thì dòng thêm bằng đơn vị chẵn (`QUYDOI`) — người dùng bấm vào ô ĐVT để đổi.
- Gộp dòng nếu cùng mặt hàng + cùng đơn giá (+ cùng kích thước / hạn dùng) trừ khi `NhapMotMatHangNhieuLanTrongPhieu=30`.
- Dòng: `SLNHAP = SLNHAPCHUAQUYDOI × quyDoi`. `THANHTIEN = Round(SL × DONGIA × (1 − TL/100), 0)`; sửa `THANHTIEN` thì `DONGIA = Round(THANHTIEN / (SL × (1 − TL/100)), 0)` (0 nếu SL = 0 hoặc TL = 100).
- Tổng phiếu: `TIENHANG = ΣTHANHTIEN`; giảm giá theo % hoặc theo tiền (như bán hàng, `TIENGIAMGIA = 100 × Round(TIENHANG × TL / 10000)`); `TIENTHUE = TILETHUE × (TIENHANG − TIENGIAMGIA) / 100`; `TONGCONG = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN` (**không làm tròn `LamTronTien`**).
- Kiểm tra trước khi lưu: mọi dòng `SLNHAP > 0`; `DKHOHANGID` dòng = kho nhập; khi `MatHangCoHanSuDung=30`, mặt hàng `COHANSUDUNG=30` phải có hạn dùng (nhập từ form `NhapHanSuDung`); `BatBuocChonNhanVienTrongNhapKho`, `BatBuocChonNhaCungCapTrongNhapKho`; ngày chỉ sửa được nếu có quyền `ThayDoiNgayTrongCacPhieu`.
- Sau khi lưu: nếu `TuDongTinhGiaVon=30` chạy `Tính lại giá vốn` ngầm (mục 7) — mã gọi `UpdateGiaVon(where)` nhưng `TinhLaiGiaVon.ThucHien()` luôn tính lại toàn bộ.
- Danh sách mặt hàng nhập: `DLOAIMATHANGID` rỗng, hoặc "mặt hàng kiêm vật tư", hoặc "nguyên liệu". Mặc định lời giải thích "Nhập mua hàng", kho mặc định `Shared.DKHOHANGID`.
- Tiện ích: nhập từ Excel (cột bắt buộc: Mã hàng hoặc Tên hàng theo `SuDungMaHang`, Số lượng; tùy chọn ĐVT, đơn giá, % giảm, hạn dùng, kích thước, ghi chú; kiểm tra đơn vị tính thuộc mặt hàng); in mã vạch / Bartender theo số lượng nhập; xuất Excel (một dòng cho mỗi đơn vị nhập, kèm giá bán).

## 4. Xuất khác (`TDONHANG2Ae`)

- Đơn giá xuất mặc định: `GIABAN` (hoặc `GIANHAP` / `GIAVON` theo `SystemConfig.GiaSuDungKhiChuyenXuatKho` = 0 / 1 / 2).
- `TONGCONG = TIENHANG − TIENGIAMGIA` (không thuế, không phí vận chuyển; các giá trị này đặt 0 khi tạo mới).
- Kiểm tra khi lưu: nếu kho `CHOPHEPAMKHO = 0` thì với mọi dòng thêm/sửa, tổng `SLXUAT` của mặt hàng trên phiếu ≤ `GetTonKho(mặt hàng, kho xuất, phiếu hiện tại)`; mặt hàng có hạn dùng phải nhập hạn dùng; mọi dòng `SLXUAT > 0`; gán kho xuất cho dòng.
- Có chọn đơn vị chẵn / kích thước / hạn dùng qua form `NhapSoLuong` (không cho nhập chiết khấu).

## 5. Chuyển kho (`TDONHANG3Ae`)

- Một phiếu `LOAI=3` có **hai nhóm dòng**: dòng nhập (`SLNHAP > 0`, `DKHOHANGID = kho nhập`) do người dùng nhập; và dòng xuất đối ứng (`SLXUAT = SLNHAP`, `DKHOHANGID = kho xuất`) được **sinh lại sau mỗi lần lưu** (xóa mọi dòng `SLXUAT > 0` của phiếu rồi tạo lại, sao chép đơn giá, % giảm, thành tiền, hạn dùng, kích thước, bảo hành). Khi mở phiếu chỉ nạp dòng `SLNHAP > 0`.
- Bắt buộc chọn kho xuất; kho nhập ≠ kho xuất; kho xuất `CHOPHEPAMKHO = 0` thì kiểm tồn (tổng `SLNHAPCHUAQUYDOI` của mặt hàng ≤ tồn — so sánh số lượng *chưa quy đổi* với tồn đơn vị cơ sở, có vẻ sai khi mặt hàng dùng đơn vị chẵn); mọi dòng `SLNHAP > 0`.
- Giá chuyển theo `GiaSuDungKhiChuyenXuatKho` như xuất khác. `TONGCONG = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN`.
- Có lỗi trong mã gốc khi cộng thêm số lượng đơn vị chẵn vào dòng đã có: `SLNHAP = SLNHAP × quyDoi` (đáng ra `SLNHAPCHUAQUYDOI × quyDoi`).

## 6. Kiểm kê (`TDONHANG4Ae`)

- Chọn kho trước (khóa không đổi khi đã có dòng); thêm mặt hàng với `SLTHUCTE` (nhập số lượng, hoặc chế độ "kiểm bằng máy không dây": mỗi lần quét + 1), `SLHETHONG = GetTonKhoEx(mặt hàng, kho, phiếu, kích thước, hạn dùng)` tại thời điểm thêm.
- Khi lưu: `SLXUATCHUAQUYDOI = max(0, SLHETHONG − SLTHUCTE)` (thiếu → xuất), `SLNHAPCHUAQUYDOI = max(0, SLTHUCTE − SLHETHONG)` (thừa → nhập). Dòng có chênh lệch tô vàng/đỏ. Nút "mặt hàng chưa kiểm kê" mở form `Phân tích kiểm kê`.
- Nhập từ máy kiểm kho: file `.txt` mỗi dòng `mã,số lượng` (cộng dồn theo mã), chỉ khi phiếu còn trống; tính sẵn `SLNHAP`/`SLXUAT` theo tồn hiện tại.
- Nhập Excel: cột bắt buộc Mã hàng / Tên hàng và "Số lượng thực tế".
- Danh sách mặt hàng: loại rỗng / kiêm vật tư / nguyên liệu.

## 7. Tồn kho ban đầu (`TonKhoBanDau`, `LOAI = 97`)

- Mỗi kho có tối đa một phiếu `LOAI=97` (`NAME = 'TONDAU'`, ngày chốt mặc định = 31/12 năm trước, `TONGCONG = TIENHANG = Σ thành tiền`, `TILETHUE = 0`).
- Danh sách = mọi mặt hàng (kiêm vật tư / nguyên liệu) đang dùng, gồm cả hàng chưa có dòng tồn đầu (SL 0). Nhập SL và giá vốn; `THANHTIEN = SL × ĐƠN GIÁ`. Lưu: SL = 0 và có dòng → xóa; SL > 0 → cập nhật hoặc thêm (`SLNHAP = SLNHAPCHUAQUYDOI = SL`, `TILEGIAMGIA = 0`, đơn vị cơ sở, kho). Sau đó gọi tính lại giá vốn. Nhập từ Excel: chọn cột Mã hàng (hoặc Tên hàng), Tồn, Giá vốn; xuất file mẫu.

## 8. Tính lại giá vốn (`TinhLaiGiaVon`) — bình quân cuối kỳ theo tháng

Chạy từ ngày nhỏ nhất đến lớn nhất có chứng từ (`MIN/MAX(TDONHANG.NGAY)`), lần lượt từng **tháng dương lịch có phát sinh**:
1. **Nguyên liệu và hàng kiêm vật tư** (`DLOAIMATHANGID` là Nguyên liệu hoặc Kiêm vật tư): tích lũy `ton` và `giaTri` qua các tháng. Trong tháng: `ton += Σ SLNHAP`, `giaTri += Σ THANHTIEN` của các dòng nhập (`LOAI = 1` nhập hàng hoặc `97` tồn đầu) có ngày trong tháng; `giaVon = ton = 0 ? 0 : Round(giaTri / ton, 2)`. Mọi dòng của mặt hàng trong tháng (nhập, xuất, chuyển, kiểm kê, bán…) được gán `GIAVON = giaVon` (nếu khác). Sau đó trừ các dòng xuất/kiểm kê khác nhập (`LOAI ≠ 1, ≠ 97`): `ton −= SLXUAT − SLNHAP`; `giaTri −= (SLXUAT − SLNHAP) × giaVon`.
2. **Mặt hàng định lượng / combo** (`DLOAIMATHANGID = '1'` hoặc `'4'`, bảng `DDINHLUONG`, tối đa 2 tầng định lượng): `giaVon = Round(Σ(giaVon vật tư × SOLUONG), 2)`; gán cho các dòng của mặt hàng trong tháng với `LOAI ≠ 1` và `≠ 97`.
3. Cuối cùng cập nhật `DMATHANG.GIAVON` = giá vốn của tháng cuối (chỉ khi khác giá cũ); định lượng lấy `giaVon` đã tính.

Lưu ý: tính thuần theo thời gian chứng từ (`NGAY`), kể cả bill bán chưa thanh toán; không phân biệt kho.
