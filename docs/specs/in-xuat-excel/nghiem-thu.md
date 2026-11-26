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
| P1 | 3 báo cáo còn lại | P0 |
| P2 | In Báo giá, Khách đặt hàng | P0, tìm được `ToVndWords` |
| P3 | Xuất Excel và In danh sách của lưới | Q4 |
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
| Q1 | Chỉ DLL FastReport thì không dựng được mẫu nào. Có cho chép `No1Lib*.dll`, `DbMapping.DLL` từ app gốc vào `Kazuko_FnB/lib/` không? Ví dụ mẫu "Thống kê mặt hàng bán A4" thiếu chúng thì báo "Metadata file No1Lib.Utils.dll could not be found". | **Có** (sản phẩm của công ty). Phương án khác là sửa từng mẫu hoặc dựng lại mẫu: tốn hơn nhiều và lệch app gốc |
| Q2 | Làm theo thứ tự nào? | P0 → P1 → P2 → P3 → P4; P5, P6 để sau |
| Q3 | Bấm In ở app gốc thì in thẳng ra máy in hay hiện xem trước? Ví dụ thu ngân bấm "In" ở Thống kê doanh thu. | Mở app gốc xem rồi làm y như vậy (cần bạn sẵn sàng). Chưa xem được thì dùng xem trước |
| Q4 | "Xuất Excel" của lưới lấy đúng cái đang hiện, hay theo mẫu in? Ví dụ lưới đang lọc còn 3 trong 50 mặt hàng: file có 3 hay 50 dòng? | 3 dòng, đúng cái đang hiện; xác nhận lại với app gốc. Có thể dùng `Aspose.Cells` đã có trong `lib/` |
| Q5 | In mã vạch: in cho các dòng đang chọn hay mở màn chọn riêng, ví dụ 3 món, mỗi món 20 tem? | Theo app gốc, cần xem trước khi viết P4 |
| Q6 | "Nhập Excel" có thuộc đợt này không? Nó ghi dữ liệu (giá, mặt hàng), không phải in. | Tách spec riêng vì sai thì hỏng giá |
| Q7 | Ctrl+P ở hộp sửa mặt hàng, bàn, khu vực in gì ở app gốc? | Chưa biết; xem app gốc, không có mẫu thì giữ "chưa hỗ trợ" |

Duyệt spec này chưa đủ để code P3 đến P6: các phần đó cần trả lời câu tương ứng và đối chiếu app gốc.
