# Spec (delta): Chi tiết hoạt động ngày

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-24. Base: `main` @ `fb09355` (đã `git fetch origin main`)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md); cùng họ báo cáo chỉ xem với [`tong-hop-ket-qua-kinh-doanh.md`](tong-hop-ket-qua-kinh-doanh.md) (cùng cơ chế `OPERATION = 5`, cùng bộ lọc ngày/cửa hàng, cùng lỗi ràng buộc cửa hàng). Spec này **chỉ ghi phần khác**; chỗ không nói = như hai spec trên.
- Menu: **HOẠT ĐỘNG > Chi tiết hoạt động ngày** (`SMENU` `b2555738-5bc9-4f84-9e6a-80188bbfac99`, `SORTORDER = ZZZ023`, **`OPERATION = 5`**, `SFORMID = NULL`, `SREPORTID` `5b986bf4-406f-45ec-896d-81951fcde35b`, `SFUNCTIONID` `5d90c04e-ca76-4941-8f48-575a07e73e19` "Chi tiết hoạt động ngày", nhóm "Bán hàng", **`NOTE` rỗng** (khác các quyền `View`)).
- Là **báo cáo** `CHI TIẾT HOẠT ĐỘNG TRONG NGÀY` (`SREPORT` `5b986bf4-…`, lớp `ChiTietHoatDongTrongNgay`, lọc trên đầu `FILTERONTOP = 30`, `HASDETAIL = 0`), gồm **4 tập dữ liệu** gộp vào một mẫu in; mẫu FastReport tên "Mẫu đầy đủ" (`SREPORTTEMPLATE` `75d5af0a-a1be-4663-b8c8-8cab10a2919c`).

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`): `TDONHANG`, `TTHUCHI` hiện **0 bản ghi** → selftest tự tạo dữ liệu.
- **[CODE]** `POS_TAP_SRC_EXPORT/SREPORT/CHI TIẾT HOẠT ĐỘNG TRONG NGÀY__5b986bf4/` (`query.sql` 4 truy vấn, `code.cs`); tài liệu [`pos-tap-nghiep-vu-bao-cao.md`](../pos-tap-nghiep-vu-bao-cao.md) (nhóm báo cáo chi tiết theo ngày). Đã đọc, không chép lại.
- **[CHƯA XÁC MINH]**: bố cục/nhóm cột/tổng của mẫu in FastReport (`.frx` không có trong bản export); cách mẫu dùng tham số `rpt.Parameters[0]`. **[APP]** đã chụp phần khung + đầu báo cáo (Q8 đã xử lý một phần; thiếu ảnh khi có dữ liệu).

## 1. Giao diện (khác khuôn)

Màn chạy báo cáo chuẩn của khung:
- **Bộ lọc trên cùng** (`PARAMS`): **Từ ngày**, **Đến ngày** (cả hai **bắt buộc**), **Thu ngân** (`@USERID`, lookup `SUSER`, không bắt buộc), **Cửa hàng** (`@DCUAHANGID`, lookup `DCUAHANG`, không bắt buộc). Hai lookup để trống = tất cả.
- Hiển thị **một báo cáo cuộn dọc có 4 khối** (theo thứ tự các tập dữ liệu), mỗi khối có tiêu đề và tổng cuối khối (tổng theo mẫu in, **[CHƯA XÁC MINH]**):
  1. **Hóa đơn bán** (danh sách bill): Số phiếu, Giờ thanh toán, Khách hàng, Tiền hàng, Giảm giá, Thuế, Phí vận chuyển, Tổng cộng, Tiền mặt, Chuyển khoản, Đặt trước, Tiền thẻ, Voucher, Thẻ trả trước, Trừ tích lũy, Còn nợ.
  2. **Mặt hàng bán**: Tên hàng, Mã hàng, ĐVT, Số lượng, Đơn giá, % giảm, Thành tiền.
  3. **Thu chi**: Số phiếu, Diễn giải, Tên đối tượng, Thu tiền mặt, Thu chuyển khoản, Chi tiền mặt, Chi chuyển khoản.
  4. **Phiếu nhập (mua hàng)**: Số phiếu, Nhà cung cấp, Tổng cộng, Đã thanh toán, Còn nợ.
  - Cuối báo cáo hiển thị **Giảm giá tổng bill** (tham số, §2.5).
- **[APP] đã chụp 24/11/2026 (DB không có bill nên báo cáo rỗng):**
  - Thanh lọc thật: **Từ ngày** (mặc định **ngày 01 của tháng hiện tại**, không phải hôm nay), **Đến ngày** (hôm nay), **Thu ngân** (trống = "Tất cả"), **Cửa hàng** (tự chọn sẵn cửa hàng của user, ví dụ "TRỤ SỞ CHÍNH", chứ không để trống), nút **Tải dữ liệu (F5)**. Dưới là thanh công cụ trình xem FastReport (In, mở, lưu, gửi mail, tìm, chuyển trang, Close).
  - Đầu trang mẫu in: khối **tên công ty / địa chỉ / điện thoại / email** (đang là chỗ trống `(TÊN CÔNG TY)`… vì chưa cấu hình), tiêu đề **"CHI TIẾT HOẠT ĐỘNG TRONG NGÀY"**, dòng "Từ ngày … Đến ngày …", "Thu ngân: Tất cả", "Ngày dd tháng MM năm yyyy", ô ký "Người lập (Ký, họ tên)".
  - **Có một khối tổng hợp ở đầu báo cáo** mà bản trước chưa ghi, gồm 8 dòng nhãn in hoa: **TIỀN MẶT, CHUYỂN KHOẢN, TIỀN THẺ, VOUCHER, THẺ TRẢ TRƯỚC, TRỪ TÍCH LŨY, KHÁCH HÀNG NỢ, NỢ NHÀ CUNG CẤP** (có số 0 cạnh Tiền mặt và Chuyển khoản; các dòng còn lại để trống khi không có dữ liệu). Cách tính từng dòng (đối chiếu `query.sql`) và 4 khối chi tiết bên dưới **chưa thấy được** vì không có dữ liệu → vẫn **[CHƯA XÁC MINH]**.
- v1: bốn khối là **bốn lưới/khối chỉ đọc** trên một tab cuộn, **và** nút In (mẫu FastReport → "chưa hỗ trợ" nếu chưa có mẫu, Q5). Tên cột lấy từ `AS` của truy vấn; nhãn tiếng Việt như trên là **[CHƯA XÁC MINH]** (mẫu in quyết định nhãn thật).

## 2. Luật và luồng (khác khuôn) [CODE `query.sql`, `AfterGetData`]

Chung cho cả 4 tập: `NGAY` (ngày giao dịch) nằm trong `[Từ ngày, Đến ngày]`; cửa hàng/thu ngân lọc **chỉ khi có chọn** (`CAST(@x AS VARCHAR) = '' OR cột = @x`). Mọi tiền `decimal`.

### 2.1 Hóa đơn bán

`TDONHANG` với `LOAI = 0 AND DATHANHTOAN = 30`; lọc **Thu ngân** = `USERTHANHTOANID` (người thu tiền); lọc cửa hàng = `DCUAHANGID`. Cột: `NAME`, `GIOTHANHTOAN`, tên khách (`DKHACHHANG.NAME` theo `DKHACHHANGID`, trống nếu khách lẻ), `TIENHANG`, `TIENGIAMGIA`, `TIENTHUE`, `PHIVANCHUYEN`, `TONGCONG`, `TIENMAT`, `CHUYENKHOAN`, `DATTRUOC`, `THE` (hiển thị "Tiền thẻ"), `VOUCHER`, `THETRATRUOC`, `TRUTICHLUY`, `CONGNO` (hiển thị "Còn nợ"). Sắp **`NGAY, NAME`**.

### 2.2 Mặt hàng bán

Gộp dòng chi tiết của các bill ở §2.1 (cùng điều kiện, **cùng lọc thu ngân + cửa hàng**) theo **(`TENHANG`, `DMATHANG.CODE`, `DDONVITINHID`, `DONGIA`, `TILEGIAMGIA`)**; `SOLUONG = Σ (SLXUATCHUAQUYDOI − SLNHAPCHUAQUYDOI)` (đã trừ hàng trả), `THANHTIEN = Σ THANHTIEN`. **Loại** dòng con combo (`COMBOPARENTID` ≠ rỗng) và dòng xuất vật tư (`XUATVATTU` ≠ 0). **Không** loại mặt hàng nguyên liệu (khác Thống kê mặt hàng bán). Sắp theo `TENHANG`.

### 2.3 Thu chi

`TTHUCHI` theo `NGAY`; thu ngân = **`USERCREATEDID`** (người **tạo phiếu**, khác tiêu chí `USERTHANHTOANID` ở bill); cửa hàng = `DCUAHANGID` (**Q13a đã chốt 24/11: để trống cửa hàng + không phải admin → giới hạn theo các cửa hàng của user, giống bill**). Tách tiền mặt / chuyển khoản theo `CHUYENKHOAN`: `CHUYENKHOAN = 0` hoặc NULL → tiền mặt, ≠ 0 → chuyển khoản; 4 cột `THUTM`, `THUCK`, `CHITM`, `CHICK`. Cột còn lại: `NAME`, `DIENGIAI`, `TENDOITUONG`. Sắp `NGAY, NAME`. **Không lọc `STATUS`, `LATAMUNG`, `LOAI`** (cả phiếu tạm ứng lẫn phiếu chi trả khách từ bill đều vào).

### 2.4 Phiếu nhập (mua hàng)

`TDONHANG` với `LOAI = 1` (phiếu nhập kho) theo `NGAY`; thu ngân = `USERCREATEDID`; cửa hàng lọc **gián tiếp** qua kho nhập (`DKHONHAPID IN (SELECT ID FROM DKHOHANG WHERE DCUAHANGID = @DCUAHANGID)`). Cột: `NAME`, nhà cung cấp (`DNHACUNGCAP.NAME` theo `DNHACUNGCAPID`), `TONGCONG`, `TIENTHANHTOAN`, **"Còn nợ"** = `CASE WHEN COALESCE(LOAITHANHTOAN, 0) < TONGCONG THEN TONGCONG − COALESCE(TIENTHANHTOAN, 0) ELSE 0 END`. Sắp `NGAY, NAME`.

### 2.5 Tham số "Giảm giá tổng bill" [CODE `AfterGetData`]

Báo cáo nhận **một giá trị** `rpt.Parameters[0]` = `Σ TIENGIAMGIA` của bill `LOAI = 0 AND DATHANHTOAN = 30` theo cùng khoảng ngày/thu ngân/cửa hàng (một truy vấn tổng riêng). Cách mẫu in dùng giá trị này (dòng "GIẢM GIÁ TỔNG BILL" cuối danh sách mặt hàng, theo tài liệu nghiệp vụ) → v1 hiển thị dòng cuối khối 2: **"Giảm giá tổng bill: −<số>"**. Nếu ô Từ/Đến ngày để trống (không xảy ra vì bắt buộc) code gốc dùng `1900-01-01` / `2100-01-01`.

### 2.6 Quyền

Quyền xem = `SFUNCTION` `5d90c04e-…` "Chi tiết hoạt động ngày" (`NOTE` rỗng, không phải `View`; hành vi kiểm quyền chưa xác minh khác gì các quyền `View` → Q6). Mặc định tạm tài khoản không phải admin: **không cấp** (Q5 spec Sử dụng dịch vụ).

## 3. Lỗi / chỗ lệch của app gốc (cần Chiến biết)

1. **"Còn nợ" của phiếu nhập so sai cột:** `COALESCE(LOAITHANHTOAN, 0) < TONGCONG` — so sánh **loại thanh toán** (mã) với tổng tiền, gần như luôn đúng khi tổng > 0, nên "Còn nợ" thực tế = `TONGCONG − TIENTHANHTOAN` ngay cả khi phiếu đã thanh toán đủ bằng cách khác. Có lẽ ý định là `TIENTHANHTOAN < TONGCONG`. (P1)
2. **Thu ngân ở 3 nơi hiểu khác nhau:** bill = người **thu** (`USERTHANHTOANID`); thu chi và phiếu nhập = người **tạo** (`USERCREATEDID`). Chọn một thu ngân → bill của họ nhưng phiếu thu chi do người khác tạo hộ sẽ không hiện.
3. **Giảm giá "tổng bill" lấy `TIENGIAMGIA`** (không phải `TIENGIAMGIATONG` như Thống kê doanh thu/KQKD) → ba báo cáo dùng ba cột giảm giá khác nhau; số có thể lệch nhau.
4. **Bill đã thanh toán trước ngày chọn nhưng `NGAY` lệch**: lọc theo `NGAY` (ngày bill), không theo `GIOTHANHTOAN` — bill qua đêm tính theo ngày mở.
5. **Lọc cửa hàng của phiếu nhập qua kho**, của bill/thu chi qua `DCUAHANGID` → phiếu nhập kho không gắn cửa hàng sẽ **mất** khi lọc cửa hàng.
6. **User thường để trống cửa hàng/thu ngân** thấy dữ liệu **mọi cửa hàng** (không ràng buộc `TNGUOIDUNGTHEOCUAHANG` như lookup), giống KQKD §3-1.

## 4. Chỗ đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Sửa "Còn nợ" phiếu nhập: `MAX(0, TONGCONG − COALESCE(TIENTHANHTOAN, 0))`. *Mặc định tạm: sửa; nếu Chiến muốn y hệt thì đảo.*
- **P2** Ràng buộc cửa hàng theo user khi để trống (giống P2 KQKD). *Mặc định tạm: áp.*
- **P3** Truy vấn dùng **tham số**, không nối chuỗi; gom 5 truy vấn thành ít lượt tải.
- **P4** Thu ngân thống nhất = người thu (bill) / người tạo (phiếu khác) — *giữ như gốc, ghi chú trên màn.*

## 5. Câu hỏi chờ Chiến quyết

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | Sửa "Còn nợ" phiếu nhập (P1)? | Sửa |
| Q2 | Có giữ khối "Phiếu nhập" trong báo cáo hoạt động ngày (F&B thuần chỉ bán, nhưng kho/nhập vẫn có)? | Giữ |
| Q3 | "Giảm giá tổng bill": dùng `TIENGIAMGIA` như gốc hay `TIENGIAMGIATONG`? | `TIENGIAMGIA` như gốc |
| Q4 | Ràng buộc cửa hàng theo user (P2)? | Áp |
| Q5 | Mẫu in "Mẫu đầy đủ": tạo FastReport mới hay "chưa hỗ trợ"? | "Chưa hỗ trợ" |
| Q6 | Quyền `5d90c04e-…` có `NOTE` rỗng: kiểm quyền thế nào? | Như quyền xem thông thường |
| Q7 | Thêm xuất Excel? | Không |
| Q8 | Chụp app gốc/mẫu in để xác minh bố cục | Có, sau |

## 6. Quyết định đã chốt (áp, không hỏi lại)

Tiền `NUMERIC(18,2)`/`decimal`; chỉ VNĐ; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; bỏ karaoke + tính giờ (không có cột tiền giờ trong báo cáo này); không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB; màn chỉ xem.

## 7. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest tự tạo bill nhiều hình thức thanh toán, vài phiếu thu/chi tiền mặt + chuyển khoản, phiếu nhập có nợ, ≥ 2 thu ngân, ≥ 2 cửa hàng)

- [ ] Bộ lọc: ngày bắt buộc; thu ngân/cửa hàng tùy chọn; để trống = tất cả (theo P2 cho user thường).
- [ ] Khối 1: chỉ bill đã thanh toán trong khoảng; lọc thu ngân theo `USERTHANHTOANID`; đủ cột; sắp `NGAY, NAME`.
- [ ] Khối 2: gộp đúng theo (tên, mã, ĐVT, đơn giá, % giảm); SL trừ hàng trả; loại combo con + xuất vật tư; **không** loại nguyên liệu; dòng cuối "Giảm giá tổng bill" khớp `Σ TIENGIAMGIA`.
- [ ] Khối 3: tách đúng tiền mặt/chuyển khoản (`CHUYENKHOAN` NULL/0 → tiền mặt); lọc thu ngân theo `USERCREATEDID`.
- [ ] Khối 4: chỉ phiếu `LOAI = 1`; lọc cửa hàng qua kho; "Còn nợ" theo P1 (hoặc theo gốc nếu Chiến đảo — selftest ghi rõ cả hai).
- [ ] Không SQL trong View; màu/cỡ chữ từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc). Nút In báo "chưa hỗ trợ".

## 8. Gợi ý implement (không bắt buộc)

`DailyActivityRepository` (Data: 5 truy vấn tham số hóa), `DailyActivitySummary` (Services: tổng cuối khối, "Còn nợ" phiếu nhập — thuần, selftest ≥ 6 ca), `DailyActivityView` (UI: bộ lọc + 4 khối). Dùng chung phần lọc ngày/cửa hàng/ràng buộc user với Thống kê doanh thu và KQKD (một `ReportFilterBar`).
