# POS TAP — nghiệp vụ khách hàng, thẻ trả trước, tích lũy, voucher

Nguồn: SFORM `Nhập thẻ trả trước__abaada83`, `Trừ tích lũy__1b396df2` (+ `ts_code.cs`), `Nhập coupon__b631d7c1`, `Khách hàng thân thiết__10e06969`, `Tạm ứng đơn hàng__064236d2`. **[Bổ sung v2]** Cuối file có mục 6 tổng hợp đối chiếu vòng 2 (số dư thẻ, trừ tích lũy, voucher, 3 công thức điểm, `ChonLoaiGia`, quyền). Công thức điểm cũng nằm trong form bán lẻ (`GetDiemTichLuy`) — xem [pos-tap-nghiep-vu.md](pos-tap-nghiep-vu.md) mục 1.8.

## 1. Thẻ trả trước (`DTHETRATRUOC`)

- Tìm thẻ theo mã (`DTHETRATRUOC.NAME`, so sánh đúng). Từ chối nếu: mã không tồn tại; `NGAYHETHAN < ngày DB` (đã quá hạn); `KHOA = 30` (thẻ bị khóa); số tiền còn lại ≤ 0.
- **Số tiền còn lại** = `Σ(THU − CHI)` của `TTHUCHI` có `DTHETRATRUOCID = thẻ` (tiền nạp, nạp bằng phiếu thu) **−** `Σ THETRATRUOC` của `TDONHANG` có `DTHETRATRUOCID = thẻ` và khác bill hiện tại (đã tiêu). Truyền bill hiện tại để khi sửa lại bill không bị tính đôi. **[Sửa v2]** Công thức trên là của **desktop** (`GetSoTienConLai` trừ `CHI`); **bản cảm ứng không trừ `CHI`** → sau "Chi trả thẻ" số dư cảm ứng cao hơn desktop đúng bằng tổng `CHI`, có thể cho dùng vượt số dư thật. Clone lấy công thức desktop làm chuẩn cho cả hai bản. Thông báo khi để trống ô mã ở form nhập thẻ ghi nhầm "Mời bạn nhập **voucher** trước" (copy từ `NhapCoupon`, cả desktop và cảm ứng).
- Nạp thẻ: lập phiếu thu có `DTHETRATRUOCID`, đối tượng "Khách nạp thẻ", `KHONGTHAYDOICONGNO` tick (xem file thu chi). Thanh toán bằng thẻ ghi `TDONHANG.THETRATRUOC` + `DTHETRATRUOCID`. Khách có thể gắn sẵn thẻ (`DKHACHHANG.DTHETRATRUOCID`); khi thanh toán nếu còn tiền thì tự dùng thẻ đó mà không hỏi mã.

## 2. Điểm tích lũy

- Số điểm hiện có: `HoaDonBanHang.GetDiemTichLuy(khách, bill)` (xem file bán lẻ 1.8): hai cách tính theo `CachTinhDiem` (0 = tổng doanh số chia `DoanhSoTuongUngVoi1Diem`; ≠ 0 = cộng `DIEM` từng bill), cộng `DIEMTICHLUYBANDAU` và `TTANGGIAMDIEM` (điểm tặng / điểm giảm thủ công).
- Giá trị tiền của điểm = `điểm × SystemConfig.QuyDoi1DiemSangTien`.
- Trừ tích lũy khi thanh toán (bản desktop): hiển thị điểm và giá trị; giá trị dùng tối đa bằng tổng tiền cần trả (`min(giá trị, TONGTIEN)`). **[Sửa v2]** Chính xác hơn: bản desktop (`TruTichLuy`, 1.118 byte) **không đồng bộ** điểm↔tiền, **không validation** (có thể nhập vượt điểm hiện có / vượt tổng tiền) và không có nút "Sử dụng hết"; bản cảm ứng (`TruTichLuyTs`, 2.908 byte) đồng bộ 2 chiều + validation + nút "Sử dụng hết" (nhưng nhập tay vẫn vượt được tổng tiền, chỉ kiểm ≤ điểm hiện có). Clone lấy hành vi cảm ứng làm chuẩn + thêm kiểm tra ≤ tổng tiền.
- Bản cảm ứng (`TruTichLuyTs`): nhập số điểm sử dụng hoặc giá trị (tự quy đổi hai chiều, làm tròn 2 số lẻ: `điểm = Round(giá trị / quyĐổi, 2)`); nút "Sử dụng hết": `min(điểm có, Round(TongTien / quyĐổi, 2))`; bắt buộc đã thiết lập quy đổi > 0 ("VUI LÒNG THIẾT LẬP QUY ĐỔI ĐIỂM SANG TIỀN TRƯỚC"); điểm sử dụng phải trong khoảng (0, điểm có].
- Khi lưu bill: `TRUTICHLUY` (tiền), `DIEMGIAM = TRUTICHLUY / QuyDoi1DiemSangTien`, `DIEM = (int)(TONGCONG / max(1, DoanhSoTuongUngVoi1Diem))`.
- Form `Khách hàng thân thiết`: danh sách khách (kèm cách tính điểm `@CACHTINH`, `@DIEM`), lọc theo nhóm khách (`DNHOMKHACHHANG`), tab hóa đơn đã thanh toán của khách và tab điểm tăng/giảm (`TTANGGIAMDIEM`, quyền `KhachHangThanThiet`). Nhóm khách có `DIEMTICHLUY` (ngưỡng điểm nâng nhóm), các tỉ lệ giảm giá (xem file nhà hàng mục 5).

## 3. Voucher / coupon (`DVOUCHER`)

- Nhập mã (`DVOUCHER.NAME`): từ chối nếu không tồn tại; `HANSUDUNG < ngày DB` (quá hạn); đã dùng trên bất kỳ bill nào (`COUNT(*) TDONHANG WHERE DVOUCHERID = voucher > 0`). Hợp lệ → trả `VoucherID` và `GiaTriVoucher = DVOUCHER.GIATRI` (số tiền cố định, dùng một lần).
- Khi thanh toán: ghi `TDONHANG.VOUCHER` (số tiền) và `DVOUCHERID`. Nếu `voucher > tổng tiền` thì không có tiền thối từ voucher ("trả lại tiền cọc" tính riêng, xem file nhà hàng mục 10).
- Hai chế độ nhập: ô số tiền tự do hoặc chọn từ danh sách (`LuaChonVoucherTuDanhSach`).
- **[Bổ sung v2]** `NhapCoupon` desktop và cảm ứng giống nhau (chỉ khác `Form1`/`No1Form1` và `SelectAll`). Không kiểm tra `NGAYPHATHANH` (chưa tới ngày vẫn dùng được), không kiểm tra `DKHACHHANGID` "Đối tác phân phối"; không có form phát hành/sinh hàng loạt. `COUNT(*)` không lọc `DATHANHTOAN`: bill **nháp** chưa thanh toán gắn voucher vẫn khóa voucher; ngược lại bill đã **hủy** (`HuyBill` DELETE dòng `TDONHANG`) làm `COUNT` về 0 → voucher dùng lại được. Clone nên lọc `DATHANHTOAN = 30` và quyết định các ràng buộc còn thiếu.

## 4. Tạm ứng đơn hàng (`TamUngDonHang`)

- Danh sách phiếu thu (`TTHUCHI`, `LOAI = 0`) có `TDONHANGID = bill` hoặc `TDATHANGID = phiếu đặt`; thêm / sửa dùng form `TTHUCHI0Ae` ở chế độ tạm ứng (xem file thu chi mục 1). Chỉ hiện khi `SuDungChucNangTamUngTrongDonHang = 30`.

## 5. Khai báo liên quan (STABLEDESC)

- **Thẻ trả trước** (`DTHETRATRUOCAe`): trên form thẻ có "Nạp tiền" (mở phiếu thu `TTHUCHI` loại 0 với `DTHETRATRUOCID`, lý do lấy theo lần nạp gần nhất hoặc lý do tên "Nạp thẻ trả trước", diễn giải "Nạp thẻ trả trước <tên thẻ>") và "Chi trả" (phiếu chi hoàn tiền thẻ, lý do "Chi trả thẻ trả trước", chỉ khi còn tiền). Hiển thị tổng nạp, tổng sử dụng, còn lại (`GetSoTienConLai`) cùng danh sách các lần nạp và các bill đã thanh toán bằng thẻ.
- **Nhóm khách hàng** (`DNHOMKHACHHANGAe`): các tỉ lệ giảm giá `TILEGIAMGIATIENHANG`, `TILEGIAMGIATIENGIO` (ẩn ô giờ khi không có tiền giờ hoặc khi gộp chung giảm giá; khi gộp chỉ dùng `TILEGIAMGIA` / giảm tổng; **[Sửa v2]** bảng chân lý đúng: ô `TILEGIAMGIA` (giảm tổng) mặc định `Visible=False` và chỉ hiện khi `CoTienGio` đúng **và** `GopChung = 30`; với POS TAP (`CoTienGio = 0`) ô này **không bao giờ hiện**, nhưng `GetTiLeGiamTheoKhach` vẫn đọc cột → giá trị kẹt ở DB (mặc định 0), không sửa được qua UI; spec clone: hoặc luôn hiện ô, hoặc bỏ cột — Chiến chốt), `TILEGIAMDOAN`, `TILEGIAMDOUONG`, `TILEGIAMDOKHAC`, `TILEGIAMDICHVU`, ngưỡng điểm nâng nhóm `DIEMTICHLUY`.
- **Tăng / giảm điểm** (`TTANGGIAMDIEMAe`): nhập `DIEMTANG` hoặc `DIEMGIAM`. Điều kiện kiểm tra trong mã gốc `DIEMGIAM + DIEMGIAM == 0 && DIEMGIAM × DIEMTANG == 0` (viết nhầm `DIEMGIAM` hai lần) khiến **phiếu chỉ có điểm tăng (điểm giảm = 0) cũng bị từ chối**; ý định là bắt buộc có ít nhất một trong hai. **[Chưa chốt]** chưa kiểm chứng bằng app (checklist KH-Q31).

## 6. Bổ sung vòng 2 (19/11/2026)

Nguồn: `deep/khach-hang.md` (đọc `DEMO.fdb`; DEMO có 0 khách/thẻ/điểm/nhóm khách nên mọi công thức chưa đối chiếu với dữ liệu thật). Các dòng seed (`DLOAIHINHKHUYENMAI` 5 dòng, `DGIAMATHANG`, `DNGANHANG`, `SCONFIG`…) là **dữ liệu DEMO**, POS TAP thật có thể khác — chỗ nào khác thì lấy dữ liệu thật.

### 6.1 Ba công thức điểm (+1 công thức xét nâng nhóm)
1. `GetDiemTichLuy` (xem mục 2): 2 nhánh theo `CachTinhDiem` — đã đối chiếu nguyên văn, đúng.
2. Công thức **thứ ba** trên lưới **Khách hàng thân thiết**: lọc `DATHANHTOAN = 30 AND LOAI = 0`, trừ `SUM(DIEMGIAM)` ở cả hai nhánh — khác `GetDiemTichLuy` (không loại bill hiện tại).
3. Công thức **xét nâng nhóm** (sau thanh toán): **không trừ `DIEMGIAM`** (điểm đã tiêu) — nên số điểm có thể khác tùy nơi xem. Chưa rõ chủ đích hay hạ nhóm khi điểm tụt (**[Chưa chốt]** — checklist KH-Q35).
- Đề xuất clone: một công thức chuẩn (khuyên công thức lưới Thân thiết: bill đã thanh toán + trừ điểm đã tiêu) hoặc giữ 3 công thức và ghi rõ khác biệt. Chiến chốt.

### 6.2 Giá theo khách và chọn loại giá
- `SetLoaiGia`: 0→`GIABAN`, 1→`GIABAN2`, 2→`GIABAN3`, 3→`GIABAN4`, 4→giá gần nhất. **Form `ChonLoaiGia` desktop lệch 1** so với `SetLoaiGia`: nút "Giá bán" (hiện `GIABAN`) áp `GIABAN2`; nút "Giá bán 4" áp giá gần nhất; không nút nào chọn được `LOAIGIA = 0`. Bản cảm ứng `ChonLoaiGiaTs` gán trực tiếp nên đúng → clone lấy hành vi cảm ứng (nút nào giá đó).
- Checkbox `chkApDungTatCa` **có tác dụng** (caller đọc `form.chkApDungTatCa.Checked` làm tham số `apDungTatCa` cho `SetLoaiGia`: không tick → chỉ đổi dòng đang chọn). Nhận định "không dùng trong CODE/TSCODE" chỉ đúng với code của chính dialog.
- `DGIAMATHANG` seed DEMO: `'0'` Hiển thị để lựa chọn, `'1'` Giá chung, `'2'` Giá bán t2, `'3'` Giá bán t3, `'4'` Giá bán cafe. `SCONFIG.DienGiaiGia3` = "Giá 2" (nhãn sai) — **đã xác nhận trên `DATA.fdb` thật**, không chỉ DEMO: sửa 1 dòng khi dùng seed.
- `GIABAN.ENABLECONFIGID = SuDungGiaTheoKhach` (config **vừa** gating ô trên dialog khách vừa điều khiển hành vi hóa đơn); `DTHETRATRUOCID.ENABLECONFIGID = SuDungTheTraTruoc` (ô "Thẻ trả trước" trên dialog khách chỉ bật khi config = 30).

### 6.3 Khuyến mại
- 8 hằng `LOAIHINHKHUYENMAI`; mọi giảm giá lấy `Max`, không cộng dồn. Kiểm tra giờ KM chỉ ở form nhà hàng, không ở bán lẻ.
- **MUA1TANG1 bán lẻ vs nhà hàng khác nhau:** bán lẻ `FLOOR(SL/SOLUONGMUA) × SOLUONGTANG`, tặng dòng 0 đồng; nhà hàng chia cho `(MUA+TẶNG)`, tặng dòng âm `DMATHANGID='KM'` → mua 1 tặng 1 thì nhà hàng mua 1 được 0. Nhánh "tặng khác món" của nhà hàng tính `slXuat = Min(SoLuong, SL món tặng trên bill)` nhưng **không dùng** (dòng ghi dùng `SoLuong`) → tặng vượt số lượng món tặng trên bill.
- **[Sửa v2] `GetSoLuongTra`** (nha-hang mục 5): thực tế trả `Math.Min(tổng, 0)` — tổng dương (dòng bán thường) → 0, chỉ trừ khi bill có dòng trả âm; so sánh `TIMECREATED` (ngày+giờ) với `Date(DENGIO)` (chỉ giờ) nên phần ngày không chuẩn hóa → "trừ SL trả vào số bán" gần như không bao giờ có tác dụng. Cần app thật xem dòng trả có tồn tại trong `dtHoaDon` nhà hàng không.
- **[Sửa v2 — DB thật]** `DLOAIHINHKHUYENMAI` thật chỉ có **5 dòng** (giảm tổng bill, theo nhóm, % theo sản phẩm, tiền theo sản phẩm, mua x tặng y): `TANGTHEOTRIGIA`, giảm theo giá trị đơn hàng/số lượng, đồng giá **không có dòng lookup nên không chọn được**; chỉ loại `8be106e5` chọn được mà không có nhánh xử lý. Phần tiếp theo mô tả theo code (enum), áp dụng cho bản DEMO vòng 2:
- **`TANGTHEOTRIGIA` nhập được nhưng không chạy (theo code/DEMO):** tab + lưới đầy đủ cột ngưỡng/món tặng, không có code áp dụng nào (đã kiểm `STABLEDESC/DDOTKHUYENMAI` trên `POS_TAP_SRC_EXPORT`: chỉ nhánh chọn tab `pageTangTheoTriGia`). Loại KM `8be106e5` "Giảm giá tiền theo sản phẩm" cũng lưu được nhưng không có nhánh xử lý. Chiến chốt: viết luật áp dụng hay ẩn khỏi danh mục loại KM.
- Kiểm tra trùng khoảng ngày giữa 2 đợt cùng loại bị comment trong `mapper_BeforeSave` (nhiều đợt chạy cùng lúc, lấy `Max`). Ngưỡng `GIAMTHEOGIATRIDONHANG` ở bán lẻ có nhánh riêng, nhà hàng không có nhánh này.
- `CanhBaoKhuyenMai` không có caller trong toàn bộ code; config `CanhBaoChuongTrinhKhuyenMai` (= 0 ở DEMO) là khóa chết trong code đã xuất. Báo cáo "CHI TIẾT HÀNG KHUYẾN MẠI" là **báo cáo tự sinh** (12 cột `.config`), không phải SQL lọc `KHUYENMAI`/`'KM'`.

### 6.4 Danh mục khách và nhóm khách
- Bảng sửa E1–E20 của bản nháp `docs/specs/danh-muc-khach-hang.md` đã được vòng 2 xử lý (giải quyết 7, sửa 3, bác 1, làm rõ một phần 1, giữ 8); **chưa sửa spec** trong đợt này (spec cần Chiến duyệt) — xem [doi-chieu-vong-2.md](doi-chieu-vong-2.md) mục "Việc để lại". Điểm đáng nhớ: dialog khách **không có group box** (1 cột, 20 trường, 466×513), `STABLEDESC.FORMAT` rỗng ở mọi bảng (nên khẳng định "FORMAT chứa 5 cặp" không có cơ sở), `SHOWONLOOKUP = 30` đúng cho `NAME/MAKHACH/DIACHI/DIENTHOAI`.
- Quyền nhóm Khách hàng (`SGROUPROLE`): Kế toán + Quản lý = 240 cho cả 6 chức năng; Thu ngân + Phục vụ = 0; Thủ kho chỉ 1 dòng (= 0) cho Thân thiết. `SFUNCTIONID` của STABLEDESC: `DKHACHHANG→36c3bad1`, `DTHETRATRUOC→09f35712`, `DDOTKHUYENMAI→e73d65a7`, `DVOUCHER→cacb3963`.
- SMS/tin nhắn: chỉ còn menu + quyền, **không có code gửi** (grep `TinNhan/SMS/SendSMS` = 0; `SSMSTEMPLATE` 0 dòng; 2 config `CanhBaoChuongTrinhKhuyenMai` + `ThongBaoKhachDenNgaySinhNhat` chỉ có trong `sconfig`, không code nào đọc) — không làm trong clone trừ khi Chiến yêu cầu.
- Thanh toán gộp `NHOMGUID`: số dư thẻ vẫn trừ đúng 1 lần, điểm tiêu vẫn ghi đúng 1 lần (trên bill hiện tại); cái thiếu thật là `DIEM` — các bill khác trong nhóm không được cộng điểm.
