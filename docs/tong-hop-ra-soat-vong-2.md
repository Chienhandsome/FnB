# Tổng hợp rà soát sâu vòng 2 — POS TAP (đối chiếu Claude)

- Ngày: 19/11/2026. Vòng 2 sâu hơn vòng 1 ở 2 điểm: (1) đọc kỹ tài liệu Claude (`docs/pos-tap-nghiep-vu*.md`) và đối chiếu từng khẳng định với code thật; (2) xuất thêm dữ liệu mà vòng 1 chưa có.
- Dữ liệu mới vòng 2: mẫu in `.frx` (38 file) + `.config` (123 file) + manifest 156 dòng; `AELAYOUT` đầy đủ 111 file (không còn cắt 4.000 ký tự); code cảm ứng `TSCODE` (26 file); `DESIGNCODE` (99 file); `SGROUPROLE` (268 dòng); `SGRID` (2 dòng). Hạ tầng: Firebird 2.5 + `FNB_DEMO.fdb` (giống `DATA.fdb` POS TAP trừ 1 form / 1 báo cáo / 3 function / 7 config + bảng `DNGANHANG`).

## 1. Bảng điểm vòng 2

| Nhóm | File vòng 2 | Quy mô | Câu hỏi vòng 1 đã trả lời | Đối chiếu Claude |
|---|---|---|---|---|
| Hoạt động — Bán hàng | `deep/hoat-dong-ban-hang.md` | 846 dòng, ~217 KB | **52/57** (24 đủ + 28 một phần), còn 5 | 5 khẳng định kiểm chứng, Tách bàn là stub rỗng |
| Khách hàng | `deep/khach-hang.md` | 602 dòng | **21/39** (15 đủ + 6 một phần), còn 18 | Lõi đúng; 3 lệch desktop/cảm ứng Claude không nhắc |
| Kho hàng | `deep/kho-hang.md` | 342 dòng | **23/27** (15 đủ + 8 phần lớn), còn 4 | Đối chiếu §8 Claude, 1 khẳng định của subagent bị bác bằng code |
| Công nợ + Quỹ | `deep/cong-no-quy.md` | 365 dòng, ~68 KB | **26/28** (15 dứt khoát + 11 một phần), còn 2 | 11/11 nghi lỗi Claude tìm thấy độc lập; 1 khẳng định dấu công nợ của Claude **sai** |
| Nhân sự | `deep/nhan-su.md` | 340 dòng, ~62 KB | **21/22** (16 đủ + 5 một phần), còn 1 | **31/33 khẳng định đúng**, 2 đính chính |
| Quản trị | `deep/quan-tri.md` | 591 dòng, ~140 KB | **20/25** (11 đủ + 9 một phần), còn 5 | **116/128 đúng**, 4 sai, 8 không xác minh được |
| Báo cáo | `deep/bao-cao.md` + `work/frx-extract.md` | 212 dòng + 1.556 dòng | **23/43** (10 đủ + 13 một phần), còn 20 | 24/28 đúng hoàn toàn; 2 cần sửa/bổ sung |
| **Tổng** | | **3.298 dòng + 1.556 dòng trích .frx** | **186/241** | |

## 2. Đính chính vòng 1 — những kết luận vòng 1 bị vòng 2 sửa (quan trọng nhất)

1. **Phiếu thu công nợ KHÔNG làm giảm bất kỳ số nợ nào** theo mọi code đọc được. `TCHITIETTHANHTOAN`: 0 tham chiếu trong toàn bộ CODE/`SREPORT.SQL`/`TSCODE`/`DESIGNCODE`/`AELAYOUT`/`.frx`/`.config` (kiểm ở cấp DB, không chỉ bản xuất). `TIENTHANHTOAN` chỉ được ghi ở 3 chỗ: hóa đơn bán lẻ, hóa đơn nhà hàng, dịch vụ MTB. Khả năng duy nhất còn lại là hành vi ngầm của framework No1Lib — **bắt buộc test app gốc** (lập 1 phiếu rồi đọc `TIENTHANHTOAN` + `TCHITIETTHANHTOAN` trước/sau).
2. **73 báo cáo "không SQL" không lấy SQL từ `.frx`.** 0/36 file `.frx` chứa `SelectCommand`. Chúng là **báo cáo tự sinh**: `.config` (danh sách cột + cờ nhóm/tổng) + `AUTOGENREPORT=30` (118/156 dòng manifest); SQL sinh động nằm trong `No1Lib`, không trích được. `.frx` chỉ có layout + `<Total>` cộng tổng cột hiển thị. Kết quả: **109/109 báo cáo lá có công thức hiển thị đầy đủ, nhưng chỉ 36/109 có công thức lấy số liệu đầy đủ (kể cả WHERE)**.
3. **`DLOAIQUY` ĐƯỢC DÙNG** (lookup `@DLOAIQUYID` của BÁO CÁO TỒN QUỸ; ID là chuỗi `'0'`/`'1'`/`'2'`) — vòng 1 nói "bảng thừa" là sai.
4. **`GetNoCu` CÓ trừ `THU` của phiếu thu thường `LOAI = 0`** — vòng 1 §6.1 viết "không trừ phiếu thu thường" là sai. `GetNoCu` loại thêm phiếu có `TDATHANGID` (đặt trước đã trừ riêng qua `GetDatTruoc`, tránh trừ trùng).
5. **Số phiếu tự sinh: cơ chế là cờ `STABLEDESC.GENNO = 30`** → framework tự sinh; `NAME` bắt buộc + không trùng; không có code sinh số trong bản xuất. Định dạng `PT20/00001` chưa đối chiếu được (nằm trong No1Lib). `TaoSoHoaDon`/`TSOHOADON` chỉ sinh số hóa đơn, không liên quan phiếu.
6. **`TANGTHEOTRIGIA`: kết luận chắc chắn — nhập được, không chạy.** Tab + lưới đủ cột ngưỡng/món tặng, không có code áp dụng nào trong toàn bộ code. Loại DB `8be106e5` "Giảm giá tiền theo sản phẩm" cũng không có nhánh xử lý.
7. **Không có "công thức lương mẫu" (ExcelSalaryEngine) ở đâu cả** — grep 0 kết quả trên toàn bộ CODE + AELAYOUT + DESIGNCODE + TSCODE + SCONFIG. Mọi công thức lương hard-code C#.
8. **`LUONGTHEOCA` là cột chết** — đúng 1 hit trong toàn bộ CODE (copy khi tạo mới ở form máy), không công thức/form/báo cáo nào dùng.
9. **`DTKTHEOKHUVUC` CÓ màn nhập** (vòng 1 nói không có là sai); quy tắc chặn dùng thử `COUNT(DISTINCT NGAY) > 30` **có thật** (vòng 1 kết luận sai); **`WebService.exe` CÓ thật** trong code (vòng 1 sai về phạm vi); ID form Lưu vết và toàn bộ ID §6 Tài khoản vòng 1 đều sai (đúng: form `TaiKhoanNguoiDung` ID `c21d52e6-…`).
10. **Kiểm kê KHÔNG kích hoạt tính lại giá vốn; phiếu xuất CÓ trường NCC** (vòng 1 sai 2 điểm này). "3 công thức tồn" đã chứng minh đầy đủ bằng SQL — thực ra là **4 công thức, F4 tự mâu thuẫn**.
11. **E10 bị bác** (`STABLEDESC.FORMAT` rỗng hoàn toàn); **E13 đảo ngược** (`ENABLECONFIGID` có thật: `GIABAN→SuDungGiaTheoKhach`, `DTHETRATRUOCID→SuDungTheTraTruoc`). Bảng E1–E20 (20 điểm sai của bản nháp `danh-muc-khach-hang.md`): giải quyết dứt điểm 7, sửa 3, bác 1, làm rõ một phần 1, giữ nguyên 8.
12. **Báo cáo thừa của DEMO (128 vs 127):** ứng viên số 1 là **"BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN"** — báo cáo SQL duy nhất không có dòng manifest + không có template (35/36 báo cáo SQL còn lại đều có `.frx`). Chưa dứt khoát — cần `SELECT NAME FROM SREPORT` trên `DATA.fdb` POS TAP thật.

## 3. Phát hiện mới quan trọng nhất vòng 2 (theo nhóm)

- **Bán hàng:** Tách bàn **không có implementation** (stub rỗng cả desktop lẫn TS — phải thiết kế mới hoặc hỏi Chiến). `LOAI 5/6` (và 99) chỉ xuất hiện ở phía đọc của báo cáo, không nơi nào trong code ghi/sinh. 33 hộp thoại nhỏ đã đọc hết (caller, ghi bảng nào, trả về gì, quyền). So sánh cảm ứng vs desktop: Claude nói "tablet chỉ tiền mặt" — **sai với TS, đúng với MTB**; "chuyển bàn tính giá cũ" — **sai, cả hai đều cập nhật giá**. Hai câu hỏi lớn nhất vòng 1 **vẫn mở**: trừ kho thực sự ở đâu (0 UPDATE tồn trong toàn bộ code form + MTB) và transaction khi thanh toán (0 hit).
- **Khách hàng:** công thức điểm **thứ ba** trên lưới Khách hàng thân thiết (khác cả `GetDiemTichLuy` lẫn công thức nâng nhóm). Nghi bug nặng: `ChonLoaiGia` desktop lệch 1 so với `SetLoaiGia` (nút "Giá bán" áp `GIABAN2`, không chọn được `LOAIGIA=0`; cảm ứng đúng); `GetSoLuongTra` trả `Math.Min(tổng, 0)` nên gần như luôn bằng 0. `TruTichLuy` desktop không đồng bộ/không validation, cảm ứng đồng bộ 2 chiều + validation + "Sử dụng hết". Quyền: Kế toán + Quản lý = 240 cho cả 6 chức năng nhóm Khách hàng; Thu ngân + Phục vụ = 0.
- **Kho:** B25 — giá vốn trừ cả bill bán **chưa thanh toán** (SQL không lọc `DATHANHTOAN`) trong khi `GetTonKho` không trừ → tồn kho và giá vốn không khớp nhau trên cùng một bill. B26 — tồn âm trong tính giá vốn không được xử lý (`TON` âm → `GIAVON` âm/vô nghĩa, mang sang tháng sau). B7 đứng vững: loại `'3'`/`'5'`/`'6'`/`'7'` bị ghi đè `GIAVON` về 0 (cột `GIAVON` thêm không `DefaultValue` → `DBNull` → 0). Tổng 26 nghi vấn giống bug (B1–B26).
- **Công nợ/Quỹ:** verdict dứt khoát về dấu NCC — **bản tổng hợp NCC sai**: mua chịu 10M, trả 4M bằng phiếu chi → màn hình + báo cáo kỳ + đối chiếu ra **6M**, riêng tổng hợp NCC ra **14M** (trả tiền làm nợ tăng, lệch đúng `2×Σ(CHI−THU)`), còn thiếu lọc `KHONGTHAYDOICONGNO`. Với KH hai báo cáo cho cùng kết quả — khẳng định của Claude "dấu KH ngược" **không đúng**. Mâu thuẫn Tồn quỹ `LOAI=2`: SQL chính tính **+**`TIENTHANHTOAN` vào THU, tồn đầu kỳ tính **−**`TIENTHANHTOAN`. Lỗi `@TODATE` (hoa) ở 2 subquery của cả 2 báo cáo công nợ theo kỳ (KH + NCC). Báo cáo tồn quỹ **không lọc `STATUS`** → phiếu xóa mềm vẫn tính.
- **Nhân sự:** B1 (nặng) — form máy mở lại hiện lương 0 cho tới khi bấm Refresh (vì `LayDuLieuTuMay.btnSave` không gán `TBANGLUONGID` cho dòng `TVAORA` import); bấm Cập nhật trong trạng thái đó sẽ ghi đè lương đã lưu bằng 0. B2/B3/B6/B9/B10/B11/B12/B14/B15 (xem file). Giả thuyết mới: `TinhLuongBangMay` không có caller/menu nào — có thể chính là **form thừa của DEMO** (DEMO 112 form vs POS TAP 111). Tổng 18 nghi vấn giống bug.
- **Quản trị:** SCONFIG — **162/212 khóa có chỗ đọc** (còn 50 khóa chưa thấy); 7 khóa thừa: 6 khóa đầu độ tin cậy cao, vị trí thứ 7 chưa chốt (`LoiCamOn` vs `CACHXUATHOADON`). MTB — bảng đầy đủ **38 route** (37 + 1 tải APK), chỉ **6 route kiểm quyền cứng, 28 route không kiểm quyền gì**; 2 cổng chặn dùng thử độc lập. Bản quyền: code nguyên văn `KiemTraDangKy` (máy đã đăng ký luôn trả `DayLefts=20`); `GetStatus==3`/DES/mã máy vẫn 0 kết quả trong cả 4 nguồn mới. Thiết kế giao diện (phần Claude ghi chưa đọc): engine designer đầy đủ + loạt quirk — nút vẽ tay insert vào DANH rồi update nhầm sang DBAN (mất vị trí sau lần lưu đầu), lỗi phím Up/Down (ghi Top vào Left), `tsbSave` báo thành công giả. Lưu vết: **101 điểm gọi `Track()` thực**. Tài khoản/phân quyền: `SGROUPROLE` 268 dòng (lệch 1 dòng so với he-thong); 10 SFUNCTION không nhóm nào có quyền; đăng nhập MTB bằng `USERID` không cần mật khẩu.
- **Báo cáo:** 6 bug đã chứng minh bằng CODE/SQL/`.frx`: (1) tổng hợp NCC sai dấu + thiếu lọc; (2) KQKD dòng LÃI LỖ — `TangGiam = (DS−CP) − DS_Truoc − CP_Truoc` (thiếu dấu `+`), lệch đúng `−2×ChiPhiThangTruoc`; (3) 2 báo cáo tháng — Total T18/T19/T24 cộng nhầm cột, tổng in ra = tổng đúng + 2×D20 + D25 − D18 − D24 (lỗi copy-paste, cả 2 file giống hệt nhau); (4) biểu đồ doanh thu ngày: trục X hỏng — CODE chạy 31 query `THANG1..12` nhưng không gán `NGAY`, `.frx` bind X=`[Table0.NGAY]` (DBNull); (5) lãi gộp chia `QUYDOI` ở bản tổng hợp giá nhập (bán bằng đơn vị lẻ vẫn bị chia); (6) "GIẢM GIÁ TỔNG BILL" chi tiết vs tổng hợp tính trên 2 tập bill khác nhau → cùng kỳ in 2 số khác nhau.

## 4. Chỗ Claude sai (đã xác minh bằng code)

- **Công nợ:** Claude viết quy ước chung `CHI − THU` — sai nếu hiểu là quy ước chung; chỉ đúng với riêng bản tổng hợp NCC (bản bị lỗi). Khẳng định "dấu KH ngược giữa 2 bản" không đúng (KH hai bản đại số tương đương).
- **Báo cáo:** "bản tổng hợp chia `QUYDOI`" — chỉ đúng bản tổng hợp **giá nhập**; bản tổng hợp **giá vốn nhân** như bản chi tiết. Dòng "GIẢM GIÁ TỔNG BILL" — bản tổng hợp **cũng có** dòng này nhưng lọc khác bản chi tiết.
- **MTB/bản quyền:** "hết hạn dùng thử thì server báo lỗi" — sai, nhánh lỗi duy nhất bắt ngày trong tương lai. Admin MTB "có hết" quyền — sai, `SuDungManHinhBep=false` với admin. "`WebService.exe` 0 lần trong toàn bộ nguồn" — sai, có trong `Sử_dụng_dịch_vụ.cs:487-494`. CODE MTB 3.863 dòng — thực tế 3.891.
- **Lương:** đầu file Claude ghi "Chưa đọc" các form/bảng mà §4–§5 lại mô tả chi tiết (mâu thuẫn nội bộ); "Xóa bảng lương thì xóa chi tiết chấm công" — thiếu: form tay chỉ xóa `TBANGLUONGCHITIET`, để lại `TBANGLUONGTONGHOP` mồ côi (DB có 0 FOREIGN KEY nên không có cascade).
- **Bán hàng:** "tablet chỉ tiền mặt" — sai với TS (đúng với MTB); "chuyển bàn tính giá cũ" — sai, cả hai đều cập nhật giá.
- Ngược lại, Claude hơn vòng 2 ở chỗ đọc đúng dữ liệu POS TAP thật — các giá trị `SCONFIG` và danh sách báo cáo của Claude mới là chuẩn POS TAP; vòng 2 chỉ đối chiếu trong phạm vi DEMO.

## 5. Câu hỏi chặn còn lại — chỉ trả lời được bằng app gốc / DATA.fdb thật

1. **Trừ kho thực sự ở đâu + transaction khi thanh toán?** (0 UPDATE tồn, 0 hit transaction trong toàn bộ code) — chặn code thanh toán.
2. **Phiếu thu công nợ có trừ nợ thật không?** (chỉ còn khả năng hành vi ngầm No1Lib) — quyết định thiết kế clone công nợ.
3. **`LOAI 5/6` là gì?** Xuất hiện ở KQKD, biểu đồ ngày, tồn quỹ, XNT chi tiết mà không có định nghĩa ở đâu.
4. **Bản tổng hợp NCC có ra 14M không?** (scenario mua 10M/trả 4M) + Tồn quỹ `LOAI=2` (trong kỳ tăng vs tồn đầu kỳ giảm) + báo cáo kỳ có mất phát sinh do lỗi `@TODATE` không.
5. **7 khóa config thừa / giá trị `SCONFIG` POS TAP thật** (đặc biệt `UngDung`, `CoTienGio`, `LamTronTien`, `ThoiGianChoPhepHuyBill`, `ChoPhepKhachNo`) — xuất `SCONFIG` từ bản sao POS TAP rồi diff với DEMO 212 khóa.
6. **`TinhLuongBangMay` có phải form thừa của DEMO không?** + báo cáo thừa có phải "BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN" không? — `SELECT NAME FROM SFORM/SREPORT` trên `DATA.fdb` POS TAP thật.
7. **Thuật toán sinh `DeviceId`/`MADANGKY` (DES?)** — công cụ nào cấp, ai giữ? (0 kết quả trong cả 4 nguồn mới.)

## 6. Giới hạn còn lại của cả hai vòng (không đọc được từ dữ liệu xuất)

- Cơ chế sinh SQL của 73 báo cáo tự sinh, định dạng số phiếu (`PT20/00001`), `Shared.TinhGia`, `DbUtils.GenSoHoaDon`, thuật toán bản quyền MTB — đều nằm trong `No1Lib`/exe/DLL, không có trong bản xuất.
- `SREPORTROLE` không có trong dữ liệu xuất nên không đối chiếu được phân quyền báo cáo.
- DEMO gần như trống dữ liệu nghiệp vụ (0 khách/thẻ/điểm, `TTHUCHI` 0 dòng, các bảng nhân sự 0 dòng) — nhiều hành vi chỉ kiểm chứng được bằng app gốc + DB thật.
- Các script trích/giải mã của vòng 2 nằm ở `/tmp` (ephemeral, sẽ mất khi VM thay); mọi kết luận quan trọng đã ghi vào file kết quả. Dữ liệu bền vững: `~/workspace/fnb/pos-tap-review/raw/` + `~/workspace/fnb/dbs/FNB_DEMO.fdb`.

## 7. Đề xuất làm tiếp

1. Chạy app gốc POS TAP trên DB mẫu để trả lời 7 câu chặn ở §5 (đặc biệt 1–3), rồi mới viết code thanh toán/công nợ.
2. Chiến chốt các nghi vấn giống bug (giữ hay sửa) theo từng nhóm — danh sách đầy đủ trong mục (e) mỗi file `deep/*.md`.
3. Viết lại spec **Danh mục khách hàng theo schema POS TAP** (bảng E1–E20 đã cập nhật ở `deep/khach-hang.md`).
4. Sau đó làm theo thứ tự menu gốc: Hoạt động (danh mục nhẹ trước, luồng tiền/giá/kho sau) → Khách hàng → Kho → Công nợ → Quỹ → Nhân sự → Báo cáo.

---
*Nguồn: `~/workspace/fnb/pos-tap-review/deep/` (7 file, 3.298 dòng) + `work/frx-extract.md` (1.556 dòng) + `raw/` (mẫu .frx/.config, AELAYOUT đầy đủ, TSCODE, DESIGNCODE, SGROUPROLE, SGRID). Vòng 1: `tong-hop-ra-soat-pos-tap.md` + 8 file nhóm. Ngày: 19/11/2026.*
