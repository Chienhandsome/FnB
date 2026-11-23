# Spec (delta): Quản lý bán hàng + Điều chỉnh hóa đơn (sửa bill đã thanh toán)

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-23. Base: `main` @ `0e37e01` (local; `origin/main` @ `13d90f8`, đã `git fetch origin main`)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md). Spec gốc của bộ máy hóa đơn: [`su-dung-dich-vu.md`](su-dung-dich-vu.md) — spec này **chỉ ghi phần khác**; chỗ không nói = như `su-dung-dich-vu.md`.
- **Hai menu, một form.** Cả hai trỏ cùng `SFORM` `417e6d1d-ed16-4847-b752-65c3cefbe341` ("Quản lý bán hàng nhà hàng", lớp `QuanLyBanHangNhaHang`); chế độ chọn theo menu gọi (`CallerMenuID`):

| | Quản lý bán hàng | Điều chỉnh hóa đơn |
|---|---|---|
| `SMENU` | `65d26e6c-682f-40cf-84d3-ba02343c6223`, ZZZ015, không `MENUCODE` | `a6bfe97d-7e61-4c79-a6a3-e8437da57e5d`, ZZZ014, `MENUCODE = DieuChinhHoaDon` |
| `SFUNCTION` | `7051d85d-341e-4b84-a4f6-eff601bbbdb8` "Quản lý bán hàng" | `607326cf-2a24-476b-a069-404eeca616db` "Điều chỉnh hóa đơn" |
| `HoaDonMode` | `QuanLyBanHang` | `DieuChinhHoaDon` |
| Mục đích | Sửa lại bill **đã thanh toán** (mặt hàng, giảm giá, thanh toán…), hủy bill | Chỉ đổi khách hàng và trả đồ trên bill đã thanh toán, **không hủy** |

(Cùng nhóm HOẠT ĐỘNG; toolbar có nút "Quản lý bán hàng".) Màn bán lẻ `Quản lý bán hàng__e91d6463` / `Quản lý hóa đơn bán hàng__cda67ab3` là **form khác**, thuộc luồng bán lẻ — ngoài phạm vi.

## 0. Nguồn, giới hạn

- **[CODE]** `SFORM/Quản lý bán hàng nhà hàng__417e6d1d` (`code.cs` 156 dòng + `layout.xml`), `Hóa đơn nhà hàng__f3f7bb77` (các nhánh `mode ≠ SuDungDichVu`), `Xác nhận thanh toán__96f6da47`, `Quản lý bán hàng__e91d6463` (`QuanLyBanHang.HuyBill / LuuBillHuy / Track`). **[DB]** bản chép `DATA.fdb` (cấu hình, quyền, cột lưới). Nghiệp vụ chung: [`pos-tap-nghiep-vu-thanh-toan-quan-ly.md`](../pos-tap-nghiep-vu-thanh-toan-quan-ly.md), [`pos-tap-nghiep-vu-nha-hang.md`](../pos-tap-nghiep-vu-nha-hang.md).
- **[APP]: không có ảnh.** Công cụ giả lập chuột/bàn phím không điều khiển được app gốc trên máy này (xem `su-dung-dich-vu.md` §0), và DB demo có 0 bill đã thanh toán nên lưới trống. Mọi thứ về bố cục lấy từ code/`layout.xml`, đánh dấu **[CHƯA XÁC MINH BẰNG APP]**. Cần chụp bổ sung (Q9).

## 1. Bố cục UI (khác khuôn)

`KryptonSplitContainer` ngang: **trái = danh sách bill**, **phải = form hóa đơn** `TDONHANG0Ae` (đúng form của `su-dung-dich-vu.md`) nhúng vào `Panel2`, chỉnh theo chế độ (§3).
Danh sách trái (`grMain`, `DataSearch` trên `TDONHANG`, chỉ đọc, hàng chẵn nền xanh nhạt `229,247,252`, không thêm/xóa/sửa tại lưới, cho kéo đổi thứ tự cột):
- Bộ lọc điều kiện cố định của lưới (từ `ViewConfig`): **`LOAI = 0`** (chỉ bill nhà hàng/bán tại bàn) và **`DATHANHTOAN = 30`** (chỉ bill đã thanh toán). Thêm: **`NGAY` trong [Từ ngày, Đến ngày]** (`FilterDateRange` `dtNgay`), sắp **`NGAY, GIOTHANHTOAN`**.
- Cột hiển thị (30 cột đã cấu hình, 2 cột lọc ẩn): Số phiếu (`NAME`), Ngày, Khách hàng (`DKHACHHANGID` + Tên khách), Tổng cộng, Tiền giảm giá, Tỉ lệ giảm giá, Tiền thuế, Tỉ lệ thuế, Tiền hàng, Giờ thanh toán, Khách đưa, Trả lại, Voucher, Thẻ trả trước, Trừ tích lũy, Tiền mặt, Bàn (`DBANID` + Tên bàn), Bắt đầu, Kết thúc, Tiền giờ, Tỉ lệ giảm giá giờ, Tiền giảm giá giờ, Số khách, Phí dịch vụ, Tỉ lệ phí dịch vụ, Số order. Cột Bắt đầu/Kết thúc định dạng `HH:mm`.
- Trên lưới: ô **Lọc** (`txtLoc`, gõ để tìm trong lưới), nút **Tải dữ liệu**, nút **Hủy phiếu** (chỉ chế độ Quản lý bán hàng).
- Chọn dòng → sau một nhịp (timer `tmrLoad`, gỡ nhiễu khi bấm nhanh) nạp bill sang form phải (`Reload(ID, "")`); chưa chọn dòng → **form phải bị vô hiệu hóa**.
- Ngày mặc định = ngày giao dịch hiện tại (từ = đến); chế độ Điều chỉnh hóa đơn **khóa chọn ngày** (`dtNgay.Enabled = false`) — chỉ xem được bill trong ngày giao dịch hiện tại.
- Làm tươi: khi bảng `TDONHANG` có thay đổi sau lần tải cuối (`Config.NeedRefresh`).

### 1.1 Form phải theo chế độ [CODE `TDONHANG0Ae.Mode`, `mapper_OnLoad`, `Reload`]

| Thành phần | Quản lý bán hàng | Điều chỉnh hóa đơn |
|---|---|---|
| Chuyển bàn, Gộp bàn, Thống kê (F9) | ẩn | ẩn |
| Thanh toán | hiện, đây là nút **cập nhật** bill (không đóng bàn) | như bên trái |
| Khối tổng cộng (giảm giá, phí dịch vụ, thuế) | bật theo quyền (xem `su-dung-dich-vu.md` §3.5) | **vô hiệu hóa** |
| Nút Giảm, Xóa, Đổi giá, `−1`, Ghi chú, CK%, Giảm theo nhóm, Đặt SL, ô ghi chú bill | hiện | **ẩn / khóa** |
| Nút Kiểm đồ | ẩn (hoặc hiện nếu bật `KichHoatChucNangKiemDo`) | hiện, đổi chữ thành **"Trả đồ"** |
| Các ô đầu form (`pnlHeader`) | bật | chỉ ô **Khách hàng** bật, và chỉ khi có quyền `ThayDoiKhachHangTrongDieuChinhHoaDon` (id `e818a646-dd01-4fd1-a500-eb86af1cf313`); thiếu quyền → khóa cả `pnlHeader` |
| Ngày bill | sửa được nếu `ChoPhepThayDoiNgayTrenHoaDon=30` | như bên trái nhưng khối đầu đã khóa |
| In lại bill (F8) | **luôn hiện** | hiện khi bill đã thanh toán |
| Giờ vào/ra (tiền giờ) | — | — (cả hai: **cắt** ở v1, như `su-dung-dich-vu.md` §1) |

Khác biệt cốt lõi so với Sử dụng dịch vụ: `IsDaThanhToan` bị bỏ qua (`daThanhToan = false` khi `mode ≠ SuDungDichVu`), nên **bill đã thanh toán sửa được**; không có timer cập nhật giờ; không tự `mapper.Update` khi tính lại — **chỉ ghi khi bấm Thanh toán/cập nhật** (xem §2.2).

## 2. Flow thao tác (khác khuôn)

### 2.1 Mở màn
Tạo form hóa đơn nhúng, đặt `Mode` theo menu, nạp `dtNgay` = ngày giao dịch, tải danh sách. Quyền vào màn: `SFUNCTION` của menu tương ứng (nhóm quyền như các màn khác).

### 2.2 Sửa bill và cập nhật [CODE `btnThanhToan_Click`, `XacNhanThanhToan.SetData`, `CapNhatLuuVet`]
1. Người dùng sửa trên form phải (mặt hàng, SL, đơn giá, giảm giá, phí, thuế, khách, số khách, số order…). Mọi thay đổi **chỉ nằm trong bộ nhớ** + danh sách lưu vết tạm `lstLuuVet` (không ghi `TLUUVET` ngay, khác Sử dụng dịch vụ).
2. Bấm **Thanh toán**:
   - Chế độ Quản lý bán hàng: cần quyền **sửa** `QuanLyBanHang` (`CanEdit`), không có thì dừng (hộp từ chối do framework).
   - "Mời bạn thêm mặt hàng vào phiếu" nếu hết dòng (và tiền giờ 0); kiểm soát order (`BatChucNangKiemSoatOrder`); `CalculateAllRows`; **Hộp Xác nhận thanh toán mở với dữ liệu thanh toán CŨ của bill** (thẻ trả trước, trừ tích lũy, đặt trước, nợ cũ, voucher, thẻ ATM, chuyển khoản + tài khoản, thẻ trả trước `DTHETRATRUOCID`, voucher `DVOUCHERID`) thay vì để trống.
   - Nút trong hộp: **OK** (cập nhật + in lại), **Đóng bill không in** (= cập nhật, không in). `Đóng bill không in` vẫn đi nhánh Retry.
3. Khi OK/Retry: `mapper.Update()` (ghi cả bill + dòng), xóa dòng `XUATVATTU=30` cũ rồi **xuất vật tư lại** (`XuatVatTu`), rồi ghi các trường thanh toán lên bill (`TIENTHANHTOAN`, `CONLAI`, `TRALAI`, `THETRATRUOC`, `TRUTICHLUY`, `DATTRUOC`, `CONGNO`, `TIENMAT`, `CHUYENKHOAN`, `THE`, `DIEMGIAM`, `KHACHDUA`, `NOCU`, `DTAIKHOANNGANHANGID`, `VOUCHER`, `DVOUCHERID`, `LOAITHANHTOAN`, `DIEM`, `CONNO`, `PASSWIFI`, `SOTT`, `DATHANHTOAN = 30`). **Không** đổi `USERTHANHTOANID`, `GIOTHANHTOAN`, `NGAY` (khác Sử dụng dịch vụ) → người thu/giờ thu gốc được giữ.
4. Sau đó: nếu OK (có in) → in lại hóa đơn (`PrintInvoice(…, rePrint = true)`); ghi lưu vết gom (`CapNhatLuuVet` → từng dòng `TLUUVET`); nếu chọn "không in" thì báo **"Đã cập nhật hóa đơn thành công"**. **Không** tạo bill trống, không nhả bàn, không xóa `TINCHEBIEN`.
5. **Không có nút Hủy thay đổi**: thoát chọn bill khác thì bản chưa cập nhật bị bỏ (app gốc không hỏi) — xem P3.

### 2.3 Trả đồ (Điều chỉnh hóa đơn) [CODE `tsbTraLai_Click`, `TraDo`]
Mở hộp **Trả đồ** liệt kê mặt hàng đã gọi (loại trừ KM tự động, dòng xuất vật tư, dịch vụ theo giờ, dòng con combo), gộp theo (mặt hàng, đơn giá, % giảm, đơn vị) với `SLORDER = Σ SLXUATCHUAQUYDOI > 0`; nhập `SLTRALAI` hoặc `SLSUDUNG` (cân bằng `SLTRALAI + SLSUDUNG = SLORDER`); nhập vượt thì về 0. Với mỗi dòng `SLTRALAI > 0`: thêm **dòng SL âm** (`−SLTRALAI`) giữ đơn giá và % giảm gốc; ghi lưu vết "Trả lại 'x', số lượng: n đơn giá: p"; tính lại tổng; **`TDONHANG.GIOTRADO = now`**. Cần quyền `XoaGiamMon` và mật khẩu giảm đồ (nếu `NhapMatKhauGiamDo=30`), như giảm hàng. Bill vẫn phải bấm Thanh toán để cập nhật (§2.2).

### 2.4 Hủy phiếu (chỉ Quản lý bán hàng) [CODE `btnHuyPhieu_Click`, `QuanLyBanHang.HuyBill`]
Quyền `HuyHoaDon` (đăng nhập lại). Không có giới hạn thời gian và không chặn bill thuộc nhóm gộp (khác hủy trên bàn ở `su-dung-dich-vu.md` §3.8). `NhapLyDoKhiHuyHoaDon=30` (hiện = 30 [DB]) → bắt nhập lý do (hộp `NhapLyDo`, Hủy thì thôi); ngược lại lý do = "Hủy hóa đơn" và hỏi **"Bạn có muỗn hủy hóa đơn số 'X' không?"** (nguyên văn lỗi chính tả "muỗn"). Thực hiện: nếu bill đã mất → "Đơn hàng này đã hủy"; ghi lưu vết "Hủy đơn hàng"; `LuuBillHuy` (§3.2); **xóa cứng** `TDONHANG`, `TDONHANGCHITIET`, `TINCHEBIEN`; tải lại danh sách. (Khớp luật "không thùng rác": hủy = xóa cứng, chỉ còn bản lưu `TDONHANGHUY`.)

## 3. Luật nghiệp vụ

### 3.1 Điều kiện bill xuất hiện trong danh sách
`LOAI = 0 AND DATHANHTOAN = 30 AND NGAY BETWEEN Từ AND Đến`. Bill đang mở (chưa thanh toán) **không** hiện ở đây — xử lý ở Sử dụng dịch vụ. Bill nhóm gộp (`NHOMGUID`) hiện từng bill riêng.

### 3.2 Bản lưu hủy [CODE `LuuBillHuy`]
Ghi `TDONHANGHUY` (sao chép: `TDONHANGID`, `NAME`, `NOTE`, `NGAY`, `GIOTHANHTOAN`, `DATHANHTOAN`, `DOITRA`, `TIENGIO`, `PHIDICHVU`, `PHIVANCHUYEN`, `TIENTHUE`, `TILETHUE`, `TILEGIAMGIA`, `TIENHANG`, `TRALAI`; tên khách, tên nhân viên xuất, tên bàn, người thanh toán `THANHTOANBOI`, thu ngân hiện tại `THUNGAN`, `GIOHUY = now`, `NGAYHUY = ngày DB`, `LYDOHUY`) và mỗi dòng `TDONHANGHUYCHITIET` (`TENHANG`, `MAHANG`, `DVT`, `DONGIA`, `SOLUONG = SLXUAT − SLNHAP`, `THANHTIEN`, `NOTE` = ghi chú **của mặt hàng**, không phải của dòng). **Hai điểm lệch trong code gốc:** `TIENGIAMGIA` được gán bằng `TILEGIAMGIA` (tỉ lệ %, không phải tiền); `THANHTOANBOI` lấy `SUSERRow(USERTHANHTOANID)` — nếu rỗng (bill nhóm) thì tên rỗng. Số bản ghi `TDONHANGHUY` hiện có trong DB chép: 0.

### 3.3 Lưu vết [CODE `Track`]
Hai hàm lưu vết khác nhau: `TDONHANG0Ae.Track` (gom, ghi khi cập nhật, `CHUCNANG` = "Quản lý bán hàng" / "Điều chỉnh hóa đơn") và `QuanLyBanHang.Track` (ghi ngay): hàm sau **gán `CHUCNANG` hai lần** và giá trị cuối là "Sử dụng dịch vụ" hoặc "POS cảm ứng" → hủy phiếu ở màn này vẫn bị ghi là "Sử dụng dịch vụ". Chỉ ghi khi `KichHoatLuuVetHoatDong=30` (hiện = 30 [DB]). Chi tiết bảng `TLUUVET`/loại lưu vết sẽ ở spec "Lưu vết hoạt động" (ZZZ017).

### 3.4 Quyền
`QuanLyBanHang` (sửa) và `HuyHoaDon` (đăng nhập lại) cho Quản lý bán hàng; `ThayDoiKhachHangTrongDieuChinhHoaDon` cho đổi khách; mọi quyền chỉnh dòng như `su-dung-dich-vu.md` §4 (`XoaGiamMon`, `SuaDonGia`, `GiamGiaMatHang`, `ThayDoiGiamGiaTongHoaDon`, `ThayDoiPhiDichVu`, `InLaiBill`, `XemDonGiaTrongHoaDonBanHang`).
Quyền xem màn = `SFUNCTION` của từng menu (`SMENU.SFUNCTIONID`). Mặc định tạm cho tài khoản không phải admin: không cấp (Q5 ở spec gốc).

### 3.5 Cấu hình `SCONFIG` hiện hành [DB] ảnh hưởng màn này
`NhapLyDoKhiHuyHoaDon` = 30 · `KichHoatLuuVetHoatDong` = 30 · `ChoPhepThayDoiNgayTrenHoaDon` = 0 · `LamTronTien` = 1000 · `NhapMatKhauGiamDo` = 0 · `KhongDuocGiamDoSauKhiInPhaChe` = 30 (không áp ở các chế độ này vì hàm kiểm trả `false` khi `mode ≠ SuDungDichVu`) · `KichHoatChucNangKiemDo` = 0 · `BatChucNangKiemSoatOrder` = 0. (`ThoiGianChoPhepHuyBill` **không** áp ở màn này.)

## 4. Phân quyền
Xem §3.4. `HuyHoaDon` yêu cầu đăng nhập lại (`CanLogin`), `QuanLyBanHang` kiểm `CanEdit`, các quyền "xem" kiểm `CanView` (im lặng).

## 5. Chỗ khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Cập nhật bill trong **một transaction**: ghi bill + dòng + xuất vật tư + lưu vết cùng lúc; nếu lỗi thì không ghi dở (app gốc ghi nhiều câu lệnh rời).
- **P2** Điều chỉnh hóa đơn cho chọn ngày/khoảng ngày (gốc khóa ở ngày giao dịch hiện tại). Mặc định tạm: giữ khóa như gốc.
- **P3** Báo "Bạn có thay đổi chưa cập nhật, bỏ qua?" khi đổi bill/đóng màn (gốc im lặng bỏ).
- **P4** Hủy phiếu: ghi đúng `TIENGIAMGIA` (tiền) và `CHUCNANG` = "Quản lý bán hàng", sửa lỗi chính tả "muỗn".
- **P5** Hủy phiếu nên chặn khi bill đã đối soát/đóng sổ (hiện chưa có khái niệm đóng sổ trong DB) — chỉ ghi chú, không làm v1.
- **P6** Bill thuộc nhóm gộp (`NHOMGUID`): cảnh báo trước khi sửa/hủy một bill vì tổng nhóm đã chia tiền (gốc không cảnh báo).
- **P7** Cập nhật dữ liệu thanh toán cũ (tiền khách đưa…) không nên làm lại điểm tích lũy `DIEM`/nhóm khách theo cách gốc tính lại im lặng — Q4.

## 6. Câu hỏi

### 6.1 Đã trả lời bằng code/DB
Lưới lọc `LOAI=0`, `DATHANHTOAN=30` ✔ · sắp `NGAY, GIOTHANHTOAN` ✔ · hủy xóa cứng + lưu `TDONHANGHUY` ✔ · hai menu dùng chung một form ✔ · hủy phiếu không giới hạn thời gian ✔.

### 6.2 Còn mở — kèm "mặc định tạm"
| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | Sau khi sửa bill đã thanh toán có phải **tính lại công nợ khách / điểm tích lũy / thẻ trả trước** (app gốc chỉ ghi các cột trên bill; còn bảng điểm, thẻ có đọc lại từ bill không?) | Chỉ ghi các cột trên bill, như gốc |
| Q2 | Có cần Điều chỉnh hóa đơn cho phép xem ngày khác (P2)? | Không, giữ như gốc |
| Q3 | `GIOTRADO` dùng để làm gì (báo cáo nào đọc)? | Chỉ ghi, không đọc |
| Q4 | Sau khi đổi khách, có tự áp lại giảm theo nhóm khách như ở Sử dụng dịch vụ (`CapNhatGiamGia`)? Code chạy khi đổi ô khách có focus | Áp lại như gốc |
| Q5 | Sau sửa bill nhóm gộp, các bill cùng nhóm có tính lại phần chia tiền thanh toán? (gốc: không) | Không, như gốc; cảnh báo P6 |
| Q6 | In lại sau khi cập nhật: tăng `LANINHOADON`/giới hạn `SoLanInToiDa`? | Như `su-dung-dich-vu.md` Q10: nút có, "chưa hỗ trợ" |
| Q7 | Có cần ô lọc theo bàn/khách/số phiếu ngoài `txtLoc`? | Không, chỉ `txtLoc` |
| Q8 | Hủy phiếu có cần đăng nhập lại bằng mật khẩu quản lý? | Có, như `CanLogin` gốc |
| Q9 | Chụp bổ sung bằng app gốc (có bill đã thanh toán trong DB mẫu) khi máy cho phép? | Có, sau |

## 7. Tiêu chí nghiệm thu (kiểm trên bản sao DB)

- [ ] Tạo ≥ 3 bill đã thanh toán + 1 bill đang mở trong ngày; danh sách chỉ hiện đúng 3 bill (`LOAI=0`, `DATHANHTOAN=30`) theo thứ tự `NGAY, GIOTHANHTOAN`; đổi ngày lọc đúng (Quản lý bán hàng); Điều chỉnh hóa đơn khóa ô ngày.
- [ ] Chưa chọn dòng: form phải bị khóa; chọn dòng: nạp đúng bill sau một nhịp gỡ nhiễu.
- [ ] Chế độ Quản lý bán hàng: hiện đủ nút giảm/xóa/đổi giá…; ẩn Chuyển bàn/Gộp bàn/Thống kê; In lại bill luôn hiện.
- [ ] Chế độ Điều chỉnh hóa đơn: khối tổng cộng và các nút chỉnh dòng bị khóa; nút "Trả đồ" hiện; chỉ ô Khách hàng bật và chỉ khi có quyền `ThayDoiKhachHangTrongDieuChinhHoaDon`.
- [ ] Sửa (đổi SL, đơn giá, giảm giá) **không ghi DB cho đến khi bấm Thanh toán**; bấm cập nhật ghi bill + dòng + xuất vật tư lại + lưu vết trong một transaction; `USERTHANHTOANID`, `GIOTHANHTOAN`, `NGAY` giữ nguyên; hộp thanh toán mở với giá trị thanh toán cũ của bill.
- [ ] Trả đồ: dòng SL âm đúng đơn giá và % giảm gốc, `GIOTRADO` = now, tổng giảm tương ứng; `SLTRALAI + SLSUDUNG = SLORDER`.
- [ ] Hủy phiếu: đủ quyền → nhập lý do (khi cờ bật) → xóa cứng bill + dòng + `TINCHEBIEN`, có đúng 1 `TDONHANGHUY` + n `TDONHANGHUYCHITIET` với `TIENGIAMGIA` là **tiền**; thiếu quyền → không làm gì.
- [ ] Tiền `decimal`, `LamTron` 1000 AwayFromZero chỉ khi tính tiền; không đọc `LOAIDINHLUONG`; không thùng rác; nút In "chưa hỗ trợ".
- [ ] Không SQL trong View; màu/cỡ chữ theo token; 125% DPI.

## 8. Gợi ý implement (không bắt buộc)

Tái dùng `OrderView`/`OrderCalculator`/`PaymentDialog` của `su-dung-dich-vu.md`, thêm tham số `OrderMode { Sale, Manage, Adjust }` để bật/tắt thành phần theo bảng §1.1 (không rẽ nhánh `if mode` rải rác — một lớp `OrderModePolicy` trả cờ). `BillListView` (lưới trái) + `BillAdjustService` (cập nhật trong transaction, hủy + `TDONHANGHUY`). Thứ tự: (1) `OrderModePolicy` + selftest; (2) danh sách + nạp bill chỉ đọc; (3) cập nhật bill; (4) trả đồ; (5) hủy phiếu.

## Phụ lục A — Ảnh [APP]
Chưa có (xem §0). Dự kiến: 01 màn Quản lý bán hàng (danh sách + bill), 02 màn Điều chỉnh hóa đơn (khối tổng khóa, nút Trả đồ), 03 hộp Trả đồ, 04 hộp nhập lý do hủy.
