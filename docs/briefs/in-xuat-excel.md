# Brief: In, xuất Excel, FastReport

- Spec **NHÁP** `docs/specs/in-xuat-excel/` (26/11). Chưa code. Q1 đã chốt, Q3 đã đối chiếu app gốc 26/11; chờ Q2, Q4–Q8 rồi duyệt.
- Điểm chính: chỉ DLL FastReport thì không dựng được mẫu; cần thêm `No1Lib*.dll`, `DbMapping.DLL` của app gốc. Đã chép 9 file từ `POS TAP/` vào `Kazuko_FnB/lib/` (thư mục không nằm trong git: **máy khác phải chép tay**).
- Đã thử ngoài app với dữ liệu giả: 30/64 mẫu `STEMPLATE` dựng được, gồm mẫu của Thống kê doanh thu, Thống kê mặt hàng bán, mã vạch. Báo giá thiếu hàm `ToVndWords`. Mẫu `SREPORTTEMPLATE` chưa thử.
- Đã mở bản chép app gốc xem luồng In ở Thống kê doanh thu (ảnh `_tmp/_origin-shots/in-xuat-excel/`); không gửi lệnh in. Chưa làm: mở file xuất ra xem nội dung, so từng màn với app gốc để rà đủ chỗ thiếu.
