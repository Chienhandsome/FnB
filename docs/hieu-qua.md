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
| 26/11 In P1a, Thống kê doanh thu (`100513f`) | 11, +737 −103 | 4, +1306 −2 | 1 / 0 / 0 / 0 | 0 (đổi xuống dòng 1 file để gỡ chặn nhầm) | 898 → 925 (0) | selftest 2 (cùng gốc: in thừa cột app gốc ẩn) | chưa bấm thử |

**Cộng 6 chức năng có code:** 10 vòng thừa: Claude 3, agy 5, đổi yêu cầu 0, lẫn 2 (một lần đổi yêu cầu kèm lỗi agy, một lần agy kèm Claude).

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
