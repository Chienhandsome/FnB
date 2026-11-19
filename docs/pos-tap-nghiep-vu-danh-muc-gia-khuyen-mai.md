# POS TAP — nghiệp vụ danh mục mặt hàng, bảng giá giờ, khuyến mại

Nguồn: STABLEDESC `DMATHANG__a4a16216` (`DMATHANGAe`, 422 dòng), `DBANGGIA__ff19c3a5` (`DBANGGIAAe`, 269), `DDOTKHUYENMAI__1732f45e` (`DDOTKHUYENMAIAe`, 609), SFORM `Thêm mua 1 tặng 1`, `Thêm nhanh khuyến mại theo nhóm`, `Cảnh báo khuyến mại`, `Mặt hàng khuyến mại`. (STABLEDESC là mã của form thêm/sửa từng bảng dữ liệu `D…`/`T…`.)

## 1. Loại mặt hàng (`DMATHANG.DLOAIMATHANGID`, bảng `DLOAIMATHANG`)

Mã suy ra từ `DMATHANGAe`, `TinhLaiGiaVon`, `Màn hình bếp`:

| Mã | Ý nghĩa |
|---|---|
| `0` | Mặt hàng kiêm vật tư (hàng thường, vừa nhập vừa bán) — mặc định khi tạo mới |
| `1` | Định lượng / pha chế (có công thức vật tư `DDINHLUONG`) |
| `2` | Vật tư / nguyên liệu (không bán, loại khỏi tổng bill và danh sách bán) |
| `3` | Mặt hàng mở (nhập tên, đơn vị, giá lúc bán) |
| `4` | Combo (gồm các mặt hàng, `GIOHATCOMBO` giờ hát nếu có tiền giờ) |
| `5` | Combo mở |
| `6` | Dịch vụ — **[Sửa v2]** đã xác nhận bằng bảng `DLOAIMATHANG` (seed DEMO): `0` kiêm vật tư, `1` pha chế (định lượng), `2` nguyên vật liệu, `3` mặt hàng mở, `4` combo, `5` combo mở, `6` dịch vụ, `7` dịch vụ theo giờ |
| `7` | Dịch vụ theo giờ |

Mã `0–3` là hệ thống; "Khác" lấy từ `DLOAIMATHANG` loại `ID` không phải `0,1,2,3` (cờ `CODINHLUONG` của loại cho biết có định lượng). Loại đồ của nhóm mặt hàng `DNHOMMATHANG.DLOAIDOID`: `0` đồ ăn, `1` đồ uống, `2` dịch vụ, `3` đồ khác, `4` nguyên liệu.

## 2. Mặt hàng (`DMATHANGAe`)

- Mã tự sinh khi tạo mới nếu `TuDongSinhMaVach=30`: `DbUtils.GenMaHang(mã nhóm, ChieuDaiMaVach, bảng, cột CODE)`; sinh lại khi đổi nhóm / sao chép.
- 2 đơn vị tính (`SuDung2DonViTinh=30`): đơn vị lẻ `DDONVITINHID`, đơn vị chẵn `DDONVITINHCHANID`, quy đổi `QUYDOI` (1 chẵn = `QUYDOI` lẻ). Bắt buộc nhập quy đổi nếu có đơn vị chẵn; hai đơn vị không được trùng. Giá nhập tính theo đơn vị chẵn nếu có.
- Kiểm tra khi lưu: mặt hàng kiêm vật tư / nguyên liệu / dịch vụ **không** có chi tiết định lượng; định lượng / combo / combo mở **phải** có ít nhất một dòng; mỗi dòng phải có vật tư và `SOLUONG > 0`; combo (kể cả combo mở) không được chứa nguyên liệu (`'2'`). Cảnh báo: combo mở chỉ hỗ trợ đầy đủ ở giao diện quản trị.
- Vật tư được chọn trong định lượng: loại `0` (hoặc rỗng), `2` hoặc `1` (định lượng lồng nhau), khác chính mặt hàng.
- Giá theo định lượng: `GIANHAP = Σ(SOLUONG × GIANHAP vật tư / QUYDOI)` (chia `QUYDOI` khi vật tư có đơn vị chẵn), `GIAVON = Σ(SOLUONG × GIAVON vật tư)`. Tự cập nhật vào mặt hàng khi `TuDongCapNhatGiaNhapCuaMatHangDinhLuong` / `TuDongCapNhatGiaVonCuaMatHangDinhLuong = 30`; sau khi lưu một vật tư, SQL cập nhật lại giá của mọi mặt hàng loại `1`, `4`, `5` chứa vật tư đó (`UPDATE DMATHANG SET GIAVON = (SELECT SUM(SOLUONG × B.GIAVON) …)`, giá nhập cùng công thức có chia `QUYDOI`). Một cấp (không đệ quy) trong các câu SQL này.
- Giá bán: `GIABAN` và `GIABAN2/3/4` (đơn vị lẻ), `GIABANCHAN` (đơn vị chẵn); `GIATHEOTHOIGIA` (hỏi giá mỗi lần bán); `MACDINHGIAMGIA`, `MACDINHGIAMTIEN` (giảm giá mặc định khi bán), `TONTOITHIEU`, `COHANSUDUNG`, `THEOCAN`, `TAMKHOA`. Xem giá nhập / lịch sử mua cần quyền `XemGiaNhap`; lịch sử bán cần quyền `HoaDonBanHang`.

## 3. Bảng giá tiền giờ (`DBANGGIA`, `DBANGGIACHITIET`)

- Mỗi bảng giá có các dòng `(TUGIO, DENGIO, SOTIEN)` với `TUGIO`/`DENGIO` là giờ nguyên `0..24`. Có 3 nhóm dòng theo cột `NGAYLE`: `0` ngày thường, `30` cuối tuần (khi tick `COCUOITUAN`), `60` ngày lễ (khi tick `CONGAYLE`).
- Hợp lệ khi lưu (mỗi nhóm): có ít nhất một dòng; `SOTIEN ≥ 0`; `DENGIO > TUGIO`; các khoảng liền mạch bắt đầu từ 0 và kết thúc ở 24 (ví dụ 0–9, 9–15, 15–21, 21–24); `TUGIO < 24`, `DENGIO ≤ 24`.
- Cấu hình hệ thống liên quan: `NgayCuoiTuan` (chủ nhật / thứ 7 + CN / thứ 6 + 7 + CN), `NgayLeDuongLich`, `NgayLeAmLich`. Hàm tính tiền `Shared.TinhGia` dùng các bảng này nhưng **không có trong bản export** (nằm trong DLL). Mỗi bàn / khu vực gắn một bảng giá hoặc một đơn giá theo giờ (`CACHTINHGIO`: theo giờ, theo bảng giá, theo khu vực).

## 4. Đợt khuyến mại (`DDOTKHUYENMAI`, chi tiết `DDOTKHUYENMAICHITIET`)

- **[Sửa v2 — DB thật]** Bảng `DLOAIHINHKHUYENMAI` của POS TAP chỉ có **5 dòng**: "Giảm giá tổng bill" (`8a96fc6b…`), "Giảm giá theo nhóm hàng" (`7c566d6f…`), "Giảm giá % theo sản phẩm" (`8402c931…`), "Giảm giá tiền theo sản phẩm" (`8be106e5…`), "Mua x sản phẩm tặng y sản phẩm" (`e5d39bb6…`). Các loại "giảm theo giá trị đơn hàng", "giảm theo số lượng", "đồng giá", "tặng theo trị giá" có trong enum của code nhưng **không có dòng lookup → không chọn được trong app**. Danh sách dưới đây là enum trong code, không phải danh mục người dùng thấy.
- Loại hình `DLOAIHINHKHUYENMAIID` (`LOAIHINHKHUYENMAI`): giảm giá tổng bill, giảm giá theo nhóm, giảm giá theo sản phẩm, giảm theo giá trị đơn hàng, giảm theo số lượng mặt hàng, mặt hàng đồng giá, mua 1 tặng 1, **tặng theo trị giá** (`TANGTHEOTRIGIA`: dòng chi tiết có mặt hàng tặng `DMATHANGTANGID`; **mã áp dụng loại này chưa thấy trong các form bán hàng đã đọc**, cần tìm thêm). **[Sửa v2]** Đã tìm hết: **không có mã áp dụng ở đâu** (cả 2 form hóa đơn, `CanhBaoKhuyenMai`, MTB, TS; trong `DDOTKHUYENMAIAe` chỉ có nhánh chọn tab `pageTangTheoTriGia` + `LoadTangTheoTriGia()` — đã kiểm trên `POS_TAP_SRC_EXPORT`) → loại KM này **nhập được, không chạy**. Tương tự loại `8be106e5` "Giảm giá tiền theo sản phẩm": lưu được nhưng không có nhánh xử lý. Chiến chốt: viết luật hay ẩn hai loại này khỏi danh mục (xem [file khách hàng](pos-tap-nghiep-vu-khach-hang-the.md) mục 6.3).
- Ràng buộc khi lưu: đủ trường bắt buộc, `TUNGAY ≤ DENNGAY`; giảm theo nhóm: mỗi nhóm `0 ≤ TILEGIAMGIA ≤ 100`. Mã gốc từng có kiểm tra "một thời điểm chỉ một chương trình" nhưng đã bị comment — nhiều đợt có thể chạy cùng lúc (lấy max theo từng loại như các form bán hàng).
- Giảm tổng bill: các trường `TILEGIAMGIA` (tiền hàng), `TILEGIAMGIATIENGIO`, `TILEGIAMGIATONG`, `KHUYENMAIGIOHAT` (phút), `TILEGIAMGIAGIODAU`; chỉ hiện ô giờ khi có tiền giờ và không gộp; gộp thì chỉ hiện giảm tổng.
- Giảm theo nhóm: dạng cây nhóm hàng (loại trừ nhóm nguyên liệu), nhóm có con thì khóa ô nhập; lưu bằng `UpdateByFields` vào `DDOTKHUYENMAICHITIET (DNHOMMATHANGID, TILEGIAMGIA)`.
- Giảm theo sản phẩm / đồng giá: thêm mặt hàng từ danh sách (mặc định tỉ lệ 0, hoặc `GIABAN` = giá bán hiện tại), nhập từ Excel (Mã hàng + Tỉ lệ giảm giá, chỉ lấy dòng tỉ lệ > 0), mỗi mặt hàng chỉ xuất hiện một lần.
- Mua 1 tặng 1: dòng `(DMATHANGID mua, SOLUONGMUA = 1, DMATHANGTANGID, SOLUONGTANG = 1)`; mỗi mặt hàng mua chỉ một dòng; có thể "tặng cùng" (tặng chính mặt hàng mua); thêm nhanh theo nhóm hàng: cho nhóm, số lượng mua, số lượng tặng (> 0), hình thức `0` tặng cùng mặt hàng / `1` tặng một mặt hàng khác cố định — áp cho mọi mặt hàng `STATUS=30` của nhóm chưa có trong đợt.
- Màn hình cảnh báo khuyến mại (`CanhBaoKhuyenMai`): liệt kê các đợt đang chạy khi vào ca, hiển thị nội dung theo loại; hộp "mặt hàng khuyến mại" ở thanh toán có ô "không hiển thị lại" ghi `SystemConfig.HienThiMatHangKhuyenMai` (cần quyền `CauHinhToanHeThong`).
