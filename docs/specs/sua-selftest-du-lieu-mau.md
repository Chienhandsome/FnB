# Spec: Selftest không phụ thuộc dữ liệu có sẵn trong CSDL mẫu

- Trạng thái: **ĐÃ DUYỆT** (Chiến chọn "Sửa ca test" ngày 26/11/2026 trong phiên). Chỉ sửa selftest, không đổi code sản phẩm.
- Base: `origin/main` @ `2b85485`.

## Bối cảnh

Selftest trên bản chép `POS TAP/Database/DATA.fdb` có 7 ca lỗi cố định. Truy vấn bản chép ngày 26/11:

| Ca | Ca test giả định | Dữ liệu thật |
|---|---|---|
| Nhóm người dùng (2 ca) | "Thu ngân" có đúng 1 tài khoản | 0 tài khoản |
| Phương thức đặt (1 ca) | có sẵn "Công văn" và "Trực tiếp" trong danh sách tải được | "Công văn" có `STATUS = 0`, các mục khác 30 |
| Lịch đặt phòng (4 ca) | CSDL chưa có bàn nào, nên tổng phòng = 4 phòng do test tạo | có sẵn 1 bàn ("bàn 1", `STATUS = 30`) |

## Làm

- Mỗi ca tự tạo dữ liệu nó cần trên CSDL được truyền vào, và dọn sạch sau khi chạy, kể cả khi ca lỗi.
- Số kỳ vọng tính từ nguồn độc lập với hàm đang được kiểm (câu SQL riêng của test cộng với dữ liệu test tự tạo), không lấy từ chính kết quả đang kiểm.
- Ca vẫn phải đỏ khi hàm được kiểm sai: đếm sai số tài khoản, bỏ sót mục đang dùng, đếm sai tổng phòng, số còn lại hoặc công suất.

## Không làm

Không sửa code sản phẩm, không sửa file CSDL, không nới hay xóa ca khác.

## Nghiệm thu

- [ ] S1. Trên bản chép CSDL mẫu: 7 ca trên đạt, tổng 0 lỗi, không mất ca đạt nào ở mốc.
- [ ] S2. Diff chỉ nằm trong thư mục `Diagnostics/` (và dòng `Compile Include` nếu thêm file).
- [ ] S3. Sau selftest, CSDL được truyền vào không còn dòng nào do 7 ca này tạo, và dòng có sẵn không bị đổi.
