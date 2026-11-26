# Nghiệm thu, kế hoạch, rủi ro

## Tiêu chí

Mỗi ca giao diện đi qua sự kiện thật của ô, và chạy ở **cả hai** cài đặt `en-US` và `vi-VN` (đổi `CurrentCulture` trong test).

- [ ] A1. Hàm đọc: đủ bảng ở N1.
- [ ] A2. Mỗi loại ô ngoài lưới: `2.5` và `2,5` ra 2,5; `1600` ra 1600 và hiện `1.600`.
- [ ] A3. Đang sửa không có dấu ngăn nghìn; số có phần lẻ vào rồi ra giữ nguyên.
- [ ] A4. Vào rồi ra không đổi: không đánh dấu đã sửa; kiểu giảm giá (tiền hay %) không đổi.
- [ ] A5. Ô tiền: gõ `15,5` hiện `15,5`, giá trị đúng 15,5.
- [ ] A6. Không đọc được: giữ số cũ, không hộp lỗi; câu báo chỉ ở chỗ N6 nêu.
- [ ] A7. Định lượng mặt hàng: `0.125` và `0,125` lưu 0,125 (đọc lại từ CSDL).
- [ ] A8. Món mặc định khu vực: `2,5` lưu 2,5.
- [ ] A9. Gõ xong bấm thẳng nút Thêm hoặc Lưu: dùng đúng số vừa gõ.
- [ ] A10. N4: trong lưới, gõ phím thật `2.128` còn `2,12` (định lượng giữ được 4 số lẻ); ô ngoài lưới `1.128` thành `1,13`, giá trị dùng đúng 1,13. N5: ô số nguyên gõ `2.5` giữ số cũ.
- [ ] A11. Excel theo N7: `1.250` ra 1250 ở cột Số lượng và Đơn giá; `0.125`, `0,125` ra 0,125 ở cả hai; `1,250` giữ như cũ; ô số thật giữ đúng số.
- [ ] A12. Không còn `new NumericUpDown` trực tiếp ngoài lớp ô số chung (kiểm bằng tìm chữ).
- [ ] A13. Công thức tiền không đổi: selftest cũ vẫn đạt, trừ ca kiểm cách đọc cũ (liệt kê từng ca; giữ tên, đổi nội dung kiểm như Chiến chốt 26/11).

## Kế hoạch implement (tách 2 patch)

1. **Patch 1:** ô ngoài lưới. Lớp ô số chung kế thừa `NumericUpDown` (ghi đè đọc, hiện, lọc phím), dùng `NumberInput`; thay ở mọi chỗ trong [`giao-dien.md`](giao-dien.md).
2. **Patch 2:** lưới Báo giá, định lượng, món mặc định khu vực (dùng `NumericEditGridCell`); chặn chữ số lẻ (N4) cho cả lưới Đặt hàng; N7 cho Excel.

File dùng chung bị chạm: `DynamicForm.cs`, có thể `ThemeApplier.cs`. File đã quá 500 dòng không được dài thêm đáng kể. Không migration.

## Rủi ro

- **Gõ theo thói quen ngăn nghìn:** `15.000` vào ô tiền thành 15. Chiến đã chốt vì người dùng không tự gõ dấu ngăn nghìn. Ô hiện lại ngay `15`.
- Dán số từ nơi khác (`15.000`) cũng thành 15.
- **Dữ liệu đã lưu có thể đã sai** do cách đọc cũ (định lượng 125 thay vì 0,125). Chưa rà dữ liệu thật; spec không tự sửa.
- `NumericUpDown` đọc giá trị ở nhiều thời điểm (rời ô, Enter, đọc `Value`): phải kiểm đủ, nếu sót thì rơi về cách đọc của Windows.

## Câu hỏi mở

Không còn. C1 ("Y hệt app gốc"), C2 ("Giữ số cũ"), C3 ("Là 0,125") Chiến chốt 26/11. Còn một đề xuất nhỏ ở N4 (dán chuỗi thừa chữ số) được duyệt cùng spec.
