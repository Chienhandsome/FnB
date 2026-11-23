# QUEUE — Spec + Implement FnB (mục Hoạt động)

Thứ tự theo menu Hoạt động thật của POS TAP. 1 spec = 1 commit. Cập nhật trạng thái sau mỗi spec.

## Đã xong (spec + implement)
- [x] Danh mục mặt hàng — spec `6a57cd3`, patch 1 đã giao
- [x] Danh mục bàn khu vực — spec `6a87224`, patch 2 đã giao
- [x] Khách đặt hàng — spec `ce52532`, patch 3 đã giao
- [x] Theo dõi đặt phòng — spec `13d90f8`, patch đã apply + commit ở `ab440dd`; Q6 + 5 mục §5 đã chốt "giữ y hệt hết" (23/11)

## Spec xong, chờ implement (Muse)
- [ ] Sử dụng dịch vụ — spec `0e37e01`, review xong (issue #1), chờ Muse implement
- [ ] Quản lý bán hàng — spec `295df3f` (gộp chung file với Điều chỉnh hóa đơn, giữ nguyên file gộp; lúc implement tách thành 2 patch)
- [ ] Điều chỉnh hóa đơn — spec `295df3f` (chung file như trên)

## Tiếp theo (Claude làm spec)
- [ ] Báo giá bán hàng — LÀM TIẾP THEO (Chiến chốt 23/11, giữ đúng thứ tự menu)
- [ ] Lưu vết hoạt động
- [ ] Thống kê doanh thu
- [ ] Thống kê mặt hàng bán
- [ ] Tổng hợp kết quả kinh doanh
- [ ] Chi tiết hoạt động ngày
- [ ] Danh mục hóa đơn hủy

## Ghi chú
- Quyết định chốt áp cho mọi spec: xóa cứng cả app, LamTron 1000, tiền decimal, F&B thuần COFFEE + POS, bán lẻ tự phục vụ.
- Sử dụng dịch vụ (Chiến chốt 23/11): Q1 = chỉ VNĐ + LamTron 1000, bỏ nhánh USD; P3 = giữ trigger gốc — lấy thân trigger TDONHANG_TRIGGER từ DATA.fdb trước khi implement phần liên kết bàn–bill.
- Review của Muse nằm ở GitHub Issues, label `spec-review` — kiểm tra trước mỗi spec mới.
- Dừng: hết queue / push conflict / app gốc lỗi / 6h30 sáng 24/11. Hết quota 5h thì chờ hồi, không dừng.
