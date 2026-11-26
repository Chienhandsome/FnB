# Brief: In, xuất Excel, FastReport

- Spec **ĐÃ DUYỆT** `docs/specs/in-xuat-excel/` (Chiến duyệt 26/11, theo đề xuất).
- **P0 đã code, đã push** `8a9c0ee` (issue #27): `Kazuko_FnB.UI/Printing/`, hộp CHỌN MẪU IN, xem trước, in Thống kê mặt hàng bán. Trước đó `30ced3e` tham chiếu 7 DLL .NET của app gốc. Selftest 898 đạt, 0 lỗi.
- `Kazuko_FnB/lib/` không nằm trong git: **máy khác phải chép tay** `No1Lib*.DLL`, `DbMapping.DLL` từ `POS TAP/`.
- Quan trọng cho P1, P2: mẫu có `<Total>` trùng tên tham số; engine đã gỡ trước khi truyền. A4 mặt hàng bán đã khớp app gốc từng ô.
- Chưa kiểm: in thật, mở file PDF/Excel xem bằng mắt, nút Close của xem trước, 125% DPI, 54mm và 80mm so với app gốc.
- Còn: P1 (in Doanh thu; đổi Chi tiết hoạt động ngày và KQKD thành trình xem nhúng, Q9; mẫu `SREPORTTEMPLATE` chưa dựng thử), P2 (đặt hàng, báo giá; `ToVndWords` = `No1Lib.Sys.DocSo.ToVndWords`), P3, P4 (đối chiếu thêm), P5 (spec riêng), P6 (chưa làm).
- Đã đối chiếu app gốc: ảnh `_tmp/_origin-shots/in-xuat-excel/`. Chưa xem: Ctrl+P, hộp sửa bàn và khu vực, lưới chi tiết, xuất định lượng, nhập Excel, "Lưu & In", Bartender, Excel khi đang lọc.
- **P1, 26/11 (đang dở):** dựng thử được cả 3 mẫu `SREPORTTEMPLATE`; KQKD phải gọi riêng sự kiện script mới có cột % (`nghiep-vu.md` mục cuối). Chưa giao agy, chưa đối chiếu số với app gốc. Script và ảnh thử: `fnb-agy-work/_tmp/in-p1/`. Hạn mức đầu phiên: Pro, 5 giờ 79%, tuần 92%.
- Cần Chiến chốt: Chi tiết hoạt động ngày dùng mẫu nào. `chi-tiet-hoat-dong-ngay.md` Q9 (25/11) chốt "Mẫu đầy đủ" (có cột Chuyển khoản), còn app gốc trỏ `LASTTEMPLATEID` vào `765b861a` (không có cột này).
