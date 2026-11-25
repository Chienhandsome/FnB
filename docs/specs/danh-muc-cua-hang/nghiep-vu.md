# Nghiệp vụ

## 1. Trường (`DCUAHANG`)

| Cột | Nhãn trên hộp thoại | Độ dài / kiểu | Ràng buộc (`SCOLUMN`, `mapper`) |
|---|---|---|---|
| `CODE` | Mã cửa hàng | VARCHAR(36) | bắt buộc, không trùng (`SCOLUMN`) |
| `NAME` | Tên cửa hàng | VARCHAR(255) | bắt buộc, không trùng (`SCOLUMN` và `mapper`) |
| `SIMAGEID` | Ảnh | tham chiếu thư viện ảnh | không |
| `DIACHI` | Địa chỉ | VARCHAR(255) | không |
| `DIENTHOAI` | Điện thoại | VARCHAR(255) | không (không thấy luật định dạng) |
| `DKHOHANGID` | Kho mặc định (`SCOLUMN` ghi "Kho hàng") | tham chiếu `DKHOHANG` | không |
| `STEMPLATEID` | Mẫu hóa đơn | tham chiếu mẫu in | danh sách chọn chỉ lấy `SFORMID = '5f029a5b-8ec2-4229-bd1d-03fed9569db2'` (`code.cs`, `lueSTEMPLATEID_CustomLoadData`) |
| `NOTE` | Ghi chú | VARCHAR(255) | không |

Cột khung (`SORTORDER`, `PARENTID`, `PARENTDIR`, `ITEMTYPE`, `AUTOID`) có trong `mapper` nhưng không có ô nhập. `STABLEDESC.TABLETYPE = 0`, `READONLY = 0`, `INVOICEPRINT = 0`, `BARCODEPRINT = 0`.

Dòng mới ghi theo quy ước chung: `ID` GUID, `STATUS = 30`, cột người và giờ tạo/sửa. Không thấy trigger trên `DCUAHANG`, `DKHOHANG`, `TNGUOIDUNGTHEOCUAHANG` (truy vấn `RDB$TRIGGERS`).

## 2. Sau khi lưu

App gốc gọi `Shared.Load()` (`code.cs`, `mapper_AfterSave`): nạp lại cửa hàng và kho đang dùng của phiên.

## 3. Cửa hàng mặc định của phiên (`SharedCode`, hàm `Shared.Load`)

- Admin: cửa hàng `STATUS = 30` đầu tiên theo `SORTORDER`.
- Người khác: dòng đầu của `TNGUOIDUNGTHEOCUAHANG` (`STATUS = 30`) theo tài khoản.
- Kho đang dùng là `DKHOHANGID` của cửa hàng đó.

Spec này không đổi luật trên.

## 4. Quyền

Chức năng "Danh mục cửa hàng" (`STABLEDESC.SFUNCTIONID` = `8fdde6f3-568b-439d-9c42-92b622228ff5`). Bản chép CSDL: nhóm Quản lý 240, Kế toán 240, Phục vụ 0. Cờ quyền và cách kiểm theo `AppSession.Can` đang dùng ở các danh mục khác.

## 5. Xóa

Quy ước dự án: xóa cứng. Các cột tham chiếu cửa hàng tìm được trong CSDL: `DKHOHANG.DCUAHANGID`, `TDONHANG.DCUAHANGID`, `TTHUCHI.DCUAHANGID`, `TNGUOIDUNGTHEOCUAHANG.DCUAHANGID`. Bản chép có 1 cửa hàng ("TRỤ SỞ CHÍNH", mã `CH1`, đã gắn kho), 1 kho trỏ tới cửa hàng, 0 dòng gán người dùng.

**Luật khi cửa hàng đang được tham chiếu chưa xác định** (nằm trong khung, form không có mã). Xem Q1 ở [`nghiem-thu.md`](nghiem-thu.md).
