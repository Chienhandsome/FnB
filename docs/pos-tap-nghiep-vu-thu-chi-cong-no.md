# POS TAP — nghiệp vụ thu chi, công nợ, tồn quỹ

Nguồn: SFORM `Phiếu thu__6a447203` (class `TTHUCHI0Ae`), `Phiếu chi__9f094553` (`TTHUCHI1Ae`), `Phiếu thu công nợ__f34aa294` (`TTHUCHI2Ae`), `Công nợ ban đầu__34d5a00b`, `Tồn quỹ__4accbf7e` (`TonQuyHandler`). Các form `Danh mục phiếu thu/chi`, `Công nợ khách hàng`, `Công nợ nhà cung cấp` chỉ là danh sách / layout. Công thức công nợ nằm ở báo cáo (xem file báo cáo khi đã đọc).

## 1. Bảng `TTHUCHI` (phiếu thu / chi / thu công nợ)

- `LOAI`: `0` = phiếu thu, `1` = phiếu chi, `2` = phiếu thu công nợ (có cờ `LAPHIEUTHUCONGNO = 30`). Số tiền ở cột `THU` (phiếu thu / thu công nợ) hoặc `CHI` (phiếu chi); các nơi khác dùng `SOTIEN` cho dòng chi tiết thu công nợ.
- Đối tượng (`LOAIDOITUONG` theo combo): 0 = khác (nhập tay `TENDOITUONG`, `DIACHI`), 1 = nhân viên (`DNHANVIENID`), 2 = khách hàng (`DKHACHHANGID`), 3 = nhà cung cấp (`DNHACUNGCAPID`). Chọn đối tượng thì tự điền tên và địa chỉ (nhân viên không có địa chỉ).
- Lý do thu / chi: bảng `DLYDOTHUCHI`, cột `LALYDOTHU = 30` là lý do **thu**, `0` là lý do **chi** (form tự lọc theo loại phiếu). Có lý do mẫu tên "Thu tạm ứng".
- Hình thức: tiền mặt hoặc chuyển khoản (`CHUYENKHOAN = 30` kèm `DTAIKHOANNGANHANGID`, bắt buộc chọn tài khoản khi tick chuyển khoản; phiếu chi tạm ứng lương ẩn mục chuyển khoản).
- Cờ `KHONGTHAYDOICONGNO`: phiếu không làm đổi công nợ của đối tượng. Phiếu "khách nạp thẻ" (nạp thẻ trả trước) mặc định tick cờ này.
- Cửa hàng `DCUAHANGID` mặc định `Shared.DCUAHANGID`; người không phải admin chỉ chọn cửa hàng được gán (`TNGUOIDUNGTHEOCUAHANG`). Ngày chỉ sửa được nếu có quyền `ThayDoiNgayTrongPhieuThuChi`.
- **Tạm ứng đơn hàng** (`SetTamUng`): gắn `TDONHANGID`, diễn giải "Tạm ứng cho bàn/phòng <tên>", lý do lấy theo phiếu tạm ứng gần nhất hoặc "Thu tạm ứng", tự tick `KHONGTHAYDOICONGNO` và khóa. Tiền này được trừ vào bill khi thanh toán (mục "đặt trước" ở các file nhà hàng).
- **Chi lương** (`SetLuong(nhân viên, số tiền, bảng lương)`): diễn giải "Chi tiền lương tháng M năm Y", `LOAIDOITUONG = 1`, ghi `TBANGLUONGID`, lý do lấy theo lần chi lương gần nhất. `LATAMUNG = 30` (tạm ứng lương): đối tượng khóa là nhân viên. `SetGioiHanNgay(tháng, năm)`: ngày phiếu bắt buộc nằm trong tháng của bảng lương.
- Truyền giá trị từ nơi gọi: `TONGCONGNO` → `THU` (hoặc `CHI`) để lập phiếu thu/chi đúng bằng tổng công nợ; `DKHACHHANGID` → loại đối tượng khách; `DNHACUNGCAPID` → loại nhà cung cấp.
- Trả lại tiền cọc dư từ bill (nhà hàng): phiếu chi "Trả lại tiền đặt trước hóa đơn số …", `KHONGTHAYDOICONGNO` tick.

## 2. Phiếu thu công nợ (`TTHUCHI2Ae`)

- Gồm phiếu `LOAI=2` và lưới chi tiết: mỗi dòng là một hóa đơn (`TDONHANGID`, tên, ngày, tổng cộng) với `SOTIEN = LANNAY` (số thu lần này). `THU = Σ SOTIEN`, diễn giải "Thu công nợ", `LOAIDOITUONG = 1` (như mã gốc ghi), `LAPHIEUTHUCONGNO = 30`, cửa hàng mặc định.
- Dữ liệu dòng đến từ màn hình công nợ khách hàng (theo đơn) qua `DETAILDATA`.

## 3. Công nợ ban đầu (`CongNoBanDau`)

- Hai biến thể: khách hàng và nhà cung cấp (nhận biết theo menu gọi: id menu `3771b8bf-e417-4162-850a-0394867a9bee` = công nợ nhà cung cấp).
- Lưu thành **một phiếu `TDONHANG` cho mỗi đối tác**: `LOAI = 99` (khách) hoặc `98` (nhà cung cấp), `NAME = 'CONGNODAU'`, diễn giải "Công nợ ban đầu khi dùng phần mềm", `DATHANHTOAN = 30`, `TONGCONG = TIENHANG = số tiền`, `TILETHUE = 0`, `NGAY = ngày chốt` (mặc định 31/12 năm trước).
- Số tiền > 0: tạo mới nếu chưa có phiếu, cập nhật nếu đổi số tiền hoặc đổi ngày; số tiền = 0: xóa phiếu nếu có; số tiền < 0: bỏ qua. Nhập từ Excel (cột "Mã đối tác", "Công nợ đầu") và xuất file mẫu.
- Chỉ liệt kê khách / nhà cung cấp `STATUS=30`.
- Quy ước công nợ khách (từ `GetNoCu`, xem file bán lẻ): nợ = Σ(TONGCONG − TIENTHANHTOAN) của bill đã thanh toán − Σ(thu − chi) có tác động công nợ.

## 4. Tồn quỹ (`TonQuyHandler`)

Lọc báo cáo quỹ theo cửa hàng và theo hình thức: Tất cả / Tiền mặt / Quẹt thẻ / Ngân hàng (cả nhóm hoặc từng tài khoản `DTAIKHOANNGANHANG` `STATUS=30`).
- Phần phiếu thu chi: tiền mặt = `CHUYENKHOAN = 0`; ngân hàng = `CHUYENKHOAN = 30` (và theo tài khoản); quẹt thẻ: phiếu thu chi không có thẻ (`0 = 1`).
- Phần bill bán (`TIENTHANHTOAN ≠ 0`): tiền mặt `COALESCE(TIENMAT,0) ≠ 0` (số tiền = `TIENMAT`), quẹt thẻ `THE ≠ 0` (số tiền = `THE`), ngân hàng `CHUYENKHOAN ≠ 0` (số tiền = `CHUYENKHOAN`), theo tài khoản `DTAIKHOANNGANHANGID`; tổng "tất cả" = `TIENMAT + CHUYENKHOAN + THE`. Lọc cửa hàng của bill theo `DCUAHANGID` hoặc theo cửa hàng của kho nhập/xuất.
- SQL gốc của báo cáo quỹ nằm ở `SREPORT/BÁO CÁO TỒN QUỸ` (sẽ ghi khi đọc báo cáo).

## 5. Lý do thu / chi (`DLYDOTHUCHIAe`)

- Cờ `LALYDOTHU = 30` (lý do thu) hoặc 0 (lý do chi), loại trừ nhau. Lý do **chi** có `LOAILYDO`: `1` = định phí, `2` = chi khác (dùng trong báo cáo kết quả kinh doanh; loại còn lại — ví dụ chi lương, chi tạm ứng, chi hoàn thẻ — không vào hai nhóm này). Các lý do hệ thống mà mã tham chiếu theo tên: "Thu tạm ứng", "Nạp thẻ trả trước", "Chi trả thẻ trả trước".
