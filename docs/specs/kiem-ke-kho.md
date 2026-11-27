# Spec: Kiểm kê kho (phiếu kiểm kê `TDONHANG.LOAI = 4`)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; Chiến chọn làm tiếp "Giá vốn và kiểm kê" 27/11; điểm **TẠM** ở §7).
- Ngày: 2026-11-27. Loại: **nặng** (kho). Nguồn: `docs/deep/kho-hang.md` §A.6, §B.6, E.1 (B1, B11, B12); `phieu-nhap-kho.md` (cùng khung), `ton-kho.md`.
- Menu: KHO HÀNG ▸ Kiểm kê kho (lưới quản lý, mở hộp phiếu từ đó).

## 1. Phạm vi

| Làm | Không làm |
|---|---|
| Lưới Quản lý kiểm kê (lọc ngày, kho, tìm; Thêm, Sửa, Xóa, xuất Excel), hộp phiếu kiểm kê, nhập số lượng thực tế, tính chênh lệch, ghi `SLNHAP`/`SLXUAT` để tồn kho khớp số thực tế, nút "Mặt hàng chưa kiểm kê", quyền, lưu vết | Nhập từ máy kiểm kho (`.txt`), nhập Excel, hạn sử dụng và kích thước (cột có nhưng chỉ lưu), in phiếu, phân tích kiểm kê nâng cao |

## 2. Hộp phiếu kiểm kê

- Đầu phiếu: Ngày (khóa nếu không có quyền "Thay đổi ngày trong các phiếu", admin luôn được), Số phiếu (bắt buộc, không trùng, tự sinh `KK` + yy + / + 5 chữ số, sửa tay được), **Kho kiểm kê** (bắt buộc; kho của cửa hàng người dùng, admin thấy hết; mặc định kho của cửa hàng đăng nhập; **khóa không đổi khi đã có dòng**), Diễn giải (mặc định "Kiểm kê"), Ghi chú. Không có ô tiền.
- Lưới chi tiết: thêm mặt hàng (nguyên liệu, kiêm vật tư hoặc không có loại; đang dùng; mặt hàng theo dõi tồn): cột Mã, Tên, ĐVT, **Tồn hệ thống**, **Số lượng thực tế**, **Chênh lệch** (= thực tế − hệ thống, tính theo ĐVT cơ sở), Ghi chú. Thêm cùng mặt hàng + cùng ĐVT thì cộng dồn số lượng thực tế (gộp dòng).
- Mặt hàng có 2 đơn vị tính: nhập số thực tế theo ĐVT chẵn hoặc lẻ; **quy đổi về ĐVT cơ sở** bằng `QUYDOI` trước khi so với tồn hệ thống (T1).
- **Tồn hệ thống (T2):** hiện lúc thêm dòng bằng hàm tồn chung (`ServiceOrderRepository.GetStockBalanceIn`, loại trừ chính phiếu đang sửa), và **tính lại đúng lúc lưu** trong transaction; chênh lệch ghi theo số tính lúc lưu (nếu khác số đã thấy trên màn thì báo "Tồn hệ thống đã thay đổi, hãy xem lại" và không lưu, để người dùng xác nhận).

## 3. Ghi dữ liệu khi lưu

Một transaction cho `TDONHANG`, `TDONHANGCHITIET`, lưu vết; lỗi thì rollback. `TDONHANG`: `LOAI = 4`, `STATUS = 30`, `DATHANHTOAN = 30`, `DKHONHAPID` = kho kiểm kê, người và giờ tạo/sửa, không có tiền (`TIENHANG`, `TONGCONG` bằng 0). Mỗi dòng:
- `SLHETHONG` (tồn hệ thống), `SLTHUCTE` (thực tế, ĐVT cơ sở), `DKHOHANGID` = kho kiểm kê.
- `SLXUAT = SLXUATCHUAQUYDOI = max(0, SLHETHONG − SLTHUCTE)` và `SLNHAP = SLNHAPCHUAQUYDOI = max(0, SLTHUCTE − SLHETHONG)` **đều ghi đầy đủ đã quy đổi** để tồn kho đổi đúng (app gốc có lỗi ghi thiếu khi bật 2 đơn vị tính: B1).
- Dòng chênh lệch bằng 0 vẫn ghi (để biết đã kiểm).
Chuỗi ghi vào cột có giới hạn (kể cả `Note`, `TableName` của lưu vết, 255) phải cắt cho vừa.

## 4. Sửa, xóa phiếu

- Sửa: ghi lại chi tiết trong cùng transaction; kho kiểm kê không đổi được khi phiếu đã lưu có dòng.
- Xóa: xóa cứng phiếu và chi tiết, ghi lưu vết, một transaction.
- **Chặn (T3):** sửa hoặc xóa làm tồn một mặt hàng ở kho đó thành âm mà kho không cho âm: không lưu, nêu tên mặt hàng, tồn hiện có và tồn sau khi sửa (cùng quyết định "âm kho mặc định chặn" của Chiến).

## 5. Lưới quản lý và nút "Mặt hàng chưa kiểm kê"

- Lưới: Ghi chú, Số phiếu, Ngày, **Kho kiểm kê** (thêm so với app gốc, T4), Diễn giải. Người dùng không phải admin chỉ thấy phiếu của kho thuộc cửa hàng mình. Quyền: chức năng "Kiểm kê kho" (Xem, Thêm, Sửa, Xóa). Xuất Excel qua `GridExcelExporter`.
- Nút **Mặt hàng chưa kiểm kê** (chỉ hiện khi phiếu đã lưu): liệt kê mặt hàng theo dõi tồn của kho đó có tồn khác 0 mà chưa có dòng trong phiếu: Mã, Tên, ĐVT, Tồn hệ thống; có nút xuất Excel.

## 6. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Mặt hàng tồn 10, kiểm thực tế 7: lưu thì dòng có `SLXUAT = 3`, `SLNHAP = 0`; tồn kho còn 7. Tồn 10, thực tế 12: `SLNHAP = 2`, tồn 12. Tồn 10, thực tế 10: cả hai bằng 0.
2. ĐVT chẵn: tồn 50 (lẻ), quy đổi 24, thực tế nhập 2 thùng thì quy 48, `SLXUAT = 2`, tồn 48 (đúng cả khi bật 2 đơn vị tính).
3. Gộp dòng cùng mặt hàng; thiếu Số phiếu hoặc Kho: chặn; trùng Số phiếu: báo; Số phiếu dài 255 ký tự không làm lưu vết tràn.
4. Tồn hệ thống đổi giữa lúc thêm dòng và lúc lưu: báo và không lưu; lưu lại sau khi xác nhận ghi đúng số mới.
5. Sửa phiếu: tồn tính lại đúng (loại trừ chính phiếu); sửa/xóa làm tồn âm bị chặn khi kho không cho âm, cho khi kho cho âm; đổi kho khi đã có dòng bị khóa.
6. Xóa phiếu: mất phiếu và chi tiết, tồn trở lại số trước kiểm kê, có lưu vết; lỗi giữa chừng rollback (hook).
7. Lưới: lọc ngày, kho, tìm; người không phải admin chỉ thấy kho của mình; xuất Excel đúng cột. Nút "Mặt hàng chưa kiểm kê" liệt kê đúng.
8. Giá vốn: phiếu kiểm kê tham gia tính giá vốn như dòng xuất/nhập (`TON`, `GIATRI`) khi chạy Tính lại giá vốn.
9. Menu mở màn thật; không đổi cấu trúc CSDL; ca selftest cũ giữ tên.

## 7. Điểm TẠM

- **T1** Quy đổi đúng ĐVT cơ sở ở mọi đường nhập (app gốc ghi thiếu hoặc sai khi bật 2 đơn vị tính; B1, B11).
- **T2** Tồn hệ thống tính lại lúc lưu, không chốt lúc thêm dòng (app gốc chốt lúc thêm; B12).
- **T3** Chặn sửa/xóa gây âm kho (theo quyết định âm kho mặc định chặn).
- **T4** Thêm cột Kho vào lưới quản lý (app gốc không có).
- **T5** Số phiếu `KK` + yy + / + 5 chữ số là suy đoán (như PN/PX/CK).
- **T6** Chưa làm nhập từ máy kiểm kho và Excel; chưa tính lại giá vốn sau kiểm kê (giống app gốc).
