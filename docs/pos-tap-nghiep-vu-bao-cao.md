# POS TAP — nghiệp vụ báo cáo và thống kê

Nguồn: `SREPORT/*` (36 báo cáo: `query.sql` + `code.cs`/`servercode.cs` điều chỉnh dữ liệu sau khi truy vấn) và SFORM `Thống kê__1f516fe7`, `Thống kê doanh thu__d913c8d9`, `Thống kê mặt hàng bán__67500401`. Mẫu in/bố cục (FastReport) ~~nằm trong `layout.xml`, chưa đọc~~ **[Sửa v2]** nằm trong bảng `SREPORTTEMPLATE` của DB (manifest 156 dòng: 36 file `.frx` + `.config`; bản export `POS_TAP_SRC_EXPORT` chỉ có `query.sql`/`code.cs` của 36 báo cáo SQL, không có `.frx`/`.config`). Vòng 2 đã đọc `.frx`/`.config` từ DEMO: xem mục 8. **Lưu ý nguồn:** 36 báo cáo SQL ở đây (POS TAP thật) trùng với 36 báo cáo SQL của DEMO; toàn bộ danh sách 127 báo cáo (gồm 73 báo cáo tự sinh) chỉ có trong DB, chưa đối chiếu được với DEMO (128).

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
| Biểu đồ doanh thu ngày trong tháng | ma trận 31 ngày × 12 tháng của một năm: `Σ TONGCONG` bill `LOAI IN (0,5,6)` đã thanh toán. **[Bổ sung v2 — bug]** code chạy đúng 31 query `THANG1..12` nhưng không bao giờ gán cột `NGAY`, trong khi mẫu in bind trục X = `[Table0.NGAY]` → trục X không hiển thị đúng ngày 1–31 (đối chiếu từ `.frx` của DEMO; chưa xem biểu đồ in ra trên app gốc) |
| Biểu đồ doanh thu tháng trong năm | `Σ TONGCONG` bill `LOAI=0` theo tháng, năm chọn và năm trước. **Mã gốc chỉ chạy tháng 1–11 (thiếu tháng 12) và đặt nhãn "năm nay" / "năm ngoái" đảo ngược** |
| 20 mặt hàng bán chạy nhất | `FIRST 20` theo `Σ(SLXUAT − SLNHAP)` giảm dần, lọc nhóm hàng (tuỳ chọn), mặt hàng `STATUS=30` |
| Bán hàng theo giờ | nhóm theo `EXTRACT(HOUR FROM BATDAU)`: số hóa đơn, `Σ TONGCONG` |
| Chi tiết bán hàng theo ngày | bill (`TONGCONG`, giảm giá tổng, `TIENHANG`, `TIENTHANHTOAN`, `TIENGIO`, phí dịch vụ, thuế, nợ) + dòng chi tiết |
| Chi tiết bán hàng theo thu ngân | dòng bán theo `USERTHANHTOANID` (tuỳ chọn), sắp theo `GIOTHANHTOAN` |
| Chi tiết bán hàng theo hóa đơn | dòng bán kèm số phiếu, chiết khấu đơn, nhân viên, thu ngân, tổng toàn kỳ |
| Chi tiết hoạt động trong ngày | 4 bảng: bill thanh toán (tiền hàng, giảm, thuế, phí vận chuyển, tổng, mặt/CK/đặt trước/thẻ/voucher/thẻ trả trước/trừ tích lũy/còn nợ), tổng hợp mặt hàng (SL, đơn giá, % giảm, thành tiền), phiếu thu chi (TM/CK), phiếu nhập kho `LOAI=1` (nhà cung cấp, tổng cộng, đã trả, còn nợ nếu `LOAITHANHTOAN < TONGCONG`); tham số thêm: tổng giảm giá `Σ TIENGIAMGIA`; lọc thu ngân và cửa hàng |
| Tổng hợp doanh thu theo loại đồ | theo ngày: `Σ THANHTIEN` chia theo `DNHOMMATHANG.DLOAIDOID` 0 ăn / 1 uống / 2 dịch vụ / 3 khác |
| Tổng hợp giá trị bán / số lượng bán theo tháng | lưới nhóm × mặt hàng × 31 cột ngày của tháng; giá trị chia 1000 (nghìn đồng; `THANHTIEN` là NUMERIC(18,2) nên không cắt lẻ kiểu chia nguyên), số lượng `SLXUAT − SLNHAP`. **[Bổ sung v2 — bug mẫu in]** `.frx` của cả hai báo cáo giống hệt nhau: Total `T18` cộng `[D19]`, `T19` cộng `[D20]`, `T24` cộng `[D25]` (đáng lẽ D18/D19/D24); biểu thức tổng cộng cộng `[T19]` hai lần → tổng in ra = tổng đúng + 2×D20 + D25 − D18 − D24 |
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
giáVốnĐơnVị (bản chi tiết theo HĐ, cả giá nhập lẫn giá vốn) = GIANHAP hoặc COALESCE(GIAVON,0), nhân QUYDOI nếu đơn vị dòng là đơn vị chẵn (DDONVITINHID = DDONVITINHCHANID)
giáVốnĐơnVị (bản tổng hợp theo mặt hàng, GIÁ VỐN) = GIAVON × (QUYDOI nếu đơn vị dòng là đơn vị chẵn, ngược lại 1)   ← nhân, giống bản chi tiết
giáVốnĐơnVị (bản tổng hợp theo mặt hàng, GIÁ NHẬP) = GIANHAP / (QUYDOI nếu mặt hàng CÓ đơn vị chẵn, ngược lại 1)   ← chia, điều kiện khác
```
Báo cáo chi tiết theo hóa đơn thêm một dòng cuối "GIẢM GIÁ TỔNG BILL" = `− Σ TIENGIAMGIA` của mọi bill trong khoảng ngày (lưu ý: lấy theo `NGAY` mà **không** lọc `LOAI` hay `DATHANHTOAN`). **[Sửa v2]** Hai nhận định cũ cần sửa: (1) chỉ bản **tổng hợp giá nhập** chia `QUYDOI`, bản **tổng hợp giá vốn** nhân như bản chi tiết (khác biệt thứ hai chưa từng nêu: điều kiện chia chỉ kiểm tra mặt hàng *có* đơn vị chẵn, không kiểm tra dòng bán *dùng* đơn vị chẵn → bán bằng đơn vị lẻ vẫn bị chia `QUYDOI`; `GIANHAP` ở hai nơi hiểu theo hai đơn vị khác nhau — cần đối chiếu khi viết lại); (2) dòng "GIẢM GIÁ TỔNG BILL" **cũng có ở bản tổng hợp** (cả 4 báo cáo lãi gộp, đã kiểm lại trên `POS_TAP_SRC_EXPORT`), nhưng bản tổng hợp lọc khác: `LOAI = 0 AND DATHANHTOAN = 30` và chỉ khi không lọc nhóm hàng → cùng một kỳ, bản chi tiết và bản tổng hợp in **hai số giảm giá tổng khác nhau** (tổng lãi in cũng lệch theo). Dùng `SLXUATCHUAQUYDOI`/`SLNHAPCHUAQUYDOI` (số lượng theo đơn vị bán) và giá theo đơn vị bán.

## 4. Báo cáo kết quả kinh doanh (so sánh với kỳ trước một tháng)

Tham số: khoảng ngày, cửa hàng (tuỳ chọn). "Tháng trước" = cùng khoảng lùi 1 tháng (`DATEADD(MONTH, -1, …)`). Bill tính: `(LOAI=0 AND DATHANHTOAN=30) OR LOAI=5 OR LOAI=6` (hai loại phiếu 5 và 6 là loại phiếu thu bán hàng chưa rõ nguồn gốc — **[Chưa chốt]** vòng 2 đã đọc hết `sform_code` và vẫn không có nơi nào sinh `LOAI 5/6`: chỉ xuất hiện ở phía đọc của KQKD và biểu đồ doanh thu ngày (tổng hợp vòng 2 còn nêu tồn quỹ và XNT chi tiết nhưng SQL thật trong `POS_TAP_SRC_EXPORT` không chứa `LOAI = 5/6` ở hai báo cáo đó); số `5`/`6` trong ghi chú "biến phí/chi tiết theo nhóm" của KQKD là mã loại *dòng báo cáo*, trùng số ngẫu nhiên; cần `SELECT DISTINCT LOAI FROM TDONHANG` trên DB thật). Các dòng:
- **DOANH THU** = `Σ TONGCONG`; bên dưới: doanh thu theo nhóm mặt hàng (`Σ THANHTIEN`, bill `LOAI=0` đã thanh toán, nhóm rỗng ghi "(Chưa phân nhóm)"), Tiền giờ `Σ TIENGIO`, Giảm giá `Σ TIENGIAMGIATONG`, Phí dịch vụ `Σ PHIDICHVU`, Thuế `Σ TIENTHUE`.
- **Định phí** = Σ `CHI` của `TTHUCHI` theo lý do `DLYDOTHUCHI.LOAILYDO = 1`, liệt kê từng lý do. **Biến phí** = `Σ (SLXUAT − SLNHAP) × GIAVON` của dòng bán (không vật tư, không combo con) theo nhóm mặt hàng. **Chi khác** = Σ `CHI` theo lý do `LOAILYDO = 2`. **CHI PHÍ** = định phí + biến phí + chi khác.
- **LÃI LỖ** = DOANH THU − CHI PHÍ (mã gốc chỉ trừ ba loại chi phí; doanh thu ở đây là `TONGCONG` đã gồm thuế và phí dịch vụ, trước khi trừ giá vốn). Cột: tháng này, tỉ lệ, tăng/giảm (`tháng này − tháng trước`), tháng trước, biểu tượng ▲/▼.
- **[Bổ sung v2 — bug đã chứng minh bằng code]** Dòng LÃI LỖ: `TangGiam = (DS_Nay − CP_Nay) − DS_Truoc − CP_Truoc` — thiếu dấu `+` trước chi phí tháng trước; đúng phải là `(DS_Nay − CP_Nay) − (DS_Truoc − CP_Truoc)`. Số in ra thấp hơn số đúng đúng `2 × ChiPhiThangTruoc`. Mẫu in chỉ hiển thị `[TANGGIAM]` (0 Total), nên in đúng như code tính. Chiến chốt giữ y hệt hay sửa.
- **[Bổ sung v2]** Cột tỉ lệ hard-code `"100%"` cho mọi dòng (`string tiLeDoanhThu = "100%"`); số bill đếm bằng `COUNT(*)` rồi bỏ (không có chỗ hiển thị "N HĐ"); `BIEUTUONG` không nhất quán (dòng doanh thu `"B"`/`"D"`, dòng chi phí `"▲"`/`"▼"` — có thể là font Wingdings, cần chạy thử).

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
- **[Bổ sung v2 — mâu thuẫn]** Phiếu `LOAI = 2` có NCC: SQL chính tính **+**`TIENTHANHTOAN` vào THU (trong kỳ) nhưng `AfterGetData` tồn đầu kỳ tính **−**`TIENTHANHTOAN` → cùng một phiếu trong kỳ là thu, trước kỳ là chi (`LOAI = 1` nhất quán). Mẫu in (3 Total `TTHU/TCHI/TTON`) chỉ cộng cột hiển thị, không đổi dấu → không giải quyết được ở tầng mẫu in. **[Chưa chốt]** cần test (checklist CN-CHAN3). Báo cáo **không lọc `STATUS`** (phiếu xóa mềm vẫn tính). SQL không đụng bảng `DNGANHANG` (tên tham số `@DNGANHANGID` nhưng lookup thật là `DTAIKHOANNGANHANG`), nên không lỗi dù POS TAP thiếu `DNGANHANG`. `@DLOAIQUYID` lookup `DLOAIQUY` (ID chuỗi `'0'/'1'/'2'`) — bảng này được dùng.

## 7. Báo cáo công nợ

- **Công nợ khách hàng** (theo kỳ): với mỗi khách `STATUS=30` (lọc nhóm khách): `MUADAUKY = Σ(TONGCONG − TIENTHANHTOAN)` các bill `LOAI IN (0, 99)` đã thanh toán trước kỳ (99 = công nợ đầu); `THANHTOANDAUKY = Σ(THU − CHI)` phiếu `TTHUCHI` không đánh `KHONGTHAYDOICONGNO` trước kỳ; `NODAU = MUADAUKY − THANHTOANDAUKY`; trong kỳ `MUA = Σ TONGCONG`, `THANHTOAN = Σ TIENTHANHTOAN(bill) + Σ(THU − CHI)(phiếu)`; `NOCUOI = NODAU + MUA − THANHTOAN`; ẩn khách mọi số bằng 0. Bấm tên khách mở **Đối chiếu công nợ**.
- **Đối chiếu công nợ khách hàng**: `NỢ ĐẦU KỲ = Σ(TONGCONG − TIENTHANHTOAN)` bill (`LOAI = 0` đã thanh toán hoặc `LOAI ≠ 0`) ngày `< từ ngày` − `Σ(THU − CHI)` phiếu trước kỳ; `PHÁT SINH MUA = Σ TONGCONG` bill bán trong kỳ; `NỢ CUỐI KỲ` tính tương tự đến hết ngày cuối; `THANH TOÁN TRONG KỲ = nợ đầu + phát sinh − nợ cuối` (số suy ngược). Danh sách bill/phiếu trong kỳ do `query.sql`.
- **Tổng hợp công nợ khách hàng**: `Σ(TONGCONG − TIENTHANHTOAN)` bill `LOAI IN (0, 99)` đã thanh toán + `Σ(CHI − THU)` phiếu thu chi (không đánh `KHONGTHAYDOICONGNO`); khách có tổng khác 0. **[Sửa v2]** Nhận định cũ "dấu của phiếu thu chi ở đây ngược với báo cáo theo kỳ" là **sai với khách hàng**: tổng hợp *cộng* `Σ(CHI − THU)`, theo kỳ *trừ* `Σ(THU − CHI)` — hai biểu thức đại số tương đương, cho cùng kết quả (nợ 10 triệu, trả 4 triệu bằng phiếu thu → cả hai ra 6 triệu). Không cần xác nhận quy ước dấu cho khách.
- **Công nợ nhà cung cấp**: tương tự nhưng nguồn là bill `LOAI IN (1 nhập hàng, 2 xuất trả, 98 công nợ đầu)` (phiếu `LOAI=2` đảo dấu cả `TONGCONG` và `TIENTHANHTOAN`) và phiếu thu chi theo `DNHACUNGCAPID` với `THANHTOAN = Σ(CHI − THU)`. Đối chiếu công nợ NCC: nợ đầu kỳ = `Σ ± (TONGCONG − TIENTHANHTOAN)` (≤ trước kỳ; không lọc `DATHANHTOAN`) − `Σ(CHI − THU)` phiếu; các dòng còn lại như khách. **[Sửa v2 — bug]** Chỉ **báo cáo Tổng hợp công nợ NCC** bị sai dấu: nó *cộng* `Σ(CHI − THU)` (theo kỳ/đối chiếu/màn hình đều *trừ*) → trả tiền làm nợ tăng (mua chịu 10M, trả 4M ra **14M** thay vì 6M), lệch `2×Σ(CHI − THU)`; và thiếu `COALESCE(KHONGTHAYDOICONGNO,0)=0` (đã kiểm trên SQL thật: file Tổng hợp NCC không chứa chuỗi này, 3 báo cáo còn lại đều có). Clone nên theo theo-kỳ/đối chiếu. Báo cáo theo kỳ KH/NCC có lỗi `@TODATE` (hoa) ở `MUATRONGKY`/`THANHTOANTRONGKY1` (đã kiểm trên SQL thật, cả 2 file) — **[Chưa chốt]** có làm mất phát sinh trong kỳ không (checklist CN-CHAN4).
- Báo cáo phụ trợ: nhãn "Tổng cộng" các cột nợ đầu / mua / thanh toán / nợ cuối. **[Bổ sung v2]** Đây là `Total` trong `.frx` đối với 6 báo cáo theo kỳ/tổng hợp (theo kỳ: `TNODAU/TMUA/TTHANHTOAN/TNOCUOI`; tổng hợp: `TONGNO`); hai báo cáo đối chiếu có 0 Total — 4 dòng tổng là dòng do code chèn.

## 8. Bổ sung vòng 2 (19/11/2026) — báo cáo tự sinh, mẫu in `.frx`, báo cáo thừa

Nguồn: `deep/bao-cao.md` + `work/frx-extract.md` (đọc `.frx`/`.config` của `DEMO.fdb`), đối chiếu với `POS_TAP_SRC_EXPORT`.

### 8.1 73 báo cáo "không SQL" là báo cáo tự sinh, không lấy SQL từ `.frx`
- 0/36 file `.frx` chứa `SelectCommand`. 73 báo cáo lá không có SQL là **báo cáo tự sinh**: mỗi báo cáo có file `.config` (XML `<DocumentElement>`: danh sách cột hiển thị + cờ gom nhóm/cộng tổng) và dòng manifest `SREPORTTEMPLATE` với `AUTOGENREPORT = 30` (118/156 dòng). Framework `No1Lib` dựng báo cáo lúc chạy từ `.config`; cơ chế sinh SQL (WHERE, bảng nguồn, JOIN) **không có trong export** → chưa biết WHERE chính xác của 73 báo cáo này.
- `.frx` chỉ chứa bố cục + `<Total>` (cộng cột hiển thị), không chứa SQL hay biểu thức tính. Kết luận về mức đầy đủ: **109/109 báo cáo lá có công thức hiển thị đầy đủ** (cột + nhóm + tổng), nhưng chỉ **36/109 có công thức lấy số liệu đầy đủ** (kể cả WHERE). 19 nút nhóm cha không phải báo cáo.
- 6 báo cáo thu chi (tổng hợp/danh sách) cũng thuộc nhóm này (`SQL = NULL`): `.config` chỉ có cột + nhóm theo ngày/lý do + tổng `TONGCONG`; công thức tổng hợp chính xác chưa biết.
- **Bản clone** làm theo cơ chế "báo cáo tự sinh từ cấu hình cột" (không viết SQL cho từng báo cáo này), và vì WHERE chưa biết nên phải thử trên app gốc trước khi chốt (checklist BC-PHU1).

### 8.2 Ý nghĩa tên báo cáo
- "XUẤT BÁN HÀNG THEO NGÀY/ĐƠN" = báo cáo **xuất vật tư theo đơn bán** (vật tư `DMATHANG_NAME`, đơn giá = `GIAVON`, SL `SLXUATCHUAQUYDOI`, thành tiền `THANHTIEN`), **khác** "BÁO CÁO BÁN HÀNG THEO NGÀY" (danh sách bill `TDONHANG`). "XUẤT" = xuất kho vật tư.
- "NHÓM HIỂN THỊ" = bảng `DNHOMHIENTHI` (0 dòng ở DEMO). "THEO NHÂN VIÊN" dùng `DNHANVIEN_NAME` = nhân viên bán (`TDONHANG.DNHANVIENID`); NVKD (`DNHANVIENXUATID`) chỉ ở biểu đồ theo NVKD — hai cột khác nhau.
- `DLOAIMATHANGID`: báo cáo kho chỉ lấy `'0'` + `'2'` (kiêm vật tư + nguyên vật liệu). `HANSUDUNG` và `DKHOHANGID` nằm trên `TDONHANGCHITIET` (dòng chi tiết), không phải `TDONHANG`/`DMATHANG`.

### 8.3 Bug/hành vi từ mẫu in (đối chiếu `.frx` DEMO)
- **THẺ KHO chỉ có số lượng**: `TableDataSource` 7 cột (`SOPHIEU/NGAYTHANG/DIENGIAI/SLNHAP/SLXUAT/SLTON/MODE`), không cột tiền; các cột `GIATRI*`/`GIANHAP`/`GIAXUAT`/`GIATON` trong SQL là cột chết (chủ ý).
- **XNT thường**: mẫu in hiển thị `[GIATRITON]` nguyên vẹn (có cả Total) → khi `Ton = 0` mà `GiaTriTon ≠ 0` vẫn in ra (code chỉ zero cột `GIAVON`). XNT chi tiết: các Total `TTONKHO_NHOM` (đặt `PrintOn='ReportSummary1'`), `TTNHAP/TTXUAT/TTXUATVATTU` (`PrintOn=None`) lạ — chưa rõ có sai hiển thị không.
- "CHI TIẾT BÁN HÀNG THEO NGÀY": Total `TTienGiamMatHang` (tổng `TIENGIAMGIA` theo dòng chi tiết) và tham số tổng giảm giá bill (`Σ TIENGIAMGIA` theo bill) — hai con số "giảm giá" khác nguồn cùng xuất hiện trên một mẫu in.
- Biểu đồ tháng trong năm: bug (thiếu tháng 12, đảo nhãn năm) nằm hoàn toàn ở CODE; `.frx` chỉ có 3 cột `THANG/NAMNAY/NAMNGOAI`, 0 Total → muốn sửa phải sửa code, không sửa mẫu in.
- "TỔNG HỢP XUẤT KHÁC THEO NGÀY": các cột tiền không có cờ tổng (khác mọi báo cáo cùng họ) — có thể sót cấu hình.

### 8.4 Báo cáo thừa của DEMO (128) so với POS TAP (127) — **chỗ DEMO khác dữ liệu thật**
- Vòng 2 (đọc DEMO) nghi ngờ ứng viên số 1 là "BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN" (`1b44aa87-…`) vì thiếu manifest + template. **[Sửa v2] Nghi ngờ này bị bác theo dữ liệu thật:** báo cáo đó **có** trong `POS_TAP_SRC_EXPORT/SREPORT/BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN__1b44aa87`. Báo cáo thừa của DEMO vẫn chưa xác định (export thật chỉ có 36 báo cáo SQL, không liệt kê 73 báo cáo tự sinh) → cần `SELECT NAME FROM SREPORT` trên `DATA.fdb` thật.
- 14 file `.config` mồ côi (không gắn báo cáo nào, 6 biến thể của báo cáo mặt hàng bán nhóm theo khách/giờ/ngày/tài khoản/nhân viên/mặt hàng): có thể là tàn dư báo cáo đã xóa — **không dùng làm căn cứ spec**.
- `SREPORTROLE` (phân quyền báo cáo) không có trong dữ liệu xuất nên chưa đối chiếu được phân quyền từng báo cáo.
