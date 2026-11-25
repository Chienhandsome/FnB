# Spec: Danh mục kho hàng (DKHOHANG)

- Trạng thái: **NHÁP** (Claude viết 26/11/2026). Phân loại **nặng** (workflow-agy §10.1); chưa duyệt, chờ Chiến ở issue #20.
- Base: `main` @ `82d9f37`
- Menu: KHO HÀNG > Danh mục kho hàng (`SMENU.SORTORDER = ZZZ001`, `STATUS = 30`, không có `VISIBLECONFIGID`; `STABLEDESC` `221299bf…`).

## Bối cảnh

Màn danh mục chuẩn của khung `No1Lib`; bản xuất **không có `code.cs`** cho form (`STABLEDESC/DKHOHANG__221299bf` chỉ có `layout.xml`, `designcode.cs`). Kho được chọn mặc định theo cửa hàng khi đăng nhập, gắn cho khu vực bán và cho phiếu kho. Nhiều màn báo cáo của bản mới đã đọc `DKHOHANG`.

## Mục tiêu

Xem, thêm, sửa, xóa kho với cùng trường và ràng buộc như app gốc, không đổi cấu trúc CSDL.

## Phạm vi

- **Làm:** màn lưới, hộp thoại thêm/sửa, quyền.
- **Không làm:** phiếu nhập, xuất, chuyển, kiểm kê; tồn kho; luật kiểm tồn khi bán (cờ âm kho chỉ được **lưu**, việc áp dụng thuộc spec bán hàng và kho).
- App gốc chặn màn này khi bản quyền dưới mức "chuyên nghiệp" (`SharedCode`, kiểm `licMode < 3`). Bản mới không dựng cơ chế bản quyền.

## Các phần

| File | Nội dung |
|---|---|
| [`nghiep-vu.md`](nghiep-vu.md) | Trường, ràng buộc, quyền, xóa |
| [`giao-dien.md`](giao-dien.md) | Lưới và hộp thoại |
| [`nghiem-thu.md`](nghiem-thu.md) | Tiêu chí, kế hoạch, nguồn, câu hỏi mở |
