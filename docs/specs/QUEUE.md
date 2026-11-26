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
- [x] Báo giá bán hàng: `bao-gia-ban-hang.md` (duyệt 25/11, issue #16); code `cca8711` (Antigravity), issue #18. **Chưa làm:** in, xuất Excel. Menu đang ẩn như app gốc

## Chờ Chiến bấm thử (gộp theo đợt, trước khi dùng ở quán)
- [ ] Lưu vết hoạt động — xem issue #6 (điểm cần chốt: lọc tài khoản theo tên đăng nhập; cột rộng phải cuộn ngang)
- [ ] Danh mục hóa đơn hủy — xem issue #7 (chưa kiểm 125% DPI)
- [ ] Thống kê mặt hàng bán — xem issue #9 (đối chiếu số với app gốc; chưa kiểm 125% DPI)

- [ ] Thống kê doanh thu — xem issue #12; số đã khớp app gốc (spec §6.1); chưa kiểm 125% DPI
- [ ] Chi tiết hoạt động ngày — xem issue #13 (thử cuộn bằng chuột/chạm, chuyển tab rồi quay lại, đối chiếu số khi có phiếu nhập; chưa kiểm 125% DPI)
- [ ] Tổng hợp kết quả kinh doanh — xem issue #15 (chưa đối chiếu số với app gốc; chưa kiểm 125% DPI)
- [ ] Khách đặt hàng, số lượng 2 đơn vị tính: xem issue #23 (thử cả ở Theo dõi đặt phòng)
- [ ] Khách đặt hàng, chặn SL 0, gộp dòng, nhập số trên lưới: xem issue #24 (gõ bàn phím thật, thử cả Theo dõi đặt phòng)
- [ ] Nhập số patch 1 (ô ngoài lưới): xem issue #25 (bấm chuột/chạm vào ô có số lớn, thử từng màn)
- [ ] Nhập số patch 2 (4 lưới): xem issue #26 (gõ bàn phím thật, Ctrl+V, dấu cách ở Ghi chú Báo giá, sửa định lượng rồi Lưu)
- [ ] In P0, Thống kê mặt hàng bán: xem issue #27 (xem trước, xuất Excel và PDF rồi mở, in thật A4 và 80mm; mở lướt các màn khác)
- [ ] In P1a, Thống kê doanh thu (`100513f`): xem trước đủ 4 mẫu, xuất Excel và PDF rồi mở, in thật A4 và 80mm
- [ ] In P1b, Chi tiết hoạt động ngày và KQKD xem nhúng (`2be56da`, #29): mở hai màn, đổi lọc, F5, thanh công cụ FastReport (in, lưu PDF/Excel), đổi theme
- [ ] In P2, Báo giá và Khách đặt hàng (`29fdaa8`, #30): Lưu & In, Lưu & Xem in, hộp IN BÁO CÁO, nút In ở danh sách; in thật; xem phiếu in có "đồng chẵn" hai lần không
- [ ] In P3a, xuất Excel lưới (`f9874e2`, #31): bấm 5 chỗ Excel, mở file bằng Excel, xem có trang "Evaluation Warning" không
- [ ] Báo giá bán hàng — xem issue #18 (menu ẩn nên không mở được từ menu; thử số lượng 2 đơn vị tính, nhập Excel; chưa kiểm 125% DPI)

## Hàng đợi spec (thứ tự làm)
| # | Spec | Trạng thái | Loại | Ghi chú |
|---|---|---|---|---|
| 1 | `danh-muc-hoa-don-huy.md` | **Đã code** (`de80345`) | nhẹ | Chờ bấm thử |
| 2 | `thong-ke-mat-hang-ban.md` | **Đã code** (`67319b2`) | nặng | Chờ bấm thử |
| 3 | `thong-ke-doanh-thu.md` | **Đã code** (`7a2fc98`) | nặng | Chờ bấm thử |
| 4 | `chi-tiet-hoat-dong-ngay.md` | **Đã code** (`ffc119b`) | nặng | Chờ bấm thử |
| 5 | `tong-hop-ket-qua-kinh-doanh.md` | **Đã code** (`c6c6c42`) | nặng | Chờ bấm thử |
| 6 | `bao-gia-ban-hang.md` | **Đã code** (`cca8711`) | nặng | Chờ bấm thử; menu đang ẩn như app gốc |
| – | `nhap-so/index.md` | **ĐÃ DUYỆT** 26/11, làm trước #7 (Chiến chọn) | nặng | Nhập số dùng chung; patch 1 `969285d`, patch 2 `054d7c7`; chờ bấm thử |
| 7 | `danh-muc-khach-hang.md` | **ĐÃ DUYỆT** 25/11, hết chặn (chốt 26/11, §9.0.2), chưa code | nặng | Điểm tích lũy, thẻ trả trước, luật xóa khách (Q2, Q5) |
| 8 | `su-dung-dich-vu.md` | **ĐÃ DUYỆT** 25/11, chưa code | nặng | Thanh toán; Q2–Q20 đã chốt 25/11 (spec §6.3–§6.5) |
| 9 | `quan-ly-ban-hang-dieu-chinh-hoa-don.md` | **ĐÃ DUYỆT** 25/11, chưa code | nặng | Sửa hóa đơn đã thanh toán; tách 2 patch |

| 10 | `danh-muc-cua-hang/index.md` | **NHÁP**, hết câu hỏi, chờ duyệt | nặng | Menu đang ẩn vì `NhieuCuaHang = 0` |
| 11 | `danh-muc-kho-hang/index.md` | **NHÁP**, hết câu hỏi, chờ duyệt | nặng | Cần rà lại giao diện (app gốc là hộp thoại cây) |
| 12 | `danh-muc-nha-cung-cap/index.md` | **NHÁP**, hết câu hỏi, chờ duyệt | nặng | Phụ thuộc #7 (dùng chung khung cây nhóm) |
| – | `in-xuat-excel/index.md` | **ĐÃ DUYỆT** 26/11; P0 `8a9c0ee`, P1a `100513f`, P1b `2be56da`, P2 `29fdaa8`, P3a `f9874e2` đã code; còn P3b, P4 | nặng | In, xuất Excel, mã vạch; dùng FastReport |

## Ghi chú
- 26/11: Chiến chốt quy tắc nhập số cho **mọi ô số** (`khach-dat-hang.md`, đợt 2, điểm 5): chấm hay phẩy đều là dấu thập phân, dấu ngăn nghìn chỉ để hiển thị. Mới áp cho lưới Khách đặt hàng (`bf45f06`); các ô còn lại cần spec riêng.
- Đã làm 26/11: Khách đặt hàng theo `bao-gia-ban-hang.md` §5.0.2, code `cc928e9` (issue #23). Việc chờ làm (Chiến chốt 26/11): sau này thêm cấu hình toàn hệ thống cho phép âm kho.
- 25/11 (vòng lặp): #8 và #9 tạm bỏ qua vì phụ thuộc #7 đang chờ (#7 tạo lớp truy vấn khách cho màn hóa đơn; #9 dùng bộ máy của #8). Bỏ qua "Danh mục bảng giá" vì là bảng giá tiền giờ, ngoài phạm vi.
- 26/11: `danh-muc-loai-phong/index.md` tạm bỏ (menu chỉ hiện ở bản HOTEL); "Thông báo sửa chữa" bỏ cùng lý do.
- Review cũ của Muse (issue #1–#5) đã đóng. Quyết định chốt áp cho mọi spec: xóa cứng cả app, LamTron 1000, tiền decimal, F&B thuần COFFEE + POS, bán lẻ tự phục vụ.
- Sử dụng dịch vụ (Chiến chốt 23/11): Q1 = chỉ VNĐ + LamTron 1000; P3 = giữ trigger gốc (thân trigger đã đọc 25/11).
