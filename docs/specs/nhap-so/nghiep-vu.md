# Nghiệp vụ

Nhãn: **CHỐT** là Chiến đã quyết; **ĐỀ XUẤT** là chờ duyệt (xem câu hỏi ở [`nghiem-thu.md`](nghiem-thu.md)).

## N1. Đọc số người dùng gõ (CHỐT)

- Dấu chấm hay dấu phẩy đều là **dấu thập phân**. Không ai tự gõ dấu ngăn nghìn.
- Hợp lệ: bỏ khoảng trắng đầu cuối, tùy chọn dấu `-` ở đầu, chữ số, tối đa **một** dấu chấm hoặc phẩy.
- Từ hai dấu trở lên (`1.500.000`), chữ, trống: không đọc được.

| Gõ | Ra |
|---|---|
| `1600` | 1600 |
| `2.5`, `2,5` | 2,5 |
| `15.000` | 15 |
| `1.500.000` | không đọc được |

Không phụ thuộc cài đặt vùng của Windows.

## N2. Hiển thị (CHỐT: app tự thêm dấu ngăn nghìn)

- Mọi ô trong phạm vi hiện kiểu Việt: ngăn nghìn `.`, thập phân `,`, kể cả ô chỉ đọc trên cùng hộp thoại (Tiền hàng, Tổng cộng). Máy en-US đang hiện `30,000` sẽ thành `30.000`.
- Khi đang sửa, ô hiện số **không có dấu ngăn nghìn** (`1500`, `2,5`).

## N3. Số hiện bằng số dùng (CHỐT cho ô tiền)

- Chiến chốt: ô tiền đang chỉ hiện số nguyên mà gõ `15,5` thì **hiện đúng 15,5**. Không có phần lẻ ẩn. Cách tính và làm tròn tiền không đổi.
- Áp cho mọi ô: chỉ hiện số chữ số lẻ đang có (`15.000`, không phải `15.000,00`; `2,5`, không phải `2,50`).

## N4. Gõ quá số chữ số lẻ CSDL lưu được (ĐỀ XUẤT)

CSDL lưu 2 chữ số lẻ cho giá, tiền, %, số lượng trên phiếu, quy đổi, tồn; **4** cho định lượng (`DDINHLUONG.SOLUONG`).

- Đề xuất: làm tròn AwayFromZero **ngay khi rời ô**, tới đúng số chữ số CSDL lưu, rồi mới tính. Ví dụ số lượng `2,125` thành `2,13`, thành tiền tính theo 2,13.
- Hiện nay: lưới hiện `2,13` nhưng tính tiền theo 2,125, còn CSDL lưu số lượng đã làm tròn, nên thành tiền có thể lệch số lượng đã lưu (suy từ định dạng cột và kiểu cột, chưa thử).

## N5. Ô chỉ nhận số nguyên (ĐỀ XUẤT)

Số chữ số khi thêm nhanh bàn, các cấu hình kiểu số nguyên, ô nào của form động khai 0 chữ số lẻ **và không phải tiền**: gõ số lẻ (`2.5`) coi như không đọc được, giữ số cũ.

## N6. Không đọc được, ngoài khoảng

- Không đọc được: giữ số cũ. Chỉ chỗ đang có câu báo thì giữ câu báo (cột Số lượng của lưới Đặt hàng và Báo giá: "Mời bạn nhập số lượng"). Không thêm câu mới.
- Ngoài khoảng cho phép của ô (âm, % quá 100): xử lý như hiện nay của từng ô. Spec này không đổi giới hạn nào.

## N7. Nhập Excel (Báo giá)

Quy tắc N1 **không** áp cho file Excel. Giữ luật trong `QuoteExcelImporter` (ô định dạng số lấy đúng số), chỉ đổi:

- **CHỐT:** ô chữ `1.250` là **1250**, kể cả cột Số lượng (hiện báo lỗi vì mơ hồ).
- **ĐỀ XUẤT:** áp cùng cách cho cột % giảm (1250% bị chặn bởi giới hạn 0–100 đã chốt). Nhóm đầu là `0` thì là thập phân: `0.125` là 0,125.
- **ĐỀ XUẤT:** dấu phẩy giữ như hiện nay (cột Đơn giá: `15,000` là 15000; cột Số lượng, %: `1,250` báo lỗi), riêng `0,125` là 0,125 ở mọi cột (hiện cột Đơn giá đọc thành 125).
