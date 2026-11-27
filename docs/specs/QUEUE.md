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
- [ ] In P4, mã vạch (`bdbf7b0`, #32): chọn mặt hàng, In mã vạch, Thêm, đổi số lượng, In, chọn mẫu, xem trước; in tem thật
- [ ] Danh mục khách hàng (`018ba78`, #33): thêm/sửa/xóa khách và nhóm, trùng Mã/Tên, xóa nhóm có nhóm con, Ctrl+K, xuất Excel
- [ ] Sử dụng dịch vụ patch (a) (`91a57c7`, #34): mở bàn, gọi món, giảm/xóa/sửa dòng, tổng tiền, combo, chặn âm kho; thanh toán chưa có
- [ ] Sử dụng dịch vụ patch (b) (`56f2036`, #35): thanh toán tiền mặt/thẻ/CK/voucher/thẻ trả trước/điểm/đặt trước, nợ khách, thanh toán chung, trừ kho, phiếu chi trả cọc dư
- [ ] Sử dụng dịch vụ patch (c) (`35c0878`, #36): chuyển bàn, gộp bàn, chuyển mặt hàng, hủy bill
- [ ] Sử dụng dịch vụ patch (d) (`ac6c750`, #37): in hóa đơn, in tạm tính, in lại, in bếp (xem trước, máy in tem/hóa đơn khi có máy)
- [ ] Nhân sự P2 (`4f40fbd`, #60): Thưởng phạt (số phiếu TP, giới hạn tháng) và Tạm ứng lương (phiếu chi LATAMUNG=30, quỹ giảm, chi vượt quỹ như phiếu chi)
- [ ] Nhân sự P1 (`708952b`, #59): Danh mục nhân viên và ca làm việc: cách tính lương, ẩn/hiện theo máy chấm công, trùng tên, chặn xóa khi còn tham chiếu
- [ ] Danh mục cửa hàng (`37211bd`, #58): thêm/sửa/xóa, trùng mã tên, chặn xóa khi còn tham chiếu và cửa hàng cuối, hiện menu theo NhieuCuaHang
- [ ] Xuất lại định lượng (`bf9dd5a`, #57): bắt buộc khoảng ngày, Xem trước, gõ XACNHAN, xóa và sinh lại dòng xuất nguyên liệu của hóa đơn đã thanh toán, báo tồn âm, tính lại giá vốn
- [ ] Công nợ ban đầu (`a66609e`, #56): nợ đầu khách và nhà cung cấp, hiện đúng ở Công nợ, thu nợ theo đơn vào nợ đầu, chặn giảm dưới số đã thu
- [ ] Tồn kho ban đầu (`1b6998a`, #55): nhập tồn đầu và giá vốn, một phiếu mỗi kho, chặn âm, tự tính giá vốn
- [ ] Khách hàng thân thiết (`911b294`, #54): lưới điểm, tab hóa đơn, tặng/giảm điểm, chặn giảm vượt, tự nâng nhóm, xuất Excel
- [ ] Khuyến mại P2 (`13d858e`, #53): áp dụng khi bán: các loại giảm, Mua x tặng y, tặng theo trị giá, giữ giảm tay lớn hơn, bill đã thanh toán không đổi, in hóa đơn có dòng tặng
- [ ] Danh mục đợt khuyến mại P1 (`902060b`, #52): thêm/sửa/xóa đợt theo loại, chặn đợt trùng, trạng thái; P2 áp dụng khi bán chưa làm
- [ ] Danh mục voucher (`fc50402`, #51): cây nhóm, thêm/sửa/xóa, đã dùng, thanh toán bằng voucher (chưa tới ngày phát hành, quá hạn, bill hủy, đã dùng)
- [ ] Tính lại giá vốn (`3f5c6ce`, #49): bấm Tính lại, chạy hai lần liền, quyền, cấu hình tự động sau phiếu nhập, đối chiếu giá vốn với app gốc
- [ ] Kiểm kê kho (`dd98a2f`, #48): chênh lệch, tồn khớp sau kiểm kê (cả 2 ĐVT), tồn đổi giữa chừng, sửa/xóa gây âm kho, chưa kiểm kê
- [ ] Công nợ KH, NCC, thu nợ theo đơn (`eafcc4e`, #46): công thức nợ, thu nợ nhiều đơn, thu quá nợ một đơn, chi trả NCC, đối chiếu "Nợ cũ" ở Sử dụng dịch vụ
- [ ] Tồn kho (`476c3ac`, #45): lọc, tồn 2 ĐVT, tô màu, đối chiếu số tồn với các phiếu, ẩn giá, xuất Excel
- [ ] Xuất khác và Chuyển kho (`477c605`, #44): xuất vượt tồn, chuyển ĐVT chẵn, dòng đối ứng, sửa/xóa gây âm kho
- [ ] Nhập hàng vào kho (`cedccd2`, #43): thêm/sửa/xóa phiếu, ĐVT chẵn, công thức tổng, chặn âm kho, lưới
- [ ] Danh mục nhà cung cấp (`0228433`, #41): cây nhóm, thêm/sửa/xóa NCC và nhóm, xóa còn tham chiếu, quyền Thủ kho
- [ ] Danh mục kho hàng (`b4bb293`, #40): thêm/sửa/xóa, trùng tên, xóa kho còn tham chiếu, cờ âm kho, quyền Thủ kho
- [ ] Quỹ (`66e5b4c`, #39): tạo/sửa/xóa phiếu thu chi, chi vượt quỹ, danh mục lý do và tài khoản, hủy bill có cọc bị chặn, Lưu & In khi đã lưu
- [ ] Quản lý bán hàng + Điều chỉnh hóa đơn (`92f92de`, #38): sửa bill đã thanh toán và kiểm Tồn quỹ/kho/công nợ, trả đồ, hủy phiếu
- [ ] Báo giá bán hàng — xem issue #18 (menu ẩn nên không mở được từ menu; thử số lượng 2 đơn vị tính, nhập Excel; chưa kiểm 125% DPI)

## Bắt buộc xong trong 2 ngày (Chiến yêu cầu 26/11 "trong vòng 2 ngày phải xong toàn bộ"; Claude chốt danh sách ưu tiên theo lõi bán hàng, Chiến giao quyền chốt)
Làm theo thứ tự; "chưa có spec" thì viết spec nháp và tự duyệt theo workflow-agy §10.1.1 (có `auto.on`), câu về tiền, kho, công nợ không đủ căn cứ thì mở issue `cho-chien` rồi làm mục kế tiếp.
1. Danh mục khách hàng (**đã code** `018ba78`)
2. Sử dụng dịch vụ: bán hàng và thanh toán (**đủ 4 patch đã code**: (a) `91a57c7`, (b) `56f2036`, (c) `35c0878`, (d) `ac6c750`)
3. Quản lý bán hàng, Điều chỉnh hóa đơn (**đã code** `92f92de`)
4. Quỹ: Tạo phiếu thu/chi ghi thật, Danh mục phiếu thu/chi, Danh mục lý do thu chi, Danh mục tài khoản ngân hàng (spec `phieu-thu-chi.md`; **đã code** `66e5b4c`)
5. Kho (Danh mục kho hàng **đã code** `b4bb293`, Danh mục nhà cung cấp **đã code** `0228433`; Nhập kho **đã code** `cedccd2`; Xuất/Chuyển **đã code** `477c605`; Tồn kho **đã code** `476c3ac`; Công nợ **đã code** `eafcc4e`, issue #42; chưa code): Danh mục kho hàng, Danh mục nhà cung cấp (spec nháp), Nhập hàng vào kho, Xuất khác, Chuyển kho, Tồn kho (chưa có spec)
6. Công nợ khách hàng, Công nợ nhà cung cấp (**đã code** `eafcc4e`)
**Ngoài 2 ngày (làm sau):** Kiểm kê kho, Tính lại giá vốn, Xuất lại định lượng, Tồn nhiều kho, Danh mục cửa hàng, khuyến mại, voucher, thẻ trả trước, khách hàng thân thiết, Kiểm soát order, mật khẩu wifi, toàn bộ Nhân sự, Dữ liệu ban đầu, cấu hình máy in theo khu vực, in P3b, nhập Excel P5, Ctrl+P P6.

## Quyết định của Chiến 26/11 (áp cho mọi spec bán hàng, kho, công nợ)
- Âm kho: có cấu hình toàn hệ thống "cho phép âm kho", bản đầu mặc định **chặn** (bán hoặc xuất khi tồn không đủ thì không cho).
- Nợ: chỉ khách **có hồ sơ** (`DKHACHHANG`) mới được ghi nợ; khách vãng lai không được mua nợ.
- Nút "Lưu & In", "Lưu & Xem in" ở hộp sửa Đặt hàng, Báo giá **bật khi phiếu đã lưu** (có ID) dù chưa sửa; vẫn tắt khi chỉ có quyền Xem. (Chưa làm: patch nhỏ sau.)
- Hủy bill (Sử dụng dịch vụ và Quản lý bán hàng) có phiếu thu cọc đặt trước (`TDATHANGID`) thì **chặn** (Chiến chốt 27/11, #36). **Đã làm** `66e5b4c`.
- Sử dụng dịch vụ gồm **cả in hóa đơn thanh toán (A4, 80mm) và in bếp**, cùng cách in của P2 (mẫu `STEMPLATE`, ra spec `su-dung-dich-vu.md` khi làm).

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
| – | `in-xuat-excel/index.md` | **ĐÃ DUYỆT** 26/11; P0 `8a9c0ee`, P1a `100513f`, P1b `2be56da`, P2 `29fdaa8`, P3a `f9874e2`, P4 `bdbf7b0` đã code; còn P3b | nặng | In, xuất Excel, mã vạch; dùng FastReport |

## Ghi chú
- 26/11: Chiến chốt quy tắc nhập số cho **mọi ô số** (`khach-dat-hang.md`, đợt 2, điểm 5): chấm hay phẩy đều là dấu thập phân, dấu ngăn nghìn chỉ để hiển thị. Mới áp cho lưới Khách đặt hàng (`bf45f06`); các ô còn lại cần spec riêng.
- Đã làm 26/11: Khách đặt hàng theo `bao-gia-ban-hang.md` §5.0.2, code `cc928e9` (issue #23). Việc chờ làm (Chiến chốt 26/11): sau này thêm cấu hình toàn hệ thống cho phép âm kho.
- 25/11 (vòng lặp): #8 và #9 tạm bỏ qua vì phụ thuộc #7 đang chờ (#7 tạo lớp truy vấn khách cho màn hóa đơn; #9 dùng bộ máy của #8). Bỏ qua "Danh mục bảng giá" vì là bảng giá tiền giờ, ngoài phạm vi.
- 26/11: `danh-muc-loai-phong/index.md` tạm bỏ (menu chỉ hiện ở bản HOTEL); "Thông báo sửa chữa" bỏ cùng lý do.
- Review cũ của Muse (issue #1–#5) đã đóng. Quyết định chốt áp cho mọi spec: xóa cứng cả app, LamTron 1000, tiền decimal, F&B thuần COFFEE + POS, bán lẻ tự phục vụ.
- Sử dụng dịch vụ (Chiến chốt 23/11): Q1 = chỉ VNĐ + LamTron 1000; P3 = giữ trigger gốc (thân trigger đã đọc 25/11).
