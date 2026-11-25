# Spec (delta): Thống kê doanh thu

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code). Rà lại 25/11 theo workflow-agy §10.2: loại **nặng**; các câu tiền đã chốt, còn Q5, Q7, Q10, Q11 gửi Chiến (issue `cho-chien`).
- Ngày: 2026-11-24, rà lại 2026-11-25. Base: `main` @ `62a4fc0`
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md). Spec này **chỉ ghi phần khác khuôn**; chỗ không nói = như khuôn. Là màn **báo cáo chỉ xem**, không thêm/sửa/xóa dữ liệu.
- Menu: **HOẠT ĐỘNG > Thống kê doanh thu** (`SMENU` `c5164013-298f-4565-8594-819bc7ce992a`, `SORTORDER = ZZZ020`, `OPERATION = 4`, không `MENUCODE`, `SFORMID` `d913c8d9-4395-476c-9612-8ca7f35b6268`, `SFUNCTIONID` `b2a025da-9c16-4578-a864-bf32c4eb2d09` "Thống kê doanh thu", nhóm "Bán hàng", `NOTE = View`). Form tùy biến `ThongKeDoanhThu`.
- Dữ liệu đọc: `TDONHANG` (danh sách bill), `TDONHANGCHITIET` (chi tiết), `TTHUCHI` (thu/chi khác), `DCUAHANG`, `TNGUOIDUNGTHEOCUAHANG`. DB chép hiện: `TDONHANG` 0, `TTHUCHI` 0, `DCUAHANG` 1, `TNGUOIDUNGTHEOCUAHANG` 0 → **không có dữ liệu mẫu**, selftest tự tạo.

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`).
- **[CODE]** `POS_TAP_SRC_EXPORT/SFORM/Thống kê doanh thu__d913c8d9/` (`code.cs`, `layout.xml`); tài liệu sẵn có [`pos-tap-nghiep-vu-bao-cao.md`](../pos-tap-nghiep-vu-bao-cao.md) §1 và `docs/deep/bao-cao.md` (C3) — đã đọc, không chép lại.
- **[ĐÃ GIẢI MÃ 25/11]** cấu hình lưới (`ViewConfig` trong `layout.xml`): lưới chính chỉ có điều kiện `NGAY BETWEEN @FromDate AND @ToDate` đang bật; `LOAI = 0` và `DATHANHTOAN = 30` có khai báo nhưng cờ `CHECKED = false` (giống Thống kê mặt hàng bán). **[CHƯA XÁC MINH]** mẫu in FastReport. **[APP]** đã chụp bố cục và cột lưới (24/11/2026; DB không có bill nên chưa đối chiếu số).
- **Điều kiện bill áp cho v1 (Chiến chốt 25/11, issue #8, áp chung mọi báo cáo):** dùng `BillReportScope` đã có trên `main` (`67319b2`): `LOAI = 0`, `DATHANHTOAN = 30`, `STATUS = 30`, ngày "Đến" tính trọn ngày, lọc cửa hàng theo user như §2.1.

## 1. Bố cục UI (khác khuôn)

Một tab "Thống kê doanh thu":

- **Hàng lọc/nút:** *Từ ngày – Đến ngày* (`dtNgay`, mặc định **hôm nay → hôm nay** theo ngày DB), *Cửa hàng* (`lueCuaHang`, lookup `DCUAHANG`), nút **Refresh (F5)**, nút **In báo cáo**. Đổi ngày hoặc cửa hàng **tải lại ngay**.
- **Lưới chính** (`grMain`, danh sách bill, sắp `NGAY, GIOTHANHTOAN`): mỗi dòng một bill; cột giờ `BATDAU`, `KETTHUC` định dạng `HH:mm`. **[APP] đã chụp 24/11/2026**, 14 cột theo thứ tự (cột đầu là cột chọn dòng, không tiêu đề): **Số phiếu, Ngày, Bàn, Giờ vào, Giờ ra, Giờ thanh toán, Khách hàng, Tiền giảm giá, Tổng cộng, Thanh toán bởi, Tiền hàng, Ghi chú, Địa chỉ, Mã khách**. Lưới **không** có cột tiền mặt/thẻ/chuyển khoản/công nợ (các khoản này chỉ có ở khối tổng dưới); có thanh cuộn ngang. DB mẫu không có bill nên chưa thấy được định dạng số/ngày trong ô.
- **Lưới chi tiết** (`grDetail`, **bên phải** lưới chính, không phải dưới như ghi trước đó): các dòng `TDONHANGCHITIET` của **bill đang chọn** (đổi dòng = tải lại; chưa chọn → trống). **[APP]** cột: **Mặt hàng, ĐVT, Đơn giá, CK%, Thành tiền** (có thanh cuộn ngang nên có thể còn cột khác ngoài vùng nhìn thấy).
- Cạnh ô *Đến ngày* có mũi tên ▼ (thu/mở thanh lọc) **[APP]**; hành vi chưa thử.
- **Khối tổng cộng** (nhãn in hoa, 3 nhóm; số căn phải): 

| Nhóm | Dòng (nhãn nguyên văn) |
|---|---|
| Doanh thu | **TIỀN HÀNG**, **TIỀN GIỜ**, **GIẢM GIÁ TIỀN HÀNG** (hiện dấu `−`), **GIẢM GIÁ TIỀN GIỜ** (`−`), **TỔNG GIẢM GIÁ** (`−`), **PHÍ DỊCH VỤ**, **THUẾ**, **TỔNG DOANH THU** |
| Hình thức thanh toán | **TIỀN MẶT**, **TIỀN THẺ**, **CHUYỂN KHOẢN**, **VOUCHER**, **TRỪ TÍCH LŨY**, **THẺ TRẢ TRƯỚC**, **CÔNG NỢ** |
| Thu chi | **THU KHÁC**, **CHI KHÁC**, **TỔNG THỰC THU (TIỀN MẶT + THẺ + CHUYỂN KHOẢN + THU - CHI)** |

## 2. Luật và luồng (khác khuôn)

### 2.1 Danh sách bill [CODE `grMain_CustomLoadData`]

- Tham số `@FromDate`, `@ToDate` kiểu **ngày** (không giờ); lọc theo `NGAY` của bill (khi bill tạo qua đêm thì tính theo `NGAY` ghi trên bill, **không** theo giờ thanh toán).
- **Cửa hàng:**
  - chọn cửa hàng → thêm `DCUAHANGID = <đã chọn>`;
  - **không chọn** và tài khoản **không phải admin** → chỉ các bill có `DCUAHANGID` thuộc `TNGUOIDUNGTHEOCUAHANG` của user hiện tại (`SUSERID = UserID`);
  - **không chọn** và là admin → tất cả cửa hàng.
- Lookup cửa hàng của user thường chỉ liệt kê cửa hàng gắn với họ (cùng bảng `TNGUOIDUNGTHEOCUAHANG`); admin thấy tất cả.
- Sắp xếp `NGAY`, `GIOTHANHTOAN` (tăng dần).

### 2.2 Chi tiết bill [CODE `grDetail_CustomLoadData`]

Dòng của bill chọn **trừ**: dòng xuất vật tư (`COALESCE(XUATVATTU, 0) <> 0`), dòng nguyên liệu (`DMATHANG.DLOAIMATHANGID = '2'`, `LoaiMatHang.NguyenLieu = 2` đọc từ DB 25/11), dòng con combo (`COMBOPARENTID` không rỗng). Chưa chọn bill → trống.

Cột theo cấu hình lưới gốc (ẩn cột ID): **Mặt hàng (`TENHANG`), ĐVT, S.lượng (`SLXUATCHUAQUYDOI`), Đơn giá, CK%, Thành tiền, Ghi chú**. Ảnh app chỉ thấy 5 cột đầu do cuộn ngang. Cấu hình gốc còn Mã hàng, Bảo hành, Mã sẵn có: v1 bỏ (không dùng ở F&B), Q10.

### 2.3 Công thức khối tổng cộng [CODE `LoadData`]

Mọi tổng là **Σ trên các dòng đang hiển thị ở lưới chính** (không truy vấn riêng); riêng THU/CHI lấy từ `TTHUCHI`.

```
TIỀN HÀNG          = Σ TIENHANGCHUAGIAM            (tiền hàng TRƯỚC giảm)
TIỀN GIỜ           = Σ TIENGIO                      (v1: ghi 0 — tiền giờ đã cắt)
GIẢM GIÁ TIỀN HÀNG = Σ TIENGIAMGIA + Σ GIAMGIAMATHANG
GIẢM GIÁ TIỀN GIỜ  = Σ TIENGIAMGIAGIO              (v1: 0)
TỔNG GIẢM GIÁ      = GIẢM GIÁ TIỀN HÀNG + GIẢM GIÁ TIỀN GIỜ
PHÍ DỊCH VỤ        = Σ PHIDICHVU
THUẾ               = Σ TIENTHUE
TỔNG DOANH THU     = Σ TONGCONG                    (đã gồm thuế, phí DV, đã trừ giảm giá)
TIỀN MẶT, VOUCHER, TIỀN THẺ(=THE), CHUYỂN KHOẢN, TRỪ TÍCH LŨY, THẺ TRẢ TRƯỚC, CÔNG NỢ = Σ các cột TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, CONGNO
THU KHÁC = Σ COALESCE(THU,0) , CHI KHÁC = Σ COALESCE(CHI,0)   của TTHUCHI với NGAY >= Từ AND NGAY < Đến + 1 ngày, lọc cửa hàng như bill (Q13a)
TỔNG THỰC THU = TIỀN MẶT + TIỀN THẺ + CHUYỂN KHOẢN + THU KHÁC − CHI KHÁC
```
- Ba dòng "Thực thu" **không** cộng VOUCHER, TRỪ TÍCH LŨY, THẺ TRẢ TRƯỚC, CÔNG NỢ (không phải tiền thực nhận) — đúng như gốc, nhưng nhãn không nói rõ → ghi chú trong tooltip (P2).
- `TTHUCHI` lọc **chỉ theo ngày**, **không** theo cửa hàng, **không** theo `STATUS` (hóa đơn khác cửa hàng vẫn tính thu/chi khác) → lệch với phần bill; **v1 sửa: lọc theo cửa hàng (P1/Q3, đã chốt)**.
- Định dạng: `GiaBanLeUsd = 30` ([DB]) → gốc hiển thị `n2` (2 số lẻ), ngược lại `n0`. **Đã chốt (Q1 Sử dụng dịch vụ):** v1 chỉ VNĐ, hiển thị số nguyên có dấu phân nhóm nghìn; tiền `decimal`.

### 2.4 Tự làm tươi [CODE `IRefreshable`]

Khi `TDONHANG` hoặc `TTHUCHI` có thay đổi sau lần tải cuối (`Config.NeedRefresh`), tab tự `LoadData()` mà không giữ vị trí dòng đang chọn (gốc) → P3.

### 2.5 In [CODE `btnIn_Click`]

`PrintInvoice(Forms.ThongKeDoanhThu, …)` với dữ liệu = **toàn bộ lưới chính** (bill) + tham số `TuNgay`, `DenNgay`. Mẫu FastReport "Thống kê doanh thu" nằm trong `SREPORTTEMPLATE`; **chưa có bản đọc được** (Q5). v1: nút In có, báo "chưa hỗ trợ" nếu chưa có mẫu.

### 2.6 Quyền

Quyền xem màn = `SFUNCTION` `b2a025da-…` "Thống kê doanh thu". Mặc định tạm tài khoản không phải admin: **không cấp** (Q5 spec Sử dụng dịch vụ). Không có quyền riêng cho nút In. Ràng buộc cửa hàng theo §2.1 là **ngoài** quyền xem màn.

## 3. Chỗ đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Lọc thu/chi khác theo **cửa hàng** giống bill (gốc không lọc; `TTHUCHI` **có** cột `DCUAHANGID` [DB], cũng có `STATUS`, `TDONHANGID`, `LOAI`, `CHUYENKHOAN`, `LATAMUNG` — gốc không lọc các cột này nên phiếu chi trả lại khách từ bill hoặc phiếu tạm ứng cũng được cộng vào THU/CHI KHÁC; cần Chiến xác nhận có đúng ý, Q3). **Đã chốt 24/11/2026 (Q13a, theo review issue #3): LỌC thu/chi khác theo cửa hàng** (như bill: chọn cửa hàng → `DCUAHANGID` đó; để trống + không phải admin → các cửa hàng của user), áp chung cho Thống kê doanh thu, Tổng hợp KQKD, Chi tiết hoạt động ngày. Các cột `STATUS`/`LOAI`/`LATAMUNG` vẫn **không** lọc (giữ như gốc).
- **P2** Thêm chú thích/tooltip cho dòng "TỔNG THỰC THU" liệt kê các khoản được cộng.
- **P3** Giữ dòng đang chọn khi tự làm tươi.
- **P4** Truy vấn cửa hàng dùng **tham số**, không nối chuỗi ID (gốc nối chuỗi `'` + ID + `'`).
- **P5** Tính tổng bằng truy vấn tổng hợp (`SUM`) thay vì cộng lưới → đúng khi bill nhiều (v1 vẫn kết quả như gốc).

## 4. Câu hỏi chờ Chiến quyết

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | ~~Điều kiện bill nền~~ **Đã chốt 25/11 (issue #8):** `BillReportScope` | — |
| Q2 | ~~`LOAI = 5, 6`?~~ **Đã chốt theo Q1:** chỉ `LOAI = 0` | — |
| Q3 | ~~Thu/chi khác có lọc theo cửa hàng (P1)?~~ **Đã chốt: lọc (Q13a)** | — |
| Q4 | ~~Mã số `LoaiMatHang.NguyenLieu`~~ **Đã xác minh: 2** | — |
| Q5 | Mẫu in: tạo mới FastReport hay "chưa hỗ trợ" | "Chưa hỗ trợ" |
| Q6 | Hiển thị `n2` khi `GiaBanLeUsd = 30` (đã chốt bỏ USD → luôn số nguyên) | Số nguyên |
| Q7 | Tiền giờ/giảm giá giờ: ẩn dòng hay hiện 0 (v1 đã cắt tiền giờ) | Ẩn 2 dòng giờ, vẫn tính vào tổng (0) |
| Q8 | Xuất Excel lưới chính (gốc không có nút) | Không |
| Q9 | ~~Chụp app gốc để lấy đủ cột lưới~~ **Đã chụp 24/11/2026** (xem §1); còn thiếu ảnh khi có bill thật | Đã giải quyết phần cột |
| Q10 | Lưới chi tiết bỏ Mã hàng, Bảo hành, Mã sẵn có (§2.2)? | Bỏ |
| Q11 | Tự làm tươi khi máy khác đổi dữ liệu: bản viết lại chưa có cơ chế (như Thống kê mặt hàng bán) | v1 chưa làm, chỉ F5 và đổi lọc |

## 5. Quyết định đã chốt (áp, không hỏi lại)

LamTron 1000 AwayFromZero (chỉ khi **tính** tiền, màn này chỉ cộng nên không làm tròn thêm); tiền `NUMERIC(18,2)`/`decimal`; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; bỏ tiền giờ/karaoke; chỉ VNĐ (Q1 Sử dụng dịch vụ); không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB.

## 6. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest tự tạo ≥ 3 bill nhiều hình thức thanh toán + vài phiếu thu/chi)

- [ ] Mở màn: ngày mặc định hôm nay → hôm nay; lưới chính chỉ bill đã thanh toán trong khoảng ngày; sắp `NGAY`, `GIOTHANHTOAN`.
- [ ] Admin không chọn cửa hàng → thấy mọi cửa hàng; user thường không chọn → chỉ cửa hàng gắn trong `TNGUOIDUNGTHEOCUAHANG`; chọn cửa hàng → chỉ cửa hàng đó.
- [ ] Từng dòng khối tổng khớp công thức §2.3 trên bộ bill mẫu (kiểm số tay), kể cả dấu `−` ở 3 dòng giảm giá; TỔNG THỰC THU không cộng voucher/tích lũy/thẻ trả trước/công nợ.
- [ ] Bill chi tiết loại bỏ dòng xuất vật tư, nguyên liệu, combo con; chưa chọn bill → lưới chi tiết trống.
- [ ] THU/CHI khác lọc theo ngày đúng; phiếu thu/chi của cửa hàng khác **không** được cộng khi đã chọn cửa hàng (P1 đã chốt, Q13a).
- [ ] Đổi ngày/cửa hàng tải lại ngay; F5 tải lại; thay đổi `TDONHANG`/`TTHUCHI` từ máy khác tự làm tươi.
- [ ] Nút In báo "chưa hỗ trợ" (hoặc in đúng mẫu nếu có); không SQL trong View; màu/cỡ chữ từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc).

## 7. Gợi ý implement (không bắt buộc)

`RevenueReportRepository` (Data: danh sách bill theo khoảng ngày + cửa hàng + ràng buộc user; chi tiết bill; tổng thu/chi), `RevenueSummary.Calculate(IEnumerable<BillRow>, thu, chi)` (Services, thuần, selftest ≥ 8 ca), `RevenueReportView` (UI). Có thể dùng chung `BillListQuery` với màn Quản lý bán hàng/Danh mục hóa đơn hủy để không viết lại truy vấn danh sách bill.
