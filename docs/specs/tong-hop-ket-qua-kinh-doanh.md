# Spec (delta): Tổng hợp kết quả kinh doanh

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-24. Base: `main` @ `9add46e` (đã `git fetch origin main`)
- Khuôn: [`danh-muc-mat-hang.md`](danh-muc-mat-hang.md); cùng họ "báo cáo chỉ xem" với [`thong-ke-doanh-thu.md`](thong-ke-doanh-thu.md). Spec này **chỉ ghi phần khác**; chỗ không nói = như khuôn.
- Menu: **HOẠT ĐỘNG > Tổng hợp kết quả kinh doanh** (`SMENU` `eb49f730-19d4-435e-b852-05b553600e88`, `SORTORDER = ZZZ022`, **`OPERATION = 5`** = chạy báo cáo, không có form riêng, `SFORMID = NULL`, `SREPORTID` `9c30f012-bec1-4d53-b8f9-7abf9d476d7d`, `SFUNCTIONID` `1866efc8-397a-45ad-9ef4-fc8640e3d7c0` "Tổng hợp kết quả kinh doanh", nhóm "Bán hàng", `NOTE = View`).
- Là **báo cáo** `BÁO CÁO KẾT QUẢ KINH DOANH` (`SREPORT` `9c30f012-…`, lớp `BaoCaoKetQuaKinhDoanh`, lọc trên đầu `FILTERONTOP = 30`), một bảng kết quả dựng bằng code rồi in/hiển thị qua FastReport (một mẫu `SREPORTTEMPLATE` tên "BÁO CÁO KẾT QUẢ KINH DOANH", `id e1645e70-…`).

## 0. Nguồn, cách đọc, giới hạn

- **[DB]** `POS TAP\Database\DATA.fdb` bản chép (không dùng `DEMO.FDB`): `TDONHANG` 0 bản ghi, `TTHUCHI` 0 bản ghi; `DLYDOTHUCHI` **23 lý do** (8 `LOAILYDO = 1` "định phí", 7 `LOAILYDO = 2` "chi khác", 5 `-1`, 1 `0`, 2 NULL).
- **[CODE]** `POS_TAP_SRC_EXPORT/SREPORT/BÁO CÁO KẾT QUẢ KINH DOANH__9c30f012/` (`query.sql` rỗng-có-cột, `code.cs` lớp `BaoCaoKetQuaKinhDoanh` tính toàn bộ); tài liệu [`pos-tap-nghiep-vu-bao-cao.md`](../pos-tap-nghiep-vu-bao-cao.md) §4 và `docs/deep/bao-cao.md` (C20, C21) — đã đọc, không chép lại.
- **[CHƯA XÁC MINH]**: bố cục mẫu in FastReport (`.frx` nằm trong `SREPORTTEMPLATE`, bản export không có); ý nghĩa `LOAI = 5, 6` của bill (không có chỗ nào sinh ra, xem Q1); font biểu tượng ▲/▼. **[APP]** đã chụp bố cục (24/11/2026); chưa cuộn tới dòng LÃI LỖ và chưa có số liệu thật.

## 1. Giao diện (khác khuôn)

Màn chạy báo cáo chuẩn của khung (không phải form tùy biến):
- **Bộ lọc trên cùng** (`PARAMS`): **Từ ngày** (`@FromDate`, ngày, **bắt buộc**), **Đến ngày** (`@ToDate`, ngày, **bắt buộc**), **Cửa hàng** (`@DCUAHANGID`, lookup `DCUAHANG`, không bắt buộc; user **không phải admin** chỉ thấy cửa hàng gắn trong `TNGUOIDUNGTHEOCUAHANG`, admin thấy tất cả). Để trống cửa hàng = mọi cửa hàng (xem lệch §3-1).
- Nút **Xem/In báo cáo** (mẫu "BÁO CÁO KẾT QUẢ KINH DOANH"); bảng kết quả hiển thị theo cột: **Nội dung · Tháng này · Tỉ lệ · Tăng/giảm · Tháng trước** (cột biểu tượng ▲/▼ cạnh Tăng/giảm). Dòng **cấp**: `LOAI` 0 DOANH THU (đậm), 1 dòng con (nhóm hàng/Tiền giờ/Giảm giá/Phí dịch vụ/Thuế), 2 CHI PHÍ (đậm), 3 Định phí, 4 chi tiết định phí, 5 Biến phí, 6 chi tiết biến phí (theo nhóm hàng), 7 Chi khác, 8 chi tiết chi khác, 9 LÃI LỖ (đậm). Bố cục chính xác theo mẫu in **[CHƯA XÁC MINH]**.
- **[APP] đã chụp 24/11/2026 (DB không có bill, mọi số 0):**
  - Thanh lọc thật: **Từ ngày** mặc định **ngày 01 của tháng hiện tại**, **Đến ngày** hôm nay, **Cửa hàng** tự chọn sẵn cửa hàng của user (ví dụ "TRỤ SỞ CHÍNH"), nút **Tải dữ liệu (F5)**; dưới là thanh công cụ trình xem FastReport (In, mở, lưu, gửi mail, tìm, chuyển trang, Close). Báo cáo hiển thị dạng **trang in** chứ không phải lưới chỉ đọc; đầu trang có khối công ty (đang là `(TÊN CÔNG TY)`, `(ĐỊA CHỈ)`…) và tiêu đề **"BÁO CÁO KẾT QUẢ KINH DOANH"**.
  - **Cột thật** (từ trái): số mục (I., 1, A.…) · Nội dung · `%/DT` (nhỏ, nằm dưới tiêu đề Giá trị) · **Giá trị** · **%** · `%/CP` · **Tăng giảm so với tháng trước** (một cột biểu tượng + một cột số) · **KQKD tháng trước**. Hàng nhỏ `%/DT` và `%/CP` chèn giữa các nhóm làm chú thích cột tỉ lệ.
  - **Dòng cố định** thấy được: **I. DOANH THU** (1 Tiền giờ, 2 Giảm giá, 3 Phí dịch vụ, 4 Thuế); **II. CHI PHÍ**: **A. Định phí** (1 Tiền nhà, 2 Tiền điện thoại, 3 Tiền nước, 4 Tiền điện, 5 Lương nhân viên, 6 Lương vệ sỹ, 7 Thưởng nhân viên, 8 Chi lương nhân viên), **B. Biến phí** (chưa có dòng con khi rỗng), **C. Chi khác** (1 Văn phòng phẩm, in ấn; 2 Xây dựng, sửa chữa, thiết kế; 3 Đồ dùng, dụng cụ; 4 Vận chuyển; 5 Ngoại giao; 6 Chi khác; 7 Đặt trước). Các dòng này hiện ngay cả khi không có số liệu (số 0, `-`). Phần dưới (LÃI LỖ) chưa cuộn tới để chụp.
  - Dòng "Tiền giờ" vẫn nằm trong mẫu gốc dù v1 cắt tiền giờ → v1 ghi 0 hoặc bỏ dòng (xem Q7).
- v1: hiển thị bảng trên màn (lưới chỉ đọc, dòng cấp 0/2/9 đậm) **và** nút In (mẫu in → "chưa hỗ trợ" nếu chưa có FastReport, Q5).

## 2. Luật tính [CODE `AfterGetData`] — mọi giá trị là **tổng theo khoảng ngày** của `NGAY`

"Tháng này" = `[Từ ngày, Đến ngày]`; "Tháng trước" = **cùng khoảng lùi đúng 1 tháng lịch** (`DATEADD(MONTH, -1, …)` cả hai đầu).
**Tập bill tính doanh thu:** `(LOAI = 0 AND DATHANHTOAN = 30) OR LOAI = 5 OR LOAI = 6`, lọc cửa hàng nếu chọn (`DCUAHANGID = chọn`, bỏ qua khi trống).

| Dòng (`LOAI` dòng báo cáo) | Giá trị |
|---|---|
| **DOANH THU** (0) | `Σ TONGCONG` (đã gồm thuế, phí DV; đã trừ giảm giá) |
| Từng **nhóm mặt hàng** (1) | `Σ THANHTIEN` dòng chi tiết bill `LOAI = 0 AND DATHANHTOAN = 30` (**không** `LOAI` 5/6; **không** loại vật tư/combo con), nhóm = `DNHOMMATHANG.NAME`, mặt hàng chưa có nhóm → **"(Chưa phân nhóm)"**; chỉ liệt kê nhóm có tháng này hoặc tháng trước ≠ 0 |
| **Tiền giờ** (1) | `Σ TIENGIO` — v1 **ẩn** (bỏ tiền giờ), giá trị 0 |
| **Giảm giá** (1) | `Σ TIENGIAMGIATONG` |
| **Phí dịch vụ** (1) | `Σ PHIDICHVU` |
| **Thuế** (1) | `Σ TIENTHUE` |
| Từng lý do **định phí** (4) | `Σ CHI` của `TTHUCHI` có `DLYDOTHUCHIID` = lý do **và** `DLYDOTHUCHI.LOAILYDO = 1`, theo `NGAY` + cửa hàng |
| **Định phí** (3) | Σ các lý do trên |
| Từng nhóm **biến phí** (6) | `Σ (SLXUAT − SLNHAP) × GIAVON` dòng chi tiết bill `(LOAI = 0 AND DATHANHTOAN = 30) OR LOAI = 5 OR LOAI = 6`, không xuất vật tư, không combo con, nhóm như trên ("(Chưa phân nhóm)") |
| **Biến phí** (5) | Σ các nhóm |
| Từng lý do **chi khác** (8) | như định phí nhưng `LOAILYDO = 2` |
| **Chi khác** (7) | Σ các lý do trên |
| **CHI PHÍ** (2) | Định phí + Biến phí + Chi khác |
| **LÃI LỖ** (9) | `DOANH THU − CHI PHÍ` |

- Cột **Tăng/giảm** = `Tháng này − Tháng trước` (riêng LÃI LỖ xem lỗi gốc §3-2). Biểu tượng: dấu theo `Tăng/giảm` (> 0 mũi lên, < 0 mũi xuống, 0 trống).
- Cột **Tỉ lệ**: gốc **hard-code chuỗi `"100%"`** cho mọi dòng (`tiLeDoanhThu = "100%"`); không phải tỉ lệ thật (xem Q3).
- Số bill (`COUNT(*)`) được tính nhưng **không hiển thị** ở đâu (dư).
- Số tiền: `decimal`, hiển thị số nguyên (đã chốt VNĐ); không làm tròn thêm (chỉ cộng/trừ).

## 3. Lỗi / chỗ lệch của app gốc (cần Chiến biết)

1. **Lọc cửa hàng:** truy vấn tổng bill dùng `(@DCUAHANGID = '' OR DCUAHANGID = @DCUAHANGID)` → user thường để trống thấy **tổng mọi cửa hàng** dù lookup chỉ cho chọn cửa hàng của họ (lệch với Thống kê doanh thu).
2. **Công thức Tăng/giảm LÃI LỖ sai:** `TangGiam = (DS_Nay − CP_Nay) − DS_Truoc − CP_Truoc` (thiếu ngoặc quanh `DS_Truoc − CP_Truoc`; đúng phải là `(DS_Nay − CP_Nay) − (DS_Truoc − CP_Truoc)`). In ra thấp hơn số đúng **`2 × CP_Truoc`**.
3. **Doanh thu theo nhóm không khớp DOANH THU:** dòng nhóm dùng `Σ THANHTIEN` (trước thuế, phí, giảm giá bill) và **chỉ** bill `LOAI = 0`; DOANH THU dùng `Σ TONGCONG` gồm cả `LOAI 5/6` → tổng các dòng con **không** bằng DOANH THU.
4. **LÃI LỖ** = `TONGCONG` (đã gồm thuế + phí dịch vụ, chưa trừ giá vốn bán chia theo nhóm ở dòng riêng) − chi phí; vì "Biến phí" đã là giá vốn nên thực chất đã trừ giá vốn, nhưng doanh thu **gồm thuế và phí dịch vụ** — phản ánh số thu, không phải lợi nhuận kế toán.
5. **Biểu tượng không nhất quán:** dòng doanh thu "B"/"D", dòng chi phí "æ"/"ç" (rất có thể ký tự Wingdings ▲/▼); v1 dùng ký tự Unicode ▲/▼ (P3).
6. **`TTHUCHI` không lọc `STATUS`** và nhận mọi `LOAI`/`CHUYENKHOAN`/phiếu tạm ứng thuộc lý do định phí/chi khác (giống Thống kê doanh thu).
7. **Tháng trước theo `DATEADD(MONTH,-1,…)`**: ngày 31/3 lùi về 28/2 (hoặc 29/2) nên khoảng "tháng trước" ngắn hơn khoảng "tháng này" khi lọc theo ngày cuối tháng; giữ y hệt.

## 4. Chỗ đề xuất khác app gốc (CHỈ đề xuất — Chiến quyết)

- **P1** Sửa lỗi Tăng/giảm LÃI LỖ (§3-2). *Mặc định tạm: sửa — lỗi rõ ràng, nhưng cần Chiến duyệt vì số in khác app gốc.*
- **P2** Lọc cửa hàng theo ràng buộc user ngay cả khi để trống (§3-1) — user thường chỉ thấy cửa hàng của họ. *Mặc định tạm: áp.*
- **P3** Biểu tượng ▲/▼ bằng Unicode thay Wingdings; cột Tỉ lệ tính thật (`dòng / DOANH THU`) thay vì "100%". *Mặc định tạm: ▲/▼ Unicode; cột Tỉ lệ giữ "100%" như gốc cho tới khi Chiến chốt Q3.*
- **P4** Truy vấn dùng tham số, không nối chuỗi.
- **P5** Gộp 12+ truy vấn thành ít truy vấn hơn (cùng kết quả).

## 5. Câu hỏi chờ Chiến quyết

| # | Câu hỏi | Mặc định tạm |
|---|---|---|
| Q1 | `LOAI = 5/6` của bill là gì (không có nơi nào sinh trong code)? Cần `SELECT DISTINCT LOAI FROM TDONHANG` trên DB thật | Giữ điều kiện như gốc; trong DB chép không có bill nên v1 vô hại |
| Q2 | Sửa lỗi Tăng/giảm LÃI LỖ (P1)? | Sửa |
| Q3 | Cột Tỉ lệ: giữ "100%" hay tính thật? | Giữ "100%" như gốc |
| Q4 | Doanh thu theo nhóm không khớp DOANH THU (§3-3): giữ? | Giữ |
| Q5 | Mẫu in: tạo mới FastReport hay "chưa hỗ trợ"? | "Chưa hỗ trợ", chỉ hiển thị bảng |
| Q6 | Lọc cửa hàng theo user khi để trống (P2)? | Áp |
| Q7 | Dòng "Tiền giờ": ẩn hay hiện 0? | Ẩn (đã cắt tiền giờ) |
| Q8 | Thêm xuất Excel? | Không |
| Q9 | ~~Chụp app gốc để xác minh bố cục~~ **Đã chụp 24/11/2026** (§1); còn thiếu ảnh phần LÃI LỖ và ảnh có số liệu thật | Đã giải quyết phần bố cục |

## 6. Quyết định đã chốt (áp, không hỏi lại)

Tiền `NUMERIC(18,2)`/`decimal`; chỉ VNĐ; F&B thuần COFFEE + POS; bán lẻ tự phục vụ; bỏ karaoke + tính giờ; không đọc `LOAIDINHLUONG`; không đổi cấu trúc DB; màn chỉ xem; LamTron 1000 chỉ khi **tính** tiền bill (báo cáo này chỉ cộng, không làm tròn thêm).

## 7. Tiêu chí nghiệm thu (kiểm trên bản sao DB; selftest tự tạo bill nhiều nhóm hàng, vài phiếu chi theo lý do định phí/chi khác, dữ liệu 2 kỳ liên tiếp)

- [ ] Bộ lọc: Từ/Đến ngày bắt buộc (thiếu → cảnh báo, không chạy); cửa hàng lookup đúng ràng buộc user; để trống cửa hàng theo P2/Q6.
- [ ] Mỗi dòng bảng khớp §2 trên bộ dữ liệu mẫu (kiểm số tay): DOANH THU, dòng nhóm (kể cả "(Chưa phân nhóm)"), Giảm giá, Phí dịch vụ, Thuế, Định phí (từng lý do + tổng), Biến phí (từng nhóm + tổng, dùng `(SLXUAT − SLNHAP) × GIAVON`), Chi khác, CHI PHÍ, LÃI LỖ.
- [ ] Tháng trước đúng `DATEADD(MONTH,-1,…)` cả hai đầu (kiểm biên 31/3, 29/2).
- [ ] Tăng/giảm = tháng này − tháng trước; LÃI LỖ theo P1 (hoặc theo gốc nếu Chiến đảo) — selftest ghi rõ cả hai kết quả.
- [ ] Bill `LOAI 5/6` có mặt thì vào DOANH THU và biến phí nhưng **không** vào dòng nhóm doanh thu (như gốc).
- [ ] Dòng nhóm chỉ hiện khi có số ≠ 0 ở ít nhất một kỳ.
- [ ] Không SQL trong View; màu/cỡ chữ từ token; 125% DPI; lưới đủ lớn cho cảm ứng (yêu cầu v1, không phải gốc). Nút In báo "chưa hỗ trợ".

## 8. Gợi ý implement (không bắt buộc)

`BusinessResultRepository` (Data: tổng bill 2 kỳ, doanh thu theo nhóm 2 kỳ, biến phí theo nhóm 2 kỳ, chi theo lý do 2 kỳ), `BusinessResultCalculator` (Services, thuần: dựng cây dòng + tăng/giảm — selftest ≥ 10 ca), `BusinessResultView` (UI: bộ lọc + lưới có dòng đậm theo cấp). Dùng chung phần lọc ngày/cửa hàng/ràng buộc user với Thống kê doanh thu.
