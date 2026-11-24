# Spec (delta): Thống kê mặt hàng bán

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-24. Base: `main` @ `e9de6de` (đã `git fetch origin main`)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md) và [`thong-ke-doanh-thu.md`](thong-ke-doanh-thu.md) (cùng họ "báo cáo chỉ xem", cùng lọc ngày + cửa hàng + ràng buộc cửa hàng theo user). Spec này **chỉ ghi phần khác**; chỗ không nói = như hai spec trên.
- Menu: **HOẠT ĐỘNG > Thống kê mặt hàng bán** (`SMENU` `a5810c3b-e57a-4cca-99c7-ed89d5dff667`, `SORTORDER = ZZZ021`, `OPERATION = 4`, không `MENUCODE`, `SFORMID` `67500401-1f1e-4c18-a0fc-f487c43f4b0c`, `SFUNCTIONID` `ff5edbec-dee4-4fe8-9315-30c96dec1c4e` "Thống kê mặt hàng bán", nhóm "Bán hàng", `NOTE = View`). Form tùy biến `ThongKeMatHangBan`; nút mở dialog con `ChiTietBanHangTheoMatHang` (`5645abce-…`).
- Chỉ đọc `TDONHANG`, `TDONHANGCHITIET`, `DMATHANG`, `DNHOMMATHANG`, `DCUAHANG`, `TNGUOIDUNGTHEOCUAHANG`. DB chép: `TDONHANG` 0 bản ghi → selftest tự tạo dữ liệu.

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`).
- **[CODE]** `POS_TAP_SRC_EXPORT/SFORM/Thống kê mặt hàng bán__67500401/` (`code.cs`, `layout.xml`), `Chi tiết bán hàng theo mặt hàng__5645abce/`; tài liệu sẵn có [`pos-tap-nghiep-vu-bao-cao.md`](../pos-tap-nghiep-vu-bao-cao.md) §1 (Thống kê mặt hàng bán) — đã đọc, không chép lại.
- **[CHƯA XÁC MINH]**: (1) lệnh `SELECT` gốc của lưới `grDetail` (nằm trong cấu hình lưới, `code.cs` chỉ **thêm** vào `e.Select/GroupBy/Where`; theo dấu hiệu thì nhóm theo `DMATHANGID` + `DONGIA` + `GIAVON` + `TILEGIAMGIA` + `DNHOMMATHANGID`); (2) mẫu in FastReport; (3) điều kiện bill nền của lưới (mặc định tạm: `LOAI = 0 AND DATHANHTOAN = 30`, giống truy vấn tổng bên dưới). **[APP]** đã chụp bố cục + cột lưới (24/11/2026); DB không có bill nên chưa đối chiếu số.

## 1. Bố cục UI (khác khuôn)

- **Hàng lọc/nút:** *Từ ngày – Đến ngày* (mặc định **hôm nay → hôm nay**), *Cửa hàng* (`lueCuaHang`), nút **Refresh (F5)** (phím F5 cũng chạy), nút **In báo cáo**; chọn **cách tính vốn** (`rdCachTinh`, 2 lựa chọn: **0 = giá vốn dòng** `TDONHANGCHITIET.GIAVON` (mặc định), **1 = giá nhập hiện tại** `DMATHANG.GIANHAP`). Đổi bất kỳ lọc nào → tải lại ngay. **[APP]** nhãn 2 nút radio thật là **"Theo giá vốn"** (chọn sẵn) / **"Theo giá nhập"**; có thêm ô nhập **"Lọc"** (nằm giữa radio và nút Refresh) mà bản trước không ghi — lọc lưới theo chuỗi gõ (phạm vi lọc theo cột nào chưa thử, Q10). Cạnh ô *Đến ngày* có mũi tên ▼ (chưa thử).
- **Trái:** cây **nhóm mặt hàng** (`tvNhom`, có nút "Tất cả"; **loại** các nhóm có `DLOAIDOID = LoaiDo.NguyenLieu`). Chọn nhóm → lọc lưới theo `DNHOMMATHANGID`.
- **Giữa:** lưới `grDetail` "Mặt hàng bán" (theo mặt hàng/đơn giá/giảm giá). **[APP] đã chụp 24/11/2026**, 11 cột theo thứ tự (cột đầu là cột chọn dòng): **Mặt hàng** (`TENHANG`), **Số lượng** (`SLXUATCHUAQUYDOI`), **ĐVT**, **Đơn giá**, **Tiền giảm**, **Thành tiền bán** (`THANHTIEN`), **Giá vốn** (`GIAVON`), **Giảm giá %** (`TILEGIAMGIA`), **Thành tiền nhập** (`THANHTIENNHAP`), **Lãi** (`LAI`), **Tỉ lệ lãi** (`TILELAI`). **Không có cột "Nhóm hàng"** (nhóm chỉ lọc qua cây bên trái) và có thêm **ĐVT** + **Tiền giảm**. Sắp theo **mã hàng** (`DMATHANG.CODE`) nếu `Shared.SapXepTheoMa()` bật, ngược lại theo **tên hàng**. **Nhấp đúp** một dòng → mở dialog chi tiết (§2.4).
- **Khối tổng (dưới)** (`pnlBottom`): **[APP]** hàng trên: "Tổng tiền bán:" · "Tổng tiền nhập" · số kèm nhãn "(Tổng tiền lãi bán hàng)"; bên dưới xếp dọc 4 dòng "(Tiền giờ)", "(Tổng tiền giảm giá)", "(Phí dịch vụ)", "(Thuế)", rồi đường kẻ và dòng cuối **"(Tổng tiền lãi)"** màu đỏ cỡ lớn. Quan hệ số học giữa các dòng chưa kiểm được (mọi tổng đang là 0, không có bill); giữ công thức §2.3. Nhãn "(Tổng tiền lãi)" nằm ở dòng cuối cùng (Thực lãi), không phải ở dòng lãi bán hàng như bản trước.
- **Karaoke** (tab "Trả tiền dịch vụ" — dịch vụ theo giờ, nút "Điều chỉnh giờ tính lương"): **cắt** (đã bỏ karaoke + tính giờ). Chỉ giữ một khung "Mặt hàng bán" (không tab).

## 2. Luật và luồng (khác khuôn)

### 2.1 Quyền xem giá nhập [CODE `xemGiaNhap`]

`CanView(XemGiaNhap)` (`SFUNCTION` `1edce531-ffba-4b06-be16-3d7d6bbedb9e` "Xem giá nhập", nhóm "Quản trị"). **Không có quyền** → ẩn cột `GIAVON`, `THANHTIENNHAP`, `THANHTIEN`, `LAI`, `TILELAI`; **ẩn cả khối tổng dưới và nút In** (người dùng chỉ thấy tên hàng, SL, đơn giá, nhóm). Mặc định tạm: admin có; tài khoản khác **không cấp** (Q5 spec Sử dụng dịch vụ).

### 2.2 Dòng lưới [CODE `LoadData`]

Dòng chi tiết **loại trừ**: mặt hàng loại **nguyên liệu** (`DLOAIMATHANGID = LoaiMatHang.NguyenLieu`, mã số hằng không có trong export — Q4), dòng xuất vật tư (`XUATVATTU` ≠ 0), dòng con combo (`COMBOPARENTID` không rỗng). Mỗi dòng tính ở máy:
```
LAI     = THANHTIEN − THANHTIENNHAP
TILELAI = THANHTIEN = 0 ? 0 : 100 × LAI / THANHTIEN
```
`THANHTIEN` = doanh thu dòng (sau chiết khấu dòng, **đã trừ** hàng trả `SLXUAT − SLNHAP`, xem `pos-tap-nghiep-vu-bao-cao.md` §0). `THANHTIENNHAP` = SL quy đổi × (`GIAVON` hoặc `GIANHAP` theo `rdCachTinh`).

### 2.3 Khối tổng [CODE `LoadData`]

```
Tổng tiền bán   = Σ (SLXUATCHUAQUYDOI × DONGIA)      (TRƯỚC chiết khấu dòng, cộng dồn từng dòng lưới)
Tổng tiền nhập  = Σ THANHTIENNHAP                   (cộng lưới)
Tổng lãi        = Tổng tiền bán − Tổng tiền nhập
Giảm giá        = Σ (TIENGIAMGIATONG + GIAMGIAMATHANG) của TDONHANG
Tiền giờ, Phí dịch vụ, Thuế = Σ TIENGIO, Σ PHIDICHVU, Σ TIENTHUE của TDONHANG
Thực lãi        = Tổng lãi − Giảm giá + Thuế + Phí dịch vụ + Tiền giờ
```
`Giảm giá`, `Thuế`, `Phí dịch vụ`, `Tiền giờ` lấy bằng **một truy vấn tổng riêng** trên `TDONHANG`: `LOAI = 0 AND DATHANHTOAN = 30 AND NGAY BETWEEN @From AND @To` (+ lọc cửa hàng, xem dưới). Hiển thị số **nguyên** (`n0`) — v1 chỉ VNĐ (đã chốt Q1 Sử dụng dịch vụ).

**Lỗi/chỗ lệch của gốc cần Chiến biết (chọn P1):**
1. Truy vấn tổng **chỉ lọc cửa hàng khi có chọn**; khi **không chọn** nó **không lọc gì** (kể cả user thường) → user thường thấy tổng giảm giá/thuế/phí của **mọi** cửa hàng trong khi lưới chỉ thấy cửa hàng được phép. Ngoài ra khi **có chọn** mà user thường, nó chỉ kiểm user thuộc cửa hàng nào chứ **không** lọc đúng cửa hàng đã chọn (`DCUAHANGID = chọn` chỉ áp cho admin).
2. "Giảm giá" ở khối tổng là giảm giá **cấp bill** (`TIENGIAMGIATONG` + `GIAMGIAMATHANG`) nhưng "Tổng tiền bán" tính **trước** chiết khấu dòng → khi dòng đã có chiết khấu, lãi dòng (`THANHTIEN − THANHTIENNHAP`) khác `Tổng lãi` gốc; **Thực lãi** theo công thức gốc không bằng Σ `LAI` của lưới. Giữ nguyên gốc, ghi cho Chiến.
3. `Thực lãi` **cộng** thuế và phí dịch vụ (và tiền giờ) vào lãi — đúng công thức gốc, dù thuế thường là khoản phải nộp, không phải lãi (Q3).

### 2.4 Dialog "Chi tiết bán hàng theo mặt hàng" [CODE `ChiTietBanHangTheoMatHang`]

Nhấp đúp dòng → dialog (tiêu đề nút "DANH MỤC HÓA ĐƠN", nút **Thoát**): hai lưới.
- **Lưới trên `grMain`:** danh sách **bill** (`TDONHANG`) trong khoảng ngày **có chứa** dòng với **cùng mặt hàng, cùng `DONGIA`, cùng `TILEGIAMGIA`** của dòng đã nhấp (điều kiện `EXISTS` trên `TDONHANGCHITIET`); lấy `@GIAVON` chỉ để truyền/hiển thị.
- **Lưới dưới `grDetail`:** dòng chi tiết của bill đang chọn (loại xuất vật tư và combo con); dòng của **mặt hàng đang xem** tô **đỏ + đậm** (`DMATHANGID` khớp), dòng khác mặc định. Đổi bill = tải lại.
- Chỉ xem; không sửa. Bấm **Thoát** đóng.

### 2.5 Tự làm tươi và in

- Tự làm tươi khi `TDONHANG` đổi sau lần tải cuối (`NeedRefresh`).
- In: `PrintInvoice(Forms.ThongKeMatHangBan, …)` với dữ liệu lưới + tham số `TuNgay`, `DenNgay`, `TongTienBan`, `TongTienNhap`, `TongGiamGia`, `TongLai`, `ThucLai`. Mẫu FastReport chưa đọc được → v1 nút In báo "chưa hỗ trợ" (Q5). Ẩn khi không có quyền xem giá nhập (§2.1).

## 3. Chỗ đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Truy vấn tổng (giảm giá/thuế/phí/giờ) áp **cùng bộ lọc cửa hàng + ràng buộc user** như lưới (sửa lỗi §2.3-1). *Mặc định tạm: sửa (P1), vì số liệu sai lệch phạm vi là lỗi rõ ràng; nếu Chiến muốn y hệt gốc thì đảo.*
- **P2** Truy vấn dùng **tham số**, không nối chuỗi ID cửa hàng/user/mặt hàng.
- **P3** Chú thích rõ công thức "Thực lãi" trên màn (tooltip), không đổi công thức.
- **P4** Giữ nhóm đang chọn khi tự làm tươi.

## 4. Câu hỏi chờ Chiến quyết

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | Sửa lỗi lọc cửa hàng của truy vấn tổng (P1)? | Sửa |
| Q2 | Công thức dùng `GIAVON` hay `GIANHAP` mặc định? | `GIAVON` (như gốc) |
| Q3 | "Thực lãi" cộng thuế + phí dịch vụ: giữ nguyên? | Giữ nguyên gốc |
| Q4 | Mã số `LoaiMatHang.NguyenLieu` / `LoaiDo.NguyenLieu` (hằng trong thư viện) | Nhận diện nguyên liệu bằng loại mặt hàng/loại đồ tương ứng trong DB; **không đọc `LOAIDINHLUONG`** |
| Q5 | Mẫu in: tạo FastReport mới hay "chưa hỗ trợ"? | "Chưa hỗ trợ" |
| Q6 | Lưới gộp theo (mặt hàng, đơn giá, giá vốn, % giảm) hay chỉ mặt hàng? | Như dấu hiệu gốc (theo 4 trường) |
| Q7 | Mặt hàng loại dịch vụ theo giờ (karaoke) có hiện trong lưới? | Cắt (không có mặt hàng này trong F&B thuần) |
| Q8 | Có thêm xuất Excel (gốc không có)? | Không |
| Q9 | ~~Chụp app gốc để lấy đủ cột lưới~~ **Đã chụp 24/11/2026** (§1) | Đã giải quyết phần cột |
| Q10 | Ô "Lọc" (gốc có, bản trước chưa ghi): lọc theo cột nào, có bắt buộc làm không | Lọc theo tên hàng, làm |

## 5. Quyết định đã chốt (áp, không hỏi lại)

LamTron 1000 AwayFromZero (màn chỉ cộng, không làm tròn thêm); tiền `NUMERIC(18,2)`/`decimal`; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; bỏ karaoke + tính giờ (khung "Trả tiền dịch vụ", điều chỉnh giờ tính lương); chỉ VNĐ; không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB; màn chỉ xem.

## 6. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest tự tạo ≥ 4 mặt hàng, 2 nhóm, vài bill có chiết khấu và trả hàng)

- [ ] Mở màn: ngày mặc định hôm nay; chỉ bill đã thanh toán trong khoảng; cây nhóm có "Tất cả" và **không** có nhóm nguyên liệu; lọc theo nhóm đúng.
- [ ] Lưới loại bỏ dòng nguyên liệu, xuất vật tư, combo con; sắp theo mã hoặc tên theo cấu hình.
- [ ] `LAI`, `TILELAI` đúng từng dòng; `TILELAI = 0` khi `THANHTIEN = 0`; đổi `rdCachTinh` đổi `GIAVON`/`GIANHAP` đúng.
- [ ] Khối tổng khớp công thức §2.3 (kể cả khi dòng có chiết khấu và có hàng trả: `SLXUAT − SLNHAP`); số liệu giảm giá/thuế/phí theo bộ lọc cửa hàng đúng (P1) hoặc y hệt gốc nếu Chiến đảo.
- [ ] Không có quyền "Xem giá nhập": ẩn 5 cột, khối tổng, nút In; admin thấy đủ.
- [ ] Nhấp đúp dòng: dialog liệt kê đúng các bill chứa đúng (mặt hàng, đơn giá, % giảm); chọn bill hiện đúng dòng, dòng mặt hàng đang xem tô đỏ đậm.
- [ ] Đổi lọc/F5/tự làm tươi tải lại; không SQL trong View; màu từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc).

## 7. Gợi ý implement (không bắt buộc)

`ItemSalesRepository` (Data: lưới mặt hàng, tổng bill, danh sách bill chứa dòng, chi tiết bill), `ItemSalesCalculator` (Services, thuần: `LAI`, `TILELAI`, tổng, thực lãi — selftest ≥ 8 ca), `ItemSalesView` + `ItemSalesDetailDialog` (UI). Tái dùng `BillListQuery`/ràng buộc cửa hàng của Thống kê doanh thu.
