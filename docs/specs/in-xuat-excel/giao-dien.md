# Giao diện

Các mục "App gốc" đã bấm thử ngày 26/11 trên bản chép ngoài repo, chỉ xem trên màn hình; ảnh ở `_tmp/_origin-shots/in-xuat-excel/` (số trong ngoặc). CSDL mẫu gần như không có dữ liệu. Bản mới làm như app gốc, chỗ khác phải được Chiến chốt.

Mọi cửa sổ xem trước là trình xem của FastReport: Print, mở, Save, gửi mail, tìm, chuyển trang, thu phóng, Close. Menu Save: Prepared report, Adobe Acrobat, Rich Text, HTML, XML (Excel), Excel 2007, PowerPoint 2007, OpenOffice Calc, CSV, Text/Matrix Printer, Image (3).

## A1. Thống kê doanh thu, Thống kê mặt hàng bán (App gốc, 1, 2, 4)

- Nút "In báo cáo" mở hộp **"CHỌN MẪU IN"**: danh sách mẫu (Doanh thu: 58mm, 80mm, tổng hợp bán hàng A4, tổng hợp thanh toán A4; Mặt hàng bán: 54mm, 80mm, A4), "Tùy chọn" chọn một: **Xem trên màn hình** (chọn sẵn), In ra máy in (kèm ô chọn máy), Xuất excel, Xuất pdf, Thiết kế mẫu in; nút OK, Cancel.
- Xem trước mở cửa sổ **"IN PHIẾU"** (đã xem ở Doanh thu). Không có dữ liệu vẫn mở.
- Mỗi màn có một mẫu được chọn sẵn (thanh toán A4; A4). **Chưa kiểm** app chọn theo gì.

## A2. Chi tiết hoạt động ngày, Kết quả kinh doanh (App gốc, 5, 6)

- **Cả màn là trình xem FastReport nhúng trong tab**: trên là bộ lọc (Từ ngày, Đến ngày, Thu ngân ở màn chi tiết, Cửa hàng, "Tải dữ liệu (F5)"), dưới là bản báo cáo cùng thanh công cụ. **Không có nút "In báo cáo", không có hộp chọn mẫu.**
- **Chiến chốt 26/11 (Q9): làm y hệt app gốc.** Phần lưới tự làm và nút "In báo cáo" hiện nay của hai màn này sẽ được thay; chỉ thay sau khi mẫu dựng được và số trên trang in khớp số đang hiện (thứ tự làm, đề xuất của Claude).

## B. Phiếu đặt hàng, Báo giá (App gốc, 9–11, 16)

- Hộp sửa có hai nút ở góc dưới trái: **"Lưu & In"** và **"Lưu & Xem in"**. **Bản mới chưa có hai nút này.** Phải lưu được phiếu trước (thiếu phòng hay trường bắt buộc thì báo như khi Lưu).
- "Lưu & Xem in" mở hộp **"IN BÁO CÁO"** (khác hộp ở A1): ô thả "Mẫu in" (đặt hàng: 54mm, 54mm 2 dòng, 80mm, A4, A5, chọn sẵn A4; báo giá chọn sẵn "Báo Giá A4"), tùy chọn Xem trên màn hình (chọn sẵn), In ra máy in, Xuất excel, Xuất pdf, **Gửi mail**, Thiết kế mẫu in; nút "Thực hiện", "Thoát".
- Xem trước phiếu đặt hàng có dòng đọc tiền thành chữ.
- **Chưa bấm** "Lưu & In" (nhiều khả năng in thẳng).

## C. Lưới danh sách (App gốc, 7, 8, 12, 13; thử ở Danh mục mặt hàng)

- **"Xuất excel":** mở thẳng hộp Lưu file, loại `.xls`, tên để trống; xong hỏi "Bạn có muốn mở file đã xuất không?". File thử có **11 cột đúng các cột đang hiện trên lưới, cùng thứ tự**; ô số là kiểu số, ô tích là `TRUE/FALSE`, dòng đầu là tiêu đề cột. **Chưa thử** khi đang lọc (CSDL chỉ có 1 mặt hàng).
- **"In"** (và nhiều khả năng cả "In danh sách" ở menu chuột phải, chưa bấm): hộp **"In lưới" / "Cấu hình trang in"**: Tiêu đề (sẵn "Mặt hàng"), Ghi chú, Mẫu in (Mẫu 80, A4 nằm ngang, A4 thẳng đứng), danh sách "Cột hiển thị" có ô tích, "In cột số thứ tự"; nút "Hiển thị", "Thoát". "Hiển thị" mở xem trước **"In danh sách"**: đầu trang công ty, tiêu đề, bảng đúng các cột đã tích, chữ ký "Trưởng phòng", "Người lập".
- Menu chuột phải còn có: Thêm nhanh (excel), Cập nhật nhanh (excel), Sao chép ô, Sao chép vùng chọn, Cột hiển thị.

## D. Mã vạch (App gốc, 14, 15)

1. Chọn dòng mặt hàng, bấm "In mã vạch": mở **khung bên phải "IN MÃ VẠCH"** gồm Thêm, Xóa, Xuất excel, In, Bartender; lưới Mã, Tên, Số lượng; "Tổng sl".
2. "Thêm" đưa mặt hàng đang chọn vào khung, số lượng 1. Lần bấm "In mã vạch" đầu khi chưa chọn dòng không thấy gì xảy ra.
3. "In" trong khung mở hộp **"MẪU IN MÃ VẠCH"**: 6 mẫu, Thêm mới, Chỉnh sửa, Xóa mẫu, Thiết kế, Chấp nhận, Thoát.
4. "Chấp nhận" mở xem trước, không in thẳng. Mẫu "2 tem, CODE128" xếp hai tem một hàng; mặt hàng thử không có mã nên **chưa thấy vạch**.

"Bartender" gọi phần mềm ngoài: **chưa xem**.

## Đề xuất cho bản mới

- Đang dựng mẫu thì khóa nút, tránh bấm hai lần.
- Phần khung của app dùng token màu `ThemeManager`; thanh công cụ FastReport giữ nguyên.
- Nút đủ lớn để bấm bằng tay; không thêm thanh cuộn dọc vào dialog hiện có.
- Cột bị chặn quyền không có trong file hay bản in (quy tắc N3).

## Chỗ không đổi

Menu Báo giá vẫn ẩn như app gốc (đã bật tạm **chỉ trên bản chép** để xem).
