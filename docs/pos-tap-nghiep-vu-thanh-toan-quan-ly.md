# POS TAP — nghiệp vụ thanh toán và quản lý bán hàng

Nguồn: SFORM `Xác nhận thanh toán__96f6da47` (`code.cs` 409 dòng; bản cảm ứng `ts_code.cs` chưa đọc), `Quản lý bán hàng__e91d6463` (833 dòng), `Quản lý bán hàng nhà hàng__417e6d1d` (156 dòng). Liên quan: [bán lẻ](pos-tap-nghiep-vu.md), [nhà hàng](pos-tap-nghiep-vu-nha-hang.md), [máy tính bảng](pos-tap-nghiep-vu-may-tinh-bang.md).

## 1. Form `Xác nhận thanh toán` (`XacNhanThanhToan`)

Dùng chung cho bán lẻ và nhà hàng. Gọi: `SetData(mode, TONGTIEN, datTruoc, NoCu, khách, bill)`.
- Bán lẻ gọi `SetData(SuDungDichVu, TONGCONG, datTruoc, noCu + datTruoc, khách, bill)` ("nợ cũ đã bao gồm đặt trước"). Nhà hàng gọi `SetData(mode, TIENPHAITRA, đặtTrước, noCu, khách, bill)`. Trong form: `TONGTIEN_nội_bộ = TONGTIEN + NoCu − datTruoc`; ô "Tiền hóa đơn" hiện `TONGTIEN` gốc; ô "Tổng tiền" hiện `TONGTIEN_nội_bộ`.
- Hiện/ẩn các ô: đặt trước (≠ 0), nợ cũ (≠ 0), tiền hóa đơn (khi có đặt trước hoặc nợ cũ), khách đưa, thẻ ATM (`CoThanhToanThe`), chuyển khoản + tài khoản ngân hàng (`CoThanhToanChuyenKhoan`), voucher/coupon (`CoThanhToanVoucher`; `LuaChonVoucherTuDanhSach=30` thì chọn từ danh sách, ô chỉ đọc), thẻ trả trước (`SuDungTheTraTruoc`), trừ tích lũy (`SuDungDiemTichLuyDeThanhToan`), trả lại. `GiaBanLeUsd=30` → 2 số lẻ, ngược lại 0 số lẻ. Nút in tạm tính chỉ khi `ChoPhepInTamTinh=30` và `SuDungDichVu` (cần quyền `InTamTinh`).
- Mặc định "Khách đưa" = `max(0, TONGTIEN_nội_bộ − tích lũy − thẻ trả trước − ATM − chuyển khoản − coupon)`.
- **Trả lại** (`UpdateTraLai`): nếu `NoCu < 0` thì 0; nếu `coupon > TONGTIEN_nội_bộ` thì `TraLai = KhachDua`; ngược lại `max(0, KhachDua + coupon + ATM + chuyển khoản + thẻ trả trước + tích lũy − TONGTIEN_nội_bộ)`.
- Các thuộc tính trả về:
  - `TienTra = KhachDua + Voucher + TheTraTruoc + TruTichLuy + TienThe + ChuyenKhoan`.
  - `TienThanhToan = (Tổng tiền < 0) ? Tổng tiền : datTruoc + max(0, min(TienTra, TONGTIEN_nội_bộ))`. Tổng tiền < 0 nghĩa là khách đã đặt cọc dư: số âm là tiền phải hoàn lại, ô "khách đưa" bị khóa.
  - `TienNo = (Tổng tiền < 0) ? 0 : Tổng tiền − (TienThanhToan − datTruoc) − NoCu`.
  - `TienMat = max(0, KhachDua − TraLai)`; `LoaiThanhToan = 0` (luôn).
- Kiểm tra khi đóng (OK / "đóng bill không in" = `Retry`):
  1. Quyền `Functions.ThanhToan` (đăng nhập lại).
  2. Nếu `Tổng tiền > TienTra` (còn nợ): chỉ cho khi `ChoPhepKhachNo=30`, đã chọn khách, và người dùng xác nhận "cho khách nợ số tiền …".
  3. Thẻ trả trước: số dùng ≤ số còn lại của thẻ (`NhapTheTraTruoc.GetSoTienConLai(thẻ, bill)`), khi chọn thẻ tự điền `min(còn lại, TONGTIEN_nội_bộ)`; thẻ của khách (`DKHACHHANG.DTHETRATRUOCID`) dùng luôn nếu còn tiền.
  4. Có tiền chuyển khoản thì phải chọn tài khoản `DTAIKHOANNGANHANG`.
  5. Chế độ bán tại bàn: `Shared.DuLieuHoaDonOk(bill)` (kiểm tính hợp lệ dữ liệu hóa đơn, nằm trong DLL).
- Chế độ `QuanLyBanHang`/`DieuChinhHoaDon`: nạp lại các số đã lưu trên bill (`THETRATRUOC`, `TRUTICHLUY`, `DATTRUOC`, `NOCU`, `VOUCHER`, `THE`, `CHUYENKHOAN`, tài khoản, thẻ, voucher).
- Trừ tích lũy (`TruTichLuy`): chọn khách trước; giá trị tối đa bằng `TONGTIEN_nội_bộ`. Nhập mã coupon: `NhapCoupon` trả `DVOUCHERID` + giá trị.

## 2. Form `Quản lý bán hàng` (bán lẻ, `QuanLyBanHang`)

Danh sách bill theo khoảng ngày (mặc định ngày DB hiện tại) và cửa hàng `DCUAHANG`; không phải admin thì chỉ cửa hàng có trong `TNGUOIDUNGTHEOCUAHANG` của user. Chọn một bill để sửa sau thanh toán.
- Tính tiền dòng và tổng bill y hệt form `Hóa đơn bán hàng` (mục 1.1, 1.2 file chính; làm tròn `Math.Round` mặc định). Khác: dòng bán chỉ tính lại `SLXUAT` theo quy đổi khi cột đang sửa là `SLXUATCHUAQUYDOI`; đổi loại giá chỉ có giá 1–4 (không có "giá gần nhất"); thêm hàng mới: SL = 1, nếu 2 đơn vị thì cho chọn đơn vị, `DONGIA = GIABAN` (đơn vị cơ sở) hoặc `GIABANCHAN`, `SLXUAT = QUYDOI` cho đơn vị chẵn; hàng nguyên liệu bị loại khỏi tìm kiếm khi `MatHangCoVatTu=30` (`LOAIDINHLUONG <> 4`).
- Thanh toán nhiều hình thức: các ô tiền mặt, thẻ, chuyển khoản, voucher, trừ tích lũy, thẻ trả trước. `TIENTHANHTOAN = Σ` các ô này. Khi người dùng sửa trực tiếp "tiền thanh toán" (chỉ cho khi bill có không quá 1 hình thức thanh toán) thì giá trị đó ghi vào đúng hình thức đang dùng, các hình thức khác về 0.
- Lưu (`DoSave`):
  - Khách lẻ (chưa chọn khách) không được nợ: `TONGCONG > TIENTHANHTOAN` thì chặn.
  - Có chuyển khoản phải chọn tài khoản; có voucher và chọn-từ-danh-sách thì phải chọn voucher; có thẻ trả trước thì phải chọn thẻ và số dùng ≤ số còn lại của thẻ (tính cả bill hiện tại).
  - `DIEMGIAM = TRUTICHLUY / QuyDoi1DiemSangTien` (nếu ≠ 0).
  - Sau khi lưu bill: nếu `SuDung2DonViTinh = 0` đồng bộ `SLNHAP := SLNHAPCHUAQUYDOI`, `SLXUAT := SLXUATCHUAQUYDOI`; gán kho xuất cho mọi dòng; **xuất lại vật tư** (`HoaDonBanHang.XuatVatTu`, định lượng đệ quy).
  - Không có phần ghi lại `CONNO`, `CONLAI`, `DIEM`, điểm tích lũy hay nâng nhóm khách khi sửa (khác lúc thanh toán).
- **Hủy phiếu**: cần quyền xóa `Functions.QuanLyBanHang`; `HuyBill`: kiểm tra bill còn tồn tại; ghi lưu vết "Hủy đơn hàng"; lưu vào `TDONHANGHUY` + `TDONHANGHUYCHITIET` (tên khách, nhân viên, bàn, người thanh toán, thu ngân, giờ/ngày hủy, lý do, các số tiền; chi tiết: mã hàng, tên, ĐVT, SL = `SLXUAT − SLNHAP`, đơn giá, thành tiền); rồi xóa `TDONHANG`, `TDONHANGCHITIET`, `TINCHEBIEN` của bill. Lỗi trong mã gốc: cột `TIENGIAMGIA` của bill hủy được gán `TILEGIAMGIA` (tỉ lệ, không phải tiền).
- Tự làm tươi danh sách khi `TDONHANG` có thay đổi (`Config.NeedRefresh`).
- Lưu vết các thao tác sửa SL / xóa / thêm hàng nếu `KichHoatLuuVetHoatDong=30` (tiền tố "QLBH:").

## 3. Form `Quản lý bán hàng nhà hàng` (`QuanLyBanHangNhaHang`)

- Danh sách bill theo khoảng ngày, mặc định ngày giao dịch (`Shared.GetNgayGiaoDich`), sắp theo `NGAY, GIOTHANHTOAN`. Panel phải nhúng form `Hóa đơn nhà hàng` ở chế độ `QuanLyBanHang`, hoặc `DieuChinhHoaDon` khi mở từ menu `Menus.DieuChinhHoaDon` (khi đó ẩn nút hủy và khóa chọn ngày).
- **Hủy hóa đơn**: quyền `Functions.HuyHoaDon`; `NhapLyDoKhiHuyHoaDon=30` thì bắt nhập lý do, không thì lý do mặc định "Hủy hóa đơn" và xác nhận; dùng chung `QuanLyBanHang.HuyBill`.

## 5. Bổ sung vòng 2 (19/11/2026)

- **[Sửa v2 — lọc danh sách bill]** Lưới `grMain` của `Quản lý bán hàng` và `Quản lý bán hàng nhà hàng` chỉ hiện bill **`LOAI = 0 AND DATHANHTOAN = 30`** (cộng lọc ngày/cửa hàng): hai cột ẩn `2f410f17` ("Loại", `CONDITION =0`) và `1c24b19b` ("Đã thanh toán", `CONDITION =30`) trong blob FieldMap base64 của AELAYOUT. (Một hướng rà của vòng 2 từng kết luận "lưới không có điều kiện lọc" vì chỉ đọc text XML mà không giải mã blob base64 — kết luận đó sai, đã được sửa sau khi tự giải mã blob.)
- **Voucher trên bill hủy:** `HuyBill` **DELETE** dòng `TDONHANG` nên sau hủy `COUNT(*) WHERE DVOUCHERID` về 0 → voucher dùng lại được; voucher gắn trên bill nháp chưa thanh toán vẫn bị tính "đã dùng" (xem [file khách hàng](pos-tap-nghiep-vu-khach-hang-the.md) mục 3).
- **Chế độ "Điều chỉnh hóa đơn"**: `QuanLyBanHangNhaHang` chỉ truyền `Mode` vào form con; hành vi nút Thanh toán ở mode này không thấy trong code đã đọc (**[Chưa chốt]** — checklist BH-Q29).
- **Thanh toán — hai câu hỏi lớn còn mở:** (1) trừ kho thực sự ở đâu (không có `UPDATE` tồn trong form nào, kể cả MTB); (2) có transaction khi thanh toán không (0 hit) — phải xác minh trước khi code thanh toán (checklist BH-Q13). `TCHITIETTHANHTOAN` là bảng chết (0 tham chiếu): không clone trừ khi DB thật chứng minh còn dùng.
- Bản cảm ứng `XacNhanThanhToanTs` (`ts_code.cs`) **đã đọc vòng 2**: có đủ ATM/chuyển khoản (+ chọn tài khoản)/voucher/thẻ trả trước/trừ tích lũy/cho nợ (`chkKhachNo`, hỏi xác nhận, theo `ChoPhepKhachNo == 30`), ghi `SOTT`, `NGAY`, điểm, nâng nhóm — khác MTB chỉ tiền mặt. Desktop vs TS kiểm tra `MaxValue` khác nhau (`>=` vs `>`): chưa rõ đâu đúng (checklist BH-Q49).
