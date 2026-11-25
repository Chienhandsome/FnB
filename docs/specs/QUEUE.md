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
- [x] Báo giá bán hàng — spec `bao-gia-ban-hang.md` (24/11, commit xem git log `docs: spec Báo giá bán hàng`), chờ Chiến duyệt + Muse review
- [x] Lưu vết hoạt động — spec `luu-vet-hoat-dong.md` (24/11), chờ Chiến duyệt + Muse review
- [x] Thống kê doanh thu — spec `thong-ke-doanh-thu.md` (24/11), chờ Chiến duyệt + Muse review
- [x] Thống kê mặt hàng bán — spec `thong-ke-mat-hang-ban.md` (24/11), chờ Chiến duyệt + Muse review
- [x] Tổng hợp kết quả kinh doanh — spec `tong-hop-ket-qua-kinh-doanh.md` (24/11), chờ Chiến duyệt + Muse review
- [x] Chi tiết hoạt động ngày — spec `chi-tiet-hoat-dong-ngay.md` (24/11), chờ Chiến duyệt + Muse review
- [x] Danh mục hóa đơn hủy — spec `danh-muc-hoa-don-huy.md` (24/11), chờ Chiến duyệt + Muse review

## Phát sinh sau queue
- [x] Quỹ tiền (tiền mặt + tài khoản) — spec `quy-tien.md` (25/11), chờ Chiến duyệt; Q1–Q13 đã chốt 25/11

## Ghi chú
- Review #1 (Sử dụng dịch vụ) và #2 (Quản lý bán hàng + Điều chỉnh hóa đơn) đã sửa spec 24/11 (commit `6d40f73`, `9e48bc8`); **chưa đóng issue** vì máy chạy loop không có `gh`/token — Chiến hoặc Muse đóng tay.
- Quyết định chốt áp cho mọi spec: xóa cứng cả app, LamTron 1000, tiền decimal, F&B thuần COFFEE + POS, bán lẻ tự phục vụ.
- Sử dụng dịch vụ (Chiến chốt 23/11): Q1 = chỉ VNĐ + LamTron 1000, bỏ nhánh USD; P3 = giữ trigger gốc — lấy thân trigger TDONHANG_TRIGGER từ DATA.fdb trước khi implement phần liên kết bàn–bill.
- Review của Muse nằm ở GitHub Issues, label `spec-review` — kiểm tra trước mỗi spec mới.
- Dừng: hết queue / push conflict / app gốc lỗi / 6h30 sáng 24/11. Hết quota 5h thì chờ hồi, không dừng.
- **24/11 00:10 — HẾT QUEUE** (7/7 spec của loop đêm đã viết, mỗi spec 1 commit). Chờ Chiến duyệt + Muse review; issue #1, #2 đã sửa spec nhưng chưa đóng (máy loop không có `gh`/token).
