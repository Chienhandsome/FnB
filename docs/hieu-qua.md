# Thống kê hiệu quả theo chức năng

Mỗi chức năng một mục, ghi khi làm xong (kể cả khi dừng giữa chừng). Chỉ ghi số đo được; không đo được thì ghi "chưa đo". Model và thời gian agy lấy từ `_tmp/<feature>/run-*/` (`agy.log`, `agy-out.txt`). Hạn mức là của cả tài khoản Claude nên mức chênh chỉ gần đúng.

## 26/11 · Khách đặt hàng đợt 2 (`bf45f06`, issue #24)

| Mục | Số đo |
|---|---|
| agy | `Gemini 4 Neon (High)`; 3 vòng: 495 s, 833 s, 2195 s (≈ 59 phút) |
| Token agy ghi cho từng lượt (chưa rõ cộng dồn hay riêng) | 286.667 · 311.837 · 440.854 |
| Vì sao thêm vòng | V2: **Claude** dặn sai cách đọc số. V3: **đổi yêu cầu** (quy tắc nhập số mới), kèm lỗi **agy** làm selftest treo |
| Claude tự sửa | 1 (biên dịch) |
| Selftest | 798 → 814 đạt; 7 lỗi cũ |
| Lỗi bắt ở đâu | đọc diff 1, selftest 1 (treo), build 1 |
| Hạn mức Claude đầu → cuối | chưa đo |
| Bài học | Thử trên lưới thật trước khi dặn agy luật đọc số |

## 26/11 · Nhập số patch 1, ô ngoài lưới (`969285d`, issue #25)

| Mục | Số đo |
|---|---|
| agy | `Gemini 4 Neon (High)`; 4 lượt, 3 vòng: 173 s + 1020 s, 1238 s, 1586 s (≈ 67 phút) |
| Token agy ghi cho từng lượt (như trên) | 228.408 · 480.673 · 503.405 · 553.376 |
| Vì sao thêm lượt, vòng | Lượt bị dừng: **agy** gọi lệnh cấm. V2: **agy** viết ca test kỳ vọng sai. V3: **Claude** dặn lệch spec (ô không ngăn nghìn) |
| Claude tự sửa | 1 (biên dịch) |
| Selftest | 814 → 845 đạt; 7 lỗi cũ |
| Lỗi bắt ở đâu | selftest 2 ca, ảnh chụp 1, build 1 |
| Hạn mức Claude | giữa vòng 1 → cuối: 5 giờ 14% → 24%, tuần 84% → 85% (gồm cả việc khác trong phiên) |
| Bài học | Câu "được giữ như cũ" trong prompt phải đối chiếu lại với spec; ảnh có dữ liệu bắt được chỗ selftest không kiểm |
