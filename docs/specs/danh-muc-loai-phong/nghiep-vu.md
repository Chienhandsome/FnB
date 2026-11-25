# Nghiệp vụ

## 1. Trường (`DLOAIPHONG`)

| Cột | Nhãn | Kiểu | Ràng buộc |
|---|---|---|---|
| `NAME` | Tên loại phòng | VARCHAR(255), collation `UNICODE_CI_AI` | bắt buộc, không trùng (`SCOLUMN` và `mapper`) |
| `SIMAGEID` | Ảnh | tham chiếu thư viện ảnh | không |
| `MAUCHU` | Màu chữ | INTEGER | không |
| `MAUNEN` | Màu nền | INTEGER | không |
| `NOTE` | Ghi chú | VARCHAR(255) | không |

Cột khung (`SORTORDER`, `PARENTID`, `PARENTDIR`, `ITEMTYPE`, `AUTOID`) có trong `mapper`, không có ô nhập. `STABLEDESC.TABLETYPE = 0`, `READONLY = 0`, `INVOICEPRINT = 0`. Dòng mới: `ID` GUID, `STATUS = 30`, cột người và giờ tạo/sửa. Không thấy trigger trên bảng. Bản chép CSDL: 0 loại phòng, 0 bàn đang gắn loại.

## 2. Màu (`code.cs`)

- Thêm mới (`mapper.ID` trống): nền trắng, chữ đen (`mapper_AfterFillData`).
- Ô "Hiển thị" cho xem trước: chữ là tên đang gõ (`txtNAME_TextChanged`), nền và màu chữ theo hai ô màu (`UpdateSample`).
- Màu là số nguyên ARGB; bản mới đang đọc theo cách này ở `BookingCalendarRepository` và `BookingCalendarView.ColorFromArgbOr`.
- Hai màu này là **dữ liệu người dùng chọn**, được phép dùng để vẽ ô xem trước; phần còn lại của màn lấy màu từ design token.

## 3. Quyền

`SMENU.SFUNCTIONID` và `STABLEDESC.SFUNCTIONID` của mục này đều **trống**, và `SFUNCTION` không có chức năng "Danh mục loại phòng". Chưa có bằng chứng khung xử lý trường hợp trống thế nào (Q2).

## 4. Xóa

Quy ước dự án: xóa cứng. Cột tham chiếu: `DBAN.DLOAIPHONGID`, `TSUACHUA.DLOAIPHONGID`. Form gốc không có mã xử lý xóa. Luật khi loại đang được tham chiếu chưa xác định (Q1). Lưu ý: màn Theo dõi đặt phòng bản mới chỉ hiện phòng có loại còn `STATUS = 30`, nên xóa loại đang gắn sẽ làm phòng biến khỏi lịch.
