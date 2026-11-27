# Spec: Danh mục nhà cung cấp (DNHACUNGCAP)

- Trạng thái: **ĐÃ DUYỆT**, loại nặng; **hết câu hỏi mở** (Chiến chốt 26/11/2026, issue #21), **Claude tự duyệt 27/11/2026** theo workflow-agy §10.1.1 (mọi câu hỏi đã được Chiến trả lời).
- Base: `main` @ `6a6d713`
- Menu: KHO HÀNG > Danh mục nhà cung cấp (`SMENU.SORTORDER = ZZZ002`, `STATUS = 30`, không có `VISIBLECONFIGID`; `STABLEDESC` `1a86ea05…`, `TABLETYPE = 2`: cây nhóm bên trái, lưới bên phải).
- Khuôn: [`danh-muc-khach-hang.md`](../danh-muc-khach-hang.md) (cùng khung cây nhóm và lưới). Spec này chỉ ghi phần của nhà cung cấp.

## Mục tiêu

Xem, thêm, sửa, xóa nhà cung cấp và nhóm nhà cung cấp, cùng trường và ràng buộc như app gốc, không đổi cấu trúc CSDL.

## Phạm vi

- **Làm:** màn cây nhóm và lưới, hộp thoại nhà cung cấp, quản lý nhóm trên cây, quyền.
- **Không làm:** công nợ nhà cung cấp, phiếu nhập, phiếu thu chi.
- App gốc chặn màn này ở bản quyền thấp (`SharedCode`, quanh `TableIds.DNHACUNGCAP`; điều kiện chính xác chưa đọc hết). Bản mới không dựng cơ chế bản quyền.

## Các phần

| File | Nội dung |
|---|---|
| [`nghiep-vu.md`](nghiep-vu.md) | Trường, ràng buộc, mã tự sinh, nhóm, quyền, xóa |
| [`giao-dien.md`](giao-dien.md) | Cây, lưới, hộp thoại |
| [`nghiem-thu.md`](nghiem-thu.md) | Tiêu chí, kế hoạch, nguồn, câu hỏi mở |
