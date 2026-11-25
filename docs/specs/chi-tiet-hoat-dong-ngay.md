# Spec (delta): Chi tiết hoạt động ngày

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-24; sửa theo review [issue #4](https://github.com/Chienhandsome/FnB/issues/4) ngày 25/11/2026 (đối chiếu lại tại `main` @ `954f133`)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md); cùng họ báo cáo chỉ xem với [`tong-hop-ket-qua-kinh-doanh.md`](tong-hop-ket-qua-kinh-doanh.md) (cùng cơ chế `OPERATION = 5`, cùng bộ lọc ngày/cửa hàng, cùng lỗi ràng buộc cửa hàng). Spec này **chỉ ghi phần khác**; chỗ không nói = như hai spec trên.
- Menu: **HOẠT ĐỘNG > Chi tiết hoạt động ngày** (`SMENU` `b2555738-5bc9-4f84-9e6a-80188bbfac99`, `SORTORDER = ZZZ023`, **`OPERATION = 5`**, `SFORMID = NULL`, `SREPORTID` `5b986bf4-406f-45ec-896d-81951fcde35b`, `SFUNCTIONID` `5d90c04e-ca76-4941-8f48-575a07e73e19` "Chi tiết hoạt động ngày", nhóm "Bán hàng", **`NOTE` rỗng** (khác các quyền `View`)).
- Là **báo cáo** `CHI TIẾT HOẠT ĐỘNG TRONG NGÀY` (`SREPORT` `5b986bf4-…`, lớp `ChiTietHoatDongTrongNgay`, lọc trên đầu `FILTERONTOP = 30`, `HASDETAIL = 0`), gồm **4 tập dữ liệu** và một khối tổng hợp trong một mẫu in. Có **2 mẫu** (`SREPORTTEMPLATE`): "Mẫu đầy đủ" `75d5af0a-a1be-4663-b8c8-8cab10a2919c` và "CHI TIẾT HOẠT ĐỘNG TRONG NGÀY" `765b861a-e8d6-4739-b65d-d2b19b47b205`. `SREPORT.LASTTEMPLATEID` đang trỏ mẫu `765b861a` (Q9).

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`): `TDONHANG`, `TTHUCHI` hiện **0 bản ghi** → selftest tự tạo dữ liệu.
- **[CODE]** `POS_TAP_SRC_EXPORT/SREPORT/CHI TIẾT HOẠT ĐỘNG TRONG NGÀY__5b986bf4/` (`query.sql` 4 truy vấn, `code.cs`); tài liệu [`pos-tap-nghiep-vu-bao-cao.md`](../pos-tap-nghiep-vu-bao-cao.md) (nhóm báo cáo chi tiết theo ngày). Đã đọc, không chép lại.
- **[FRX]** Cả 2 mẫu lấy từ blob `SREPORTTEMPLATE.TEMPLATE` trên bản sao `DATA.fdb` (25/11). Hai mẫu không có script, khác nhau ở cột khối Bán hàng. Thư mục export `SREPORT/…` không chứa `.frx` nên bản spec trước ghi nhầm là không có mẫu in.
- **[CHƯA XÁC MINH]**: hình dạng 4 khối khi có dữ liệu (chưa chạy app gốc với bill thật); cơ chế khung điền giá trị mặc định cho bộ lọc và chữ "Tất cả". **[APP]** ảnh 24/11 (DB không có bill) chỉ có đầu trang, ô ký và khối tổng hợp; **không có tiêu đề cột của 4 khối** nên không dùng để chọn mẫu in được.

## 1. Giao diện (khác khuôn)

Màn chạy báo cáo chuẩn của khung:
- **Bộ lọc trên cùng** (`PARAMS`): **Từ ngày**, **Đến ngày** (cả hai **bắt buộc**), **Thu ngân** (`@USERID`, lookup `SUSER`, không bắt buộc), **Cửa hàng** (`@DCUAHANGID`, lookup `DCUAHANG`, không bắt buộc), nút **Tải dữ liệu (F5)**.
  - **[APP]** mặc định quan sát được: Từ = **ngày 01 của tháng hiện tại**, Đến = hôm nay, Thu ngân trống (in ra "Tất cả"), Cửa hàng **được chọn sẵn** ("TRỤ SỞ CHÍNH"). Cơ chế chọn sẵn cửa hàng **[CHƯA XÁC MINH]** (Q15).
  - Khi **xóa chọn** Cửa hàng/Thu ngân thì truy vấn không lọc theo trường đó (§2; user thường vẫn bị giới hạn cửa hàng theo P2).
- **Một báo cáo cuộn dọc**, theo đúng thứ tự in **[FRX]**: đầu trang → 1. BÁN HÀNG → 2. MẶT HÀNG BÁN → 3. NHẬP HÀNG → 4. THU-CHI → **khối tổng hợp 8 dòng** và ô ký (band `ReportSummary`, tức **cuối báo cáo**; ảnh 24/11 thấy nó ở gần đầu trang vì 4 khối trên không có dữ liệu).
- **Đầu trang:** tên công ty / địa chỉ / điện thoại / email, tiêu đề "CHI TIẾT HOẠT ĐỘNG TRONG NGÀY", "Từ ngày … Đến ngày … Thu ngân: …". Mẫu ghi `Thu ngân: [@USERID]` còn app hiện chữ "Tất cả". **Cửa hàng đã chọn không được in**; v1 phải cho thấy rõ bộ lọc đang áp trên màn.

**Cột từng khối** (nhãn và thứ tự theo mẫu in; khối 1 theo "Mẫu đầy đủ", **đã chốt Q9 ngày 25/11**):

| Khối | Cột | Dòng TỔNG CỘNG cuối khối |
|---|---|---|
| 1. BÁN HÀNG | STT, Số HĐ, Giờ tt, Khách hàng, Tổng cộng, Tiền mặt, Chuyển khoản, **Thẻ ATM** (`TIENTHE`), Voucher, Trừ tích lũy, Thẻ trả trước, Còn nợ | mọi cột tiền của khối |
| 2. MẶT HÀNG BÁN | STT, Mã hàng, Tên hàng, ĐVT, Số lượng, Đơn giá, % G.giá, Thành tiền | 4 dòng, xem §2.5 |
| 3. NHẬP HÀNG | STT, Số phiếu, Nhà cung cấp, Tổng cộng, Tiền thanh toán, Còn nợ | Tổng cộng, Tiền thanh toán, Còn nợ |
| 4. THU-CHI | STT, Số phiếu, Diễn giải, Tên đối tượng, **Tiền mặt: Thu, Chi**; **Chuyển khoản: Thu, Chi** (`THUTM`, `CHITM`, `THUCK`, `CHICK`) | 4 cột tiền |

- Truy vấn bill trả **16 cột**, nhưng `TIENHANG`, `TIENGIAMGIA`, `TIENTHUE`, `PHIVANCHUYEN` **không được in ở mẫu nào** (mẫu có khai báo tổng cho 4 cột này nhưng không ô nào dùng). `DATTRUOC` chỉ in ở mẫu `765b861a`.
- Mẫu `765b861a` in khối 1 với **9 cột**: STT, Số HĐ, Giờ tt, Khách hàng, Tổng cộng, Tiền mặt, Thẻ ATM, **Đặt trước**, Còn nợ. Cột của ba khối còn lại và khối tổng hợp giống nhau.
- v1: bốn khối là **bốn lưới/khối chỉ đọc** trên một tab cuộn, khối tổng hợp nằm dưới cùng, **và** nút In (báo "chưa hỗ trợ" nếu chưa có mẫu, Q5). Lưới khối 1 hiện các cột in của "Mẫu đầy đủ" (Q9; có hiện thêm cột khác hay không: Q13).

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

### 2.5 Dòng tổng khối Mặt hàng bán và tham số "Giảm giá tổng bill" [CODE `AfterGetData` + FRX]

| Dòng (đúng thứ tự in) | Công thức |
|---|---|
| TIỀN HÀNG CHƯA GIẢM GIÁ | Σ (`SOLUONG × DONGIA`); cùng dòng in tổng số lượng Σ `SOLUONG` |
| GIẢM GIÁ TRÊN MẶT HÀNG | Σ (`SOLUONG × DONGIA − THANHTIEN`) |
| GIẢM GIÁ TỔNG BILL | tham số `GiamGiaTongBill` |
| TỔNG CỘNG | Σ `THANHTIEN` − `GiamGiaTongBill` |

`GiamGiaTongBill` = Σ `TIENGIAMGIA` của bill `LOAI = 0 AND DATHANHTOAN = 30` theo cùng khoảng ngày/thu ngân/cửa hàng (truy vấn tổng riêng). Đây là tham số duy nhất mẫu khai báo nên `rpt.Parameters[0]` trỏ đúng nó. Nếu ô Từ/Đến ngày để trống (không xảy ra vì bắt buộc) code gốc dùng `1900-01-01` / `2100-01-01`.

### 2.6 Quyền

Quyền xem = `SFUNCTION` `5d90c04e-…` "Chi tiết hoạt động ngày". `SGROUPROLE` trên bản sao `DATA.fdb`: **Quản lý 240, Kế toán 240, Phục vụ 0**; nhóm khác không có dòng. v1 theo mapping này qua cơ chế phân quyền chung. `NOTE` của quyền này rỗng, không phải `View` (Q6).

### 2.7 Khối tổng hợp 8 dòng [FRX]

Σ là tổng trên toàn bộ dòng của tập tương ứng (sau khi lọc). Công thức giống nhau ở cả 2 mẫu.

| Dòng | Công thức |
|---|---|
| TIỀN MẶT | Σ bill `TIENMAT` + Σ `THUTM` − Σ `CHITM` − Σ `TIENTHANHTOAN` của phiếu nhập **trả tiền mặt** (Q12) |
| CHUYỂN KHOẢN | Σ bill `CHUYENKHOAN` + Σ `THUCK` − Σ `CHICK` − Σ `TIENTHANHTOAN` của phiếu nhập **trả chuyển khoản** (Q12) |
| TIỀN THẺ | Σ bill `THE` |
| VOUCHER | Σ bill `VOUCHER` |
| THẺ TRẢ TRƯỚC | Σ bill `THETRATRUOC` |
| TRỪ TÍCH LŨY | Σ bill `TRUTICHLUY` |
| KHÁCH HÀNG NỢ | Σ bill `CONGNO` |
| NỢ NHÀ CUNG CẤP | Σ "Còn nợ" phiếu nhập (chịu lỗi §3-1) |

`DATTRUOC` không tham gia dòng nào. Hai dòng đầu **khác app gốc** theo quyết định Q12 (app gốc trừ mọi phiếu nhập vào TIỀN MẶT, §3-7). Cách xác định hình thức trả của phiếu nhập: [`quy-tien.md`](quy-tien.md) §4.5 (truy vấn phiếu nhập phải trả thêm `CHUYENKHOAN`). TIỀN THẺ vẫn in riêng một dòng dù thẻ thuộc quỹ Tài khoản. Khi không có dữ liệu, ảnh 24/11 cho thấy 2 dòng đầu hiện 0, 6 dòng còn lại để trống.

## 3. Lỗi / chỗ lệch của app gốc (cần Chiến biết)

1. **"Còn nợ" của phiếu nhập so sai cột:** `COALESCE(LOAITHANHTOAN, 0) < TONGCONG` — so sánh **loại thanh toán** (mã) với tổng tiền, gần như luôn đúng khi tổng > 0, nên "Còn nợ" thực tế = `TONGCONG − TIENTHANHTOAN` ngay cả khi phiếu đã thanh toán đủ bằng cách khác. Có lẽ ý định là `TIENTHANHTOAN < TONGCONG`. (P1)
2. **Thu ngân ở 3 nơi hiểu khác nhau:** bill = người **thu** (`USERTHANHTOANID`); thu chi và phiếu nhập = người **tạo** (`USERCREATEDID`). Chọn một thu ngân → bill của họ nhưng phiếu thu chi do người khác tạo hộ sẽ không hiện.
3. **Cột giảm giá khác nhau giữa các báo cáo:** báo cáo này và Thống kê doanh thu dùng `TIENGIAMGIA` (Thống kê doanh thu cộng thêm `GIAMGIAMATHANG`); chỉ KQKD dùng `TIENGIAMGIATONG` → số có thể lệch nhau.
4. **Bill đã thanh toán trước ngày chọn nhưng `NGAY` lệch**: lọc theo `NGAY` (ngày bill), không theo `GIOTHANHTOAN` — bill qua đêm tính theo ngày mở.
5. **Lọc cửa hàng của phiếu nhập qua kho**, của bill/thu chi qua `DCUAHANGID` → phiếu nhập kho không gắn cửa hàng sẽ **mất** khi lọc cửa hàng.
6. **User thường để trống cửa hàng/thu ngân** thấy dữ liệu **mọi cửa hàng** (không ràng buộc `TNGUOIDUNGTHEOCUAHANG` như lookup), giống KQKD §3-1.
7. **Phiếu nhập trả bằng chuyển khoản bị trừ vào TIỀN MẶT:** dòng TIỀN MẶT (§2.7) trừ `TIENTHANHTOAN` của **mọi** phiếu nhập vì truy vấn không tách hình thức trả; dòng CHUYỂN KHOẢN không trừ khoản nhập nào. Tổng hai dòng không đổi nhưng chia sai ngăn (Q12).
8. **Không lọc `TDONHANG.STATUS`:** các truy vấn bill, mặt hàng, phiếu nhập và `GiamGiaTongBill` đều không lọc; KQKD đã chốt thêm `STATUS = 30` cho bill (spec Thống kê doanh thu hiện chưa ghi) (Q11).
9. **Biên "Đến ngày":** gốc dùng `NGAY BETWEEN @FromDate AND @ToDate`; nếu `@ToDate` là 00:00 thì mất bill ngày cuối có giờ lẻ. KQKD đã chốt tính trọn ngày cuối (Q16).

## 4. Chỗ đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Sửa "Còn nợ" phiếu nhập: `MAX(0, TONGCONG − COALESCE(TIENTHANHTOAN, 0))`. *Mặc định tạm: sửa; nếu Chiến muốn y hệt thì đảo.*
- **P2** Ràng buộc cửa hàng theo user khi để trống, cho bill và thu/chi. **Đã chốt: áp** (Q4/Q10; quyết định chung ghi ở spec KQKD §4 P2). Phiếu nhập lọc qua kho (§2.4) theo cùng danh sách cửa hàng.
- **P3** Truy vấn dùng **tham số**, không nối chuỗi; gom 5 truy vấn thành ít lượt tải.
- **P4** Thu ngân thống nhất = người thu (bill) / người tạo (phiếu khác) — *giữ như gốc, ghi chú trên màn.*

## 5. Câu hỏi chờ Chiến quyết

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | Sửa "Còn nợ" phiếu nhập (P1)? | Sửa |
| Q2 | Có giữ khối "Phiếu nhập" trong báo cáo hoạt động ngày (F&B thuần chỉ bán, nhưng kho/nhập vẫn có)? | Giữ |
| Q3 | "Giảm giá tổng bill": giữ `TIENGIAMGIA` như gốc hay đổi `TIENGIAMGIATONG` cho khớp KQKD? | `TIENGIAMGIA` như gốc |
| Q4 | ~~Ràng buộc cửa hàng theo user (P2)?~~ **Đã chốt: áp** | — |
| Q5 | Mẫu in "Mẫu đầy đủ": tạo FastReport mới hay "chưa hỗ trợ"? | "Chưa hỗ trợ" |
| Q6 | Quyền `5d90c04e-…` có hành vi gì khác quyền `View` không, hay chỉ khác nhãn? | Như quyền xem thông thường |
| Q7 | Thêm xuất Excel? | Không |
| Q8 | Chụp app gốc để xác minh bố cục. Đã chụp bộ lọc, đầu trang, ô ký, khối tổng hợp (24/11). **Còn thiếu:** ảnh khi có dữ liệu (gồm tiêu đề cột các khối) | Có, sau |
| Q9 | ~~Mẫu in mặc định~~ **Đã chốt 25/11: "Mẫu đầy đủ"** `75d5af0a` (khối Bán hàng 12 cột), dù `LASTTEMPLATEID` của dữ liệu gốc đang trỏ mẫu `765b861a`. v1 không đọc `LASTTEMPLATEID` để chọn mẫu | — |
| Q10 | ~~Ràng buộc cửa hàng khi để trống, chốt chung các báo cáo~~ **Đã chốt: áp** (P2) | — |
| Q11 | ~~Bill có thêm `STATUS = 30`?~~ **Chốt 25/11 (issue #8, áp chung mọi báo cáo): thêm cho bill** (khối Bán hàng, Mặt hàng bán, `GiamGiaTongBill`); phiếu nhập giữ như gốc | — |
| Q12 | ~~TIỀN MẶT trừ cả tiền nhập trả bằng chuyển khoản~~ **Đã chốt 25/11: sửa**, tiền nhập trừ đúng quỹ theo hình thức trả ([`quy-tien.md`](quy-tien.md)) | — |
| Q13 | Lưới khối Bán hàng: chỉ cột in của mẫu chốt ở Q9, hay đủ 16 cột truy vấn? | Chỉ cột in của "Mẫu đầy đủ" |
| Q14 | Phiếu thu gắn `TDONHANGID` (khách trả nợ bill) có làm tiền bị cộng 2 lần vào TIỀN MẶT/CHUYỂN KHOẢN không? Cần dữ liệu thật hoặc chạy app gốc | Giữ như gốc |
| Q15 | Cửa hàng chọn sẵn trên bộ lọc lấy từ đâu (cửa hàng của user hay cửa hàng duy nhất)? | User chỉ có 1 cửa hàng thì chọn sẵn, ngược lại để trống |
| Q16 | Biên "Đến ngày": tính trọn ngày cuối như KQKD hay giữ `BETWEEN` như gốc? | Trọn ngày cuối |

## 6. Quyết định đã chốt (áp, không hỏi lại)

Tiền `NUMERIC(18,2)`/`decimal`; chỉ VNĐ; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; bỏ karaoke + tính giờ (không có cột tiền giờ trong báo cáo này); không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB; màn chỉ xem.

## 7. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest tự tạo bill nhiều hình thức thanh toán, vài phiếu thu/chi tiền mặt + chuyển khoản, phiếu nhập có nợ, ≥ 2 thu ngân, ≥ 2 cửa hàng)

- [ ] Bộ lọc: ngày bắt buộc; thu ngân/cửa hàng tùy chọn; mở màn có Từ = ngày 01 tháng hiện tại, Đến = hôm nay (cửa hàng chọn sẵn theo Q15); xóa chọn thì không lọc theo trường đó, riêng user thường vẫn chỉ thấy cửa hàng của mình (P2). Bill ngày cuối có giờ lẻ xử lý theo Q16; bill `STATUS ≠ 30` theo Q11.
- [ ] Khối 1: chỉ bill đã thanh toán trong khoảng; lọc thu ngân theo `USERTHANHTOANID`; đúng cột, nhãn, thứ tự và dòng tổng của "Mẫu đầy đủ" (Q9); sắp `NGAY, NAME`.
- [ ] Khối 2: gộp đúng theo (tên, mã, ĐVT, đơn giá, % giảm); SL trừ hàng trả; loại combo con + xuất vật tư; **không** loại nguyên liệu; đủ **4 dòng tổng** §2.5, `GiamGiaTongBill` khớp Σ `TIENGIAMGIA`.
- [ ] Khối 3 (Nhập hàng): chỉ phiếu `LOAI = 1`; lọc cửa hàng qua kho; "Còn nợ" theo P1 (hoặc theo gốc nếu Chiến đảo, selftest ghi rõ cả hai). Seed thêm phiếu có `DKHONHAPID` NULL để kiểm §3-5.
- [ ] Khối 4 (Thu-chi): tách đúng tiền mặt/chuyển khoản (`CHUYENKHOAN` NULL/0 → tiền mặt); thứ tự cột Thu TM, Chi TM, Thu CK, Chi CK; lọc thu ngân theo `USERCREATEDID`.
- [ ] Khối tổng hợp: nằm cuối; **8 dòng khớp số tính tay** theo §2.7; có phiếu nhập trả tiền mặt (chỉ trừ TIỀN MẶT), trả chuyển khoản (chỉ trừ CHUYỂN KHOẢN), phiếu cũ không ghi hình thức (trừ TIỀN MẶT) và bill có `DATTRUOC` (không ảnh hưởng dòng nào).
- [ ] Quyền theo mapping §2.6.
- [ ] Không SQL trong View; màu/cỡ chữ từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc). Nút In báo "chưa hỗ trợ".

## 8. Gợi ý implement (không bắt buộc)

`DailyActivityRepository` (Data: 5 truy vấn tham số hóa), `DailyActivitySummary` (Services: dòng tổng từng khối, 8 dòng §2.7, "Còn nợ" phiếu nhập; thuần, selftest ≥ 8 ca), `DailyActivityView` (UI: bộ lọc + 4 khối + khối tổng hợp). `main` @ `954f133` **chưa có** component lọc dùng chung (`ReportFilterBar`), nên patch này tự chứa phần lọc ngày/cửa hàng/ràng buộc user (nếu patch báo cáo khác vào `main` trước thì dùng lại), và thêm route `MenuId = b2555738-5bc9-4f84-9e6a-80188bbfac99` trong `ScreenFactory`.
