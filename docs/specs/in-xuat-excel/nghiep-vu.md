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
| Báo giá (2) | lỗi: script không thấy `ToVndWords`. **ĐÃ KIỂM** (đọc chữ ký DLL): hàm là `No1Lib.Sys.DocSo.ToVndWords(Decimal)`; **chưa thử** cách cho script của mẫu gọi được |
| Mẫu đặt hàng (5) | lỗi trừ `decimal` với chữ; nhiều khả năng do dữ liệu giả sai kiểu. **CHƯA KIỂM** với dữ liệu đúng kiểu |
| Chi tiết hoạt động ngày, Kết quả kinh doanh (`SREPORTTEMPLATE`) | **CHƯA THỬ** |

34 mẫu còn lại không dựng được với dữ liệu giả đều thuộc diện chưa kiểm, không kết luận là mẫu hỏng.

## Cách app gốc gọi in (ĐÃ KIỂM một phần)

- Thống kê doanh thu và Thống kê mặt hàng bán: `btnIn_Click` gọi `Config.PrintInvoice(null, <form>, "", true, true, ...)`, chép lưới vào `DataSet` (`CopyToDataSet`) rồi thêm tham số `TuNgay`, `DenNgay`.
- **ĐÃ KIỂM** (đọc tên tham số trong `No1Lib.Sys.DLL`): lời gọi trên ứng với `PrintInvoice(row, SFORMID, RecordID, showSelectTemplate, showPreview, OnCustomReport)`, tức hai cờ là hiện hộp chọn mẫu và xem trước. Bấm thử khớp: app hiện hộp "CHỌN MẪU IN", không in thẳng.
- Cùng DLL có `Config.ShowReport(title, ID, dicParams)`, `UiUtils.ExportToExcel(DataGridView)` và các `UiUtils.PrintBarCode(...)` (nhận mẫu, danh sách mã và số lượng). **Chưa đọc** thân hàm, chưa biết màn nào gọi hàm nào để xuất lưới.
- **CHƯA KIỂM:** cách chọn mẫu mặc định và máy in, nguồn các tham số chung (`CompanyName`, `CompanyAddress`, logo; trên CSDL mẫu chân trang hiện chữ "(TÊN CÔNG TY)").
- Báo cáo trong menu Báo cáo gọi `ShowReport`. **CHƯA KIỂM** với hai báo cáo thuộc phạm vi này.

## Quy tắc bản mới (đề xuất)

- N1. Đọc mẫu thẳng từ CSDL lúc in; không chép mẫu vào mã nguồn; không ghi vào `STEMPLATE`, `SREPORTTEMPLATE`.
- N2. Số trên bản in lấy đúng số màn hình đang hiện (cùng nguồn dữ liệu), không tính lại bằng công thức thứ hai. Công thức tổng nằm trong mẫu thì để mẫu tính, rồi đối chiếu với màn hình.
- N3. Quyền: người không được xem cột nào (ví dụ giá vốn) thì dữ liệu cột đó không được đưa vào `DataSet`, file Excel hay bộ nhớ tạm.
- N4. Mọi truy vấn có tham số hóa, nằm ở tầng Data; dựng mẫu nằm ở một lớp dịch vụ in riêng, View chỉ gọi.
- N5. Mẫu lỗi hoặc thiếu: báo một câu rõ tên mẫu, không văng app.
- N6. **Mẫu in có script C# được biên dịch và chạy khi in.** Ai sửa được CSDL thì chạy được mã trên máy in. App gốc cũng vậy. Bản mới giữ nguyên hành vi, không thêm cách nạp mẫu từ file ngoài.

## Ô tổng của mẫu trùng tên tham số (ĐÃ KIỂM 26/11)

- Mẫu A4 của Thống kê mặt hàng bán có `<Total>` tên `TongTienBan`, `TongTienNhap`, `TongLai`, `TongGiamGia`, `ThucLai`, trùng 5 tham số app truyền.
- Truyền `TongGiamGia` hoặc `TongLai` mà không gỡ: `NullReferenceException`. Không truyền: trang in tổng mẫu tự cộng, `ThucLai` cộng dồn sai (lãi 1.000 + 2.000, giảm 100 + 200: in 3.600 thay vì 2.700).
- **App gốc in đúng số trên màn** (hóa đơn thử trên bản chép: 180.000 / 68.000 / 112.000 / −14.000 / 108.000).
- Bản mới: trước khi truyền tham số, gỡ `Total` trùng tên khỏi `report.Dictionary.Totals` (chỉ trong bộ nhớ). `Total` không trùng giữ nguyên. Áp cho mọi mẫu của các đợt sau; mỗi mẫu mới vẫn phải so số với màn.
