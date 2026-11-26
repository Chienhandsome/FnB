# Quy định model cho agy

Chiến duyệt 26/11/2026. Bổ sung cho [`workflow-agy.md`](workflow-agy.md); chỗ giới hạn số vòng sửa vẫn theo file đó.

## Chọn model

Truyền bằng `tools/agy/Invoke-Agy.ps1 -Model <tên>`.

| Việc giao cho agy | `-Model` | Lý do |
|---|---|---|
| **Mặc định:** viết code và sửa lỗi | `gemini-4-neon-high` | Google giới thiệu Neon cân bằng độ sâu và hiệu quả cho việc code phức tạp |
| Vòng sửa nhỏ: đổi hiển thị, sửa ca test; không đụng tiền, kho, quyền, xóa | `gemini-4-neon-medium` | Dự kiến tốn ít hạn mức hơn; chưa đo |
| Việc lớn, dài, đụng nhiều phần (ví dụ Sử dụng dịch vụ) | vẫn `gemini-4-neon-high`; `gemini-4-carbon-high` **chỉ khi Chiến đồng ý** | Google khuyên Carbon cho dự án khó và dài nhất; tốn nhiều token hơn |
| Hỏi agy ở `-Mode plan` | `gemini-4-hydrogen-low` | không viết code |

## Quy tắc

1. **Luôn truyền `-Model`.** Lệnh `/model` của agy đổi model mặc định và giữ qua các phiên.
2. **Nâng một bậc** (Neon High → Neon XHigh → Carbon) khi agy tự gây lỗi nặng 2 vòng liền trên cùng một việc. Vòng thừa do prompt, spec hoặc đổi yêu cầu không tính.
3. **Không dùng Hydrogen** viết code đụng tiền, kho, công nợ, quyền, xóa dữ liệu (quy tắc thận trọng của dự án, không phải khuyến cáo của Google).
4. **Không dùng model Claude hay GPT trong agy:** người viết khác người kiểm.
5. **Chưa dùng Orchestrator.** Google đề xuất nó làm mặc định cho làm tính năng và sửa lỗi, nhưng nó chạy model lập kế hoạch cùng model thực thi, không nhận `--effort`, và chưa thử với bộ quyền hiện tại. Muốn dùng phải thử chặn quyền bằng file mồi trước.
6. **Ghi model và số token** từng lượt vào [`hieu-qua.md`](hieu-qua.md).
7. **Xem lại** khi agy đổi bản hoặc Google đổi danh sách: chạy `agy models` trước khi giao. Theo tài liệu Google, tên model sai thì lượt chạy báo lỗi, không tự đổi model.

## Số liệu lúc soạn (26/11/2026)

- `agy models` trên máy này có đủ 4 tên ở bảng trên. Chưa chạy thử lượt nào bằng `-Model`; có trong danh sách chưa chứng minh gói tài khoản đủ hạn mức cho Carbon.
- Các lượt trước không truyền `-Model`; log quét được đều ghi `Gemini 4 Neon (High)`.
- Google báo gỡ ngày 30/11: Gemini 4 Argon, Gemini 3.8 Flash, GPT-OSS-120b, Claude 4.6. Quy định này không dùng các model đó.
- Gói Pro trả phí: hạn mức làm mới mỗi 5 giờ tới khi hết hạn mức tuần; Google không công bố con số.
- Giá API Neon: 3 USD / triệu token vào, 15 USD / triệu token ra; theo Google, ở mức High rẻ hơn Carbon 32% mỗi tác vụ.
- Các trang Google không khớp nhau: ngữ cảnh Neon ghi 512 nghìn và 1 triệu token; mức suy nghĩ mặc định ghi Medium và High.
- Điểm benchmark là do Google công bố, chưa đo trên dự án này.

## Nguồn

- [Danh sách model](https://antigravity.google/docs/models/) · [Tham chiếu CLI](https://antigravity.google/docs/cli/reference/) · [Headless](https://antigravity.google/docs/cli/headless/) · [Gói và hạn mức](https://antigravity.google/docs/plans/)
- [Giới thiệu Neon](https://blog.google/innovation-and-ai/models-and-research/gemini-models/introducing-gemini-4-neon/) · [Carbon](https://antigravity.google/blog/gemini-4-carbon-in-google-antigravity) · [Hydrogen](https://antigravity.google/blog/gemini-4-hydrogen-in-google-antigravity)
