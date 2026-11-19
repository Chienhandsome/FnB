# POS TAP — nghiệp vụ kho

Nguồn: SFORM `Phiếu nhập kho__24399cd6` (821 dòng), `Phiếu xuất kho__1f13b546` (552), `Phiếu chuyển kho__cc92bb29` (609), `Phiếu kiểm kê__93d9d7d4` (544), `Tồn kho__f0cc94d9` (303, chứa `TonKhoHandler`), `Tồn kho ban đầu__cb34a58c` (340), `Tính lại giá vốn__12ab9341` (512). Chưa đọc (vòng 1): `Phân tích kiểm kê`, `Quản lý nhập/xuất/chuyển kho/kiểm kê` (form danh sách, 28–35 dòng), `Lấy dữ liệu từ máy`, `Đánh lại mã hàng`, `Xuất lại vật tư`, `Hàng tồn hết hạn dùng`, `Nhập hạn sử dụng`, `Chọn kích thước nhập kho`, `Nhập dữ liệu`. **[Bổ sung v2]** Các form quản lý, `Xuất lại vật tư`, `Đánh lại mã hàng`, `Hàng tồn hết hạn dùng` đã được rà vòng 2 — xem mục 9 cuối file.

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
- Có chọn đơn vị chẵn / kích thước / hạn dùng qua form `NhapSoLuong` (không cho nhập chiết khấu; nhưng `TILEGIAMGIA` vẫn vào được qua import Excel).
- **[Bổ sung v2] `THANHTIEN` dòng phiếu xuất/chuyển không có công thức trong code**: `detail_OnRowCalculate` của `TDONHANG2Ae`/`TDONHANG3Ae` chỉ quy đổi số lượng; `mapper_OnCalculation` chỉ cộng `SUM(THANHTIEN)` có sẵn; AELAYOUT/SCOLUMN cũng không có công thức. Khả năng duy nhất: framework (GridMapper) tự tính theo cùng công thức phiếu nhập → **[Chưa chốt]** (checklist KHO-R3). Nếu framework không tự tính thì mọi phiếu xuất/chuyển có tổng = 0.
- **[Bổ sung v2]** Phiếu xuất **có** ô Nhà cung cấp (`lueDNHACUNGCAPID`, tự lưu vào `TDONHANG.DNHACUNGCAPID`; code form không đọc/ghi thêm) — đây là cơ chế "xuất trả NCC" mà báo cáo công nợ dùng (`LOAI = 2` + NCC); `TIENTHANHTOAN` là ô nhập tay, không tham gia công thức. Đơn giá dòng khi **import Excel** luôn là `GIABAN` gốc, không theo `GiaSuDungKhiChuyenXuatKho`.

## 5. Chuyển kho (`TDONHANG3Ae`)

- Một phiếu `LOAI=3` có **hai nhóm dòng**: dòng nhập (`SLNHAP > 0`, `DKHOHANGID = kho nhập`) do người dùng nhập; và dòng xuất đối ứng (`SLXUAT = SLNHAP`, `DKHOHANGID = kho xuất`) được **sinh lại sau mỗi lần lưu** (xóa mọi dòng `SLXUAT > 0` của phiếu rồi tạo lại, sao chép đơn giá, % giảm, thành tiền, hạn dùng, kích thước, bảo hành). Khi mở phiếu chỉ nạp dòng `SLNHAP > 0`.
- Bắt buộc chọn kho xuất; kho nhập ≠ kho xuất; kho xuất `CHOPHEPAMKHO = 0` thì kiểm tồn (tổng `SLNHAPCHUAQUYDOI` của mặt hàng ≤ tồn — so sánh số lượng *chưa quy đổi* với tồn đơn vị cơ sở, có vẻ sai khi mặt hàng dùng đơn vị chẵn); mọi dòng `SLNHAP > 0`.
- Giá chuyển theo `GiaSuDungKhiChuyenXuatKho` như xuất khác. `TONGCONG = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN`.
- Có lỗi trong mã gốc khi cộng thêm số lượng đơn vị chẵn vào dòng đã có: `SLNHAP = SLNHAP × quyDoi` (đáng ra `SLNHAPCHUAQUYDOI × quyDoi`). **[Sửa v2]** Kết luận "có lỗi" là **vội**: code sai thật (`Phiếu chuyển kho` dòng ~296–298, bản xuất kho viết đúng) nhưng ngay sau đó có `detail.CalculateRow(r)`; nếu framework tính lại `SLNHAP` từ `SLNHAPCHUAQUYDOI` qua `detail_OnRowCalculate` thì lỗi bị che. **[Chưa chốt]** (checklist KHO-R4).
- **[Bổ sung v2]** Các ô thuế/phí vận chuyển trên phiếu chuyển kho **hiển thị và nhập được thật** (AELAYOUT không `Visible=False`) → chuyển nội bộ vẫn tính thuế/phí như phiếu nhập; Chiến chốt giữ hay ép 0 như phiếu xuất. Form quản lý chuyển kho **không** lọc theo cửa hàng của user (3 form quản lý còn lại có lọc `TNGUOIDUNGTHEOCUAHANG`).
- **[Bổ sung v2]** Kiểm tồn dùng `SUM(SLNHAPCHUAQUYDOI)` so với tồn đơn vị cơ sở → sai khi dùng đơn vị chẵn (cho qua dù thiếu); phiếu xuất dùng `SUM(SLXUAT)` đã quy đổi nên đúng.

## 6. Kiểm kê (`TDONHANG4Ae`)

- Chọn kho trước (khóa không đổi khi đã có dòng); thêm mặt hàng với `SLTHUCTE` (nhập số lượng, hoặc chế độ "kiểm bằng máy không dây": mỗi lần quét + 1), `SLHETHONG = GetTonKhoEx(mặt hàng, kho, phiếu, kích thước, hạn dùng)` tại thời điểm thêm.
- Khi lưu: `SLXUATCHUAQUYDOI = max(0, SLHETHONG − SLTHUCTE)` (thiếu → xuất), `SLNHAPCHUAQUYDOI = max(0, SLTHUCTE − SLHETHONG)` (thừa → nhập). Dòng có chênh lệch tô vàng/đỏ. Nút "mặt hàng chưa kiểm kê" mở form `Phân tích kiểm kê`.
- Nhập từ máy kiểm kho: file `.txt` mỗi dòng `mã,số lượng` (cộng dồn theo mã), chỉ khi phiếu còn trống; tính sẵn `SLNHAP`/`SLXUAT` theo tồn hiện tại.
- Nhập Excel: cột bắt buộc Mã hàng / Tên hàng và "Số lượng thực tế".
- Danh sách mặt hàng: loại rỗng / kiêm vật tư / nguyên liệu.
- **[Bổ sung v2] Ba điểm vòng 1 của tài liệu này bỏ sót:** (1) phiếu kiểm kê **không có tiền** (không có `TIENHANG/TONGCONG/DONGIA`, `mapper_OnCalculation` không tính tiền); (2) **không có `detail_OnRowCalculate`** → khi `SuDung2DonViTinh = 30`, đường nhập tay/Excel chỉ ghi `SLNHAPCHUAQUYDOI`/`SLXUATCHUAQUYDOI`, **không ghi `SLNHAP`/`SLXUAT`** → `GetTonKho` không đổi, phiếu kiểm kê "thành công" mà tồn không đổi (chỉ đường nhập từ máy kiểm kho ghi cả hai cặp); `mapper_AfterSave` chỉ copy `...CHUAQUYDOI → SLNHAP/SLXUAT` khi `SuDung2DonViTinh = 0`; (3) phiếu kiểm kê **không** kích hoạt tính lại giá vốn (`UpdateGiaVon|TinhLaiGiaVon` = 0 kết quả trong code form). Chỉ phiếu nhập, tồn kho ban đầu và `Xuất lại vật tư` kích hoạt tính lại giá vốn.
- **[Bổ sung v2]** Ba đường vào có 3 cách xử lý đơn vị: nhập tay (`SLTHUCTE = numSoLuong.Value`, ngầm hiểu đơn vị lẻ; `SLHETHONG` từ `GetTonKhoEx` có size/HSD), Excel (`SLTHUCTE = số_excel × QUYDOI`; `SLHETHONG` từ `GetTonKho` không size/HSD), máy kiểm kho (ghi `SLNHAP/SLXUAT` bằng số *chưa* quy đổi — sai khi dùng đơn vị chẵn). `SLHETHONG` chốt lúc thêm dòng, `BeforeSave` không tính lại.

## 7. Tồn kho ban đầu (`TonKhoBanDau`, `LOAI = 97`)

- Mỗi kho có tối đa một phiếu `LOAI=97` (`NAME = 'TONDAU'`, ngày chốt mặc định = 31/12 năm trước, `TONGCONG = TIENHANG = Σ thành tiền`, `TILETHUE = 0`).
- Danh sách = mọi mặt hàng (kiêm vật tư / nguyên liệu) đang dùng, gồm cả hàng chưa có dòng tồn đầu (SL 0). Nhập SL và giá vốn; `THANHTIEN = SL × ĐƠN GIÁ`. Lưu: SL = 0 và có dòng → xóa; SL > 0 → cập nhật hoặc thêm (`SLNHAP = SLNHAPCHUAQUYDOI = SL`, `TILEGIAMGIA = 0`, đơn vị cơ sở, kho). Sau đó gọi tính lại giá vốn. Nhập từ Excel: chọn cột Mã hàng (hoặc Tên hàng), Tồn, Giá vốn; xuất file mẫu.

## 8. Tính lại giá vốn (`TinhLaiGiaVon`) — bình quân cuối kỳ theo tháng

Chạy từ ngày nhỏ nhất đến lớn nhất có chứng từ (`MIN/MAX(TDONHANG.NGAY)`), lần lượt từng **tháng dương lịch có phát sinh**:
1. **Nguyên liệu và hàng kiêm vật tư** (`DLOAIMATHANGID` là Nguyên liệu hoặc Kiêm vật tư): tích lũy `ton` và `giaTri` qua các tháng. Trong tháng: `ton += Σ SLNHAP`, `giaTri += Σ THANHTIEN` của các dòng nhập (`LOAI = 1` nhập hàng hoặc `97` tồn đầu) có ngày trong tháng; `giaVon = ton = 0 ? 0 : Round(giaTri / ton, 2)`. Mọi dòng của mặt hàng trong tháng (nhập, xuất, chuyển, kiểm kê, bán…) được gán `GIAVON = giaVon` (nếu khác). Sau đó trừ các dòng xuất/kiểm kê khác nhập (`LOAI ≠ 1, ≠ 97`): `ton −= SLXUAT − SLNHAP`; `giaTri −= (SLXUAT − SLNHAP) × giaVon`.
2. **Mặt hàng định lượng / combo** (`DLOAIMATHANGID = '1'` hoặc `'4'`, bảng `DDINHLUONG`, tối đa 2 tầng định lượng): `giaVon = Round(Σ(giaVon vật tư × SOLUONG), 2)`; gán cho các dòng của mặt hàng trong tháng với `LOAI ≠ 1` và `≠ 97`.
3. Cuối cùng cập nhật `DMATHANG.GIAVON` = giá vốn của tháng cuối (chỉ khi khác giá cũ); định lượng lấy `giaVon` đã tính.

Lưu ý: tính thuần theo thời gian chứng từ (`NGAY`), kể cả bill bán chưa thanh toán; không phân biệt kho.

## 9. Bổ sung vòng 2 (19/11/2026)

Nguồn: `deep/kho-hang.md` (đọc `DEMO.fdb`; AELAYOUT đầy đủ + truy vấn schema vật lý) đối chiếu với `POS_TAP_SRC_EXPORT`. Các mục 1–8 ở trên đã được đối chiếu từng khẳng định: đúng gần hết; điểm sai/thiếu đã sửa inline (dấu **[Sửa v2]**/**[Bổ sung v2]**).

### 9.1 Tồn kho — 4 công thức (không phải 3), F4 tự mâu thuẫn
- Chứng minh bằng SQL thật (vòng 1 chỉ nêu "3 công thức", vòng 2 xác nhận là **4**):
  - **F1** `TonKhoHandler.GetTonKho` (màn Tồn kho, kiểm tồn khi bán/xuất/chuyển, kiểm kê, HSD): `Σ(SLNHAP − SLXUAT)`, `((LOAI <> 0) OR (LOAI = 0 AND DATHANHTOAN = 30))`, **không lọc `STATUS`**, không lọc ngày (lũy kế toàn lịch sử), theo `(mặt hàng, kho)`. Chuẩn tham chiếu.
  - **F2** báo cáo XNT tổng hợp (`BaoCaoTongHopXuatNhapTon.AfterGetData`): như F1 **+ `TDONHANG.STATUS = 30`** + giới hạn ngày (`TONDAU: NGAY < @TUNGAY`; trong kỳ `BETWEEN`) + `LOAI IS NULL OR LOAI <> 0` (NULL được tính, F1 loại NULL). Chênh với F1: phiếu xóa mềm (`STATUS = −30`) + phiếu chưa thanh toán ngoài kỳ.
  - **F3** báo cáo XNT chi tiết: **không lọc `STATUS`, không lọc `DATHANHTOAN`, không lọc ngày trong SQL**; tách cột theo `LOAI` (tồn đầu 97, nhập mua 1, nhập điều chỉnh 4, nhập chuyển 3, xuất bán `0 + XUATVATTU=0`, xuất vật tư `0 + XUATVATTU=30`, điều chỉnh 4, chuyển 3, xuất khác 2); chỉ lọc `DMATHANG.STATUS = 30`. Bill bán chưa thanh toán và phiếu xóa mềm vẫn tính.
  - **F4** Thẻ kho (`TheKho.GetNhapXuatTon`): tồn đầu kỳ như F1 (**không** lọc `STATUS`); chi tiết trong kỳ **có** lọc `STATUS = 30` → **tự mâu thuẫn trong một báo cáo**, số dư chạy lệch nếu có phiếu `STATUS = −30`.
  - **Đề xuất chuẩn duy nhất cho clone: F2** (`STATUS = 30` + `DATHANHTOAN = 30` cho bill bán) — Chiến chốt. Cần xác minh phiếu kho có bao giờ mang `STATUS = −30` không (xóa cứng hay mềm do framework — checklist KHO-R2).
- Nhắc tồn tối thiểu (`TonKhoHandler.Reminder`) là `static bool = false` và không nơi nào trong code đặt `= true` → màn nhắc không kích hoạt từ 112 form (chỉ còn khả năng `Application.exe`). Tab "tồn theo kích thước/HSD" bị bỏ khi menu là "Tồn nhiều kho" (`TatCaCacKho = true`).

### 9.2 Tính lại giá vốn — bổ sung
- Chạy toàn bộ mỗi lần (`UpdateGiaVon(whereIds)` bỏ qua tham số), **không dùng transaction** (mọi cập nhật là `Row.Update()` lẻ; lỗi giữa chừng để dữ liệu dở dang; lỗi hiện `Msg.ShowWarning` kể cả khi `Hidden`).
- Tồn kho ≠ giá vốn trên cùng một bill chưa thanh toán: tính giá vốn trừ **cả bill bán chưa thanh toán** (SQL không lọc `DATHANHTOAN`, không lọc `XUATVATTU`), `GetTonKho` thì không. Tồn âm (`TON < 0`) không có nhánh xử lý → `GIAVON` có thể âm/vô nghĩa và mang sang tháng sau.
- Mặt hàng loại `'3'/'5'/'6'/'7'` có giao dịch không thuộc bước nguyên liệu lẫn bước định lượng → cột `GIAVON` (thêm bằng `Columns.Add` không `DefaultValue`) giữ `DBNull` → `0` → `CapNhatGiaVon` **ghi đè `DMATHANG.GIAVON` về 0** nếu khác 0. Vật tư không có trong `dicMatHang` (chưa từng giao dịch) cộng 0 lặng lẽ.
- Độ sâu bung định lượng không thống nhất: giá vốn tháng 2 cấp; `DMATHANG.GIAVON` (cập nhật khi lưu vật tư) 1 cấp; trừ kho khi bán lẻ **đệ quy không giới hạn** (công thức vòng tròn A⊃B⊃A sẽ treo lúc thanh toán); nhà hàng 1 cấp + `InsertVatTu`.

### 9.3 Xuất lại vật tư, Đánh lại mã, Tồn đầu, HSD
- **Xuất lại vật tư**: mật khẩu = `PASSWORD` của `SUSER` có `ISADMIN = 1` (không `ORDER BY` — nhiều admin thì lấy dòng đầu không xác định) + `"6666"`. Nếu bỏ trống cả hai ngày: DELETE xóa `XUATVATTU = 30` **toàn lịch sử** nhưng SELECT sinh lại luôn lọc `NGAY BETWEEN @FromDate AND @ToDate` (không có nhánh null) → có thể xóa sạch mà không sinh lại dòng nào (**[Chưa chốt]** phụ thuộc `TuNgay/DenNgay` khi trống — checklist KHO-R5). SELECT sinh lại `INNER JOIN DBAN` (loại phiếu kho) và không lọc `DATHANHTOAN` → sinh lại cả bill chưa thanh toán; xong chạy `frmTinhLai.ThucHien()`.
- **Đánh lại mã hàng**: mật khẩu cứng `"4321"`, không kiểm quyền, không sao lưu/hoàn tác; mã nhóm con `10, 11, 12…` theo từng `PARENTID` (hard-code 10); `chkMaNhom` mặc định không tick; `dicInt` là code chết nên số thứ tự luôn bắt đầu lại từ 1 mỗi nhóm; chỉ `UPDATE DNHOMMATHANG.CODE` + `DMATHANG.CODE`.
- **Tồn đầu**: số lượng âm rơi qua cả hai nhánh lưu (không ghi, giữ giá trị cũ, không báo lỗi) nhưng vẫn kích hoạt tính lại giá vốn; import Excel không chặn âm (lỗi parse → 0). Dòng tồn đầu không có HSD/kích thước.
- **Hàng tồn hết hạn**: gom theo mặt hàng + HSD, `HAVING SUM > 0`, **không lọc kho**; sắp hết hạn dùng `ThongBaoTruocNgay` (DEMO = 0); không có caller trong 112 form/`SMENU`, không có nút in/xuất (khả năng caller ở `Application.exe`).
- Không có kiểm tra ngày (tương lai/quá khứ) nào trong 4 phiếu kho; chỉ quyền `ThayDoiNgayTrongCacPhieu` khóa `dtNGAY`. Không có trạng thái duyệt/khóa phiếu; cơ chế xóa (cứng/mềm) và số phiếu `NAME` do framework (**[Chưa chốt]** — checklist KHO-R1, KHO-R2).

### 9.4 Schema: chỗ DEMO khác POS TAP thật
- `DMATHANG.LOAIDINHLUONG` **không có** trong schema DEMO/TEMPLATE nhưng code bán lẻ dùng tới (2 chỗ: SQL `XuatVatTu` `WHERE (B.LOAIDINHLUONG = 1 OR B.LOAIDINHLUONG = 2)` và `grMatHang_CustomLoadData` `COALESCE(LOAIDINHLUONG,0) <> NguyenLieu`). Đã kiểm lại trên `POS_TAP_SRC_EXPORT`: đúng 4 hit trong `Hóa đơn bán hàng` + `Quản lý bán hàng`. `XuatVatTu` bán lẻ chạy không try/catch → cột **phải tồn tại trong `DATA.fdb` POS TAP thật** → DEMO khác POS TAP thật ở ít nhất cột này (checklist KHO-R6; tài liệu bán lẻ mục 1.7 vẫn đúng theo POS TAP thật). `TDONHANGCHITIET` không có cột `LOAI` (`LOAI` trong SQL giá vốn là `TDONHANG.LOAI`).
- 5 mức giá `DGIAMATHANG` (seed): `'0'` "Hiển thị để lựa chọn", `'1'` "Giá chung", `'2'` "Giá bán t2", `'3'` "Giá bán t3", `'4'` "Giá bán cafe" → `GIABAN`/`GIABAN2/3/4`. Dữ liệu seed DEMO — POS TAP thật có thể khác.

### 9.5 Quyền kho / form quản lý
- 4 form quản lý (`TDONHANG1..4MgHandler`): cột lưới giải mã từ AELAYOUT; lọc phạm vi user có ở nhập/xuất/kiểm kê, **không có ở chuyển kho** (chỉ `AND SLNHAP > 0` để ẩn dòng mirror); không có cột trạng thái. Trường bắt buộc mức layout: nhập `NAME, NGAY, DKHONHAPID`; xuất `NAME, NGAY, DKHOXUATID, DIENGIAI`; chuyển `NAME, NGAY, DKHONHAPID` (+ `DKHOXUATID` bắt buộc trong code); kiểm kê chỉ `NAME`.
- Quyền (`SGROUPROLE`, MODE 0/16/80/112/240): ngữ nghĩa bit nằm trong `No1Lib`.
