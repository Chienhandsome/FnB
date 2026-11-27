# Spec: Kiểm soát order (HOẠT ĐỘNG ▸ Kiểm soát order)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 28/11/2026 theo workflow-agy §10.1.1; Chiến dặn làm liên tục đến 7h sáng 28/11, mục này nằm trong danh sách "làm sau"). Điểm **TẠM** ở §5.
- Ngày: 2026-11-28. Loại: **nhẹ** (màn chỉ đọc, không ghi, không đổi tiền hay kho). Nguồn: `docs/deep/hoat-dong-ban-hang.md` mục 4.1, Q36 và dòng "Kiểm_soát_order" (bảng form); `docs/deep/quan-tri.md` (quyền "Kiểm soát order" `abc261a7-ac27-452e-b1e2-7398f5a285fa`, Quản lý/Kế toán 240, Phục vụ 0); mã đọc hóa đơn có sẵn `SalesManageRepository` (cột `TDONHANG.SOORDER`, `VARCHAR(255)`).
- Menu: `SMENU` "Kiểm soát order" (HOẠT ĐỘNG; nhận diện theo tên không dấu, không phân biệt hoa thường); quyền theo tên chức năng "Kiểm soát order" (Xem; admin luôn được). Không có Thêm/Sửa/Xóa.

## 1. Mục đích

Số order là số viết tay trên phiếu giấy gắn vào hóa đơn (`TDONHANG.SOORDER`, một hoặc nhiều số nguyên cách nhau bằng dấu phẩy, ghi ở Quản lý bán hàng / Điều chỉnh hóa đơn / thanh toán khi cấu hình `BatChucNangKiemSoatOrder` bật). Màn này giúp đối chiếu quyển order giấy với hóa đơn: số nào đã dùng, số nào bị **trùng**, số nào bị **thiếu** trong dải đã dùng, số nào **viết sai** (không phải số nguyên).

## 2. Màn

- **Bộ lọc:** **Từ ngày**, **Đến ngày** (mặc định hôm nay; Từ ngày không sau Đến ngày; so theo ngày, biên đóng), nút **Xem**, xuất Excel.
- **Dữ liệu nguồn:** hóa đơn bán `TDONHANG.LOAI = 0`, `STATUS = 30` có `SOORDER` không rỗng và `NGAY` trong khoảng (gồm cả hóa đơn đã và chưa thanh toán, như gốc chỉ lọc theo ngày; bỏ hóa đơn đã hủy, T1). Người không phải admin chỉ thấy hóa đơn của các cửa hàng được xem (cùng cách giới hạn của màn Quản lý bán hàng).
- **Xử lý (đúng `KiemSoatOrder.LoadData` của gốc):** với mỗi hóa đơn tách `SOORDER` theo dấu phẩy, bỏ khoảng trắng, bỏ phần rỗng, rồi chuyển thành số nguyên:
  - không chuyển được: lỗi **"Lỗi số order '{mã}' ở bill '{số bill}'"** (số bill = `TDONHANG.NAME`);
  - số đã xuất hiện ở hóa đơn khác (hoặc hai lần trong cùng hóa đơn): lỗi **"Trùng số order {N} (bill '{bill1}', '{bill2}'...)"** (một dòng cho mỗi số trùng, liệt kê mọi bill chứa số đó);
  - lấy tập các số hợp lệ **khác nhau** (số trùng chỉ tính một lần), sắp tăng dần rồi quét dải giữa số nhỏ nhất và lớn nhất: số thiếu đơn lẻ: **"Thiếu order {N}"**; nhiều số liên tiếp thiếu: **"Thiếu order {X}-{Y}"**.
- **Hai lưới:** **Đã dùng** (mỗi số order hợp lệ một dòng: **Số order**, **Số bill** (bill đầu tiên theo ngày, số trùng ghi thêm "(trùng)"), **Ngày**, sắp tăng) và **Cần kiểm tra** (các dòng lỗi viết sai, trùng, thiếu: **Loại** [Viết sai/Trùng/Thiếu], **Nội dung** như các câu trên, **Ngày/Bill** khi có). Hàng tổng cộng không cần.
- **Tóm tắt phía trên:** **Lớn nhất**, **Nhỏ nhất**, **Số order đã dùng** (số các số hợp lệ khác nhau), **Số trùng**, **Số lỗi viết sai**, **Số thiếu** (tổng số order thiếu, không phải số dải). Khi không có dữ liệu: "Không có order nào trong khoảng ngày".
- Mở nhanh: nhấp đôi một dòng có số bill thì mở hóa đơn đó ở chế độ xem trong màn có sẵn (nếu cách mở xem hóa đơn từ màn Quản lý bán hàng dễ dùng lại thì dùng; không dễ thì chỉ hiển thị số bill, T2).
- Xuất Excel qua `GridExcelExporter` cho từng lưới (đúng cột đang hiện).
- Chỉ đọc: không có nút lưu; không ghi lưu vết (gốc không ghi).

## 3. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu, số cụ thể)

1. Ba hóa đơn trong ngày D có `SOORDER` = "1,2", "4", "5,7": Đã dùng = 1, 2, 4, 5, 7 (5 dòng, đúng bill); Cần kiểm tra: "Thiếu order 3" và "Thiếu order 6"; Lớn nhất 7, Nhỏ nhất 1, đã dùng 5, thiếu 2, trùng 0.
2. Dải thiếu nhiều số: hóa đơn "1" và "5" → "Thiếu order 2-4"; thiếu 3 số.
3. Trùng: hóa đơn A "2" và B "2,3": Trùng số order 2 liệt kê cả hai bill; số 2 xuất hiện một lần ở "Đã dùng" kèm "(trùng)", số 3 cũng đã dùng, không có "Thiếu"; Số trùng = 1, Số order đã dùng = 2.
4. Viết sai: "12,abc" → "Lỗi số order 'abc' ở bill 'X'", số 12 vẫn tính; "1.5" và "٣" (chữ số lạ) là viết sai; khoảng trắng "1, 2 , 3" hợp lệ; phần rỗng "1,,2" bỏ qua không lỗi.
5. Lọc: hóa đơn ngoài khoảng ngày, hóa đơn đã hủy (`STATUS` khác 30), hóa đơn `LOAI` khác 0, hóa đơn `SOORDER` rỗng không vào kết quả; biên đóng đúng ngày đầu và ngày cuối; người không phải admin không thấy hóa đơn cửa hàng khác.
6. Từ ngày sau Đến ngày: chặn; không dữ liệu: thông báo "Không có order nào trong khoảng ngày".
7. Số rất lớn (vượt `int`) coi là viết sai (không crash); dải rộng không làm treo (khoảng 1.000.000 số thiếu thì nhóm thành dải, không liệt kê từng số; tổng số thiếu tính bằng phép trừ, không lặp từng số).
8. Quyền: không quyền Xem thì menu ẩn; menu mở đúng màn; xuất Excel đúng cột; lưới đủ rộng không cắt chữ; hộp không cuộn dọc thanh nút ở 1366×768; không ghi DB, không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 4. Giao diện

Khung như các màn thống kê đã có (`BusinessResultView`, `DailyActivityView`: bộ lọc ngày phía trên, nhãn tóm tắt, hai lưới, nút Xuất Excel); màu qua `ThemeManager`; tiêu đề cột đủ rộng; cảm ứng được; số hiển thị không phụ thuộc culture máy.

## 5. Điểm TẠM

- **T1** Gốc chỉ lọc `NGAY`, không nói rõ loại và trạng thái hóa đơn (bảng nguồn suy ra `TDONHANG`, Q36); Claude lọc `LOAI = 0`, `STATUS = 30`.
- **T2** Nhấp đôi mở hóa đơn: làm khi dễ, nếu không thì bỏ.
- **T3** Cách xử lý số trùng (tính một lần trong dải quét thiếu) suy ra từ mô tả gốc "trùng/thiếu/tồn tại"; chưa kiểm trên app gốc.
- **T4** Chưa có ảnh app gốc để đối chiếu bố cục (dùng mặc định dự án).
