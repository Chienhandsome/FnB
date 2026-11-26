# Nghiệm thu, kế hoạch, rủi ro, câu hỏi

## Tiêu chí

- [ ] T1. Selftest dựng được từng mẫu thuộc nhóm đang làm trên bản chép CSDL, với dữ liệu mẫu tự tạo; số trang lớn hơn 0.
- [ ] T2. Với bộ dữ liệu cố định, đọc lại file Excel xuất ra: từng dòng và dòng tổng khớp số trên màn hình.
- [ ] T3. Với cùng CSDL, bản in của app mới khớp bản in của app gốc (đối chiếu bằng ảnh hoặc PDF, mỗi mẫu một lần).
- [ ] T4. Người không có quyền xem giá vốn: bản in và file Excel không chứa giá vốn.
- [ ] T5. Sau khi in và xuất, `STEMPLATE` và `SREPORTTEMPLATE` không đổi.
- [ ] T6. Mẫu hỏng hoặc thiếu: có câu báo, app không văng.
- [ ] T7. Chữ tiếng Việt hiện đúng trên xem trước, PDF và Excel.
- [ ] T8. In thật ra giấy A4 và máy in nhiệt 80mm: Chiến bấm thử.

## Chia patch (mỗi patch dùng được độc lập)

| Patch | Nội dung | Phụ thuộc |
|---|---|---|
| P0 | Thêm thư viện, lớp dịch vụ in, cửa sổ xem trước, áp cho **Thống kê mặt hàng bán** (3 mẫu đều dựng được) | Q1 |
| P1 | In Thống kê doanh thu; đổi Chi tiết hoạt động ngày và Kết quả kinh doanh thành trình xem nhúng (Q9) | P0; mẫu `SREPORTTEMPLATE` chưa dựng thử |
| P2 | In Báo giá, Khách đặt hàng: thêm nút "Lưu & In", "Lưu & Xem in" và hộp "IN BÁO CÁO" | P0, cho script gọi được `ToVndWords` |
| P3 | Xuất excel của lưới; "In" qua hộp "Cấu hình trang in" (3 mẫu) | Q4 |
| P4 | Mã vạch | P0, Q5 |
| P5 | Nhập Excel ở Danh mục và Đặt hàng | Q6; nên tách spec riêng |
| P6 | Ctrl+P ở hộp sửa | Q7 |

`Kazuko_FnB/lib/` không nằm trong git: thêm DLL phải chép tay ở mỗi máy và ghi vào tài liệu cài đặt. Worktree của agy được chép `lib/` từ thư mục chính.

## Rủi ro

- Đưa DLL `No1Lib` (nền của app gốc) vào app mới: DLL có thể đòi khởi tạo cấu hình riêng khi mẫu gọi hàm của nó. Mới thử với dữ liệu giả, chưa thử trong app mới.
- FastReport 1.2.47 cũ; biên dịch script lúc chạy làm lần in đầu chậm (**chưa đo**).
- Mẫu có thể đã được quán sửa riêng, khác bản mẫu trong `POS TAP/Database`.
- Công thức tổng trong mẫu có thể lệch với màn hình bản mới.

## Câu hỏi cho Chiến

| # | Câu hỏi, ví dụ | Đề xuất |
|---|---|---|
| Q1 | **Chiến chốt 26/11:** "Đồng ý, chép" `No1Lib*.dll` và `DbMapping.DLL` từ app gốc vào `Kazuko_FnB/lib/` | Đã chép 9 file (mã băm khớp `POS TAP/`). Chưa thêm tham chiếu nào vào project |
| Q2 | **Chiến duyệt 26/11 theo đề xuất.** Làm theo thứ tự nào? | P0 → P1 → P2 → P3 → P4; P5, P6 để sau |
| Q3 | **Đã kiểm trên app gốc 26/11:** không in thẳng. App hiện hộp "CHỌN MẪU IN" (mẫu + Xem trên màn hình, In ra máy in, Xuất excel, Xuất pdf, Thiết kế mẫu in) | Làm y như vậy, xem [`giao-dien.md`](giao-dien.md). Phần "Thiết kế mẫu in" hỏi ở Q8 |
| Q4 | **Chiến duyệt 26/11 theo đề xuất.** **App gốc (26/11):** "Xuất excel" ra file `.xls` đúng các cột đang hiện. Còn chưa thử khi đang lọc: ví dụ lọc còn 3 trong 50 mặt hàng, file có 3 hay 50 dòng? | 3 dòng, đúng cái đang hiện. Có thể dùng `Aspose.Cells` đã có trong `lib/` |
| Q5 | **Chiến duyệt 26/11 theo đề xuất.** **Đã kiểm:** khung "IN MÃ VẠCH", thêm từng mặt hàng, chọn 1 trong 6 mẫu, xem trước. Nút "Bartender" (phần mềm in tem ngoài) có cần không? | Làm khung và 6 mẫu; chưa làm Bartender |
| Q6 | **Chiến duyệt 26/11 theo đề xuất.** "Nhập Excel" có thuộc đợt này không? Nó ghi dữ liệu (giá, mặt hàng), không phải in. | Tách spec riêng vì sai thì hỏng giá |
| Q7 | **Chiến duyệt 26/11 theo đề xuất.** Ctrl+P ở hộp sửa mặt hàng, bàn, khu vực in gì? **Chưa xem** trên app gốc | Giữ "chưa hỗ trợ" đợt này |
| Q8 | **Chiến duyệt 26/11 theo đề xuất.** Hộp chọn mẫu của app gốc có "Thiết kế mẫu in" (sửa mẫu và lưu vào CSDL). Ví dụ quán muốn thêm số điện thoại vào chân bill. Bản mới có cần không? | Chưa làm đợt này: ẩn lựa chọn đó. Làm sau nếu quán cần sửa mẫu |
| Q9 | **Chiến chốt 26/11:** "Chốt y hệt": màn Chi tiết hoạt động ngày và Kết quả kinh doanh làm như app gốc, **cả màn là trình xem FastReport nhúng trong tab**, bộ lọc ở trên, không nút "In báo cáo" | Thứ tự làm (đề xuất của Claude): giữ lưới hiện nay cho đến khi mẫu dựng được và số trên trang in khớp số đang hiện, rồi mới thay |

Spec đã duyệt 26/11. Trước khi code P3, P4 vẫn phải đối chiếu thêm với app gốc (Excel khi đang lọc, "In danh sách" ở menu chuột phải, lưới chi tiết, tem có mã vạch). P5 (nhập Excel) cần spec riêng được duyệt. P6 (Ctrl+P) giữ "chưa hỗ trợ".
