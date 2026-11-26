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

**Lúc gõ (CHỐT, Chiến 26/11: "dấu chấm thành dấu phẩy, dấu phẩy tức là thập phân"):** bấm `.` thì ô hiện ngay `,`; chuỗi dán vào cũng đổi `.` thành `,`. Vậy ô đang sửa không bao giờ có dấu chấm. **Không chặn** dấu thứ hai: nếu nuốt phím thì `1.500.000` sẽ thành 1,5 mà không ai biết. Ô giữ đủ hai dấu, rời ô thì coi là không đọc được và giữ số cũ.

## N2. Hiển thị (CHỐT: app tự thêm dấu ngăn nghìn)

- Mọi ô trong phạm vi hiện kiểu Việt: ngăn nghìn `.`, thập phân `,`, kể cả ô chỉ đọc trên cùng hộp thoại (Tiền hàng, Tổng cộng). Máy en-US đang hiện `30,000` sẽ thành `30.000`.
- Khi đang sửa, ô hiện số **không có dấu ngăn nghìn** (`1500`, `2,5`).

## N3. Số hiện bằng số dùng (CHỐT cho ô tiền)

- Chiến chốt: ô tiền đang chỉ hiện số nguyên mà gõ `15,5` thì **hiện đúng 15,5**. Không có phần lẻ ẩn. Cách tính và làm tròn tiền không đổi.
- Áp cho mọi ô: chỉ hiện số chữ số lẻ đang có (`15.000`, không phải `15.000,00`; `2,5`, không phải `2,50`).

## N4. Quá số chữ số lẻ CSDL lưu được (CHỐT: "Y hệt app gốc")

Số chữ số lẻ tối đa của ô = của cột CSDL: **2** cho giá, tiền, %, số lượng trên phiếu, quy đổi, tồn; **4** cho định lượng (`DDINHLUONG.SOLUONG`).

| Loại ô | Cư xử | Ví dụ (ô 2 số lẻ) |
|---|---|---|
| Cột sửa trong lưới | **không gõ được** chữ số vượt mức | gõ `2.128` còn `2,12` |
| Ô ngoài lưới | gõ được, **làm tròn khi rời ô** | `1.128` thành `1,13` |

Số hiện, số tính và số lưu luôn trùng nhau.

- Bằng chứng: bấm thử app gốc 26/11, Khách đặt hàng (bản chép, chưa bấm Lưu): lưới gõ `2.125`, `2.128` đều còn `2.12`, giá 100.000 ra 212.000; ô Số lượng gõ `1.128` thành `1.13`. Ảnh: `_tmp/_origin-shots/nhap-so/`.
- Chỉ thử cột và ô Số lượng. Ô khác, gồm lưới định lượng (4 số lẻ), áp theo cùng luật, **chưa thử trên app gốc**. Điểm giữa (`1.125`) chưa thử: bản mới làm tròn AwayFromZero theo quy ước dự án.
- **ĐỀ XUẤT:** dán chuỗi thừa chữ số vào ô lưới thì cắt phần thừa, như khi gõ.
- Lưới Đặt hàng đã push (`bf45f06`) chưa giới hạn số chữ số lẻ; sửa trong spec này.

## N5. Ô chỉ nhận số nguyên (CHỐT: giữ số cũ)

Số chữ số khi thêm nhanh bàn, cấu hình kiểu số nguyên, ô nào của form động khai 0 chữ số lẻ **và không phải tiền**: gõ số lẻ (`2.5`) coi như không đọc được, giữ số cũ. N4 không áp cho loại ô này.

## N6. Không đọc được, ngoài khoảng

- Không đọc được: giữ số cũ. Chỉ chỗ đang có câu báo thì giữ câu báo (cột Số lượng của lưới Đặt hàng và Báo giá: "Mời bạn nhập số lượng"). Không thêm câu mới.
- Ngoài khoảng cho phép của ô (âm, % quá 100): xử lý như hiện nay của từng ô. Spec này không đổi giới hạn nào.

## N7. Nhập Excel (Báo giá) (CHỐT)

Quy tắc N1 **không** áp cho file Excel. Giữ luật trong `QuoteExcelImporter` (ô định dạng số lấy đúng số), chỉ đổi với ô chữ:

- `1.250` là **1250** ở mọi cột, kể cả cột Số lượng (hiện báo lỗi vì mơ hồ). Cột % giảm ra 1250 thì bị giới hạn 0–100 chặn.
- Bắt đầu bằng `0` rồi tới dấu thì dấu đó là thập phân ở mọi cột: `0.125`, `0,125` là **0,125** (hiện cột Đơn giá đọc `0,125` thành 125).
- Dấu phẩy trong các trường hợp khác giữ như hiện nay (cột Đơn giá: `15,000` là 15000; cột Số lượng, %: `1,250` báo lỗi).
