# Checklist test bằng app gốc POS TAP — 55 câu còn mở sau vòng 2

- **Ngày lập:** 19/11/2026. Nguồn danh sách: mục 5 của [tong-hop-ra-soat-vong-2.md](tong-hop-ra-soat-vong-2.md) (7 câu chặn) + mục (d) của 7 file `deep/*.md`. Con số: tổng 241 câu vòng 1, vòng 2 trả lời 186, **còn 55** = Bán hàng 5 + Khách hàng 18 + Kho 4 + Công nợ/Quỹ 2 + Nhân sự 1 + Quản trị 5 + Báo cáo 20.
- Mỗi câu ghi: **thao tác test cụ thể** trên app gốc → **DB cần đọc sau test** → **cách đọc kết quả**. Cuối mỗi câu có dòng `Kết quả:` để điền (khớp / khác / ngày).
- Câu nào test app **không** trả lời được (nằm trong DLL/APK, hoặc là quyết định của Chiến) thì ghi rõ và nói ai/cái gì trả lời được — không bịa thao tác.
- ⭐ = thuộc 55 câu. Không ⭐ = vế mở lẻ của câu đã trả lời một phần (Phần C), nên test cùng lượt cho tiện.
- Tài liệu nghiệp vụ đã ghi **[Chưa chốt]** kèm ID trong file này (vd `CN-CHAN1`, `BC-Q4`, `KHO-R3`).

## 0. Chuẩn bị (làm một lần)

1. **Không test trên `POS TAP\Database\DATA.fdb` gốc** (file này mới bị chạm hôm nay 15:58; CLAUDE.md cấm sửa). Sao cả thư mục `POS TAP\` sang chỗ khác (vd `D:\Test\POS_TAP_TEST\`) rồi test trên bản sao. Đóng hẳn app trước khi mở DB bằng công cụ khác (Firebird embedded chỉ cho một tiến trình).
2. **Hai loại DB test** (đều sẵn trong thư mục):
   - **DB trống:** chép `POS TAP\Data\TEMPLATE.FDB` (19,5 MB) đè `Database\DATA.fdb` của bản sao → tự tạo dữ liệu theo ca chuẩn ở bước 4.
   - **DB có dữ liệu thật:** restore bản backup `POS TAP\Data\Backup\bak_02_02_2023.gbk` (13 MB, mới nhất) bằng `gbak` Firebird 2.5: `gbak -c -user SYSDBA -password masterkey bak_02_02_2023.gbk DATA_TEST.fdb` rồi đặt thành `Database\DATA.fdb` của bản sao. Dùng cho các câu cần dữ liệu lịch sử (báo cáo, công nợ, điểm tích lũy, `SELECT DISTINCT LOAI`).
   - Có thêm `Database\DEMO.FDB` (22 MB) và **`Database\ONLINE.FDB` (2 MB — chưa ai mở, xem QT-V3)**.
3. **Công cụ đọc DB:** (đã dựng sẵn một công cụ console x86 đọc được `DATA.fdb`/`DEMO.FDB` — xem `reference/db-pos-tap-do-truc-tiep.md`; Python 64-bit trên máy không nạp được `fbembed.dll` 32-bit) hoặc FlameRobin / `isql` 2.5 (user `SYSDBA`, mật khẩu mặc định `masterkey`, embedded dùng `fbembed.dll` 32-bit). Cách "trước/sau": chạy cùng một truy vấn trước và sau mỗi thao tác, hoặc chép file DB làm snapshot.
4. **Ca dữ liệu chuẩn** (tạo trên DB trống): 1 cửa hàng; kho `K1`, `K2` (`CHOPHEPAMKHO = 0`); mặt hàng `A` (kiêm vật tư, đơn vị lẻ "chai" + chẵn "thùng" = 12, giá nhập 10.000 / bán 15.000), `B` (pha chế, định lượng 2 vật tư), `C` (nguyên liệu); 1 nhóm hàng; khách `KH01`, nhà cung cấp `NCC01`; 2 tài khoản: `admin` và `thungan` (nhóm Thu ngân); 1 khu vực + 2 bàn. Chốt cấu hình trước khi test (ghi lại giá trị): `SuDung2DonViTinh`, `TuDongTinhGiaVon`, `ChoPhepKhachNo`, `CachTinhDiem`, `QuyDoi1DiemSangTien`, `GiaMatHangKhiChuyenBan`.
5. **Quy ước SQL mẫu (dùng lại trong checklist):**
   - Tồn kho 1 mặt hàng/kho: `SELECT COALESCE(SUM(COALESCE(SLNHAP,0)-COALESCE(SLXUAT,0)),0) FROM TDONHANGCHITIET C JOIN TDONHANG D ON D.ID=C.TDONHANGID WHERE C.DMATHANGID=:mh AND C.DKHOHANGID=:kho AND (D.LOAI<>0 OR D.DATHANHTOAN=30)`
   - Nợ khách (nguyên văn `GetNoCu`): `SELECT SUM(TONGCONG)-SUM(COALESCE(TIENTHANHTOAN,0)) FROM TDONHANG WHERE DKHACHHANGID=:kh AND DATHANHTOAN=30` rồi trừ `SELECT SUM(CASE WHEN LOAI=0 THEN THU ELSE -CHI END) FROM TTHUCHI WHERE DKHACHHANGID=:kh AND COALESCE(KHONGTHAYDOICONGNO,0)=0 AND TDATHANGID IS NULL`
   - Dấu vết loại phiếu: `SELECT LOAI, COUNT(*) FROM TDONHANG GROUP BY LOAI`
6. **Chụp màn hình** mọi thông báo lỗi/hộp thoại (nguyên văn tiếng Việt) và lưu cùng câu hỏi.

## A. Bảy câu chặn (làm trước — quyết định thiết kế lõi)

Thứ tự theo mức ảnh hưởng. Câu A1–A3 bắt buộc xong **trước khi viết code thanh toán/công nợ**.

### A1. Trừ kho thực sự xảy ra ở đâu + có transaction khi thanh toán? — `BH-Q13` ⭐ (gồm Q13/Q19 vế lẻ của bán hàng)
- **Thao tác (kho):** nhập kho `K1` 10 chai `A` (phiếu nhập). Bán lẻ 3 chai `A` (tiền mặt, thanh toán xong). Mở màn **Tồn kho** `K1`.
- **DB sau test:** chạy truy vấn tồn kho mẫu cho `A`/`K1`; `SELECT DKHOHANGID, SLXUAT, SLNHAP FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`; so sánh `DMATHANG` (mọi cột) trước/sau để xem có cột tồn nào bị `UPDATE`.
- **Đọc:** tồn giảm 3 mà `DMATHANG` không đổi và chỉ có dòng `SLXUAT=3` → tồn là **truy vấn** (không có UPDATE tồn; trả lời vế "trừ kho ở đâu").
- **Thao tác (transaction):** trên DB test tạo trigger làm lỗi giữa chừng: `CREATE EXCEPTION E_TEST 'loi'; CREATE TRIGGER T_TEST FOR TDONHANGCHITIET ACTIVE BEFORE INSERT POSITION 0 AS BEGIN IF (NEW.XUATVATTU = 30) THEN EXCEPTION E_TEST; END` (làm lỗi đúng khi `XuatVatTu` sinh dòng vật tư). Bán 1 món `B` (pha chế, có định lượng) và thanh toán.
- **DB sau test:** `SELECT DATHANHTOAN, TIENTHANHTOAN, GIOTHANHTOAN FROM TDONHANG WHERE ID=:bill`.
- **Đọc:** nếu bill đã `DATHANHTOAN=30` dù `XuatVatTu` lỗi → **không có transaction bao quanh** (thanh toán và xuất vật tư ghi rời nhau); nếu bill vẫn chưa thanh toán → có transaction. Xóa trigger/exception sau test.
- Kết quả: ______

### A2. Phiếu thu công nợ có trừ nợ thật không? — `CN-CHAN1` ⭐ (CN-2/CN-3/Q2 vế lẻ)
- **Thao tác:** `KH01` mua chịu (bật `ChoPhepKhachNo`): bán lẻ 100.000, khách đưa 0, tick nợ. Mở **Công nợ khách hàng** → mở màn **theo đơn** → lập **Phiếu thu công nợ** trả 40.000 cho bill đó. Ghi lại: nút lập phiếu nằm ở đâu (tab/nút/nhấp đúp) — trả lời cả CN-2. Thử sửa ô `LANNAY` trong lưới (lưới có `ReadOnly`), thử nhập lớn hơn số còn nợ.
- **DB trước/sau:** `SELECT TONGCONG, TIENTHANHTOAN, CONNO FROM TDONHANG WHERE ID=:bill`; `SELECT * FROM TTHUCHI WHERE LOAI=2`; `SELECT COUNT(*) FROM TCHITIETTHANHTOAN` (nếu bảng tồn tại); nợ khách bằng truy vấn `GetNoCu` mẫu; chạy lại báo cáo **TỔNG HỢP CÔNG NỢ KHÁCH HÀNG** và **CÔNG NỢ KHÁCH HÀNG** (kỳ có phát sinh).
- **Đọc:** `TIENTHANHTOAN` của bill tăng 40.000 hoặc có dòng `TCHITIETTHANHTOAN` → framework làm ngầm, nợ giảm (rút lại nghi bug BUG-4). Không đổi gì và nợ vẫn 100.000 → **bug gốc**: tiền vào quỹ nhưng nợ không giảm; clone không sao chép.
- **Thao tác phụ (CN-3):** xóa phiếu vừa lập → `TIENTHANHTOAN` có hoàn lại? Thử lập phiếu thu công nợ **chuyển khoản** (tick, kiểm tra không có ô chọn tài khoản) và xem lưu được không.
- Kết quả: ______

### A3. `LOAI = 5/6` là phiếu gì? — `BC-Q4` ⭐ (cũng là câu lớn nhất của nhóm Báo cáo)
- **Thao tác:** trên **DB có dữ liệu thật** (backup 2023) chạy `SELECT LOAI, COUNT(*), MIN(NGAY), MAX(NGAY) FROM TDONHANG GROUP BY LOAI`. Nếu có `LOAI` 5/6: `SELECT * FROM TDONHANG WHERE LOAI IN (5,6) ROWS 5` rồi xem `TDATHANGID`, `DBANID`, `NAME` để suy ra luồng.
- **Nếu DB thật không có:** trên DB test thử lần lượt các luồng có thể sinh loại mới (đặt phòng/đặt hàng rồi chốt bill; đặt trước; nhập trả hàng NCC; bill tách `NHOMGUID`; "Điều chỉnh hóa đơn"), sau mỗi luồng chạy lại `SELECT DISTINCT LOAI FROM TDONHANG`.
- **Đọc:** xác định luồng sinh ra 5/6 → ghi vào mục lục §2. Nếu không luồng nào sinh ra và DB thật cũng không có → loại 5/6 là di sản, clone chỉ cần giữ điều kiện đọc trong báo cáo, không sinh.
- Kết quả: ______

### A4. Tổng hợp công nợ NCC có ra 14M không? — `CN-CHAN2`
- **Thao tác:** nhập kho từ `NCC01` giá trị 10.000.000, thanh toán 0 (còn nợ). Lập **Phiếu chi** 4.000.000 cho `NCC01` (đối tượng nhà cung cấp, không tick `KHONGTHAYDOICONGNO`).
- **Đọc:** chạy 4 nguồn công nợ NCC: màn **Công nợ nhà cung cấp**, báo cáo **TỔNG HỢP CÔNG NỢ NHÀ CUNG CẤP**, **CÔNG NỢ NHÀ CUNG CẤP** (theo kỳ), **ĐỐI CHIẾU CÔNG NỢ NHÀ CUNG CẤP**. Kỳ vọng nếu đúng nghiên cứu: ba nguồn ra **6.000.000**, riêng tổng hợp ra **14.000.000**. Thử thêm: phiếu chi tick `KHONGTHAYDOICONGNO` → tổng hợp có bỏ qua không (nghi: không).
- **DB:** `SELECT LOAI, THU, CHI, KHONGTHAYDOICONGNO FROM TTHUCHI WHERE DNHACUNGCAPID=:ncc`.
- Kết quả: ______

### A5. Tồn quỹ với phiếu `LOAI = 2` có NCC — `CN-CHAN3`
- **Thao tác:** lập phiếu **xuất khác** (trả hàng NCC) chọn `NCC01`, `TIENTHANHTOAN` = 500.000, ngày hôm nay. Mở báo cáo **TỒN QUỸ** kỳ gồm hôm nay (xem phát sinh trong kỳ). Sau đó chạy báo cáo với kỳ **bắt đầu sau ngày phiếu** (để phiếu rơi vào tồn đầu kỳ) và ghi tồn đầu.
- **Đọc:** trong kỳ phiếu tăng quỹ (+500.000) còn tồn đầu kỳ lại trừ (−500.000)? Nếu đúng → mâu thuẫn dấu theo thời gian (BUG-2) xác nhận. So sánh thêm với màn hình **Tồn quỹ** (SQL khác).
- Kết quả: ______

### A6. `@TODATE` (hoa) trong báo cáo công nợ theo kỳ — `CN-CHAN4`
- **Thao tác:** dữ liệu có phát sinh **trong kỳ** (bill bán chịu + phiếu thu giữa `Từ ngày`/`Đến ngày`). Chạy **CÔNG NỢ KHÁCH HÀNG** và **CÔNG NỢ NHÀ CUNG CẤP** (theo kỳ). Đối chiếu cột `MUA`/`THANHTOAN` với tổng tay tính từ `TDONHANG`/`TTHUCHI`.
- **Đọc:** nếu `MUA`/`THANHTOAN` = 0 hoặc thiếu so với số tay tính → lỗi hoa/thường làm mất phát sinh (BUG-3). Nếu khớp → framework không phân biệt hoa/thường, bỏ nghi vấn.
- Kết quả: ______

### A7. Đặt trước đầu–cuối — `CN-CHAN5`
- **Thao tác:** tạo phiếu đặt hàng/phòng (nhánh `HOTEL`/đặt trước), thu đặt trước 2.000.000 (phiếu thu có `TDATHANGID`); mở bill nhà hàng từ phiếu đặt đó (tổng 10.000.000), chốt trả thêm 8.000.000 tiền mặt.
- **DB:** `SELECT TONGCONG, TIENTHANHTOAN, DATTRUOC, CONNO FROM TDONHANG WHERE ID=:bill`; báo cáo **CÔNG NỢ KHÁCH HÀNG** kỳ chứa bill.
- **Đọc:** `TIENTHANHTOAN = 8.000.000` và báo cáo vẫn hiện nợ 2.000.000 → BUG-12 xác nhận (đặt trước không giảm nợ báo cáo). Ghi thêm: `GetNoCu` trên hóa đơn mới của khách có loại phiếu đặt trước ra không.
- Kết quả: ______

## B. 55 câu còn mở, theo nhóm

### B1. Bán hàng (5 câu)

#### ⭐ BH-Q7 — Công thức `Shared.TinhGia` (tiền giờ) + `GetNoCu` 
- **Trạng thái:** **ĐÓNG — không cần test.** Tiền giờ đã chốt bỏ khỏi clone. `GetNoCu` đã đọc nguyên văn trên `POS_TAP_SRC_EXPORT` (xem [thu-chi-cong-no](pos-tap-nghiep-vu-thu-chi-cong-no.md) §3). Chỉ test lại nếu Chiến đổi quyết định bỏ tiền giờ (cần decompile `No1Lib.Sys.dll` hoặc đối chiếu bill giờ).

#### ⭐ BH-Q14 — Cơ chế "dừng tính giờ khi in tạm tính" (`TUTHAYDOIGIO`, `INTAMTINHLUC`, `SOLANINTAMTINH`)
- **Trạng thái:** **ĐÓNG theo phạm vi** (tính giờ đã bỏ). Phần `SOLANINTAMTINH`/`INTAMTINHLUC` còn dùng ở bản không giờ: thao tác kiểm tra nhanh — in tạm tính bill, `SELECT SOLANINTAMTINH, INTAMTINHLUC FROM TDONHANG WHERE ID=:bill` (tăng 1, ghi giờ); in tạm tính lần 2 khi `SoLanInTamTinhToiDa`=1 → có chặn không.

#### ⭐ BH-Q29 — Chế độ "Điều chỉnh hóa đơn": bấm Thanh toán thì làm gì?
- **Thao tác:** thanh toán xong 1 bill nhà hàng (có số hóa đơn). Vào **Quản lý bán hàng nhà hàng** → mở bill ở chế độ **Điều chỉnh** → sửa số lượng 1 dòng → bấm **Thanh toán**.
- **DB trước/sau:** `SELECT TONGCONG, TIENTHANHTOAN, GIOTHANHTOAN, DATHANHTOAN, SOTT, NGAY FROM TDONHANG WHERE ID=:bill`; `SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`.
- **Đọc:** `GIOTHANHTOAN`/`SOTT` có đổi không, có in lại bill không, có mở hộp xác nhận thanh toán không, tiền thanh toán chênh có ghi vào `TIENTHANHTOAN` không.
- Kết quả: ______

#### ⭐ BH-Q39 — Designer `ThietKeGiaoDienBan`: `DANH` khi nào thành `DBAN`? và các cột phụ
- **Thao tác:** **Quản trị → Thiết kế giao diện bàn**: (1) vẽ 1 nút mới bằng tay, **Lưu**, mở lại: nút còn không, đúng vị trí không; (2) di chuyển nút rồi Lưu lần 2, mở lại; (3) xóa nút, Lưu, mở lại; (4) dùng phím Up/Down/Shift+Down chỉnh vị trí/kích thước, so sánh; (5) bấm Lưu khi cố ý để trống trường bắt buộc xem có báo thành công giả.
- **DB:** `SELECT * FROM DANH` và `SELECT * FROM DBAN` (cột `CUSTOMDATA1/2/3`, `REF1ID/REF2ID`, `NUMCHAIRS`, `ROTATE`, `BUTTONSHAPE`, `LEFT/TOP/WIDTH/HEIGHT`) sau mỗi bước; `SELECT WIDTH, HEIGHT FROM DKHUVUC`.
- **Đọc:** xác nhận quirk suy ra từ code: nút vẽ tay vào `DANH`, lần lưu sau update nhầm `DBAN` (0 dòng) → mất vị trí/xuất hiện lại; Up/Down ghi nhầm cột. Kiểm tra `DKHUVUC.WIDTH/HEIGHT` có áp lên sơ đồ bàn desktop.
- Kết quả: ______

#### ⭐ BH-Q44 — Menu "Khách đặt hàng", quyền 61 (Gộp bàn) và 66 (Thay đổi phí dịch vụ), két tiền
- **Thao tác:** (a) mở từng menu cấp trên "Khách đặt hàng" ghi form mở ra; (b) đăng nhập `thungan` với quyền chỉ có/không có Gộp bàn, mở màn Sử dụng dịch vụ → nút gộp bàn bị ẩn/mờ/báo không đủ quyền; (c) **Thay đổi phí dịch vụ**: đăng nhập user không có quyền, mở bill nhà hàng, đổi % phí DV; (d) tìm menu/nút "mở két"/"két tiền" trong toàn bộ menu (không thấy thì ghi "không có").
- **DB:** `SELECT * FROM SFUNCTION WHERE NAME CONTAINING 'phí'` và `... CONTAINING 'Gộp'`; `SELECT * FROM SGROUPROLE WHERE SFUNCTIONID=:id`.
- **Đọc:** quyền 61/66 tương ứng form/nút nào. Kết luận tách bàn (BH-Q44b ở dưới).
- Kết quả: ______

#### BH-Q44b — Tách bàn (stub rỗng) — ghi chú
- **Thao tác:** trên bill có 3 món ở bàn 1, bấm **Tách bàn** (nút có ở màn Sử dụng dịch vụ desktop và POS cảm ứng). Ghi hành vi thật (nút ẩn? không làm gì? mở form?).
- **Đọc:** nếu không làm gì như code (`SetButtonVisible(false)`) thì tách bàn **không có**; Chiến quyết định có thiết kế mới hay bỏ. Không tự bịa luật.

### B2. Khách hàng (18 câu)

*Mẫu chung: mọi test trên DB test; xem tệp `deep/khach-hang.md` mục (d) để biết bằng chứng code.*

#### ⭐ KH-Q2 — Thông báo lỗi nguyên văn khi lưu khách
- **Thao tác:** mở **Danh mục khách hàng**, tạo lần lượt: (1) để trống Tên; (2) để trống Mã (khi `TuDongSinhMa` tắt nếu có); (3) trùng Mã; (4) trùng Tên; (5) trùng cả Mã và Tên; (6) trùng trong khác nhóm; (7) vi phạm nhiều lỗi cùng lúc. Chụp thông báo, ghi **thứ tự** kiểm tra.
- **DB:** không ghi gì nếu bị chặn: `SELECT COUNT(*) FROM DKHACHHANG` trước/sau.
- Kết quả: ______

#### ⭐ KH-Q3 — So sánh trùng: hoa/thường, dấu, khoảng trắng; sửa chính khách đó
- **Thao tác:** tạo `Nguyễn Văn A`; thử `nguyễn văn a`, `NGUYEN VAN A`, ` Nguyễn Văn A ` (khoảng trắng đầu/cuối), `Nguyễn  Văn A` (2 khoảng trắng); sửa chính khách `Nguyễn Văn A` (chỉ đổi địa chỉ) → lưu.
- **DB:** `SELECT NAME, MAKHACH FROM DKHACHHANG`.
- **Đọc:** biến thể nào bị coi trùng; sửa chính khách có bị báo trùng không.
- Kết quả: ______

#### ⭐ KH-Q4 — Xóa khách/nhóm
- **Thao tác:** xóa (a) khách chưa phát sinh; (b) khách đã có bill; (c) nhóm không khách; (d) nhóm có khách; (e) chọn nhiều dòng rồi xóa. Ghi câu hỏi xác nhận.
- **DB:** `SELECT STATUS FROM DKHACHHANG WHERE ID=:id` hoặc đếm dòng; `SELECT DKHACHHANGID FROM TDONHANG WHERE ID=:bill` (bill cũ còn trỏ tới khách đã xóa?).
- **Đọc:** xóa cứng hay `STATUS=-30`; khách đã phát sinh bị chặn hay xóa được; ảnh hưởng bill/công nợ.
- Kết quả: ______

#### ⭐ KH-Q5 — Ô Tìm, chọn nhóm cha, cây nhóm
- **Thao tác:** gõ vào ô Tìm: không dấu, hoa/thường, một phần SĐT, địa chỉ, mã; chọn nhóm cha khi nhóm con có khách (hiện khách nhóm con?); chuột phải cây nhóm: thêm/sửa/xóa ở đâu.
- **DB:** không cần.
- Kết quả: ______

#### ⭐ KH-Q14 — Lưu vết `STATUS`/`USERCREATEDID`/`TIMECREATED`/`USERMODIFIEDID`/`TIMEMODIFIED`
- **Thao tác:** tạo khách mới bằng `admin`; sửa khách đó bằng `thungan` (đổi địa chỉ) vài phút sau.
- **DB:** `SELECT STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED FROM DKHACHHANG WHERE ID=:id` sau tạo và sau sửa.
- **Đọc:** `STATUS` = 30 ngay khi thêm? `TIMECREATED` giữ nguyên khi sửa? `USERMODIFIEDID` đổi?
- Kết quả: ______

#### ⭐ KH-Q15 — Độ dài Tên/Mã và tiếng Việt
- **Thao tác:** nhập Tên 255 ký tự, 256 ký tự; thêm ký tự tiếng Việt có dấu/ký tự đặc biệt (ơ, ữ, ‘’).
- **DB:** `SELECT CHAR_LENGTH(NAME), NAME FROM DKHACHHANG WHERE ID=:id`; `SELECT RDB$FIELD_LENGTH FROM RDB$FIELDS` cho cột NAME (nếu cần).
- **Đọc:** 256 ký tự bị chặn gõ hay báo lỗi khi lưu; tiếng Việt đọc lại đúng.
- Kết quả: ______

#### ⭐ KH-Q18 — `CanhBaoKhuyenMai`: mở từ đâu; config `CanhBaoChuongTrinhKhuyenMai`
- **Thao tác:** tạo đợt KM đang chạy (hôm nay); vào **Cấu hình → nhóm Cảnh báo**, đặt `CanhBaoChuongTrinhKhuyenMai` = bật; đóng/mở lại app, đăng nhập, vào ca, mở bán hàng; ghi form hiện ra (nếu có). Thử menu mà code không gọi: tìm "Cảnh báo khuyến mại" trong mọi menu.
- **DB:** `SELECT NAME, INTVALUE FROM SCONFIG WHERE NAME CONTAINING 'CanhBao'`.
- **Đọc:** form có hiện khi nào; nếu không bao giờ hiện → khóa chết như code ngụ ý.
- Kết quả: ______

#### ⭐ KH-Q21 — Giảm tay có bị KM tự động ghi đè (`Max(khách, KM)`) không?
- **Thao tác:** đợt KM "giảm theo nhóm" 10% cho nhóm của mặt hàng `A`. Bán lẻ 1 `A`, nhập tay giảm 25% cho dòng; sửa số lượng; thêm món khác (kích hoạt tính lại); thử giảm tay 5% (thấp hơn KM).
- **DB:** `SELECT TILEGIAMGIA, TIENGIAMGIA, THANHTIEN FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`.
- **Đọc:** giá trị giảm sau tính lại: giữ 25%? về 10%? 5% bị nâng lên 10%? Làm tương tự trên bill **nhà hàng**.
- Kết quả: ______

#### ⭐ KH-Q22 — Ngưỡng `GIAMTHEOGIATRIDONHANG` so với số nào
- **Thao tác:** đợt KM "giảm theo giá trị đơn hàng": ≥ 100.000 giảm 10%. Bill bán lẻ có `TIENHANG` 110.000 nhưng sau giảm tay còn 95.000; bill khác `TIENHANG` 95.000 nhưng có thuế làm `TONGCONG` 105.000. Làm cả ở nhà hàng (nghi: không có nhánh này).
- **DB:** `SELECT TIENHANG, TIENGIAMGIA, TIENTHUE, TONGCONG, TILEGIAMGIA FROM TDONHANG WHERE ID=:bill`.
- **Đọc:** mốc áp theo `TIENHANG` hay `TONGCONG`; nhà hàng có áp không.
- Kết quả: ______

#### ⭐ KH-Q24 — Phát hành voucher; ràng buộc đối tác phân phối
- **Thao tác:** tìm trong menu form phát hành/sinh voucher hàng loạt/nhập Excel voucher; tạo voucher tay có đối tác `DKHACHHANGID`; dùng nó ở bill của **khách khác**; tạo voucher có `NGAYPHATHANH` ở tương lai và dùng ngay.
- **DB:** `SELECT * FROM DVOUCHER`; `SELECT DVOUCHERID, VOUCHER FROM TDONHANG WHERE ID=:bill`.
- **Đọc:** có chặn khách khác/ngày chưa tới không (code nghi: không).
- Kết quả: ______

#### ⭐ KH-Q25 — Công thức tách tiền giảm gộp ở nhà hàng (`TIENGIO` ở mẫu số?) + `Shared.TinhGia`
- **Trạng thái:** **ĐÓNG theo phạm vi** (nhánh tiền giờ bỏ khỏi clone). Chỉ ghi nhận "không còn dùng"; không đào sâu.

#### ⭐ KH-Q26 — Hai đợt KM cùng loại trùng ngày
- **Thao tác:** tạo 2 đợt "giảm theo nhóm" cùng nhóm, trùng ngày, tỉ lệ 10% và 20%; lưu cả hai (kiểm tra trùng bị comment trong code); bán lẻ + nhà hàng 1 món của nhóm.
- **DB:** `SELECT * FROM DDOTKHUYENMAI`; `SELECT TILEGIAMGIA FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`.
- **Đọc:** lưu được không; bill lấy 20% (Max)? Ghi nhận để Chiến chốt có chặn trùng khi clone.
- Kết quả: ______

#### ⭐ KH-Q29 — Phiếu nạp thẻ trả trước: bắt buộc khách? Có vào công nợ/quỹ?
- **Thao tác:** tạo thẻ trả trước cho `KH01`; mở form thẻ → **Nạp tiền** 500.000 (thử cả trường hợp thẻ không gắn khách). Xem Nợ cũ trên hóa đơn mới của `KH01` trước/sau; chạy báo cáo **TỒN QUỸ**.
- **DB:** `SELECT LOAI, THU, DKHACHHANGID, DTHETRATRUOCID, KHONGTHAYDOICONGNO FROM TTHUCHI WHERE DTHETRATRUOCID=:the`; nợ khách theo `GetNoCu` mẫu.
- **Đọc:** nạp có đổi công nợ (khi tick sẵn `KHONGTHAYDOICONGNO` thì không)? Tiền vào tồn quỹ (tiền mặt)? Có bắt buộc khách?
- Kết quả: ______

#### ⭐ KH-Q31 — Validation tăng/giảm điểm (`DIEMGIAM` hai lần)
- **Thao tác:** **Khách hàng thân thiết → Điểm tăng/giảm**: (1) chỉ nhập `DIEMTANG` 10; (2) chỉ `DIEMGIAM` 5; (3) cả hai; (4) cả hai bằng 0; ghi thông báo mỗi trường hợp.
- **DB:** `SELECT DIEMTANG, DIEMGIAM FROM TTANGGIAMDIEM WHERE DKHACHHANGID=:kh`.
- **Đọc:** (1) bị từ chối như nghi bug? (3) cho đồng thời?
- Kết quả: ______

#### ⭐ KH-Q35 — Nâng nhóm tự động: điểm xét có trừ điểm đã tiêu? có hạ nhóm không? ngưỡng thật?
- **Thao tác:** nhóm `N1` `DIEMTICHLUY`=100, `N2`=500; bật `TuDongNangCapThanhVienKhiDatHanMuc`; `KH01` bán đủ 120 điểm → thanh toán; tiêu bớt điểm bằng Trừ tích lũy (`DIEMGIAM`) làm còn 50 điểm; thanh toán thêm 1 bill nhỏ.
- **DB:** `SELECT DNHOMKHACHHANGID FROM DKHACHHANG WHERE ID=:kh` sau từng bill; `SELECT NAME, DIEMTICHLUY FROM DNHOMKHACHHANG` (trên DB thật: ngưỡng thực).
- **Đọc:** nâng khi điểm **tổng** hay **sau trừ**; có hạ nhóm khi điểm tụt (nghi: không).
- Kết quả: ______

#### ⭐ KH-Q36 — `CachChonKhachHang` = 0 là chế độ nào; mã thẻ quẹt là `MAKHACH`?
- **Thao tác:** **Cấu hình → Bán hàng**, đổi `CachChonKhachHang` qua các giá trị khả dĩ (0, 1, 2…); mỗi giá trị mở bán lẻ, bấm chọn khách (F6), thử gõ/quẹt mã (nhập `MAKHACH` vào ô).
- **DB:** `SELECT INTVALUE FROM SCONFIG WHERE NAME='CachChonKhachHang'`.
- **Đọc:** ghi giá trị → hành vi; mã quẹt khớp `MAKHACH` hay `CARDCODE`. (Enum nằm trong `No1Lib` — test chỉ lập bảng giá trị→hành vi.)
- Kết quả: ______

#### ⭐ KH-Q37 — Menu "Gửi tin nhắn tới khách hàng"; `SSMSTEMPLATE`; sinh nhật
- **Thao tác:** bấm menu gửi tin nhắn (ghi form/thông báo); bật `ThongBaoKhachDenNgaySinhNhat`, có khách `NGAYSINH` = hôm nay, đăng nhập lại/mở ca xem có thông báo.
- **DB:** `SELECT * FROM SSMSTEMPLATE`; cột bảng `SSMSTEMPLATE` (`SELECT RDB$FIELD_NAME FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='SSMSTEMPLATE'`).
- **Đọc:** nếu menu chết/không có gì → khẳng định "chỉ còn menu + quyền"; clone không làm.
- Kết quả: ______

#### ⭐ KH-Q39 — Kiểm chứng công thức với dữ liệu thật + trường hợp biên
- **Thao tác:** trên **DB thật** (backup 2023) chọn 1 khách có điểm: so sánh 3 công thức điểm (`GetDiemTichLuy` hóa đơn, lưới Khách hàng thân thiết, xét nâng nhóm) bằng SQL tương ứng (xem [khach-hang-the](pos-tap-nghiep-vu-khach-hang-the.md) §6.1). Trường hợp biên trên DB test: thẻ hết hạn, thẻ bị khóa, thẻ hết tiền; `QuyDoi1DiemSangTien = 0`; bill chưa thanh toán có cộng điểm khi `CachTinhDiem=0`.
- **DB:** truy vấn tay cho 3 công thức; `SELECT * FROM DTHETRATRUOC WHERE ...`.
- **Đọc:** số khác nhau giữa 3 nơi; thông báo từng trường hợp biên.
- Kết quả: ______

### B3. Kho hàng (4 câu)

#### ⭐ KHO-R1 (Q1) — Số phiếu `NAME` sinh thế nào?
- **Manh mối từ DB thật (19/11/2026):** `STABLEDESC.GENNO = 30` ở `TDONHANG` (dùng chung cho nhập/xuất/chuyển/kiểm kê/bán), `TTHUCHI`, `TDATHANG`, `TBAOGIA`, `TTHUONGPHAT`, `TTANGGIAMDIEM` → framework tự sinh số phiếu cho cả 6 loại; nhóm cấu hình "Số phiếu"/"Mẫu hóa đơn" tồn tại trong `SCONFIGGROUP` nhưng **không có khóa `SCONFIG` nào** → định dạng không nằm ở `SCONFIG`. Vẫn cần test để biết định dạng/thời điểm sinh.
- **Thao tác:** tạo liên tiếp: 2 phiếu nhập, 2 phiếu xuất, 2 phiếu chuyển, 2 phiếu kiểm kê (cùng ngày), sau đó phiếu nhập sang tháng/năm khác (đổi ngày nếu có quyền); xem ô số phiếu lúc **mở form** vs lúc **lưu**; thử sửa tay số phiếu, thử trùng số. Xem **Cấu hình → Số phiếu**.
- **DB:** `SELECT LOAI, NAME, NGAY FROM TDONHANG WHERE LOAI IN (1,2,3,4) ORDER BY TIMECREATED`; `SELECT NAME, STRVALUE, INTVALUE FROM SCONFIG WHERE NAME CONTAINING 'SoPhieu' OR NAME CONTAINING 'SOPHIEU'`.
- **Đọc:** định dạng (`PN…`/`NK20/…`), tiền tố theo `LOAI`, dãy theo ngày/tháng/năm/cửa hàng, sửa tay được/không. Làm tương tự phiếu thu/chi (`PT20/00001`).
- Kết quả: ______

#### ⭐ KHO-R10 (Q14) — Mẫu in 4 phiếu kho
- **Thao tác:** in thử từng phiếu (nhập/xuất/chuyển/kiểm kê) ra PDF/xem trước; chụp mẫu; ghi các trường có/không (giá khi thiếu quyền `XemGiaNhap`).
- **DB:** báo cáo mẫu in nằm trong `SREPORTTEMPLATE` (`SELECT SREPORTID, FILE FROM SREPORTTEMPLATE WHERE ...`, cột blob); trích `.frx` nếu cần.
- **Đọc:** các trường in; điều kiện in; quyền ẩn giá.
- Kết quả: ______

#### ⭐ KHO-R11 (Q21) — Giá theo kích thước / size ly
- **Thao tác:** thử 3 hướng nghiệp vụ: (a) tạo 2 mặt hàng "Ly M"/"Ly L"; (b) dùng giá 2/3/4 (`GIABAN2/3/4`) theo khu vực/loại giá; (c) mặt hàng có kích thước (`MatHangCoKichThuoc` bật) xem form `ChonKichThuocNhapKho`/giá theo kích thước. Ghi giá hiển thị và hành vi hóa đơn.
- **DB:** `SELECT KICHTHUOC FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`.
- **Đọc:** nghiệp vụ thật quán dùng cách nào → **quyết định của Chiến** (test chỉ cho thấy các cách app gốc cho phép).
- Kết quả: ______

#### ⭐ KHO-R6/R7 (Q27) — Delta schema DEMO vs POS TAP thật + giá trị cấu hình thật
- **ĐÃ TRẢ LỜI bằng DB thật (19/11/2026) — không cần restore:** `DMATHANG.LOAIDINHLUONG` **không tồn tại** trong `DATA.fdb`; `SCONFIG` = **205 khóa**; giá trị: `MatHangCoHanSuDung` 0, `MatHangCoKichThuoc` 0, `SuDungNhieuKho` 0, `GiaSuDungKhiChuyenXuatKho` **1** (giá nhập), `TuDongTinhGiaVon` 30, `ThongBaoTruocNgay` 0, `SuDung2DonViTinh` 30, `ChieuDaiMaVach` 7 (bảng đầy đủ ở `reference/db-pos-tap-do-truc-tiep.md` mục 3). Chỉ còn việc chụp các tab Cấu hình. Câu Q27 coi như đóng.
- **Thao tác (không cần app):** mở **DB thật** (restore backup) bằng FlameRobin/isql: `SELECT RDB$FIELD_NAME FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='DMATHANG' AND RDB$FIELD_NAME='LOAIDINHLUONG'`; `SELECT NAME, INTVALUE, STRVALUE FROM SCONFIG ORDER BY NAME` (xuất ra file, diff với `sconfig.txt` DEMO); `SELECT COUNT(*) FROM SCONFIG`; liệt kê bảng thừa/thiếu so với DEMO.
- **Thao tác (app):** mở **Cấu hình** chụp từng tab (đặc biệt combo và tab Số phiếu).
- **Đọc:** `LOAIDINHLUONG` tồn tại; số khóa `SCONFIG` (kỳ vọng 205); giá trị `MatHangCoHanSuDung`, `MatHangCoKichThuoc`, `SuDungNhieuKho`, `GiaSuDungKhiChuyenXuatKho`, `TuDongTinhGiaVon`, `ThongBaoTruocNgay`, `SuDung2DonViTinh`, `ChieuDaiMaVach`.
- Kết quả: ______

### B4. Công nợ + Quỹ (2 câu)

#### ⭐ CN-12 — Giá trị thật `ChoPhepKhachNo` và các config nhóm Thanh toán
- **ĐÃ TRẢ LỜI bằng DB thật (19/11/2026):** `ChoPhepKhachNo` = **0** (tắt bán chịu), `CoTienGio` 0, `LamTronTien` 1000, `ThoiGianChoPhepHuyBill` 50. Đây là cấu hình của bản cài/ DB mẫu hiện tại; nếu quán thật dùng bán chịu thì giá trị trong DB quán khác — hỏi Chiến (hoặc đọc backup `bak_02_02_2023.gbk`).
- **Thao tác:** như KHO-R6/R7: đọc `SCONFIG` trên **DB thật**: `SELECT NAME, INTVALUE FROM SCONFIG WHERE NAME CONTAINING 'Khach' OR NAME CONTAINING 'ThanhToan' OR NAME CONTAINING 'No'`; mở **Cấu hình → Thanh toán**.
- **Đọc:** quán dùng bán chịu hay không (`ChoPhepKhachNo`) → quyết định công nợ có là lõi hay chỉ cần tối thiểu.
- Kết quả: ______

#### ⭐ CN-Q9 — Ngày mặc định của phiếu thu/chi mới
- **Thao tác:** mở **Phiếu thu**/**Phiếu chi** mới vào lúc 23:50 (hoặc đổi giờ hệ thống), sau đó mở sau nửa đêm; thử với user không có quyền `ThayDoiNgayTrongPhieuThuChi`; ghi ô Ngày hiển thị gì.
- **DB:** `SELECT NGAY, TIMECREATED FROM TTHUCHI WHERE ID=:id`.
- **Đọc:** mặc định = hôm nay (`DbDate` hay `Now`)? Có giờ?
- Kết quả: ______

### B5. Nhân sự (1 câu)

#### ⭐ NS-R7 (Q18) — Bug `GioTinhLuong` (getter trả `dtBATDAU`)
- **Trạng thái:** **ĐÓNG theo phạm vi** (tính giờ đã bỏ). Chỉ cần Chiến nói "giữ/bỏ hành vi này" nếu sau này làm dịch vụ theo giờ. Không có thao tác test cần thiết.

### B6. Quản trị (5 câu)

#### ⭐ QT-V1 — Thuật toán sinh `DeviceId`/`MADANGKY`; `GetStatus(mã, true) == 3`; có dùng DES không
- **Test app không trả lời được thuật toán** (nằm trong `No1Lib`/APK; 0 kết quả trong cả 4 nguồn). Việc có thể làm:
  1. Mở **Bản quyền máy tính bảng**, nhập mã tùy ý (khác 3): ghi hành vi (im lặng), ghi `SELECT * FROM DBANQUYEN`.
  2. Gọi HTTP khi MTB chạy: `http://localhost:6868/dang-ky?DeviceId=TEST1` (hoặc tên tham số đúng của route `dang-ky`), xem phản hồi `LicMode/Serial/DayLefts` và dòng `DBANQUYEN` mới (`NGAYBATDAU`, `KETNOILUC`).
  3. Hỏi Chiến/nhà cung cấp: công cụ nào cấp `MADANGKY`, ai giữ khóa; nếu có APK thật, decompile APK + `No1Lib.Sys.dll`.
- **Đọc:** chỉ ghi nhận hành vi server; không tự bịa thuật toán.
- Kết quả: ______

#### ⭐ QT-V4 — `app.apk` là bản nào; cách tải/cài; có kiểm version?
- **Thao tác:** thư mục có sẵn `POS TAP\Data\app.apk` (1,8 MB, ngày 25/10/2018). Đọc manifest bằng `aapt dump badging app.apk` (hoặc APK Analyzer): `versionName`, `versionCode`, `package`. Khi MTB chạy, mở `http://<ip>:6868/` bằng trình duyệt trên máy khác: có tải được không; thử tải khi chưa có bản quyền.
- **Đọc:** version/gói; xác nhận "không kiểm version, không kiểm bản quyền khi tải".
- Kết quả: ______

#### ⭐ QT-V5 — Chu kỳ APK gọi `dang-ky`; mất mạng thì xử lý thế nào
- **Test cần thiết bị Android + APK thật** (hoặc giả lập): kết nối APK với MTB, theo dõi `SELECT KETNOILUC FROM DBANQUYEN` theo thời gian (vài phút); tắt wifi giữa lúc gọi món → gọi món tiếp rồi bật lại, xem có hàng đợi offline/mất món; đối chiếu `TDONHANGCHITIET`.
- **Nếu không có thiết bị:** hỏi Chiến; hoặc đọc APK (decompile).
- Kết quả: ______

#### ⭐ QT-V8 — Công nợ/tồn đầu kỳ (`LOAI 99/98/97`) hiện ở đâu; số âm bị bỏ qua có báo?
- **Thao tác:** nhập **Công nợ ban đầu** cho `KH01` = 1.000.000 và `NCC01` = 500.000; thử số âm −100.000 và số 0 (xem có báo gì); nhập **Tồn kho ban đầu** `A` = 20 chai. Sau đó: mở màn bán hàng/danh sách bill; chạy báo cáo công nợ (tổng hợp, theo kỳ, đối chiếu) và **Báo cáo bán hàng**; xem bill `LOAI=99/98/97` có lọt vào chi tiết bán hàng/thống kê doanh thu không.
- **DB:** `SELECT LOAI, NAME, NGAY, TONGCONG FROM TDONHANG WHERE LOAI IN (97,98,99)`.
- **Đọc:** các bill đặc biệt có xuất hiện sai chỗ (doanh thu, danh sách bill) không; số âm bị bỏ qua lặng hay báo.
- Kết quả: ______

#### ⭐ QT-V25 — Hai khóa số dài `140242-…`/`140243-…` trong `SCONFIG`: đổi khi nào
- **Thao tác:** `SELECT NAME, STRVALUE FROM SCONFIG WHERE NAME STARTING WITH '14024'` trước/sau khi: (a) khởi động app trên máy khác (copy bản test sang máy 2); (b) đổi tên máy; (c) kích hoạt/mở Bản quyền; (d) sao lưu rồi restore.
- **Đọc:** khóa đổi theo máy/ngày/kích hoạt → có phải khóa bản quyền máy trạm.
- Kết quả: ______

### B7. Báo cáo (20 câu)

*Mẫu chung: dùng **DB có dữ liệu** (backup 2023) hoặc DB test đã seed cố ý nhiều dạng bill; chạy báo cáo, đối chiếu tay bằng SQL.*

#### ⭐ BC-Q4 — `LOAI 5/6` → xem **A3**.

#### ⭐ BC-Q5 — `TIENGIAMGIA` vs `TIENGIAMGIATONG`
- **Thao tác:** bill nhà hàng có giảm % tổng 10% **và** khách thuộc nhóm có giảm giá; thanh toán.
- **DB:** `SELECT TIENGIAMGIA, TIENGIAMGIATONG, GIAMGIAMATHANG, TIENGIAMGIAGIO FROM TDONHANG WHERE ID=:bill`.
- **Đọc:** hai cột bằng nhau hay khác; báo cáo "Chi tiết bán hàng theo ngày" in số nào (cột tham số vs Total).
- Kết quả: ______

#### ⭐ BC-Q6 — Biểu đồ tháng trong năm: thiếu tháng 12, đảo nhãn (**quyết định Chiến**)
- **Thao tác:** DB có doanh thu tháng 12 của năm trước/nay; chạy **BIỂU ĐỒ DOANH THU THÁNG TRONG NĂM**; ghi thấy tháng 12 hay không; nhãn "năm nay"/"năm ngoái".
- **Đọc:** xác nhận bug hiển thị; sau đó Chiến chốt giữ hay sửa (không phải câu test).
- Kết quả: ______

#### ⭐ BC-Q7 — Phân tích tình hình bán hàng: bảng 2/3 không lọc `XUATVATTU`; chia 0
- **Thao tác:** kỳ chỉ có bill bán món định lượng (có dòng vật tư `XUATVATTU=30`) → chạy; thêm kỳ **không có bill nào** (`tongSlBan=0`).
- **DB:** `SELECT XUATVATTU, SUM(SLXUAT-SLNHAP) FROM TDONHANGCHITIET GROUP BY XUATVATTU`.
- **Đọc:** 3 bảng có khớp tổng; kỳ rỗng có lỗi chia 0 không.
- Kết quả: ______

#### ⭐ BC-Q8 — "Chi tiết bán hàng theo ngày": SL lấy `SLXUAT`, không trừ `SLNHAP` — hàng trả hiển thị ra sao
- **Thao tác:** bán lẻ 5 chai `A`, tạo **trả đồ** 2 chai (bill có `SLNHAP=2`); chạy báo cáo ngày đó.
- **DB:** `SELECT SLXUAT, SLNHAP, THANHTIEN FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`.
- **Đọc:** dòng SL hiển thị 5, 3, hay dòng riêng −2.
- Kết quả: ______

#### ⭐ BC-Q9 — "Thống kê mặt hàng bán": tiền bán không trừ trả lại; vốn dòng trả
- **Thao tác:** bill có trả đồ như BC-Q8; mở **Thống kê mặt hàng bán** (F9/F10 hoặc menu), chọn cách vốn giá vốn dòng.
- **DB:** `SELECT THANHTIEN, GIAVON, SLXUAT, SLNHAP FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`.
- **Đọc:** tiền bán/lãi có trừ phần trả; `THANHTIENNHAP` dòng trả có bù lại không (lỗi tính "Σ(SLXUATCHUAQUYDOI × DONGIA)").
- Kết quả: ______

#### ⭐ BC-Q10 — Lưới bill 3 form thống kê có lọc `LOAI=0 AND DATHANHTOAN=30` không
- **Thao tác:** có bill chưa thanh toán, bill hủy, bill công nợ đầu (`LOAI=99`), phiếu nhập; mở **Thống kê**, **Thống kê doanh thu**, **Thống kê mặt hàng bán** cùng ngày.
- **Đọc:** lưới có hiện bill nào ngoài bill đã thanh toán; tổng có khớp bill đã thanh toán.
- Kết quả: ______

#### ⭐ BC-Q11 — "Thống kê doanh thu": truy vấn thu/chi `TTHUCHI` không lọc cửa hàng
- **Thao tác:** 2 cửa hàng, mỗi cửa hàng 1 phiếu thu 100.000 và 1 phiếu chi 30.000; chạy **Thống kê doanh thu** chỉ chọn cửa hàng 1.
- **DB:** `SELECT DCUAHANGID, THU, CHI FROM TTHUCHI`.
- **Đọc:** thu/chi hiển thị gồm cửa hàng 2 hay chỉ cửa hàng 1.
- Kết quả: ______

#### ⭐ BC-Q13 — Báo cáo **CẤU HÌNH BÀN KHU VỰC**: `banRow.DONGIA` hay `kvRow.DONGIA` cho cột `NOTE`
- **Thao tác:** khu vực đơn giá 50.000/giờ; 1 bàn đặt đơn giá riêng 80.000; chạy báo cáo. (Nếu không có tiền giờ trong POS TAP đã bỏ: ghi "ngoài phạm vi" nhưng vẫn chạy 1 lần xem cột NOTE.)
- **Đọc:** giá in là của bàn hay khu vực.
- Kết quả: ______

#### ⭐ BC-Q15 — Biến phí KQKD dùng `GIAVON` dòng vs lãi gộp dùng `DMATHANG.GIAVON`
- **Thao tác:** bán `A` ở giá vốn 10.000; sau đó nhập thêm hàng giá 14.000 và chạy **Tính lại giá vốn** (`TuDongTinhGiaVon`); chạy **KQKD** và **Tổng hợp lãi gộp (giá vốn)** cùng kỳ.
- **DB:** `SELECT GIAVON FROM TDONHANGCHITIET WHERE TDONHANGID=:bill`; `SELECT GIAVON FROM DMATHANG WHERE ID=:a`.
- **Đọc:** hai báo cáo ra "giá vốn" khác nhau do nguồn khác (dòng vs mặt hàng) — ghi số.
- Kết quả: ______

#### ⭐ BC-Q17 — `@ToDate` mang 00:00:00 hay 23:59:59
- **Thao tác:** tạo chứng từ ngày cuối kỳ lúc 22:00 (bill thanh toán lúc 22:00, phiếu nhập `TIMECREATED` 22:00); chạy báo cáo có `Đến ngày` = ngày đó.
- **DB:** kiểu cột `SELECT RDB$FIELD_TYPE FROM RDB$FIELDS ...` cho `NGAY`; `SELECT NGAY, TIMECREATED FROM TDONHANG`.
- **Đọc:** chứng từ ngày cuối có vào báo cáo không (nếu `NGAY` là DATE thì luôn vào).
- Kết quả: ______

#### ⭐ BC-Q18 — "Hàng đã hết hạn"/"Theo hạn dùng" không lọc `NGAY`
- **Thao tác:** phiếu nhập `A` có HSD 10 ngày sau, ghi **ngày phiếu sau** `Đến ngày` của báo cáo; chạy báo cáo với `Đến ngày` trước ngày phiếu.
- **Đọc:** lô có vào báo cáo dù ngày phiếu sau kỳ (nếu có → thiếu lọc `NGAY`).
- Kết quả: ______

#### ⭐ BC-Q25 — Giá trị bit `RoleMode` để giải mã cột `MODE` của **BÁO CÁO CHI TIẾT PHÂN QUYỀN HỆ THỐNG**
- **Thao tác:** màn **Phân quyền**: với nhóm test, tick từng quyền mức Xem/Thêm/Sửa/Xóa trên 1 chức năng (lần lượt 1 mức mỗi lần); chạy báo cáo; ghi cột hiển thị.
- **DB:** `SELECT MODE FROM SGROUPROLE WHERE SGROUPUSERID=:nhom AND SFUNCTIONID=:ch` sau từng lần.
- **Đọc:** lập bảng bit (có 0/16/80/112/240 đã gặp); chốt ý nghĩa 16/32/64/128.
- Kết quả: ______

#### ⭐ BC-Q26 — `DDINHLUONG.SOLUONG`: cho 1 đơn vị hay 1 lần pha
- **Thao tác:** món `B` định lượng 20g vật tư X cho 1 ly; bán 3 ly, thanh toán. Xem báo cáo **CÔNG THỨC ĐỊNH LƯỢNG**, **XUẤT ĐỊNH LƯỢNG THEO ĐƠN**.
- **DB:** `SELECT SLXUAT FROM TDONHANGCHITIET WHERE TDONHANGID=:bill AND XUATVATTU=30`.
- **Đọc:** vật tư xuất = 3 × 20g = 60g (theo từng đơn vị bán) — như code; xác nhận.
- Kết quả: ______

#### ⭐ BC-Q27 — `LOAITHANHTOAN` trong "Chi tiết hoạt động trong ngày": số tiền hay mã loại
- **Thao tác:** bill thanh toán tiền mặt, bill chuyển khoản, bill thẻ, bill ghi nợ; chạy báo cáo ngày đó.
- **DB:** `SELECT LOAITHANHTOAN, TIENMAT, CHUYENKHOAN, THE FROM TDONHANG WHERE ID=:bill`.
- **Đọc:** cột in ra có khớp mã loại (0/1/...) hay số tiền; lập bảng mã.
- Kết quả: ______

#### ⭐ BC-Q29 — "Bán hàng theo giờ": bill `BATDAU` NULL; `BATDAU` khác ngày `NGAY`
- **Thao tác:** bill bán lẻ (nghi `BATDAU` NULL); bill nhà hàng; bill có đổi ngày giao dịch (khác `NGAY` với `BATDAU`); chạy **BÁO CÁO BÁN HÀNG THEO GIỜ**.
- **DB:** `SELECT BATDAU, NGAY, GIOTHANHTOAN FROM TDONHANG WHERE ID=:bill`.
- **Đọc:** bill `BATDAU` NULL rơi nhóm giờ nào hoặc không hiện.
- Kết quả: ______

#### ⭐ BC-Q31 — "Tổng hợp doanh thu theo loại đồ": nhãn `DLOAIDOID`; mặt hàng tính giờ
- **Thao tác (không cần app):** `SELECT * FROM DLOAIDO`; chạy báo cáo với mặt hàng thuộc từng loại ăn/uống/dịch vụ/khác. (Phần tính giờ ngoài phạm vi.)
- **Đọc:** nhãn 0/1/2/3 (+4 nguyên liệu) đúng như tài liệu.
- Kết quả: ______

#### ⭐ BC-Q37 — Tên "BÀN PHÒNG" → "THEO BÀN" (**quyết định đặt tên của Chiến**, không phải câu test)
- Chỉ cần Chiến chốt. Lưu ý: giữ tên cũ nếu muốn khớp báo cáo gốc.

#### ⭐ BC-Q42 — Đếm dòng bán `LOAI=0` có `XUATVATTU` NULL; `LOAI=97` mỗi kho đúng một phiếu?
- **Thao tác (không cần app):** trên **DB thật**: `SELECT COUNT(*) FROM TDONHANGCHITIET C JOIN TDONHANG D ON D.ID=C.TDONHANGID WHERE D.LOAI=0 AND C.XUATVATTU IS NULL`; `SELECT DKHONHAPID, COUNT(*) FROM TDONHANG WHERE LOAI=97 GROUP BY DKHONHAPID`.
- **Đọc:** có dòng `XUATVATTU` NULL hay luôn 0; kho nào có nhiều hơn 1 phiếu tồn đầu.
- Kết quả: ______

#### ⭐ BC-Q43 — "Thống kê" mở từ F9/F10: phím tắt ở đâu; tab dịch vụ theo giờ có tính `LOAI 5/6`
- **Thao tác:** mở màn bán hàng, bấm F9, F10 (ghi form); tìm menu "Thống kê"; ghi phím tắt hiển thị trên tooltip/menu. (Tab dịch vụ theo giờ: ngoài phạm vi.)
- **Đọc:** phím tắt khai báo ở form cha/menu.
- Kết quả: ______

## C. Vế mở lẻ (không thuộc 55 câu — test cùng lượt cho tiện)

Các mục này là phần còn mở của câu đã trả lời một phần. Thao tác ngắn gọn, dùng chung DB test.

### C1. Bán hàng
| ID | Việc cần xác minh | Thao tác + DB cần đọc |
|---|---|---|
| BH-Q1 | Ai ghi `DBAN.TDONHANGID` khi mở bàn | Mở bàn, tạo bill: `SELECT TDONHANGID FROM DBAN WHERE ID=:ban` ngay sau khi thêm món đầu; so với trước (nghi trigger/framework) |
| BH-Q38 | Ai set `DBAN.TRANGTHAI` 1/2; `TSUACHUA.DASUAXONG` | Bấm "Báo dọn"/"Dọn dẹp" trên bàn; tạo phiếu sửa chữa; đọc `DBAN.TRANGTHAI`, `TSUACHUA` |
| BH-Q49 | `MaxValue` desktop vs TS (`>=` vs `>`); công thức nạp thẻ | Nhập số lượng/giá đúng giá trị `MaxValue` ở desktop và POS cảm ứng; so thông báo |
| BH-Q9 | `SoHoaDonQuayVongTheo = 1` là chu kỳ nào; chống trùng số hóa đơn nhiều máy | Đổi cấu hình qua Ngày/Tháng/Năm, sinh bill nhà hàng; `SELECT SOHD FROM TSOHOADON`, `SELECT NAME FROM TDONHANG` |
| BH-Q12/43 | Giá trị số enum `HoaDonMode`, `CachChonKhachHang`, `CachChonNgayGiaoDich`, `CachTinhDiem`, `CACHXUATHOADON`, `InHoaDonTheoKhuVuc` | Đổi lần lượt từng cấu hình qua các giá trị trong combo, ghi `SCONFIG.INTVALUE` và hành vi |
| BH-Q17 | `WHERE STATUS = 30` trong `LoadInvoice` | Tạo bill rồi đặt `STATUS=-30` bằng SQL; mở lại bill từ danh sách |
| BH-Q36/40/41/45 | `TDONHANG.SOORDER`, `DBAN.MAUSAC=0`, `TDONHANG.THANHTOAN`, `TDATHANG.LOAI/LOAIGIA` | Dùng chức năng tương ứng rồi đọc cột; hoặc ghi "chưa gặp" nếu không có luồng sinh |
| BH-Q51/52/53/54/56 | Timeout quẹt thẻ; layout `NhapThongTinEinvoice`; cột tìm `TimKiemMatHang`; caller `ThemMua1Tang1`/`ChonKichThuocNhapKho`/`ChonNgayGiaoDich`; `KhongDuocPhepGiamDo()` | Mỗi cái: mở chức năng, chụp màn hình, ghi điều kiện/thông báo |

### C2. Công nợ + Quỹ
| ID | Việc cần xác minh | Thao tác + DB cần đọc |
|---|---|---|
| CN-6 | Xuất khác gắn NCC có phải "trả hàng NCC"; kiểm tồn; khi nào gắn | Lập phiếu xuất khác có/không NCC; tồn thiếu → có chặn; `SELECT DNHACUNGCAPID, TIENTHANHTOAN FROM TDONHANG WHERE LOAI=2` |
| CN-8 | `CHITIETCONGNOEX` là gì | `SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$RELATION_NAME CONTAINING 'CONGNO'` trên DB thật; tìm view/bảng |
| CN-Q1 | Định dạng số phiếu thu/chi | Như KHO-R1 cho `TTHUCHI` |
| CN-Q5 | SQL gốc màn **Tồn quỹ** (ngày nhóm, `NGAY` vs `TIMECREATED`, mặc định, đầu/cuối kỳ) | Mở màn Tồn quỹ với phiếu thu/chi/bill khác ngày giờ; đối chiếu số theo `NGAY` và theo `TIMECREATED` |
| CN-Q6 | Danh mục phiếu thu/chi lọc `LOAI`; phiếu `LOAI=2` xem ở đâu | Tạo phiếu thu công nợ; mở **Danh mục phiếu thu** và **phiếu chi**: có hiện `LOAI=2` không; tìm màn khác xem phiếu loại 2 |
| CN-Q7 | Function "Thu chi" (`5d0b8bb7`) khóa gì | Đăng nhập user không có quyền này, thử "Tạo phiếu thu/chi" các menu |
| CN-Q10 | Công thức 6 báo cáo thu chi tự sinh | Seed phiếu thu/chi nhiều lý do/ngày; chạy từng báo cáo, đối chiếu SQL tay |
| CN-Q15 | Phiếu xóa mềm có tính vào màn Tồn quỹ không | Đặt `STATUS=-30` cho 1 phiếu bằng SQL, xem màn Tồn quỹ vs báo cáo Tồn quỹ |

### C3. Kho (đánh số theo `deep/kho-hang.md` §D)
| ID | Việc cần xác minh | Thao tác + DB cần đọc |
|---|---|---|
| KHO-R2 | Xóa phiếu: cứng hay mềm; ai được xóa | Xóa phiếu nhập/xuất/kiểm kê trong Quản lý nhập/xuất/…; `SELECT STATUS FROM TDONHANG WHERE ID=:id` (còn dòng?) |
| KHO-R3 | `THANHTIEN` phiếu xuất/chuyển có tự tính | Tạo phiếu xuất 5 chai giá 15.000: đọc `THANHTIEN`/`TIENHANG`/`TONGCONG` ngay trên lưới và trong DB |
| KHO-R4 | `ThemSoLuongChan` phiếu chuyển có bị `CalculateRow` che lỗi | Thêm `A` đơn vị chẵn 2 lần vào phiếu chuyển (cùng mặt hàng, `NhapMotMatHangNhieuLanTrongPhieu` tắt); `SELECT SLNHAP, SLNHAPCHUAQUYDOI FROM TDONHANGCHITIET WHERE TDONHANGID=:phieu AND SLNHAP>0` |
| KHO-R5 | Xuất lại vật tư bỏ trống ngày | Trên **bản sao DB**, mở **Xuất lại vật tư**, nhập mật khẩu (admin + `6666`), **để trống 2 ngày**, chạy; `SELECT COUNT(*) FROM TDONHANGCHITIET WHERE XUATVATTU=30` trước/sau |
| KHO-R8/R9 | Màn "Hàng tồn hết hạn dùng" mở từ đâu; `Reminder` có bao giờ bật | Tìm trong mọi menu; để tồn dưới `TONTOITHIEU` rồi vào Tồn kho xem có màn nhắc |
| KHO-R12 | Sửa phiếu kho sau khi đã dùng/khóa sổ | Sửa phiếu nhập đã có xuất dùng; có chặn/cảnh báo |
| KHO-R13 | Ý nghĩa `MODE` 0/16/80/112/240 | Như BC-Q25 (cùng giải bit) |
| KHO-R14 | Hiệu năng tính lại giá vốn | Trên **DB thật lớn** bấm **Tính lại giá vốn**, đo thời gian; ghi số dòng `TDONHANGCHITIET` |

### C4. Nhân sự (đánh số theo `deep/nhan-su.md` §4)
| ID | Việc cần xác minh | Thao tác + DB cần đọc |
|---|---|---|
| NS-R1 | `TAMUNG` `SUM(CHI)` vs `SUM(THU−CHI)` | Lập phiếu thu `LATAMUNG=30` cho NV; so số tạm ứng hiện ở form tay vs form máy |
| NS-R2 | Chi lương thiếu rồi chi nốt | Chi 1 phần lương; thử chi lần 2 (nút đổi "Xem phiếu"?) → ghi cách quán làm (hỏi Chiến/quán) |
| NS-R3 | `TVAORA.SOGIO` thật (×100? nguyên?) | Chạy máy chấm công (hoặc nhập tay `TVAORA`) rồi `SELECT SOGIO, SOGIOTANGCA FROM TVAORA`; so với hiển thị lưới |
| NS-R4 | `TBANGLUONG.CHITIET` BLOB | `SELECT COUNT(*) FROM TBANGLUONG WHERE CHITIET IS NOT NULL` trên **DB thật** (DEMO = 0) |
| NS-R5 | **B1 form máy: lương 0 sau khi mở lại; Cập nhật ghi đè 0** + `TinhLuongSuDungMayChamCong`, `IpMayChamCong` thật | Nếu form có menu: nhập `TVAORA` (file mẫu hoặc tay), lưu bảng lương, mở lại, bấm Cập nhật **không** Refresh; `SELECT TONGLUONG, THUCNHAN FROM TBANGLUONGTONGHOP`; `SELECT NAME, INTVALUE, STRVALUE FROM SCONFIG WHERE NAME CONTAINING 'MayChamCong'` trên DB thật |
| NS-R6 | **Một phần đã trả lời bằng DB thật:** `Tính lương bằng máy` và `Lấy dữ liệu từ máy` **không có menu `STATUS=30` nào trỏ tới** (chỉ `Tính lương` có 2 menu); `TinhLuongSuDungMayChamCong` = NULL. Còn mở: có dùng thật hay không. Form lương máy có menu/dùng thật; "form thừa của DEMO" là form nào | `SELECT NAME FROM SFORM` trên DB thật (đã biết `Tính lương bằng máy` **có** trong export 111 form; tìm form DEMO thừa); `SELECT * FROM SMENU WHERE SFORMID=:id`. Lưu ý: thư mục `POS TAP` **không có `zkemkeeper\zkemkeeper.exe`** mà form lương máy cần để lấy dữ liệu máy chấm công → bản cài này không chạy được luồng đó (gợi ý quán không dùng lương máy; hỏi Chiến để chốt) |
| NS-R8 | Lỗ hổng quyền Chấm công (lưu được tiền) | User chỉ có quyền Chấm công mở màn Chấm công, sửa công, bấm Cập nhật; `SELECT THUCNHAN FROM TBANGLUONGTONGHOP` (đổi?) |

### C5. Quản trị
| ID | Việc cần xác minh | Thao tác + DB cần đọc |
|---|---|---|
| QT-V2 | APK khi `DayLefts <= 0` hiện gì | Đặt `NGAYBATDAU` máy test về trước hơn 20 ngày; mở APK (cần thiết bị) |
| QT-V3 | **`ONLINE.FDB` chứa gì** (có sẵn `POS TAP\Database\ONLINE.FDB`, 2 MB) và **`WebService.exe` là gì** (có sẵn `POS TAP\WebService.exe` + `WebService.exe.config`) | Mở `ONLINE.FDB` bằng FlameRobin: danh sách bảng, `SELECT COUNT(*)`; so với `SCONFIG`/`SFUNCTION`/`SMENU`. Đọc `WebService.exe.config` (cổng, chuỗi kết nối, URL). Bật `KichHoatDongBo=30` trên bản sao, quan sát `onlineTimer_Tick` 5 phút có tự chạy `webservice.exe` không (Task Manager); ghi cổng/log |
| QT-V11 | Registry `EasyReg` (hive + key cha) | Cấu hình máy in bếp một khu vực; mở `regedit`, tìm các key `_AN/_UONG/_KHAC/_DICHVU` hoặc `MAY_IN_DO_AN…` |
| QT-V14/V15/Q18 | `DeleteSelectedRows()` xóa mềm/cứng (user, bản quyền MTB) | Xóa 1 user test / 1 dòng bản quyền test; `SELECT COUNT(*), STATUS FROM SUSER WHERE ...`; `SELECT * FROM DBANQUYEN` |
| QT-V19 | **Một phần đã trả lời bằng DB thật:** `LamTronTien` 1000, `ThoiGianChoPhepHuyBill` 50, 205 khóa (không có "khóa thừa" — các khóa DEMO thừa nằm ở DB khác). Còn mở: enum combo (đổi từng giá trị trên app) — `SCONFIG` thật: `LamTronTien`, `ThoiGianChoPhepHuyBill`, khóa thừa thứ 7, enum combo | `SELECT NAME, INTVALUE, STRVALUE FROM SCONFIG ORDER BY NAME` trên DB thật, diff với `sconfig.txt` DEMO; chụp tab Cấu hình |
| QT-V20 | 50 khóa chưa thấy chỗ đọc | Đổi từng khóa quan trọng (`SuDungNhieuKho`, `CoManHinhPhu`, `ChonNgayLamViecKhiDangNhap`, `LuuVetTrong`/`SoNgayLuuVet`, `ChucNangMacDinh`, nhóm Cảnh báo) rồi quan sát hành vi |
| QT-V21 | `PrintProcessor.exe` in gì; định dạng `_out` | **Thư mục `POS TAP` không có `PrintProcessor.exe`** (chỉ có `PrintService.exe`, 52 mục): code MTB gọi `<StartupPath>\PrintProcessor.exe <guid>`. Test: gửi POST in tới MTB và theo dõi `temp\<guid>`/`<guid>_out`, xem lỗi khi thiếu exe; kiểm tra `PrintService.exe` có phải bản thay thế (chạy với tham số `<guid>`); hỏi Chiến bản cài thật có `PrintProcessor.exe` không |
| QT-V23 | `USERID` là mã quẹt thẻ hay mã tự đặt; `CARDCODE` | Tạo user có `USERID` = số; thử đăng nhập MTB bằng `matkhau` = `USERID` (đăng nhập không mật khẩu); quẹt thẻ cho `CARDCODE` có logic không (Chiến xác nhận) |
| QT-Q16 | Thiết kế giao diện: giữ y hệt lỗi hay sửa | **Quyết định Chiến** (sau khi BH-Q39 xác nhận lỗi) |
| QT-Q19 | Đăng nhập MTB bằng `USERID` không mật khẩu | **Quyết định Chiến** (giữ y hệt hay siết lại) |

### C6. Khách hàng — vế còn lại của câu trả lời một phần
| ID | Việc cần xác minh | Thao tác + DB cần đọc |
|---|---|---|
| KH-Q1 | Cách tăng số `MAKHACH` kế tiếp (`KH(*****)`) | Tạo 3 khách liên tiếp, xóa khách giữa, tạo tiếp: `SELECT MAKHACH FROM DKHACHHANG` |
| KH-Q8 | Chặn > 100% hay tỉ lệ âm ở nhóm khách | Nhập `TILEGIAMGIA` 150, −5, 12,5 trên dialog nhóm |
| KH-Q10/12/23/38 | Nút ẩn hay mờ khi thiếu quyền; ghi ngưỡng thật; `Shared.TinhGia` | Đăng nhập `thungan` (quyền 0) mở các màn nhóm Khách hàng; ghi nút ẩn/mờ |

### C7. Báo cáo — vế còn lại
| ID | Việc cần xác minh | Thao tác + DB cần đọc |
|---|---|---|
| BC-PHU1 | WHERE chính xác của 73 báo cáo tự sinh | Seed bill cố ý: 1 bill chưa thanh toán, 1 bill hủy, 1 `LOAI=1`, 1 `LOAI=2`, 1 `LOAI=99`, 1 phiếu `STATUS=-30`; chạy mỗi **họ** báo cáo (bán hàng, kho, công nợ, quỹ, danh mục, đặt hàng — theo `deep/bao-cao.md` §b) và so tổng với SQL tay theo giả thuyết (`LOAI=0 AND DATHANHTOAN=30`…); loại trừ giả thuyết sai |
| BC-PHU2 | Báo cáo thừa của DEMO (128 vs 127) | **ĐÃ TRẢ LỜI bằng DB thật (19/11/2026):** `SREPORT` thật có 128 dòng, 127 `STATUS=30` + 1 `STATUS=0` ("BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN") → không có báo cáo thừa, chỉ là báo cáo bị tắt. Đóng |
| BC-PHU3 | Mẫu in: biểu đồ doanh thu ngày trục X hỏng; Total sai ở 2 báo cáo tháng; `GIATRITON ≠ 0` khi `Ton=0` ở XNT | Chạy 3 báo cáo trên DB có dữ liệu, chụp màn hình; đối chiếu tổng tay |

## D. Tổng kết đếm

| Nhóm | Số câu ⭐ | Đóng theo phạm vi / quyết định thuần (không có thao tác test) | Có thao tác test app/DB |
|---|---|---|---|
| Bán hàng | 5 | 2 (Q7, Q14) | 3 (Q29, Q39, Q44) |
| Khách hàng | 18 | 1 (Q25) | 17 |
| Kho | 4 | 0 | 4 (Q1, Q14, Q21 — kèm quyết định Chiến, Q27) |
| Công nợ + Quỹ | 2 | 0 | 2 (CN-12, Q9) |
| Nhân sự | 1 | 1 (Q18) | 0 |
| Quản trị | 5 | 0 | 5 (V1, V5 cần APK/decompile/Chiến — app không đủ) |
| Báo cáo | 20 | 1 (Q37) | 19 (Q6 kèm quyết định Chiến; Q4 trùng A3) |
| **Tổng** | **55** | **5** | **50** |

**Cập nhật sau khi đo `DATA.fdb` thật (19/11/2026):** đã đóng thêm KHO-R6/R7 (Q27), CN-12, BC-PHU2, một phần QT-V19/NS-R6; số câu cần test app giảm tương ứng (kho 3 thay vì 4, công nợ+quỹ 1 thay vì 2).

Bảy câu chặn (Phần A) hợp nhất thành 7 thao tác test ở trên; thứ tự làm đề nghị: A1 → A2 → A3 → A4/A5/A6 (cùng một kịch bản NCC) → A7, rồi tới các nhóm B.
