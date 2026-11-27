# Spec: Xuất lại định lượng (sinh lại dòng trừ nguyên liệu của hóa đơn bán)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; Chiến chọn làm theo thứ tự đề xuất 27/11). Điểm **TẠM** ở §6.
- Ngày: 2026-11-27. Loại: **nặng** (xóa và sinh lại dòng trừ kho, ảnh hưởng tồn và giá vốn). Nguồn: `docs/deep/kho-hang.md` Q17, B2, B13, B19; cách trừ nguyên liệu khi thanh toán đã làm ở `su-dung-dich-vu.md` §3.7 (`ServicePaymentRepository.ComputeMaterialExportsIn`); `tinh-lai-gia-von.md`.
- Menu: KHO HÀNG ▸ Xuất lại định lượng (`SMENU` có sẵn, mã gốc `ZZZ009`; quyền "Xuất lại định lượng").

## 1. Mục đích và phạm vi

Khi công thức định lượng (`DDINHLUONG`) của món đổi, hóa đơn đã bán trước đó đã trừ nguyên liệu theo công thức cũ. Chức năng này **xóa các dòng xuất vật tư cũ** (`TDONHANGCHITIET.XUATVATTU = 30`) của các hóa đơn bán trong một khoảng ngày rồi **sinh lại** theo công thức hiện tại, rồi tính lại giá vốn.

| Làm | Không làm |
|---|---|
| Màn chọn khoảng ngày, xem trước số hóa đơn bị ảnh hưởng, xác nhận, xóa và sinh lại trong một transaction, tính lại giá vốn, lưu vết | Mật khẩu cứng của app gốc (`6666` + mật khẩu admin đầu tiên): thay bằng quyền và xác nhận rõ ràng; khôi phục lại bản cũ; hóa đơn của Khách đặt hàng/Báo giá |

## 2. Màn Xuất lại định lượng

- **Từ ngày**, **Đến ngày** (**bắt buộc cả hai**, không được để trống, T1; app gốc cho trống nên xóa sạch toàn lịch sử mà không sinh lại gì: lỗi nghiêm trọng, không lặp lại), mặc định tháng hiện tại; Từ ngày không sau Đến ngày. Cửa hàng/Kho: mặc định mọi hóa đơn thuộc cửa hàng người dùng được xem; admin chọn được tất cả.
- Nút **Xem trước**: đếm số hóa đơn bán đủ điều kiện trong khoảng ngày, số dòng xuất vật tư hiện có sẽ bị xóa, số dòng sẽ sinh lại; không ghi gì.
- Nút **Thực hiện**: hiện hộp xác nhận nêu khoảng ngày, số hóa đơn, số dòng sẽ xóa và sinh lại, và câu "Thao tác này thay đổi số liệu tồn kho và giá vốn, không hoàn tác được. Nên sao lưu dữ liệu trước." — người dùng phải gõ đúng từ **XACNHAN** (không phân biệt hoa thường) mới bấm Đồng ý được. Chạy nền (`BackgroundWorker`), thanh tiến độ theo hóa đơn, đồng hồ, không treo giao diện; xong báo kết quả.
- Quyền: chức năng "Xuất lại định lượng" (Xem, Sửa); nút Thực hiện cần quyền Sửa, admin luôn được.

## 3. Quy tắc xử lý

- **Hóa đơn đủ điều kiện:** `TDONHANG.LOAI = 0`, `STATUS = 30`, `DATHANHTOAN = 30` (**chỉ hóa đơn đã thanh toán**, cùng công thức tồn chung; bill chưa thanh toán sẽ được sinh dòng khi thanh toán, T2; app gốc cả bill chưa thanh toán), có bàn (`DBANID` không rỗng, như gốc: chỉ đơn bán qua sơ đồ bàn), `NGAY` nằm trong khoảng ngày (so theo ngày, biên đóng).
- **Một transaction** cho toàn bộ: với từng hóa đơn đủ điều kiện, xóa dòng `XUATVATTU = 30` của hóa đơn đó rồi sinh lại bằng đúng hàm đã dùng lúc thanh toán (`ServicePaymentRepository.ComputeMaterialExportsIn` và cách ghi dòng xuất vật tư, kho xuất theo kho của khu vực bàn như lúc thanh toán; **không viết bản sao thứ hai**). Lỗi giữa chừng thì rollback, không hóa đơn nào đổi.
- **Không chặn âm kho** khi xuất lại (đây là dữ liệu lịch sử đã bán): sau khi xong, báo số mặt hàng có tồn âm (nếu có) để người dùng kiểm.
- Sau khi commit: **tính lại giá vốn** bằng `CostRecalcService` (như gốc, bất kể cấu hình tự động); lỗi tính chỉ cảnh báo một câu "Đã xuất lại định lượng nhưng chưa tính lại được giá vốn, hãy chạy Tính lại giá vốn", không làm mất kết quả đã ghi.
- Ghi lưu vết một dòng: người, khoảng ngày, số hóa đơn, số dòng xóa và sinh (chuỗi ghi vào cột có giới hạn cắt cho vừa, 255).
- Hóa đơn của khoảng ngày đang có người mở bàn/điều chỉnh: không khóa, nhưng chỉ đụng hóa đơn đã thanh toán nên không xung đột.

## 4. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Món X công thức 2 × nguyên liệu A, bán 3 phần trong một hóa đơn đã thanh toán (có dòng xuất vật tư A = 6): đổi công thức X thành 3 × A rồi Xuất lại: dòng xuất vật tư A của hóa đơn thành 9; tồn A đúng; giá vốn tính lại.
2. Món có định lượng 2 tầng (món lồng món): sinh đúng 2 tầng như lúc thanh toán (kết quả giống hệt `ComputeMaterialExportsIn`).
3. Khoảng ngày chỉ đụng hóa đơn trong khoảng: hóa đơn ngoài khoảng, hóa đơn chưa thanh toán, hóa đơn đã hủy, hóa đơn không có bàn không đổi.
4. Để trống Từ ngày hoặc Đến ngày, hoặc Từ ngày sau Đến ngày: chặn, không xóa dòng nào.
5. Xem trước đếm đúng và không ghi gì; xác nhận sai từ XACNHAN thì không chạy.
6. Lỗi giữa chừng (hook) rollback: dòng cũ còn nguyên, không dòng mới nào; chạy hai lần cho cùng kết quả; sau khi xong báo đúng số mặt hàng tồn âm.
7. Lỗi tính lại giá vốn chỉ cảnh báo, kết quả xuất lại còn nguyên; lưu vết đúng một dòng; chỉ Xem thì không chạy được; menu mở màn thật; không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 5. Giao diện

Hộp gọn: hai ô ngày, nút Xem trước và Thực hiện, vùng kết quả, thanh tiến độ, đồng hồ; tiêu đề đủ rộng; màu qua `ThemeManager`; hộp xác nhận rõ ràng, nút Hủy là mặc định.

## 6. Điểm TẠM

- **T1** Bắt buộc chọn khoảng ngày (sửa lỗi nghiêm trọng B2 của gốc).
- **T2** Chỉ xuất lại cho hóa đơn đã thanh toán (gốc cả chưa thanh toán, B19).
- **T3** Thay mật khẩu cứng bằng quyền "Xuất lại định lượng" và ô xác nhận gõ XACNHAN (gốc `6666` + mật khẩu admin).
- **T4** Không chặn âm kho khi xuất lại; chỉ báo số mặt hàng tồn âm.
- **T5** Chưa làm khôi phục dòng cũ; khuyến nghị sao lưu dữ liệu trước khi chạy.
