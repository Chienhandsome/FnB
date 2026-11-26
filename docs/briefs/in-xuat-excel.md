# Brief: In, xuất Excel, FastReport

- Spec **NHÁP** `docs/specs/in-xuat-excel/` (26/11). Chưa code. Q1 đã chốt, Q3 đã đối chiếu app gốc 26/11; chờ Q2, Q4–Q9 rồi duyệt.
- Điểm chính: chỉ DLL FastReport thì không dựng được mẫu; cần thêm `No1Lib*.dll`, `DbMapping.DLL` của app gốc. Đã chép 9 file từ `POS TAP/` vào `Kazuko_FnB/lib/` (thư mục không nằm trong git: **máy khác phải chép tay**).
- Đã thử ngoài app với dữ liệu giả: 30/64 mẫu `STEMPLATE` dựng được, gồm mẫu của Thống kê doanh thu, Thống kê mặt hàng bán, mã vạch. Báo giá thiếu hàm `ToVndWords`. Mẫu `SREPORTTEMPLATE` chưa thử.
- Đã đối chiếu app gốc 26/11 (không gửi lệnh in): 4 màn báo cáo, Khách đặt hàng, Báo giá (bật menu tạm trên bản chép), Danh mục: Xuất excel, In lưới, mã vạch. Ảnh `_tmp/_origin-shots/in-xuat-excel/`.
- Chưa xem: Ctrl+P, hộp sửa bàn và khu vực, lưới chi tiết, xuất định lượng, nhập Excel, "Lưu & In", Bartender, Excel khi đang lọc; chưa mở file Excel xuất từ mẫu báo cáo.
