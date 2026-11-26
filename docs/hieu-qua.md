# Thống kê hiệu quả theo chức năng

Mỗi chức năng một dòng, ghi khi làm xong (kể cả khi dừng giữa chừng). Chỉ ghi số đo được; không đo được thì ghi "chưa đo". Model, thời gian và token agy lấy từ `_tmp/<feature>/run-*/` (`agy.log`, `agy-out.txt`); token là số agy ghi cho từng lượt, chưa rõ cộng dồn hay riêng. Hạn mức là của cả tài khoản Claude nên mức chênh chỉ gần đúng.

| Ngày, chức năng | agy: model; vòng; thời gian; token | Vì sao thêm vòng | Claude tự sửa | Selftest đạt (lỗi cũ) | Lỗi bắt ở đâu | Hạn mức Claude đầu → cuối | Bài học |
|---|---|---|---|---|---|---|---|
| 26/11 Khách đặt hàng đợt 2 (`bf45f06`, #24) | Gemini 4 Neon (High); 3 vòng; 495 + 833 + 2195 s (≈ 59 phút); 286.667 / 311.837 / 440.854 | V2: **Claude** dặn sai cách đọc số. V3: **đổi yêu cầu**, kèm lỗi **agy** làm selftest treo | 1 (biên dịch) | 798 → 814 (7) | đọc diff 1, selftest 1, build 1 | chưa đo | Thử trên lưới thật trước khi dặn agy luật đọc số |
| 26/11 Nhập số patch 1 (`969285d`, #25) | Gemini 4 Neon (High); 3 vòng, 4 lượt; 173 + 1020 + 1238 + 1586 s (≈ 67 phút); 228.408 / 480.673 / 503.405 / 553.376 | Lượt bị dừng: **agy** gọi lệnh cấm. V2: **agy** viết ca test kỳ vọng sai. V3: **Claude** dặn lệch spec | 1 (biên dịch) | 814 → 845 (7) | selftest 2 ca, ảnh chụp 1, build 1 | giữa vòng 1 → cuối: 5 giờ 14% → 24%, tuần 84% → 85% (gồm việc khác trong phiên) | Câu "được giữ như cũ" trong prompt phải đối chiếu lại với spec |
