# Brief: Công nợ khách hàng, nhà cung cấp
- Spec: `docs/specs/cong-no.md` (Claude tự duyệt 27/11; Chiến chốt thu nợ khách phân bổ theo từng đơn).
- **27/11 đã code, push `eafcc4e`** (#46): hai màn (`Debt*`), công thức nợ một bên mỗi đối tượng, hộp Thu nợ theo đơn (một phiếu thu + dòng `TCHITIETTHANHTOAN`, một transaction, không sửa `TIENTHANHTOAN`, Còn nợ từng đơn đã trừ các lần phân bổ trước), Chi trả nợ NCC qua hộp Phiếu chi của Quỹ, xuất Excel, quyền, menu.
- **Chưa kiểm:** bấm thật, 125% DPI. Cần đối chiếu app gốc: thu nợ theo đơn ghi gì vào bill, dấu công nợ NCC, xuất trả hàng (issue #42).
