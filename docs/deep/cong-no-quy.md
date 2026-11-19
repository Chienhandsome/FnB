# Rà SÂU vòng 2 — Nhóm CÔNG NỢ + QUỸ (POS TAP / FnB clone)

- Ngày rà: 19/11/2026. Nguồn: `~/workspace/fnb/pos-tap-review/raw/` (xuất từ FnB `DEMO.fdb`) + đọc trực tiếp DB bằng Firebird 2.5 embedded + cây giải mã đầy đủ tại `/tmp/deep/` (sform_code/tscode/designcode/aelayout/sreport_sql đã giải mã đúng cả phần code text thường lẫn literal `b'...'` — xem ghi chú cuối file).
- Đối chiếu: `docs/pos-tap-nghiep-vu-thu-chi-cong-no.md`, `docs/pos-tap-nghiep-vu-muc-luc.md`, `docs/pos-tap-nghiep-vu-bao-cao.md` (Claude) + vòng 1 `cong-no.md` (12 câu) + `quy.md` (Q1–Q16).
- Quy ước: cờ `30` = bật/có, `0`/NULL = tắt/không. `STATUS = 30` = đang dùng, `-30` = xóa mềm. Tiền là `BIGINT` (đồng, không thập phân).
- Phạm vi: chỉ đọc file/DB, không sửa repo/DB, không commit/push.

## (a) Đối chiếu từng khẳng định của Claude

### a.1. File `pos-tap-nghiep-vu-thu-chi-cong-no.md`

| # | Khẳng định của Claude | Kết luận vòng 2 | Bằng chứng |
|---|---|---|---|
| 1 | `TTHUCHI.LOAI`: 0 = phiếu thu, 1 = phiếu chi, 2 = phiếu thu công nợ (`LAPHIEUTHUCONGNO = 30`); tiền ở `THU`/`CHI`; dòng chi tiết thu công nợ dùng `SOTIEN` | **Đúng** | `TTHUCHI0Ae/1Ae/2Ae`, `SFORM.LOAI`; lưới phiếu thu công nợ map `SOTIEN = LANNAY` |
| 2 | `LOAIDOITUONG`: 0 = khác (nhập tay), 1 = nhân viên, 2 = khách hàng, 3 = NCC; chọn đối tượng tự điền tên/địa chỉ (nhân viên không có địa chỉ) | **Đúng** | Code `TTHUCHI0Ae` (4 mục bật/tắt control); `Phiếu thu.txt` AELAYOUT đầy đủ 21 FieldMap |
| 3 | Lý do thu/chi: `DLYDOTHUCHI`, `LALYDOTHU = 30` là lý do thu, `0` là lý do chi; form tự lọc theo loại phiếu; có lý do mẫu "Thu tạm ứng" | **Đúng** | `lueDLYDOTHUCHIID_CustomLoadData` (`LALYDOTHU = 30` / `= 0`); dữ liệu mẫu `DLYDOTHUCHI` có "Thu tạm ứng" |
| 4 | Chuyển khoản `CHUYENKHOAN = 30` kèm `DTAIKHOANNGANHANGID`, bắt buộc chọn tài khoản khi tick; phiếu chi tạm ứng lương ẩn mục chuyển khoản | **Đúng** | `mapper_BeforeSave` (cảnh báo "Mời bạn chọn tài khoản ngân hàng"); `LATAMUNG = 30` → ẩn `chkCHUYENKHOAN` (`TTHUCHI1Ae`) |
| 5 | Cờ `KHONGTHAYDOICONGNO`; phiếu "khách nạp thẻ" mặc định tick cờ này | **Đúng** | `SetNapThe()` tick `chkKHONGTHAYDOICONGNO` (cả `TTHUCHI0Ae` và `TTHUCHI1Ae`) |
| 6 | Cửa hàng `DCUAHANGID` mặc định `Shared.DCUAHANGID`; người không-admin chỉ chọn cửa hàng được gán (`TNGUOIDUNGTHEOCUAHANG`) | **Đúng** | Code form phiếu thu/chi + `TonQuyHandler.lue_CustomLoadData` + danh mục phiếu thu/chi |
| 7 | Ngày chỉ sửa được nếu có quyền `ThayDoiNgayTrongPhieuThuChi` | **Đúng** | `DbUtils.CanView(Functions.ThayDoiNgayTrongPhieuThuChi)`; function `24b12b0d` nhóm Quỹ |
| 8 | `SetTamUng`: gắn `TDONHANGID`, diễn giải "Tạm ứng cho bàn/phòng …", lý do lấy theo phiếu tạm ứng gần nhất hoặc "Thu tạm ứng", tự tick `KHONGTHAYDOICONGNO` và khóa | **Đúng** (phần mô tả `SetTamUng`) | `TTHUCHI0Ae.SetTamUng`, `Tạm_ứng_đơn_hàng.grTamUng_AfterCreatedEditForm` |
| 9 | "Tiền này được trừ vào bill khi thanh toán (mục 'đặt trước' ở các file nhà hàng)" | **Chưa chính xác — cần phân biệt 2 luồng** | Xem (b.5). `SetTamUng` chỉ gán `TDONHANGID`; tiền đặt trước đi vào `GetDatTruoc()` qua `TDATHANGID`, mà `TDATHANGID` được gán từ form đặt hàng/phòng (nhánh `HOTEL`), không phải từ `SetTamUng`. Hơn nữa `TIENTHANHTOAN` ghi lên bill = `TienThanhToan − DatTruoc` nên đặt trước **không** làm giảm số dư bill trong báo cáo |
| 10 | `SetLuong(nhân viên, số tiền, bảng lương)`, `LATAMUNG = 30`, `SetGioiHanNgay(tháng, năm)` | **Đúng** | `TTHUCHI1Ae.SetLuong`, `SetGioiHanNgay`; `Lương_chi_tiết`, `Chi_lương` |
| 11 | Truyền giá trị từ nơi gọi: `TONGCONGNO` → `THU`/`CHI`; `DKHACHHANGID` → loại đối tượng khách; `DNHACUNGCAPID` → loại NCC | **Đúng** | `mapper_OnSetValue` cả 2 form phiếu thu/chi |
| 12 | Trả lại tiền cọc dư: phiếu chi "Trả lại tiền đặt trước hóa đơn số …", `KHONGTHAYDOICONGNO` tick | **Đúng** | `TraLaiKhach` (`Hóa_đơn_nhà_hàng.txt`) |
| 13 | Phiếu thu công nợ: lưới chi tiết `SOTIEN = LANNAY`, `THU = Σ SOTIEN`, diễn giải "Thu công nợ", `LOAIDOITUONG = 1`, `LAPHIEUTHUCONGNO = 30`, cửa hàng mặc định | **Đúng** | `TTHUCHI2Ae.mapper_OnSetValue("DETAILDATA")`, code form chỉ 2.051 ký tự, không có `BeforeSave` |
| 14 | "Dữ liệu dòng đến từ màn hình công nợ khách hàng (theo đơn) qua `DETAILDATA`" | **Chưa xác minh được từ code** | 0 nơi gửi `DETAILDATA` trong `SFORM.CODE`, `STABLEDESC.CODE`, `TSCODE`, `DESIGNCODE` (đã kiểm ở cấp DB: chỉ có form 'Phiếu thu công nợ' chứa chuỗi này, là bên nhận). Không có caller `CreateAeForm(Tables.TTHUCHI, 2, …)`. Điểm vào phải nằm trong framework (`CongNo0Handler` trong No1Lib) — không đọc được |
| 15 | Công nợ ban đầu: 2 biến thể KH/NCC theo menu gọi (`3771b8bf` = NCC); lưu thành 1 phiếu `TDONHANG` cho mỗi đối tác, `LOAI = 99` (KH) / `98` (NCC), `NAME = 'CONGNODAU'`, `DATHANHTOAN = 30`, `TONGCONG = TIENHANG = số tiền`, `TILETHUE = 0`, `NGAY` mặc định 31/12 năm trước; > 0 tạo/cập nhật, = 0 xóa, < 0 bỏ qua; nhập Excel; chỉ liệt kê đối tác `STATUS = 30` | **Đúng toàn bộ** | `CongNoBanDau` (CODE_LEN 11.330); đã đối chiếu từng nhánh `btnOK_Click` |
| 16 | "Quy ước công nợ khách (từ `GetNoCu`): nợ = Σ(`TONGCONG − TIENTHANHTOAN`) của bill đã thanh toán − Σ(thu − chi) có tác động công nợ" | **Gần đúng, thiếu 2 chi tiết quan trọng** | `GetNoCu` thật (`Hóa_đơn_bán_hàng.txt`): vế `TTHUCHI` loại thêm phiếu có `TDATHANGID IS NOT NULL`; và phiếu thu công nợ (`LOAI = 2`) đóng góp `−CHI` = 0, tức **không** giảm `GetNoCu`. Xem (b.1) |
| 17 | Tồn quỹ (`TonQuyHandler`): 4 nhánh cây, tiền mặt `CHUYENKHOAN = 0`, ngân hàng `CHUYENKHOAN = 30` (+ tài khoản), quẹt thẻ phiếu thu chi `0 = 1`; phía bill `TIENMAT`/`THE`/`CHUYENKHOAN`, tổng "tất cả" = `TIENMAT + CHUYENKHOAN + THE`; lọc cửa hàng của bill theo `DCUAHANGID` hoặc cửa hàng của kho nhập/xuất | **Đúng** | Đã đọc toàn bộ `TonQuyHandler` (`Tồn_quỹ.txt`); SQL báo cáo `BÁO CÁO TỒN QUỸ` (xem (b.7)) |
| 18 | "SQL gốc của báo cáo quỹ nằm ở `SREPORT/BÁO CÁO TỒN QUỸ` (sẽ ghi khi đọc báo cáo)" | **Đã bổ sung vòng 2** | SQL đầy đủ đã lấy được (report ID `4c7c3a16-b88b-4347-aab9-d2b39475f346`, class `BaoCaoTonQuy`). Xem (b.7) |
| 19 | Lý do thu/chi (`DLYDOTHUCHIAe`): `LALYDOTHU = 30`/`0`, loại trừ nhau; lý do chi có `LOAILYDO`: 1 = định phí, 2 = chi khác; loại còn lại không vào 2 nhóm KQKD; các lý do hệ thống "Thu tạm ứng", "Nạp thẻ trả trước", "Chi trả thẻ trả trước" | **Đúng**; vòng 2 bổ sung nhãn combo chính xác | `STABLEDESC.AELAYOUT` của `DLYDOTHUCHI` (blob 12.623 byte): items [0] = "Không lên báo cáo kết quả kinh doanh", [1] = "Chi phí cố định (chi phí phát sinh hàng tháng)", [2] = "Chi phí khác"; map 0 → −1, 1 → 1, 2 → 2. Báo cáo KQKD: `LOAILYDO = 1` (định phí), `= 2` (chi khác). Xem (b.9) |

### a.2. File `pos-tap-nghiep-vu-muc-luc.md` + `pos-tap-nghiep-vu-bao-cao.md` (phần công nợ / quỹ)

| # | Khẳng định của Claude | Kết luận vòng 2 | Bằng chứng |
|---|---|---|---|
| 20 | "Công nợ khách = Σ(`TONGCONG − TIENTHANHTOAN`) bill (`LOAI 0` đã thanh toán, `99`) − Σ(`THU − CHI`) phiếu không đánh `KHONGTHAYDOICONGNO`; nhà cung cấp tương tự với `LOAI 1, 2, 98` và `CHI − THU`" (mục lục, dòng 29) | **Nửa đầu đúng; nửa sau SAI nếu hiểu là quy ước chung** | Xem (b.2): `CHI − THU` chỉ đúng với đúng 1 trong 4 nguồn NCC (bản `TỔNG_HỢP_CÔNG_NỢ_NHÀ_CUNG_CẤP` — và chính bản đó bị đảo dấu). Quy ước đúng theo form màn hình + báo cáo kỳ + đối chiếu là `THU − CHI` (khi cộng) / `CHI − THU` (khi trừ) |
| 21 | "Công nợ dùng dấu `CHI − THU` ở bản tổng hợp, `THU − CHI` ở bản theo kỳ (khách hàng)" — liệt kê vào mục nghi lỗi #8 (mục lục, dòng 50); "Tổng hợp công nợ khách hàng: … **dấu của phiếu thu chi ở đây ngược với báo cáo theo kỳ** (`CHI − THU`; báo cáo theo kỳ dùng `THU − CHI`) … Cần xác nhận quy ước dấu khi viết lại" (bao-cao, dòng 86) | **Chưa chính xác — với KH hai bản không lệch nhau** | Bản tổng hợp KH **CỘNG** nhánh (`CHI − THU`); bản theo kỳ KH **TRỪ** số hạng (`THU − CHI`). Hai biểu thức đại số tương đương: đều = bill − (thu − chi). Scenario: nợ 10 triệu, trả 4 triệu bằng phiếu thu → cả hai bản đều ra 6 triệu. Lệch thật chỉ nằm ở bản tổng hợp **NCC** (xem #20, (b.2)) |
| 22 | "Công nợ nhà cung cấp: … phiếu thu chi theo `DNHACUNGCAPID` với `THANHTOAN = Σ(CHI − THU)`" (bao-cao, dòng 87) | **Đúng với báo cáo kỳ + đối chiếu NCC; SAI với màn hình + bản tổng hợp NCC (theo chiều ngược nhau)** | Báo cáo kỳ NCC: `NOCUOI = … − Σ(CHI−THU)` (trừ) ✓; đối chiếu NCC: `Tra = Σ(CHI−THU)`, `No = Mua − Tra` ✓; màn hình NCC: `THU − CHI` (cộng) — tương đương với 2 báo cáo trên ✓; bản tổng hợp NCC: `+ Σ(CHI−THU)` (cộng) ✗ — đây mới là bản lệch |
| 23 | Tồn quỹ (bao-cao, dòng 79–80): "phiếu `LOAI = 2` có nhà cung cấp → `TIENTHANHTOAN` là thu; phiếu `LOAI = 1` (nhập hàng) → `TIENTHANHTOAN` là chi"; "Tồn đầu kỳ = (thu − chi của `TTHUCHI` trước kỳ) + (bán hàng đã thanh toán trước kỳ theo loại quỹ …, trừ `TIENTHANHTOAN` của phiếu nhập `LOAI=1` hoặc `LOAI=2` có NCC), cộng dồn `TON` từng dòng" | **Đúng theo code**; nhưng Claude **không phát hiện mâu thuẫn nội bộ** của `LOAI = 2` | SQL chính: `LOAI=2 AND DNHACUNGCAPID<>''` → **+**`TIENTHANHTOAN` vào THU. `AfterGetData` tồn đầu kỳ: cùng điều kiện → **−**`TIENTHANHTOAN`. Cùng một phiếu, trong kỳ tính là thu, trước kỳ tính là chi. Xem (b.7), (e) BUG-2 |
| 24 | KQKD: "Định phí = Σ `CHI` của `TTHUCHI` theo lý do `LOAILYDO = 1` … Chi khác = Σ `CHI` theo lý do `LOAILYDO = 2`" (bao-cao, dòng 61) | **Đúng** | Đã đối chiếu SQL báo cáo KQKD: `WHERE LOAILYDO = 1` / `= 2`, cộng `CHI` theo lý do |
| 25 | Đối chiếu công nợ KH/NCC: công thức nợ đầu/cuối kỳ, phát sinh mua, thanh toán trong kỳ suy ngược (bao-cao, dòng 85, 87) | **Đúng** | Đã đọc SQL chi tiết + `AfterGetData` của cả 2 báo cáo đối chiếu. Xem (b.11) |
| 26 | Danh sách 11 nghi lỗi của Claude (mục lục §4) | **Đã tìm thấy độc lập 11/11 trong vòng 1**; vòng 2 không đào sâu lại các mục ngoài công nợ/quỹ | Ghi nhận ở tổng kết vòng 1. Riêng mục #8 về dấu công nợ được sửa lại như #21 trên đây |

### a.3. Những điểm Claude chưa viết / vòng 2 bổ sung mới

- Công thức `GetNoCu` đầy đủ (kèm điều kiện `TDATHANGID IS NULL` và hành vi `LOAI = 2` đóng góp 0).
- Phiếu thu công nợ **không làm giảm bất kỳ con số nợ nào** theo mọi code đọc được (xem (b.3)) — đây là phát hiện quan trọng nhất vòng 2, Claude không nêu.
- SQL gốc `BÁO CÁO TỒN QUỸ` đầy đủ + mâu thuẫn `LOAI = 2` + SQL màn hình Tồn quỹ khác SQL báo cáo.
- `DLOAIQUY` **được dùng** (lookup `@DLOAIQUYID` của BÁO CÁO TỒN QUỸ) — đính chính cả vòng 1 lẫn suy đoán "bảng thừa".
- Nhãn combo `LOAILYDO` chính xác + map 0 → −1.
- Cột MASTER/DETAIL màn công nợ KH/NCC đã giải mã đầy đủ (tên cột, công thức).
- Nơi gọi `SetNapThe` nằm trong `STABLEDESC` (`DTHETRATRUOC`), không phải `SFORM`.
- `TDATHANGID` của phiếu đặt trước được gán từ form đặt hàng/phòng (nhánh `HOTEL`).
- 6 báo cáo thu chi có `SQL = NULL` trong DB (framework tự sinh từ `COLCONFIG`).
- Phân quyền Quỹ theo `SGROUPROLE` (MODE 240/16/0 theo nhóm người dùng).
- Lỗi `@TODATE` (viết hoa) ở báo cáo công nợ KH + NCC theo kỳ.
- **Đính chính 2 lỗi chép của vòng 1:** (i) `cong-no.md` §6.1 viết "`GetNoCu` … không trừ các phiếu thu thường" là **SAI** — `GetNoCu` CÓ trừ `THU` của phiếu thu `LOAI = 0` (xem (b.1)); (ii) `cong-no.md` §1.3 chép công thức NCC thành `− Σ(CHI − THU)` là **SAI so với SQL thật** — SQL thật là `UNION ALL` rồi `SUM`, tức **cộng** nhánh `CHI − THU` (xem (b.2)).

## (b) Công thức + luồng đào sâu theo chủ đề

### b.1. Công nợ KH — 5 nguồn tính nợ (đã đối chiếu từng SQL)

`GetNoCu(DKHACHHANGID, ID)` — `Hóa_đơn_bán_hàng.txt` (dùng để hiện "Nợ cũ" khi chốt bill mới):

```sql
noCu = SELECT SUM(TONGCONG) - SUM(COALESCE(TIENTHANHTOAN, 0)) FROM TDONHANG
       WHERE DKHACHHANGID = '...' AND DATHANHTOAN = 30 AND ID <> '<bill hiện tại>'
noCu -= SELECT SUM(CASE WHEN LOAI = 0 THEN THU ELSE -CHI END) FROM TTHUCHI
        WHERE DKHACHHANGID = '...' AND COALESCE(KHONGTHAYDOICONGNO, 0) = 0 AND TDATHANGID IS NULL
```

Lưu ý: vế `TDONHANG` **không lọc `LOAI`** (tính mọi bill đã chốt của khách, thực tế là `LOAI 0` + `99`); vế `TTHUCHI` trừ `THU` của phiếu thu `LOAI = 0`, cộng `CHI` của phiếu chi `LOAI = 1`, và **loại phiếu có `TDATHANGID`** (đặt trước theo đơn đặt hàng); phiếu thu công nợ `LOAI = 2` đóng góp `−CHI` = 0 → **không giảm `GetNoCu`**.

| Nguồn | Vế bill (`TDONHANG`) | Vế phiếu (`TTHUCHI`) | Lọc thêm |
|---|---|---|---|
| Màn hình (DETAIL, giải mã AELAYOUT) | `COALESCE(TONGCONG,0) − COALESCE(TIENTHANHTOAN,0)`, `LINKCOL = DKHACHHANGID` | `COALESCE(CHI,0) − COALESCE(THU,0)` (cộng vào tổng) | CONFIG có cột lọc `CONDITION = (LOAI = 0 OR LOAI = 99)` (COLID `2f410f17`, `CHECKED = false` — chưa rõ có bật mặc định không) |
| `TỔNG_HỢP_CÔNG_NỢ_KHÁCH_HÀNG` | `TONGCONG − TIENTHANHTOAN`, `LOAI IN (0, 99)`, `DATHANHTOAN = 30` | `CHI − THU` (cộng qua `UNION ALL` + `SUM`) | `KHONGTHAYDOICONGNO = 0`; chỉ giữ khách có tổng ≠ 0; lọc nhóm khách |
| `BÁO CÁO CÔNG NỢ KHÁCH HÀNG` (theo kỳ) | `LOAI IN (0, 99)`, `DATHANHTOAN = 30` | `THU − CHI` (trừ) | `KHONGTHAYDOICONGNO = 0`; `NOCUOI = MUADAUKY − THANHTOANDAUKY + MUATRONGKY − THANHTOANTRONGKY1 − THANHTOANTRONGKY2` |
| `ĐỐI_CHIẾU_CÔNG_NỢ_KHÁCH_HÀNG` | `Mua = Σ(TONGCONG − TIENTHANHTOAN)`, lọc `((LOAI = 0 AND DATHANHTOAN = 30) OR LOAI <> 0)` | `Tra = Σ(THU − CHI)`; `No = Mua − Tra` | Chi tiết: dòng bán lọc `DATHANHTOAN = 30` + `XUATVATTU = 0`; dòng phiếu lọc `KHONGTHAYDOICONGNO = 0` |
| `GetNoCu` | Như trên (không lọc `LOAI`) | `− Σ(CASE LOAI=0 THEN THU ELSE −CHI END)` | Thêm `TDATHANGID IS NULL` |

**Kết luận KH:** cả 5 nguồn nhất quán về hướng dấu (`thu làm giảm nợ`). Khác biệt còn lại chỉ là phạm vi lọc: đối chiếu dùng lọc `LOAI` rộng hơn báo cáo kỳ (xem (e) BUG-12); `GetNoCu` loại thêm phiếu đặt trước.

### b.2. Công nợ NCC — 4 nguồn + verdict dấu (kết luận dứt khoát)

SQL thật của 2 bản tổng hợp (đã đọc nguyên văn từ `SREPORT.SQL`):

```sql
-- TỔNG_HỢP_CÔNG_NỢ_KHÁCH_HÀNG: UNION ALL rồi SUM(TONGNO)
--   nhánh bill:  COALESCE(TONGCONG,0) - COALESCE(TIENTHANHTOAN,0)   -- LOAI 0/99, DATHANHTOAN=30
--   nhánh phiếu: COALESCE(CHI,0) - COALESCE(THU,0)                  -- KHONGTHAYDOICONGNO=0
-- TỔNG_HỢP_CÔNG_NỢ_NHÀ_CUNG_CẤP: UNION ALL rồi SUM(TONGNO)
--   nhánh bill:  COALESCE(CASE WHEN LOAI=2 THEN -TONGCONG ELSE TONGCONG END,0)
--                - COALESCE(CASE WHEN LOAI=2 THEN -TIENTHANHTOAN ELSE TIENTHANHTOAN END,0)
--                -- LOAI 1/2/98, KHÔNG lọc DATHANHTOAN
--   nhánh phiếu: COALESCE(CHI,0) - COALESCE(THU,0)                  -- KHÔNG có lọc KHONGTHAYDOICONGNO
```

| Nguồn NCC | Vế bill | Vế phiếu | Hướng dấu (quy về "nợ = bill ± phiếu") |
|---|---|---|---|
| Màn hình (DETAIL, giải mã AELAYOUT) | `COALESCE(CASE WHEN LOAI=2 THEN −TONGCONG ELSE TONGCONG END,0) − COALESCE(TIENTHANHTOAN,0)` — chỉ đảo `TONGCONG`, **không** đảo `TIENTHANHTOAN` khi `LOAI=2` | `COALESCE(THU,0) − COALESCE(CHI,0)` (cộng) | nợ = bill + (`THU − CHI`) |
| `TỔNG_HỢP_CÔNG_NỢ_NHÀ_CUNG_CẤP` | Đảo **cả** `TONGCONG` lẫn `TIENTHANHTOAN` khi `LOAI=2` | `CHI − THU` (cộng) | nợ = bill + (`CHI − THU`) ✗ |
| `BÁO CÁO CÔNG NỢ NHÀ CUNG CẤP` (theo kỳ) | Đảo cả 2 khi `LOAI=2` (`MUATRONGKY` chỉ cộng `TONGCONG` đã đảo; `THANHTOANTRONGKY1` cộng `TIENTHANHTOAN` đã đảo riêng) | `THANHTOAN* = Σ(CHI − THU)` (trừ) | nợ = bill − (`CHI − THU`) = bill + (`THU − CHI`) ✓ |
| `ĐỐI_CHIẾU_CÔNG_NỢ_NHÀ_CUNG_CẤP` | `Mua = Σ(CASE LOAI=2 THEN −1 ELSE 1 END × (TONGCONG − TIENTHANHTOAN))` — đảo cả 2 | `Tra = Σ(CHI − THU)`; `No = Mua − Tra` | nợ = bill − (`CHI − THU`) ✓ |

**Kiểm chứng bằng scenario** (mua chịu 10 triệu, trả NCC 4 triệu bằng phiếu chi `CHI = 4`):
- Màn hình: 10 + (0 − 4) = **6 triệu** ✓
- Báo cáo kỳ: 10 − (4 − 0) = **6 triệu** ✓
- Đối chiếu: 10 − 4 = **6 triệu** ✓
- **Tổng hợp: 10 + (4 − 0) = 14 triệu** ✗ — trả tiền làm nợ **tăng** thêm 4 triệu (lệch đúng 2 × Σ(`CHI − THU`) so với 3 nguồn còn lại).

**Verdict dứt khoát:** công thức đúng theo code form là hướng **`THU − CHI` (khi cộng) / `CHI − THU` (khi trừ)** — tức phiếu chi trả NCC làm **giảm** nợ, phiếu thu từ NCC (hoàn tiền) làm **tăng** nợ. Claude viết `CHI − THU` cho NCC (mục lục dòng 29, bao-cao dòng 87) là **sai nếu hiểu là quy ước chung** — nó chỉ đúng khi chép lại nhánh của riêng bản `TỔNG_HỢP_CÔNG_NỢ_NHÀ_CUNG_CẤP`, và chính bản đó bị đảo dấu (bug của riêng báo cáo này, xem (e) BUG-1). Bản tổng hợp NCC còn thiếu lọc `KHONGTHAYDOICONGNO` (bản KH có lọc) — phiếu tích cờ vẫn bị tính vào nợ NCC ở bản này.

**Lệch `LOAI = 2` về `TIENTHANHTOAN` (còn mở):** màn hình không đảo `TIENTHANHTOAN` khi `LOAI = 2`, cả 3 báo cáo đều đảo — 3-chọi-1 nghiêng về đảo. Ngữ nghĩa phụ thuộc `TIENTHANHTOAN` trên phiếu xuất trả hàng nghĩa là gì: ô `numTIENTHANHTOAN` trên `Phiếu_xuất_kho` chỉ là field map thường (nhãn theo `SCOLUMN` là "Tiền thanh toán"), không code nào đọc/ghi ngoài framework. Cần test live: lập phiếu xuất trả hàng có `TIENTHANHTOAN ≠ 0` rồi đối chiếu màn hình vs báo cáo.

**Trường hợp biên — phiếu THU gắn NCC (NCC hoàn tiền):** theo hướng `THU − CHI` của màn hình/báo cáo kỳ/đối chiếu, một phiếu thu (`THU > 0`) gắn NCC làm nợ **tăng**. Về mặt kế toán thì hoàn tiền từ NCC phải làm **giảm** nợ (NCC trả lại tiền mình đã trả thừa) — hướng này cũng đáng ngờ, nhưng ca thường gặp (trả tiền bằng phiếu CHI) đã đủ kết luận bản tổng hợp sai. Cần test live ca hoàn tiền.

### b.3. Phiếu thu công nợ — câu chặn số 1: `LANNAY` có cộng vào `TDONHANG.TIENTHANHTOAN` không?

**Đáp án: KHÔNG — qua bất kỳ đường code nào nhìn thấy được.** Bằng chứng (đã kiểm ở cấp DB, không chỉ bản xuất):

1. **`TCHITIETTHANHTOAN`: 0 tham chiếu** trong toàn bộ `SFORM.CODE`, `SREPORT.SQL`, `TSCODE`, `DESIGNCODE`, `AELAYOUT`, `.frx`/`.config`. Không code nào ghi, không code nào đọc. Bảng có 0 dòng trong DEMO.
2. **Grid `grMain` của phiếu thu công nợ** bind vào `TCHITIETTHANHTOAN` (TABLEID `0737d5bc`, xác minh qua `scolumn.txt`: các cột `NOTE`, `SOTIEN`, `TDONHANGID`, `TTHUCHIID` thuộc TABLEID này): cột link `TDONHANGID` (ẩn) + 3 cột lookup hiển thị `TDONHANG` (`NAME`/`NGAY`/`TONGCONG`, COLID `06752d49`/`a4cc8621`/`b758941b`) + `SOTIEN`. Grid `ReadOnly = True`. `mapper_OnSetValue("DETAILDATA")` chép `LANNAY → SOTIEN`, `numTHU = Σ LANNAY`, gán `LOAIDOITUONG = 1`, `LOAI = 2`, `LAPHIEUTHUCONGNO = 30`, `DCUAHANGID = Shared.DCUAHANGID`. CODE form chỉ 2.051 ký tự, không có `BeforeSave`, không có logic lưu chi tiết.
3. **Không tìm thấy nơi gửi `DETAILDATA`:** 0 kết quả trong `SFORM.CODE`, `STABLEDESC.CODE`, `TSCODE`, `DESIGNCODE`. Không có caller `CreateAeForm(Tables.TTHUCHI, 2, …)`. Điểm vào phải nằm trong framework (`CongNo0Handler` trong No1Lib) — không xác minh được từ bản xuất.
4. **Ghi `TIENTHANHTOAN` chỉ xảy ra ở 3 chỗ:** `Hóa_đơn_bán_hàng` (`row.TIENTHANHTOAN = form.TienThanhToan − form.DatTruoc`), `Hóa_đơn_nhà_hàng` (desktop + TSCODE), `Dịch_vụ_máy_tính_bảng` (`dhRow.TIENTHANHTOAN = dhRow.TIENMAT`). Không có chỗ nào khác.
5. **Hệ quả qua `GetNoCu`:** phiếu `LOAI = 2` đóng góp `−CHI` = 0 → không giảm "Nợ cũ". Header phiếu không có `DKHACHHANGID` → vế `TTHUCHI` của mọi báo cáo công nợ KH (lọc theo `DKHACHHANGID`) đều **không thấy** phiếu này. Màn hình công nợ KH cũng không thấy nó (DETAIL `TTHUCHI` lọc theo `DKHACHHANGID`).
6. **Nơi duy nhất phiếu thu công nợ xuất hiện:** Tồn quỹ — nhánh `TTHUCHI` không lọc theo khách, `THU = Σ LANNAY` được tính vào tiền thu trong kỳ.

**Kết luận:** tiền thu vào quỹ nhưng nợ khách không giảm ở bất kỳ con số nào đọc được — **trừ khi** framework No1Lib có hành vi ngầm (tự ghi `TCHITIETTHANHTOAN` + cộng `TIENTHANHTOAN` khi lưu form `LOAI = 2` qua `GridMapper`/`AeController`). Đây là khả năng duy nhất còn lại và không đọc được từ bản xuất. **Bắt buộc xác minh bằng app gốc:** lập 1 phiếu thu công nợ trên app gốc rồi đọc `TIENTHANHTOAN` của bill + `TCHITIETTHANHTOAN` + tổng ở `TỔNG_HỢP_CÔNG_NỢ_KHÁCH_HÀNG` trước/sau khi lưu (và sau khi xóa phiếu đó).

### b.4. Bán chịu — cổng chặn nợ, không FIFO

- `XacNhanThanhToan.CheckAndClose` (desktop + `XacNhanThanhToanTs` cảm ứng, công thức giống hệt): không quyền `Functions.ThanhToan` → return lặng; trả thiếu (`numTongTien > TienTra`): `ChoPhepKhachNo != 30` → cảnh báo "Chức năng này hiện tại chưa được kích hoạt…" + return; chưa chọn khách → cảnh báo + return; hỏi Yes/No "Bạn có muốn cho khách hàng '…' nợ số tiền '…' không?". Thêm kiểm tra hạn mức thẻ trả trước và tài khoản chuyển khoản. `ChoPhepKhachNo` = 0 trong DEMO (đã truy vấn DB).
- Công thức dialog: `TONGTIEN = TONGTIEN_bill + NoCu − datTruoc` (`SetData` dòng 31); `TienThanhToan = datTruoc + max(0, min(TienTra, TONGTIEN))`; `TienNo = numTongTien − (TienThanhToan − numDatTruoc) − numNoCu`; `chkKhachNo` tick → `KhachDua = 0` + khóa ô.
- **Không có phân bổ FIFO:** toàn bộ tiền trả dồn vào `TIENTHANHTOAN` của bill hiện tại (kể cả trả nợ cũ — bill hiện tại có thể "trả thừa" để xóa ngầm nợ cũ). Phân bổ theo từng đơn chỉ có ở phiếu thu công nợ (§b.3 — và chính cơ chế đó cũng chưa chứng minh được).

### b.5. Tạm ứng / đặt trước — 2 luồng khác nhau (đính chính Claude #9)

**Luồng 1 — Tạm ứng cho đơn hàng (bill nhà hàng):** form `Tạm_ứng_đơn_hàng` (mở từ `Hóa_đơn_nhà_hàng.txt` dòng 4929: `form.LoadData(mapper.ID, mapper[TDONHANGInfo.TDATHANGID], lblBan.Text)`). Lưới lọc `(TDONHANGID = '…' OR TDATHANGID = '…')`; tạo mới → `loai = 0` + `SetTamUng(TDONHANGID)`: gán `TDONHANGID`, `DIENGIAI = "Tạm ứng cho <bàn/phòng> <tên>"`, lý do = `DLYDOTHUCHIID` gần nhất trong `TTHUCHI WHERE TDONHANGID IS NOT NULL ORDER BY TIMECREATED DESC`, fallback `NAME = 'Thu tạm ứng' AND LALYDOTHU = 30`; tick + khóa `KHONGTHAYDOICONGNO` và `LOAIDOITUONG`. **Không gán `TDATHANGID`.**

**Luồng 2 — Đặt trước theo đơn đặt hàng (`TDATHANG`):** code trong `STABLEDESC` của form đặt hàng/phòng, nhánh `UngDung.HOTEL`:

```csharp
IAddEditForm obj = Config.CreateAeForm(Tables.TTHUCHI, 0, "");
// lấy ra phiếu đặt trước gần nhất
string sql = "SELECT ID FROM TTHUCHI WHERE TDATHANGID = '" + mapper.ID + "'";
string TTHUCHIID = Config.Db.GetFirstFieldString(sql);
obj.ReLoad(TTHUCHIID);              // mở lại phiếu cũ nếu đã có
if (TTHUCHIID.Length == 0)
{
    obj.SetValue("TDATHANGID", mapper.ID);
    obj.SetValue("TENDOITUONG", txtTENKHACH.Text);
    obj.SetValue("DLYDOTHUCHIID", Config.Db.GetFirstFieldString(
        "SELECT FIRST 1 DLYDOTHUCHIID FROM TTHUCHI WHERE TDATHANGID IS NOT NULL ORDER BY TIMECREATED DESC"));
    obj.SetValue("DIENGIAI", "Đặt tiền trước số phiế...");   // (chuỗi đầy đủ bị cắt trong bản xuất)
}
```

- Form đặt hàng còn hiện số tiền/số phiếu đặt trước: `SELECT NAME, THU FROM TTHUCHI WHERE TDATHANGID = '<id>'` → "Số tiền: …", "Số phiếu thu: …".
- **Thực tế 1 phiếu đặt trước / 1 đơn đặt hàng** qua luồng này (mở lại phiếu cũ thay vì tạo mới). Nhưng `GetDatTruoc()` chỉ lấy dòng đầu (`GetFirstFieldDec`) — nếu có nhiều phiếu cho cùng 1 `TDATHANGID` (tạo tay qua luồng khác), các phiếu sau bị bỏ qua.
- Phiếu thu AELAYOUT **có** FieldMap `TDATHANGID` (1 trong 21 map) — form mang được trường này dù không có control hiển thị; đó là cách `SetValue("TDATHANGID", …)` hoạt động.
- **Ảnh hưởng tới nợ báo cáo (câu 7 công nợ):** phiếu đặt trước có `KHONGTHAYDOICONGNO = 30`? — luồng 2 không tick cờ này trong code đọc được (chỉ luồng 1 `SetTamUng` tick). Nhưng `GetNoCu` loại mọi phiếu có `TDATHANGID` nên đặt trước không chạm "Nợ cũ" trên hóa đơn. Về báo cáo: `TIENTHANHTOAN` ghi lên bill = `TienThanhToan − DatTruoc` → số dư bill `TONGCONG − TIENTHANHTOAN` **không** phản ánh phần đã đặt trước (khách đã trả 2 triệu đặt trước + 8 triệu lúc chốt cho bill 10 triệu thì báo cáo vẫn hiện nợ 2 triệu). Cần test live 1 ca có đặt trước để chốt hành vi đầu-cuối.

### b.6. Số phiếu tự sinh (`TTHUCHI.NAME`) — cơ chế đã rõ, định dạng chưa

- `STABLEDESC.GENNO` là cờ INTEGER: `GENNO = 30` ở `TTHUCHI`, `TDONHANG`, `TDATHANG`, `TBAOGIA`, `TTANGGIAMDIEM`, `TTHUONGPHAT`; 0 ở đa số bảng khác. Nhiều khả năng là cờ "tự sinh số" do framework (No1Lib) đọc — định dạng không nằm trong `STABLEDESC`.
- `SCOLUMN` của `TTHUCHI.NAME`: `FORMULA = None`, `TAG = None`, `DEFAULTCONFIGID = None`, `CONTROLTYPE = Text` — không có công thức.
- Nhóm cấu hình "Số phiếu" (`SCONFIGGROUPID 8ad79fe9-…`) có **0 dòng** (đã truy vấn DB); `TTHUCHI` 0 dòng; `TSOHOADON` (bảng sổ số hóa đơn: `ID, NAME, NOTE, STATUS, …, NGAY, SO`) 0 dòng.
- `STABLEDESC.AELAYOUT` của `TTHUCHI` chỉ 1.252 byte — form mặc định trống, không có default cho `NAME`.
- Cơ chế sinh số **của hóa đơn** (để đối chiếu, không phải của phiếu): `TaoSoHoaDon` (`Hóa_đơn_nhà_hàng.txt` dòng 1728) dùng bảng `TSOHOADON` (`NGAY` + `SO` đếm), quay vòng theo `Shared.SoHDQuayVong` (Ngày/Tháng/Năm/Không quay vòng), định dạng `ddMM+0000` / `MMyy+00000` / `yy+000000` / `0000000`. Hóa đơn bán lẻ dùng `DbUtils.GenSoHoaDon` (No1Lib, không đọc được). Cả hai đều chỉ sinh số cho `TDONHANG.NAME`, **không** liên quan `TTHUCHI`.
- Phiếu thu/chi CODE không sinh số; `mapper_BeforeSave` chỉ kiểm tra tài khoản ngân hàng khi tick chuyển khoản.
- Mẫu `PT20/00001` trong pilot vẫn chưa đối chiếu được; logic sinh số phiếu nằm trong framework (`GENNO = 30`) — còn mở (Q1).
- `Sinh_dữ_liệu` là form chết (`btnThucHien_Click` rỗng, chỉ set `dtNgay` = hôm nay).

### b.7. Tồn quỹ — SQL báo cáo đầy đủ + mâu thuẫn `LOAI = 2` + SQL màn hình khác SQL báo cáo

**SQL gốc `BÁO CÁO TỒN QUỸ`** (report ID `4c7c3a16-b88b-4347-aab9-d2b39475f346`, class `BaoCaoTonQuy`; tham số `@FromDate`, `@ToDate`, `@DLOAIQUYID` (lookup `DLOAIQUY`), `@DNGANHANGID` (lookup `DTAIKHOANNGANHANG`)):

- Nhánh `TTHUCHI` (`NGAY BETWEEN @FromDate AND @ToDate`, lọc theo `@DLOAIQUYID`: `''` = tất cả, `'0'` = tiền mặt `CHUYENKHOAN = 0`, `'2'` = quẹt thẻ `0 = 1`, `'1'` = ngân hàng `CHUYENKHOAN = 30` + tài khoản) `UNION ALL` nhánh `TDONHANG` (`TIENTHANHTOAN <> 0`; THU = `LOAI 0 + DATHANHTOAN = 30` → `TIENMAT`/`CHUYENKHOAN`/`THE` theo lọc, `WHEN LOAI = 2 AND DNHACUNGCAPID <> '' THEN TIENTHANHTOAN`; CHI = `LOAI 1` → `TIENTHANHTOAN`).
- `AfterGetData`: tồn đầu kỳ = `Σ(THU − CHI)` của `TTHUCHI` trước `FromDate` + `Σ(CASE LOAI = 0 + DATHANHTOAN = 30 THEN <theo loại> WHEN LOAI = 1 OR (LOAI = 2 AND DNHACUNGCAPID <> '') THEN −TIENTHANHTOAN ELSE 0 END)` của `TDONHANG` trước `FromDate`; cộng dồn `TON` theo từng dòng; chèn dòng "TỒN ĐẦU KỲ" (`LOAI = 0`) lên đầu.
- **Không có lọc `STATUS`** trong SQL báo cáo (cả 2 nhánh) → phiếu xóa mềm vẫn tính vào báo cáo này (xem Q15).
- `.frx` của báo cáo này (`4c7c3a16-….frx`) **không có `ScriptText`** — logic hoàn toàn nằm ở SQL + class `BaoCaoTonQuy`; `.frx` chỉ là layout. Tương tự `.frx` `TỔNG_HỢP_CÔNG_NỢ_NHÀ_CUNG_CẤP` (`e5b8eb61-…`) không có script — không có override nào trong mẫu in.

**Mâu thuẫn `LOAI = 2` (đã chứng minh bằng code exact):** trong SQL chính, `LOAI = 2` + NCC tính **+**`TIENTHANHTOAN` vào THU; trong `AfterGetData` tồn đầu kỳ, nó tính **−**`TIENTHANHTOAN`. `LOAI = 1` nhất quán (CHI ở SQL chính, − ở tồn đầu kỳ). Hệ quả: một phiếu xuất trả hàng NCC trước kỳ làm tồn đầu kỳ **giảm**, nhưng nếu nó nằm trong kỳ thì làm tồn trong kỳ **tăng** — cùng một nghiệp vụ, dấu đổi theo vị trí thời gian. Xem (e) BUG-2.

**SQL màn hình Tồn quỹ KHÁC SQL báo cáo:** `TonQuyHandler` (đã đọc toàn bộ) thay chuỗi `" UNION ALL"`, `"(TDONHANG.TIENTHANHTOAN <>0)"`, `"COALESCE(TIENMAT, 0) + COALESCE(CHUYENKHOAN, 0) + COALESCE(THE, 0)"` — 2 chuỗi sau **không có** trong SQL báo cáo (báo cáo dùng `CASE` và `TIENTHANHTOAN <> 0` trần), nên SQL gốc của màn hình (nằm trong class `TonQuy` của framework) là một SQL khác, đơn giản hơn (tổng `TIENMAT + CHUYENKHOAN + THE` trên mỗi dòng bill). Cách màn hình xử lý `LOAI = 1/2` không xác định được từ bản xuất.

**Handler lọc theo nút cây** (`tonQuy_CustomLoadData`, `Tồn_quỹ.txt` dòng ~116–190):

| Nút cây | Phía `TTHUCHI` | Phía `TDONHANG` | Cột số tiền dùng |
|---|---|---|---|
| Tất cả (mặc định) | không lọc thêm | không lọc thêm | `TIENMAT + CHUYENKHOAN + THE` |
| Tiền mặt (`TIENMAT`) | `CHUYENKHOAN = 0` | `COALESCE(TIENMAT, 0) <> 0` | `COALESCE(TIENMAT, 0)` |
| Quẹt thẻ (không đặt `Name`) | **`0 = 1`** (loại toàn bộ phiếu thu/chi) | `COALESCE(THE, 0) <> 0` | `COALESCE(THE, 0)` |
| Ngân hàng (`NGANHANG`) | `CHUYENKHOAN = 30` | `COALESCE(CHUYENKHOAN, 0) <> 0` | `COALESCE(CHUYENKHOAN, 0)` |
| Một tài khoản cụ thể (`.Name` = ID tài khoản) | `CHUYENKHOAN = 30 AND DTAIKHOANNGANHANGID = '<id>'` | `DTAIKHOANNGANHANGID = '<id>'` | `COALESCE(CHUYENKHOAN, 0)` |

- Cây bên trái hard-code 3 nút + nút con mỗi tài khoản `DTAIKHOANNGANHANG STATUS = 30`; nút "Quẹt thẻ" không đặt `Name` (xác minh dòng 61–64: chỉ có `Text`, `ImageIndex`, `SelectedImageIndex` — khác 3 nút còn lại đều có `.Name`).
- Người không-admin chỉ thấy cửa hàng được gán (`TNGUOIDUNGTHEOCUAHANG`); lọc cửa hàng chèn `WHERE DCUAHANGID = '…'` trước ` UNION ALL` (phía phiếu) và `(DCUAHANGID = '…' OR (SELECT DCUAHANGID FROM DKHOHANG WHERE ID = COALESCE(DKHONHAPID, DKHOXUATID)) = '…')` sau `(TDONHANG.TIENTHANHTOAN <>0)` (phía bill).
- Ghi chú form (`NOTE = "NGAY|THU|CHI|"`) cho thấy lưới gốc có các cột Ngày / Thu / Chi (tổng hợp theo ngày) — nhưng SQL gốc đầy đủ nằm trong framework, chưa đọc được.

### b.8. `DLOAIQUY` — đính chính kết luận vòng 1 ("bảng thừa" là SAI)

- ID là chuỗi `'0'` (Tiền mặt), `'1'` (Ngân hàng), `'2'` (Tiền thẻ) — không phải GUID (đã truy vấn trực tiếp DB). Vì vậy các so sánh `CAST(@DLOAIQUYID AS VARCHAR(36)) = '0'/'1'/'2'` trong `BÁO CÁO TỒN QUỸ` hoạt động đúng.
- Bảng **ĐƯỢC dùng làm lookup** cho tham số `@DLOAIQUYID` của `BÁO CÁO TỒN QUỸ`, và chỉ ở báo cáo này (truy vấn DB: không báo cáo nào khác dùng `@DLOAIQUYID`). Cây màn hình Tồn quỹ thì hard-code, không đọc bảng.
- Trả lời Q14 quỹ (nửa đầu): không phải bảng chết.

### b.9. `LOAILYDO` — nhãn combo + ý nghĩa (Q4 quỹ đã trả lời được)

- Combo `cboLOAILYDO` trong `STABLEDESC.AELAYOUT` của `DLYDOTHUCHI` (blob 12.623 byte): items [0] = "Không lên báo cáo kết quả kinh doanh", [1] = "Chi phí cố định (chi phí phát sinh hàng tháng)", [2] = "Chi phí khác"; `EditValue` mặc định −1. Map: 0 → −1, 1 → 1, 2 → 2.
- Báo cáo KQKD xác nhận: `WHERE LOAILYDO = 1` (mục định phí) và `WHERE LOAILYDO = 2` (mục chi khác), cộng `CHI` theo lý do.
- Nghĩa: −1 = không lên KQKD; 1 = chi phí cố định; 2 = chi phí khác; 0 ("Tạm ứng") = không vào KQKD và **không chọn được từ combo** (chỉ có 3 mục) — phải được seed sẵn; NULL ("Thu công nợ", "Thanh toán công nợ") = không vào KQKD.
- Code `DLYDOTHUCHIAe`: `chkLALYDOTHU` / `chkLaLyDoChi` loại trừ lẫn nhau; phiếu mới mặc định là lý do thu; `cboLOAILYDO` chỉ bật khi là lý do chi.

### b.10. Nạp thẻ trả trước — nơi gọi `SetNapThe` (Q3 quỹ đã trả lời được)

- Nơi gọi nằm trong **`STABLEDESC`**, không phải `SFORM` — vòng 1 grep sai chỗ (`DTHETRATRUOCAe.btnNapTien_Click` / `btnChiTra_Click` trong `stablesdesc.txt`):
  - Nạp: lưu thẻ trước → `CreateAeForm(TTHUCHI, 0, "")` → `SetValue(DTHETRATRUOCID, card ID)` → lý do = `DLYDOTHUCHIID` dùng gần nhất trong `TTHUCHI WHERE THU > 0 AND DTHETRATRUOCID <> '' ORDER BY TIMECREATED DESC`, fallback `NAME = 'Nạp thẻ trả trước'` → `DIENGIAI = "Nạp thẻ trả trước <tên thẻ>"` → `SetNapThe()` → `ShowDialog` → `LoadData` nếu đã lưu.
  - Chi trả: tương tự với phiếu chi (`LOAI 1`), lý do từ `CHI > 0`, fallback `'Chi trả thẻ trả trước'`, `DIENGIAI = "Chi trả tiền thẻ trả trước <tên thẻ>"`, `SetNapThe()` trên `TTHUCHI1Ae`.
- Kiểm chứng cấp DB: `SetNapThe` chỉ xuất hiện trong `SFORM.CODE` của 'Phiếu thu' và 'Phiếu chi' (định nghĩa); caller nằm trong `STABLEDESC.CODE`. **Bài học:** mọi kết luận "không có caller" chỉ dựa trên `SFORM` đều phải kiểm tra lại với `stablesdesc.txt` — đã kiểm lại `DETAILDATA` trong `stablesdesc.txt`: 0 kết quả, vẫn không có sender.
- Số dư thẻ (`NhapTheTraTruoc.GetSoTienConLai`): `nap = Σ(THU − CHI) FROM TTHUCHI WHERE DTHETRATRUOCID = '<id>'`; `suDung = Σ(THETRATRUOC) FROM TDONHANG WHERE DTHETRATRUOCID = '<id>' AND ID <> '<đơn hiện tại>'`; số dư = `nap − suDung` (thẻ rỗng → 0).

### b.11. Đối chiếu công nợ — chỉ là sao kê, không sinh bút toán

- `ĐỐI_CHIẾU_CÔNG_NỢ_KHÁCH_HÀNG`: SQL chi tiết 2 khối `UNION ALL` (`LOAI 0` = dòng hàng đã bán: `TDONHANG ⨝ TDONHANGCHITIET ⨝ DMATHANG`, `DATHANHTOAN = 30`, `XUATVATTU = 0`; `LOAI 1` = phiếu thu/chi, `KHONGTHAYDOICONGNO = 0`), `ORDER BY LOAI, NGAY, SO`. `AfterGetData` chèn thêm 4 dòng tổng (`LOAI 2`): NỢ ĐẦU KỲ = `Mua − Tra` (`Mua = Σ(TONGCONG − TIENTHANHTOAN)` trên `((LOAI = 0 AND DATHANHTOAN = 30) OR LOAI <> 0)`, `NGAY < @FromDate`; `Tra = Σ(THU − CHI)` trước kỳ); PHÁT SINH MUA TRONG KỲ = `Σ(TONGCONG)` trên `(LOAI = 0 AND DATHANHTOAN = 30)` trong kỳ; THANH TOÁN TRONG KỲ = `NoDauKy + PsMua − NoCuoiKy` (suy ngược, không cộng trực tiếp); NỢ CUỐI KỲ tính tương tự đến hết ngày cuối.
- `ĐỐI_CHIẾU_CÔNG_NỢ_NHÀ_CUNG_CẤP`: cùng khung; dòng hàng không lọc `DATHANHTOAN`/`XUATVATTU`, `TONGCONG`/`TIENTHANHTOAN` đảo dấu khi `LOAI = 2`, `SOLUONG = SLNHAPCHUAQUYDOI`; `Mua` đầu kỳ = `Σ(CASE WHEN LOAI = 2 THEN −1 ELSE 1 END × (TONGCONG − TIENTHANHTOAN))` trên **mọi** `TDONHANG` của NCC trước kỳ (không lọc `LOAI`/`DATHANHTOAN`); `Tra = Σ(CHI − THU)`; 4 dòng tổng giống hệt.
- **Không có chức năng "khớp/lệch":** báo cáo không nhận số liệu đối chiếu từ đối tác, không tính chênh lệch, không sinh bút toán điều chỉnh — lệch chỉ phát hiện bằng mắt khi đọc sao kê. Không có code nào trong bản xuất sinh bút toán công nợ.
- Lỗi `@TODATE`: cả 2 báo cáo công nợ theo kỳ (KH + NCC) đều viết `NGAY BETWEEN @FromDate AND @TODATE` (hoa) ở 2 subquery (`MUATRONGKY`, `THANHTOANTRONGKY1`) trong khi tham số khai báo là `@ToDate`. `THANHTOANTRONGKY2` viết đúng `@ToDate`. Hành vi lúc chạy phụ thuộc cách framework bind tham số (phân biệt hoa/thường hay không) — cần test live (xem (e) BUG-3).

### b.12. Phân quyền Quỹ (`SGROUPROLE`) — Q7 quỹ trả lời một phần

- Cột thứ 5 của `SGROUPROLE` là **MODE** (không phải VALUE). Phân bố MODE: 0 (104 dòng), 16 (28), 80 (8), 112 (2), 240 (126). Giả thuyết: 240 = đủ quyền (16+32+64+128), 16 = chỉ xem/được phép, 0 = không quyền.
- Nhóm người dùng (`SGROUPUSER`): Thu ngân (`e6b89ecd`), Kế toán (`4195e059`), Thủ kho (`bdf36303`), Quản lý (`c75f95fd`), Phục vụ (`7d908baf`).
- 5 function Quỹ: Kế toán + Quản lý có MODE 240 trên Danh mục lý do thu chi / Danh mục phiếu thu chi / Thu chi (`5d0b8bb7`) / Tồn quỹ, và MODE 16 trên "Thay đổi ngày trong phiếu thu chi"; Phục vụ có 0 trên 4 function; Thu ngân/Thủ kho **không có dòng nào** cho các function này.
- Function "Thu chi" **CÓ** được gán (240 cho Kế toán/Quản lý) — nhưng nó khóa cái gì vẫn chưa chứng minh được (không menu/code nào tham chiếu; suy đoán mạnh: khóa 2 menu "Tạo phiếu thu/chi" loại 1 vốn không có `FUNCTIONID` trong `SMENU`).

### b.13. 6 báo cáo thu chi — không có SQL tự viết (Q10 quỹ trả lời một phần)

- Đã xác minh trực tiếp trên DB: cột `SREPORT.SQL` của cả 6 báo cáo đều rỗng/`NULL` (`DANH SÁCH PHIẾU THU/CHI THEO NGÀY/LÝ DO`, `TỔNG HỢP THU CHI THEO NGÀY/LÝ DO`). Trong 128 báo cáo, 46 báo cáo có `SQL = NULL` — đây là nhóm "báo cáo framework".
- `.config` tương ứng trong `sreport_template/` (cả 6 đều có, mỗi báo cáo 2 mẫu: 80mm + A4) chỉ chứa định nghĩa cột (`Data`/`ID`/`COT`/`DATAFIELD`/`HIENTHI`/`WIDTH`/`CONTROLTYPE` + `NHOM1` nhóm theo ngày/lý do + `TONGCONG` tổng). Không có SQL nhúng trong `COLCONFIG`/`MASTERVIEW`.
- `FILTERCONFIG` chỉ chứa preset lọc ngày ("Tháng này", "30 ngày gần nhất") — không thấy lọc `LOAI`.
- Kết luận: SQL của 6 báo cáo do framework tự sinh từ `COLCONFIG` + tham số chuẩn (khoảng ngày; lý do cho bản "theo lý do"); công thức tổng hợp chính xác và cơ chế lọc `LOAI = 0/1` không đọc được từ bản xuất.

### b.14. Trường bắt buộc + ngày mặc định trên phiếu thu/chi (Q8, Q9 quỹ)

- AELAYOUT đầy đủ của Phiếu thu / Phiếu chi: `AllowEmpty = False` trên đúng 3 map — `NAME` (Số phiếu), `THU` / `CHI` (Số tiền), `DCUAHANGID` (Cửa hàng). Không có `DefaultValue` nào trong cả 2 AELAYOUT (0 kết quả) → ngày mặc định của phiếu mới không nằm ở form; nhiều khả năng framework mặc định `NGAY` = hôm nay — chưa chứng minh được (Q9 vẫn mở).
- Lệch với `SCOLUMN`: cờ `NO` (bắt buộc) còn xuất hiện ở `NGAY`, `TENDOITUONG`, `DIENGIAI`, `DLYDOTHUCHIID`. Giải thích: validation trên form chạy theo `AELAYOUT.AllowEmpty` (3 trường trên); cờ `NO` của `SCOLUMN` không quyết định validation trên form này (có thể là ràng buộc ở tầng danh mục chung / DB). Thông báo lỗi khi thiếu từng trường chưa đọc được — cần test live.

### b.15. Trigger duy nhất + cột MASTER/DETAIL màn công nợ (câu 9, câu 2 công nợ)

- `TDONHANG_TRIGGER` (trigger duy nhất trong DB, type 113, đã đọc source từ `RDB$TRIGGERS`): chỉ quản lý `DBAN.TDONHANGID` khi trạng thái thanh toán đổi (trả bàn về trống / gán bill mới). **KHÔNG** chạm `TIENTHANHTOAN`/`CONGNO`/`TCHITIETTHANHTOAN`. Trả lời câu 9 công nợ.
- Nguồn map COLID → cột là các file `.config` trong `sreport_template/` (mỗi `Data` có `ID`, `COT` (caption), `DATAFIELD`, `HIENTHI`, `WIDTH`, `CONTROLTYPE`):
  - **Công nợ KH MASTER** (6 cột + 1 cột tính): Tên khách hàng/`NAME`, Mã khách/`MAKHACH`, Địa chỉ/`DIACHI`, Điện thoại/`DIENTHOAI` (hiện); Email/`EMAIL`, Mã số thuế/`MASOTHUE` (ẩn, `HIENTHI = 0`); cộng cột tính 'Còn nợ' / FIELD = `TONGCONGNO_` (Number, hiện).
  - **Công nợ NCC MASTER** tương tự: Tên NCC/`NAME`, Mã NCC/`MANHACUNGCAP`, Địa chỉ, Điện thoại (hiện); Email (ẩn); 'Còn nợ'/`TONGCONGNO_`.
  - **KH DETAIL** (giải mã blob base64 trong AELAYOUT): `TDONHANG` `COALESCE(TONGCONG,0) − COALESCE(TIENTHANHTOAN,0)` `LINKCOL = DKHACHHANGID`; `TTHUCHI` `COALESCE(CHI,0) − COALESCE(THU,0)`; CONFIG có cột lọc `CONDITION = (LOAI = 0 OR LOAI = 99)` (COLID `2f410f17`, `CHECKED = false`).
  - **NCC DETAIL:** `TDONHANG` `COALESCE(CASE WHEN LOAI = 2 THEN −TONGCONG ELSE TONGCONG END,0) − COALESCE(TIENTHANHTOAN,0)`; `TTHUCHI` `COALESCE(THU,0) − COALESCE(CHI,0)`; CONFIG `CONDITION = (LOAI = 1 OR LOAI = 98 OR LOAI = 2)`.
  - **Màn "theo đơn" DETAIL** (11 cột, **không có cột `LANNAY`**): Số phiếu/`TDONHANG_NAME` (`06752d49`), Ngày/`TDONHANG_NGAY` (`a4cc8621`), Khách hàng/`DKHACHHANG_NAME` (`0c63ad24`), Tổng cộng/`TONGCONG` (`b758941b`), Nhân viên bán/`DNHANVIEN_NAME` (`19a33af4`), Nhân viên (`744e6f2a`, TABLEID `e5f5937a`, CAPTION = 'Nhân viên'), + 4 cột thông tin KH (`a36594f3`/`ccbc6538`/`2db4943e`/`7a857a34`) + `2a8812a2` (cột `TDONHANG`, **chưa xác định được** — không xuất hiện ở đâu khác).
  - Form `2f5b51e9` (theo đơn) và `f34aa294` (phiếu thu công nợ) **không có dòng nào** trong `SREPORTTEMPLATE`/manifest — config của chúng chỉ nằm trong `SFORM.AELAYOUT`.
  - AELAYOUT màn "theo đơn" và màn công nợ KH đều có **0 `ToolStrip`** — không có nút toolbar nào được định nghĩa trong layout; cách mở "theo đơn" và nút lập phiếu thu công nợ (nếu có) nằm trong handler framework, chưa đọc được.
  - `SGRID` chỉ có 2 dòng (lưới `DMATHANG` `a4a16216`, lưới `DKHACHHANG` `5fcc571a`). `SCOLUMN` không có cột `TABLEID` (truy vấn lỗi "Column unknown TABLEID").

## (c) Đáp án cho các câu hỏi vòng 1

### c.1. Nhóm CÔNG NỢ (12 câu)

| # | Câu hỏi vòng 1 | Đáp án vòng 2 | Bằng chứng |
|---|---|---|---|
| 1 | Lưu phiếu thu công nợ có cộng `LANNAY` vào `TDONHANG.TIENTHANHTOAN` của từng bill không? Và có ghi `TCHITIETTHANHTOAN` không? | **Không — theo mọi code đọc được.** `TCHITIETTHANHTOAN` 0 tham chiếu; không có sender `DETAILDATA`; `TIENTHANHTOAN` chỉ được ghi ở 3 chỗ (hóa đơn bán lẻ, hóa đơn nhà hàng, dịch vụ MTB). Chỉ còn khả năng duy nhất là hành vi ngầm của framework No1Lib — cần test app gốc để chốt 100% | (b.3) |
| 2 | Màn "Công nợ khách hàng" mở "theo đơn" bằng cách nào? 5 cột MASTER của 2 màn KH/NCC là những cột nào? | **Trả lời một phần.** Cột MASTER đã giải mã đầy đủ (xem (b.15)). Cách mở "theo đơn" vẫn chưa rõ: AELAYOUT không có `ToolStrip`, handler là framework | (b.15) |
| 3 | Khi lập phiếu thu công nợ, `LANNAY` mặc định bằng số còn nợ của đơn hay 0? Có sửa được từng dòng không? Có chặn `LANNAY` vượt số còn nợ không? Xóa/sửa phiếu đã lưu thì `TIENTHANHTOAN` có hoàn lại không? | **Trả lời một phần.** Grid `ReadOnly = True` → không sửa được `LANNAY` trong lưới (giá trị lấy từ `DETAILDATA` do framework tạo). Giá trị mặc định/chặn vượt nợ/hoàn lại khi xóa-sửa: chưa rõ, vì nguồn `DETAILDATA` và cơ chế ghi đều nằm trong framework | (b.3) |
| 4 | Sau khi khách trả nợ bằng **phiếu thu thường**, lần chốt bill sau "Nợ cũ" (`GetNoCu`) có giảm không? | **CÓ giảm.** `GetNoCu` trừ `THU` của phiếu thu `LOAI = 0` (và cộng `CHI` của phiếu chi). Đính chính vòng 1 §6.1 viết "không trừ phiếu thu thường" là sai | (b.1) — SQL `GetNoCu` nguyên văn |
| 5 | Phiếu tạm ứng: `TDATHANGID` của phiếu tạm ứng được gán ở đâu? | **Đã trả lời.** Từ form đặt hàng/phòng (code `STABLEDESC`, nhánh `HOTEL`): `SetValue("TDATHANGID", mapper.ID)`, mở lại phiếu cũ nếu có (1 phiếu/1 đặt hàng). `SetTamUng` chỉ gán `TDONHANGID` | (b.5) |
| 6 | Phiếu xuất kho `LOAI = 2` gắn NCC có phải là "trả hàng nhà cung cấp" không? Có kiểm tra tồn kho / có cho `TIENTHANHTOAN` trên phiếu xuất không? Khi nào xuất khác gắn NCC, khi nào không? | **Trả lời một phần.** Chỉ có bằng chứng gián tiếp: `lueDNHACUNGCAPID` + `numTIENTHANHTOAN` trên form xuất kho, phép đảo dấu `LOAI = 2` trong báo cáo NCC. Tên gọi/quy trình chính xác và kiểm tra tồn kho: chưa có code nào ghi chữ "trả hàng NCC" — cần test live | (b.2) |
| 7 | Tiền đặt trước (tạm ứng) sau khi chốt bill có làm giảm số nợ hiển thị trong báo cáo không? | **Không.** Phiếu đặt trước bị loại khỏi `GetNoCu` (`TDATHANGID IS NOT NULL`); `TIENTHANHTOAN` ghi lên bill = `TienThanhToan − DatTruoc` nên số dư bill không phản ánh đặt trước. Cần test live 1 ca đầu-cuối để chốt hành vi hiển thị | (b.5) |
| 8 | `CHITIETCONGNOEX` trong spec §4.4 là gì? | **Trả lời một phần (đã loại trừ).** Không tồn tại trong DEMO (không có trong `00_counts.txt`, grep toàn bộ `raw/` không thấy). Có thể là tên ở schema TEA hoặc tên cũ của `TCHITIETTHANHTOAN` — chưa xác định được bản chất | Vòng 1 §8 + kiểm lại toàn bộ cây `/tmp/deep/` |
| 9 | `TDONHANG_TRIGGER` (trigger duy nhất trong DB) làm gì — có chạm `TIENTHANHTOAN`/`CONGNO`/`DATHANHTOAN` không? | **Đã trả lời.** Chỉ quản lý `DBAN.TDONHANGID` khi trạng thái thanh toán đổi (trả bàn về trống / gán bill mới). Không chạm `TIENTHANHTOAN`/`CONGNO`/`TCHITIETTHANHTOAN` | (b.15) — source đọc từ `RDB$TRIGGERS` |
| 10 | Công thức chi tiết màn KH "theo đơn" có đúng là `TONGCONG − TIENTHANHTOAN` không (có lọc `DATHANHTOAN`/`LOAI` gì thêm không)? | **Đã trả lời (công thức chính).** Đúng là `TONGCONG − TIENTHANHTOAN`; có cấu hình lọc `CONDITION = (LOAI = 0 OR LOAI = 99)` (COLID `2f410f17`, `CHECKED = false` — chưa rõ có bật mặc định không). Không thấy lọc `DATHANHTOAN` ở DETAIL này | (b.15) |
| 11 | Trả nợ NCC làm thế nào — chỉ có phiếu chi thường, hay có màn riêng? Phiếu chi có `TONGCONGNO` tương tự phiếu thu không? | **Đã trả lời.** Chỉ có phiếu chi thường (`TTHUCHI` `LOAI = 1`); không có form "Phiếu chi công nợ". Phiếu chi **có** nhận `TONGCONGNO` → `numCHI` (`mapper_OnSetValue`) | `TTHUCHI1Ae.mapper_OnSetValue` |
| 12 | Giá trị thật của `ChoPhepKhachNo` (và các config nhóm Thanh toán) trên DB POS TAP là gì? | **Chưa trả lời.** Giá trị DEMO = 0 (tắt bán chịu). POS TAP có ít hơn DEMO 7 config — cần DB POS TAP thật | — |

### c.2. Nhóm QUỸ (Q1–Q16)

| # | Câu hỏi vòng 1 | Đáp án vòng 2 | Bằng chứng |
|---|---|---|---|
| Q1 | Số phiếu sinh tự động theo định dạng nào? Tiền tố `PT`/`PC` theo `LOAI`? Năm `20` là gì? Dãy số chạy theo năm/cửa hàng/toàn cục? Sinh khi mở form hay khi lưu? Có cho sửa tay không? | **Trả lời một phần.** Cơ chế: cờ `STABLEDESC.GENNO = 30` ở `TTHUCHI` → framework tự sinh; `NAME` bắt buộc + không trùng; không có code sinh số trong bản xuất; nhóm cấu hình "Số phiếu" trống trong DEMO; `TTHUCHI` 0 dòng nên mẫu `PT20/00001` không đối chiếu được. Định dạng/tiền tố/thời điểm sinh/sửa tay: còn mở, nằm trong No1Lib | (b.6) |
| Q2 | Phiếu thu công nợ mở từ đâu, và lưới chi tiết ghi đi đâu? `TDONHANG.TIENTHANHTOAN` có tăng theo `LANNAY` khi lưu phiếu không? | **Trả lời một phần.** Phần "ghi đi đâu": đáp án phủ định mạnh — không có code nào ghi `TCHITIETTHANHTOAN` hay cộng `TIENTHANHTOAN` (xem (b.3)). Phần "mở từ đâu": vẫn chưa rõ (không caller, không menu, AELAYOUT không có nút) | (b.3) |
| Q3 | Luồng nạp thẻ trả trước vào phiếu thu bằng đường nào? Nơi nào gọi `SetNapThe()`? Có gán sẵn lý do "Nạp thẻ trả trước" và `DTHETRATRUOCID` không? | **Đã trả lời.** Caller trong `STABLEDESC` (`DTHETRATRUOCAe.btnNapTien_Click` / `btnChiTra_Click`): gán `DTHETRATRUOCID`, lý do gần nhất/fallback "Nạp thẻ trả trước"/"Chi trả thẻ trả trước", `DIENGIAI`, rồi `SetNapThe()` | (b.10) |
| Q4 | Nhãn và ý nghĩa các giá trị `LOAILYDO` (−1/0/1/2/NULL) là gì? | **Đã trả lời.** Combo: [0] "Không lên báo cáo kết quả kinh doanh" → −1, [1] "Chi phí cố định (chi phí phát sinh hàng tháng)" → 1, [2] "Chi phí khác" → 2. KQKD dùng 1 (định phí) và 2 (chi khác). 0 = "Tạm ứng" (không chọn được từ combo); NULL = công nợ | (b.9) |
| Q5 | Câu SQL gốc của màn Tồn quỹ là gì? Nhóm theo ngày ra sao, cột Ngày lấy `NGAY` hay `TIMECREATED`, khoảng ngày mặc định thế nào, có số dư đầu kỳ/cuối kỳ không? | **Trả lời một phần.** Đã có SQL báo cáo đầy đủ + toàn bộ luật sửa chuỗi của handler; SQL gốc của màn hình (lớp `TonQuy` framework) vẫn chưa đọc được — chỉ biết 3 anchor chuỗi bị thay (`" UNION ALL"`, `"(TDONHANG.TIENTHANHTOAN <>0)"`, `"COALESCE(TIENMAT,0) + COALESCE(CHUYENKHOAN,0) + COALESCE(THE,0)"`), suy ra SQL màn hình đơn giản hơn SQL báo cáo | (b.7) |
| Q6 | Danh mục phiếu thu/chi lọc `LOAI` bằng cơ chế nào? Phiếu thu công nợ (`LOAI = 2`) xem ở đâu? | **Trả lời một phần.** Handler không lọc `LOAI` → suy đoán framework tự áp theo `LOAI` của form (chưa chứng minh). Phiếu `LOAI = 2` không hiện ở 2 danh mục (lọc `LATAMUNG` + `LOAI` form); xem ở đâu: chưa rõ, không có danh mục riêng | `quy.md` §5 + kiểm lại `TTHUCHI0MgHandler`/`TTHUCHI1MgHandler` |
| Q7 | Function "Thu chi" (`5d0b8bb7`) khóa cái gì, và từng nhóm người dùng có quyền Quỹ nào? | **Trả lời một phần.** Đã map `SGROUPROLE`: Kế toán + Quản lý MODE 240 trên 4 function Quỹ (16 trên đổi ngày); Phục vụ 0; Thu ngân/Thủ kho không có dòng. "Thu chi" khóa cái gì: chưa chứng minh (suy đoán mạnh: khóa 2 menu "Tạo phiếu thu/chi" loại 1 không có `FUNCTIONID`) | (b.12) |
| Q8 | Trường bắt buộc khi lưu phiếu gồm những gì? `SCOLUMN` đánh dấu bắt buộc cả `NGAY`, `TENDOITUONG`, `DIENGIAI`, `DLYDOTHUCHIID`, pilot đếm theo `AELAYOUT` chỉ có 3 trường. Thông báo lỗi khi thiếu trường là gì? | **Đã trả lời (phần chính).** Theo AELAYOUT đầy đủ: 3 trường bắt buộc — `NAME` (Số phiếu), `THU`/`CHI` (Số tiền), `DCUAHANGID` (Cửa hàng). Cờ `NO` của `SCOLUMN` không quyết định validation trên form này. Thông báo lỗi khi thiếu từng trường: chưa đọc được — cần test live | (b.14) |
| Q9 | Ngày mặc định của phiếu mới là ngày nào? | **Chưa trả lời.** Không có `DefaultValue` trong AELAYOUT phiếu thu/chi (0 kết quả); nhiều khả năng framework mặc định `NGAY` = hôm nay — chưa chứng minh được | (b.14) |
| Q10 | 6 báo cáo thu chi tổng hợp/danh sách tính số liệu ra sao? | **Trả lời một phần.** `SREPORT.SQL = NULL` với cả 6 (46/128 báo cáo không có SQL); `.config` chỉ có cột + `NHOM1` nhóm theo ngày/lý do + `TONGCONG` tổng; SQL do framework tự sinh từ `COLCONFIG` + `FILTERCONFIG` (preset ngày). Công thức tổng hợp chính xác và cơ chế lọc `LOAI`: không đọc được | (b.13) |
| Q11 | Công nợ nhà cung cấp tính thế nào? Có hàm tương tự `GetNoCu` cho NCC không? | **Đã trả lời.** Không có hàm `GetNoCu` cho NCC. Công nợ NCC tính bằng 4 nguồn (màn hình, tổng hợp, kỳ, đối chiếu) — công thức + verdict dấu xem (b.2) | (b.2) |
| Q12 | `GetNoCu` loại phiếu có `TDATHANGID`, báo cáo đối chiếu thì không — cố ý hay lệch? | **Đã trả lời (kèm lý giải).** Khác nhau đã xác nhận. Lý giải hợp lý có bằng chứng: `GetNoCu` phục vụ "nợ cũ trên hóa đơn mới" — đặt trước đã được trừ riêng qua `GetDatTruoc()` (`TONGTIEN = bill + NoCu − datTruoc`) nên loại khỏi `GetNoCu` để tránh trừ trùng; đối chiếu là sao kê đầy đủ nên không loại. Cố ý hay vô tình thì chỉ Chiến/test live mới chốt được, nhưng lý giải trên đủ mạnh để giữ nguyên khi clone | (b.1), (b.5) |
| Q13 | Tạo lý do mới từ Phiếu thu có gán sẵn `LALYDOTHU = 30` không? (Phiếu chi có gán sẵn `0`) | **Đã trả lời.** Phiếu thu không có override màn quản lý lý do như phiếu chi; nhưng form lý do mới mặc định là lý do thu (`chkLALYDOTHU.Checked = true` khi tạo mới) nên lý do tạo từ Phiếu thu mặc định là thu | `DLYDOTHUCHIAe` |
| Q14 | `DLOAIQUY` có còn dùng ở đâu không? Một `TDATHANGID` có thể có nhiều phiếu đặt trước không (`GetDatTruoc` chỉ lấy dòng đầu)? | **Đã trả lời cả hai nửa.** (i) `DLOAIQUY` **được dùng** — lookup `@DLOAIQUYID` của `BÁO CÁO TỒN QUỸ` (ID `'0'`/`'1'`/`'2'`). (ii) `GetDatTruoc` chỉ lấy dòng đầu; nhưng flow đặt hàng mở lại phiếu cũ nên thực tế 1 phiếu/1 đặt hàng qua flow đó; tạo tay qua luồng khác vẫn có thể sinh nhiều phiếu | (b.8), (b.5) |
| Q15 | Phiếu đã xóa mềm (`STATUS = -30`) còn tính vào nợ / tồn quỹ / báo cáo không? | **Trả lời một phần.** `BÁO CÁO TỒN QUỸ` **không lọc `STATUS`** (cả 2 nhánh) → phiếu xóa mềm vẫn tính vào báo cáo tồn quỹ. Các công thức công nợ (`GetNoCu`, báo cáo kỳ/tổng hợp/đối chiếu) cũng không lọc `STATUS`. Màn hình Tồn quỹ: chưa rõ (SQL gốc trong framework) | (b.7), (b.1), (b.2) |
| Q16 | Phiếu thu công nợ có cho chuyển khoản không? (Form có `chkCHUYENKHOAN` nhưng không có ô chọn tài khoản ngân hàng; không có kiểm tra `BeforeSave`.) | **Đã trả lời (từ code).** Form có `chkCHUYENKHOAN` nhưng không có lookup `DTAIKHOANNGANHANGID` và không có kiểm tra `BeforeSave` → tick chuyển khoản được nhưng không chọn được tài khoản. Tiền sẽ vào quỹ theo nhánh `CHUYENKHOAN = 30` mà không gắn tài khoản — cần test live xem có lưu được không và tồn quỹ hiển thị ra sao | `TTHUCHI2Ae` DESIGNCODE/AELAYOUT |

## (d) Câu hỏi còn lại sau vòng 2

### d.1. Chưa trả lời được (2 câu)

| # | Câu hỏi | Vì sao vẫn mở |
|---|---|---|
| CN-12 | Giá trị thật của `ChoPhepKhachNo` (và các config nhóm Thanh toán) trên DB POS TAP là gì? | Cần DB POS TAP thật; DEMO = 0 (tắt bán chịu), POS TAP có ít hơn DEMO 7 config |
| Q9 | Ngày mặc định của phiếu mới là ngày nào? | Không có `DefaultValue` trong AELAYOUT; nằm trong framework (nhiều khả năng = hôm nay, chưa chứng minh) |

### d.2. Trả lời một phần — phần còn mở (11 câu)

| # | Phần còn mở |
|---|---|
| CN-2 | Màn "theo đơn" mở bằng cách nào (tab / double-click / nút)? Nút lập phiếu thu công nợ nằm ở đâu? |
| CN-3 | `LANNAY` mặc định bằng số còn nợ hay 0? Có chặn `LANNAY` vượt số còn nợ không? Xóa/sửa phiếu thu công nợ đã lưu thì `TIENTHANHTOAN` có hoàn lại không? (Phụ thuộc câu 1: nếu framework làm ngầm thì các câu này mới có ý nghĩa) |
| CN-6 | Phiếu xuất kho `LOAI = 2` gắn NCC có phải "trả hàng NCC" không? Có kiểm tra tồn kho không? Khi nào xuất khác gắn NCC, khi nào không? |
| CN-8 | `CHITIETCONGNOEX` rốt cuộc là gì (bảng/view schema TEA, hay tên cũ của `TCHITIETTHANHTOAN`)? |
| Q1 | Định dạng số phiếu (`PT`/`PC`? năm `20`? dãy theo năm/cửa hàng/toàn cục? sinh khi mở form hay khi lưu? sửa tay được không?) |
| Q2 | Phiếu thu công nợ mở từ đâu (màn/men/nút nào truyền `DETAILDATA`)? |
| Q5 | SQL gốc đầy đủ của màn hình Tồn quỹ (lớp `TonQuy` framework): nhóm theo ngày ra sao, cột Ngày lấy `NGAY` hay `TIMECREATED`, khoảng ngày mặc định, có số dư đầu/cuối kỳ không? |
| Q6 | Cơ chế lọc `LOAI` ở Danh mục phiếu thu/chi (framework tự áp theo `LOAI` form — chưa chứng minh); phiếu thu công nợ (`LOAI = 2`) xem ở đâu? |
| Q7 | Function "Thu chi" (`5d0b8bb7`) khóa cái gì (suy đoán: 2 menu "Tạo phiếu thu/chi" — chưa chứng minh)? |
| Q10 | Công thức tổng hợp chính xác + cơ chế lọc `LOAI` của 6 báo cáo thu chi (SQL do framework tự sinh, không đọc được)? |
| Q15 | Phiếu xóa mềm có tính vào **màn hình** Tồn quỹ không (SQL gốc màn hình chưa đọc được)? |

### d.3. Câu chặn cần xác minh bằng app gốc (ưu tiên cao nhất)

1. **Phiếu thu công nợ có trừ nợ thật không?** Lập 1 phiếu thu công nợ trên app gốc rồi đọc `TIENTHANHTOAN` của bill + `TCHITIETTHANHTOAN` + tổng `TỔNG_HỢP_CÔNG_NỢ_KHÁCH_HÀNG` trước/sau khi lưu và sau khi xóa phiếu. (Quyết định toàn bộ thiết kế công nợ của bản clone: nếu app gốc thật sự không trừ nợ thì đây là bug gốc phải sửa, không được sao chép.)
2. **Bản tổng hợp NCC có ra 14 triệu không?** Mua chịu 10 triệu, trả 4 triệu bằng phiếu chi, đọc `TỔNG_HỢP_CÔNG_NỢ_NHÀ_CUNG_CẤP` (kỳ vọng bug: 14 triệu) vs màn hình + báo cáo kỳ + đối chiếu (6 triệu).
3. **Tồn quỹ `LOAI = 2`:** lập phiếu xuất trả hàng NCC có `TIENTHANHTOAN ≠ 0`, xem tồn trong kỳ (kỳ vọng: tăng) vs tồn đầu kỳ sau khi qua ngày (kỳ vọng: giảm).
4. **Báo cáo kỳ KH/NCC:** chạy báo cáo theo kỳ có phát sinh trong kỳ, xem `MUA`/`THANHTOAN` có tính đúng không (kiểm tra lỗi `@TODATE` có làm mất dữ liệu trong kỳ không).
5. **Đặt trước đầu-cuối:** đặt trước 2 triệu cho bill 10 triệu, chốt bill trả thêm 8 triệu, đọc báo cáo công nợ KH (kỳ vọng: vẫn hiện nợ 2 triệu).

## (e) Nghi vấn giống bug — cập nhật sau vòng 2

| # | Nghi vấn | Mức độ | Bằng chứng |
|---|---|---|---|
| BUG-1 | **`TỔNG_HỢP_CÔNG_NỢ_NHÀ_CUNG_CẤP` đảo dấu nhánh `TTHUCHI`** (`CHI − THU` thay vì `THU − CHI`) + thiếu lọc `KHONGTHAYDOICONGNO` → trả tiền làm nợ tăng (scenario 10M/4M → 14M thay vì 6M) | **Cao — sai số tiền** | (b.2); SQL nguyên văn; 3 nguồn còn lại nhất quán hướng ngược lại |
| BUG-2 | **`BÁO CÁO TỒN QUỸ` mâu thuẫn `LOAI = 2`:** SQL chính tính +`TIENTHANHTOAN` vào THU, `AfterGetData` tồn đầu kỳ tính −`TIENTHANHTOAN` | **Cao — sai dấu theo thời gian** | (b.7); `LOAI = 1` nhất quán nên đây không phải quy ước chung |
| BUG-3 | **Lỗi `@TODATE`** (viết hoa) ở `MUATRONGKY`/`THANHTOANTRONGKY1` của cả 2 báo cáo công nợ theo kỳ (KH + NCC), trong khi tham số khai báo `@ToDate` | **Cao nếu framework phân biệt hoa/thường — có thể làm mất toàn bộ phát sinh trong kỳ** | (b.11); cần test live (câu d.3 #4) |
| BUG-4 | **Phiếu thu công nợ không giảm bất kỳ số nợ nào** (nếu framework không làm ngầm): tiền vào quỹ nhưng nợ khách không giảm ở `GetNoCu`, màn hình, mọi báo cáo | **Cao — sai nghiệp vụ cốt lõi, nếu đúng** | (b.3); cần test live (câu d.3 #1) — nếu framework có làm ngầm thì rút lại |
| BUG-5 | **Màn hình NCC không đảo `TIENTHANHTOAN` khi `LOAI = 2`, 3 báo cáo đều đảo** (3-chọi-1 nghiêng về đảo) | Trung bình | (b.2); cần test live với phiếu xuất trả hàng có `TIENTHANHTOAN ≠ 0` |
| BUG-6 | **Phiếu THU gắn NCC làm nợ tăng** theo hướng `THU − CHI` của màn hình/báo cáo kỳ/đối chiếu (hoàn tiền từ NCC lẽ ra phải làm giảm nợ) | Trung bình — cần test live ca hoàn tiền | (b.2) |
| BUG-7 | **Đối chiếu KH lọc `LOAI` rộng hơn báo cáo kỳ KH** (`((LOAI = 0 AND DATHANHTOAN = 30) OR LOAI <> 0)` vs `LOAI 0/99 + DATHANHTOAN = 30`) → hai báo cáo lệch nhau nếu KH có bill `LOAI 1/2/97/98` | Trung bình | (b.1), (b.11); đã có từ vòng 1, giữ nguyên |
| BUG-8 | **`GetDatTruoc` chỉ lấy dòng đầu** (`GetFirstFieldDec`): nhiều phiếu đặt trước cho cùng 1 `TDATHANGID` thì các phiếu sau bị bỏ qua | Thấp — flow đặt hàng mở lại phiếu cũ nên thực tế khó xảy ra qua flow đó | (b.5) |
| BUG-9 | **`BÁO CÁO TỒN QUỸ` không lọc `STATUS`** → phiếu thu/chi đã xóa mềm vẫn tính vào tồn quỹ | Trung bình | (b.7); các công thức công nợ cũng không lọc `STATUS` |
| BUG-10 | **Nút "Quẹt thẻ" loại toàn bộ phiếu thu/chi** (`0 = 1`) — phiếu thu chuyển khoản không được tính vào đây; chỉ tiền bán hàng (`TDONHANG.THE`) được tính | Thấp — có thể là cố ý (phiếu thu không có quẹt thẻ), cần Chiến xác nhận | (b.7) |
| BUG-11 | **Phiếu thu công nợ tick chuyển khoản được nhưng không chọn được tài khoản ngân hàng** (không lookup, không `BeforeSave` check) | Trung bình | (c.2) Q16 |
| BUG-12 | **Đặt trước không làm giảm nợ báo cáo** (`TIENTHANHTOAN = TienThanhToan − DatTruoc`): khách đã trả đủ (đặt trước + tiền mặt) mà báo cáo vẫn hiện nợ phần đặt trước | Trung bình — cần test live đầu-cuối | (b.5); câu d.3 #5 |
| — | **Rút lại / sửa lại so với vòng 1 + Claude:** (i) Claude mục #8 "dấu công nợ KH lệch giữa 2 bản" — **không lệch**, hai biểu thức tương đương (xem (a.2) #21); (ii) vòng 1 "GetNoCu không trừ phiếu thu thường" — **sai**, `GetNoCu` có trừ (xem (a.3)); (iii) vòng 1 "`DLOAIQUY` là bảng thừa" — **sai**, bảng được dùng làm lookup của BÁO CÁO TỒN QUỸ (xem (b.8)); (iv) vòng 1 "không tìm thấy nơi gọi `SetNapThe`" — **đã tìm thấy**, caller nằm trong `STABLEDESC` (xem (b.10)) | — | — |

## Tổng kết số câu vòng 1 đã trả lời được

- **Nhóm CÔNG NỢ (12 câu): trả lời dứt khoát 7** (câu 1, 4, 5, 7, 9, 10, 11); **trả lời một phần 4** (câu 2, 3, 6, 8); **chưa trả lời 1** (câu 12).
- **Nhóm QUỸ (16 câu): trả lời dứt khoát 8** (Q3, Q4, Q8, Q11, Q12, Q13, Q14, Q16); **trả lời một phần 7** (Q1, Q2, Q5, Q6, Q7, Q10, Q15); **chưa trả lời 1** (Q9).
- **Tổng: 15/28 câu trả lời dứt khoát; 11/28 trả lời một phần; 2/28 chưa trả lời được.**

## Ghi chú phương pháp (để vòng sau không lặp lại sai lầm)

- Cây giải mã đúng đã dựng tại `/tmp/deep/`: file `sform_code/*.txt` có cấu trúc header + `===== CODE =====` + code C# dạng text thường + `===== DESIGNCODE =====` + literal `b'...'` + `===== AELAYOUT…` + literal `b'...'`; **một số file có cả phần CODE là literal `b'...'`** (vd `Sử_dụng_dịch_vụ.txt`) — lần giải mã đầu bỏ phần text thường nên file bị cắt (vd `Hóa_đơn_bán_hàng.txt` chỉ còn 4.077 byte, bản đúng 108.505 byte). Grep từ khóa ASCII trên file raw vẫn đúng, nhưng mọi kết luận "không tìm thấy" trước khi dựng lại cây `/tmp/deep/` đều phải kiểm tra lại — vòng 2 đã kiểm lại các kết luận phủ định quan trọng (`TCHITIETTHANHTOAN`, `DETAILDATA`, `SetNapThe`, `TaoSoHoaDon`) trên cây mới + trực tiếp trên DB.
- Mọi kết luận "không có caller" chỉ dựa trên `SFORM` đều phải kiểm tra lại với `stablesdesc.txt` (bài học `SetNapThe`: caller nằm trong `STABLEDESC`, không phải `SFORM`).
- `/tmp` là ephemeral (VM có thể bị thay): cây `/tmp/deep/` và hạ tầng Firebird 2.5.9 tại `/tmp/fb25/` + venv `~/workspace/fnb/venv` cần dựng lại nếu mất; dữ liệu gốc bền vững nằm ở `~/workspace/fnb/pos-tap-review/raw/` và `~/workspace/fnb/dbs/FNB_DEMO.fdb`.
