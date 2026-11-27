# Spec: Danh mục cửa hàng (DCUAHANG)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; Chiến chốt 26/11/2026, issue #19, hết câu hỏi mở), loại nặng.
- Base: `main` @ `9d7f8d2`
- Menu: HOẠT ĐỘNG > Danh mục cửa hàng (`SMENU.SORTORDER = ZZZ003`, `STABLEDESC` `71442d6c-3236-44af-b312-21042b8ea94f`).
- Menu có `VISIBLECONFIGID` = cấu hình `NhieuCuaHang`; bản chép CSDL đang **0**, nên menu đang ẩn.

## Bối cảnh

Màn danh mục chuẩn do khung `No1Lib` dựng từ metadata. Mã riêng của form rất ít (`POS_TAP_SRC_EXPORT/STABLEDESC/DCUAHANG__71442d6c/code.cs`). Cửa hàng được dùng để lọc báo cáo, quỹ, và chọn kho mặc định khi đăng nhập. Các màn báo cáo của bản mới đã đọc `DCUAHANG`.

## Mục tiêu

Xem, thêm, sửa, xóa cửa hàng với cùng trường và ràng buộc như app gốc, trên cấu trúc CSDL hiện có.

## Phạm vi

- **Làm:** màn lưới, hộp thoại thêm/sửa, quyền, ẩn/hiện menu theo `NhieuCuaHang`.
- **Không làm:** gán người dùng vào cửa hàng (`TNGUOIDUNGTHEOCUAHANG`), thiết kế mẫu hóa đơn, danh mục kho, đổi cấu trúc CSDL.

## Các phần

| File | Nội dung |
|---|---|
| [`nghiep-vu.md`](nghiep-vu.md) | Trường, ràng buộc, quyền, cửa hàng mặc định |
| [`giao-dien.md`](giao-dien.md) | Lưới và hộp thoại |
| [`nghiem-thu.md`](nghiem-thu.md) | Tiêu chí, kế hoạch, nguồn, câu hỏi mở |
