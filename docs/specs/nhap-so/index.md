# Spec: Nhập số dùng chung cho mọi ô số

- Trạng thái: **NHÁP**
- Ngày: 2026-11-26. Base: `origin/main` @ `5bb66f2`.
- Nguồn quyết định: Chiến chốt 26/11/2026 trong phiên (cũng ghi ở [`../khach-dat-hang.md`](../khach-dat-hang.md), đợt 2, điểm 5).
- Loại: **nặng** (số lượng, giá, tiền). Khác app gốc (Chiến chốt).

## Bối cảnh

Mỗi loại ô đang đọc số một kiểu, có ô theo cài đặt Windows. Thử 26/11 **bằng đoạn mã đọc số giống app, ngoài app**, máy en-US:

| Ô | Gõ | App hiểu |
|---|---|---|
| Định lượng của mặt hàng | `0.125` | **125** |
| Số lượng món mặc định của khu vực | `0,125` | **125** |
| Ô tiền không số lẻ (loại ô ngoài lưới) | `15.000` | **15** (đúng quy tắc mới) |
| Cùng ô đó | `0.5` | hiện `1`, giá trị thật 0,5 |
| Ô số ngoài lưới | `2,5` | **25** (máy cài vi-VN thì `2.5` ra 25) |

Lưới Khách đặt hàng đã theo quy tắc mới (`bf45f06`), dùng `Services/NumberInput.cs` và `Dialogs/NumericEditGridCell.cs`.

## Mục tiêu

Mọi chỗ người dùng gõ số đọc giống nhau, không phụ thuộc cài đặt Windows, và số hiện trên ô đúng bằng số được dùng.

## Phạm vi

**Làm:** mọi ô số và cột số sửa được đang có trong app (danh sách ở [`giao-dien.md`](giao-dien.md)), kể cả Báo giá (menu vẫn ẩn); một thay đổi nhỏ ở luật nhập Excel của Báo giá.

**Không làm:** định dạng số ở màn chỉ xem và báo cáo; ngày giờ; đổi công thức hay làm tròn tiền (`LamTron`); đổi cấu trúc CSDL; sửa dữ liệu đã lưu; cấu hình cho phép số âm.

## Các file

| File | Nội dung |
|---|---|
| [`nghiep-vu.md`](nghiep-vu.md) | Quy tắc đọc, hiển thị, số lẻ, Excel |
| [`giao-dien.md`](giao-dien.md) | Danh sách ô và cách ô cư xử |
| [`nghiem-thu.md`](nghiem-thu.md) | Tiêu chí, kế hoạch implement, rủi ro, câu hỏi mở |
