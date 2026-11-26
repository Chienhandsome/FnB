# Giao diện

Mục "In" theo app gốc đã chụp 26/11 (Thống kê doanh thu, bản chép ngoài repo; ảnh ở `_tmp/_origin-shots/in-xuat-excel/`). Các mục còn lại là **đề xuất**.

## In (nhóm A, B)

1. Bấm "In báo cáo": hiện hộp **"CHỌN MẪU IN"**: danh sách mẫu của màn, tên như trong CSDL (ở đây: 58mm, 80mm, Báo cáo tổng hợp bán hàng A4, Báo cáo tổng hợp thanh toán A4); dưới là "Tùy chọn" gồm 5 nút chọn một: **Xem trên màn hình** (chọn sẵn), **In ra máy in** (kèm ô chọn máy in), **Xuất excel**, **Xuất pdf**, **Thiết kế mẫu in**; nút OK, Cancel.
   - Lúc mở, mẫu "thanh toán A4" được chọn sẵn và có biểu tượng khác ba mẫu kia. **Chưa kiểm** app chọn mẫu mặc định theo gì, có nhớ lần chọn trước không.
   - "Thiết kế mẫu in" ngoài phạm vi spec này (câu Q8).
2. "Xem trên màn hình": cửa sổ **"IN PHIẾU"** là xem trước của FastReport: Print, mở, Save, gửi mail, tìm, chuyển trang, thu phóng, Close. Menu Save có: Prepared report, Adobe Acrobat, Rich Text, HTML, XML (Excel), Excel 2007, PowerPoint 2007, OpenOffice Calc, CSV, Text/Matrix Printer, Image.
3. Không có dữ liệu: app gốc **vẫn mở xem trước** (tiêu đề, khoảng ngày, tiêu đề cột, dòng TỔNG CỘNG trống).
4. Đang dựng mẫu thì nút In bị khóa, tránh bấm hai lần.
5. Cửa sổ dùng token màu của `ThemeManager` cho phần khung của app; thanh công cụ của FastReport giữ nguyên.
6. Nút đủ lớn để bấm bằng tay; không thêm thanh cuộn dọc vào dialog hiện có.

## Xuất Excel của lưới (nhóm C)

- Xuất đúng các cột đang hiện, đúng thứ tự, đúng bộ lọc và sắp xếp đang áp.
- Ô số xuất ra kiểu số (không phải chữ), ngày ra kiểu ngày.
- Chọn nơi lưu bằng hộp Lưu file; xong hỏi mở file.
- Cột bị chặn quyền không có trong file (quy tắc N3).

## Mã vạch (nhóm D)

- Chọn mặt hàng, số tem mỗi mặt hàng, mẫu giấy; xem trước rồi in.
- Mẫu có hai mặt hàng một hàng (`NAME0`, `NAME1`…): cách chia dòng phải theo app gốc (**chưa kiểm**).

## Chỗ không đổi

Menu Báo giá vẫn ẩn như app gốc. Bố cục các màn hiện có không đổi; chỉ thay câu "chưa hỗ trợ" bằng chức năng.
