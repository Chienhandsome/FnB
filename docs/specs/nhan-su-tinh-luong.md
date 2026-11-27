# Spec: Nhân sự P3 (Bảng lương, Tính lương, Chấm công, Chi lương)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 28/11/2026 theo workflow-agy §10.1.1; Chiến chọn làm Nhân sự theo thứ tự đề xuất 27/11 và dặn làm liên tục đến 7h sáng 28/11). Điểm **TẠM** ở §8 (nhiều điểm: Chiến cần xem lại khi dậy).
- Ngày: 2026-11-28. Loại: **nặng** (lương và chi tiền). Nguồn: `docs/deep/nhan-su.md` (đặc biệt §1.2, §1.3, §2.1, §2.5, §2.7, §2.8, §2.10, §3, §5), mã form gốc `Tính_lương.txt` (`TinhLuong`), `ChiLuong`, `TTHUCHI1Ae.SetLuong`; `phieu-thu-chi.md`, `quy-tien.md`, `nhan-su-danh-muc.md`, `nhan-su-thuong-phat-tam-ung.md`.
- Menu: NHÂN SỰ ▸ **Tính lương** (`SMENU.SORTORDER = ZZZb`), NHÂN SỰ ▸ **Chấm công** (`ZZZaZ`, chức năng `8761354d-5b96-4a52-a3ea-7cc62d7693ea`); **Chi lương** là nút trong màn Tính lương (như gốc). Nhận diện menu theo tên không dấu, không phân biệt hoa thường; quyền theo tên chức năng "Tính lương", "Chấm công" (Xem, Thêm, Sửa, Xóa; admin luôn được).
- **Không làm:** lương theo máy chấm công (`TinhLuongBangMay`, `Lấy dữ liệu từ máy`, `TVAORA`, `SOGIOLAM`: form gốc không có caller, R6), lương theo giờ, báo cáo lương (gốc không có), popup "Lương chi tiết" (thưởng/phạt/tạm ứng làm ở màn riêng đã có), in bảng lương.
- Chia **hai patch**: (P3a) Bảng lương + Tính lương + Chấm công; (P3b) Chi lương.

## 1. Mô hình dữ liệu (không đổi cấu trúc CSDL)

- `TBANGLUONG`: một bảng lương mỗi (tháng, năm): `THANG` (1–12), `NAM` (0–2999), `NAME` = "Tháng m/yyyy" tự sinh. Cột `CHITIET` (BLOB) là cột chết: không đọc, không ghi.
- `TBANGLUONGTONGHOP`: một dòng mỗi (nhân viên, bảng lương): `TBANGLUONGID`, `DNHANVIENID`, `LUONGCA`, `LUONGTHANG`, `CACHTINHLUONG`, `TONGLUONG`, `PHAT`, `THUONG`, `TAMUNG`, `THUCNHAN`; các cột `SOGIOLAM`, `SOGIOTANGCA`, `LUONGTHEOCA` không dùng (giữ nguyên).
- `TBANGLUONGCHITIET`: chấm công theo (nhân viên, ca, ngày): `TBANGLUONGID`, `DNHANVIENID`, `DCALAMVIECID`, `NGAY`, `TRANGTHAI` (1 Đi làm, 2 Nghỉ có phép, 3 Nghỉ không phép; 0 không có dòng).
- Cột thực tế (tên, kiểu, độ dài) đọc từ `RDB$RELATION_FIELDS` trong code mẫu hoặc grep trong repository hiện có, không đoán; kiểm độ lớn của kiểu số (BIGINT hay NUMERIC) trước khi viết SQL.

## 2. Khung màn Tính lương / Chấm công (một màn, hai chế độ)

Một view `PayrollView` mở bằng hai menu; chế độ **Tính lương** (menu "Tính lương") hiện đủ cột tiền và nút; chế độ **Chấm công** (menu "Chấm công") ẩn 6 cột tiền (`Lương cơ bản`, `Tổng lương`, `Thưởng`, `Phạt`, `Tạm ứng`, `Thực nhận`) và các nút Tải lại, Chi lương.

- **Bên trái: danh sách bảng lương** (mới nhất trước: sắp theo năm giảm, tháng giảm), nút **Thêm**, **Xóa**. Thêm: hộp chọn Tháng (1–12), Năm (0–2999), mặc định tháng và năm hiện tại, tên tự sinh "Tháng m/yyyy"; **không cho trùng (tháng, năm)** ("Bảng lương tháng m/yyyy đã tồn tại", T1). Xóa: chặn khi bảng lương đã có phiếu chi lương (`TTHUCHI.TBANGLUONGID`) nêu số phiếu; ngược lại xóa **cả ba bảng** (`TBANGLUONGCHITIET`, `TBANGLUONGTONGHOP`, `TBANGLUONG`) trong một transaction sau xác nhận (sửa lỗi mồ côi của gốc, T2), có lưu vết.
- **Bên phải: lưới chấm công** của bảng lương đang chọn: mỗi dòng là một cặp (nhân viên, ca làm việc đang dùng), sắp theo tên nhân viên rồi tên ca; cột: **Nhân viên**, **Ca**, các cột ngày 1…n (n = số ngày trong tháng, tiêu đề "ngày\nT2…CN", cột hẹp 27–30 px, ô màu theo trạng thái dùng `ThemeManager`: Đi làm, Nghỉ có phép, Nghỉ không phép, trống) rồi 6 cột tiền (gộp theo nhân viên: hiện ở dòng đầu của nhân viên, các dòng ca còn lại để trống; chế độ Chấm công không có). Cột Nhân viên, Ca cố định bên trái khi cuộn ngang.
- **Nhân viên đưa vào lưới:** nhân viên `STATUS = 30` (tất cả, kể cả chưa có dòng tổng hợp), cộng nhân viên đã có dòng tổng hợp của bảng lương này (kể cả nhân viên sau này bị khóa `STATUS <> 30`), mỗi người xuất hiện một lần. Ca đưa vào: `DCALAMVIEC` `STATUS = 30`; nhân viên không có ca nào để chọn thì không có dòng (hộp báo "Chưa có ca làm việc nào").
- **Chấm công:** chọn một hoặc nhiều ô (kéo chuột, Shift/Ctrl) rồi bấm nút **Đi làm**, **Nghỉ có phép**, **Nghỉ không phép**, **Xóa** (hoặc phím tắt 1, 2, 3, 0); mỗi ô một trạng thái. Có ô hiển thị tổng số ca và số ngày đi làm của nhân viên (xem §3). **Khi sửa ô thì tính lại ngay trong bộ nhớ (chưa ghi DB)**, chỉ ghi khi bấm **Cập nhật**; đóng màn khi còn thay đổi chưa lưu thì hỏi.
- **Tải lại** (chỉ chế độ Tính lương): hỏi xác nhận "Tải lại sẽ lấy lại lương cơ bản, cách tính lương, thưởng, phạt, tạm ứng hiện tại của nhân viên và tính lại; thay đổi chưa lưu sẽ mất"; lấy lại **Lương ca, Lương tháng, Cách tính, Nghỉ CN, Nghỉ T7 từ `DNHANVIEN`** và Thưởng, Phạt, Tạm ứng từ DB rồi tính lại mọi dòng, giữ nguyên chấm công đang hiện (như gốc B10; T3). Thêm tuỳ chọn tải lại cho **một nhân viên** (menu chuột phải).
- **Cập nhật** (lưu): một transaction (xem §4). Nút **Cập nhật** hiện ở cả hai chế độ nhưng **ở chế độ Chấm công chỉ ghi chấm công**, không ghi `TBANGLUONGTONGHOP` (đóng lỗ hổng B7; R8 chốt theo đề xuất, T4).
- Quyền: chỉ Xem thì lưới chỉ đọc, nút chấm công/Cập nhật/Tải lại/Thêm/Xóa không dùng được; người chỉ có quyền Chấm công không thấy lương dù mở menu nào khác.

## 3. Công thức lương (chế độ Tính lương), đúng `UpdateThucNhan` của gốc

Với mỗi nhân viên trong bảng lương tháng `m`/năm `y`, `totalDays` = số ngày của tháng; `tiLe(ca) = TILELUONG/100` (`TILELUONG = 0` hoặc rỗng coi là 100; **TILELUONG âm coi là 0**, T5):
- `soCaDiLam` = Σ `tiLe(ca)` cho mọi ô (ca × ngày) có trạng thái 1 (Đi làm) hoặc 2 (Nghỉ có phép); ô 3 (Nghỉ không phép) và ô trống không cộng (phạt tự động của gốc đã bị tắt, không làm).
- `soNgayDiLam` = số ngày có ít nhất một ca ở trạng thái 1 hoặc 2.
- `CACHTINHLUONG = 30` (theo ca): `TONGLUONG = LUONGCA × soCaDiLam`.
- `CACHTINHLUONG = 60` (tháng theo ca): `TONGLUONG = soCaDiLam × LUONGTHANG / totalDays`.
- `CACHTINHLUONG = 0` hoặc giá trị khác (theo tháng): `TONGLUONG = soNgayDiLam × LUONGTHANG / soNgayTrongThang`, với `soNgayTrongThang` = `totalDays` trừ số Chủ nhật nếu `NGHICHUNHAT = 30`, trừ số Thứ 7 nếu `NGHITHU7 = 30`, trừ cả hai nếu cả hai bằng 30 (đếm đúng theo lịch tháng/năm; không trừ ngày nào trùng).
- `THUONG` = Σ `TTHUONGPHAT.THUONG`, `PHAT` = Σ `TTHUONGPHAT.PHAT` của nhân viên có `NGAY` trong tháng (từ ngày 1 đến hết ngày cuối tháng, so theo ngày); `TAMUNG` = Σ `TTHUCHI.CHI` của nhân viên có `LATAMUNG = 30`, `LOAI = 1`, `STATUS = 30`, `NGAY` trong tháng (chỉ `CHI`, không trừ `THU`: Chiến chưa chốt R1, Claude chọn đề xuất, T6).
- `THUCNHAN = TONGLUONG + THUONG − PHAT − TAMUNG` (có thể âm: hiện màu đỏ, vẫn lưu).
- Tiền: tính bằng `decimal`, **không làm tròn trong công thức**; khi lưu ghi theo độ chính xác của cột (đọc từ CSDL); khi hiện dùng định dạng nghìn không số lẻ (làm tròn hiển thị `MidpointRounding.AwayFromZero`) (T7). Lương cơ bản hiện = `LUONGCA` khi theo ca, `LUONGTHANG` ngược lại.
- Sau khi mở bảng lương đã lưu: tiền hiện **lấy từ `TBANGLUONGTONGHOP` đã lưu** (snapshot), chấm công từ `TBANGLUONGCHITIET`; tính lại tại chỗ mỗi khi sửa ô chấm công dùng **lương cơ bản/cách tính đã lưu của dòng** (không đọc lại danh mục); nhân viên chưa có dòng tổng hợp thì dùng danh mục nhân viên hiện tại và thưởng/phạt/tạm ứng hiện tại.

## 4. Lưu (nút Cập nhật), một transaction

- Chế độ **Tính lương**: với mỗi nhân viên có dòng trong lưới (kể cả chưa chấm công): upsert `TBANGLUONGTONGHOP` (`LUONGCA`, `LUONGTHANG`, `CACHTINHLUONG`, `TONGLUONG`, `THUONG`, `PHAT`, `TAMUNG`, `THUCNHAN`, `TBANGLUONGID`, `DNHANVIENID`; cột chuẩn người/giờ tạo hoặc sửa; không đụng `SOGIOLAM`, `SOGIOTANGCA`, `LUONGTHEOCA`). Nhân viên không có ô chấm công và không có tiền thì **không** tạo dòng tổng hợp mới.
- Cả hai chế độ: đồng bộ `TBANGLUONGCHITIET` theo lưới: ô có trạng thái 1/2/3 mà chưa có dòng thì thêm, đã có thì cập nhật `TRANGTHAI`; ô trống mà đang có dòng thì **xóa dòng** (gốc không xóa: ô về 0 thì giữ dòng cũ, lỗi nhỏ, T8). Khóa duy nhất theo (nhân viên, ca, ngày đầy đủ cả năm 4 chữ số; gốc dùng 2 chữ số năm nên trùng key im lặng, không lặp lại); nếu DB đã có dòng trùng khóa thì dùng dòng đầu và xóa các dòng thừa khi lưu.
- Ghi lưu vết một dòng "Cập nhật bảng lương Tháng m/yyyy (n nhân viên)" (chuỗi cắt vừa 255). Lỗi giữa chừng rollback toàn bộ. Báo "Đã cập nhật" khi xong; tải lại lưới từ DB.
- **Không có chốt/khóa bảng lương** (như gốc): bảng đã tính vẫn sửa/xóa/chi lại tự do (T9).

## 5. Chi lương (patch P3b, nút **Chi lương** ở màn Tính lương, chỉ chế độ Tính lương, cần quyền "Tính lương" Sửa)

- Hộp **Chi lương** của bảng lương đang chọn (chỉ từ bảng đã lưu): lưới các nhân viên có dòng tổng hợp: **Nhân viên**, **Thực nhận** (`THUCNHAN`), **Đã chi** (Σ `TTHUCHI.CHI` có `TBANGLUONGID` = bảng này và `DNHANVIENID` = nhân viên, `LOAI = 1`, `STATUS = 30`), **Còn lại** (= Thực nhận − Đã chi, có thể âm: chi quá), **Số phiếu** (số phiếu đã chi, nếu nhiều phiếu thì nối bằng dấu phẩy); hàng tổng. Nút **Tạo phiếu chi lương** (dòng chưa chi đủ; vẫn cho tạo thêm khi đã có phiếu: hỏi xác nhận "Nhân viên đã có phiếu chi lương X đ, tạo thêm phiếu?", T10 mở rộng so với gốc là chỉ cho tạo khi chưa có phiếu), **Xem/Sửa phiếu** (mở phiếu đã có), **Xóa phiếu** (xóa phiếu chi lương, quỹ trả lại).
- **Phiếu chi lương:** dùng đúng hàm ghi phiếu chi và luật quỹ của patch Phiếu thu chi (`PhieuThuRepository`, `FundLedgerRules`, `FundRules`, sinh số phiếu `PC` + yy + `/` + 5 chữ số; **không viết bản sao thứ hai**), với: `LOAI = 1`, `TBANGLUONGID` = bảng lương, `LOAIDOITUONG = 1`, `DNHANVIENID` = nhân viên, `TENDOITUONG` = tên nhân viên (khóa), `LATAMUNG` = **0/null** (không phải tạm ứng), diễn giải mặc định "Chi tiền lương tháng m năm y" (sửa được), **Số tiền mặc định = `Còn lại` nếu > 0, ngược lại `THUCNHAN`** (T10: gốc mặc định `THUCNHAN`; chọn Còn lại để chi nốt đúng; sửa được), lý do mặc định = lý do của phiếu chi lương gần nhất nếu có (`SELECT FIRST 1 DLYDOTHUCHIID ... WHERE TBANGLUONGID IS NOT NULL ORDER BY TIMECREATED DESC`), cho phép chuyển khoản (bắt buộc chọn tài khoản như phiếu chi thường), Ngày phải nằm trong tháng của bảng lương ("Ngày phải nằm trong tháng m năm y"; gốc chỉ giới hạn khi mở từ bảng lương, ở đây luôn có), Cửa hàng như phiếu chi thường. `THUCNHAN = 0` và `Còn lại = 0`: cảnh báo "Nhân viên này không còn tiền lương cần chi" nhưng vẫn cho tạo nếu bấm đồng ý (như gốc cảnh báo). **Không** có trần so với Thực nhận (như gốc), chỉ cảnh báo màu khi tổng đã chi vượt Thực nhận.
- Phiếu chi lương hiện ở Danh mục phiếu chi như phiếu thường nhưng **không sửa/xóa được ở đó** (đã đúng luật patch Phiếu thu chi: phiếu có `TBANGLUONGID`); màn Chi lương là nơi sửa/xóa.

## 6. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu, số cụ thể)

**P3a:**
1. Bảng lương: Thêm "Tháng 3/2004" có `THANG = 3`, `NAM = 2004`, `NAME = "Tháng 3/2004"`; thêm trùng chặn; tháng 13, năm 3000 chặn; danh sách sắp mới trước.
2. Công thức theo ca: NV A `LUONGCA = 100.000`, ca "Sáng" `TILELUONG = 100`, ca "Tối" `TILELUONG = 50`; chấm A: 10 ngày Sáng đi làm, 4 ngày Tối đi làm, 2 ngày Sáng nghỉ có phép, 1 ngày Sáng nghỉ không phép → `soCaDiLam = 12 + 4 × 0,5 = 14`, `TONGLUONG = 1.400.000`; nghỉ không phép không cộng.
3. Theo tháng (tháng 3/2004: 01/03/2004 là thứ Hai, 31 ngày, 4 Chủ nhật, 4 Thứ 7): NV B `LUONGTHANG = 3.100.000`; 20 ngày có chấm công. Cả hai không nghỉ: mẫu 31, `20 × 3.100.000 / 31 = 2.000.000`; nghỉ CN: mẫu 27; nghỉ T7: mẫu 27; nghỉ cả hai: mẫu 23; đúng từng trường hợp (tính số cụ thể bằng tay, không lấy kết quả từ chính hàm đang test).
4. Tháng theo ca (60): C `LUONGTHANG = 3.100.000`, 10 ca → `10 × 3.100.000 / 31 = 1.000.000`.
5. Thưởng/phạt/tạm ứng: có thưởng 200.000, phạt 50.000 (cùng tháng) và 40.000 ở tháng khác, tạm ứng 500.000 trong tháng: `THUCNHAN = TONGLUONG + 200.000 − 50.000 − 500.000`, phiếu tháng khác không tính; phiếu thu `LATAMUNG = 30` (THU 100.000) không trừ vào `TAMUNG`.
6. Lưu (Tính lương): `TBANGLUONGTONGHOP` đúng một dòng mỗi nhân viên có chấm công/tiền với các cột tiền đúng; `TBANGLUONGCHITIET` đúng số dòng và `TRANGTHAI`; lưu lần hai không nhân đôi dòng; đưa ô về trống thì dòng chi tiết bị xóa; sửa ô → số tiền lưới đổi ngay (trong bộ nhớ) trước khi lưu, DB chưa đổi.
7. Chế độ Chấm công: ẩn 6 cột tiền và nút Tải lại, Chi lương; Cập nhật chỉ ghi chi tiết chấm công, **không tạo/sửa dòng tổng hợp** (kiểm DB); người chỉ có Chấm công không đọc/ghi tiền.
8. Tải lại: đổi `LUONGCA` của NV A trong danh mục rồi Tải lại → lương cơ bản và `TONGLUONG` đổi; chấm công giữ nguyên; nhân viên mới thêm vào danh mục xuất hiện ở lưới.
9. Xóa bảng lương: bảng có phiếu chi lương bị chặn nêu số phiếu; bảng không có phiếu xóa sạch cả ba bảng (không còn dòng mồ côi); một transaction (hook lỗi thì giữ nguyên); lưu vết đúng một dòng; chuỗi 255 ký tự không làm lưu vết tràn.
10. Quyền: chỉ Xem thì mọi nút khóa; menu "Tính lương" và "Chấm công" mở đúng chế độ; không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

**P3b:**
11. Chi lương: A `THUCNHAN = 1.000.000`, chưa chi: Đã chi 0, Còn lại 1.000.000; tạo phiếu mặc định 1.000.000 → `TTHUCHI` `LOAI = 1`, `CHI = 1000000`, `TBANGLUONGID`, `DNHANVIENID`, `LATAMUNG` không phải 30, diễn giải "Chi tiền lương tháng 3 năm 2004", quỹ tiền mặt giảm đúng 1.000.000; sau đó Đã chi 1.000.000, Còn lại 0.
12. Chi một phần 600.000 rồi tạo thêm: số tiền mặc định 400.000 (Còn lại); hỏi xác nhận khi đã có phiếu; chuyển khoản thiếu tài khoản bị chặn; ngày ngoài tháng chặn; sửa số tiền phiếu cập nhật quỹ; xóa phiếu trả quỹ.
13. Phiếu chi lương không hiện ở Tạm ứng lương, không trừ vào `TAMUNG`; không sửa/xóa được ở Danh mục phiếu chi; chi vượt quỹ xử lý như phiếu chi thường; một transaction; lưu vết; quyền; menu.

## 7. Giao diện

Lưới chấm công là `DataGridView` tự dựng cột ngày (n cột hẹp), tô màu ô qua `ThemeManager`, không hard-code màu; hàng tiêu đề đủ cao cho "ngày\nT2"; cột tiền định dạng nghìn, căn phải; không cuộn dọc cho thanh nút ở 1366×768 (cuộn lưới được); cảm ứng được (nút chấm công đủ lớn); thanh tiến độ khi tải/ lưu nhiều nhân viên (BackgroundWorker nếu thao tác lâu).

## 8. Điểm TẠM

- **T1** Chặn trùng (tháng, năm) của bảng lương là thêm so với gốc.
- **T2** Xóa bảng lương xóa cả `TONGHOP` (gốc để mồ côi) và chặn khi đã có phiếu chi lương.
- **T3** Tải lại đè lương cơ bản bằng danh mục (như gốc) nhưng có hộp xác nhận.
- **T4** Chế độ Chấm công không lưu tiền (R8: Claude chọn đề xuất, Chiến chưa chốt).
- **T5** `TILELUONG` âm coi là 0 (gốc không chặn, hành vi âm không định nghĩa).
- **T6** `TAMUNG = SUM(CHI)` (R1: Chiến chưa chốt).
- **T7** Cách làm tròn tiền (gốc không nêu rõ).
- **T8** Ô chấm công về trống thì xóa dòng chi tiết (gốc giữ dòng cũ).
- **T9** Không chốt/khóa bảng lương (như gốc).
- **T10** Chi lương cho phép tạo thêm phiếu và mặc định số tiền còn lại (gốc chỉ tạo khi chưa có phiếu, mặc định thực nhận).
- **T11** Chưa có ảnh app gốc để đối chiếu bố cục lưới, màu ô chấm công, tên nút (dùng mặc định dự án).
