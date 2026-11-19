# Tổng hợp rà soát nghiệp vụ toàn bộ UI POS TAP (bản clone FnB)

- Ngày rà soát: 19/11/2026. Ngôn ngữ: tiếng Việt.
- App đích: **POS TAP** (`D:\Working\FnB\POS TAP`) — bản desktop quản lý + bản POS cảm ứng. Quyết định đã chốt 19/11: chỉ giữ loại hình **COFFEE (0) + POS (1)**, bỏ tính tiền theo giờ (BIDA/KARAOKE/HOTEL/MASSAGE/PS3/SPA).
- Bản clone: WinForms .NET Framework 4.8 + Firebird 2.5, schema POS TAP nguyên trạng, desktop trước.

## 0. Nguồn và phương pháp

- **Nguồn chính:** code C# trong `SFORM.CODE` (112 form), `STABLEDESC.CODE`, SQL trong `SREPORT.SQL` (128 báo cáo), `SCOLUMN` (771 cột), `SCONFIG` (212 khóa), `SMENU` (78 mục), `SFUNCTION` (89 quyền) — xuất trực tiếp từ DB **FnB DEMO.fdb** bằng Firebird 2.5 embedded, lưu ở `pos-tap-review/raw/` (3,2 MB).
- **Vì sao dùng DEMO thay cho DATA.fdb POS TAP:** Chiến xác nhận 19/11 DATA.fdb của POS TAP giống hệt DB FnB trong repo. Đối chiếu cho thấy chỉ khác: DEMO thừa **1 form, 1 báo cáo, 3 function, 7 config** và có thêm bảng **`DNGANHANG`** (58 dòng) mà POS TAP không có. Mọi chỗ dính `DNGANHANG` đã được ghi chú riêng trong từng file nhóm.
- **Phương pháp:** 8 người rà song song, mỗi người một nhóm menu/báo cáo, mọi khẳng định đều trích tên cột/method/SQL làm bằng chứng, không suy đoán. Chỗ nào code không trả lời được thì ghi vào mục "Cần xác minh bằng app gốc" của từng file — tổng cộng **241 câu hỏi**.
- **Tài liệu tham chiếu:** spec chính `docs/specs/kazuko-tea-clone.md` (§4 khung nghiệp vụ, §9 câu hỏi mở), `he-thong.md`, `danh-muc-khach-hang.md` (nháp, viết theo schema TEA — đã phát hiện 20 điểm sai/lỗi thời, xem khach-hang.md bảng E1–E20), `pilot-phieu-thu.md`, `goi-demo-ui.md`.

## 1. Bản đồ phủ sóng — 8 file chi tiết

| Nhóm | File chi tiết | Quy mô | Câu hỏi xác minh |
|---|---|---|---|
| Hoạt động — Bán hàng | `hoat-dong-ban-hang.md` | 885 dòng, ~176 KB | 57 câu + 13 nghi vấn giống bug |
| Khách hàng | `khach-hang.md` | 728 dòng, ~115 KB | Q1–Q39 |
| Kho hàng | `kho-hang.md` | 523 dòng, ~57 KB | Q1–Q27 |
| Công nợ | `cong-no.md` | 242 dòng | 12 câu |
| Quỹ | `quy.md` | 260 dòng | Q1–Q16 |
| Nhân sự | `nhan-su.md` | 484 dòng | 22 câu (§15) |
| Quản trị | `quan-tri.md` | 1.665 dòng, ~150 KB | V1–V25 (§13) + 15 quyết định cần chốt (§12) |
| Báo cáo | `bao-cao.md` | 699 dòng, ~95 KB | 43 câu (§10) |
| **Tổng** | | **5.486 dòng, ~723 KB** | **241 câu** |

File trung gian (đối chiếu): `pos-tap-review/work/` (phiếu kho, giá vốn/định lượng, tồn kho/HSD, 5 nhóm báo cáo, 5 file con Quản trị, decoder).

## 2. Mô hình nghiệp vụ chung (dùng chéo mọi nhóm)

- **Quy ước engine** (giữ khi clone): khóa chính `ID` là GUID; `STATUS = 30` đang dùng, `-30` xóa mềm; engine tự điền `USERCREATEDID`/`TIMECREATED` khi thêm, `USERMODIFIEDID`/`TIMEMODIFIED` khi sửa. Tiền/giờ trong DB là `BIGINT`, không phải DECIMAL.
- **`TDONHANG.LOAI`** (bảng trung tâm 101 cột): 0 bán hàng, 1 nhập kho, 2 xuất khác (gắn NCC = trả hàng, tính âm vào nợ NCC), 3 chuyển kho, 4 kiểm kê, 97 tồn đầu, 98 công nợ đầu NCC, 99 công nợ đầu KH. `TDONHANG` **không có cột STATUS** — trạng thái suy ra từ `DATHANHTOAN` và các cờ.
- **Công nợ khách:** `Σ(TONGCONG − TIENTHANHTOAN)` trên bill đã chốt (LOAI 0/99, `DATHANHTOAN = 30`) − `Σ(THU − CHI)` trên `TTHUCHI` (`KHONGTHAYDOICONGNO = 0`). Công nợ NCC tương tự nhưng LOAI 1/2/98, đảo dấu LOAI 2, **không lọc `DATHANHTOAN`** (lệch cần lưu ý, xem §4).
- **Tồn kho** là số tính động: `Tồn = Σ(SLNHAP − SLXUAT)`; bill bán chỉ tính khi `DATHANHTOAN = 30`. Không có cột tồn lưu sẵn.
- **Giá vốn:** bình quân gia quyền cuối kỳ theo tháng, toàn hệ thống, không phân biệt kho; `giaVon = Round(giaTri/ton, 2)`; tồn/giá trị mang sang tháng sau. Món định lượng bung 2 cấp khi tính lại giá vốn.
- **Lương tay:** `soCaDiLam = Σ TILELUONG/100`; theo ca `LUONGCA × soCaDiLam`; tháng-theo-ca `soCaDiLam × LUONGTHANG / totalDays`; theo tháng `soNgayDiLam × LUONGTHANG / soNgayTrongThang` (trừ T7/CN theo cờ nghỉ); nghỉ không phép = 0 công. `THUCNHAN = TONGLUONG + THUONG − PHAT − TAMUNG`. Lương máy: `TONGLUONG = Round((SOGIOLAM + SOGIOTANGCA × 1,5) × LUONGCA, 0)`.
- **Điểm thân thiết:** không có cột số dư, tính động. `DIEM = (int)(TONGCONG / 20000)`; 1 điểm = 1.000đ khi trừ. Công thức xét tự nâng nhóm **khác** công thức hiển thị (nâng nhóm không trừ `DIEMGIAM`, có lọc `DATHANHTOAN=30 AND LOAI=0`).
- **Thẻ trả trước:** `conLai = SUM(TTHUCHI.THU − CHI) − SUM(TDONHANG.THETRATRUOC)`; nạp/rút qua phiếu thu/chi `TTHUCHI`. Bản desktop trừ `CHI`, bản cảm ứng chỉ `SUM(THU)` — lệch số dư (xem §4).
- **Khuyến mại:** mọi giảm giá lấy **Max, không cộng dồn**. Voucher là phương thức thanh toán (không phải giảm giá), dùng 1 lần, cộng dồn với điểm/thẻ/ATM/chuyển khoản.

## 3. Phát hiện quan trọng nhất (đính chính spec §4)

1. **Hai bộ công thức tiền khác nhau, không thống nhất:** màn nhà hàng dùng `LamTron` (AwayFromZero, làm tròn tới bội số 1000) + phí dịch vụ + tiền giờ + `TIENPHAITRA = TONGCONG + BILLKHAC`; màn bán lẻ dùng `Math.Round` mặc định + tab trả lại (`DOITRA`) + không phí dịch vụ. Hai cơ chế sinh số hóa đơn cũng khác nhau (nhà hàng: `TaoSoHoaDon` + bảng `TSOHOADON`, đủ code; bán lẻ: `DbUtils.GenSoHoaDon` của No1Lib, không có trong dữ liệu xuất).
2. **Menu "Quản lý bán hàng" + "Điều chỉnh hóa đơn" cùng trỏ 1 form vỏ bọc** `QuanLyBanHangNhaHang` nhúng form Hóa đơn nhà hàng — không phải 2 form riêng. Ngược lại `HoaDonBanHang`, `QuanLyBanHang`, "Quản lý hóa đơn bán hàng" không có dòng menu nào trỏ tới — đường mở chưa rõ.
3. **Trừ kho thực sự xảy ra ở đâu và transaction khi thanh toán** là 2 câu hỏi lớn nhất còn mở — phải xác minh bằng app gốc/DB thật trước khi viết code thanh toán. **Tách bàn chưa tìm thấy implementation** (chỉ có mode `TachBan` ở `ChonBanTs`).
4. **Hóa đơn đã thanh toán được sửa trực tiếp** (không sinh phiếu điều chỉnh, không tính lại điểm/tồn kho ở form Quản lý bán hàng) — cần Chiến xác nhận có giữ hành vi này không.
5. **Công nợ đầu kỳ / tồn kho đầu kỳ không có bảng riêng:** 1 form chung lưu thành 1 phiếu `TDONHANG` đặc biệt (LOAI 99/98/97, `NAME='CONGNODAU'`, ngày mặc định 31/12 năm trước); >0 tạo/cập nhật, =0 xóa phiếu, <0 bỏ qua lặng lẽ.
6. **Phiếu thu công nợ khác hẳn phiếu thu thường:** `LOAI`=2, thu theo từng đơn (`LANNAY` → chi tiết `TCHITIETTHANHTOAN`); đối tượng là **nhân viên thu**, header **không có `DKHACHHANGID`** — khách chỉ nằm ở dòng chi tiết. Chưa chứng minh được `LANNAY` có cộng vào `TDONHANG.TIENTHANHTOAN` từng bill không (câu hỏi chặn số 1 của nhóm Công nợ).
7. **Số phiếu tự sinh chưa tìm ra:** nhóm cấu hình "Số phiếu" có 0 dòng trong DEMO; mẫu `PT20/00001` trong pilot không đối chiếu được. 6 báo cáo thu chi có `SREPORT.SQL` rỗng/NULL — công thức nằm trong framework.
8. **Gửi tin nhắn tới khách hàng chỉ còn menu + quyền:** grep `TinNhan`/`SMS`/`SendSMS` = 0 kết quả; `SSMSTEMPLATE` 0 dòng, không có cột nội dung. `CongCom=COM4` là cổng cân điện tử, không phải SMS.
9. **Dịch vụ MTB không phải "đồng bộ theo lô":** server HTTP tự viết trên cổng 6868 (hard-code, không TLS, không session/token, mật khẩu thô qua query string), nhiều endpoint không kiểm quyền; POST thực chất là cầu nối sang `PrintProcessor.exe`. `ONLINE.FDB`/`WebService.exe` có 0 bằng chứng trong code — phải mở ONLINE.FDB thật để đối chiếu trước khi chốt G6.
10. **73/128 báo cáo không có SQL trong `SREPORT`** — số liệu nằm trong mẫu FastReport `.frx` (`SREPORTTEMPLATE.TEMPLATE`, chưa xuất). Đây là việc tiếp theo bắt buộc phải làm trước khi clone nhóm báo cáo.
11. **Schema POS TAP vs TEA (`DKHACHHANG`):** 26 cột vs 23 — POS TAP bỏ `SOCMND`/`NGAYCAP`/`NOICAP`, thêm `DTHETRATRUOCID` + 5 cột hóa đơn điện tử. Bản nháp `danh-muc-khach-hang.md` có 20 điểm sai/lỗi thời (bảng E1–E20 trong khach-hang.md) — phải viết lại theo POS TAP.
12. **"Thay đổi giờ vào" không phải chấm công:** thực chất đổi giờ vào/ra của hóa đơn bàn (`TDONHANG`). "Điều chỉnh giờ tính lương dịch vụ theo giờ" (`GIOTINHLUONG`) gắn với mặt hàng dịch vụ theo giờ + KARAOKE, kèm bug getter trả về `dtBATDAU` khiến điều chỉnh ở màn thống kê thành no-op — ngoài phạm vi clone (đã bỏ tính giờ).

## 4. Mâu thuẫn / nghi vấn giống bug — cần Chiến chốt GIỮ hay SỬA

(Chi tiết đầy đủ trong từng file nhóm; dưới đây chỉ tóm tắt. Mặc định khi clone: **giữ y hệt** trừ khi Chiến nói sửa.)

- **Bán hàng:** 2 bộ công thức tiền + 2 cơ chế sinh số hóa đơn (mục 3.1); `TCHITIETTHANHTOAN` không form nào ghi; `DLYDOHUY` không thấy dùng ở đâu; `DTRANGTHAICHEBIEN` không có giá trị mẫu.
- **Khách hàng:** `TTANGGIAMDIEM.mapper_BeforeSave` viết `numDIEMGIAM` 2 lần (nhập chỉ điểm tăng bị chặn); nhà hàng tính `slXuat = Min(...)` rồi không dùng; công thức tách tiền giảm gộp ở nhà hàng chia cho `TIENGIO` (nghi lỗi đánh máy); mua 1 tặng 1: bán lẻ dòng 0 đồng, nhà hàng chia cho `MUA+TẶNG` nên mua 1 tặng 0 + dòng âm `DMATHANGID='KM'`.
- **Kho:** 3 công thức tồn khác nhau (lọc `STATUS`/`DATHANHTOAN`); 3 độ sâu bung định lượng khác nhau; kiểm tồn chuyển kho dùng số chưa quy đổi; `TONGCONG` phiếu chuyển có thuế/phí; kiểm tra âm kho khi bán không cộng vật tư bung từ định lượng; mật khẩu cứng `"6666"` (xuất lại vật tư), `"4321"` (đánh lại mã).
- **Công nợ:** KH bắt buộc `DATHANHTOAN`=30 còn NCC thì không; lưới NCC đảo dấu `TONGCONG` nhưng không đảo `TIENTHANHTOAN` khi LOAI=2 (báo cáo đảo cả hai); đối chiếu KH lọc `XUATVATTU`=0 còn NCC không; `CHITIETCONGNOEX` (spec §4.4 nhắc) **không tồn tại** trong DEMO.
- **Quỹ:** `DLOAIQUY` (Tiền mặt/Tiền thẻ/Ngân hàng) có 0 tham chiếu trong toàn bộ code — nhiều khả năng là bảng thừa; nút "Quẹt thẻ" ở Tồn quỹ loại toàn bộ phiếu thu/chi (`0 = 1`), chỉ tính `TDONHANG.THE`.
- **Nhân sự:** `TAMUNG` form tay `SUM(CHI)` vs form máy `SUM(THU − CHI)`; `LUONGTHEOCA` được copy nhưng không dùng trong công thức nào; xóa bảng lương form tay để lại `TBANGLUONGTONGHOP` mồ côi (form máy xóa cả 3 bảng); `TVAORA.TBANGLUONGID` null sau import máy; user chỉ có quyền Chấm công vẫn lưu được tiền qua nút Cập nhật (phân biệt theo tên menu, không theo quyền).
- **Báo cáo:** mâu thuẫn dấu công nợ NCC — báo cáo theo kỳ + đối chiếu trừ `Σ(CHI − THU)` (trả NCC làm giảm nợ), báo cáo tổng hợp NCC cộng `Σ(CHI − THU)` (trả làm tăng nợ), lệch `2 × Σ(CHI − THU)`; nhánh phiếu tổng hợp NCC thiếu lọc `KHONGTHAYDOICONGNO = 0`; mâu thuẫn nội bộ Tồn quỹ về phiếu `LOAI=2` (trong kỳ `+TIENTHANHTOAN` vào THU, tồn đầu kỳ `−TIENTHANHTOAN`); dòng LÃI LỖ trong KQKD lệch `2 × ChiPhiThangTruoc`; biểu đồ tháng thiếu tháng 12 + nhãn năm đảo ngược; tỉ lệ KQKD hard-code `"100%"`.
- **Quản trị:** mật khẩu cứng `"6789"` (mở dữ liệu import), `"9876"` (xóa lưu vết), mật khẩu wifi/user lưu chữ thường; import dữ liệu cũ không transaction, trùng = bỏ dòng, checkbox "Tồn kho" chết, `SinhDuLieu` là form chết; máy in theo khu vực lưu file INI theo máy trạm, máy in bếp lưu registry `EasyReg` (sao lưu DB không mang theo); MTB quá hạn dùng thử 20 ngày thì server **không chặn** (chỉ trả `DayLefts`, việc chặn nằm ở APK); xóa lưu vết = `DELETE FROM TLUUVET` toàn bảng; wifi dùng 1 lần: lấy 1 mật khẩu rồi `DELETE` luôn, `SELECT FIRST 1` không `ORDER BY`.

## 5. Câu hỏi chặn — phải xác minh bằng app gốc TRƯỚC KHI viết code phần đó

(Tổng 241 câu trong 8 file; dưới đây là những câu chặn code trực tiếp. Toàn bộ danh sách xem mục "Cần xác minh" cuối mỗi file nhóm.)

1. **Thanh toán:** trừ kho thực sự xảy ra ở đâu (lúc gọi món / lúc chốt bill / lúc in chế biến)? Có transaction bao quanh thanh toán không? (Bán hàng)
2. **Phiếu thu công nợ:** `LANNAY` có cộng vào `TDONHANG.TIENTHANHTOAN` từng bill không? (Công nợ Q1 — kèm test cụ thể trong cong-no.md)
3. **Số phiếu tự sinh:** định dạng và thời điểm sinh (khi mở hay khi lưu)? Nhóm cấu hình "Số phiếu" ở đâu trên app gốc? (Quỹ Q1)
4. **Bán chịu:** cổng chặn nợ (`ChoPhepKhachNo`, DEMO đang = 0 tức tắt) có giá trị thật nào trên DB POS TAP? Tiền trả dồn hết vào bill hiện tại — có đúng là không có FIFO trả nợ cũ? (Công nợ)
5. **Giá vốn:** `TuDongTinhGiaVon=30` thì tính lại toàn bộ hay chỉ mặt hàng trong phiếu? (Kho Q9/Quản trị V9)
6. **Lương:** công thức lương mẫu (ExcelSalaryEngine) nằm ở đâu — không có công thức Excel nào trong code đã đọc; `LUONGTHEOCA` thực sự dùng ở đâu? (Nhân sự)
7. **Báo cáo:** xuất `SREPORTTEMPLATE.TEMPLATE` (.frx) cho 73 báo cáo không SQL — số liệu của chúng nằm trong mẫu in. (Báo cáo §10)
8. **MTB/bản quyền:** thuật toán sinh `DeviceId`/`MADANGKY` (DES?) lấy từ đâu — công cụ nào cấp, ai giữ? APK có làm lại không (quyết định cách siết server :6868)? (Quản trị V1/V2, Q6–Q8)
9. **SCONFIG POS TAP:** xuất `SCONFIG` từ bản sao POS TAP rồi diff với DEMO 212 khóa — 7 khóa nào thừa? Giá trị mặc định POS TAP của `UngDung`, `CoTienGio`, `LamTronTien`, `ThoiGianChoPhepHuyBill`… là gì? (Quản trị V19–V20)
10. **Tồn quỹ:** SQL gốc của form Tồn quỹ (handler sửa SQL bằng thay chuỗi, chưa thấy bản gốc)? (Quỹ Q5)

## 6. Việc còn thiếu / giới hạn của đợt rà này

- **Chưa xuất `SREPORTTEMPLATE.TEMPLATE`** (.frx, 156 mẫu) — 73 báo cáo không SQL chưa rà được công thức. Cần xuất tiếp trước khi clone báo cáo.
- **`AELAYOUT` bị cắt ở 4.000 ký tự đầu** trong dữ liệu xuất — cấu hình lưới/cột sau điểm cắt chưa đọc được (ảnh hưởng: lưới form NCC, form Bản quyền, form Tài khoản, `STABLEDESC.FORMAT`…).
- **`SGROUPROLE`, `SGRID`, `SHOWONLOOKUP` không có trong dữ liệu xuất** — một số quyền/lưới chưa đối chiếu được.
- **DEMO gần như trống dữ liệu gốc** (0 khách/thẻ/điểm, `TTHUCHI` 0 dòng, các bảng nhân sự 0 dòng) — nhiều công thức chưa đối chiếu được với dữ liệu thật; hành vi thêm/sửa/xóa của màn danh mục nằm trong `No1Lib` (không mã nguồn).
- **`No1Lib.Run`/`No1Lib.Core` chưa đọc được** (decompile rỗng) — logic nào nằm trong đó (nếu có) thì code form không thấy.
- **Báo cáo thừa của DEMO (128 vs 127) không xác định được tên** — cách xác minh dứt điểm: `SELECT NAME FROM SREPORT` trên `DATA.fdb` POS TAP thật.
- **Form `TinhLuongBangMay` không tìm thấy menu/caller nào mở**; `SinhDuLieu` là form chết; `DLOAIQUY` có thể là bảng thừa; `DGIAODIENTHAOTAC` (4 dòng DEMO) không có CODE nào nhắc tới.
- **Đính chính `he-thong.md` §4.1:** `CARDCODE` và `USERID` **có** control trên form Tài khoản (he-thong viết không có là chưa đúng); `ISADMIN` có trong FieldMap nhưng không có control.

## 7. Đề xuất thứ tự làm tiếp (theo spec §7)

1. Xuất `SREPORTTEMPLATE.TEMPLATE` (.frx) + `SGROUPROLE`/`SGRID` và phần `AELAYOUT` còn lại — lấp các lỗ hổng ở §6.
2. Viết lại spec **Danh mục khách hàng theo schema POS TAP** (bản nháp hiện có 20 điểm sai — khach-hang.md bảng E1–E20).
3. Chạy app gốc POS TAP trên DB mẫu để trả lời 10 câu chặn ở §5 (đặc biệt Q1–Q3), rồi mới viết code thanh toán/công nợ/phiếu thu.
4. Chiến chốt các quyết định ở quan-tri.md §12 (mật khẩu cứng, import, MTB, e-invoice…) và các nghi vấn giống bug ở §4 (giữ hay sửa) trước khi code phần đó.
5. Sau đó làm theo thứ tự menu gốc: Hoạt động (danh mục nhẹ trước, luồng tiền/giá/kho sau) → Khách hàng → Kho → Công nợ → Quỹ → Nhân sự → Báo cáo.

---
*Nguồn rà soát: `~/workspace/fnb/pos-tap-review/raw/` (DB FnB DEMO.fdb — giống hệt DATA.fdb POS TAP trừ DNGANHANG/1 form/1 báo cáo/3 function/7 config). 8 file chi tiết xem cùng thư mục. Ngày rà soát: 19/11/2026.*
