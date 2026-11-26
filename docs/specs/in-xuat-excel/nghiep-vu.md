# Nghiệp vụ và engine in

Nhãn: **ĐÃ KIỂM** là đã đọc nguồn hoặc chạy thử; **CHƯA KIỂM** là suy đoán hoặc chưa thử.

## Thư viện (ĐÃ KIỂM, thử ngoài app 26/11, PowerShell 32-bit)

- `Kazuko_FnB/lib/FastReport.DLL` bản `1.2.47.0`; mẫu trong CSDL ghi cùng phiên bản tạo. Có các lớp xuất Excel 2007, PDF, CSV, HTML, ảnh.
- **Chỉ có DLL FastReport thì không dựng được mẫu nào.** Bản này tự thêm tham chiếu `No1Lib.Utils.dll`, `No1Lib.Db.dll`, `No1Lib.Sys.dll` khi biên dịch script của mẫu. Các DLL đó ở thư mục app gốc `POS TAP/`.
- Chạy được khi: nạp các DLL `No1Lib*`, `DbMapping.DLL` của app gốc và đổi tham chiếu trong `Report.ReferencedAssemblies` thành đường dẫn đầy đủ.
- Bỏ tham chiếu hoặc xóa dòng `using No1Lib` thì hỏng.

## Dữ liệu cho mẫu (ĐÃ KIỂM bằng dữ liệu giả)

- Mẫu lấy dữ liệu từ bảng tên `Data.Table0`: đăng ký một `DataSet` tên `Data` chứa `DataTable` `Table0`. Tên cột nằm trong mục `Dictionary` của từng mẫu.
- Giá trị đầu và cuối trang truyền bằng tham số theo tên, kể cả tên tiếng Việt có dấu cách (ví dụ `[In bởi]`). Kiểu phải đúng: `[Day([NGAY])]` cần `NGAY` là ngày.
- Dựng được 30/64 mẫu `STEMPLATE` bằng dữ liệu giả; đã thử xuất `.xlsx`, `.pdf` với 4 mẫu (1 mã vạch, 3 báo cáo), ra file khác rỗng, **chưa mở file xem nội dung**.

| Mẫu cần | Kết quả thử |
|---|---|
| Thống kê doanh thu (A4 bán hàng, A4 thanh toán, 80mm, 58mm) | dựng được cả 4 |
| Thống kê mặt hàng bán (A4, 80mm, 54mm) | dựng được cả 3 |
| Mã vạch (6) | dựng được cả 6 |
| "Mẫu A4 nằm ngang", "Mẫu A4 thẳng đứng" | dựng được |
| Báo giá (2) | lỗi: thiếu hàm `ToVndWords` (đọc tiền thành chữ). **CHƯA KIỂM** hàm này nằm ở đâu |
| Mẫu đặt hàng (5) | lỗi trừ `decimal` với chữ; nhiều khả năng do dữ liệu giả sai kiểu. **CHƯA KIỂM** với dữ liệu đúng kiểu |
| Chi tiết hoạt động ngày, Kết quả kinh doanh (`SREPORTTEMPLATE`) | **CHƯA THỬ** |

34 mẫu còn lại không dựng được với dữ liệu giả đều thuộc diện chưa kiểm, không kết luận là mẫu hỏng.

## Cách app gốc gọi in (ĐÃ KIỂM một phần)

- Thống kê doanh thu và Thống kê mặt hàng bán: `btnIn_Click` gọi `Config.PrintInvoice(null, <form>, "", true, true, ...)`, chép lưới vào `DataSet` (`CopyToDataSet`) rồi thêm tham số `TuNgay`, `DenNgay`.
- **CHƯA KIỂM:** ý nghĩa hai cờ `true, true` (hộp chọn mẫu, xem trước hay in thẳng), cách chọn mẫu mặc định và máy in, nguồn các tham số chung (`CompanyName`, `CompanyAddress`, logo).
- Báo cáo trong menu Báo cáo gọi `ShowReport`. **CHƯA KIỂM** với hai báo cáo thuộc phạm vi này.

## Quy tắc bản mới (đề xuất)

- N1. Đọc mẫu thẳng từ CSDL lúc in; không chép mẫu vào mã nguồn; không ghi vào `STEMPLATE`, `SREPORTTEMPLATE`.
- N2. Số trên bản in lấy đúng số màn hình đang hiện (cùng nguồn dữ liệu), không tính lại bằng công thức thứ hai. Công thức tổng nằm trong mẫu thì để mẫu tính, rồi đối chiếu với màn hình.
- N3. Quyền: người không được xem cột nào (ví dụ giá vốn) thì dữ liệu cột đó không được đưa vào `DataSet`, file Excel hay bộ nhớ tạm.
- N4. Mọi truy vấn có tham số hóa, nằm ở tầng Data; dựng mẫu nằm ở một lớp dịch vụ in riêng, View chỉ gọi.
- N5. Mẫu lỗi hoặc thiếu: báo một câu rõ tên mẫu, không văng app.
- N6. **Mẫu in có script C# được biên dịch và chạy khi in.** Ai sửa được CSDL thì chạy được mã trên máy in. App gốc cũng vậy. Bản mới giữ nguyên hành vi, không thêm cách nạp mẫu từ file ngoài.
