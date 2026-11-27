# Brief: Nhân sự P1 (Danh mục nhân viên, Danh mục ca làm việc)
- Spec: `docs/specs/nhan-su-danh-muc.md` (Claude tự duyệt 27/11).
- **28/11 đã code, push `708952b`** (#59): `Employee*`, `Shift*`: hai màn CatalogView phẳng; nhân viên có Cách tính lương (30/0/60) khóa mở ô lương, Nghỉ T7/CN, Mã và Ca chỉ khi `TinhLuongSuDungMayChamCong = 30`; ca có Từ/Đến giờ (ca qua đêm), Tỉ lệ lương; trùng Tên, chặn xóa khi còn tham chiếu, lưu vết, quyền theo tên chức năng, xuất Excel.
- **Chưa kiểm:** bấm thật, 125% DPI, ảnh app gốc (bố cục lưới là mặc định). Chưa có màn bật tắt cấu hình máy chấm công. Tiếp theo: Thưởng phạt, Chấm công, Tính lương, Tạm ứng lương.
