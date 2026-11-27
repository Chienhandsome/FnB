# Báo cáo đêm 27 → 28/11/2026 (Claude chạy liên tục, Chiến vắng)

Selftest: **1376 đạt, 0 lỗi → 1553 đạt, 0 lỗi** (thêm 177 ca). 11 chức năng mới lên `main`, mỗi cái một issue "Bấm thử" nhãn `cho-chien`. Chưa cái nào được bấm thử bằng tay; chưa có ảnh app gốc cho các màn mới (computer use chưa nhận được trong phiên này).

## Đã làm (theo thứ tự)

| # | Chức năng | Commit | Issue | Vòng agy | Selftest sau |
|---|---|---|---|---|---|
| 1 | Xuất lại định lượng | `bf9dd5a` | #57 | 2 | 1392 |
| 2 | Danh mục cửa hàng | `37211bd` | #58 | 2 | 1413 |
| 3 | Nhân sự P1: Danh mục nhân viên, ca làm việc | `708952b` | #59 | 1 | 1446 |
| 4 | Nhân sự P2: Thưởng phạt, Tạm ứng lương | `4f40fbd` | #60 | 3 (1 vòng hết hạn mức) | 1476 |
| 5 | Nhân sự P3a: Bảng lương, Tính lương, Chấm công | `7d7653e` | #61 | 3 | 1492 |
| 6 | Nhân sự P3b: Chi lương | `ba293bd` | #62 | 1 | 1497 |
| 7 | Mục nhỏ P-A: Thông báo sửa chữa, Danh mục mật khẩu wifi | `cfff63f` | #63 | 3 | 1522 |
| 8 | Mục nhỏ P-B: Tồn nhiều kho | `8d26868` | #64 | 1 (+1 dòng Claude sửa) | 1528 |
| 9 | Kiểm soát order | `e750244` | #65 | 1 | 1537 |
| 10 | Danh mục thẻ trả trước | `63df5fb` | #66 | 2 | 1553 |

Spec mới, Claude tự duyệt theo §10.1.1: `nhan-su-danh-muc.md`, `nhan-su-thuong-phat-tam-ung.md`, `nhan-su-tinh-luong.md`, `quan-tri-nho.md`, `kiem-soat-order.md`, `danh-muc-the-tra-truoc.md` (Danh mục cửa hàng và Xuất lại định lượng đã có spec từ trước). Brief, QUEUE, `hieu-qua.md` (42 chức năng có code) đã cập nhật; bài học agy ghi ở `docs/kinh-nghiem.md`.

## Chiến cần xem lại (quyết định Claude tự chốt, có tiền hoặc quyền)

1. **Trừ tạm ứng khi tính lương chỉ lấy `CHI`** (R1, `nhan-su-tinh-luong.md` T6). Form máy của gốc trừ cả `THU`; vì luồng chuẩn không tạo phiếu thu tạm ứng nên hai cách cho cùng kết quả.
2. **Chế độ Chấm công không lưu tiền** (R8, T4): người chỉ có quyền Chấm công bấm Cập nhật chỉ ghi chấm công, không ghi bảng lương (gốc ghi cả tiền: lỗ hổng).
3. **Chi lương mặc định số "Còn lại" và cho tạo thêm phiếu** khi đã có phiếu (T10; gốc mặc định Thực nhận và chỉ tạo khi chưa có phiếu).
4. **Ô chấm công về trống thì xóa dòng chi tiết** (T8; gốc giữ dòng cũ), **không có chốt/khóa bảng lương** (T9, như gốc), **không làm tròn trong công thức lương** (T7).
5. **Thẻ trả trước** (T1–T8 trong `danh-muc-the-tra-truoc.md`): chặn chi trả quá số dư, chặn sửa/xóa phiếu gây số dư âm, nạp vào thẻ khóa/hết hạn sau xác nhận, phiếu nạp/chi đặt cờ `KHONGTHAYDOICONGNO` (không đổi công nợ khách), quyền Nạp/Chi gắn Thêm/Sửa.
6. **Số phiếu tự sinh là suy đoán** (gốc sinh trong khung No1Lib): `TP` (thưởng phạt), `SC` (sửa chữa); tạm ứng, chi lương, nạp/chi thẻ dùng `PC`/`PT` như Phiếu thu chi.
7. **Bắt buộc/chặn thêm so với gốc:** thưởng phạt không cho cả hai số tiền bằng 0; sửa chữa bắt buộc chọn bàn và nội dung; trùng mã nhân viên; bảng lương không trùng (tháng, năm); ca làm việc có giờ bằng nhau khi bật máy chấm công bị chặn.
8. **Tồn nhiều kho** chỉ có ở menu "Tồn nhiều kho"; màn Tồn kho thường không có mục "Tất cả các kho".
9. **Kiểm soát order**: số trùng chỉ tính một lần khi quét thiếu (suy từ mô tả gốc), nhấp đôi mở hóa đơn có thể chưa làm.
10. **Mật khẩu wifi chưa gán lên hóa đơn** khi thanh toán (gốc lấy rồi xóa mật khẩu), làm cùng việc in hóa đơn.

## Việc chưa làm (và vì sao)

- Cấu hình máy in theo khu vực, In P3b "In danh sách", In mã vạch P4, nhập Excel P5, Ctrl+P P6: cần đối chiếu app gốc/mẫu in; không làm khi vắng.
- Dữ liệu ban đầu / Nhập dữ liệu từ hệ thống cũ, Gửi tin nhắn (menu chết): chưa có yêu cầu rõ.
- Còn nợ từ trước: #42, #47, #50, #53 (điểm TẠM Kho/Công nợ, giá vốn/kiểm kê, voucher/KM, cờ `GIAMTONGTHEOTIEN`), hiệu năng giá vốn trên DB lớn chưa đo.

## Sự cố trong đêm

- agy hết hạn mức (429) lúc 00:37, làm mới khoảng 02:03: mất khoảng 1,5 giờ chờ (đã dùng để viết spec P3).
- Lệnh kiểm phạm vi báo một lỗi chặn thoáng qua khi mở Danh mục cửa hàng (file ngoài worktree), chạy lại thì đạt; không ảnh hưởng.
- Computer use không nhận được dù Chiến đã bật: chưa bấm thử app gốc; phiên mới có thể làm (prompt đã đưa).

## Gợi ý buổi sáng

1. Mở issue #57–#66 và bấm theo từng mô tả, ưu tiên Tính lương (#61), Chi lương (#62), Thẻ trả trước (#66) vì có tiền.
2. Quyết các điểm 1–5 ở trên (đồng ý hoặc đổi).
3. Cho một phiên mới bấm thử app gốc để đối chiếu Khuyến mại, Voucher, Số dư đầu kỳ, các màn Nhân sự (bố cục lưới, màu ô chấm công, tên nút).
