# Chiến chốt 28/11/2026 (trả lời lần lượt các câu hỏi sau đêm làm liên tục)

Nguồn: Chiến trả lời trong phiên Claude Code sáng 28/11. Các quyết định này **thắng** mọi chỗ khác trong spec liên quan.

## Đã áp dụng vào spec và đang sửa code (patch `chien-chot-2811`)
- Tính lương: trừ tạm ứng chỉ lấy `CHI` (giữ). **Chấm công vẫn ghi cả tiền khi Cập nhật, như gốc** (bỏ T4 của `nhan-su-tinh-luong.md`).
- Chi lương: mặc định "Còn lại", cho tạo thêm phiếu (giữ T10). Ô chấm công về trống thì xóa dòng (giữ T8).
- Thẻ trả trước: **bỏ** các chặn thêm (chi trả quá số dư; sửa/xóa phiếu làm số dư âm); **phiếu nạp/chi thẻ có tính vào công nợ** (không ép `KHONGTHAYDOICONGNO`); nạp vào thẻ khóa/hết hạn cho phép sau xác nhận; quyền Nạp = Thêm, Chi trả = Sửa.
- Giữ nguyên: các chặn thêm ở Thưởng phạt (hai số tiền không cùng 0), Sửa chữa (bắt buộc bàn và nội dung), trùng mã nhân viên, bảng lương trùng (tháng, năm); số phiếu `TP`, `SC`; Tồn nhiều kho chỉ ở menu Tồn nhiều kho; Kiểm soát order tính số trùng một lần.

## Việc mới chốt, chưa làm
1. **Mật khẩu wifi trên hóa đơn:** làm, **không xóa mật khẩu** khỏi danh mục; chọn **xoay vòng theo thứ tự** (lấy lần lượt từng mật khẩu theo thứ tự danh mục, hết thì quay lại đầu); mật khẩu đã dùng ghi lên hóa đơn (`TDONHANG.PASSWIFI`) để biết mật khẩu kế tiếp. Làm cùng phần in hóa đơn khi thanh toán.
2. **Cấu hình máy in hóa đơn theo khu vực:** làm, lưu file cục bộ theo máy (không đổi CSDL), mục `INKHUVUC` key `DKHUVUCID` = tên máy in có trong danh sách máy in cài trên máy.
3. **In danh sách, In mã vạch, nhập Excel ở Danh mục và Đặt hàng, Ctrl+P ở hộp sửa:** **đối chiếu app gốc trước** (phiên mới bấm thử app gốc), rồi mới viết spec và làm.
4. **Nhập dữ liệu từ hệ thống cũ:** xem app gốc trước rồi mới quyết.
5. **Gửi tin nhắn tới khách hàng:** không làm, **ẩn menu**.

## Chiến trả lời tiếp (28/11, sau khi bấm thử app gốc) — ĐÃ DUYỆT
1. **Menu Voucher, Khuyến mại, Thẻ trả trước, Kiểm soát order, Mật khẩu wifi, Cửa hàng:** hiện/ẩn theo cờ `SCONFIG` trong cấu hình toàn hệ thống (`VISIBLECONFIGID` của mục menu), như app gốc.
2. Ô **Phân loại** của phiếu chi Tạm ứng: xem app gốc rồi làm theo.
3. **Danh mục nhân viên làm dạng cây** (thư mục, thư mục con, phân cách, Thùng rác xóa mềm) như app gốc.
4. **Tạm ứng:** lọc Từ/Đến mặc định để trống như gốc.
5. **Tồn kho ban đầu:** thêm cột "Mã sẵn có" và ô "Tổng giá trị".
6. **Bảng lương:** Số phiếu sửa được, thêm nút "Lưu & Mới" và "In".
