# Nghiệp vụ

## 1. Trường (`DKHOHANG`)

| Cột | Nhãn | Kiểu | Ràng buộc |
|---|---|---|---|
| `NAME` | Tên kho hàng | VARCHAR(255), `UNICODE_CI_AI` | bắt buộc, không trùng (`SCOLUMN` và `mapper`) |
| `DCUAHANGID` | Cửa hàng | tham chiếu `DCUAHANG` | **bắt buộc** (`mapper`, `AllowEmpty = False`) |
| `CHOPHEPAMKHO` | Cho phép âm kho | INTEGER | không |
| `SIMAGEID` | Ảnh | tham chiếu thư viện ảnh | không |
| `NOTE` | Ghi chú | VARCHAR(255) | không |

Cột khung (`SORTORDER`, `PARENTID`, `PARENTDIR`, `ITEMTYPE`, `AUTOID`) không có ô nhập. Dòng mới: `ID` GUID, `STATUS = 30`, cột người và giờ tạo/sửa. Không thấy trigger trên `DKHOHANG`. Bản chép có 1 kho "KHO BÁN HÀNG".

Menu bị ẩn hay không: không có `VISIBLECONFIGID`; chưa thấy `No1System_OnCustomSeeMenu` nhắc tới danh mục kho.

## 2. Cờ âm kho

Các form bán hàng, chuyển kho, xuất kho chỉ kiểm tồn khi `CHOPHEPAMKHO == 0` (`Hóa đơn bán hàng` dòng 415 và 2043; `Phiếu chuyển kho` dòng 179; `Phiếu xuất kho` dòng 154). Giá trị khi tích theo quy ước chung của app là 30; **chưa đọc được chỗ ghi** (Q2). Spec này chỉ lưu cờ, không đổi luật kiểm tồn.

## 3. Quyền

Chức năng "Danh mục kho hàng" (`STABLEDESC.SFUNCTIONID` `49cc3a6a-afc6-4efb-ad5d-af9da000f4c7`; `SMENU.SFUNCTIONID` trống). Bản chép: Quản lý 240, Kế toán 240, **Thủ kho 112** (Xem, Sửa, Thêm; không Xóa), Phục vụ 0.

## 4. Xóa

Quy ước dự án: xóa cứng. Cột tham chiếu kho: `DCUAHANG.DKHOHANGID`, `DKHUVUC.DKHOHANGID`, `TDONHANG.DKHONHAPID`, `TDONHANG.DKHOXUATID`, `TDONHANGCHITIET.DKHOHANGID`, `TLUUTAM.DKHOXUATID`. Luật khi kho đang được tham chiếu **chưa xác định** (Q1).
