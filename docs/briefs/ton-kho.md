# Brief: Tồn kho
- Spec: `docs/specs/ton-kho.md` (Claude tự duyệt 27/11).
- **27/11 đã code, push `476c3ac`** (#45): màn xem tồn theo kho (`InventoryView`), lọc kho/loại/nhóm/tìm, tồn 2 ĐVT, tô màu, ẩn cột giá khi không có quyền xem giá nhập, xuất Excel, menu. SQL tồn gộp cùng điều kiện với `GetStockBalanceIn` (STATUS = 30, LOAI <> 0 hoặc bán đã thanh toán).
- **Chưa kiểm:** bấm thật, 125% DPI. Tồn theo ngày và nhiều kho cạnh nhau chưa làm.
