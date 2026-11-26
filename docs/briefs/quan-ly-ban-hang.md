# Brief: Quản lý bán hàng + Điều chỉnh hóa đơn

- Spec: `docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md` (đã duyệt; §6.4 thắng §6.3).
- **Đã code, push `92f92de`** (#38): hai màn dùng chung `SalesManageView`: danh sách bill đã thanh toán, sửa dòng/giảm giá/phí/thuế trong bộ nhớ rồi "Cập nhật" trong MỘT transaction (dùng lại `PaymentDialog`), trả đồ (ghi dòng SL âm), đổi khách + áp giảm giá nhóm khách (Q4), hủy phiếu (chỉ Quản lý bán hàng; ghi `TDONHANGHUY`, giữ nguyên phiếu thu chi và cảnh báo), cảnh báo cọc dư đã chi (§6.4), bill thanh toán chung bị chặn tuyệt đối, chặn âm kho mặc định, cảnh báo khi đóng tab đang sửa dở.
- **Chưa kiểm:** bấm thật; sửa bill rồi xem Tồn quỹ, kho, công nợ, điểm; trả đồ; hủy phiếu có phiếu thu liên quan; 125% DPI.
- **Việc kế (Chiến chốt 27/11):** hủy bill có phiếu thu cọc đặt trước thì CHẶN (Sử dụng dịch vụ và Quản lý bán hàng).
