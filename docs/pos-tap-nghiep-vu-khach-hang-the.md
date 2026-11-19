# POS TAP — nghiệp vụ khách hàng, thẻ trả trước, tích lũy, voucher

Nguồn: SFORM `Nhập thẻ trả trước__abaada83`, `Trừ tích lũy__1b396df2` (+ `ts_code.cs`), `Nhập coupon__b631d7c1`, `Khách hàng thân thiết__10e06969`, `Tạm ứng đơn hàng__064236d2`. Công thức điểm cũng nằm trong form bán lẻ (`GetDiemTichLuy`) — xem [pos-tap-nghiep-vu.md](pos-tap-nghiep-vu.md) mục 1.8.

## 1. Thẻ trả trước (`DTHETRATRUOC`)

- Tìm thẻ theo mã (`DTHETRATRUOC.NAME`, so sánh đúng). Từ chối nếu: mã không tồn tại; `NGAYHETHAN < ngày DB` (đã quá hạn); `KHOA = 30` (thẻ bị khóa); số tiền còn lại ≤ 0.
- **Số tiền còn lại** = `Σ(THU − CHI)` của `TTHUCHI` có `DTHETRATRUOCID = thẻ` (tiền nạp, nạp bằng phiếu thu) **−** `Σ THETRATRUOC` của `TDONHANG` có `DTHETRATRUOCID = thẻ` và khác bill hiện tại (đã tiêu). Truyền bill hiện tại để khi sửa lại bill không bị tính đôi.
- Nạp thẻ: lập phiếu thu có `DTHETRATRUOCID`, đối tượng "Khách nạp thẻ", `KHONGTHAYDOICONGNO` tick (xem file thu chi). Thanh toán bằng thẻ ghi `TDONHANG.THETRATRUOC` + `DTHETRATRUOCID`. Khách có thể gắn sẵn thẻ (`DKHACHHANG.DTHETRATRUOCID`); khi thanh toán nếu còn tiền thì tự dùng thẻ đó mà không hỏi mã.

## 2. Điểm tích lũy

- Số điểm hiện có: `HoaDonBanHang.GetDiemTichLuy(khách, bill)` (xem file bán lẻ 1.8): hai cách tính theo `CachTinhDiem` (0 = tổng doanh số chia `DoanhSoTuongUngVoi1Diem`; ≠ 0 = cộng `DIEM` từng bill), cộng `DIEMTICHLUYBANDAU` và `TTANGGIAMDIEM` (điểm tặng / điểm giảm thủ công).
- Giá trị tiền của điểm = `điểm × SystemConfig.QuyDoi1DiemSangTien`.
- Trừ tích lũy khi thanh toán (bản desktop): hiển thị điểm và giá trị; giá trị dùng tối đa bằng tổng tiền cần trả (`min(giá trị, TONGTIEN)`).
- Bản cảm ứng (`TruTichLuyTs`): nhập số điểm sử dụng hoặc giá trị (tự quy đổi hai chiều, làm tròn 2 số lẻ: `điểm = Round(giá trị / quyĐổi, 2)`); nút "Sử dụng hết": `min(điểm có, Round(TongTien / quyĐổi, 2))`; bắt buộc đã thiết lập quy đổi > 0 ("VUI LÒNG THIẾT LẬP QUY ĐỔI ĐIỂM SANG TIỀN TRƯỚC"); điểm sử dụng phải trong khoảng (0, điểm có].
- Khi lưu bill: `TRUTICHLUY` (tiền), `DIEMGIAM = TRUTICHLUY / QuyDoi1DiemSangTien`, `DIEM = (int)(TONGCONG / max(1, DoanhSoTuongUngVoi1Diem))`.
- Form `Khách hàng thân thiết`: danh sách khách (kèm cách tính điểm `@CACHTINH`, `@DIEM`), lọc theo nhóm khách (`DNHOMKHACHHANG`), tab hóa đơn đã thanh toán của khách và tab điểm tăng/giảm (`TTANGGIAMDIEM`, quyền `KhachHangThanThiet`). Nhóm khách có `DIEMTICHLUY` (ngưỡng điểm nâng nhóm), các tỉ lệ giảm giá (xem file nhà hàng mục 5).

## 3. Voucher / coupon (`DVOUCHER`)

- Nhập mã (`DVOUCHER.NAME`): từ chối nếu không tồn tại; `HANSUDUNG < ngày DB` (quá hạn); đã dùng trên bất kỳ bill nào (`COUNT(*) TDONHANG WHERE DVOUCHERID = voucher > 0`). Hợp lệ → trả `VoucherID` và `GiaTriVoucher = DVOUCHER.GIATRI` (số tiền cố định, dùng một lần).
- Khi thanh toán: ghi `TDONHANG.VOUCHER` (số tiền) và `DVOUCHERID`. Nếu `voucher > tổng tiền` thì không có tiền thối từ voucher ("trả lại tiền cọc" tính riêng, xem file nhà hàng mục 10).
- Hai chế độ nhập: ô số tiền tự do hoặc chọn từ danh sách (`LuaChonVoucherTuDanhSach`).

## 4. Tạm ứng đơn hàng (`TamUngDonHang`)

- Danh sách phiếu thu (`TTHUCHI`, `LOAI = 0`) có `TDONHANGID = bill` hoặc `TDATHANGID = phiếu đặt`; thêm / sửa dùng form `TTHUCHI0Ae` ở chế độ tạm ứng (xem file thu chi mục 1). Chỉ hiện khi `SuDungChucNangTamUngTrongDonHang = 30`.

## 5. Khai báo liên quan (STABLEDESC)

- **Thẻ trả trước** (`DTHETRATRUOCAe`): trên form thẻ có "Nạp tiền" (mở phiếu thu `TTHUCHI` loại 0 với `DTHETRATRUOCID`, lý do lấy theo lần nạp gần nhất hoặc lý do tên "Nạp thẻ trả trước", diễn giải "Nạp thẻ trả trước <tên thẻ>") và "Chi trả" (phiếu chi hoàn tiền thẻ, lý do "Chi trả thẻ trả trước", chỉ khi còn tiền). Hiển thị tổng nạp, tổng sử dụng, còn lại (`GetSoTienConLai`) cùng danh sách các lần nạp và các bill đã thanh toán bằng thẻ.
- **Nhóm khách hàng** (`DNHOMKHACHHANGAe`): các tỉ lệ giảm giá `TILEGIAMGIATIENHANG`, `TILEGIAMGIATIENGIO` (ẩn ô giờ khi không có tiền giờ hoặc khi gộp chung giảm giá; khi gộp chỉ dùng `TILEGIAMGIA` / giảm tổng), `TILEGIAMDOAN`, `TILEGIAMDOUONG`, `TILEGIAMDOKHAC`, `TILEGIAMDICHVU`, ngưỡng điểm nâng nhóm `DIEMTICHLUY`.
- **Tăng / giảm điểm** (`TTANGGIAMDIEMAe`): nhập `DIEMTANG` hoặc `DIEMGIAM`. Điều kiện kiểm tra trong mã gốc `DIEMGIAM + DIEMGIAM == 0 && DIEMGIAM × DIEMTANG == 0` (viết nhầm `DIEMGIAM` hai lần) khiến **phiếu chỉ có điểm tăng (điểm giảm = 0) cũng bị từ chối**; ý định là bắt buộc có ít nhất một trong hai.
