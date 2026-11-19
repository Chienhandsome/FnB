# POS TAP — mục lục nghiệp vụ rút từ mã nguồn export

Nguồn: `POS_TAP_SRC_EXPORT/` (gitignored). Đây là **ghi chú đầu vào cho `/spec`**, chưa phải spec đã duyệt. Mọi công thức đều trích từ mã gốc; chỗ nào nghi là lỗi của mã gốc đã ghi rõ để Chiến quyết "giữ y hệt" hay sửa.

## 1. Các tài liệu

| Tài liệu | Nội dung |
|---|---|
| [pos-tap-nghiep-vu.md](pos-tap-nghiep-vu.md) | Quy ước chung (cờ 30/0, cấu hình), **form `Hóa đơn bán hàng`** (bán lẻ): tính tiền, giá, khuyến mại, thanh toán, vật tư, điểm tích lũy, lưu tạm; hộp thoại nhỏ |
| [pos-tap-nghiep-vu-nha-hang.md](pos-tap-nghiep-vu-nha-hang.md) | **Hóa đơn nhà hàng / karaoke**: mở bàn, số hóa đơn, tiền giờ, giá theo khu vực và giờ, khuyến mại (mua 1 tặng 1), tổng bill, thanh toán gộp nhóm, in |
| [pos-tap-nghiep-vu-may-tinh-bang.md](pos-tap-nghiep-vu-may-tinh-bang.md) | **Máy chủ HTTP cho tablet** (cổng 6868): các API, quyền, chuyển/gộp/hủy, thanh toán đơn giản, bếp |
| [pos-tap-nghiep-vu-thanh-toan-quan-ly.md](pos-tap-nghiep-vu-thanh-toan-quan-ly.md) | Form `Xác nhận thanh toán`, `Quản lý bán hàng` (sửa/hủy bill sau thanh toán) |
| [pos-tap-nghiep-vu-ban-khu-vuc.md](pos-tap-nghiep-vu-ban-khu-vuc.md) | Màn hình sử dụng dịch vụ, chuyển/gộp bàn trên desktop, trạng thái bàn, kiểm soát order |
| [pos-tap-nghiep-vu-dat-phong-in-bep.md](pos-tap-nghiep-vu-dat-phong-in-bep.md) | Đặt phòng/đặt hàng, lịch Gantt, in xuống bếp/pha chế |
| [pos-tap-nghiep-vu-kho.md](pos-tap-nghiep-vu-kho.md) | Nhập / xuất / chuyển / kiểm kê, tồn kho, tồn đầu, tính lại giá vốn |
| [pos-tap-nghiep-vu-thu-chi-cong-no.md](pos-tap-nghiep-vu-thu-chi-cong-no.md) | Phiếu thu / chi / thu công nợ, công nợ đầu, tồn quỹ, lý do thu chi |
| [pos-tap-nghiep-vu-khach-hang-the.md](pos-tap-nghiep-vu-khach-hang-the.md) | Thẻ trả trước, tích lũy điểm, voucher, nhóm khách |
| [pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md](pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md) | Loại mặt hàng, định lượng, bảng giá tiền giờ, đợt khuyến mại |
| [pos-tap-nghiep-vu-khu-vuc-ban-bao-gia.md](pos-tap-nghiep-vu-khu-vuc-ban-bao-gia.md) | Khai báo khu vực, bàn, báo giá |
| [pos-tap-nghiep-vu-luong.md](pos-tap-nghiep-vu-luong.md) | Tính lương theo công và theo máy chấm công, chi lương |
| [pos-tap-nghiep-vu-bao-cao.md](pos-tap-nghiep-vu-bao-cao.md) | 36 báo cáo (SQL), thống kê trên form, lãi gộp, kết quả kinh doanh, kho, quỹ, công nợ |
| [pos-tap-nghiep-vu-quan-tri-tien-ich.md](pos-tap-nghiep-vu-quan-tri-tien-ich.md) | Nhập dữ liệu từ DB cũ, đánh lại mã hàng, xuất lại vật tư, import định lượng, lưu vết, tài khoản |
| [doi-chieu-vong-2.md](doi-chieu-vong-2.md) | **Nhật ký đối chiếu vòng 2 (19/11/2026)**: đã sửa gì ở đâu, phát hiện mới, chỗ DEMO khác POS TAP thật |
| [checklist-test-app-goc.md](checklist-test-app-goc.md) | **Checklist test bằng app gốc** cho 55 câu còn mở sau vòng 2 (thao tác test + DB cần đọc sau test) |

> Quy ước đánh dấu trong các file nghiệp vụ: **[Sửa v2]** = chỗ vòng 1 của tài liệu này viết sai, đã sửa sau đối chiếu vòng 2; **[Bổ sung v2]** = phát hiện mới của vòng 2; **[Chưa chốt]** = cần test app gốc (xem checklist).

## 2. Mô hình dữ liệu cốt lõi (rút ra)

- `TDONHANG` / `TDONHANGCHITIET` chứa **mọi chứng từ** theo `LOAI`: `0` bán hàng, `1` nhập hàng, `2` xuất khác, `3` chuyển kho, `4` kiểm kê, `97` tồn đầu, `98` công nợ đầu nhà cung cấp, `99` công nợ đầu khách; `5`, `6` xuất hiện trong báo cáo doanh thu (chưa rõ nguồn). **[Bổ sung v2]** `5`/`6` chỉ có ở phía *đọc*; không form nào trong export ghi/sinh loại này. Kiểm lại trên `POS_TAP_SRC_EXPORT`: chỉ có trong `code.cs` của **BÁO CÁO KẾT QUẢ KINH DOANH** và **BIỂU ĐỒ DOANH THU NGÀY TRONG THÁNG** (bản tổng hợp vòng 2 còn liệt kê tồn quỹ và XNT chi tiết, nhưng SQL thật của hai báo cáo đó không chứa `LOAI = 5/6` — lấy dữ liệu thật làm chuẩn). `99`/`98` (công nợ đầu khách/NCC) **do form `Công nợ ban đầu` ghi** (`CONGNODAU = khachHang ? 99 : 98`, đã kiểm trên `POS_TAP_SRC_EXPORT/SFORM/Công nợ ban đầu`) — một đoạn của bản rà vòng 2 (nhóm Bán hàng §7.2) viết "`99` chỉ có phía đọc, chỉ ghi 98" là sai. **Bản clone không tự sinh `LOAI 5/6`** cho tới khi có xác nhận từ app gốc (checklist BC-Q4).
- Số lượng dòng có cặp "chưa quy đổi" (đơn vị người dùng) và "đã quy đổi" (đơn vị cơ sở); bán = `SLXUAT`, trả/nhập = `SLNHAP`.
- Bàn → bill đang mở qua `DBAN.TDONHANGID`; nhóm gộp phòng qua `TDONHANG.NHOMGUID`; combo cha–con qua `COMBOID`/`COMBOPARENTID`; vật tư xuất tự sinh có `XUATVATTU=30`; dòng khuyến mại tự sinh có `DMATHANGID='KM'`.
- Công nợ khách = Σ(`TONGCONG − TIENTHANHTOAN`) bill (`LOAI 0` đã thanh toán, `99`) − Σ(`THU − CHI`) phiếu không đánh `KHONGTHAYDOICONGNO`. **[Sửa v2]** Công thức thật của `GetNoCu` (đã đối chiếu lại trên `POS_TAP_SRC_EXPORT/SFORM/Hóa đơn bán hàng`): vế phiếu = `SUM(CASE WHEN LOAI = 0 THEN THU ELSE -CHI END)` trên `TTHUCHI` của khách, với `COALESCE(KHONGTHAYDOICONGNO,0)=0` **và `TDATHANGID IS NULL`** (phiếu đặt trước không tính — đã trừ riêng qua `GetDatTruoc`). Hệ quả: phiếu thu thường (`LOAI=0`) **có** giảm nợ cũ; phiếu thu công nợ (`LOAI=2`) rơi vào nhánh `-CHI` = 0 nên **không giảm nợ** (xem [file thu chi](pos-tap-nghiep-vu-thu-chi-cong-no.md) mục 2).
- **[Sửa v2] Dấu công nợ nhà cung cấp:** nguồn là bill `LOAI 1, 2, 98` (`LOAI=2` đảo dấu cả `TONGCONG` lẫn `TIENTHANHTOAN`); phiếu chi trả NCC **làm giảm nợ**: nợ = Σ±(`TONGCONG − TIENTHANHTOAN`) − Σ(`CHI − THU`). Màn hình công nợ NCC, báo cáo theo kỳ, đối chiếu đều theo quy ước này. Riêng báo cáo **Tổng hợp công nợ NCC** *cộng* Σ(`CHI − THU`) → sai dấu (mua chịu 10M, trả 4M ra 14M thay vì 6M) và thiếu lọc `KHONGTHAYDOICONGNO`. Với khách hàng hai báo cáo (tổng hợp và theo kỳ) cho **cùng kết quả** — nhận định cũ "dấu khách ngược nhau giữa hai bản" là sai. Đừng ghi "quy ước chung `CHI − THU`".
- Tồn kho = Σ(`SLNHAP − SLXUAT`) theo kho, chỉ tính `LOAI ≠ 0` hoặc bill bán đã thanh toán.
- Loại mặt hàng `DLOAIMATHANGID`: `0` kiêm vật tư, `1` định lượng (pha chế), `2` nguyên liệu, `3` mặt hàng mở, `4` combo, `5` combo mở, `6` dịch vụ, `7` dịch vụ theo giờ. **[Sửa v2]** Đã tra bảng `DLOAIMATHANG` (seed): đúng 8 mã trên; `6` = Dịch vụ (trước đây ghi "dự đoán"); `0` = **kiêm vật tư** (một tài liệu con của chính file này từng ghi `0 = thường` — sai, xem [file máy tính bảng](pos-tap-nghiep-vu-may-tinh-bang.md) mục 3). Báo cáo kho chỉ lấy `0` + `2`. Loại đồ `DLOAIDOID`: `0` ăn, `1` uống, `2` dịch vụ, `3` khác, `4` nguyên liệu.

## 3. Phạm vi đã đọc

Đã đọc kỹ: toàn bộ các form bán hàng (bán lẻ, nhà hàng, tablet), thanh toán, quản lý bán hàng, kho (5 loại phiếu + tồn + giá vốn), thu chi / công nợ / quỹ, thẻ / tích lũy / voucher, khuyến mại, bảng giá giờ, đặt phòng, in bếp, chuyển / gộp bàn, lương (2 cách), 36 báo cáo, các form khai báo chính trong `STABLEDESC` (mặt hàng, khu vực, bàn, bảng giá, báo giá, đợt KM, thẻ trả trước, nhân viên, nhóm khách…), tiện ích quản trị.

**[Bổ sung v2]** Danh sách "chưa đọc" dưới đây đã được rà vòng 2 (hộp thoại nhỏ, `Thiết kế giao diện`, `Màn cảm ứng`, `Bản quyền máy tính bảng`, `Sinh dữ liệu`, 26 bản `ts_code.cs`, form danh sách kho, 73 báo cáo tự sinh…), trừ `Chọn màu ngẫu nhiên`; kết quả nằm trong các mục **[Bổ sung v2]** của từng file và trong [doi-chieu-vong-2.md](doi-chieu-vong-2.md). Phần không đọc được vẫn là code trong `No1Lib`/exe.

**Chưa đọc (chủ yếu giao diện hoặc hộp thoại nhỏ, ít nghiệp vụ) — danh sách gốc của vòng 1:** `Thiết kế giao diện khu vực` (1463 dòng) và `…bàn`, `Thêm bàn vào giao diện`, `Thêm nhanh bàn`, các bản cảm ứng `ts_code.cs` (đã kiểm: bản cảm ứng của `Hóa đơn nhà hàng` dùng lại cùng hàm tính toán và ghi dữ liệu như bản desktop, khác phần giao diện), `Màn cảm ứng`, `Màn hình phụ`, `Chọn bàn`, `Chọn loại giá`, `Chọn máy in`, `Cấu hình máy in theo khu vực`, `Chọn phòng`, `Chọn kích thước nhập kho`, `Nhập hạn sử dụng`, `Chọn hóa đơn tạm`, `Tìm kiếm đặt trước / mặt hàng`, `Thay đổi giờ vào`, `Lịch sử mua bán`, `Phân tích kiểm kê`, các form danh sách `Quản lý nhập / xuất / chuyển kho / kiểm kê`, `Sinh dữ liệu`, `Bản quyền máy tính bảng`, `Sửa dòng hóa đơn`, `Giảm giá theo nhóm`, `Đặt số lượng / ghi chú`, `Nhập lý do`, `Nhập mật khẩu giảm đồ`, `Chi tiết combo`, `Chi tiết bán hàng theo mặt hàng`, một số `STABLEDESC` nhỏ (`DCUAHANG`, `DNHOMMATHANG`, `DLOAIPHONG`, `DCALAMVIEC`, `TSUACHUA`), toàn bộ `designcode.cs` và `layout.xml` (bố cục giao diện và mẫu in FastReport).

**Không có trong export — nằm trong exe/DLL, cần decompile nếu muốn đọc:** `Shared.TinhGia` (công thức tiền giờ, quan trọng nhất), `Shared.DuLieuHoaDonOk`, `DbUtils` (sinh số hóa đơn / mã hàng, kiểm quyền), định nghĩa `SystemConfig` (tên, mặc định từng khóa cấu hình), các enum (`LoaiMatHang`, `LoaiDo`, `CachTinhGiaGio`, `GiaMatHang`, `Functions` = danh sách quyền, `LoaiLuuVet`…), phân quyền nhóm người dùng, `Application.exe` / `ApplicationTs.exe` (khung ứng dụng, menu, đăng nhập, bản quyền), `No1Lib.*` (engine form động, theo ghi nhớ không có nghiệp vụ).

## 4. Điểm lệch / nghi lỗi của mã gốc (cần Chiến quyết giữ hay sửa)

1. **Làm tròn không thống nhất**: bán lẻ và các phiếu kho dùng `Math.Round` mặc định (làm tròn chẵn — banker's), nhà hàng dùng `AwayFromZero` cho tổng bill và tiền giờ.
2. **Giảm giá tổng khi có tiền giờ**: chia tiền giảm cho hàng bằng `TIENHANG × TIENGIAMGIATONG / TIENGIO` (mẫu số là tiền giờ, không phải tổng) — có vẻ sai.
3. **Chuyển bàn** trên tablet: chỉ "nâng giá" và tính thành tiền bằng đơn giá cũ; desktop tuân theo `GiaMatHangKhiChuyenBan` và tính đúng. **[Sửa v2]** Phạm vi: "tablet" ở đây là **server MTB** (`Dịch vụ máy tính bảng`, cổng 6868), và chỉ sai ở nhánh *giảm theo %* (nhánh giảm theo tiền dùng giá mới đúng); MTB còn chỉ cập nhật giá khi `GiaMatHangKhiChuyenBan = 1`. **POS cảm ứng (TS)** gọi `ChuyenBan.ChuyenGopBan` của desktop nên cũng cập nhật giá theo khu vực mới, không mắc lỗi này.
4. **Thanh toán tablet** bỏ qua chuyển khoản, thẻ, voucher, điểm, công nợ, số thứ tự, ngày giao dịch (chỉ tiền mặt). **[Sửa v2]** Chỉ đúng với **server MTB** (`ProcessThanhToan` chỉ nhận `KHACHDUA`/`PRINT`). **POS cảm ứng (TS)** (`XacNhanThanhToanTs`) thanh toán đủ ATM/chuyển khoản/voucher/thẻ trả trước/trừ tích lũy/cho nợ, vẫn ghi `SOTT`, `NGAY`, điểm, nâng nhóm. Hai "tablet" là hai thứ khác nhau — khi viết spec luôn ghi rõ bản nào.
5. **Tablet tính lại bill**: không áp làm tròn phút dịch vụ theo giờ; dùng chung cờ giảm theo tiền cho hàng và giờ; mục "giảm giá tiền giờ" ghi vào ô %. **[Bổ sung v2]** Đúng cho server MTB (`CapNhatHoaDon`); ý đầu cũng đúng cho POS cảm ứng (`UpdateTongThoiGian` thiếu `TinhSoPhutCombo`). Toàn bộ phần tiền giờ đã chốt bỏ khỏi bản clone.
6. Form tăng / giảm điểm (`TTANGGIAMDIEM`) chặn nhầm phiếu chỉ có điểm tăng (điều kiện viết nhầm `DIEMGIAM` hai lần).
7. Bill hủy lưu `TIENGIAMGIA = TILEGIAMGIA` (%); `DatGioChoMatHang.GioTinhLuong` trả giờ bắt đầu.
8. Báo cáo: biểu đồ tháng trong năm thiếu tháng 12 và đảo nhãn năm; lãi gộp: **[Sửa v2]** chỉ bản *tổng hợp giá nhập* chia `QUYDOI` (điều kiện chỉ cần mặt hàng *có* đơn vị chẵn), ba bản còn lại (chi tiết giá nhập, chi tiết giá vốn, tổng hợp giá vốn) nhân `QUYDOI` khi dòng bán dùng đơn vị chẵn; **[Sửa v2]** dấu công nợ: chỉ **Tổng hợp công nợ NCC** sai dấu (xem §2); với khách hàng hai bản tương đương; "Thực lãi" cộng thêm thuế / phí / tiền giờ (là công thức thật theo mã form, không phải lỗi đọc); "Doanh thu" kết quả kinh doanh là `TONGCONG` (gồm thuế).
9. Kiểm tồn chuyển kho so sánh số lượng chưa quy đổi với tồn đơn vị cơ sở; thêm đơn vị chẵn vào dòng chuyển kho có sẵn nhân sai.
10. Bảo mật tablet: mật khẩu qua query string, SQL ghép chuỗi, quyền kiểm ở client — không nên sao chép.
11. Mật khẩu cố định trong mã tiện ích (`6789` nhập dữ liệu, `4321` đánh lại mã, `<mật khẩu admin>6666` xuất lại vật tư, `9876` xóa lưu vết) và `SYSDBA/masterkey`.

**[Bổ sung v2] Nghi lỗi mới của vòng 2** (chi tiết + bằng chứng ở file nhóm tương ứng; tất cả chờ Chiến chốt giữ hay sửa, không tự sửa):

12. **Công nợ/quỹ:** phiếu thu công nợ (`TTHUCHI` `LOAI=2`) không giảm bất kỳ số nợ nào theo mọi code đọc được (câu chặn #1, cần test); Tổng hợp công nợ NCC sai dấu + thiếu lọc `KHONGTHAYDOICONGNO`; Tồn quỹ `LOAI=2` tự mâu thuẫn (trong kỳ cộng, tồn đầu kỳ trừ); lỗi `@TODATE` (viết hoa) ở 2 báo cáo công nợ theo kỳ; báo cáo tồn quỹ không lọc `STATUS`.
13. **Báo cáo:** dòng LÃI LỖ của KQKD sai `TANGGIAM` (thiếu dấu `+` ở chi phí tháng trước, lệch `−2×CP tháng trước`); 2 báo cáo "theo tháng" cộng nhầm Total ngày 18/19/24; biểu đồ doanh thu ngày không gán cột `NGAY` (trục X hỏng); dòng "GIẢM GIÁ TỔNG BILL" ở bản chi tiết và bản tổng hợp tính trên hai tập bill khác nhau.
14. **Kho:** kiểm kê (nhập tay/Excel) không ghi `SLNHAP`/`SLXUAT` khi `SuDung2DonViTinh=30` → tồn không đổi; "Xuất lại vật tư" bỏ trống ngày có thể xóa toàn bộ dòng vật tư mà không sinh lại; tính giá vốn trừ cả bill chưa thanh toán trong khi `GetTonKho` thì không; tính lại giá vốn ghi đè `DMATHANG.GIAVON` về 0 cho loại `3/5/6/7`; không transaction. Kiểm kê **không** kích hoạt tính lại giá vốn.
15. **Khách hàng:** `ChonLoaiGia` desktop lệch 1 so với `SetLoaiGia`; `GetSoLuongTra` trả `Math.Min(tổng,0)` nên gần như luôn 0; số dư thẻ trả trước bản cảm ứng không trừ `CHI`; công thức điểm thứ ba trên lưới Khách hàng thân thiết; loại KM `8be106e5` và `TANGTHEOTRIGIA` nhập được nhưng không có luật áp dụng.
16. **Nhân sự:** form lương máy hiện 0 sau khi mở lại cho tới khi Refresh, và bấm Cập nhật lúc đó ghi đè lương đã lưu bằng 0; NV mới vào giữa tháng không xuất hiện ở form máy; xóa bảng lương form tay để lại `TBANGLUONGTONGHOP` mồ côi; `TAMUNG` hai công thức khác nhau (tay `SUM(CHI)`, máy `SUM(THU−CHI)`).
17. **MTB/quản trị:** 28/38 route không kiểm quyền; đăng nhập bằng `USERID` không cần mật khẩu; kiểm tra "chưa cấu hình máy in" của `in-che-bien` là code chết; thiết kế giao diện: nút vẽ tay insert vào `DANH` rồi update nhầm `DBAN`, phím Up/Down ghi `Top` vào `Left`, `tsbSave` báo thành công giả; `thanh-toan` MTB còn các tác dụng phụ (xóa mật khẩu wifi, tự phát hành e-invoice).

## 5. Câu hỏi cần Chiến xác nhận trước khi viết spec

- Có viết lại **cả ba**: bán lẻ, nhà hàng/karaoke, tablet? (Memory ghi app đích là POS TAP với bản desktop + POS cảm ứng; tablet là máy chủ HTTP riêng.)
- Công thức tiền giờ (`Shared.TinhGia`) không đọc được: lấy từ app gốc bằng test đối chiếu hay decompile `Application.exe`?
- Loại phiếu `LOAI = 5, 6` là gì (ngoài những loại đã biết)? **[Chưa chốt]** vòng 2 vẫn không tìm được nơi sinh (xem §2); cần `SELECT DISTINCT LOAI FROM TDONHANG` trên `DATA.fdb` thật (checklist BC-Q4). Nghiệp vụ "tặng theo trị giá" (`TANGTHEOTRIGIA`) áp dụng ở đâu? **[Sửa v2]** Không có nơi nào áp dụng: chỉ có tab + lưới nhập (đã kiểm lại trong `STABLEDESC/DDOTKHUYENMAI`: chỉ nhánh chọn tab), không có code tính trong 2 form hóa đơn, `CanhBaoKhuyenMai` hay MTB. Tương tự loại KM `8be106e5` ("Giảm giá tiền theo sản phẩm"): lưu được nhưng không chạy. **DB thật** (`DLOAIHINHKHUYENMAI` 5 dòng) còn không có dòng lookup cho `TANGTHEOTRIGIA` → người dùng **không chọn được** loại này; chỉ `8be106e5` chọn được. Bản clone: hoặc bỏ hai loại này khỏi danh mục, hoặc Chiến định nghĩa luật mới.
- **[Bổ sung v2]** Hai câu hỏi lớn nhất của bán hàng vẫn mở: **trừ kho thực sự xảy ra ở đâu** (không có `UPDATE` tồn trong form nào) và **transaction khi thanh toán** (không có hit) — phải test app gốc trước khi viết code thanh toán (checklist BH-Q13). **Tách bàn không có implementation** (`tsbTachBan_Click` chỉ `SetButtonVisible(false)` ở cả desktop và cảm ứng): không tự bịa luật, hỏi Chiến.
- Giữ hay sửa các điểm lệch ở mục 4 (tiền / giá / kho / công nợ là lõi, theo CLAUDE.md không được đoán).

## 6. Phạm vi đã chốt (Chiến, 2026-11-19)

- **Bỏ** karaoke và toàn bộ tính tiền giờ: `TDONHANGGIO`, bảng giá giờ (`DBANGGIA`), `Shared.TinhGia`, dịch vụ theo giờ (`DLOAIMATHANGID=7`), combo hát, tiền mở bàn, ngắt giờ, giá theo khung giờ. Các mục tương ứng trong các file trên chỉ để tham khảo.
- **Để sau**: app Android / máy chủ tablet ([pos-tap-nghiep-vu-may-tinh-bang.md](pos-tap-nghiep-vu-may-tinh-bang.md)).
- **Làm**: bán lẻ, nhà hàng theo bàn (không giờ), kho, thu chi/công nợ, khuyến mại, thẻ/tích lũy, báo cáo, lương.
