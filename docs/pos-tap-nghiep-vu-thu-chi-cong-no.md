# POS TAP — nghiệp vụ thu chi, công nợ, tồn quỹ

Nguồn: SFORM `Phiếu thu__6a447203` (class `TTHUCHI0Ae`), `Phiếu chi__9f094553` (`TTHUCHI1Ae`), `Phiếu thu công nợ__f34aa294` (`TTHUCHI2Ae`), `Công nợ ban đầu__34d5a00b`, `Tồn quỹ__4accbf7e` (`TonQuyHandler`). Các form `Danh mục phiếu thu/chi`, `Công nợ khách hàng`, `Công nợ nhà cung cấp` chỉ là danh sách / layout. Công thức công nợ nằm ở báo cáo (xem file báo cáo khi đã đọc).

## 1. Bảng `TTHUCHI` (phiếu thu / chi / thu công nợ)

- `LOAI`: `0` = phiếu thu, `1` = phiếu chi, `2` = phiếu thu công nợ (có cờ `LAPHIEUTHUCONGNO = 30`). Số tiền ở cột `THU` (phiếu thu / thu công nợ) hoặc `CHI` (phiếu chi); các nơi khác dùng `SOTIEN` cho dòng chi tiết thu công nợ.
- Đối tượng (`LOAIDOITUONG` theo combo): 0 = khác (nhập tay `TENDOITUONG`, `DIACHI`), 1 = nhân viên (`DNHANVIENID`), 2 = khách hàng (`DKHACHHANGID`), 3 = nhà cung cấp (`DNHACUNGCAPID`). Chọn đối tượng thì tự điền tên và địa chỉ (nhân viên không có địa chỉ).
- Lý do thu / chi: bảng `DLYDOTHUCHI`, cột `LALYDOTHU = 30` là lý do **thu**, `0` là lý do **chi** (form tự lọc theo loại phiếu). Có lý do mẫu tên "Thu tạm ứng".
- Hình thức: tiền mặt hoặc chuyển khoản (`CHUYENKHOAN = 30` kèm `DTAIKHOANNGANHANGID`, bắt buộc chọn tài khoản khi tick chuyển khoản; phiếu chi tạm ứng lương ẩn mục chuyển khoản).
- Cờ `KHONGTHAYDOICONGNO`: phiếu không làm đổi công nợ của đối tượng. Phiếu "khách nạp thẻ" (nạp thẻ trả trước) mặc định tick cờ này.
- Cửa hàng `DCUAHANGID` mặc định `Shared.DCUAHANGID`; người không phải admin chỉ chọn cửa hàng được gán (`TNGUOIDUNGTHEOCUAHANG`). Ngày chỉ sửa được nếu có quyền `ThayDoiNgayTrongPhieuThuChi`.
- **Tạm ứng đơn hàng** (`SetTamUng`): gắn `TDONHANGID`, diễn giải "Tạm ứng cho bàn/phòng <tên>", lý do lấy theo phiếu tạm ứng gần nhất hoặc "Thu tạm ứng", tự tick `KHONGTHAYDOICONGNO` và khóa. **[Sửa v2]** Nhận định cũ "tiền này được trừ vào bill khi thanh toán" chưa chính xác — có **hai luồng khác nhau**: (1) `SetTamUng` chỉ gán `TDONHANGID` (tạm ứng theo bill) và tick `KHONGTHAYDOICONGNO`; (2) tiền *đặt trước* đi qua `GetDatTruoc()` bằng `TDATHANGID`, được gán từ form đặt hàng/phòng (nhánh `HOTEL` trong code `STABLEDESC`: `SetValue("TDATHANGID", mapper.ID)`, mở lại phiếu cũ nếu có → 1 phiếu/1 đặt hàng), **không** từ `SetTamUng`. Khi chốt bill `TIENTHANHTOAN` ghi lên bill = `TienThanhToan − DatTruoc`, nên đặt trước không làm giảm số dư bill trong báo cáo công nợ (xem mục 6.6).
- **Chi lương** (`SetLuong(nhân viên, số tiền, bảng lương)`): diễn giải "Chi tiền lương tháng M năm Y", `LOAIDOITUONG = 1`, ghi `TBANGLUONGID`, lý do lấy theo lần chi lương gần nhất. `LATAMUNG = 30` (tạm ứng lương): đối tượng khóa là nhân viên. `SetGioiHanNgay(tháng, năm)`: ngày phiếu bắt buộc nằm trong tháng của bảng lương.
- Truyền giá trị từ nơi gọi: `TONGCONGNO` → `THU` (hoặc `CHI`) để lập phiếu thu/chi đúng bằng tổng công nợ; `DKHACHHANGID` → loại đối tượng khách; `DNHACUNGCAPID` → loại nhà cung cấp.
- Trả lại tiền cọc dư từ bill (nhà hàng): phiếu chi "Trả lại tiền đặt trước hóa đơn số …", `KHONGTHAYDOICONGNO` tick.

## 2. Phiếu thu công nợ (`TTHUCHI2Ae`)

- Gồm phiếu `LOAI=2` và lưới chi tiết: mỗi dòng là một hóa đơn (`TDONHANGID`, tên, ngày, tổng cộng) với `SOTIEN = LANNAY` (số thu lần này). `THU = Σ SOTIEN`, diễn giải "Thu công nợ", `LOAIDOITUONG = 1` (như mã gốc ghi), `LAPHIEUTHUCONGNO = 30`, cửa hàng mặc định.
- Dữ liệu dòng đến từ màn hình công nợ khách hàng (theo đơn) qua `DETAILDATA`. **[Sửa v2 / Chưa chốt]** Điểm này *chưa xác minh được từ code*: không có nơi nào trong `SFORM`, `STABLEDESC`, code cảm ứng gửi `DETAILDATA` (form `Phiếu thu công nợ` chỉ là bên nhận, code form 2.051 ký tự, không có `BeforeSave`), cũng không có caller `CreateAeForm(TTHUCHI, 2, …)`. Điểm vào nằm trong framework `No1Lib` (không đọc được).
- **[Bổ sung v2 — quan trọng nhất] Phiếu thu công nợ không làm giảm bất kỳ số nợ nào theo mọi code đọc được:** `TCHITIETTHANHTOAN` có **0 tham chiếu** trong toàn bộ code (đã kiểm lại trên `POS_TAP_SRC_EXPORT`: 0 kết quả); `TDONHANG.TIENTHANHTOAN` chỉ được ghi ở 3 chỗ (hóa đơn bán lẻ, hóa đơn nhà hàng, dịch vụ MTB); `GetNoCu` chỉ trừ `THU` của phiếu `LOAI = 0` (phiếu `LOAI = 2` rơi vào nhánh `-CHI` = 0). Lưới chi tiết là `ReadOnly`, nên không sửa được `LANNAY` trên lưới. Khả năng duy nhất còn lại là framework `No1Lib` làm ngầm → **bắt buộc test app gốc** trước khi thiết kế công nợ của bản clone (checklist CN-CHAN1). Nếu app gốc thật sự không trừ nợ thì đây là bug gốc, không sao chép.
- **[Bổ sung v2]** Phiếu thu công nợ có ô `chkCHUYENKHOAN` nhưng không có lookup `DTAIKHOANNGANHANGID` và không có kiểm tra `BeforeSave` → tick chuyển khoản được nhưng không chọn được tài khoản.

## 3. Công nợ ban đầu (`CongNoBanDau`)

- Hai biến thể: khách hàng và nhà cung cấp (nhận biết theo menu gọi: id menu `3771b8bf-e417-4162-850a-0394867a9bee` = công nợ nhà cung cấp).
- Lưu thành **một phiếu `TDONHANG` cho mỗi đối tác**: `LOAI = 99` (khách) hoặc `98` (nhà cung cấp), `NAME = 'CONGNODAU'`, diễn giải "Công nợ ban đầu khi dùng phần mềm", `DATHANHTOAN = 30`, `TONGCONG = TIENHANG = số tiền`, `TILETHUE = 0`, `NGAY = ngày chốt` (mặc định 31/12 năm trước).
- Số tiền > 0: tạo mới nếu chưa có phiếu, cập nhật nếu đổi số tiền hoặc đổi ngày; số tiền = 0: xóa phiếu nếu có; số tiền < 0: bỏ qua. Nhập từ Excel (cột "Mã đối tác", "Công nợ đầu") và xuất file mẫu.
- Chỉ liệt kê khách / nhà cung cấp `STATUS=30`.
- Quy ước công nợ khách (từ `GetNoCu`, xem file bán lẻ): nợ = Σ(TONGCONG − TIENTHANHTOAN) của bill đã thanh toán − Σ(thu − chi) có tác động công nợ. **[Sửa v2]** Chính xác hơn: vế phiếu = `Σ (LOAI = 0 ? THU : −CHI)` của phiếu có `KHONGTHAYDOICONGNO` rỗng/0 **và `TDATHANGID IS NULL`** (loại phiếu đặt trước để không trừ trùng với `GetDatTruoc`); phiếu thu công nợ `LOAI = 2` đóng góp 0. Đã đối chiếu nguyên văn trên `POS_TAP_SRC_EXPORT`.
- **[Sửa v2]** Công nợ ban đầu: `LOAI = 99` (khách) và `98` (NCC) chỉ được ghi bởi form này; số tiền < 0 bị bỏ qua lặng lẽ (không báo).

## 4. Tồn quỹ (`TonQuyHandler`)

Lọc báo cáo quỹ theo cửa hàng và theo hình thức: Tất cả / Tiền mặt / Quẹt thẻ / Ngân hàng (cả nhóm hoặc từng tài khoản `DTAIKHOANNGANHANG` `STATUS=30`).
- Phần phiếu thu chi: tiền mặt = `CHUYENKHOAN = 0`; ngân hàng = `CHUYENKHOAN = 30` (và theo tài khoản); quẹt thẻ: phiếu thu chi không có thẻ (`0 = 1`).
- Phần bill bán (`TIENTHANHTOAN ≠ 0`): tiền mặt `COALESCE(TIENMAT,0) ≠ 0` (số tiền = `TIENMAT`), quẹt thẻ `THE ≠ 0` (số tiền = `THE`), ngân hàng `CHUYENKHOAN ≠ 0` (số tiền = `CHUYENKHOAN`), theo tài khoản `DTAIKHOANNGANHANGID`; tổng "tất cả" = `TIENMAT + CHUYENKHOAN + THE`. Lọc cửa hàng của bill theo `DCUAHANGID` hoặc theo cửa hàng của kho nhập/xuất.
- SQL gốc của báo cáo quỹ nằm ở `SREPORT/BÁO CÁO TỒN QUỸ` (đã ghi trong [file báo cáo](pos-tap-nghiep-vu-bao-cao.md) mục 6; vòng 2 phát hiện mâu thuẫn `LOAI = 2` — xem mục 6.4 dưới đây). SQL của **màn hình** Tồn quỹ (lớp `TonQuy` của framework) khác SQL báo cáo và chưa đọc được: handler chỉ thay 3 chuỗi (`" UNION ALL"`, `"(TDONHANG.TIENTHANHTOAN <>0)"`, `"COALESCE(TIENMAT,0) + COALESCE(CHUYENKHOAN,0) + COALESCE(THE,0)"`).

## 5. Lý do thu / chi (`DLYDOTHUCHIAe`)

- Cờ `LALYDOTHU = 30` (lý do thu) hoặc 0 (lý do chi), loại trừ nhau. Lý do **chi** có `LOAILYDO`: `1` = định phí, `2` = chi khác (dùng trong báo cáo kết quả kinh doanh; loại còn lại — ví dụ chi lương, chi tạm ứng, chi hoàn thẻ — không vào hai nhóm này). Các lý do hệ thống mà mã tham chiếu theo tên: "Thu tạm ứng", "Nạp thẻ trả trước", "Chi trả thẻ trả trước".
- **[Bổ sung v2]** Nhãn combo `LOAILYDO` (từ `STABLEDESC.AELAYOUT` của `DLYDOTHUCHI`): "Không lên báo cáo kết quả kinh doanh" (lưu `−1`), "Chi phí cố định (chi phí phát sinh hàng tháng)" (`1`), "Chi phí khác" (`2`); `0` = "Tạm ứng" (không chọn được từ combo), `NULL` = công nợ. Lý do tạo mới từ form lý do mặc định là lý do thu (`chkLALYDOTHU.Checked = true`).

## 6. Bổ sung vòng 2 (19/11/2026) — công thức công nợ/quỹ đã đối chiếu từng SQL

Nguồn: `deep/cong-no-quy.md` (đọc `DEMO.fdb`) đối chiếu với `POS_TAP_SRC_EXPORT` (dữ liệu thật). Các SQL báo cáo công nợ trích dưới đây đã kiểm lại trong `SREPORT/*/query.sql` thật: tổng hợp NCC không chứa `KHONGTHAYDOICONGNO`, `@TODATE` viết hoa có ở cả 2 báo cáo công nợ theo kỳ.

### 6.1 Công nợ khách hàng — nhiều nguồn tính, một quy ước
- 5 nguồn: `GetNoCu` (nợ cũ trên hóa đơn mới), màn hình Công nợ khách hàng (MASTER/DETAIL), báo cáo Tổng hợp, báo cáo theo kỳ, báo cáo đối chiếu. Hai báo cáo khách (tổng hợp cộng `Σ(CHI − THU)`, theo kỳ trừ `Σ(THU − CHI)`) **tương đương đại số** — cùng một kết quả (nợ 10 triệu, trả 4 triệu bằng phiếu thu → cả hai ra 6 triệu).
- Màn hình "theo đơn": công thức dòng `TONGCONG − TIENTHANHTOAN`; có cấu hình lọc `CONDITION = (LOAI = 0 OR LOAI = 99)` (cột `2f410f17`, `CHECKED = false` — chưa rõ có bật mặc định không); không thấy lọc `DATHANHTOAN` ở DETAIL.
- Lệch giữa báo cáo kỳ và đối chiếu: đối chiếu lọc bill rộng hơn (`((LOAI = 0 AND DATHANHTOAN = 30) OR LOAI <> 0)`) nên hai báo cáo lệch nhau nếu khách có bill `LOAI 1/2/97/98`.
- Bán chịu: chỉ là cổng chặn (`ChoPhepKhachNo`), không có FIFO/phân bổ thanh toán theo từng bill.
- `LOAI = 99` trong kỳ không vào phát sinh mua của báo cáo đối chiếu nhưng vẫn vào nợ cuối kỳ (cần chạy thử để xem hiển thị).

### 6.2 Công nợ nhà cung cấp — 4 nguồn, 1 bản sai
- Nguồn: màn hình, Tổng hợp, theo kỳ, đối chiếu. Quy ước đúng = phiếu chi trả NCC **giảm** nợ (màn hình dùng `THU − CHI` khi cộng; theo kỳ/đối chiếu trừ `Σ(CHI − THU)`). **Tổng hợp công nợ NCC** *cộng* `Σ(CHI − THU)` → trả tiền làm nợ **tăng** (mua chịu 10M, trả 4M: ra **14M** thay vì 6M, lệch `2×Σ(CHI − THU)`) và thiếu `COALESCE(KHONGTHAYDOICONGNO,0)=0` (3/4 báo cáo còn lại đều có). Khuyến nghị clone: theo quy ước của theo kỳ/đối chiếu; Chiến xác nhận hướng sửa.
- Bản sửa lại nhận định cũ: không có hàm `GetNoCu` cho NCC; trả nợ NCC chỉ bằng phiếu chi thường (`LOAI=1`, **có** nhận `TONGCONGNO`), không có form "Phiếu chi công nợ".
- Màn hình NCC không đảo `TIENTHANHTOAN` khi `LOAI = 2` trong khi 3 báo cáo đều đảo (3-chọi-1); phiếu **thu** gắn NCC làm nợ tăng theo màn hình/báo cáo kỳ (hoàn tiền từ NCC lẽ ra làm giảm) — cần test với phiếu xuất trả hàng có `TIENTHANHTOAN ≠ 0`.
- Báo cáo theo kỳ NCC không lọc `DATHANHTOAN` (bill nhập chưa thanh toán vẫn vào nợ).

### 6.3 Lỗi `@TODATE` ở báo cáo công nợ theo kỳ
- Subquery `MUATRONGKY`/`THANHTOANTRONGKY1` của cả báo cáo công nợ KH và NCC dùng `@TODATE` (viết hoa) trong khi tham số khai báo là `@ToDate`. Nếu framework phân biệt hoa/thường, toàn bộ phát sinh trong kỳ bị mất → **[Chưa chốt]** cần chạy báo cáo thật có phát sinh trong kỳ (checklist CN-CHAN4).

### 6.4 Tồn quỹ
- `LOAI = 2` có NCC: SQL chính của báo cáo tính **+**`TIENTHANHTOAN` vào THU, nhưng `AfterGetData` tồn đầu kỳ tính **−**`TIENTHANHTOAN` → cùng một phiếu, trong kỳ là thu, trước kỳ là chi (`LOAI = 1` nhất quán nên đây không phải quy ước chung). **[Chưa chốt]** (checklist CN-CHAN3).
- Báo cáo **không lọc `STATUS`** ở cả hai nhánh → phiếu xóa mềm (`STATUS = −30`) vẫn tính; các công thức công nợ cũng không lọc `STATUS`. Màn hình Tồn quỹ: chưa rõ.
- Nút "Quẹt thẻ" loại toàn bộ phiếu thu/chi (`0 = 1`): chỉ tiền bán hàng (`TDONHANG.THE`) được tính — có thể là cố ý.
- `DLOAIQUY` **được dùng** (lookup `@DLOAIQUYID` của BÁO CÁO TỒN QUỸ, ID là chuỗi `'0'/'1'/'2'`) — nhận định "bảng thừa" của vòng 1 là sai. SQL tồn quỹ không đụng `DNGANHANG` (lookup thật là `DTAIKHOANNGANHANG`) nên chạy được trên POS TAP dù thiếu `DNGANHANG`.
- Phân quyền Quỹ (`SGROUPROLE`): Kế toán + Quản lý MODE 240 trên 4 function Quỹ (16 trên "đổi ngày"); Phục vụ 0; Thu ngân/Thủ kho không có dòng.

### 6.5 Số phiếu, trường bắt buộc, báo cáo thu chi
- Số phiếu: cờ `STABLEDESC.GENNO = 30` ở `TTHUCHI` → framework tự sinh; `NAME` bắt buộc + không trùng; không có code sinh số trong export; định dạng (`PT20/00001`…) nằm trong No1Lib, chưa đối chiếu. `TaoSoHoaDon`/`TSOHOADON` chỉ sinh số **hóa đơn**, không liên quan phiếu thu/chi.
- Trường bắt buộc (theo AELAYOUT đầy đủ): `NAME`, `THU`/`CHI`, `DCUAHANGID`. Không có `DefaultValue` cho ngày (nhiều khả năng framework mặc định hôm nay).
- 6 báo cáo thu chi có `SQL = NULL` → **báo cáo tự sinh** (xem [file báo cáo](pos-tap-nghiep-vu-bao-cao.md) mục 8).
- `SetNapThe()` được gọi từ `STABLEDESC` `DTHETRATRUOC` (`btnNapTien_Click`/`btnChiTra_Click`: gán `DTHETRATRUOCID`, lý do gần nhất/fallback "Nạp thẻ trả trước"/"Chi trả thẻ trả trước", `DIENGIAI`), không phải từ `SFORM`.
- Trigger duy nhất của DB (`TDONHANG_TRIGGER`) chỉ quản lý `DBAN.TDONHANGID` khi trạng thái thanh toán đổi; không chạm `TIENTHANHTOAN`/công nợ.

### 6.6 Đặt trước (tạm ứng đơn hàng)
- Phiếu đặt trước bị loại khỏi `GetNoCu` (`TDATHANGID IS NOT NULL`), vì đã trừ riêng qua `GetDatTruoc()` (`TONGTIEN = bill + NoCu − datTruoc`). `GetDatTruoc` chỉ lấy dòng đầu (nhiều phiếu cùng `TDATHANGID` thì phiếu sau bị bỏ — thực tế khó xảy ra vì flow đặt hàng mở lại phiếu cũ). Báo cáo công nợ/đối chiếu thì **không** loại phiếu này.
- Hệ quả nghi ngờ: khách đặt trước 2 triệu cho bill 10 triệu, chốt trả thêm 8 triệu → báo cáo công nợ vẫn hiện nợ 2 triệu (`TIENTHANHTOAN = TienThanhToan − DatTruoc`). **[Chưa chốt]** (checklist CN-CHAN5).

### 6.7 Đối chiếu công nợ
- Chỉ là **sao kê** (không sinh bút toán); `THANH TOÁN TRONG KỲ` = nợ đầu + phát sinh − nợ cuối (số suy ngược). Xem checklist cho các ca biên.
