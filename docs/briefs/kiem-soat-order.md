# Brief: Kiểm soát order
- Spec: `docs/specs/kiem-soat-order.md` (Claude tự duyệt 28/11).
- **28/11 đã code, push `e750244`** (#65): `OrderControl*`: màn chỉ đọc, `OrderControlAnalyzer` hàm thuần (tách theo dấu phẩy, số hợp lệ chỉ chữ số ASCII ≤ int.MaxValue, trùng tính một lần khi quét thiếu, dải thiếu tính bằng phép trừ), lưới Đã dùng và Cần kiểm tra, tóm tắt, xuất Excel, giới hạn cửa hàng theo quyền.
- **Chưa kiểm:** bấm thật, đối chiếu app gốc (T3 cách xử lý số trùng suy từ mô tả), nhấp đôi mở hóa đơn (T2).
