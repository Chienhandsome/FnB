# Brief: Công nợ ban đầu (P2 của Số dư đầu kỳ)
- Spec: `docs/specs/so-du-dau-ky.md` (Claude tự duyệt 27/11).
- **27/11 đã code, push `a66609e`** (#56): `OpeningDebt*`: một khung hai chế độ khách hàng và nhà cung cấp, mỗi đối tác một phiếu `LOAI = 99` hoặc `98` `NAME = CONGNODAU`, nợ 0 xóa phiếu, nợ âm chặn, chặn giảm nợ đầu dưới số đã thu theo đơn, một transaction, lưu vết, quyền, xuất Excel, menu nhận theo tên không dấu (kể cả tên gốc thiếu dấu). Các hàm công nợ cũ đã tính đúng 99/98 nên không sửa.
- **Chưa kiểm:** bấm thật, 125% DPI. Chưa có nhập Excel (P5).
