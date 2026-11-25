# Spec: Danh mục loại phòng (DLOAIPHONG)

- Trạng thái: **TẠM BỎ, ngoài phạm vi** (Claude rà 26/11/2026). Không code. Chiến muốn làm thì đổi lại **NHÁP** rồi trả lời Q1, Q2.
- **Lý do:** `SharedCode`, hàm `No1System_OnCustomSeeMenu`, ẩn menu này khi ứng dụng không phải HOTEL hoặc HOTEL_KARA (`IsHotel`); "Thông báo sửa chữa" cũng vậy. Dự án là F&B thuần COFFEE + POS (`workflow-agy.md` §8).
- Base: `main` @ `a479d7d`
- Menu: HOẠT ĐỘNG > Danh mục loại phòng (`SMENU.SORTORDER = ZZZ005`, `STATUS = 30`, `STABLEDESC` `4b737682…`).
- Lần chạy bản chép app gốc ngày 25/11, menu HOẠT ĐỘNG không hiện mục này (khớp lý do trên).

## Bối cảnh

Màn danh mục chuẩn của khung `No1Lib`. Loại phòng gắn cho bàn (`DBAN.DLOAIPHONGID`). Màn Theo dõi đặt phòng của app gốc và của bản mới nhóm phòng theo loại và dùng hai màu của loại.

## Mục tiêu

Xem, thêm, sửa, xóa loại phòng với cùng trường và ràng buộc như app gốc, không đổi cấu trúc CSDL.

## Phạm vi

- **Làm:** màn lưới, hộp thoại thêm/sửa có xem trước màu.
- **Không làm:** gán loại cho bàn (đã có ở Danh mục bàn khu vực), báo sửa chữa (`TSUACHUA`), tính giờ.

## Các phần

| File | Nội dung |
|---|---|
| [`nghiep-vu.md`](nghiep-vu.md) | Trường, ràng buộc, màu, quyền, xóa |
| [`giao-dien.md`](giao-dien.md) | Lưới và hộp thoại |
| [`nghiem-thu.md`](nghiem-thu.md) | Tiêu chí, kế hoạch, nguồn, câu hỏi mở |
