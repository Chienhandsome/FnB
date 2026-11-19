# POS TAP — nghiệp vụ khai báo khu vực, bàn, báo giá

Nguồn: STABLEDESC `DKHUVUC__9e2afcb8` (`DKHUVUCAe`, 334 dòng), `DBAN__f2288345` (`DBANAe`, 196), `TBAOGIA__b2d3ffd4` (`TBAOGIAAe`, 394). Giá giờ / bảng giá xem [file danh mục](pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md).

## 1. Khu vực (`DKHUVUC`)

- Thuộc tính: tên, biểu tượng `DBIEUTUONGID` (ảnh bàn; mặc định theo ứng dụng `Shared.GetIcon()`), kho `DKHOHANGID` (kho xuất hàng cho mọi bill ở khu vực; mặc định kho của cửa hàng), mẫu hóa đơn `STEMPLATEID` (chỉ cho chọn mẫu của form hóa đơn bán hàng `5f029a5b-8ec2-4229-bd1d-03fed9569db2`), tab hiển thị `TABHIENTHI` (0 = tab trên, 1 = tab dưới), `SORTORDER`, `STATUS`, giá mặt hàng áp dụng `DGIAMATHANGID` (mặc định "0" = giá bán 1; các lựa chọn giá 2/3/4 chỉ có khi `SuDungGia2/3/4=30`, nhãn lấy theo `DienGiaiGia2/3/4`; còn có lựa chọn "hiển thị để chọn"), khung giờ áp giá riêng `TUGIO`–`DENGIO` + giá `DGIATHEOGIOID` (chỉ hiện khi `SuDungGiaTheoGio=30`).
- Cách tính tiền giờ của khu vực (chỉ khi `Shared.CoTienGio`): **theo giờ** (đơn giá `DONGIA` mỗi giờ) hoặc **theo bảng giá** (`DBANGGIAID`); `TIENMOBAN` (tiền mở bàn: phụ thu cố định). Khi bàn chọn "theo khu vực" thì dùng cấu hình này.
- Mặt hàng mặc định tự thêm khi mở bàn (`SuDungMatHangMacDinh=30`, bảng `DMATHANGMACDINH`: mặt hàng + số lượng > 0; bắt buộc chọn mặt hàng và nhập số lượng).
- Nhiều bảng giá áp dụng trên một bill (`ChoPhepDungNhieuBangGiaTrenBill=30` và theo bảng giá): chọn các bảng giá được phép tại khu vực (`DBANGGIATHEOKHUVUC`).
- Phân quyền truy cập theo khu vực (`PhanQuyenTruyCapTheoKhuVuc=30`): tick người dùng không phải admin được vào khu vực (`DTKTHEOKHUVUC`).
- Lưu: chọn "theo bảng giá" bắt buộc chọn bảng giá; ghi `CACHTINHGIO = THEOGIO` hoặc `THEOBANGGIA`.

## 2. Bàn / phòng (`DBAN`)

- Cách tính giờ riêng của bàn: **theo khu vực** (mặc định khi tạo mới), theo giờ (`DONGIA`/giờ) hoặc theo bảng giá (`DBANGGIAID`); `TIENMOBAN`; có thể gắn thêm nhiều bảng giá khác (`DBANGGIATHEOBAN`) nếu `ChoPhepDungNhieuBangGiaTrenBill=30`. Chọn "theo bảng giá" thì bắt buộc chọn bảng giá.
- Không dùng tiền giờ (`Shared.CoTienGio = false`): ẩn toàn bộ phần cách tính giờ.
- Công tắc điện (`CoSuDungCongTacDienTu=30`, chỉ admin): IP thiết bị điều khiển (`IPCONGTAC`) và nút bật / tắt thử (`Shared.BatDen / TatDen`); bàn mở thì bật đèn, đóng bàn thì tắt.
- Trạng thái vận hành của bàn: `TDONHANGID` (bill đang mở), `TRANGTHAI` (1 báo dọn, 2 dọn dẹp), `TAIKHOANGIU`/`GIULUC` (khóa bàn nhiều máy trạm), nhóm hiển thị `DNHOMHIENTHI`, loại phòng `DLOAIPHONG` (khách sạn), vị trí / kích thước / hình dạng khi dùng giao diện thiết kế.

## 3. Báo giá (`TBAOGIA`, chi tiết `TDATHANGCHITIET`-tương tự)

Phiếu báo giá cho khách (không tác động kho, công nợ hay quỹ):
- Chọn khách hàng thì tự điền tên, địa chỉ, điện thoại, email (khóa các ô nhập tay); không chọn thì nhập tay.
- Chi tiết: mặt hàng, đơn vị (2 đơn vị: nhập số lượng lẻ / chẵn qua `NhapSoLuong`, đơn giá `GIABAN` / `GIABANCHAN`), số lượng, đơn giá, % giảm, thành tiền; gộp dòng cùng mặt hàng + đơn vị; nhập từ Excel (Mã hàng hoặc Tên hàng, Số lượng, ĐVT, đơn giá, % giảm).
- Tổng như phiếu nhập kho: `TIENHANG = ΣTHANHTIEN`; giảm giá `%`/tiền (`TIENGIAMGIA = 100 × Round(TIENHANG × TL / 10000)`; theo tiền thì `TL = 100 × tiền / TIENHANG`); `TIENTHUE = TILETHUE × (TIENHANG − TIENGIAMGIA)/100`; `TONGCONG = TIENHANG − TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN`.

## Bổ sung vòng 2 (19/11/2026)

- **[Sửa v2]** `DTKTHEOKHUVUC` **có màn nhập**: tab "Truy cập" trong form khu vực (`DKHUVUCAe`), chỉ hiện khi `PhanQuyenTruyCapTheoKhuVuc == 30` (nhận định "không có màn nhập" của vòng 1 là sai). Không làm màn này thì cấu hình `PhanQuyenTruyCapTheoKhuVuc` vô dụng — nên làm theo mẫu đó.
- Nhớ phân biệt: máy in bếp theo khu vực lưu ở `app.dat` section `INKHUVUC` (key = `DKHUVUC.ID`, 1 khu vực = 1 máy in) / registry `EasyReg`; `DTKTHEOKHUVUC` chỉ là phân quyền user↔khu vực.
