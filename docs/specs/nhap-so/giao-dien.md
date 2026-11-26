# Giao diện

Bố cục, nhãn, thứ tự ô không đổi. Rà bằng tìm `NumericUpDown`, `CellParsing`, `CellEndEdit`, `Parse` trong `Kazuko_FnB.UI` ngày 26/11.

## Ô số ngoài lưới (`NumericUpDown`)

| Màn / file | Ô |
|---|---|
| Mặt hàng (`ProductEditDialog`) | 4 giá bán, quy đổi, giá nhập, giá vốn, tồn tối thiểu |
| Bàn (`RoomEditDialog`), Khu vực (`AreaEditDialog`) | đơn giá, tiền mở bàn |
| Thêm nhanh bàn (`QuickAddRoomDialog`) | số chữ số (số nguyên) |
| Khách đặt hàng (`BookingEditDialog`) | số lượng cạnh nút Thêm, tiền hàng, % và tiền giảm, % và tiền thuế, phí vận chuyển, tổng cộng |
| Nhập số lượng của Đặt hàng (`QuantityDialog`) | lẻ, chẵn |
| Báo giá (`QuoteEditDialog*`) | như Khách đặt hàng |
| Nhập số lượng của Báo giá (`QuoteQuantityDialog`) | lẻ, chẵn, % giảm |
| Cấu hình (`ConfigDialog`) | các cấu hình kiểu số |
| Phiếu thu và form dựng từ layout (`DynamicForm`, `PhieuThuDialog`) | mọi ô số |

## Cột sửa được trong lưới

| Lưới | Cột | Hiện đọc thế nào |
|---|---|---|
| Chi tiết Khách đặt hàng | SL, Đơn giá, % giảm | đã theo quy tắc mới |
| Chi tiết Báo giá | SL, Đơn giá, % giảm | CellEndEdit, kiểu cũ |
| Định lượng của mặt hàng | Số lượng | thử vi-VN trước: `0.125` ra 125 |
| Món mặc định của khu vực | Số lượng | InvariantCulture: `2,5` ra 25 |

Lúc implement phải rà lại; thấy thêm chỗ gõ số thì ghi vào báo cáo.

## Cách ô cư xử

1. Bình thường: số kiểu Việt có ngăn nghìn.
2. Vào ô (chuột, chạm, Tab, phím): hiện số không ngăn nghìn, đủ phần lẻ.
3. Rời ô, Enter, hoặc bấm nút dùng giá trị: đọc theo N1, rồi hiện lại như bước 1. Nút phải nhận đúng số vừa gõ, kể cả khi chưa rời ô.
4. Vào rồi ra, không đổi: giá trị không đổi, không đánh dấu phiếu đã sửa, không tính lại giảm giá theo tiền hay theo %.
5. Nút mũi tên lên xuống giữ bước nhảy hiện tại.
6. Gõ phím: ô cho gõ chữ số, `.`, `,`, `-` trên mọi cài đặt Windows. Cột sửa trong lưới chặn chữ số lẻ vượt mức (N4).
7. Không hộp lỗi mặc định của Windows ở bất kỳ ô nào.

Màu và cỡ chữ lấy theo `ThemeManager` như ô hiện tại.
