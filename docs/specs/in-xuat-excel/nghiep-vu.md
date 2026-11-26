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
| Chi tiết hoạt động ngày (2 mẫu), Kết quả kinh doanh (`SREPORTTEMPLATE`) | dựng được cả 3 bằng dữ liệu giả (26/11, qua `FastReportEngine` của P0); xem mục cuối |

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

## Mẫu `SREPORTTEMPLATE` (ĐÃ KIỂM 26/11 bằng dữ liệu giả có giảm giá)

- **Chi tiết hoạt động ngày:** `Data.Table0`–`Table3` đúng tên cột 4 truy vấn gốc (`THE` đổi tên thành `TIENTHE`); `GIOTHANHTOAN` kiểu ngày giờ, số kiểu `decimal`. Tham số: `GiamGiaTongBill` (decimal), `@FromDate`, `@ToDate` (thử bằng chuỗi `dd/MM/yyyy`), `@USERID` (chữ hiện sau "Thu ngân:"), `CompanyName`, `CompanyAddress`, `CompanyPhone`, `CompanyEmail`. Mẫu không có script.
- Khối tổng hợp do mẫu tự tính: TIỀN MẶT = tiền mặt của bill + thu tiền mặt − chi tiền mặt − tiền đã trả phiếu nhập (thử: 50.000 + 11.000 − 0 − 100.000 = −39.000); CHUYỂN KHOẢN = 40.000 + 0 − 6.000 = 34.000. Cả hai mẫu ra cùng số. Mẫu `765b861a` không có cột Chuyển khoản ở khối Bán hàng.
- **Kết quả kinh doanh:** một `Table0` (10 cột như `query.sql`), 10 dải lọc theo `LOAI`; tham số `@FromDate`, `@ToDate` và thông tin công ty.
- **Bẫy:** script `_StartReport` chỉ tính % khi sự kiện nhận `FastReport.ReportStartEventArgs(DataSet ds, Hashtable hst)`. `Prepare()` thường **không** truyền kiểu này: trang in thiếu toàn bộ cột %, không báo lỗi. Cách đã thử chạy đúng: `Prepare()`, `report.OnStartReport(new ReportStartEventArgs(ds, new Hashtable()))`, rồi `Prepare()` lại. Ca 900.000 / 100.000 / giảm giá 100.000 ra 82% / 9% / 9%. **Chưa biết** app gốc gọi sự kiện theo cách nào.
- Script chỉ ghi % cho một số dòng; "100%" ở DOANH THU và CHI PHÍ phải có sẵn trong dữ liệu như mã gốc (`AddRow`).
- **CHƯA KIỂM:** số so với app gốc; xem trước trong app mới; Excel và PDF của ba mẫu này.

## Đối chiếu app gốc bằng hóa đơn thử có giảm giá (ĐÃ KIỂM 26/11, bản chép)

Dữ liệu: `fnb-agy-work/_tmp/in-p1/seeded.fdb`; ảnh `_tmp/_origin-shots/in-xuat-excel/` số 18–24. HD001: tiền hàng chưa giảm 110.000, giảm trên món 10.000, giảm bill 10.000, còn 90.000. HD002: 80.000, giảm bill 5.000, còn 75.000. Một phiếu nhập 300.000 trả 100.000, thu mặt 11.000, chi chuyển khoản 6.000.

- **Thống kê doanh thu:** màn gốc: tiền hàng 190.000, giảm tiền hàng 25.000, doanh thu 165.000, thực thu 115.000. Hộp "CHỌN MẪU IN" chọn sẵn "Báo cáo tổng hợp thanh toán A4" (`a5182e61-…`).
  - Thanh toán A4: dòng tổng 165.000 / mặt 50.000 / thẻ 20.000 / CK 40.000 / voucher 10.000 / đặt trước 10.000 / thẻ trả trước 7.000 / nợ 25.000. Không có cột trừ tích lũy.
  - Bán hàng A4: cột "Giảm hàng" chỉ in `TIENGIAMGIA` từng dòng (10.000; 5.000) nhưng **dòng tổng là 25.000** vì mẫu cộng thêm `GIAMGIAMATHANG`. Giữ như gốc. Tổng tiền hàng 190.000, tổng cộng 165.000.
  - 80mm, 58mm: hai dòng, tổng 165.000.
  - Bốn mẫu không khai báo tham số; app truyền `TuNgay`, `DenNgay`; không có `Total` trùng tên tham số. `Table0` là lưới chính (tên cột theo `Dictionary` của mẫu).
- **Chi tiết hoạt động ngày:** app gốc dùng mẫu `765b861a` (khối Bán hàng có "Đặt trước", không có "Chuyển khoản"). Mọi số trên trang gốc trùng bản dựng thử (tổng 165.000; mặt hàng 190.000 − 10.000 − 15.000 = 165.000; TIỀN MẶT −39.000; CHUYỂN KHOẢN 34.000; nợ NCC 200.000). **Chiến chốt 26/11:** "Theo app gốc": đọc `SREPORT.LASTTEMPLATEID` (thay Q9 ngày 25/11 "Mẫu đầy đủ").
- **Kết quả kinh doanh:** trang gốc có cột % (ví dụ chi phí 70.000 / doanh thu 165.000 = 42%), có dòng "Tiền giờ", biểu tượng Wingdings. Bản mới vẫn ẩn "Tiền giờ" theo Q7 của spec KQKD.
- **Lọc ngày:** app gốc để "Đến ngày" lúc 0 giờ nên xem 26/11–26/11 không thấy bill ngày 26/11. Bản mới giữ quyết định đã có (trọn ngày cuối).
## Cột ẩn theo cấu hình (ĐÃ KIỂM 26/11, code `100513f`)

- `TableColumn` trong `.frx` có thể mang `VisibleByConfigID` = ID bản ghi `SCONFIG`. Cột chỉ hiện khi `INTVALUE = 30`. ID không có trong `SCONFIG` thì giữ cột hiện (quy tắc của bản mới, chưa kiểm app gốc).
- Doanh thu: bán hàng A4 có `CoTienGio` (Tiền giờ, Giảm giờ), `CoPhiDichVu` (Phí dv), `CoThueSuat` (Thuế); thanh toán A4 có `SuDungDiemTichLuyDeThanhToan` (Trừ tích lũy). Cả bốn đang tắt trên CSDL mẫu, trang app gốc thiếu đúng các cột đó. Trạng thái bật mới kiểm bằng selftest, chưa xem trên app gốc.
- 4 trang Thống kê doanh thu của bản mới đã so bằng mắt với app gốc trên cùng hai hóa đơn: số, cột và dòng tổng khớp. Tiêu đề ngày khác nhau do lọc trọn ngày cuối.

## P2: mẫu Báo giá và Đặt hàng (ĐÃ KIỂM 26/11, dựng thử bằng dữ liệu đúng kiểu)

- **Cả 7 mẫu dựng được** (Báo Giá A4, Báo giá A4 (mẫu 2); Mẫu đặt hàng 54 mm, 54 mm 2 dòng, 80 mm, A4, A5) khi: (1) trước khi dựng đăng ký hàm `No1Lib.Sys.DocSo.ToVndWords(Decimal)` cho FastReport bằng `FastReport.Utils.RegisteredObjects.AddFunction(MethodInfo, "No1")` (mẫu không có script; `[ToVndWords(...)]` nằm trong biểu thức ô chữ); (2) truyền đúng kiểu như dưới. Lỗi "trừ decimal với chữ" cũ là do dữ liệu giả sai kiểu.
- **Mẫu gắn theo bảng, không theo màn:** `STEMPLATE.SFORMID` rỗng; liên kết bằng `STEMPLATE.STABLEDESCID`: `780a9daf-b2b7-417c-8ff3-21700fab6990` (TDATHANG, 5 mẫu STATUS 30) và `b2d3ffd4-9037-4dd1-b255-ec16dd122e66` (TBAOGIA, 2 mẫu STATUS 30; "Bao gia 54mm", "Bao gia 80 mm" STATUS 0, không có nội dung, bỏ qua). Cần hàm đọc mẫu theo `STABLEDESCID` (chỉ STATUS = 30, có TEMPLATE).
- **Dữ liệu:** `Data.Table0` = các dòng chi tiết (cột: ID, NOTE, DMATHANGID, DMATHANG_NAME, DDONVITINH_NAME, DMATHANG_CODE, SOLUONG, DONGIA, THANHTIEN, TILEGIAMGIA decimal; đặt hàng thêm BAOHANH, DDONVITINHID, TDATHANGID, TDATHANG_TUNGAY, TDATHANG_DENNGAY (DateTime), TDATHANG_DBANID, DBAN_NAME; báo giá thêm DMATHANG_DDONVITINHID). **Tham số** (tên theo cột của phiếu, đúng kiểu): NAME, TENKHACH, DIACHI, DIENTHOAI, NOTE (chuỗi); NGAY, TUGIO (DateTime; TUGIO chỉ ở đặt hàng); TIENHANG, TILEGIAMGIA, TIENGIAMGIA, TILETHUE, TIENTHUE, PHIVANCHUYEN, TONGCONG (decimal); đặt hàng thêm DatTruoc (decimal, tiền đặt trước = số tiền phiếu thu đặt cọc của phiếu, 0 nếu chưa có) và DBAN_NAME (chuỗi: tên phòng/bàn); `In bởi` (tên người dùng); CompanyName, CompanyAddress, CompanyPhone, CompanyEmail, CompanyFax. `TUGIO` kiểu DateTime cho ra "26/11/26 18:00" theo định dạng của mẫu.
- **Số trang in = số trên phiếu đã lưu** (đọc lại từ CSDL sau khi lưu, không tính lại): TIENHANG, giảm giá, thuế, phí, TONGCONG lấy từ bản ghi đã lưu; CÒN LẠI = TONGCONG − DatTruoc do mẫu tự tính.
- **Chưa đối chiếu app gốc** (không điều khiển được app gốc phiên 26/11): "Lưu & In" làm gì, "Gửi mail" làm gì, chữ "đồng chẵn" bị in hai lần ở Báo Giá A4 (hàm đã thêm "đồng chẵn", mẫu thêm lần nữa; app gốc nhiều khả năng cũng vậy vì cùng hàm).
- **Quyết định tạm (Chiến "làm theo đề xuất" 26/11, chờ Chiến thử app gốc để sửa nếu lệch):** "Lưu & In" = lưu (cùng kiểm tra như "Lưu") rồi in thẳng ra máy in mặc định bằng mẫu chọn sẵn (Mẫu đặt hàng A4 / Báo Giá A4), không hiện hộp; "Lưu & Xem in" = lưu rồi mở hộp "IN BÁO CÁO". Hộp này ẩn "Gửi mail" và "Thiết kế mẫu in" (Q8). Không đổi chữ "đồng chẵn" của mẫu.

## P3a: xuất Excel của lưới (quyết định 26/11, Chiến "tiếp tục" P3; chưa đối chiếu thêm app gốc)

- **Đã kiểm trên app gốc (Danh mục mặt hàng):** "Xuất excel" mở thẳng hộp Lưu file loại `.xls`, tên trống; xong hỏi "Bạn có muốn mở file đã xuất không?"; file có đúng các cột đang hiện, đúng thứ tự, dòng đầu là tiêu đề cột, ô số là kiểu số, ô tích `TRUE/FALSE`.
- **Quyết định (Q4 đề xuất, Chiến duyệt):** xuất đúng các dòng đang hiện trên lưới (đang lọc ra 3 dòng thì file có 3 dòng), theo thứ tự đang sắp. Cột ẩn hay bị chặn quyền (N3) không có trong file. Dùng `Aspose.Cells` có sẵn trong `lib/`, ghi `.xls`.
- **Chưa biết / để sau:** "xuất định lượng" ở hộp sửa mặt hàng (chưa xem app gốc, giữ "chưa hỗ trợ"); "Thêm nhanh", "Cập nhật nhanh", "Nhập Excel" thuộc P5.
- **Mẫu "In lưới" (P3b, chưa làm):** 3 mẫu `STEMPLATE` không gắn bảng nào ("Mẫu 80", "Mẫu A4 nằm ngang", "Mẫu A4 thẳng đứng") **không có băng dữ liệu**, chỉ có tiêu đề (`[CompanyName]`, `[TIÊU ĐỀ BÁO CÁO]`, `[Tham số báo cáo]`) và chữ ký ("Trưởng phòng", "Người lập"); app gốc thêm bảng theo các cột đã tích lúc chạy. Bản mới phải tự dựng bảng đó; cách app gốc xếp độ rộng cột chưa biết.
- **Mã vạch (P4):** 6 mẫu `STEMPLATE` có `BARCODE = 30`, `STABLEDESCID = a4a16216-aee9-4dd3-9f92-c1e4d85c06a2`, mỗi mẫu một hàng là N tem; cột `NAME0..`, `CODE0..`, `GIABAN0..` (số cuối là vị trí tem trong hàng; 2, 3 hoặc 5 tem).
