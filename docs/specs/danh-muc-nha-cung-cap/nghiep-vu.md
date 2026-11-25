# Nghiệp vụ

## 1. Nhà cung cấp (`DNHACUNGCAP`)

| Cột | Nhãn | Ràng buộc | Hiện ở ô chọn |
|---|---|---|---|
| `MANHACUNGCAP` | Mã nhà cung cấp | bắt buộc, không trùng; mặc định `{NCC(*****)}` | không |
| `NAME` | Tên nhà cung cấp | bắt buộc, không trùng | có |
| `DNHOMNHACUNGCAPID` | Nhóm nhà cung cấp | không | không |
| `DIACHI` | Địa chỉ | không | có |
| `DIENTHOAI` | Điện thoại | không | có |
| `EMAIL` | Email | không | không |
| `WEBSITE` | Website | không | không |
| `NOTE` | Ghi chú | không | không |

Các cột chữ dài 255, collation `UNICODE_CI_AI`. Bảng không có cột tiền hay công nợ. Dòng mới: `ID` GUID, `STATUS = 30`, cột người và giờ tạo/sửa. Không thấy trigger. Bản chép: 0 nhà cung cấp.

**So trùng:** ở màn khách hàng, app gốc so đúng theo collation (không phân biệt hoa thường và dấu, không cắt khoảng trắng; thử 25/11). Màn này chưa thử; xem Q2.

**Mã tự sinh:** ở màn khách hàng, mã kế tiếp là số lớn nhất trong các mã đúng mẫu cộng 1, mã khác dạng bị bỏ qua, sửa tay được. Màn này chưa thử; xem Q2.

## 2. Nhóm (`DNHOMNHACUNGCAP`)

Bảng cây (`NAME`, `NOTE`, `SIMAGEID`, `PARENTID`, `PARENTDIR`, `ITEMTYPE`, `SORTORDER`, `AUTOID`). Bản xuất không có form riêng cho bảng này; ràng buộc của tên nhóm chưa đọc (Q5).

## 3. Quyền

`STABLEDESC.SFUNCTIONID` `7ffea55c-2b6e-4b08-9470-4fe5b6b5f0b9`. Bản chép: Quản lý 240, Kế toán 240, **Thủ kho 80** (Xem, Thêm; không Sửa, không Xóa), Phục vụ 0.

## 4. Xóa

Quy ước: xóa cứng. Tham chiếu nhà cung cấp: `TDONHANG.DNHACUNGCAPID`, `TTHUCHI.DNHACUNGCAPID`. Tham chiếu nhóm: `DNHACUNGCAP.DNHOMNHACUNGCAPID` và nhóm con qua `PARENTID`. Luật xem Q1.
