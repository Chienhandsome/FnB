# QUEUE — Spec + Implement FnB (mục Hoạt động)

Thứ tự theo menu Hoạt động thật của POS TAP. 1 spec = 1 commit. Từ 25/11 do vòng lặp tự động ([`../workflow-agy.md`](../workflow-agy.md) §10) đọc và cập nhật; mục "Loại" theo tiêu chí §10.1 (nhẹ: Claude tự duyệt; nặng: chờ Chiến trả lời trên issue nhãn `cho-chien`).

## Đã có code trên `main`
- [x] Danh mục mặt hàng — spec `6a57cd3`, patch 1
- [x] Danh mục bàn khu vực — spec `6a87224`, patch 2
- [x] Khách đặt hàng — spec `ce52532`, patch 3
- [x] Theo dõi đặt phòng — spec `13d90f8`, code `ab440dd`
- [x] Quỹ tiền bước 1–2 — `quy-tien.md` (đã duyệt); bước 3–5 chờ spec bán hàng
- [x] Lưu vết hoạt động — `luu-vet-hoat-dong.md` (đã duyệt 25/11); code `3bec043`, `f9dd6bc` (Antigravity), issue #6

## Chờ Chiến bấm thử (gộp theo đợt, trước khi dùng ở quán)
- [ ] Lưu vết hoạt động — xem issue #6 (điểm cần chốt: lọc tài khoản theo tên đăng nhập; cột rộng phải cuộn ngang)

## Hàng đợi spec (thứ tự làm)
| # | Spec | Trạng thái | Loại | Ghi chú |
|---|---|---|---|---|
| 1 | `danh-muc-hoa-don-huy.md` | ĐÃ DUYỆT 25/11 (Claude tự duyệt), đang code | **nhẹ** | Màn chỉ xem; Q3 chỉ là hiển thị số đã lưu |
| 2 | `thong-ke-mat-hang-ban.md` | NHÁP | nặng | Cộng số lượng và tiền |
| 3 | `thong-ke-doanh-thu.md` | NHÁP | nặng | Công thức doanh thu |
| 4 | `chi-tiet-hoat-dong-ngay.md` | NHÁP | nặng | Tiền theo quỹ |
| 5 | `tong-hop-ket-qua-kinh-doanh.md` | NHÁP | nặng | Kết quả kinh doanh |
| 6 | `bao-gia-ban-hang.md` | NHÁP | nặng | Giá bán |
| 7 | `danh-muc-khach-hang.md` | NHÁP | nặng | Điểm tích lũy, thẻ trả trước, luật xóa khách (Q2, Q5) |
| 8 | `su-dung-dich-vu.md` | NHÁP | nặng | Thanh toán; Q14–Q20 chốt 25/11; còn Q2–Q13 (đang dùng mặc định tạm) |
| 9 | `quan-ly-ban-hang-dieu-chinh-hoa-don.md` | NHÁP | nặng | Sửa hóa đơn đã thanh toán; tách 2 patch |

## Ghi chú
- Review cũ của Muse (issue #1–#5) đã đóng. Quyết định chốt áp cho mọi spec: xóa cứng cả app, LamTron 1000, tiền decimal, F&B thuần COFFEE + POS, bán lẻ tự phục vụ.
- Sử dụng dịch vụ (Chiến chốt 23/11): Q1 = chỉ VNĐ + LamTron 1000; P3 = giữ trigger gốc (thân trigger đã đọc 25/11).
