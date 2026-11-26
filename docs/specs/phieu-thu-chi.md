# Spec: Phiếu thu, Phiếu chi (ghi thật), danh mục phiếu, lý do thu chi, tài khoản ngân hàng

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 26/11/2026 theo workflow-agy §10.1.1, vì nghiệp vụ tiền đã có nguồn chuẩn ở `quy-tien.md` đã duyệt và chốt giống app gốc; các điểm **TẠM** ở §5 Claude chọn hướng an toàn nhất, liệt kê ở issue `ra-soat`).
- Ngày: 2026-11-26. Loại: **nặng** (ghi tiền). Phụ thuộc: `quy-tien.md` (nguồn chuẩn quỹ), `pilot-phieu-thu.md` (luật hộp "Phiếu thu"), `nhap-so/index.md` (ô số), `khach-dat-hang.md` (cọc đặt trước dùng `TTHUCHI.TDATHANGID`).
- Menu: Quỹ ▸ Tạo phiếu thu, Tạo phiếu chi, Danh mục phiếu thu, Danh mục phiếu chi, Danh mục lý do thu chi, Danh mục tài khoản ngân hàng.

## 1. Phạm vi

| Màn | Làm |
|---|---|
| Tạo phiếu thu / Tạo phiếu chi | Hộp thoại `PhieuThuDialog` hiện chỉ kiểm tra rồi báo "bản thử". **Làm ghi thật** vào `TTHUCHI` (`LOAI = 0` thu, `LOAI = 1` chi), dùng chung một hộp, đảo nhãn/luật theo loại. |
| Danh mục phiếu thu, Danh mục phiếu chi | Lưới danh sách `TTHUCHI` theo loại (lọc ngày, cửa hàng, tìm), mở hộp sửa, xóa, xuất Excel (`GridExcelExporter`). |
| Danh mục lý do thu chi (`DLYDOTHUCHI`), Danh mục tài khoản ngân hàng (`DTAIKHOANNGANHANG`) | Thêm/sửa/xóa trên khung `CatalogView` (cùng cách Danh mục khách hàng), kiểm trùng Tên, xóa chỉ khi không còn tham chiếu. |
| **Không làm** | Phiếu thu công nợ (`LAPHIEUTHUCONGNO`, thuộc Công nợ), nạp thẻ trả trước (`DTHETRATRUOCID`), tạm ứng lương (`TBANGLUONGID`), chuyển quỹ/số dư đầu kỳ (`quy-tien.md` bước 4), in phiếu (làm sau cùng cách in P2). |

## 2. Luật (theo `pilot-phieu-thu.md` §3 và `quy-tien.md`)

- Trường: Số phiếu (bắt buộc), Ngày, Cửa hàng (bắt buộc), Lý do (`DLYDOTHUCHI`; thu chỉ lấy `LALYDOTHU = 30`, chi lấy `LALYDOTHU <> 30`), Loại đối tượng (0 khác, 1 nhân viên, 2 khách hàng, 3 nhà cung cấp) với các ô bật/tắt đúng `pilot-phieu-thu.md` §3, Số tiền (`THU` cho phiếu thu, `CHI` cho phiếu chi; > 0, decimal, `NUMERIC(18,2)`), Diễn giải, Chứng từ gốc, Ghi chú, tích "Chuyển vào/ra tài khoản" (`CHUYENKHOAN = 30`) kèm Tài khoản ngân hàng bắt buộc khi tích.
- **Quỹ nào đổi:** theo `quy-tien.md` §4.2 (tiền mặt khi `CHUYENKHOAN = 0`; quỹ tài khoản `DTAIKHOANNGANHANGID` khi `CHUYENKHOAN = 30`). Dùng lại `FundRules`/`FundRepository`; không tự tính lại ở lớp mới.
- Chi vượt quỹ (số dư tiền mặt sau chi < 0): theo `quy-tien.md` (xem §5 điểm T3).
- Số phiếu tự sinh khi mở phiếu mới: `PT` + `yy` + `/` + 5 chữ số tăng dần cho thu, `PC` + `yy` + `/` + 5 chữ số cho chi (mẫu dữ liệu `PT20/00001`); cách làm giống `BookingRepository.GenerateNumber` (lớn nhất hiện có + 1, kiểm trùng khi lưu, báo "đã tồn tại" như màn khác). Người dùng được sửa tay.
- Quyền: tên chức năng và quyền Thêm/Sửa/Xóa theo `SMENU`/`SGROUPROLE` của menu tương ứng (Tạo phiếu, Danh mục phiếu); ngày bị khóa khi không có quyền "Thay đổi ngày trong phiếu thu chi" (admin luôn được).
- Sửa phiếu đã lưu: ghi `USERMODIFIEDID`, `TIMEMODIFIED`; **phiếu có `TDATHANGID` (cọc đặt trước), `TDONHANGID` (thanh toán bán hàng), `TBANGLUONGID`, `LAPHIEUTHUCONGNO` hoặc `LATAMUNG` không được sửa/xóa ở đây** (quản lý ở màn gốc của chúng; báo một câu rõ).
- Xóa: xóa cứng sau khi chắc không còn tham chiếu và ghi lưu vết (`TLUUVET`, cùng cách các màn khác); phiếu thuộc quỹ đã khóa kỳ: không có khái niệm khóa kỳ trong v1.
- Một transaction cho mọi ghi; lỗi thì rollback.

## 3. Danh mục lý do thu chi, tài khoản ngân hàng

- `DLYDOTHUCHI` có `LALYDOTHU` (30 = lý do thu, 0 = lý do chi), `LOAILYDO` (loại dành riêng, xem `tong-hop-ket-qua-kinh-doanh.md`: 1 định phí, 2 chi khác, 99 không tính; **không cho sửa `LOAILYDO` ở màn này nếu dòng là lý do hệ thống** của `quy-tien.md` bước 4). Thêm: Tên (bắt buộc, không trùng, so UNICODE_CI_AI), Loại (thu/chi), Ghi chú.
- `DTAIKHOANNGANHANG`: Tên (bắt buộc, không trùng), Ghi chú. Xóa chỉ khi không còn dòng `TTHUCHI`, `TDONHANG`, `TDATHANG`... tham chiếu `DTAIKHOANNGANHANGID` (kiểm tất cả bảng có cột này; không lọc `STATUS`).
- Xóa lý do: chỉ khi không còn `TTHUCHI.DLYDOTHUCHIID` tham chiếu và không phải lý do hệ thống.

## 4. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Lưu phiếu thu tiền mặt 100.000: `TTHUCHI` có 1 dòng `LOAI = 0`, `THU = 100000`, `CHUYENKHOAN = 0`; Tồn quỹ tiền mặt tăng đúng 100.000.
2. Lưu phiếu chi chuyển khoản 40.000 qua tài khoản A: `CHI = 40000`, `CHUYENKHOAN = 30`, quỹ tài khoản A giảm 40.000, tiền mặt không đổi.
3. Tích chuyển khoản mà chưa chọn tài khoản: không lưu, báo "Mời bạn chọn tài khoản ngân hàng".
4. Số tiền <= 0, thiếu Số phiếu hoặc Cửa hàng: báo, không lưu.
5. Số phiếu tự sinh tăng đúng (`PT26/00001` rồi `PT26/00002`; `PC26/00001`), trùng thì báo.
6. Sửa phiếu: giữ `USERCREATEDID`/`TIMECREATED`, cập nhật `USERMODIFIEDID`/`TIMEMODIFIED`, Tồn quỹ cập nhật theo số mới.
7. Phiếu có `TDATHANGID` hoặc `TDONHANGID`: sửa/xóa bị từ chối bằng một câu rõ, không đổi DB.
8. Xóa phiếu thường: dòng mất, Tồn quỹ giảm đúng; có lưu vết.
9. Danh mục phiếu: lọc ngày/cửa hàng/tìm đúng, xuất Excel đúng cột đang hiện.
10. Lý do/tài khoản: thêm, trùng Tên bị chặn, xóa khi còn tham chiếu bị chặn và nêu số tham chiếu, xóa khi sạch được.
11. Menu mở màn thật (không còn "sẽ được bổ sung"); ẩn menu khi không có quyền Xem; Ctrl+S/Ctrl+L như các hộp khác.
12. Không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 5. Điểm TẠM (Claude chọn hướng an toàn, Chiến xác nhận sau)

- **T1. Định dạng số phiếu** `PT yy / 00000` suy từ một dòng dữ liệu mẫu (`PT20/00001`) và cách `GenerateNumber` các màn khác; chưa thấy mã gốc sinh số cho `TTHUCHI`. Nếu lệch thì chỉ đổi hàm sinh.
- **T2. Phiếu có `TDONHANGID`/`TDATHANGID` không sửa/xóa ở màn này** (an toàn: không phá liên kết bill/cọc). App gốc có thể cho sửa; chưa kiểm.
- **T3. Chi tiền mặt làm quỹ âm:** mặc định **chặn** (cùng tinh thần chốt 26/11 cho âm kho: chặn mặc định), nêu số dư hiện có; cấu hình toàn hệ thống cho phép sẽ thêm sau. App gốc có chặn hay không chưa kiểm.
- **T4. Xóa cứng** (theo luật dự án "xóa cứng cả app") kèm lưu vết.
