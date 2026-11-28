# Chốt 28/11 (đợt B): Tạm ứng, Tồn đầu, Bảng lương

- Trạng thái: **ĐÃ DUYỆT** (Chiến trả lời trong chat 28/11/2026, xem `chien-chot-28-11.md` mục "Chiến trả lời tiếp" các số 4, 5, 6)
- Cơ sở: bấm thử app gốc và đọc mã `POS_TAP_SRC_EXPORT` ngày 28/11. Chỉ làm đúng ba mục dưới đây, không đụng chỗ khác.

## A. Tạm ứng lương: lọc Từ/Đến mặc định để trống
- Màn `SalaryAdvanceListView`: hai ô ngày Từ / Đến hiện đang mặc định đầu tháng và cuối tháng. Đổi thành **trống** khi mở (ô tích bên trái ô ngày, bỏ tích = không lọc), như gốc.
- Ô bỏ tích: không lọc phía đó. Tích một ô: lọc từ ngày đó (hoặc đến ngày đó). Đổi tích hoặc ngày thì nạp lại lưới.
- Danh sách mới mở hiện **toàn bộ** phiếu tạm ứng (STATUS = 30), thứ tự cũ.
- Quy tắc lọc theo ngày giữ nguyên (so theo ngày, gồm cả ngày Đến).
- Selftest: mở màn không lọc thấy đủ phiếu; tích Từ lọc đúng; tích Đến lọc đúng; tích cả hai lọc đúng.

## B. Tồn kho ban đầu: cột "Mã sẵn có" và ô "Tổng giá trị"
- Lưới (`OpeningStockView`): thêm cột **"Mã sẵn có"** (cột `DMATHANG.MASANCO`, chỉ đọc) ngay sau cột "Mã hàng". Lấy dữ liệu trong cùng truy vấn nạp lưới.
- Ô tìm theo mã: so thêm với `MASANCO` ngoài `CODE` (gốc: `CODE = x OR CODE2 = x`).
- Thêm hộp **"Tổng giá trị"** (ô số chỉ đọc, có nhãn) ở thanh thông tin dưới lưới, cạnh dòng "N mặt hàng có tồn — Tổng giá trị". Giá trị = tổng Thành tiền các dòng có tồn, đồng bộ với hàng "Tổng cộng". Cập nhật khi sửa Tồn đầu hoặc Giá vốn.
- Cột nhỏ gọn, không làm xuất hiện cuộn ngang ở 1366x768.
- Selftest: cột có mặt và đúng giá trị `MASANCO`; tìm theo mã sẵn có ra đúng dòng; ô Tổng giá trị bằng tổng Thành tiền và đổi theo khi sửa số lượng.

## C. Bảng lương (hộp tạo mới): Số phiếu sửa được, Lưu & Mới, Lưu & In
- Hộp `PayrollCreateDialog`: ô **Số phiếu** (hiện là tên tự sinh "Tháng M/yyyy", không sửa) cho **sửa tay**; mặc định tự điền "Tháng M/yyyy" khi đổi Tháng/Năm nếu người dùng chưa sửa tay. Số phiếu không được trống, tối đa 255 ký tự.
- Giữ nguyên chặn trùng (tháng, năm) của bảng lương và các chặn đã có.
- Thêm nút **"Lưu & Mới"**: lưu bảng lương, rồi giữ hộp mở với dữ liệu trống: Tháng/Năm về mặc định hiện tại, Số phiếu tự điền lại, Ghi chú xóa; bảng vừa lưu hiện ở danh sách bên trái khi đóng hộp.
- Thêm nút **"Lưu & In"**: lưu rồi mở màn in/xem bảng lương hiện có (`PayrollPreviewForm`) cho bảng vừa tạo, rồi đóng hộp. Nếu màn in cần Tính lương xong mới in được thì in bảng hiện có (có thể rỗng) theo đúng đường mà nút In hiện tại của màn Chấm công/Bảng lương đã dùng; không viết thêm báo cáo mới.
- Thoát khi đã nhập dữ liệu nhưng chưa lưu: hỏi "Dữ liệu của bạn đã thay đổi, bạn có muốn hủy bỏ các thay đổi không?" (Có/Không); chưa nhập gì thì đóng thẳng.
- Selftest: sửa Số phiếu rồi lưu thì DB lưu đúng tên đã sửa; Lưu & Mới tạo đúng một bảng và hộp trở về trống; Lưu & In tạo bảng và mở màn in; thoát khi có thay đổi hỏi, thoát khi chưa đổi không hỏi.

## Ngoài phạm vi
Không đổi tính lương, chi lương, schema, menu, quyền. Không thêm màn mới ngoài hộp thoại đã nêu.
