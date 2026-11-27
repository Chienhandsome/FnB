# Brief: Danh mục đợt khuyến mại và áp dụng khi bán
- Spec: `docs/specs/danh-muc-khuyen-mai.md` (Claude tự duyệt 27/11; Chiến chốt: Mua x tặng y theo bán lẻ, chặn đợt trùng, cho chạy 2 loại gốc không chạy).
- **P1 đã code, push `902060b`** (#52): màn CatalogView `Promotion*`, hộp thêm/sửa theo 9 loại (nhận diện loại theo từ khóa trong tên), chặn lưu đợt trùng (cùng loại, khoảng ngày giao nhau, bỏ qua đợt ngừng), xóa đầu đợt và chi tiết một transaction, trạng thái hiển thị, lọc, tìm, xuất Excel, quyền, menu.
- **P2 (áp dụng khi bán ở Sử dụng dịch vụ) chưa làm.**
- **Chưa kiểm:** bấm thật, 125% DPI; tên loại trong DB thật có khớp từ khóa không.
