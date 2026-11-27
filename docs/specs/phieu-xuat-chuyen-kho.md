# Spec: Xuất khác (`LOAI = 2`) và Chuyển kho (`LOAI = 3`), cùng lưới quản lý

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; điểm **TẠM** ở §8).
- Ngày: 2026-11-27. Loại: **nặng** (kho). Nguồn: `docs/deep/kho-hang.md`, `phieu-nhap-kho.md` (cùng khung).
- Menu: KHO HÀNG ▸ Xuất khác, Chuyển kho (mỗi menu một lưới quản lý và một hộp phiếu).

## 1. Phạm vi

Làm: hộp phiếu, ghi `TDONHANG` + `TDONHANGCHITIET`, kiểm tồn, quy đổi 2 ĐVT, lưới quản lý, sửa/xóa, quyền, lưu vết. Không làm: in phiếu, nhập Excel, hạn sử dụng/kích thước bắt buộc, thuế/phí cho phiếu chuyển.

## 2. Xuất khác

- Đầu phiếu: Ngày (khóa như phiếu nhập), Số phiếu (tự sinh `PX` + yy + / + 5 chữ số, TẠM), Kho xuất (bắt buộc), Nhân viên xuất, Nhà cung cấp (tùy chọn, dùng cho xuất trả hàng), Diễn giải, Ghi chú.
- Dòng: mặt hàng, ĐVT, Số lượng (`SLXUATCHUAQUYDOI`; `SLXUAT = QUYDOI × …` khi xuất theo ĐVT chẵn), Đơn giá mặc định `GIABAN` (hoặc `GIANHAP`/`GIAVON` theo cấu hình "giá sử dụng khi chuyển, xuất kho" = 0/1/2), Thành tiền. Không có ô chiết khấu.
- **Công thức (T1):** `THANHTIEN = Round(SL × DONGIA, 0)`; `TIENHANG = ΣTHANHTIEN`; `TILETHUE = TIENTHUE = PHIVANCHUYEN = 0`; `TONGCONG = TIENHANG − TIENGIAMGIA`.
- Lưu: mọi dòng `SLXUAT > 0`; dòng ghi `DKHOHANGID` = kho xuất; `LOAI = 2`, `DKHOXUATID`, `STATUS = 30`, `DATHANHTOAN = 30`.
- **Kiểm tồn:** khi kho xuất không cho âm (kho tắt "cho phép âm kho" và cấu hình toàn hệ thống mặc định chặn): tổng `SLXUAT` theo mặt hàng không được vượt tồn hiện có của kho (ĐVT cơ sở). Vượt: không lưu, nêu tên mặt hàng, tồn có, số xuất.

## 3. Chuyển kho

- Đầu phiếu: Ngày, Số phiếu (`CK` + yy + / + 5 chữ số, TẠM), Kho xuất (bắt buộc), Kho nhập (bắt buộc, **khác** kho xuất: "Kho nhập và kho xuất không được phép trùng"), Nhân viên xuất, Nhân viên nhập, Diễn giải, Ghi chú.
- Dòng người dùng nhập là dòng nhập: `SLNHAPCHUAQUYDOI`, `SLNHAP`, `DKHOHANGID` = kho nhập. Mỗi lần lưu ghi thêm **dòng xuất đối ứng** cùng mặt hàng: `SLXUAT = SLNHAP`, `SLXUATCHUAQUYDOI = SLNHAPCHUAQUYDOI`, `DKHOHANGID` = kho xuất; sửa phiếu thì xóa dòng xuất đối ứng cũ rồi tạo lại trong cùng transaction. Lưới chi tiết chỉ hiện dòng `SLNHAP > 0`.
- Giá: đơn giá theo cấu hình giá xuất như trên; `THANHTIEN = Round(SL × DONGIA, 0)`. **Thuế và phí vận chuyển giữ như app gốc (Chiến chốt 27/11):** phiếu có ô Tỉ lệ thuế, Phí vận chuyển, Giảm giá; `TIENGIAMGIA`, `TIENTHUE`, `TONGCONG` tính đúng như phiếu nhập (`phieu-nhap-kho.md` §2). Lưới quản lý chuyển kho vẫn không có cột thuế/phí như gốc.
- **Kiểm tồn kho nguồn (T3):** khi kho xuất không cho âm: tổng số lượng chuyển theo mặt hàng, **đã quy đổi về ĐVT cơ sở (`SLNHAP`)**, không được vượt tồn kho xuất (bản gốc so số chưa quy đổi, sai khi dùng ĐVT chẵn).

## 4. Công thức tồn (dùng chung, không viết công thức thứ hai)

Tồn của mặt hàng ở một kho = `Σ(SLNHAP − SLXUAT)` trên dòng chi tiết có `DKHOHANGID` = kho đó, của phiếu `TDONHANG.STATUS = 30` và (`LOAI <> 0` hoặc (`LOAI = 0` và `DATHANHTOAN = 30`)). Dùng lại hàm tồn đã có của bán hàng (`TableService`, `ServiceOrderRepository`, `WarehouseRepository`); nếu hàm đó khác công thức này thì thống nhất về công thức này và ghi trong brief.

## 5. Sửa, xóa phiếu

Sửa: ghi lại chi tiết trong một transaction, kiểm tồn lại với số mới. Xóa: xóa cứng phiếu và mọi dòng (cả dòng đối ứng của chuyển kho), ghi lưu vết. Chặn khi sửa/xóa làm tồn một kho thành âm mà kho không cho âm (cùng T3 của phiếu nhập).

## 6. Lưới quản lý

- Quản lý xuất kho: Ghi chú, Số phiếu, Ngày, Tổng cộng, Tiền giảm giá, Tiền hàng, Kho xuất, Nhân viên xuất (thuế, phí luôn 0 nên không hiện).
- Quản lý chuyển kho: Ghi chú, Số phiếu, Ngày, Tổng cộng, Kho xuất, Kho nhập, Nhân viên xuất, Nhân viên nhập, Diễn giải.
- Người dùng không phải admin chỉ thấy phiếu có kho thuộc cửa hàng mình (áp cả cho chuyển kho, T4). Quyền: "Xuất kho" và "Chuyển kho" (Xem, Thêm, Sửa, Xóa). Xuất Excel qua `GridExcelExporter`.

## 7. Nghiệm thu (selftest tự tạo và dọn dữ liệu)

1. Xuất khác: lưu 2 mặt hàng, tồn giảm đúng; `THANHTIEN`, `TONGCONG` đúng; vượt tồn bị chặn khi kho không cho âm, cho khi kho cho âm.
2. Chuyển kho: lưu tạo đủ dòng nhập (kho nhập) và dòng xuất đối ứng (kho xuất); tồn kho nguồn giảm, kho đích tăng đúng; kho nhập trùng kho xuất bị chặn.
3. ĐVT chẵn: chuyển 2 thùng (quy đổi 24) kiểm tồn theo 48, không theo 2.
4. Sửa chuyển kho: dòng đối ứng được tạo lại, không nhân đôi; xóa phiếu xóa cả dòng đối ứng; lỗi giữa chừng rollback.
5. Lưới: lọc, quyền theo cửa hàng, xuất Excel; Số phiếu dài 255 ký tự không làm lưu vết tràn; không đổi cấu trúc CSDL; ca selftest cũ giữ tên.

## 8. Điểm TẠM

- **T1** Công thức `THANHTIEN` xuất/chuyển chưa thấy trong mã gốc (nằm ở framework): lấy `SL × DONGIA` như phiếu nhập và bán lẻ.
- **T2** (đã chốt, Chiến 27/11) Thuế/phí phiếu chuyển giữ như app gốc, không ép 0.
- **T3** Quy đổi đúng ĐVT khi kiểm tồn chuyển kho.
- **T4** Bản gốc không lọc theo cửa hàng ở lưới chuyển kho; bản mới lọc cho an toàn.
- **T5** Số phiếu `PX`/`CK` tự sinh là suy đoán.
