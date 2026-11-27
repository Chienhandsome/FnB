# Spec: Công nợ khách hàng, Công nợ nhà cung cấp

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; điểm **TẠM** ở §7, riêng T1 mở issue `cho-chien`).
- Ngày: 2026-11-27. Loại: **nặng** (tiền, công nợ). Nguồn: `docs/deep/cong-no-quy.md` §b.1–b.4. Phụ thuộc: `phieu-thu-chi.md` (phiếu thu/chi ghi thật), `su-dung-dich-vu.md` §3.7 (cách bill ghi `TIENTHANHTOAN`), `phieu-nhap-kho.md`.
- Menu: CÔNG NỢ ▸ Công nợ khách hàng, Công nợ nhà cung cấp.

## 1. Phạm vi

Làm: hai màn xem công nợ (danh sách đối tượng còn nợ và chi tiết phát sinh), **Thu nợ khách theo từng đơn** (hộp riêng, ghi phiếu thu và `TCHITIETTHANHTOAN`, xem T1), nút Chi trả nợ NCC (dùng lại hộp Phiếu chi của Quỹ), xuất Excel. Không làm: đối chiếu công nợ, báo cáo công nợ theo kỳ, in.

## 2. Công thức (một công thức mỗi bên)

**Khách hàng:** nợ = `Σ(TONGCONG − TIENTHANHTOAN)` của bill `TDONHANG.LOAI IN (0, 99)`, `DATHANHTOAN = 30`, `STATUS = 30` của khách đó, **cộng** `Σ(CHI − THU)` của `TTHUCHI` có `DKHACHHANGID` đó với `COALESCE(KHONGTHAYDOICONGNO, 0) = 0`, `TDATHANGID IS NULL`, phiếu chưa xóa. (Phiếu thu của khách làm nợ giảm; phiếu chi trả lại khách làm nợ tăng.) `LOAI = 99` là nợ đầu.

**Nhà cung cấp:** nợ = `Σ(sign × (TONGCONG − TIENTHANHTOAN))` của phiếu `LOAI IN (1, 2, 98)` (sign = −1 khi `LOAI = 2` xuất trả, ngược lại +1; không lọc `DATHANHTOAN`), `STATUS = 30`, **trừ** `Σ(CHI − THU)` của `TTHUCHI` có `DNHACUNGCAPID` đó (phiếu chi trả NCC làm nợ giảm; phiếu thu hoàn tiền từ NCC làm nợ tăng). `LOAI = 98` là nợ đầu.

Dấu: nợ dương là khách còn nợ mình, hoặc mình còn nợ NCC. Bản gốc có một báo cáo tổng hợp NCC đảo dấu sai; bản mới không lặp lại lỗi đó. Phải khớp "Nợ cũ" mà Sử dụng dịch vụ đã dùng cho khách (`GetNoCu`): nếu hàm đó đã có thì dùng chung.

## 3. Màn Công nợ khách hàng

- Phần trên (danh sách khách): Mã, Tên, Địa chỉ, Điện thoại, **Còn nợ**; lọc nhóm khách, tìm, tích "Chỉ hiện khách còn nợ" (mặc định bật); hàng tổng cộng.
- Phần dưới (chi tiết khách đang chọn): các bill (Số phiếu, Ngày, Tổng cộng, Đã thanh toán, Còn nợ) và các phiếu thu/chi (Số phiếu, Ngày, Thu, Chi, Diễn giải), sắp theo ngày.
- Nút **Thu tiền nợ**: mở hộp **Thu nợ theo đơn** (xem T1 ở §7) cho khách đang chọn; chọn tiền mặt hoặc chuyển khoản + tài khoản, ngày, ghi chú; lưu xong tải lại. Quyền theo "Tạo phiếu thu".
- Xuất Excel qua `GridExcelExporter`.

## 4. Màn Công nợ nhà cung cấp

Giống trên: danh sách Mã, Tên, Địa chỉ, Điện thoại, Còn nợ; chi tiết là các phiếu nhập/xuất trả và các phiếu thu/chi; nút **Chi trả nợ** mở hộp Phiếu chi đặt sẵn đối tượng = NCC, số tiền = Còn nợ. Chặn chi tiền mặt vượt quỹ theo luật của Quỹ.

## 5. Quyền

Chức năng "Công nợ khách hàng" và "Công nợ nhà cung cấp" (Xem); nút Thu/Chi theo quyền phiếu thu/chi.

## 6. Nghiệm thu

1. Khách: bill 300.000 thanh toán 100.000 còn nợ 200.000; thêm phiếu thu 50.000 gắn khách thì còn 150.000; phiếu có `TDATHANGID` hoặc `KHONGTHAYDOICONGNO` tích thì không tính.
2. Khách vãng lai (không có hồ sơ) không xuất hiện.
3. NCC: phiếu nhập 10 triệu `TIENTHANHTOAN` 0, phiếu chi 4 triệu gắn NCC thì nợ 6 triệu; phiếu xuất trả 1 triệu thì nợ 5 triệu.
4. "Còn nợ" của khách khớp "Nợ cũ" trong Sử dụng dịch vụ cho cùng khách.
5. Thu nợ theo đơn: khách nợ 2 bill 200.000 và 150.000, thu 250.000 thì bill cũ hết nợ, bill sau còn 100.000; có đúng 1 phiếu thu `THU = 250000` và 2 dòng `TCHITIETTHANHTOAN`; tổng nợ khách giảm 250.000; `TIENTHANHTOAN` của bill không đổi; thu quá nợ một đơn bị chặn; lỗi giữa chừng rollback. Chi trả nợ NCC ghi phiếu chi thật (qua `VoucherService`), tải lại thấy nợ giảm; Chi trả NCC tương tự; chi tiền mặt vượt quỹ bị chặn.
6. Lọc nhóm, tìm, "chỉ còn nợ" đúng; xuất Excel đúng cột; không đổi cấu trúc CSDL; ca selftest cũ giữ tên.

## 7. Điểm TẠM (tiền, mở issue `cho-chien`)

- **T1** **Chiến chốt 27/11: thu nợ khách phân bổ theo từng đơn**; chỗ chưa rõ thì Claude khám phá trong app gốc. Mã gốc không cho thấy phiếu thu công nợ (`TTHUCHI` `LAPHIEUTHUCONGNO = 30`, `TCHITIETTHANHTOAN`) làm nợ khách giảm bằng cách nào. Cách làm bản mới (TẠM, tới khi đối chiếu app gốc): hộp **Thu nợ theo đơn** liệt kê các bill còn nợ của khách (Số phiếu, Ngày, Tổng cộng, Còn nợ, **Lần này** nhập số tiền thu cho từng đơn, mặc định phân bổ từ đơn cũ nhất tới hết số tiền); lưu ghi **một phiếu thu** `TTHUCHI` (`LOAI = 0`, `LAPHIEUTHUCONGNO = 30`, `DKHACHHANGID` = khách, `THU` = tổng, không `TDATHANGID`, không `KHONGTHAYDOICONGNO`) **kèm các dòng `TCHITIETTHANHTOAN`** (`TTHUCHIID`, `TDONHANGID`, `SOTIEN` theo từng đơn) trong một transaction. Nợ khách giảm nhờ phiếu thu (công thức §2, giống mọi báo cáo gốc); **không sửa `TIENTHANHTOAN` của bill** để khỏi trừ hai lần. Mỗi đơn không được thu quá "Còn nợ" của đơn đó. Phiếu loại này không sửa/xóa ở màn Quỹ (quản lý ở đây). Nên thêm: tạo phiếu thu thường gắn khách (không phân bổ) vẫn dùng được từ Quỹ.
- **T2** Dấu NCC theo kết luận của rà soát (phiếu chi giảm nợ).
- **T3** Xuất trả hàng (`LOAI = 2`): đảo dấu cả `TIENTHANHTOAN` (3 báo cáo gốc đảo, màn hình gốc không đảo).
- **T4** Không có phân bổ FIFO: nợ chỉ là tổng.
