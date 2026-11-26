# Giao diện

Mọi điểm ở đây là **đề xuất**; luồng của app gốc chưa được chụp (câu Q3).

## In (nhóm A, B)

1. Bấm In: nếu màn có từ 2 mẫu thì hiện hộp chọn mẫu (tên mẫu như trong CSDL), nhớ mẫu chọn lần trước trên máy đó.
2. Hiện cửa sổ xem trước của FastReport, có: In (chọn máy in), Xuất Excel, Xuất PDF, thu phóng, chuyển trang.
3. Không có dữ liệu: vẫn mở được bản xem trước như app gốc (**chưa kiểm** hành vi app gốc).
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
