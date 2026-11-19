# Nhật ký đối chiếu vòng 2 — tài liệu nghiệp vụ POS TAP

- **Ngày:** 19/11/2026. Base: `origin/main` @ `4fdfe48` (đã `git fetch`). Các sửa đổi dưới đây **chưa commit** (theo CLAUDE.md, chờ Chiến bảo).
- **Đã đọc:** `tong-hop-ra-soat-pos-tap.md` (vòng 1) + `tong-hop-ra-soat-vong-2.md` + 7 file `deep/*.md` (`hoat-dong-ban-hang`, `khach-hang`, `kho-hang`, `cong-no-quy`, `nhan-su`, `quan-tri`, `bao-cao`) + phần bằng chứng `.frx` ở `work/frx-extract.md`. Với các file lớn (`hoat-dong-ban-hang` 217 KB, `quan-tri` 140 KB) đã đọc kỹ các phần đối chiếu Claude, đáp án Q, câu còn mở, nghi bug, kết luận; **chưa đọc từng dòng** các phần mô tả luồng (`hoat-dong-ban-hang` §2, §3.3, §6 N1–N49; `quan-tri` (b.1) bảng 212 khóa `SCONFIG`, (b.6) danh sách 101 điểm `Track()`): các ý lấy từ đó chỉ là tóm tắt có sẵn trong phần tổng kết của chính các file này.
- **Dữ liệu thật:** `POS_TAP_SRC_EXPORT/` (111 form, 36 báo cáo SQL, 29 `STABLEDESC`; mỗi form có `code.cs`/`designcode.cs`/`layout.xml`, một số có `ts_code.cs`; báo cáo chỉ có `query.sql`/`code.cs`; **không** có `.frx`/`.config`, `SCONFIG`, `SGROUPROLE`, danh sách `SREPORT` đầy đủ). Quy ước: chỗ nào DEMO khác dữ liệu thật thì **dữ liệu thật là chuẩn**, ghi rõ ở mục 3.
- Phạm vi: chỉ sửa tài liệu + lập checklist. Không động vào code `Kazuko_FnB/`, không sửa `docs/specs/`.

## 1. Đã sửa những chỗ sai nào, ở đâu

Chỗ sai của bản tài liệu trước (đã được code chứng minh, theo mục 4 của `tong-hop-ra-soat-vong-2.md` + mục (a) các file deep). Đánh dấu **[Sửa v2]** trong từng file.

| # | Chỗ sai / thiếu | Sửa thành | File + mục |
|---|---|---|---|
| 1 | "Quy ước công nợ NCC là `CHI − THU`" / "dấu công nợ khách ngược nhau giữa báo cáo tổng hợp và theo kỳ" | Khách: hai báo cáo tương đương đại số (cùng kết quả). NCC: màn hình, theo kỳ, đối chiếu cùng quy ước (trả tiền giảm nợ); **chỉ Tổng hợp NCC sai dấu** (+ thiếu lọc `KHONGTHAYDOICONGNO`) | `muc-luc` §2 + §4.8; `bao-cao` §7; `thu-chi-cong-no` §6.1–6.2 |
| 2 | "Bản tổng hợp lãi gộp chia `QUYDOI`" | Chỉ **tổng hợp giá nhập** chia (điều kiện: mặt hàng *có* đơn vị chẵn); **tổng hợp giá vốn** nhân như bản chi tiết (điều kiện: dòng bán dùng đơn vị chẵn) | `bao-cao` §3; `muc-luc` §4.8 |
| 3 | Dòng "GIẢM GIÁ TỔNG BILL" chỉ có ở bản chi tiết | Bản tổng hợp **cũng có**, lọc khác (`LOAI=0 AND DATHANHTOAN=30`, chỉ khi không lọc nhóm) → hai bản in hai số khác nhau | `bao-cao` §3 |
| 4 | `GetNoCu` "không trừ phiếu thu thường"; mô tả thiếu `TDATHANGID IS NULL` và hành vi `LOAI=2` | `GetNoCu` **có** trừ `THU` của phiếu `LOAI=0`, loại phiếu có `TDATHANGID`; phiếu thu công nợ `LOAI=2` đóng góp 0 | `muc-luc` §2; `thu-chi-cong-no` §3 |
| 5 | `SetTamUng` "tiền được trừ vào bill khi thanh toán" | Hai luồng: tạm ứng theo bill (`TDONHANGID`) và đặt trước (`TDATHANGID` → `GetDatTruoc`); `TIENTHANHTOAN = TienThanhToan − DatTruoc` | `thu-chi-cong-no` §1 + §6.6 |
| 6 | Phiếu thu công nợ "dữ liệu dòng đến từ màn công nợ qua `DETAILDATA`" | **Không xác minh được** (0 nơi gửi `DETAILDATA`); điểm vào nằm trong framework | `thu-chi-cong-no` §2 |
| 7 | MTB: "hết hạn dùng thử thì server báo lỗi" | Server **không chặn** máy quá hạn (nhánh lỗi duy nhất bắt ngày tương lai) | `may-tinh-bang` §1 |
| 8 | MTB: "admin có hết quyền" | Admin có `SuDungManHinhBep = false` cứng; `ThongKeTrongSuDungDichVu` không bypass admin | `may-tinh-bang` §3 |
| 9 | "`WebService.exe` 0 lần trong toàn bộ nguồn" | Có trong `Sử_dụng_dịch_vụ.cs` (~487–494), timer 5 phút + `KichHoatDongBo==30` | `may-tinh-bang` §10 |
| 10 | CODE MTB "3.863 dòng" | 3.891 dòng | `may-tinh-bang` đầu file |
| 11 | MTB `in-che-bien` "máy in chưa cấu hình thì báo CHƯA CẤU HÌNH…" | Kiểm tra là **code chết** trên route MTB (in với `TenMayIn=""`) | `may-tinh-bang` §9; `dat-phong-in-bep` §3 |
| 12 | "Tablet chỉ tiền mặt" / "tablet chuyển bàn tính giá cũ" / "cảm ứng dùng cùng hàm" | Phải tách 2 "tablet": MTB (HTTP, chỉ tiền mặt, lỗi giá cũ ở nhánh giảm %) ≠ POS cảm ứng TS (thanh toán đủ, chuyển bàn cập nhật giá); hàm static dùng chung, hàm tính tiền là **bản sao viết lại** | `muc-luc` §4.3–4.5; `ban-khu-vuc` §6; `nha-hang` mục 5; `thanh-toan-quan-ly` §5 |
| 13 | `DLOAIMATHANGID`: `6` "dự đoán"; hai tài liệu ghi `0` là "kiêm vật tư" và "thường" | Tra bảng `DLOAIMATHANG`: `0` kiêm vật tư, `1` pha chế, `2` NVL, `3` mở, `4` combo, `5` combo mở, `6` dịch vụ, `7` DV theo giờ | `muc-luc` §2; `danh-muc-gia-khuyen-mai` §1; `may-tinh-bang` §3 |
| 14 | `TANGTHEOTRIGIA` "chưa thấy mã áp dụng, cần tìm thêm" | Không có mã áp dụng ở đâu: nhập được, không chạy (cùng loại KM `8be106e5`) | `danh-muc-gia-khuyen-mai` §4; `khach-hang-the` §6.3; `muc-luc` §5 |
| 15 | Mẫu in báo cáo "nằm trong `layout.xml`, chưa đọc" | Nằm trong `SREPORTTEMPLATE` (DB); 73 báo cáo là tự sinh từ `.config` | `bao-cao` đầu file + §8 |
| 16 | Lương: dòng "Chưa đọc `Tính lương bằng máy`…" mâu thuẫn với §4–§5 | Xóa dòng cũ (ghi chú chưa cập nhật) | `luong` đầu file |
| 17 | "Xóa bảng lương thì xóa chi tiết chấm công" | Form tay chỉ xóa `TBANGLUONGCHITIET` (`TONGHOP` mồ côi); form máy xóa cả 3 bảng | `luong` §1 |
| 18 | Kho `ThemSoLuongChan` "có lỗi" (chắc chắn) | Code sai thật nhưng `CalculateRow` có thể che lỗi → chưa chắc ở runtime | `kho` §5 |
| 19 | Kho §4 thiếu công thức `THANHTIEN` phiếu xuất/chuyển, thiếu NCC trên phiếu xuất; §6 thiếu 3 điểm kiểm kê | Bổ sung (khoảng trống `THANHTIEN`, ô NCC, kiểm kê không tiền/không `OnRowCalculate`/không trigger giá vốn) | `kho` §4, §6 |
| 20 | Khách hàng: `TruTichLuy` desktop = cảm ứng; số dư thẻ giống nhau; `chkApDungTatCa` không dùng | Desktop không đồng bộ/không validation; cảm ứng không trừ `CHI` ở số dư thẻ; `chkApDungTatCa` có tác dụng qua caller | `khach-hang-the` §2, §1, §6.2 |
| 21 | `TILEGIAMGIA` nhóm khách ẩn "khi gộp chung" | Chỉ hiện khi `CoTienGio` đúng **và** `GopChung = 30` (POS TAP: không bao giờ hiện) | `khach-hang-the` §5 |
| 22 | `GetSoLuongTra` "min(Σ SL các dòng thêm sau DENGIO, 0)" | Gần như luôn 0 (`Math.Min(tổng, 0)` + so sánh ngày/giờ lệch) | `nha-hang` mục 5; `khach-hang-the` §6.3 |
| 23 | `DTKTHEOKHUVUC` "không có màn nhập" (vòng 1 viết sai) | Có (tab "Truy cập" trong `DKHUVUCAe`) | `khu-vuc-ban-bao-gia`; `quan-tri-tien-ich` §7 |
| 24 | Tài khoản: ID form/SFUNCTION sai | Form `c21d52e6-…`; SFUNCTION gần nhất `3` "Quản lý người dùng"; `USERID`/`CARDCODE` có control | `quan-tri-tien-ich` §6 |
| 25 | Quản lý bán hàng: lưới "không lọc" | Lọc `LOAI=0 AND DATHANHTOAN=30` (cột ẩn trong blob base64) | `thanh-toan-quan-ly` §5 |

## 2. Đã bổ sung phát hiện mới của vòng 2 vào đâu

| Phát hiện | Nội dung chính | Vị trí |
|---|---|---|
| **Phiếu thu công nợ không trừ nợ theo code** | `TCHITIETTHANHTOAN` 0 tham chiếu; `TIENTHANHTOAN` chỉ ghi ở 3 chỗ; `GetNoCu` bỏ qua `LOAI=2`; chỉ còn khả năng framework làm ngầm → bắt buộc test | `thu-chi-cong-no` §2; `muc-luc` §4.12; checklist CN-CHAN1 |
| **73 báo cáo là tự sinh** | `.config` + `AUTOGENREPORT=30`; SQL sinh trong `No1Lib`; 109/109 có công thức hiển thị, 36/109 có công thức lấy số liệu | `bao-cao` §8.1 |
| **`TANGTHEOTRIGIA` nhập được nhưng không chạy** | + loại KM `8be106e5` | `danh-muc-gia-khuyen-mai` §4; `khach-hang-the` §6.3 |
| **`LUONGTHEOCA` cột không tác dụng** | Có ô nhập trên form NV; 1 hit duy nhất là copy sang `TONGHOP`; không công thức/báo cáo; không có "công thức lương mẫu" | `luong` §5 |
| **Tổng hợp NCC sai dấu** | 14M thay vì 6M; thiếu `KHONGTHAYDOICONGNO` | `bao-cao` §7; `thu-chi-cong-no` §6.2 |
| Tồn quỹ `LOAI=2` mâu thuẫn; `@TODATE`; không lọc `STATUS`; `DLOAIQUY` được dùng | | `thu-chi-cong-no` §6.3–6.4; `bao-cao` §6 |
| KQKD LÃI LỖ sai `TANGGIAM`; Total sai 2 báo cáo tháng; biểu đồ ngày hỏng trục X; `QUYDOI`; giảm giá tổng bill | 6 bug chứng minh bằng code/SQL/`.frx` | `bao-cao` §2, §3, §4, §8 |
| Kho: kiểm kê không đổi tồn khi `SuDung2DonViTinh=30`; xuất lại vật tư bỏ trống ngày; giá vốn trừ bill chưa thanh toán; ghi đè `GIAVON` về 0; 4 công thức tồn | | `kho` §6, §9 |
| Khách hàng: 3 công thức điểm, `ChonLoaiGia` lệch 1, số dư thẻ cảm ứng, voucher | | `khach-hang-the` §6 |
| Nhân sự: B1 (lương máy ghi đè 0), B2 (NV mới vô hình), `TAMUNG` 2 công thức, lỗ hổng quyền Chấm công | | `luong` §2, §4 |
| Bán hàng: tách bàn là stub rỗng; `LOAI 5/6` chỉ phía đọc; trừ kho + transaction vẫn mở; 2 loại "tablet" | | `ban-khu-vuc` §6; `muc-luc` §2, §4, §5 |
| MTB/quản trị: 38 route (28 không kiểm quyền); đăng nhập `USERID`; bản quyền; 101 `Track()`; thiết kế giao diện; SCONFIG | | `may-tinh-bang` §10; `quan-tri-tien-ich` §7 |

## 3. Chỗ DEMO khác POS TAP thật (dữ liệu thật là chuẩn)

Đối chiếu bằng `POS_TAP_SRC_EXPORT`. Bảng 3a là **kết luận vòng 2 bị dữ liệu thật bác hoặc sửa**; 3b là **xác nhận khớp**; 3c là **chưa đối chiếu được** vì export thật không có dữ liệu đó.

### 3a. Bị bác / sửa bởi dữ liệu thật

| Kết luận vòng 2 (đọc DEMO) | Dữ liệu thật | Xử lý trong tài liệu |
|---|---|---|
| `TinhLuongBangMay` "có thể chính là form thừa của DEMO" (DEMO 112 form vs POS TAP 111) | Form `Tính lương bằng máy__0046913e` **có** trong export thật (111 form). Vẫn không có caller/menu | `luong` §4: bác giả thuyết; form thừa của DEMO là form khác, chưa xác định |
| Báo cáo thừa của DEMO (128 vs 127) có thể là "BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN" | Báo cáo đó **có** trong `SREPORT` thật (`1b44aa87`). Export thật chỉ liệt kê 36 báo cáo SQL | `bao-cao` §8.4: bác ứng viên; báo cáo thừa chưa xác định |
| `LUONGTHEOCA` "không hiện trên form nào" | `numLUONGTHEOCA` là ô nhập có thật trên form Nhân viên (hiện/ẩn theo `TinhLuongSuDungMayChamCong`). Vẫn **không** công thức/báo cáo nào dùng | `luong` §5 |
| `DMATHANG.LOAIDINHLUONG` không có trong DEMO/TEMPLATE | Code POS TAP thật dùng cột này (4 hit) → cột tồn tại trong DB thật; schema DEMO thiếu | `muc-luc`/`pos-tap-nghiep-vu` §1.7; `kho` §9.4 |
| `LOAI 99` "chỉ có phía đọc, không chỗ ghi; Công nợ ban đầu chỉ ghi 98" (`deep/hoat-dong-ban-hang.md` §7.2) | Form `Công nợ ban đầu` ghi `CONGNODAU = khachHang ? 99 : 98` (khớp `deep/cong-no-quy.md` và `deep/kho-hang.md`; file deep bán hàng tự mâu thuẫn) | `muc-luc` §2 |
| `LOAI 5/6` xuất hiện ở KQKD, biểu đồ ngày, **tồn quỹ, XNT chi tiết** (bảng tổng hợp vòng 2) | SQL thật chỉ có ở **KQKD** và **biểu đồ doanh thu ngày**; tồn quỹ và XNT chi tiết không chứa `LOAI = 5/6` | `muc-luc` §2; `bao-cao` §4 |

### 3b. Khớp dữ liệu thật

- `TCHITIETTHANHTOAN`: 0 tham chiếu trong toàn bộ export thật.
- `GetNoCu`: nguyên văn khớp (`CASE WHEN LOAI = 0 THEN THU ELSE -CHI END`, `KHONGTHAYDOICONGNO` rỗng, `TDATHANGID IS NULL`).
- SQL báo cáo: tổng hợp NCC không chứa `KHONGTHAYDOICONGNO` (7 file công nợ/đối chiếu còn lại đều có); `@TODATE` hoa ở đúng 2 báo cáo công nợ theo kỳ; lãi gộp tổng hợp giá nhập **chia** `QUYDOI` (`COALESCE(DDONVITINHCHANID,'') <> ''`), tổng hợp giá vốn **nhân** (`DDONVITINHID = DDONVITINHCHANID`); "GIẢM GIÁ TỔNG BILL" có ở cả 4 báo cáo lãi gộp với bộ lọc khác nhau như đã mô tả; `LOAI = 5/6` chỉ trong `code.cs` của KQKD + biểu đồ ngày.
- `TANGTHEOTRIGIA`: chỉ nhánh chọn tab trong `DDOTKHUYENMAI/code.cs`; `WebService.exe` có trong `Sử dụng dịch vụ`; `ExcelSalaryEngine` 0 kết quả.

### 3c. Chưa đối chiếu được với dữ liệu thật (export thật không có)

`SCONFIG` (giá trị khóa, 205 vs 212), `SGROUPROLE` (số dòng 267/268/269 giữa các tài liệu — chưa biết dòng lệch), danh sách 127 báo cáo gồm 73 tự sinh, `SREPORTTEMPLATE` (`.frx`/`.config`), `SFUNCTION`/`SMENU` (menu của form lương máy, "Khách đặt hàng", form quyền 61/66), seed các bảng `D…` (nhãn `DGIAMATHANG`, `DLOAIHINHKHUYENMAI`, `DNGANHANG`…), số dòng/dữ liệu nghiệp vụ. Các kết luận dựa vào đó ghi **[Chưa chốt]** và có mục trong [checklist-test-app-goc.md](checklist-test-app-goc.md). Khi gặp bất kỳ giá trị `SCONFIG`/seed nào khác DEMO trên DB thật: lấy DB thật làm chuẩn.

### 3d. Phát hiện thêm từ thư mục app gốc `POS TAP\` (không có trong bản rà vòng 2)

Chỉ liệt kê file (không mở DB). Phiên bản app: `app.dat` ghi `VERSION=7.2.0.0`, `RELEASE=20-09-2018`, tiêu đề "PHẦN MỀM QUẢN LÝ BAR - NHÀ HÀNG".

- **`Database\ONLINE.FDB` (2 MB) và `WebService.exe` + `.config` có sẵn** → câu V3 ("`ONLINE.FDB` chứa gì, `WebService.exe` là gì") trả lời được bằng cách mở/đọc chúng (checklist QT-V3). Vòng 2 ghi "`ONLINE.FDB` 0 bằng chứng" chỉ đúng với phạm vi export mã nguồn.
- **`PrintProcessor.exe` không có trong thư mục** (chỉ `PrintService.exe`) trong khi code MTB gọi `PrintProcessor.exe` → đường in qua MTB (`POST`) có thể không chạy được trên bản cài này (checklist QT-V21). **`zkemkeeper\zkemkeeper.exe` cũng không có** → form lương máy không dùng được ở bản cài này (checklist NS-R6).
- **7 bản backup thật** `Data\Backup\*.gbk` (2022–2023, ~13 MB) → có thể restore để có dữ liệu thật cho các câu cần lịch sử (báo cáo, công nợ, điểm tích lũy) — xem checklist mục 0. `Data\TEMPLATE.FDB` (19,5 MB) là DB trống để dựng ca test.
- `Data\app.dat` thực tế (UTF-16): các section `[DATA]` (VERSION…), `[PRINTER]` (key = ID form có mẫu in — Hóa đơn nhà hàng, Phiếu nhập/xuất/chuyển/kiểm kê, Phiếu thu/chi/thu công nợ, `TDATHANG`, `TBAOGIA`… — giá trị đang trống), `[CAN]` (`MAY_IN_MA_VACH`), `[USB3G]` (COM4, 9600), `[LAYOUT]`. **Không có section `INKHUVUC`** như vòng 2 mô tả (khu vực chưa cấu hình in) → section `[PRINTER]` là máy in theo *mẫu in/form*, `INKHUVUC` chỉ xuất hiện khi cấu hình máy in theo khu vực (checklist QT-V11). `Data\BanQuyen.dat` chỉ `[CONFIG] LOGIN=0`. `Data\app.apk` (1,8 MB, 25/10/2018) để đọc version (checklist QT-V4).
- `Database\DATA.fdb` bị sửa lần cuối 19/11 15:58 (mới hơn `TEMPLATE.FDB` cùng kích thước) — không dùng làm DB test, không sửa.

## 4. Việc để lại (không làm trong đợt này)

- `docs/specs/danh-muc-khach-hang.md`: bảng E1–E20 đã có bản sửa ở `deep/khach-hang.md` (e) (dialog 466×513, 1 cột, 20 trường, không group box; E10 bị bác — `STABLEDESC.FORMAT` rỗng; E13 đảo ngược — `ENABLECONFIGID` có thật…). **Chưa sửa spec** vì spec phải qua Chiến duyệt (quy trình spec-driven); nên làm bước "viết lại spec danh mục khách hàng theo schema POS TAP" riêng.
- `docs/specs/he-thong.md` §4.1 (`USERID`/`CARDCODE`, acceptance H1 sai theo vòng 2; `SGROUPROLE` 267 vs 268): cần diff trên DB thật trước khi sửa.
- 18+ quyết định Chiến cần chốt (giữ hay sửa các bug đã chứng minh) nằm rải rác ở mục (e) của từng file deep; tài liệu nghiệp vụ chỉ ghi "Chiến chốt", không tự quyết.
