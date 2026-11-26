# Brief: Selftest không phụ thuộc dữ liệu mẫu

- Spec: `docs/specs/sua-selftest-du-lieu-mau.md` (Chiến chọn 26/11). Code `9e7c6f1` (agy, `gemini-4-neon-medium`).
- Selftest 857 đạt, 7 lỗi → **864 đạt, 0 lỗi**. Chỉ sửa `Diagnostics/`.
- S3 đã kiểm: chạy trên bản chép giữ lại, số dòng `SUSER`, `DPHUONGTHUCDAT`, `DBAN`, `DLOAIPHONG`, `TDATHANG` trước và sau bằng nhau, không còn dòng thử, `STATUS` của phương thức đặt không đổi.
- Chưa kiểm: chạy trên CSDL mẫu "sạch" (chưa có bàn nào).
- `docs/workflow-agy.md` §4 vẫn ghi mốc 7 lỗi cũ; chưa sửa vì cần Chiến cho phép.
