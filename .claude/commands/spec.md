---
description: Viết đặc tả (spec) cho một ý tưởng/tính năng mới theo quy trình spec-driven của KAZUKO. Dừng lại chờ Chiến duyệt trước khi code.
---

# Lệnh /spec

Ý tưởng của Chiến: $ARGUMENTS

## Quy trình

1. **Hỏi rõ chỗ mơ hồ trước.** Nếu idea thiếu thông tin quyết định được (nghiệp vụ, phạm vi, màn hình nào, dữ liệu nào), hỏi Chiến 1 lượt câu hỏi ngắn gọn rồi mới viết. Không đoán nghiệp vụ vàng/tiền/kho/công nợ.
2. **Viết spec** vào thư mục `docs/specs/<ten-tinh-nang>/`, mỗi file tối đa khoảng 150 dòng (dài hơn thì tách tiếp theo màn hoặc luồng):
   - `index.md`: dòng `- Trạng thái:`, Bối cảnh, Mục tiêu, Phạm vi, danh sách file phần và mỗi file nói gì
   - `nghiep-vu.md`, `giao-dien.md`, `nghiem-thu.md` (Acceptance criteria, Kế hoạch implement, Rủi ro / câu hỏi mở)

   Mỗi quy tắc chỉ ghi ở một file, nơi khác dẫn link. Spec cũ dạng một file giữ nguyên. Giao cho `agy` thì truyền `-Spec docs/specs/<ten-tinh-nang>/index.md`.

   Nội dung theo khung:
   - Bối cảnh
   - Mục tiêu
   - Phạm vi (làm gì / KHÔNG làm gì)
   - Nghiệp vụ chi tiết (luồng, quy tắc tính toán, trạng thái)
   - Acceptance criteria (checklist kiểm thử được, từng mục cụ thể)
   - Kế hoạch implement (các bước, module/file chạm tới, migration nếu có)
   - Rủi ro / câu hỏi mở
3. **DỪNG LẠI.** Trình bày spec cho Chiến duyệt. KHÔNG code, KHÔNG làm patch khi spec chưa được duyệt.
4. Khi Chiến duyệt: chạy plan mode, implement theo spec, verify từng mục acceptance criteria rồi báo kết quả.
