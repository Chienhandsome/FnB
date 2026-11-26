# Brief: Báo giá bán hàng

- Spec: `docs/specs/bao-gia-ban-hang.md` (§5.0.2 chốt 25–26/11, issue #16 đã đóng).
- **Đã push `cca8711` ngày 26/11.** Build 0 lỗi; selftest 773 đạt, 7 lỗi cũ. Báo cáo và bảng tiêu chí: issue #18.
- Agy: 1 vòng viết, 2 vòng sửa, thêm 1 vòng (r4) do Chiến cho phép. Claude không tự sửa code.
- Sự cố r4: kiểm phạm vi chặn vì Claude chạy `isql` ở thư mục repo trong lúc agy chạy; đã kiểm lại bằng lượt chỉ đọc, đạt.
- Chưa làm: in, xuất Excel; menu giữ ẩn. Chưa kiểm: 125% DPI; chưa chụp hộp nhập Excel và hộp số lượng; bản mới chưa được bấm thử.
- Việc tiếp (Chiến chốt): sửa Khách đặt hàng theo §5.0.2.
