# QUEUE — Spec + Implement FnB (mục Hoạt động)

Thứ tự theo menu Hoạt động thật của POS TAP. 1 spec = 1 commit. Từ 25/11 do vòng lặp tự động ([`../workflow-agy.md`](../workflow-agy.md) §10) đọc và cập nhật; mục "Loại" theo tiêu chí §10.1 (nhẹ: Claude tự duyệt; nặng: chờ Chiến trả lời trên issue nhãn `cho-chien`).

## Đã có code trên `main`
- [x] Danh mục mặt hàng — spec `6a57cd3`, patch 1
- [x] Danh mục bàn khu vực — spec `6a87224`, patch 2
- [x] Khách đặt hàng — spec `ce52532`, patch 3
- [x] Theo dõi đặt phòng — spec `13d90f8`, code `ab440dd`
- [x] Quỹ tiền bước 1–2 — `quy-tien.md` (đã duyệt); bước 3–5 chờ spec bán hàng
- [x] Lưu vết hoạt động — `luu-vet-hoat-dong.md` (đã duyệt 25/11); code `3bec043`, `f9dd6bc` (Antigravity), issue #6
- [x] Danh mục hóa đơn hủy — `danh-muc-hoa-don-huy.md` (Claude tự duyệt 25/11); code `de80345` (Antigravity), issue #7
- [x] Thống kê mặt hàng bán — `thong-ke-mat-hang-ban.md` (duyệt 25/11, issue #8); code `67319b2` (Antigravity), issue #9. **Chưa làm:** tự làm tươi khi dữ liệu đổi

- [x] Thống kê doanh thu — `thong-ke-doanh-thu.md` (duyệt 25/11, issue #10); code `7a2fc98` (Antigravity). **Chưa làm:** tự làm tươi khi dữ liệu đổi
- [x] Chi tiết hoạt động ngày — `chi-tiet-hoat-dong-ngay.md` (duyệt 25/11, issue #11); code `ffc119b` (Antigravity), issue #13. **Chưa làm:** mẫu in (Q5)
- [x] Tổng hợp kết quả kinh doanh — `tong-hop-ket-qua-kinh-doanh.md` (duyệt 25/11, issue #14); code `c6c6c42` (Antigravity), issue #15. **Chưa làm:** mẫu in (C10)

## Chờ Chiến bấm thử (gộp theo đợt, trước khi dùng ở quán)
- [ ] Lưu vết hoạt động — xem issue #6 (điểm cần chốt: lọc tài khoản theo tên đăng nhập; cột rộng phải cuộn ngang)
- [ ] Danh mục hóa đơn hủy — xem issue #7 (chưa kiểm 125% DPI)
- [ ] Thống kê mặt hàng bán — xem issue #9 (đối chiếu số với app gốc; chưa kiểm 125% DPI)

- [ ] Thống kê doanh thu — xem issue #12; số đã khớp app gốc (spec §6.1); chưa kiểm 125% DPI
- [ ] Chi tiết hoạt động ngày — xem issue #13 (thử cuộn bằng chuột/chạm, chuyển tab rồi quay lại, đối chiếu số khi có phiếu nhập; chưa kiểm 125% DPI)
- [ ] Tổng hợp kết quả kinh doanh — xem issue #15 (chưa đối chiếu số với app gốc; chưa kiểm 125% DPI)

## Hàng đợi spec (thứ tự làm)
| # | Spec | Trạng thái | Loại | Ghi chú |
|---|---|---|---|---|
| 1 | `danh-muc-hoa-don-huy.md` | **Đã code** (`de80345`) | nhẹ | Chờ bấm thử |
| 2 | `thong-ke-mat-hang-ban.md` | **Đã code** (`67319b2`) | nặng | Chờ bấm thử |
| 3 | `thong-ke-doanh-thu.md` | **Đã code** (`7a2fc98`) | nặng | Chờ bấm thử |
| 4 | `chi-tiet-hoat-dong-ngay.md` | **Đã code** (`ffc119b`) | nặng | Chờ bấm thử |
| 5 | `tong-hop-ket-qua-kinh-doanh.md` | **Đã code** (`c6c6c42`) | nặng | Chờ bấm thử |
| 6 | `bao-gia-ban-hang.md` | **ĐÃ DUYỆT** 25/11, chưa code; **chờ Chiến** (issue #16) | nặng | Giá bán; SL lẻ khi hàng 2 ĐVT |
| 7 | `danh-muc-khach-hang.md` | **ĐÃ DUYỆT** 25/11, chưa code; **chờ Chiến** (issue #17) | nặng | Điểm tích lũy, thẻ trả trước, luật xóa khách (Q2, Q5) |
| 8 | `su-dung-dich-vu.md` | **ĐÃ DUYỆT** 25/11, chưa code | nặng | Thanh toán; Q2–Q20 đã chốt 25/11 (spec §6.3–§6.5) |
| 9 | `quan-ly-ban-hang-dieu-chinh-hoa-don.md` | **ĐÃ DUYỆT** 25/11, chưa code | nặng | Sửa hóa đơn đã thanh toán; tách 2 patch |

## Ghi chú
- Review cũ của Muse (issue #1–#5) đã đóng. Quyết định chốt áp cho mọi spec: xóa cứng cả app, LamTron 1000, tiền decimal, F&B thuần COFFEE + POS, bán lẻ tự phục vụ.
- Sử dụng dịch vụ (Chiến chốt 23/11): Q1 = chỉ VNĐ + LamTron 1000; P3 = giữ trigger gốc (thân trigger đã đọc 25/11).
