# Spec: Số dư đầu kỳ (Tồn kho ban đầu, Công nợ khách hàng ban đầu, Công nợ nhà cung cấp ban đầu)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; Chiến chọn làm tiếp theo thứ tự đề xuất 27/11). Điểm **TẠM** ở §6.
- Ngày: 2026-11-27. Loại: **nặng** (số dư đầu kỳ quyết định tồn, giá vốn, công nợ). Nguồn: `docs/deep/kho-hang.md` §A.7, B.1 (Tồn kho ban đầu, `LOAI = 97`); `docs/deep/cong-no-quy.md` mục 15 (Công nợ ban đầu, `LOAI = 99` khách và `98` nhà cung cấp); `phieu-nhap-kho.md`, `tinh-lai-gia-von.md`, `cong-no.md`.
- Menu: **QUẢN TRỊ** hoặc nhóm đang chứa chúng trong `SMENU`: "Tồn kho ban đầu", "Công nợ khách hàng ban đâu" (bản gốc viết thiếu dấu "đầu"), "Công nợ nhà cung cấp ban đầu". Nhận diện menu theo tên không dấu, không phân biệt hoa thường.
- Chia **hai patch**: (P1) Tồn kho ban đầu; (P2) Công nợ ban đầu của khách và của nhà cung cấp (một khung, hai chế độ).

## 1. Phạm vi

| Làm | Không làm |
|---|---|
| P1: nhập tồn đầu và giá vốn theo từng kho, ghi phiếu `LOAI = 97`. P2: nhập nợ đầu kỳ theo từng khách hoặc nhà cung cấp, ghi phiếu `LOAI = 99` hoặc `98`. Xuất Excel, quyền, lưu vết | Nhập từ Excel và file mẫu (làm cùng nhập Excel P5), nhập dữ liệu từ hệ thống cũ, in |

## 2. P1: Tồn kho ban đầu

- **Màn:** chọn **Kho** (kho của cửa hàng người dùng, admin thấy hết; mặc định kho của cửa hàng đăng nhập), **Ngày** (mặc định 31/12 năm trước; không có quyền "Thay đổi ngày trong các phiếu" thì khóa), ô Tìm không dấu, tích "Chỉ hiện hàng đã nhập tồn". Lưới: mọi mặt hàng đang dùng loại kiêm vật tư hoặc nguyên liệu hoặc không có loại (như `ton-kho.md`), kể cả hàng chưa có tồn đầu: **Mã**, **Tên**, **ĐVT**, **Tồn đầu** (nhập, ≥ 0), **Giá vốn** (đơn giá, nhập, ≥ 0; mặc định `DMATHANG.GIAVON` hiện có khi chưa có dòng tồn đầu), **Thành tiền** (= Tồn đầu × Giá vốn, làm tròn 0), **Tổng giá trị** ở hàng tổng cộng. Mặt hàng có 2 ĐVT: nhập theo ĐVT cơ sở (v1).
- **Mỗi kho tối đa một phiếu `LOAI = 97`.** Nếu DB có nhiều phiếu 97 cho cùng kho thì dùng phiếu đầu theo ngày và báo cảnh báo (gốc lấy dòng đầu không xử lý trùng).
- **Lưu (một transaction):** ghi/cập nhật phiếu `TDONHANG`: `LOAI = 97`, `NAME = 'TONDAU'`, `NGAY`, `DKHONHAPID` = kho, `STATUS = 30`, `DATHANHTOAN = 30`, `TONGCONG = TIENHANG = Σ thành tiền`, `TILETHUE = 0`, người/giờ tạo hoặc sửa. Dòng chi tiết: Tồn đầu > 0 thì thêm hoặc cập nhật (`SLNHAP = SLNHAPCHUAQUYDOI = Tồn đầu`, `DONGIA = Giá vốn`, `THANHTIEN`, `TILEGIAMGIA = 0`, `DKHOHANGID` = kho); Tồn đầu = 0 mà đang có dòng thì xóa dòng; Tồn đầu < 0: **chặn** có báo rõ tên mặt hàng (gốc im lặng bỏ qua, T1). Không còn dòng nào thì xóa cả phiếu. Chuỗi ghi vào cột có giới hạn (kể cả `Note`, `TableName` của lưu vết, 255) cắt cho vừa.
- **Chặn gây âm kho (T2):** nếu sửa tồn đầu (giảm) làm tồn hiện tại của một mặt hàng ở kho thành âm mà kho không cho phép âm kho: không lưu, nêu tên mặt hàng, tồn hiện có và tồn sau khi sửa (cùng quyết định âm kho mặc định chặn).
- **Sau khi lưu:** nếu cấu hình `TuDongTinhGiaVon` bật thì tự tính lại giá vốn bằng `CostRecalcService.RunAutoAfterPurchase` (đã có, lỗi chỉ cảnh báo, không làm hỏng phiếu); phiếu 97 vốn đã tham gia tính giá vốn như phiếu nhập.
- Quyền: chức năng "Tồn kho ban đầu" (Xem, Sửa; gốc không kiểm quyền, T3); nút Lưu cần quyền Sửa, admin luôn được. Xuất Excel qua `GridExcelExporter` (đúng cột đang hiện).

## 3. P2: Công nợ ban đầu (khách hàng và nhà cung cấp)

- **Màn:** một khung dùng cho hai menu: chế độ **Khách hàng** (`DKHACHHANG`, đang dùng) và chế độ **Nhà cung cấp** (`DNHACUNGCAP`, đang dùng); **Ngày** (mặc định 31/12 năm trước, khóa như trên), ô Tìm không dấu, tích "Chỉ hiện đối tác có nợ đầu". Lưới: **Mã**, **Tên**, **Địa chỉ**, **Điện thoại**, **Nợ đầu kỳ** (nhập, ≥ 0; số dương là khách còn nợ mình hoặc mình còn nợ nhà cung cấp, cùng dấu với `cong-no.md` §2); hàng tổng cộng.
- **Mỗi đối tác tối đa một phiếu** nợ đầu: `LOAI = 99` (khách, `DKHACHHANGID`) hoặc `LOAI = 98` (nhà cung cấp, `DNHACUNGCAPID`), `NAME = 'CONGNODAU'`, `DATHANHTOAN = 30`, `STATUS = 30`, `TONGCONG = TIENHANG = số nợ`, `TIENTHANHTOAN = 0`, `TILETHUE = 0`, `NGAY`, người/giờ tạo hoặc sửa.
- **Lưu (một transaction cho mọi dòng đã đổi):** nợ > 0: tạo hoặc cập nhật phiếu; nợ = 0 mà có phiếu: xóa phiếu; nợ < 0: **chặn** nêu tên đối tác (gốc im lặng bỏ qua, T1). **Không cho giảm hoặc xóa nợ đầu của khách xuống thấp hơn số đã thu theo đơn** (tổng `TCHITIETTHANHTOAN.SOTIEN` đã phân bổ vào phiếu nợ đầu đó): "Nợ đầu của X không thể thấp hơn số đã thu (Y)". Ghi lưu vết mỗi đối tác thay đổi.
- **Nhất quán với Công nợ:** phiếu 99/98 đã nằm trong công thức nợ của `cong-no.md` §2 (`LOAI IN (0, 99)` khách, `LOAI IN (1, 2, 98)` nhà cung cấp); màn Công nợ khách hàng phải liệt kê phiếu nợ đầu trong danh sách "đơn còn nợ" để **Thu nợ theo đơn** phân bổ được vào nợ đầu kỳ (kiểm lại hàm `LoadOpenCustomerBills`/chi tiết công nợ, sửa nếu đang bỏ qua `LOAI = 99`).
- Quyền: "Công nợ khách hàng ban đầu" và "Công nợ nhà cung cấp ban đầu" (Xem, Sửa; admin luôn được).

## 4. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

**P1:**
1. Lưu tồn đầu: 2 mặt hàng (10 cái giá vốn 10.000; 5 cái giá vốn 20.000): một phiếu `LOAI = 97`, `NAME = 'TONDAU'`, `TONGCONG = TIENHANG = 200.000`, đúng 2 dòng (`SLNHAP`, `DONGIA`, `THANHTIEN`, `DKHOHANGID`); tồn kho của hai mặt hàng đúng (màn Tồn kho và hàm tồn chung).
2. Sửa: đổi số lượng, đưa một mặt hàng về 0 thì dòng bị xóa; hết dòng thì phiếu bị xóa; chỉ một phiếu cho mỗi kho kể cả lưu nhiều lần.
3. Tồn đầu âm bị chặn nêu tên hàng; giảm tồn đầu làm tồn thành âm bị chặn khi kho không cho âm, cho khi kho cho âm.
4. Phiếu 97 tham gia Tính lại giá vốn như phiếu nhập (giá vốn tháng đầu = giá trị / số lượng); bật `TuDongTinhGiaVon` thì sau lưu giá vốn đổi, tắt thì không; lỗi tính tự động không làm mất phiếu.
5. Lỗi giữa chừng (hook) rollback; chuỗi 255 ký tự không làm lưu vết tràn; ngày bị khóa khi không có quyền đổi ngày; chỉ Xem thì không lưu; xuất Excel; menu mở màn thật.

**P2:**
6. Khách: nợ đầu 500.000 cho khách A, 0 cho khách B (đã có phiếu thì xóa), âm bị chặn; công thức Công nợ khách hàng của A bằng 500.000 + các phát sinh khác; nhà cung cấp tương tự với `LOAI = 98` và công thức Công nợ nhà cung cấp.
7. Phiếu nợ đầu xuất hiện trong Thu nợ theo đơn: thu 200.000 vào nợ đầu thì nợ của khách còn 300.000; giảm nợ đầu xuống 100.000 bị chặn vì đã thu 200.000; nâng lên 600.000 được.
8. Mỗi đối tác một phiếu kể cả lưu nhiều lần; một transaction cho nhiều đối tác (hook lỗi thì không đối tác nào đổi); lưu vết đúng; chỉ Xem thì không lưu; menu mở đúng chế độ khách hoặc nhà cung cấp theo tên (kể cả tên "ban đâu" thiếu dấu); xuất Excel.
9. Không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 5. Giao diện

Khung như các màn kho (Tồn kho, Công nợ): bộ lọc trên, lưới có ô nhập số ngay trên lưới (dùng ô nhập số chung của dự án, Enter chuyển ô), hàng tổng cộng đậm, nút Lưu, Xuất Excel; tiêu đề cột đủ rộng; cảnh báo khi đóng màn còn thay đổi chưa lưu; màu qua `ThemeManager`; hỗ trợ cảm ứng.

## 6. Điểm TẠM

- **T1** Số âm bị chặn có báo (gốc im lặng bỏ qua).
- **T2** Chặn giảm tồn đầu gây âm kho (thêm so với gốc).
- **T3** Quyền "Tồn kho ban đầu", hai quyền Công nợ ban đầu: gốc không kiểm trong code; bản mới dùng quyền theo tên chức năng và cho admin luôn được.
- **T4** Chặn giảm nợ đầu dưới số đã thu theo đơn là thêm so với gốc.
- **T5** Nhập Excel và file mẫu chưa làm (thuộc P5).
