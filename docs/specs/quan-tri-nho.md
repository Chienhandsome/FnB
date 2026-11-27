# Spec: Mục nhỏ còn lại (Thông báo sửa chữa, Danh mục mật khẩu wifi, Tồn nhiều kho)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 28/11/2026 theo workflow-agy §10.1.1; Chiến dặn làm liên tục đến 7h sáng 28/11, các mục này nằm trong danh sách "làm sau" đã giao). Điểm **TẠM** ở §5.
- Ngày: 2026-11-28. Loại: **nhẹ** (danh mục thông tin; không đổi tiền, kho, công nợ). Nguồn: `docs/deep/hoat-dong-ban-hang.md` mục 3.1 và Q38; `docs/deep/kho-hang.md` Q22; `docs/deep/khach-hang.md` (mật khẩu wifi trên hóa đơn); `POS_TAP_SRC_EXPORT/STABLEDESC/TSUACHUA__296ae39d`, `DMATKHAUWIFI__58d3982f`; bảng `SCOLUMN` trên DEMO.FDB.
- Chia **hai patch**: (P-A) Thông báo sửa chữa + Danh mục mật khẩu wifi; (P-B) Tồn nhiều kho.
- Menu: `SMENU` "Thông báo sửa chữa" (`ZZZ009`, bảng `TSUACHUA`), "Danh mục mật khẩu wifi" (`ZZZ007`, bảng `DMATKHAUWIFI`), "Tồn nhiều kho" (`ZZZ012`). Nhận diện theo tên không dấu, không phân biệt hoa thường; quyền theo tên chức năng "Thông báo sửa chữa", "Danh mục mật khẩu wifi" (Xem, Thêm, Sửa, Xóa; admin luôn được) và "Tồn kho" (Xem) cho Tồn nhiều kho.

## 1. Khung chung (P-A)

Dùng khung `CatalogView` như Danh mục kho hàng, nhà cung cấp, cửa hàng, nhân viên (đọc `Store*`, `Employee*`, `CatalogView.Employee`): lưới, hộp thêm/sửa, Xóa, tìm không dấu, xuất Excel (`GridExcelExporter`), lưu vết mỗi thêm/sửa/xóa (chuỗi ghi vào cột có giới hạn, kể cả `Note` và `TableName`, cắt cho vừa 255), dòng mới `ID` GUID, `STATUS = 30`, người và giờ tạo/sửa. Không đổi cấu trúc CSDL. Chỉ có quyền Xem: hộp thoại chỉ đọc.

## 2. Thông báo sửa chữa (`TSUACHUA`)

Lưới: **Số phiếu** (`NAME`), **Ngày**, **Phòng/Bàn** (`DBANID` → `DBAN.NAME`), **Loại phòng** (`DLOAIPHONGID` → `DLOAIPHONG.NAME`), **Nhân viên** (`DNHANVIENID`), **Nội dung** (`NOIDUNG`), **Đã sửa xong** (Có/Không), **Còn sử dụng được** (Có/Không), Ghi chú. Sắp: chưa sửa xong trước, rồi ngày giảm dần. Bộ lọc: tích "Chỉ chưa sửa xong" (mặc định tắt), **Từ ngày**, **Đến ngày** (để trống = tất cả), ô tìm không dấu (số phiếu, bàn, nhân viên, nội dung, ghi chú).

Hộp thêm/sửa (thứ tự trường như gốc):
- **Số phiếu** (`NAME`, bắt buộc, ≤ 255, **không trùng** UNICODE_CI_AI: `SCOLUMN` ghi `ALLOWDUPLICATE = NO`): tự sinh `SC` + yy + `/` + 5 chữ số tăng dần (lớn nhất hiện có cùng tiền tố + 1; sửa tay được) (T1).
- **Ngày** (`NGAY`, mặc định hôm nay; khóa khi không có quyền "Thay đổi ngày trong các phiếu", admin được).
- **Phòng/Bàn** (`DBANID`, bắt buộc, chọn từ `DBAN` `STATUS = 30`; gốc không bắt buộc nhưng phiếu không có bàn thì không ảnh hưởng gì, T2). Khi chọn bàn, **Loại phòng** (`DLOAIPHONGID`) tự điền theo `DBAN.DLOAIPHONGID` (sửa được; chọn từ `DLOAIPHONG` đang dùng, tùy chọn).
- **Nhân viên** (`DNHANVIENID`, tùy chọn, chọn từ `DNHANVIEN` `STATUS = 30`).
- **Nội dung** (`NOIDUNG`, bắt buộc, ≤ 255) (T3: gốc không bắt buộc).
- **Đã sửa xong** (`DASUAXONG`: 30 khi tích, 0 khi không), **Còn sử dụng được** (`CONSUDUNGDUOC`: 30 khi tích, 0 khi không; mặc định tích khi tạo mới) — hai ô tích độc lập như gốc.
- Ghi chú (`NOTE`, ≤ 255).
- **Hiệu lực lên bàn (đã có sẵn trong code):** bàn có phiếu `DASUAXONG = 0` hiển thị "Sửa chữa" ở sơ đồ bàn (`ServiceOrderRepository`, `OrderCalculator` trạng thái bàn); tích "Đã sửa xong" thì bàn trở lại trạng thái bình thường. Spec này **không đổi** cách tính trạng thái bàn, chỉ cho khai báo/đóng phiếu; selftest kiểm bằng gọi đúng hàm trạng thái bàn đã có.
- Xóa: xóa cứng sau xác nhận, có lưu vết (không bảng nào tham chiếu `TSUACHUA`).

## 3. Danh mục mật khẩu wifi (`DMATKHAUWIFI`, nhóm `DNHOMWIFI`)

`CatalogView` có cây nhóm bên trái (`DNHOMWIFI`: cột cây chuẩn `PARENTID`, `SORTORDER`, `PARENTDIR`, `ITEMTYPE`, `AUTOID`; nhóm gốc "Tất cả"; chọn nhóm cha thì hiện cả mật khẩu của nhóm con), lưới bên phải: **Mật khẩu wifi** (`NAME`), **Nhóm wifi** (`DNHOMWIFIID`), Ghi chú. Cách làm giống Danh mục voucher (cây nhóm + lưới; đọc `Voucher*`, `CatalogView.Voucher`).
- Hộp mật khẩu: **Wifi** (`NAME`, bắt buộc, ≤ 255, **không trùng** UNICODE_CI_AI trên toàn bảng), **Nhóm wifi** (chọn, tùy chọn), Ghi chú. Hộp nhóm: Tên (bắt buộc, không trùng trong cùng cha), Ghi chú, thêm nhóm con.
- Xóa mật khẩu: xóa cứng sau xác nhận (có lưu vết). Xóa nhóm: chặn khi còn mật khẩu hoặc nhóm con ("nhóm còn N mật khẩu").
- Mật khẩu hiển thị rõ trong lưới (như gốc: mật khẩu được in trên hóa đơn). **Không làm** việc gán mật khẩu wifi vào hóa đơn khi thanh toán và xóa mật khẩu đã dùng (gốc `GetPasswordWifi`: lấy một mật khẩu rồi xóa khỏi bảng), để patch in hóa đơn làm sau (T4).

## 4. Tồn nhiều kho (P-B)

Menu "Tồn nhiều kho" mở **màn Tồn kho** (`InventoryView`, `docs/specs/ton-kho.md`) ở chế độ **tất cả các kho**: bộ lọc Kho mặc định là mục mới "Tất cả các kho" (tổng tồn của mọi kho mà người dùng được xem: admin mọi kho, người khác chỉ kho thuộc cửa hàng của mình), vẫn cho chọn một kho cụ thể. Lưới như Tồn kho (cùng cột, cùng công thức tồn chung `ServiceOrderRepository.GetStockBalanceIn` hoặc truy vấn tương đương đã dùng ở Tồn kho; **một công thức duy nhất**), kèm **bảng chi tiết theo kho** ở dưới lưới cho mặt hàng đang chọn: **Kho**, **Tồn**, **Giá trị vốn** (ẩn giá khi không có quyền xem giá nhập). Ở chế độ "Tất cả các kho" không có thêm tab tồn theo kích thước/hạn dùng (như gốc `TatCaCacKho`). Màn "Tồn kho" thường **không đổi** (mặc định vẫn kho của cửa hàng đăng nhập; không thêm mục "Tất cả" vào màn đó, T5). Xuất Excel đúng cột đang hiện (lưới chính).

## 5. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

**P-A, Thông báo sửa chữa:**
1. Thêm phiếu hợp lệ cho bàn B (Loại phòng tự điền theo bàn): có dòng, `STATUS = 30`, `DASUAXONG = 0`, `CONSUDUNGDUOC = 30`, người và giờ tạo; số phiếu tự sinh `SC26/00001` rồi `SC26/00002`; bàn B có trạng thái "Sửa chữa" theo hàm trạng thái bàn đã có; sửa thành "Đã sửa xong": bàn B hết "Sửa chữa".
2. Chặn thiếu Số phiếu, trùng Số phiếu (khác hoa thường/dấu), thiếu Bàn, thiếu Nội dung, Nội dung/Ghi chú 256 ký tự; chuỗi 255 ký tự trong lưu vết không tràn; sửa chính nó không báo trùng.
3. Lọc "Chỉ chưa sửa xong", Từ/Đến ngày, tìm không dấu đúng; xuất Excel đúng cột; ngày bị khóa khi không có quyền; xóa có lưu vết; chỉ Xem thì hộp chỉ đọc và nút không dùng được; menu mở đúng màn.

**P-A, Mật khẩu wifi:**
4. Thêm mật khẩu hợp lệ, chặn trống và trùng (khác hoa thường/dấu); cây nhóm: thêm nhóm cha và con, chọn cha hiện mật khẩu của cả con; xóa nhóm còn mật khẩu hoặc còn con chặn; xóa nhóm trống và xóa mật khẩu được, có lưu vết; quyền, tìm, xuất Excel, menu.
5. Không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

**P-B, Tồn nhiều kho:**
6. Hai kho K1, K2 (cùng cửa hàng): mặt hàng X tồn 10 ở K1 và 5 ở K2 (qua phiếu nhập `LOAI = 1`): "Tất cả các kho" hiện tồn 15; chọn K1 hiện 10; bảng chi tiết theo kho của X hiện K1 = 10, K2 = 5 và giá trị vốn theo giá vốn mặt hàng; mặt hàng tồn 0 ở mọi kho không hiện khi tích "Ẩn hàng tồn 0".
7. Người không phải admin chỉ thấy kho thuộc cửa hàng mình: tồn kho của cửa hàng khác không cộng vào tổng; không có quyền xem giá nhập thì ẩn cột giá và giá trị vốn ở cả bảng chi tiết; màn Tồn kho thường vẫn đúng như cũ (không có mục "Tất cả các kho"); menu "Tồn nhiều kho" mở đúng chế độ; không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 6. Giao diện

Như các danh mục đã có: hộp thoại gọn, không cuộn dọc ở 1366×768, tiêu đề cột đủ rộng không cắt chữ, màu qua `ThemeManager`, cảm ứng được.

## 7. Điểm TẠM

- **T1** Số phiếu sửa chữa `SC` + yy/00000 là suy đoán (gốc sinh số trong khung No1Lib).
- **T2** Bắt buộc chọn Bàn là thêm so với gốc.
- **T3** Bắt buộc Nội dung là thêm so với gốc.
- **T4** Chưa làm việc gán mật khẩu wifi lên hóa đơn và xóa mật khẩu đã dùng (làm cùng patch in hóa đơn).
- **T5** Màn Tồn kho thường không có mục "Tất cả các kho"; chỉ Tồn nhiều kho có.
- **T6** Chưa có ảnh app gốc để đối chiếu bố cục lưới (dùng mặc định dự án).
