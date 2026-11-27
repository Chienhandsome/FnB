# Brief: Nhân sự P3 (Bảng lương, Tính lương, Chấm công, Chi lương)
- Spec: `docs/specs/nhan-su-tinh-luong.md` (Claude tự duyệt 28/11). Chia P3a (đã xong) và P3b (Chi lương, chưa làm).
- **28/11 P3a đã code, push `7d7653e`** (#61): `Payroll*`: view `PayrollView` hai chế độ (menu Tính lương, Chấm công), lưới ca × ngày, `PayrollCalculator` thuần (theo ca, theo tháng, tháng theo ca), lưu một transaction, chế độ Chấm công chỉ ghi `TBANGLUONGCHITIET`, xóa bảng lương xóa cả ba bảng, Tải lại có xác nhận.
- **Chưa kiểm:** bấm thật, 125% DPI, ảnh app gốc. Điểm TẠM T1–T11 trong spec (nhất là T4 quyền Chấm công, T6 công thức tạm ứng chỉ `CHI`, T8 xóa dòng khi ô về trống).
- **Còn:** P3b Chi lương (nút ở màn Tính lương, phiếu chi có `TBANGLUONGID`).
