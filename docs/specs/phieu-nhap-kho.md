# Spec: Nhập hàng vào kho (phiếu nhập `TDONHANG.LOAI = 1`) và lưới Quản lý nhập kho

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; các điểm **TẠM** ở §7 là hướng an toàn Claude chọn, Chiến xác nhận sau).
- Ngày: 2026-11-27. Loại: **nặng** (kho, tiền). Nguồn: `docs/deep/kho-hang.md` (rà soát sâu bản gốc), `docs/specs/su-dung-dich-vu.md` §3.7, spec `danh-muc-kho-hang`.
- Menu: KHO HÀNG ▸ Nhập hàng vào kho (lưới quản lý, mở hộp thêm/sửa phiếu từ đó).

## 1. Phạm vi

| Làm | Không làm |
|---|---|
| Lưới Quản lý nhập kho (lọc ngày, kho, NCC, tìm; Thêm, Sửa, Xóa, xuất Excel), hộp phiếu nhập, ghi `TDONHANG` + `TDONHANGCHITIET`, quy đổi 2 đơn vị tính, quyền, lưu vết | Tính lại giá vốn (làm sau), nhập từ Excel, in phiếu, in mã vạch theo số nhập, hạn sử dụng/kích thước bắt buộc, kiểm kê |

## 2. Hộp phiếu nhập

- Đầu phiếu: Ngày (khóa nếu không có quyền "Thay đổi ngày trong các phiếu", admin luôn được), Số phiếu (bắt buộc, không trùng, tự sinh, sửa tay được), Kho nhập (bắt buộc; chỉ kho của cửa hàng người dùng, admin thấy hết; mặc định kho của cửa hàng đăng nhập), Nhà cung cấp (bắt buộc khi cấu hình "bắt buộc chọn NCC khi nhập kho" bật; mặc định không bắt buộc), Nhân viên nhập, Diễn giải (mặc định "Nhập mua hàng"), Ghi chú.
- Lưới chi tiết: thêm mặt hàng (loại nguyên liệu, kiêm vật tư hoặc không có loại; đang dùng); cột Mã, Tên, ĐVT, Số lượng, Đơn giá (mặc định `GIANHAP`), % giảm, Thành tiền, Ghi chú. Mặt hàng có 2 đơn vị tính (`DDONVITINHCHANID` khác `DDONVITINHID`) nhập được theo ĐVT chẵn hoặc lẻ: `SLNHAP = QUYDOI × SLNHAPCHUAQUYDOI` khi nhập theo chẵn, ngược lại `SLNHAP = SLNHAPCHUAQUYDOI`.
- Thêm cùng mặt hàng + cùng đơn giá + cùng ĐVT thì gộp dòng (cộng số lượng).
- Dòng: `THANHTIEN = Round(SL × DONGIA × (1 − TILEGIAMGIA/100), 0)`. Sửa Thành tiền thì suy ngược `DONGIA` (0 khi SL = 0 hoặc giảm 100%).
- Tổng: `TIENHANG = ΣTHANHTIEN`; giảm giá nhập theo % thì `TIENGIAMGIA = 100 × Round(TIENHANG × TL / 10000)`, nhập theo tiền thì giữ; `TIENTHUE = TILETHUE × (TIENHANG − TIENGIAMGIA) / 100`; `TONGCONG = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN` (không làm tròn thêm).
- Thanh toán cho NCC (`TIENTHANHTOAN`): ô nhập tay, mặc định 0; phần còn lại là nợ NCC (xem `cong-no.md`).

## 3. Kiểm tra trước khi lưu

Số phiếu, Kho nhập bắt buộc; có ít nhất 1 dòng; mọi dòng `SLNHAP > 0` ("Số lượng hàng nhập phải lớn hơn 0"); mỗi dòng ghi `DKHOHANGID` = kho nhập; trùng Số phiếu báo "đã tồn tại". Một transaction cho `TDONHANG`, `TDONHANGCHITIET` và lưu vết; lỗi thì rollback. Ghi `TDONHANG`: `LOAI = 1`, `STATUS = 30`, `DATHANHTOAN = 30`, `DKHONHAPID`, `DNHACUNGCAPID`, người và giờ tạo/sửa. Chuỗi ghi vào cột có giới hạn (kể cả `Note`, `TableName` của lưu vết, VARCHAR 255) phải cắt cho vừa.

## 4. Sửa, xóa phiếu

- Sửa: xóa chi tiết cũ, ghi chi tiết mới trong cùng transaction; ghi người và giờ sửa.
- Xóa: xóa cứng phiếu và chi tiết, ghi lưu vết, một transaction.
- **Chặn (T3):** sửa hoặc xóa làm tồn của một mặt hàng ở kho nhập thành âm mà kho không bật "cho phép âm kho" (và cấu hình toàn hệ thống cũng không cho): không lưu, nêu tên mặt hàng, tồn hiện có và tồn sau khi sửa.

## 5. Lưới Quản lý nhập kho

Cột: Ghi chú, Số phiếu, Ngày, Tổng cộng, Phí vận chuyển, Tiền giảm giá, Tiền thuế, Tiền hàng, Nhà cung cấp, Kho nhập, Nhân viên nhập, Diễn giải; hàng tổng cộng các cột tiền. Người dùng không phải admin chỉ thấy phiếu của kho thuộc cửa hàng mình. Quyền: chức năng "Nhập kho" (Xem, Thêm, Sửa, Xóa). Cột tiền ẩn khi không có quyền xem giá nhập. Xuất Excel qua `GridExcelExporter`.

## 6. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Lưu phiếu nhập 2 mặt hàng: `TDONHANG LOAI = 1` và 2 dòng chi tiết đúng `SLNHAP`, `THANHTIEN`, `DKHOHANGID`; tồn mặt hàng tăng đúng số nhập.
2. Công thức dòng và tổng: giảm %, giảm tiền, thuế, phí vận chuyển cho ra đúng `TONGCONG` (nhiều ca).
3. ĐVT chẵn: nhập 3 thùng, quy đổi 24 thì `SLNHAP = 72`.
4. Gộp dòng trùng; SL = 0 chặn; thiếu Số phiếu, Kho: chặn; trùng Số phiếu: báo; Số phiếu dài 255 ký tự không làm lưu vết tràn.
5. Sửa phiếu đổi số lượng: tồn cập nhật đúng; sửa/xóa làm tồn âm bị chặn khi kho không cho âm, cho khi kho cho âm.
6. Xóa phiếu: mất phiếu và chi tiết, có lưu vết; lỗi giữa chừng rollback (hook).
7. Lưới: lọc ngày, kho, NCC, tìm; người không phải admin chỉ thấy kho của mình; xuất Excel đúng cột.
8. Menu mở màn thật; không đổi cấu trúc CSDL; ca selftest cũ giữ tên.

## 7. Điểm TẠM

- **T1** Số phiếu tự sinh `PN` + `yy` + `/` + 5 chữ số (chưa thấy mã gốc sinh số cho phiếu kho).
- **T2** Không tính lại giá vốn sau khi nhập (bản gốc có khi bật "tự động tính giá vốn"); làm ở chức năng Tính lại giá vốn.
- **T3** Chặn sửa/xóa gây âm kho, theo quyết định "âm kho mặc định chặn" của Chiến 26/11.
- **T4** `TIENTHANHTOAN` mặc định 0 và nhập tay: công nợ NCC lấy từ đây.
