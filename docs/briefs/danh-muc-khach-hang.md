# Brief: Danh mục khách hàng

- Spec: `docs/specs/danh-muc-khach-hang.md` (đã duyệt 25/11).
- **Chưa có code, chưa tạo worktree. Bị chặn: issue #17** (cho-chien).
- Chặn vì: Q10 (tài khoản chỉ có quyền Xem có mở được hộp thoại không) và xóa nhóm còn nhóm con. Hỏi kèm: cách so trùng (Q4), số mã kế tiếp (Q3).
- Đã kiểm 25/11 trên bản chép CSDL: 7 bảng có `DKHACHHANGID`, khớp §4.4 (`TDONHANGHUY` chỉ có cột `KHACHHANG`, chưa xem chứa gì); `MAKHACH` mặc định `{KH(*****)}`, `DEMO.FDB` có `001`, `002`, `KH00001`; `DNHOMKHACHHANG` có `PARENTID`.
- Đã thử trên app gốc 25/11 (comment ở #17): so trùng không phân biệt hoa thường và dấu, không cắt khoảng trắng; mã kế tiếp = số `KH` lớn nhất + 1; xóa nhóm cha thì cả con vào thùng rác. **Chưa thử:** tài khoản chỉ có quyền Xem. Ảnh: `_tmp/_origin-shots/danh-muc-khach-hang/`.
- Còn: chờ trả lời, ghi vào spec, rồi làm `workflow-agy.md` §4. Q7, Q8, Q12 là phần hiển thị.

- 26/11: Chiến chốt đủ (spec §9.0.2, #17). **Hết chặn**, chưa code.
- **26/11 đã code, push `018ba78`** (#33): Danh mục khách hàng và nhóm khách hàng trên khung `CatalogView` (cây nhóm, lưới, hộp Thêm/Sửa/Xóa khách và nhóm, kiểm trùng Mã/Tên theo §9.0.2, xóa chỉ khi 7 bảng tham chiếu `DKHACHHANGID` không còn dòng nào, xuất Excel qua `GridExcelExporter`). agy 2 vòng (vòng 1 dừng vì gọi Bash), Claude sửa 1 ca test. In, Thêm nhanh/Cập nhật nhanh/nhập Excel: "chưa hỗ trợ".
- **Chưa kiểm:** bấm thật, 125% DPI, Ctrl+K, xóa nhóm có nhóm con (agy chọn chặn, Chiến đã chốt chặn, agy làm đúng), điểm tích lũy, thẻ trả trước (chưa có màn).
