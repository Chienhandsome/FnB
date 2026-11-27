# Spec: Tính lại giá vốn

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; Chiến chọn làm tiếp "Giá vốn và kiểm kê" 27/11; các điểm **TẠM** ở §6 là hướng an toàn Claude chọn).
- Ngày: 2026-11-27. Loại: **nặng** (giá vốn, tiền). Nguồn: `docs/deep/kho-hang.md` §A.8, §B.3, §B.4, E.1 (B7, B18, B25, B26); `phieu-nhap-kho.md`, `ton-kho.md`.
- Menu: KHO HÀNG ▸ Tính lại giá vốn (thêm vào nhóm KHO HÀNG nếu chưa có). Chạy từ nút trên màn, và tự chạy sau khi lưu/sửa/xóa phiếu nhập kho khi cấu hình bật.

## 1. Mục tiêu và phạm vi

Tính lại `TDONHANGCHITIET.GIAVON` của mọi dòng chứng từ và `DMATHANG.GIAVON` theo phương pháp **bình quân cuối kỳ theo tháng**, giống app gốc. Không đổi cấu trúc CSDL. **Không làm** ở đây: Xuất lại định lượng, Tồn kho ban đầu (nhưng phiếu `LOAI = 97` nếu có thì được tính như phiếu nhập), mật khẩu cứng của app gốc (không dùng; dùng quyền).

## 2. Thuật toán (đúng app gốc, trừ các điểm TẠM)

1. Lấy `TuNgay = MIN(NGAY)`, `DenNgay = MAX(NGAY)` của `TDONHANG` (chỉ phiếu `STATUS = 30`). Lấy mọi mặt hàng `STATUS = 30` có giao dịch; mặt hàng không có giao dịch thì bỏ qua.
2. Duyệt **từng tháng dương lịch có phát sinh**, theo thứ tự thời gian (mỗi tháng làm bước A rồi bước B).
3. **Bước A, nguyên liệu và kiêm vật tư** (mặt hàng `DLOAIMATHANGID` thuộc nguyên liệu hoặc kiêm vật tư): `ton = ton tháng trước + Σ SLNHAP` và `giaTri = giá trị tháng trước + Σ THANHTIEN` của dòng nhập trong tháng (`TDONHANG.LOAI = 1` hoặc `97`; **dùng `THANHTIEN` sau chiết khấu**, không dùng `DONGIA × SL`); `giaVon = ton = 0 ? 0 : Round(giaTri / ton, 2)`. Gán `GIAVON = giaVon` cho **mọi dòng** của mặt hàng trong tháng (cả dòng nhập). Sau đó với các dòng không phải nhập: `ton −= (SLXUAT − SLNHAP)` và `giaTri −= (SLXUAT − SLNHAP) × giaVon`. `ton`, `giaTri` mang sang tháng sau.
4. **Bước B, món định lượng và combo** (loại `'1'` và `'4'` có công thức `DDINHLUONG`, bung tối đa **2 cấp**): `giaVon = Round(Σ(giaVon vật tư × SOLUONG), 2)` với giá vốn vật tư vừa tính ở bước A cho tháng đó; gán cho các dòng của món trong tháng thuộc phiếu `LOAI` khác 1 và 97 nếu `GIAVON` khác giá mới. Vật tư không có giá vốn thì coi 0.
5. Kết thúc: ghi `DMATHANG.GIAVON` = giá vốn của tháng cuối **chỉ khi khác giá hiện có**, và chỉ cho mặt hàng đã được tính ở bước A hoặc B (xem T1).
6. Toàn bộ ghi trong **một transaction** (T2): lỗi giữa chừng thì rollback, không để dữ liệu dở dang.

## 3. Màn Tính lại giá vốn

Một hộp đơn giản: mô tả ngắn ("Tính lại giá vốn theo bình quân cuối kỳ từ phiếu nhập đầu tiên đến nay"), nút **Tính lại**, thanh tiến độ (theo tháng) và đồng hồ thời gian, chạy nền (`BackgroundWorker`, không treo giao diện), nút Đóng khi đang chạy bị khóa. Xong báo "Đã tính xong: N mặt hàng, M dòng chứng từ cập nhật". Lỗi báo một câu rõ và rollback. Quyền: chức năng "Tính lại giá vốn" (Xem; nút Tính lại cần quyền Sửa, admin luôn được). Ghi lưu vết một dòng (người, giờ, số mặt hàng/dòng đổi). Không có mật khẩu.

## 4. Chạy tự động

Cấu hình "Tự động tính giá vốn" (`TuDongTinhGiaVon`, mặc định theo `SCONFIG`; không có thì tắt): khi bật, sau khi **commit** việc lưu, sửa hoặc xóa phiếu nhập kho, chạy cùng thuật toán. Lỗi của lần chạy tự động **không làm hỏng phiếu đã lưu**, chỉ hiện cảnh báo một câu ("Đã lưu phiếu nhưng chưa tính lại được giá vốn, hãy chạy Tính lại giá vốn"). Không chạy tự động sau xuất, chuyển, kiểm kê (giống app gốc).

## 5. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Một mặt hàng nguyên liệu: nhập tháng 1: 10 cái, `THANHTIEN` 100.000; nhập tháng 2: 10 cái, `THANHTIEN` 140.000 (có chiết khấu 0). Giá vốn tháng 1 = 10.000; tháng 2: ton = 20 − (xuất tháng 1), giaTri đúng công thức; kiểm từng bước bằng số cụ thể.
2. Xuất tháng 1 (bán đã thanh toán) 4 cái lấy `GIAVON` 10.000; `ton` và `giaTri` mang sang tháng 2 đúng.
3. Món định lượng gồm 2 vật tư: `GIAVON` món = Σ(giá vốn vật tư × số lượng) làm tròn 2 số lẻ; combo 2 cấp.
4. `DMATHANG.GIAVON` cập nhật bằng giá tháng cuối; mặt hàng không thuộc nguyên liệu/kiêm vật tư/định lượng **không bị ghi đè về 0** (T1).
5. `THANHTIEN` có chiết khấu: giá vốn dùng `THANHTIEN`, không dùng `DONGIA × SL`.
6. Tồn âm: không chia cho 0 hay ra giá vốn âm (T3).
7. Bill bán chưa thanh toán không trừ tồn và giá trị (T4), khớp công thức tồn chung.
8. Lỗi giữa chừng (hook) rollback: không đổi dòng nào; chạy hai lần liên tiếp cho cùng kết quả.
9. Tự động sau lưu phiếu nhập khi cấu hình bật: giá vốn đổi; cấu hình tắt: không đổi; lỗi tính lại không làm mất phiếu.
10. Menu mở màn thật; quyền Xem/Sửa; không đổi cấu trúc CSDL; ca selftest cũ giữ tên.

## 6. Điểm TẠM (tiền, giá vốn; mở issue `cho-chien`)

- **T1** Không ghi đè `DMATHANG.GIAVON` về 0 cho loại mặt hàng không được tính (app gốc có lỗi này: B7).
- **T2** Một transaction (app gốc không dùng transaction).
- **T3** Tồn ≤ 0 khi tính: giữ giá vốn của tháng trước, không chia (app gốc không xử lý).
- **T4** Chỉ tính bill bán đã thanh toán (`DATHANHTOAN = 30`) và phiếu `STATUS = 30`, khớp công thức tồn chung (app gốc không lọc; B25).
- **T5** Hiệu năng trên DB lớn chưa đo; thuật toán đọc theo từng mặt hàng, không nạp thừa.
- **T6** Chưa có màn Xuất lại định lượng và Tồn kho ban đầu: giá vốn của món pha chế chỉ đúng khi dòng bán đã có sẵn định lượng.
