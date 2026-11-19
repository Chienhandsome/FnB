# POS TAP — nghiệp vụ báo cáo và thống kê

Nguồn: `SREPORT/*` (36 báo cáo: `query.sql` + `code.cs`/`servercode.cs` điều chỉnh dữ liệu sau khi truy vấn) và SFORM `Thống kê__1f516fe7`, `Thống kê doanh thu__d913c8d9`, `Thống kê mặt hàng bán__67500401`. Mẫu in/bố cục (FastReport) nằm trong `layout.xml`, chưa đọc.

## 0. Điều kiện lọc chuẩn của các báo cáo bán hàng

- Chỉ tính bill bán hàng đã thanh toán: `TDONHANG.LOAI = 0 AND DATHANHTOAN = 30` (một số báo cáo thêm `STATUS = 30`), theo `NGAY BETWEEN @FromDate AND @ToDate`.
- Dòng chi tiết bán: loại các dòng xuất vật tư (`COALESCE(XUATVATTU,0) = 0`) và dòng con của combo (`COALESCE(COMBOPARENTID,'') = ''`); loại mặt hàng nguyên liệu (`DLOAIMATHANGID = '2'`) ở các thống kê trên form.
- Số lượng bán = `SLXUAT − SLNHAP` (hàng trả trừ đi); doanh thu dòng = `THANHTIEN` (đã trừ chiết khấu dòng).
- Tồn kho: xem công thức ở [file kho](pos-tap-nghiep-vu-kho.md): chứng từ `LOAI <> 0` hoặc bill bán đã thanh toán; kho lọc theo `DKHOHANGID` (rỗng = mọi kho).
- Phân quyền theo cửa hàng: người dùng không phải admin chỉ thấy các cửa hàng trong `TNGUOIDUNGTHEOCUAHANG`. Không có quyền `XemGiaNhap` thì ẩn giá nhập/vốn/lãi.

## 1. Thống kê trong ngày trên form

- **`Thống kê`** (của một thu ngân trong ngày, mở từ F9/F10): lọc theo `NGAY = ngày chọn` và `USERTHANHTOANID = user` (xem được tài khoản khác nếu có quyền `XemThongKeCuaCacCaiKhoanKhac`). Tổng hợp từ các bill và phiếu thu chi trong ngày: tiền mặt, chuyển khoản, tiền thẻ, voucher, thẻ trả trước, trừ tích lũy, `CONGNO`, `TIENTHANHTOAN`, tổng thu, tổng chi. **Tổng doanh số hiển thị = tiền mặt + tiền thẻ + chuyển khoản + tổng thu − tổng chi** (tức tiền thực nhận, không phải `TONGCONG`); "thu chi" = tổng thu − tổng chi. Không có quyền `ThongKeTrongSuDungDichVu` thì các số tiền hiển thị 0 và ẩn tab thu chi / mặt hàng / in báo cáo. Phiếu thu chi lọc theo `USERCREATEDID`.
- **`Thống kê doanh thu`** (theo khoảng ngày và cửa hàng, quản lý): dựa trên danh sách bill đã thanh toán:
  - Tiền hàng = `Σ TIENHANGCHUAGIAM`; tiền giờ `Σ TIENGIO`; giảm giá hàng = `Σ TIENGIAMGIA + Σ GIAMGIAMATHANG`; giảm giá giờ `Σ TIENGIAMGIAGIO`; tổng giảm = hai cái cộng lại; phí dịch vụ `Σ PHIDICHVU`; thuế `Σ TIENTHUE`; tổng cộng `Σ TONGCONG`; theo hình thức thanh toán `TIENMAT`, `THE`, `CHUYENKHOAN`, `VOUCHER`, `TRUTICHLUY`, `THETRATRUOC`, `CONGNO`; thu `Σ THU` và chi `Σ CHI` của `TTHUCHI` trong khoảng ngày; **Cộng = tiền mặt + tiền thẻ + chuyển khoản + thu − chi**.
  - Chi tiết dưới lưới: dòng của bill đang chọn (không vật tư, không combo con, không nguyên liệu).
- **`Thống kê mặt hàng bán`** (lãi theo mặt hàng): theo khoảng ngày + cửa hàng + nhóm hàng; chọn cách tính vốn: giá vốn dòng (`TDONHANGCHITIET.GIAVON`, mặc định) hoặc giá nhập (`DMATHANG.GIANHAP`). Mỗi dòng: `LAI = THANHTIEN − THANHTIENNHAP`, `TILELAI = 100 × LAI / THANHTIEN` (0 nếu THANHTIEN = 0). Tổng: **tiền bán = Σ(SLXUATCHUAQUYDOI × DONGIA)** (trước chiết khấu), tiền nhập = Σ THANHTIENNHAP, giảm giá = `Σ(TIENGIAMGIATONG + GIAMGIAMATHANG)` của bill trong khoảng, `tongLai = tiền bán − tiền nhập`, **thực lãi = tongLai − tổng giảm giá + thuế + phí dịch vụ + tiền giờ** (công thức theo mã gốc). Có hiển thị thuế, phí dịch vụ, tiền giờ. Karaoke: thêm tab tổng hợp "dịch vụ theo giờ" (`DLOAIMATHANGID='7'`): số lần `COUNT(TUGIO)`, tổng chi phí `Σ 1000 × ROUND(GIANHAP × 24 × (DENGIO hoặc KETTHUC − GIOTINHLUONG hoặc TUGIO) / 1000)` (chênh lệch ngày × 24 = giờ), chi tiết từng lần và cho chỉnh `GIOTINHLUONG` (quyền `DieuChinhGioTinhLuongDichVuTheoGio`).

## 2. Báo cáo doanh thu / bán hàng

| Báo cáo | Nội dung chính |
|---|---|
| Biểu đồ doanh số theo nhóm | `Σ THANHTIEN` theo nhóm mặt hàng (bill `LOAI=0` đã thanh toán), bỏ nhóm bằng 0 |
| Biểu đồ theo nhân viên kinh doanh | `Σ TONGCONG` theo `DNHANVIENXUATID` |
| Biểu đồ doanh thu ngày trong tháng | ma trận 31 ngày × 12 tháng của một năm: `Σ TONGCONG` bill `LOAI IN (0,5,6)` đã thanh toán |
| Biểu đồ doanh thu tháng trong năm | `Σ TONGCONG` bill `LOAI=0` theo tháng, năm chọn và năm trước. **Mã gốc chỉ chạy tháng 1–11 (thiếu tháng 12) và đặt nhãn "năm nay" / "năm ngoái" đảo ngược** |
| 20 mặt hàng bán chạy nhất | `FIRST 20` theo `Σ(SLXUAT − SLNHAP)` giảm dần, lọc nhóm hàng (tuỳ chọn), mặt hàng `STATUS=30` |
| Bán hàng theo giờ | nhóm theo `EXTRACT(HOUR FROM BATDAU)`: số hóa đơn, `Σ TONGCONG` |
| Chi tiết bán hàng theo ngày | bill (`TONGCONG`, giảm giá tổng, `TIENHANG`, `TIENTHANHTOAN`, `TIENGIO`, phí dịch vụ, thuế, nợ) + dòng chi tiết |
| Chi tiết bán hàng theo thu ngân | dòng bán theo `USERTHANHTOANID` (tuỳ chọn), sắp theo `GIOTHANHTOAN` |
| Chi tiết bán hàng theo hóa đơn | dòng bán kèm số phiếu, chiết khấu đơn, nhân viên, thu ngân, tổng toàn kỳ |
| Chi tiết hoạt động trong ngày | 4 bảng: bill thanh toán (tiền hàng, giảm, thuế, phí vận chuyển, tổng, mặt/CK/đặt trước/thẻ/voucher/thẻ trả trước/trừ tích lũy/còn nợ), tổng hợp mặt hàng (SL, đơn giá, % giảm, thành tiền), phiếu thu chi (TM/CK), phiếu nhập kho `LOAI=1` (nhà cung cấp, tổng cộng, đã trả, còn nợ nếu `LOAITHANHTOAN < TONGCONG`); tham số thêm: tổng giảm giá `Σ TIENGIAMGIA`; lọc thu ngân và cửa hàng |
| Tổng hợp doanh thu theo loại đồ | theo ngày: `Σ THANHTIEN` chia theo `DNHOMMATHANG.DLOAIDOID` 0 ăn / 1 uống / 2 dịch vụ / 3 khác |
| Tổng hợp giá trị bán / số lượng bán theo tháng | lưới nhóm × mặt hàng × 31 cột ngày của tháng; giá trị chia 1000 (nghìn đồng), số lượng `SLXUAT − SLNHAP` |
| Phân tích tình hình bán hàng | 3 bảng: theo (mặt hàng, đơn giá), theo mặt hàng, theo đơn giá; tham số: tồn cuối kỳ (`Σ(SLNHAP − SLXUAT)` mọi chứng từ ngày ≤ ngày cuối — gồm cả bill chưa thanh toán), tổng SL bán, số ngày (`ngày cuối − ngày đầu + 1`), bình quân/ngày `= Round(tổng SL / số ngày, 2)`; cột `TILE = Round(100 × SL / tổng SL, 2)` |
| Cấu hình bàn khu vực | từng bàn `STATUS=30`: khu vực, cách tính giờ (nếu theo khu vực thì lấy cấu hình khu vực; nếu cũng theo khu vực thì coi theo giờ), đơn giá "…/giờ" hoặc tên bảng giá, thêm "Bảng giá khác" khi `ChoPhepDungNhieuBangGiaTrenBill=30`; kèm bảng chi tiết `DBANGGIACHITIET` (Ngày thường / Cuối tuần / Ngày lễ) |
| Chi tiết phân quyền hệ thống | quyền theo nhóm người dùng (`SGROUPROLE`, `MODE ≠ 0`) và (tuỳ chọn) quyền xem báo cáo (`SREPORTROLE`) |
| Công thức định lượng | danh sách (mặt hàng, vật tư, số lượng, đơn vị) từ `DDINHLUONG`, lọc theo nhóm hàng |

## 3. Báo cáo lãi gộp

Bốn báo cáo: Chi tiết lãi theo hóa đơn / Tổng hợp lãi gộp theo mặt hàng, mỗi loại có bản **giá nhập** và **giá vốn**. Với mỗi dòng (nhóm theo đơn giá, mặt hàng, tên hóa đơn hoặc không):
```
SOLUONG   = Σ SLXUATCHUAQUYDOI            DOITRA = Σ SLNHAPCHUAQUYDOI
DOANHTHU  = Σ (SLXUATCHUAQUYDOI − SLNHAPCHUAQUYDOI) × DONGIA
GIAMGIA   = Σ (…) × DONGIA × TILEGIAMGIA / 100
THUCTHU   = Σ (…) × DONGIA × (100 − TILEGIAMGIA) / 100
GIATRIVON = Σ (…) × giáVốnĐơnVị
LOINHUAN  = Σ (…) × (DONGIA × (100 − TILEGIAMGIA)/100 − giáVốnĐơnVị)
giáVốnĐơnVị (bản chi tiết theo HĐ) = GIANHAP hoặc COALESCE(GIAVON,0), nhân QUYDOI nếu đơn vị dòng là đơn vị chẵn
giáVốnĐơnVị (bản tổng hợp theo mặt hàng, giá nhập) = GIANHAP / (QUYDOI nếu có đơn vị chẵn, ngược lại 1)
```
Báo cáo chi tiết theo hóa đơn thêm một dòng cuối "GIẢM GIÁ TỔNG BILL" = `− Σ TIENGIAMGIA` của mọi bill trong khoảng ngày (lưu ý: lấy theo `NGAY` mà **không** lọc `LOAI` hay `DATHANHTOAN`). Lưu ý: bản giá nhập chi tiết theo HĐ **nhân** `QUYDOI` còn bản tổng hợp **chia** `QUYDOI` (`GIANHAP` ở hai nơi hiểu theo hai đơn vị khác nhau — cần đối chiếu khi viết lại). Dùng `SLXUATCHUAQUYDOI`/`SLNHAPCHUAQUYDOI` (số lượng theo đơn vị bán) và giá theo đơn vị bán.

## 4. Báo cáo kết quả kinh doanh (so sánh với kỳ trước một tháng)

Tham số: khoảng ngày, cửa hàng (tuỳ chọn). "Tháng trước" = cùng khoảng lùi 1 tháng (`DATEADD(MONTH, -1, …)`). Bill tính: `(LOAI=0 AND DATHANHTOAN=30) OR LOAI=5 OR LOAI=6` (hai loại phiếu 5 và 6 là loại phiếu thu bán hàng chưa rõ nguồn gốc — cần tìm khi đọc phần còn lại của hệ thống). Các dòng:
- **DOANH THU** = `Σ TONGCONG`; bên dưới: doanh thu theo nhóm mặt hàng (`Σ THANHTIEN`, bill `LOAI=0` đã thanh toán, nhóm rỗng ghi "(Chưa phân nhóm)"), Tiền giờ `Σ TIENGIO`, Giảm giá `Σ TIENGIAMGIATONG`, Phí dịch vụ `Σ PHIDICHVU`, Thuế `Σ TIENTHUE`.
- **Định phí** = Σ `CHI` của `TTHUCHI` theo lý do `DLYDOTHUCHI.LOAILYDO = 1`, liệt kê từng lý do. **Biến phí** = `Σ (SLXUAT − SLNHAP) × GIAVON` của dòng bán (không vật tư, không combo con) theo nhóm mặt hàng. **Chi khác** = Σ `CHI` theo lý do `LOAILYDO = 2`. **CHI PHÍ** = định phí + biến phí + chi khác.
- **LÃI LỖ** = DOANH THU − CHI PHÍ (mã gốc chỉ trừ ba loại chi phí; doanh thu ở đây là `TONGCONG` đã gồm thuế và phí dịch vụ, trước khi trừ giá vốn). Cột: tháng này, tỉ lệ, tăng/giảm (`tháng này − tháng trước`), tháng trước, biểu tượng ▲/▼.

## 5. Báo cáo kho

| Báo cáo | Công thức |
|---|---|
| Hàng tồn kho | theo mặt hàng `STATUS=30`, loại `0` hoặc `2` (hàng kiêm vật tư và nguyên liệu), nhóm / kho tùy chọn: `TON = Σ(SLNHAP − SLXUAT)` chứng từ `NGAY < ngày cuối + 1`, `GIATRIVON = GIAVON × TON`, `GIATRIBAN = GIABAN × TON`; chỉ in `TON ≠ 0`; kèm `TONTOITHIEU`, `TONTOIDA` |
| Hàng tồn kho có hạn dùng | `COHANSUDUNG=30`: tồn theo từng `HANSUDUNG` (`≠ 0`, hạn dùng không rỗng) |
| Hàng đã hết hạn | tồn `≠ 0` với `HANSUDUNG <= ngày cuối + 1` |
| Theo hạn dùng | tồn `≠ 0` có `HANSUDUNG` trong [từ ngày, đến ngày] |
| Thẻ kho (một mặt hàng) | tồn đầu kỳ = `Σ(SLNHAP − SLXUAT)` chứng từ `NGAY < từ ngày` (cùng điều kiện kho / trạng thái); sau đó liệt kê từng dòng trong kỳ sắp theo `NGAY, TIMECREATED` với diễn giải = "Xuất vật tư" (bán + `XUATVATTU=30`), "Bán hàng" (bán thường) hoặc `DIENGIAI` chứng từ; số tồn lũy kế; tồn cuối kỳ. Danh sách mặt hàng cho chọn: loại `0` hoặc `2` |
| Tổng hợp xuất nhập tồn | mỗi mặt hàng (loại `0`/`2`, `STATUS=30`, nhóm / kho tùy chọn): tồn đầu = `Σ(SLNHAP − SLXUAT)` ngày `< từ ngày`; nhập `Σ SLNHAP` và xuất `Σ SLXUAT` trong kỳ; `Ton = tồn đầu + nhập − xuất`; giá vốn = `GIAVON` của dòng mới nhất có giá vốn > 0 có `NGAY ≤ ngày cuối` (0 nếu `Ton = 0`); `GTTONDAU = TONDAU × GIAVON`, `GTNHAP`, `GTXUAT`, `GiaTriTon = GTTONDAU + GTNHAP − GTXUAT`; chỉ in dòng có phát sinh hoặc còn tồn |
| Tổng hợp xuất nhập tồn chi tiết | theo mặt hàng (`DLOAIMATHANGID '0'`/`'2'`, `STATUS=30`), nhập tách: tồn đầu (`LOAI=97`), nhập mua (`LOAI=1`), nhập điều chỉnh (`LOAI=4`), nhập chuyển kho (`LOAI=3`); xuất tách: bán (`LOAI=0`, `XUATVATTU=0`), xuất vật tư (`LOAI=0`, `XUATVATTU=30`), điều chỉnh (`LOAI=4`), chuyển kho (`LOAI=3`), xuất khác (`LOAI=2`); tồn kho = tổng tất cả. Lưu ý: bản này **không** lọc bill chưa thanh toán và không lọc `STATUS` chứng từ |

## 6. Báo cáo quỹ

`Báo cáo tồn quỹ` (tham số: loại quỹ `'' tất cả / 0 tiền mặt / 1 ngân hàng / 2 thẻ`, tài khoản ngân hàng):
- Dòng trong kỳ (`query.sql`): phiếu thu chi `TTHUCHI` (`THU`, `CHI`) theo loại quỹ (`0`: `CHUYENKHOAN = 0`; `1`: `CHUYENKHOAN = 30` và đúng tài khoản; `2`: không có phiếu thu chi); và bill `TDONHANG`: bán hàng đã thanh toán → thu = `TIENMAT` (quỹ tất cả / tiền mặt) `+ CHUYENKHOAN` (tất cả / ngân hàng) `+ THE` (tất cả / thẻ); phiếu `LOAI = 2` có nhà cung cấp (nhập trả / xuất trả) → `TIENTHANHTOAN` là thu; phiếu `LOAI = 1` (nhập hàng) → `TIENTHANHTOAN` là chi.
- Tồn đầu kỳ = (thu − chi của `TTHUCHI` trước kỳ) + (bán hàng đã thanh toán trước kỳ theo loại quỹ: `TIENTHANHTOAN` / `TIENMAT` / `THE` / `CHUYENKHOAN`, trừ `TIENTHANHTOAN` của phiếu nhập `LOAI=1` hoặc `LOAI=2` có NCC), cộng dồn `TON` từng dòng = tồn trước + thu − chi.

## 7. Báo cáo công nợ

- **Công nợ khách hàng** (theo kỳ): với mỗi khách `STATUS=30` (lọc nhóm khách): `MUADAUKY = Σ(TONGCONG − TIENTHANHTOAN)` các bill `LOAI IN (0, 99)` đã thanh toán trước kỳ (99 = công nợ đầu); `THANHTOANDAUKY = Σ(THU − CHI)` phiếu `TTHUCHI` không đánh `KHONGTHAYDOICONGNO` trước kỳ; `NODAU = MUADAUKY − THANHTOANDAUKY`; trong kỳ `MUA = Σ TONGCONG`, `THANHTOAN = Σ TIENTHANHTOAN(bill) + Σ(THU − CHI)(phiếu)`; `NOCUOI = NODAU + MUA − THANHTOAN`; ẩn khách mọi số bằng 0. Bấm tên khách mở **Đối chiếu công nợ**.
- **Đối chiếu công nợ khách hàng**: `NỢ ĐẦU KỲ = Σ(TONGCONG − TIENTHANHTOAN)` bill (`LOAI = 0` đã thanh toán hoặc `LOAI ≠ 0`) ngày `< từ ngày` − `Σ(THU − CHI)` phiếu trước kỳ; `PHÁT SINH MUA = Σ TONGCONG` bill bán trong kỳ; `NỢ CUỐI KỲ` tính tương tự đến hết ngày cuối; `THANH TOÁN TRONG KỲ = nợ đầu + phát sinh − nợ cuối` (số suy ngược). Danh sách bill/phiếu trong kỳ do `query.sql`.
- **Tổng hợp công nợ khách hàng**: `Σ(TONGCONG − TIENTHANHTOAN)` bill `LOAI IN (0, 99)` đã thanh toán + `Σ(CHI − THU)` phiếu thu chi (không đánh `KHONGTHAYDOICONGNO`) — **dấu của phiếu thu chi ở đây ngược với báo cáo theo kỳ** (`CHI − THU`; báo cáo theo kỳ dùng `THU − CHI`); khách có tổng khác 0. Cần xác nhận quy ước dấu khi viết lại.
- **Công nợ nhà cung cấp**: tương tự nhưng nguồn là bill `LOAI IN (1 nhập hàng, 2 xuất trả, 98 công nợ đầu)` (phiếu `LOAI=2` đảo dấu cả `TONGCONG` và `TIENTHANHTOAN`) và phiếu thu chi theo `DNHACUNGCAPID` với `THANHTOAN = Σ(CHI − THU)`. Đối chiếu công nợ NCC: nợ đầu kỳ = `Σ ± (TONGCONG − TIENTHANHTOAN)` (≤ trước kỳ; không lọc `DATHANHTOAN`) − `Σ(CHI − THU)` phiếu; các dòng còn lại như khách.
- Báo cáo phụ trợ: nhãn "Tổng cộng" các cột nợ đầu / mua / thanh toán / nợ cuối.
