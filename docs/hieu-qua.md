# Thống kê hiệu quả theo chức năng

Mỗi chức năng một dòng ở bảng 1 và 3, ghi khi làm xong (kể cả khi dừng giữa chừng). Chỉ ghi số đo được; không đo được thì ghi "chưa đo". Viết lại 26/11 theo 5 mục Chiến yêu cầu.

## Cách đo

- **Cỡ thay đổi:** `git show --numstat <commit> -- Kazuko_FnB`, tách `Diagnostics/` (selftest) khỏi phần còn lại (code, gồm `.csproj`). `+` là dòng thêm, `−` là dòng bỏ.
- **Vòng thừa:** mỗi lượt agy chạy thêm sau lượt đầu (kể cả lượt chỉ để hỏi hoặc làm tiếp sau khi bị dừng) hoặc mỗi vòng sửa tính là 1, theo số đã ghi ở bảng cũ. Nguyên nhân: Claude / agy / Đổi yêu cầu / Lẫn (từ hai bên trở lên).
- **Lỗi sau push:** lỗi thấy sau khi code lên `main`. Chiến chưa bấm thử thì ghi "chưa bấm thử", **không ghi 0**. Chiến báo lỗi thì cập nhật dòng đó.
- **Hạn mức Claude:** đo bằng `get_usage` lúc bắt đầu và kết thúc từng chức năng. Là số của cả tài khoản nên phải ghi việc khác lẫn vào. Cửa sổ 5 giờ làm mới giữa chừng thì không trừ được.
- **Thời gian, token agy:** `duration_seconds` và `usage` trong `agy-out.txt` là **cộng dồn theo hội thoại**.
  - Thời gian: đã xác minh 26/11 trên 18 lượt. Giờ mở hội thoại cộng `duration_seconds` khớp giờ ghi `agy-out.txt` (lệch 12–20 giây), tức là gồm cả khoảng chờ giữa các lượt. **Không lấy số này làm thời gian chạy.** Thời gian một lượt = giờ ghi `agy-out.txt` trừ giờ trên tên thư mục `run-*`.
  - Token: **suy ra** là cộng dồn (không giảm ở lượt nào, kể cả lượt 23 giây; `total` = `input` + `output`), log không ghi rõ. Một lượt = số của lượt đó trừ lượt trước cùng hội thoại. Tổng chức năng = số của lượt cuối. `cache_read` đếm riêng, không nằm trong `total`.

## Bảng 1. Kết quả theo chức năng

| Ngày, chức năng | Code (file, dòng) | Selftest (file, dòng) | Vòng thừa: Claude / agy / Đổi YC / Lẫn | Claude tự sửa | Selftest đạt (lỗi) | Lỗi bắt trước push | Lỗi sau push |
|---|---|---|---|---|---|---|---|
| 26/11 Khách đặt hàng đợt 2 (`bf45f06`, #24) | 6, +274 −40 | 3, +494 −13 | 1 / 0 / 0 / 1 | 1 (biên dịch) | 798 → 814 (7 cũ) | đọc diff 1, selftest 1, build 1 | chưa bấm thử |
| 26/11 Nhập số patch 1 (`969285d`, #25) | 13, +488 −48 | 3, +974 −0 | 1 / 2 / 0 / 0 | 1 (biên dịch) | 814 → 845 (7 cũ) | selftest 2, ảnh chụp 1, build 1 | chưa bấm thử |
| 26/11 Nhập số patch 2 (`054d7c7`, #26) | 10, +490 −110 | 5, +921 −8 | 0 / 2 / 0 / 0 | 0 (đổi xuống dòng 1 file để gỡ chặn nhầm) | 845 → 857 (7 cũ) | selftest 4; đọc diff 0 | chưa bấm thử |
| 26/11 Sửa 7 ca selftest (`9e7c6f1`) | 1, +1 −0 | 2, +163 −14 | 0 / 1 / 0 / 0 | 0 | 857 (7) → 864 (0) | không có lỗi sau vòng 1 | không áp dụng (chủ yếu sửa test) |
| 26/11 Spec in, xuất Excel | không đổi code | – | – | – | – | thử ngoài app: thiếu `No1Lib*.dll` | – |
| 26/11 In FastReport P0 (`30ced3e`, `8a9c0ee`, #27) | 14, +1570 −7 | 4, +1161 −4 | 0 / 0 / 0 / 1 | 1 (biên dịch, trong test) | 864 → 898 (0) | build 1, selftest 3, thử ngoài app + app gốc (lỗi tiền) | chưa bấm thử. Claude thấy khi làm P1a: lớp in chưa ẩn cột theo `VisibleByConfigID` (sửa ở `100513f`; chưa kiểm 3 mẫu mặt hàng bán có dùng thuộc tính này không) |
| 27/11 Nhập hàng vào kho (`cedccd2`, #43) | 23 | 2 | 0 / 1 / 0 / 1 | 1 (thiếu using FlatButton) | 1220 → 1234 (0) | build 1 (lỗi biên dịch); selftest 1 ca gõ phím thật lỗi chập chờn do tranh tiêu điểm, chạy lại đạt | chưa bấm thử |
| 27/11 Danh mục nhà cung cấp (`0228433`, #41) | 21 | 1 | 0 / 2 / 0 / 0 | 2 (lọc cây nhóm, quyền Thủ kho trên UI) rồi 1 (tiêu đề cột bị cắt) | 1197 → 1220 (0) | selftest 3 (ca mới của agy; build 0) | chưa bấm thử |
| 27/11 Danh mục kho hàng (`b4bb293`, #40) | 15, +? | 1 | 0 / 0 / 0 / 0 | 1 (ghi lưu vết không cắt chuỗi 255 ký tự: ca tên kho dài 255) | 1176 → 1197 (0) | selftest 1 (ngoại lệ tràn chuỗi) | chưa bấm thử |
| 27/11 Quỹ: phiếu thu/chi, danh mục + hai sửa nhỏ (`66e5b4c`, #39) | 26, +705 −362 (sửa) và 15 file mới | 3 file selftest | 0 / 1 / 0 / 1 | 0 | 1149 → 1176 (0) | build 0, selftest 0 ngay lần đầu; kiểm phạm vi dừng vì Claude ghi nhầm thư mục | chưa bấm thử |
| 27/11 Quản lý bán hàng và Điều chỉnh hóa đơn (`92f92de`, #38) | 26, +4492 −15 | 5, +1234 −1 | 0 / 2 / 0 / 0 | 3 (hai chỗ test dùng cột không có thật; in ngăn xếp ngoại lệ trong `SelfTest.cs`) | 1129 → 1149 (0) | build 0, selftest 3 vòng chẩn đoán (SQL cột sai, âm kho, cọc dư, thanh toán chung, tên cột lưới) | chưa bấm thử |
| 26/11 Sử dụng dịch vụ patch (d), in hóa đơn và in bếp (`ac6c750`, #37) | 28, +3511 −9 | 6, +962 −6 | 0 / 2 / 0 / 0 | 5 (test dùng cột không có thật 2 chỗ; tham số mẫu kiểm đồ; kiểu `Tạm tính`; mẫu bếp thật dùng nguồn `DataSet` và tham số khác; kỳ vọng số lỗi in bếp) | 1109 → 1129 (0) | build 0, selftest 5 vòng chẩn đoán (SQL cột sai, mẫu, tham số) | chưa bấm thử |
| 26/11 Sử dụng dịch vụ patch (c), chuyển/gộp bàn, hủy bill (`35c0878`, #36) | 17, +2128 −7 | 5, +989 −4 | 0 / 0 / 0 / 0 | 0 | 1092 → 1109 (0) | không có lỗi bắt được; build, selftest đạt ngay vòng 1 | chưa bấm thử |
| 26/11 Sử dụng dịch vụ patch (b), thanh toán (`56f2036`, #35) | 23, +3866 −11 | 6, +1292 −4 | 0 / 1 / 0 / 0 | 0 | 1064 → 1092 (0) | selftest 0, build 0 (đạt ngay vòng 2); đọc diff: luật phân bổ nợ/cọc | chưa bấm thử |
| 26/11 Sử dụng dịch vụ patch (a) (`91a57c7`, #34) | 22, +5460 −0 | 4, +842 −0 | 0 / 0 / 0 / 0 | 3 (4 hàm `internal` gọi qua assembly; `NULLIF(@T)` Firebird −804; ca test bấm nút khi chưa chọn bàn) | 1018 → 1064 (0) | build 1 (không dịch được), selftest 2 (SQL −804; ca nút) | chưa bấm thử |
| 26/11 Danh mục khách hàng (`018ba78`, #33) | 16, +3346 −33 | 4, +1097 −0 | 0 / 1 / 0 / 0 | 1 (thứ tự ca test sửa chính mình) | 979 → 1018 (0) | selftest 2 (cùng gốc); đọc diff: không thấy lỗi ở tầng Data, luật xóa | chưa bấm thử |
| 26/11 In P4, mã vạch ở Danh mục (`bdbf7b0`, #32) | 14, +1565 −11 | 4, +1170 −0 | 0 / 0 / 0 / 0 | 1 (4 ca test kỳ vọng sai về FastReport: tem trống không phát ô mã vạch; ca tự xóa mặt hàng rồi so snapshot) | 965 → 979 (0) | selftest 4 (cùng gốc: kỳ vọng test); đọc diff 0 | chưa bấm thử |
| 26/11 In P3a, xuất Excel của lưới (`f9874e2`, #31) | 12, +356 −6 | 5, +967 −4 | 0 / 0 / 0 / 0 | 0 | 949 → 965 (0) | không có lỗi bắt được; phạm vi, build, selftest đạt ngay lần đầu | chưa bấm thử |
| 26/11 In P2, Báo giá và Khách đặt hàng (`29fdaa8`, #30) | 23, +1515 −77 | 7, +1244 −3 | 0 / 0 / 0 / 0 | 2 (cột `LOAILYDO` trong dữ liệu test; thứ tự tên phòng) + đổi xuống dòng 1 file để gỡ chặn nhầm | 923 → 949 (0) | selftest 2 (cùng một ca vỡ giữa chừng, rồi lỗi thứ tự phòng); đọc diff 0 | chưa bấm thử |
| 26/11 In P1b, hai màn xem nhúng (`2be56da`, #29) | 12, +811 −1285 | 8, +1134 −648 | 0 / 0 / 0 / 0 | 2 (biên dịch trong test; chữ xuống dòng trong chi tiết ca làm script đọc sai tên ca) + xóa 4 file lưới agy không xóa được | 925 → 923 (0); bỏ 16 ca giao diện cũ (Chiến đồng ý), thêm 14 | build 1 (CS0165), selftest 0, kiểm phạm vi chặn nhầm 2 lần (CRLF; pull ở dự án khác, lối tắt Desktop) | chưa bấm thử |
| 26/11 In P1a, Thống kê doanh thu (`100513f`) | 11, +737 −103 | 4, +1306 −2 | 1 / 0 / 0 / 0 | 0 (đổi xuống dòng 1 file để gỡ chặn nhầm) | 898 → 925 (0) | selftest 2 (cùng gốc: in thừa cột app gốc ẩn) | chưa bấm thử |

**Cộng 20 chức năng có code:** 10 vòng thừa: Claude 3, agy 5, đổi yêu cầu 0, lẫn 2 (một lần đổi yêu cầu kèm lỗi agy, một lần agy kèm Claude).

## Bảng 2. Thời gian và token agy từng lượt

Số của **riêng lượt đó** (đã trừ cộng dồn), token tính bằng nghìn, làm tròn, ghi "vào / ra". Model: P1a, P0, Nhập số patch 2: `gemini-4-neon-high`; Khách đặt hàng, Nhập số patch 1: Gemini 4 Neon (High) mặc định; Sửa selftest: `gemini-4-neon-medium`. Lượt "hỏi" chạy `gemini-4-hydrogen-low`.

| Chức năng | Từng lượt: thời gian; token vào / ra | Tổng thời gian chạy | Tổng token (cache đọc) |
|---|---|---|---|
| Khách đặt hàng đợt 2 | 510 s; 233 / 54 · 88 s; 18 / 7 · 462 s; 78 / 51 | 1060 s (18 phút) | 441 (4.702) |
| Nhập số patch 1 | 185 s; 221 / 7 · 815 s; 170 / 82 · 98 s; 15 / 8 · 205 s; 37 / 13 | 1303 s (22 phút) | 553 (9.264) |
| Nhập số patch 2 | 979 s; 406 / 99 · 2059 s; 431 / 207 · hỏi 23 s; 228 / 2 · 523 s; 99 / 62 | 3584 s (60 phút) | 1.533 (16.937) |
| Sửa 7 ca selftest | 49 s; 54 / 1 · hỏi 21 s; 56 / 0 · 250 s; 73 / 22 | 320 s (5 phút) | 206 (1.161) |
| In P0 | 883 s; 270 / 97 · 136 s; 32 / 9 | 1019 s (17 phút) | 408 (4.390) |
| In P1a | 848 s; 270 / 84 · 499 s; 83 / 41 | 1347 s (22 phút) | 477 (6.309) |
| In P1b | 1283 s; 464 / 113 | 1283 s (21 phút) | 577 (6.645) |
| In P2 | 1143 s; 841 / 104 | 1143 s (19 phút) | 945 (8.970) |
| In P3a | 769 s; 330 / 66 | 769 s (13 phút) | 396 (4.666) |
| In P4 | 1065 s; 429 / 92 | 1065 s (18 phút) | 521 (4.592) |
| Danh mục khách hàng | 333 s; 325 / 6 (dừng: gọi Bash bị từ chối) · 1721 s; 253 / 125 | 2054 s (34 phút) | 709 (8.850) |
| Sử dụng dịch vụ patch (a) | 1572 s; 1077 / 153 | 1572 s (26 phút) | 1.230 (8.450) |
| Sử dụng dịch vụ patch (b) | 1572 s; 697 / 144 (dừng: gọi Bash) · 2090 s; 157 / 49 | 3662 s (61 phút) | 1.046 (12.208) |
| Sử dụng dịch vụ patch (c) | 1236 s; 498 / 113 | 1236 s (21 phút) | 611 (9.028) |
| Sử dụng dịch vụ patch (d) | 1612 s; 689 / 135 (dừng: gọi Bash) · 1717 s; 146 / 4 (dừng: agy hết quota) | 3329 s (55 phút) | 974 (9.653) |
| Nhập hàng vào kho | 877 s (dừng: gọi Bash) · 1279 s | 2156 s (36 phút) | chưa đo |
| Danh mục nhà cung cấp | 925 s · 1458 s · 1788 s | 4171 s (70 phút) | chưa đo |
| Danh mục kho hàng | 1226 s | 1226 s (20 phút) | chưa đo |
| Quỹ phiếu thu/chi (+ A1, A2) | 895 s (dừng: gọi Bash) · 1618 s | 2513 s (42 phút) | chưa đo |
| Quản lý bán hàng + Điều chỉnh hóa đơn | 1569 s; 1136 / 180 (dừng: gọi Bash) · 1823 s; 69 / 21 · 3014 s; 104 / 23 | 6406 s (107 phút) | 1.532 (13.211) |

In P1a còn 3 lượt thử quyền sau khi agy lên 1.5.18 (3, 6, 4 s; 37 nghìn token), không tính vào tổng.

Các tổng ghi ở bản trước (khoảng 59, 67, 38 phút) đã cộng chồng số cộng dồn nên **sai, cao hơn thực tế**.

## Bảng 3. Hạn mức Claude và bài học

| Chức năng | Hạn mức đầu → cuối | Việc khác lẫn vào | Bài học |
|---|---|---|---|
| Khách đặt hàng đợt 2 | chưa đo | – | Thử trên lưới thật trước khi dặn agy luật đọc số |
| Nhập số patch 1 | 5 giờ 14% → 24%, tuần 84% → 85% (số đầu đo giữa vòng 1) | có: việc khác trong phiên | Câu "được giữ như cũ" trong prompt phải đối chiếu lại với spec |
| Nhập số patch 2 | 5 giờ 27% → 36%, tuần 85% → 86% (đầu phiên → push) | có: câu hỏi thêm về lỗi cũ, in/Excel | Ca đếm `DataError` bắt được lỗi ngầm có từ trước |
| Sửa 7 ca selftest, Spec in | 5 giờ 36% → 44%, tuần 86% → 87% (đo trước khi push docs) | hai việc chung một số | Prompt đã nêu luật gạch đứng mà agy vẫn dùng. Thử dựng mẫu thật trước khi viết spec mới lộ ra phụ thuộc `No1Lib` |
| In P0 | đầu: chưa đo; lúc push: 5 giờ 77%, tuần 92% | cả phiên | Thử mẫu thật với đúng bộ tham số trước khi viết prompt; so app gốc bằng số liệu phân biệt được |
| In P1a | tuần 92% → 95%; 5 giờ: 79% lúc đầu, làm mới giữa chừng, 7% sau push | có: dựng thử 3 mẫu của P1b, đối chiếu app gốc cả 3 màn, câu hỏi về file này. Tuần lúc giao agy: 94%. Phần sửa file này (sau lúc đo) chưa tính | Trang app gốc ít cột hơn mẫu thì phải tìm luật ẩn (`VisibleByConfigID`) trước khi viết prompt |
| In P1b | tuần 4% → 7% (vừa làm mới đầu việc); 5 giờ 24% → 47% | có: câu hỏi về FVM, gỡ chặn phạm vi | Thử dựng mẫu và cách gọi script trước khi viết prompt thì agy làm đúng ngay vòng 1. Chi tiết ca test có xuống dòng làm script đọc sai tên ca |
| In P2 | tuần 7% → 11% (7% đo trước khi bắt đầu P2); 5 giờ 78% lúc push | có: thử dựng mẫu, hỏi/xin mở app gốc (không điều khiển được), một lần nén ngữ cảnh | Mẫu của phiếu gắn theo `STABLEDESCID` chứ không theo `SFORMID`: dựng thử chỉ ra trước khi viết prompt. Test tự tạo dữ liệu vỡ vì sai tên cột (`DLYDOTHUCHI.LOAILYDO`), làm selftest dừng giữa chừng và báo "mất ca" |
| In P3a | 5 giờ 78% → 83%; tuần 11% → 11% (đo trước khi viết prompt và sau khi push) | có: soạn trước prompt P4 và dựng thử 9 mẫu lưới, mã vạch | Việc nhỏ, ranh giới rõ (một hàm dùng chung và các nút nối vào) thì agy làm đúng vòng 1, không có sửa của Claude |
| In P4 | 5 giờ 83% → 85%; tuần 11% → 12% | có: sửa test, ghi docs P3a | agy không chạy được selftest nên kỳ vọng về cách FastReport phát đối tượng (ô mã vạch của tem trống) chỉ lộ khi chạy: Claude chạy và sửa test |
| Danh mục khách hàng | 5 giờ 85% → chưa đo lúc push; tuần 12% | có: đổi yêu cầu, trả lời câu hỏi nghiệp vụ, danh sách bắt buộc 2 ngày | Prompt ngắn chỉ trỏ spec thì agy tự đọc đủ, nhưng nhớ nhắc không dùng Bash. Không commit docs ở thư mục chính khi agy đang chạy (phạm vi chặn, mất một vòng) |
| Sử dụng dịch vụ patch (a) | 5 giờ chưa đo; tuần 12% | có: viết spec §6.6, trả lời câu hỏi, loop | agy không dịch được trong sandbox (MSBuild bị chặn thư mục tạm) nên lỗi biên dịch và SQL −804 chỉ lộ ở bước Claude chạy `Test-AgyBuild`. Cho ca selftest in ngăn xếp ngoại lệ đầy đủ (tạm sửa `SelfTest.cs` để chẩn đoán rồi hoàn lại) giúp tìm câu SQL nhanh |
| Sử dụng dịch vụ patch (b) | chưa đo (làm xen kẽ nhiều việc); tuần 12% → chưa đo | có: loop, hỏi đáp, viết docs | Dặn "không dùng Bash" trong prompt vẫn bị agy bỏ qua một lần; thêm bài học lỗi cũ (internal/public, -804) vào prompt giúp vòng 2 đạt ngay lần build đầu |
| Sử dụng dịch vụ patch (c) | chưa đo (xen kẽ nhiều việc) | có: loop, docs | Prompt có đủ bài học cũ và lệnh cấm Bash thì agy làm đạt ngay vòng 1, Claude không phải sửa |
| Sử dụng dịch vụ patch (d) | chưa đo | có: loop, hết quota agy, chờ Chiến đổi tài khoản | Agy hết quota giữa chừng nên Claude chẩn đoán và sửa tay năm chỗ; chẩn đoán nhanh bằng cách tạm cho ca selftest in ngăn xếp đầy đủ. Mẫu STEMPLATE thật có tên nguồn dữ liệu và tham số riêng: đọc Dictionary mẫu trước khi dặn agy, như P2 |
| Quản lý bán hàng + Điều chỉnh hóa đơn | 5 giờ 33% lúc giữa chừng; tuần 17% | có: xin quyền computer-use (không có), trả lời issue, loop | Chạy selftest sau mỗi vòng và gửi nguyên văn từng ca lỗi cho agy sửa (vòng 3) nhanh hơn Claude tự dò; script giao việc cấm dấu ngoặc kép trong lời nhắn |
| Quỹ phiếu thu/chi | chưa đo | có: viết spec phieu-thu-chi, loop | Chạy xong ngay lần đầu khi spec chi tiết và prompt chỉ rõ phần tái dùng (FundRepository, FundLedgerRules). Bài học Claude: .NET `File` dùng thư mục làm việc của tiến trình, không phải của PowerShell: luôn dùng đường dẫn tuyệt đối khi ghi file; script kiểm phạm vi so cả giờ sửa UTC của file ngoài worktree |
| Danh mục kho hàng | chưa đo | có: tự duyệt spec | Ca biên (tên dài đúng 255 ký tự) làm lộ chỗ ghi lưu vết nối chuỗi vào cột 255: dặn agy cắt mọi chuỗi ghi vào cột có giới hạn |
| Danh mục nhà cung cấp | chưa đo | có: viết 4 spec Kho, Công nợ | Ca UI (cây nhóm, quyền, tiêu đề) khiến agy cần 3 vòng; gửi nguyên văn từng ca lỗi cho agy sửa nhanh hơn Claude tự dò. Chốt chặn "file ngoài worktree" báo nhầm khi Chiến deploy AWS ở phiên khác (đã xác nhận); Publish không soi file ngoài nên không cần lách |
| Nhập hàng vào kho | chưa đo | có: viết spec, hỏi Chiến, loop | Ca selftest gõ phím thật (Nhập số P1) chập chờn nếu cửa sổ khác giành tiêu điểm: chạy lại một lần trước khi kết luận lỗi. agy bắt tay làm đủ nhưng bị Bash chặn giữa chừng nên cần vòng nối tiếp |
