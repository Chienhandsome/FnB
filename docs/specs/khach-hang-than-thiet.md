# Spec: Khách hàng thân thiết (điểm tích lũy, tặng/giảm điểm)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1). Chiến chốt 27/11: màn dùng công thức điểm riêng như gốc; tự nâng nhóm chỉ nâng, không hạ; chặn giảm điểm vượt điểm hiện có. Điểm **TẠM** ở §8.
- Ngày: 2026-11-27. Loại: **nặng** (điểm đổi ra tiền: 1 điểm = 1.000 đ khi trừ tích lũy). Nguồn: `docs/deep/khach-hang.md` §1 (điểm tích lũy), §5.5, Q31–Q35; `su-dung-dich-vu.md` §3.7 (trừ tích lũy khi thanh toán, đã làm).
- Menu: KHÁCH HÀNG ▸ Khách hàng thân thiết (menu có sẵn trong `SMENU`; quyền "Khách hàng thân thiết").

## 1. Phạm vi

| Làm | Không làm |
|---|---|
| Màn xem khách kèm doanh số, số hóa đơn, điểm tích lũy; tab Hóa đơn của khách; tab "Tặng điểm - tặng quà" (thêm, sửa, xóa phiếu tặng/giảm điểm `TTANGGIAMDIEM`); lọc theo nhóm khách; xuất Excel; tự nâng nhóm sau khi tặng điểm | Gửi tin nhắn/SMS (gốc không triển khai), sửa hồ sơ khách (đã có Danh mục khách hàng), đổi quy tắc cộng điểm hay trừ điểm khi thanh toán (đã làm ở bán hàng), thẻ trả trước |

## 2. Màn Khách hàng thân thiết

Khung trái/phải như các màn danh mục (cây nhóm khách `DNHOMKHACHHANG` bên trái, chọn nhóm cha hiện cả nhóm con, và dòng "Tất cả"); bên phải trên: lưới khách; bên phải dưới: hai tab của khách đang chọn.
- **Lưới khách (11 cột như gốc):** Tên khách (`NAME`, sắp tăng), Nhóm khách, Địa chỉ, Điện thoại, Email, Điểm tích lũy ban đầu (`DIEMTICHLUYBANDAU`), **Doanh số**, **Số hóa đơn**, **Điểm tích lũy**, Ghi chú. Ô tìm không dấu (tên, mã, điện thoại, địa chỉ); lọc "Chỉ khách có điểm". Hàng tổng cộng cho Doanh số, Số hóa đơn, Điểm tích lũy.
  - `Doanh số` = Σ `TONGCONG` của hóa đơn của khách có `DATHANHTOAN = 30` và `LOAI = 0`.
  - `Số hóa đơn` = số hóa đơn cùng điều kiện.
  - **Điểm tích lũy (công thức riêng của màn này, Chiến chốt):** `DIEMTICHLUYBANDAU` + (với `CachTinhDiem = 1`: Σ(`DIEM` − `DIEMGIAM`) của hóa đơn đã thanh toán `LOAI = 0`; với `CachTinhDiem` khác 1: Σ `TONGCONG` / `DoanhSoTuongUngVoi1Diem` − Σ `DIEMGIAM`) + Σ(`DIEMTANG` − `DIEMGIAM`) của phiếu tặng/giảm điểm (`TTANGGIAMDIEM`, chưa xóa). `DoanhSoTuongUngVoi1Diem` bằng 0 thì phần chia coi là 0.
- **Tab "Hóa đơn"** của khách đang chọn: các hóa đơn đã thanh toán `LOAI = 0`: Điểm (`DIEM`), Số phiếu, Ngày, Tổng cộng, Điểm sử dụng (`DIEMGIAM`); hàng tổng cộng.
- **Tab "Tặng điểm - tặng quà"** của khách đang chọn: các phiếu `TTANGGIAMDIEM`: Số phiếu, Ngày, Điểm tặng, Điểm giảm, Lý do, Ghi chú; nút Thêm, Sửa, Xóa (theo quyền).
- Quyền: chức năng "Khách hàng thân thiết" (Xem, Thêm, Sửa, Xóa); người chỉ có Xem thì tab Tặng điểm chỉ đọc. Xuất Excel qua `GridExcelExporter` cho lưới khách và từng tab.

## 3. Hộp tặng/giảm điểm (370 × 233)

Trường: **Ngày** (mặc định hôm nay, bắt buộc; khóa nếu không có quyền "Thay đổi ngày", admin được), **Số phiếu** (bắt buộc, không trùng; tự sinh `TD` + yy + `/` + 5 chữ số, sửa tay được, T1), **Khách hàng** (bắt buộc, khách có hồ sơ; mở từ tab thì đặt sẵn và khóa), **Điểm tặng**, **Điểm giảm**, **Lý do** (bắt buộc, tối đa 255), **Ghi chú**.
- Điểm tặng và Điểm giảm ≥ 0, không cùng bằng 0; nhập số nguyên (làm tròn xuống ô thập phân), tối đa 9 chữ số.
- **Chặn giảm vượt điểm (Chiến chốt):** điểm giảm của phiếu không được lớn hơn điểm hiện có của khách (theo công thức màn này, đã bỏ chính phiếu đang sửa) cộng điểm tặng của chính phiếu: "Điểm giảm vượt điểm hiện có (còn X điểm)".
- Lưu: một transaction (phiếu + lưu vết); sửa: giữ người/giờ tạo, ghi người/giờ sửa; xóa: xóa cứng sau khi kiểm xóa không làm điểm của khách thành âm ("Xóa phiếu này làm điểm của khách âm") rồi lưu vết. Chuỗi ghi vào cột có giới hạn (kể cả `Note`, `TableName` của lưu vết, 255) cắt cho vừa.
- Sau khi lưu phiếu **tặng điểm** (điểm tặng > 0): nếu cấu hình `TuDongNangCapThanhVienKhiDatHanMuc` bật thì **tự nâng nhóm** khách bằng đúng hàm nâng nhóm khi thanh toán đã có (`ServicePaymentRepository.Write`, không viết bản sao): chỉ nâng lên nhóm có ngưỡng `DIEMTICHLUY` cao hơn nhóm hiện tại, **không hạ nhóm** (Chiến chốt), điểm xét nâng không trừ điểm đã tiêu như gốc; báo một câu "Khách X đã được nâng lên nhóm Y" khi có nâng.

## 4. Nghiệm thu (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Khách có `DIEMTICHLUYBANDAU` 10, hai hóa đơn đã thanh toán (`DIEM` 5 và 7, `DIEMGIAM` 2 ở hóa đơn sau) và một hóa đơn chưa thanh toán, một phiếu tặng 20: Doanh số, Số hóa đơn đúng (chỉ hóa đơn đã thanh toán, `LOAI = 0`); Điểm = 10 + (5 + 7 − 2) + 20 = 40 với `CachTinhDiem = 1`; nhánh `CachTinhDiem` khác 1 đúng công thức §2 bằng số cụ thể.
2. Tab Hóa đơn và tab Tặng điểm đúng dòng, đúng hàng tổng; lọc nhóm (cha hiện cả con), tìm không dấu, "chỉ khách có điểm", xuất Excel đúng cột.
3. Thêm phiếu tặng điểm hợp lệ; thiếu số phiếu, thiếu khách, thiếu lý do, hai điểm cùng 0, âm, trùng số phiếu: chặn có thông báo; số phiếu tự sinh tăng đúng.
4. Giảm điểm vượt điểm hiện có bị chặn nêu số điểm còn lại; giảm bằng đúng điểm hiện có thì được; sửa phiếu không tự chặn chính nó; xóa phiếu tặng làm điểm âm thì chặn.
5. Tặng điểm làm khách đạt ngưỡng nhóm cao hơn: tự nâng đúng một lần khi cấu hình bật, không nâng khi tắt; tụt điểm (giảm điểm) không hạ nhóm; nhóm hiện tại đã cao hơn thì không đổi.
6. Quyền: chỉ Xem thì không có nút Thêm/Sửa/Xóa; chuỗi lý do 255 ký tự không làm lưu vết tràn; rollback bằng hook không để lại phiếu; menu mở màn thật; không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 5. Giao diện

Khung như Danh mục khách hàng; tiêu đề cột đủ rộng không cắt chữ; hộp gọn, cảm ứng được; màu qua `ThemeManager`; hàng tổng cộng đậm.

## 6. Dữ liệu

Không đổi cấu trúc CSDL. `TTANGGIAMDIEM` (`NAME`, `NOTE`, `NGAY`, `DKHACHHANGID`, `DIEMTANG`, `DIEMGIAM`, `LYDO`, cột chuẩn); đọc `DKHACHHANG`, `DNHOMKHACHHANG` (`DIEMTICHLUY`), `TDONHANG` (`DIEM`, `DIEMGIAM`, `TONGCONG`, `DATHANHTOAN`, `LOAI`), `SCONFIG` (`CachTinhDiem`, `DoanhSoTuongUngVoi1Diem`, `TuDongNangCapThanhVienKhiDatHanMuc`).

## 7. Liên quan

Điểm khi thanh toán (cộng `DIEM`, trừ `DIEMGIAM`) và hàm điểm của thanh toán đã làm ở Sử dụng dịch vụ; spec này **không** đổi chúng (hai công thức khác nhau có chủ đích theo gốc, xem T2).

## 8. Điểm TẠM

- **T1** Số phiếu tặng điểm `TD` + yy/00000 là suy đoán (gốc sinh số trong khung No1Lib).
- **T2** Màn dùng công thức điểm riêng của gốc nên có thể lệch số điểm lúc thanh toán (hóa đơn chưa thanh toán, đã tiêu điểm); Chiến đã chốt giữ như gốc.
- **T3** Chặn giảm vượt điểm và chặn xóa làm điểm âm là thêm so với gốc (gốc không kiểm).
- **T4** Mã khách trên hộp tặng điểm dùng danh sách khách có hồ sơ, không có khách vãng lai.
