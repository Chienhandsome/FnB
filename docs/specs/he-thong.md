# Spec: Nhóm "Hệ thống" (G1)

- Trạng thái: **ĐÃ CÓ CODE MỘT PHẦN trên `main`** (từ commit `9152133`): màn người dùng, nhóm và phân quyền, cấu hình hệ thống, thư viện ảnh, đổi mật khẩu. **Chưa thấy code** sao lưu, phục hồi, tạo mới CSDL (§4.7; menu mở tab tạm). Các câu hỏi chính đã chốt 19/11; file chưa ghi việc duyệt để code. Đối chiếu ngày 25/11: **chưa nghiệm thu lại từng tiêu chí**.
- Ngày: 2026-11-19
- Base: `origin/main` @ `b0d15db` (đã `git fetch origin main`)
- Spec tổng: `docs/specs/kazuko-tea-clone.md` (mục 4.7, mục 6 G1). Spec "Danh mục khách hàng" (`danh-muc-khach-hang.md`) đang gác lại; nó **phụ thuộc** spec này ở phần Cấu hình (mục 4.4).
- **Đối chiếu DB thật (2026-11-19, sau khi duyệt vòng 1):** số liệu metadata ở mục 4 đã được đo lại trực tiếp trên bản chép của `POS TAP\Database\DATA.fdb` — xem `docs/reference/db-pos-tap-do-truc-tiep.md`. Các chỗ đã sửa đánh dấu **[Sửa theo DB thật]**; số liệu cũ viết theo bản sao trước đó (đa phần khác 1–2 đơn vị).
- Nguồn đã đọc: `SFORM/Tài khoản người dùng`, `SFORM/Chọn ảnh`, `SFORM/Lưu vết hoạt động`, `SFORM/Xác nhận xóa lưu vết`, `SFORM/Nhập dữ liệu`; metadata `SUSER`, `SGROUPUSER`, `SGROUPROLE`, `SFUNCTION`, `SCONFIG`, `SCONFIGGROUP`, `SIMAGE`, `TNGUOIDUNGTHEOCUAHANG` đọc từ **bản sao** `DATA.FDB` (chỉ đọc, đã xóa bản sao tạm); mã khung chính đã có trong `Kazuko_FnB` (`MainMenuBuilder`, `LoginService`, `UserRepository`, `AppSettings`).

## 1. Bối cảnh

Khung chính đã có menu "Hệ thống" (Cơ sở dữ liệu ▸ Tạo mới / Sao lưu / Phục hồi; Đổi mật khẩu đăng nhập; Đăng xuất; Thoát) và các mục quản trị liên quan trong menu "Quản trị" (Người dùng và phân quyền, Cấu hình toàn hệ thống, Thư viện ảnh…). Hiện mọi mục này mới mở tab "sẽ được bổ sung" (Đăng nhập, Đăng xuất, Thoát đã chạy thật). Spec này gom **các chức năng nền** mà mọi chức năng nghiệp vụ sau đều dựa vào: tài khoản và quyền, cấu hình, ảnh dùng chung, sao lưu/phục hồi.

Hạn chế nguồn: phần lớn màn hình này là **màn dựng sẵn trong framework** (`No1Lib.*`) nên chỉ có **form "Tài khoản người dùng"** và **"Chọn ảnh"** đọc được mã. Màn Cấu hình, Phân quyền, Thư viện ảnh, Sao lưu/Phục hồi/Tạo mới CSDL, Đổi mật khẩu: chỉ suy được **dữ liệu** từ DB, còn **hành vi/thông báo** phải dựa vào ảnh app gốc và câu trả lời của Chiến (mục 8, 9).

## 2. Mục tiêu

1. Quản lý người dùng, nhóm người dùng, phân quyền theo chức năng và theo cửa hàng, **ghi đúng như app gốc** để quyền ẩn/hiện menu (đã làm) và quyền Xem/Sửa/Thêm/Xóa dùng được ở mọi màn sau.
2. Màn **Cấu hình toàn hệ thống** đọc/ghi `SCONFIG` đúng nhóm/tab/kiểu như app gốc; cung cấp lớp đọc cấu hình có kiểu (`SystemConfig`) cho các giai đoạn sau.
3. **Thư viện ảnh** và hộp **Chọn ảnh** dùng chung (ảnh nhóm, ảnh menu, logo…).
4. **Sao lưu / Phục hồi / Tạo mới CSDL** và **Đổi mật khẩu** hoạt động trên bản sao DB, không làm hỏng DB.
5. Không đổi cấu trúc DB.

## 3. Phạm vi

**Làm**
- H1 Người dùng (`SUSER`) + gán cửa hàng (`TNGUOIDUNGTHEOCUAHANG`).
- H2 Nhóm người dùng (`SGROUPUSER`) + Phân quyền (`SGROUPROLE`) theo `SFUNCTION`.
- H3 Cấu hình toàn hệ thống (`SCONFIG`, `SCONFIGGROUP`) + lớp `SystemConfig`.
- H4 Thư viện ảnh (`SIMAGE`) + hộp Chọn ảnh.
- H5 Đổi mật khẩu đăng nhập.
- H6 Cơ sở dữ liệu: Sao lưu, Phục hồi, Tạo mới (+ sao lưu tự động theo cấu hình).

**KHÔNG làm (giai đoạn/spec khác)**
- Đăng nhập, khung chính, menu (đã xong).
- Đăng ký bản quyền; Xóa dữ liệu; Dữ liệu ban đầu; Công cụ nhà phát triển; Lịch sử tương tác hệ thống / Lưu vết hoạt động (`TLUUVET`) — hỏi Chiến ở Q13 có gộp vào không.
- Báo cáo chi tiết phân quyền hệ thống (G4).
- Chế độ máy chủ Firebird (tab "Cơ sở dữ liệu" ở màn đăng nhập đã ghi "chưa hỗ trợ").
- Đổi cấu trúc DB, thêm cột.

## 4. Nghiệp vụ chi tiết

### 4.1 Người dùng — `SUSER`

Form "Tài khoản người dùng" (một màn nhập liệu `SUSER` + lưới cửa hàng). Các trường **hiện trên form**:

| Cột | Nhãn | Kiểu | Bắt buộc | Duy nhất |
| --- | --- | --- | --- | --- |
| `NAME` | Họ và tên | VARCHAR(255) | có | có |
| `USERNAME` | Tài khoản | VARCHAR(255) | có | có |
| `PASSWORD` | Mật khẩu | VARCHAR(255) | không | — |
| `EMAIL` | Email | VARCHAR(255) | không | — |
| `SGROUPUSERID` | Nhóm người dùng | → `SGROUPUSER.ID` | có | — |
| `DNHANVIENID` | Nhân viên | → `DNHANVIEN.ID` | không | — |
| `USERID` | ID đăng nhập | VARCHAR | không | chưa rõ | **[Sửa theo DB thật]** có `txtUSERID` trên form (vị trí 104,315; nhãn "ID đăng nhập"); dùng cho đăng nhập MTB không cần mật khẩu (xem `pos-tap-nghiep-vu-may-tinh-bang.md` mục 10) |
| `CARDCODE` | Mã thẻ | VARCHAR | không | chưa rõ | **[Sửa theo DB thật]** có `txtCARDCODE` trên form (nhãn "Mã thẻ", 104,345); không có logic nào dùng giá trị trong code đã xuất |
| (lưới) Cửa hàng + cột Truy cập (tích) | | `DCUAHANG` × `TNGUOIDUNGTHEOCUAHANG` | xem dưới | — |

Quy tắc đọc từ mã:
- Chọn **Nhân viên** → ô "Họ và tên" bị khóa và **tự lấy tên nhân viên**; bỏ chọn nhân viên → mở lại để nhập tay.
- Lưới cửa hàng: liệt kê **mọi** cửa hàng (`DCUAHANG`) kèm ô tích Truy cập. Thêm mới → **tích sẵn tất cả**; sửa → tích theo `TNGUOIDUNGTHEOCUAHANG`. Bấm ô để đổi 0/30.
- Trước khi lưu: nếu **không tích cửa hàng nào** → hỏi *"Bạn chưa phân quyền cho người dùng vào cửa hàng nào, bạn có muốn tiếp tục không?"* (Có = lưu tiếp, Không = hủy lưu).
- Sau khi lưu: **xóa toàn bộ** dòng `TNGUOIDUNGTHEOCUAHANG` của người dùng đó rồi **ghi lại** mỗi cửa hàng được tích một dòng (`SUSERID`, `DCUAHANGID`).
- Trùng `NAME` hoặc `USERNAME`, trống `NAME`/`USERNAME`/`SGROUPUSERID` → lỗi (thông báo nguyên văn: chờ ảnh).

Cột có trong bảng nhưng **không có trên form** (giữ nguyên khi sửa, không tự đổi): `ISADMIN`, `SMTP`, `SSL`, `PORT`, `PASS` (gửi mail), `VIETINPUT`, `INPUTMODE`, `SIMAGEID`, `DEFAULTFUNCID` (chức năng mặc định — không có control), `NOTE`, `ITEMTYPE/PARENTID/PARENTDIR/SORTORDER/AUTOID`.

Dữ liệu hiện có: **[Sửa theo DB thật]** `DATA.fdb` hiện chỉ có **1 người dùng**: `Admin` (tên hiển thị "Administrator", `ISADMIN=1`, **mật khẩu rỗng**, nhóm trỏ tới một ID không có trong `SGROUPUSER`); tài khoản `a` (nhóm Thu ngân, gắn nhân viên) của bản sao cũ không còn. `DEMO.FDB` có thêm `phucvu1`. **Mật khẩu lưu dạng chữ thường (không băm)** — khung đăng nhập hiện cũng so sánh trực tiếp. Giữ nguyên để tương thích; xem rủi ro.

Hiển thị lưới danh sách người dùng, nút Thêm/Sửa/Xóa, xóa người dùng đã phát sinh thao tác: **chưa biết** (Q2).

### 4.2 Nhóm người dùng và phân quyền — `SGROUPUSER`, `SGROUPROLE`, `SFUNCTION`

- Nhóm: `NAME`, `NOTE`, `SIMAGEID`, cây (`PARENTID`…). Hiện có 5 nhóm: Thu ngân, Kế toán, Thủ kho, Quản lý, Phục vụ. (Tài khoản `Admin` là quản trị tối cao theo `ISADMIN`, không phụ thuộc nhóm.)
- Chức năng: **[Sửa theo DB thật]** **86** dòng `SFUNCTION` (không phải 81), chia theo `GROUPNAME`: Bán hàng 20, Báo cáo 1, Cơ sở dữ liệu 3, Công nợ 2, Danh mục 11, Hệ thống 7, Hóa đơn bán hàng 18, Kho hàng 9, Nhân sự 5, Quản trị 5, Quỹ 5. 8 chức năng không nhóm nào được cấp quyền (In lại bill, Xóa giảm món, Xóa giảm món sau khi in chế biến, Sử dụng màn hình bếp, Thay đổi khách hàng trong điều chỉnh hóa đơn, Danh mục mật khẩu wifi, Điều chỉnh giờ tính lương dịch vụ theo giờ, Bản quyền máy tính bảng) — vẫn phải hiện trên màn phân quyền để Chiến cấp được. `NOTE='View'` ở nhiều chức năng (nghi: chức năng chỉ có quyền Có/Không, không có Sửa/Thêm/Xóa — Q5).
- Phân quyền: mỗi dòng `SGROUPROLE` = (nhóm, chức năng, `MODE`). `MODE` là cờ bit: **Xem 0x10, Sửa 0x20, Thêm 0x40, Xóa 0x80**, 0 = khóa. Giá trị đang dùng: 0, 16, 80, 112, 240. Không có dòng = khóa (đã làm đúng ở khung chính). Số dòng/nhóm: **[Sửa theo DB thật]** Quản lý 81, **Kế toán 73**, Phục vụ 67, **Thu ngân 35**, Thủ kho 12 (tổng **268**, không phải 267); `MODE` = 0 (104 dòng), 16 (28), 80 (8), 112 (2), 240 (126). Quyền xem báo cáo `SREPORTROLE`: 196 dòng (cột `SGROUPUSERID`, `SREPORTID`, `MODE`).
- Màn phân quyền: chọn nhóm → bảng chức năng (gom theo `GROUPNAME`) với các ô tích Xem/Sửa/Thêm/Xóa; lưu = ghi lại dòng `SGROUPROLE` của nhóm. Bố cục/hành vi chi tiết (tích Sửa có tự tích Xem không, nút chọn cả nhóm…) chờ ảnh (Q5).
- Cấu hình liên quan: `PhanQuyenTruyCapTheoKhuVuc` (=0 hiện tại) — bật thì có thêm phân quyền theo khu vực bàn (nguồn dữ liệu chưa thấy trong schema nhóm này → Q6).

### 4.3 (dự phòng) — gộp vào 4.1/4.2.

### 4.4 Cấu hình toàn hệ thống — `SCONFIG`, `SCONFIGGROUP`

`SCONFIG` là bảng khóa–giá trị. **Khóa = `NAME`**, giá trị nằm ở cột theo kiểu: `INTVALUE` (số nguyên/bật tắt), `DECIMALVALUE`, `TEXTVALUE`, `DATETIMEVALUE`, `BLOBVALUE` (ảnh logo…). `DATATYPE` (1 chuỗi, 3 số, 5 blob), `CONTROLTYPE` (3 ô số, 5 ô chữ, 6 ô chữ nhiều dòng, 7 ảnh, 8 danh sách chọn từ bảng `REFTABLEID` lọc bởi `OTHERCONFIG`, 9 ô tích, 10 combo với danh sách ở `MOREDETAIL`; giá trị combo = chỉ số 0,1,2…). **Ô tích: 30 = bật, 0 = tắt.**

Hiển thị: theo `SCONFIGGROUP` (một tab cấp 1 mỗi nhóm) rồi theo `TAB` (0 hoặc 30) và `SORTORDER`. **[Sửa theo DB thật]** `SCONFIGGROUP` có **18 nhóm** (không có "Khách sạn"): Thông tin chung (7 khóa), Máy in mặc định (0), In HĐ và in chế biến (22), Mẫu hóa đơn (0), Số phiếu (0), Bán hàng (26), Giờ trên bill (14), Thanh toán (11), Tích điểm (4), Kho hàng (10), Mặt hàng (14), Thiết bị khác (14), Cảnh báo (4), Quản trị (21), Cảm ứng (5), Tùy chọn khác (13), Ứng dụng (3), **Ẩn** (28); 3 nhóm "Máy in mặc định", "Mẫu hóa đơn", "Số phiếu" **không có khóa `SCONFIG` nào** (màn dựng riêng — xem 8b); 9 khóa không thuộc nhóm nào. (Danh sách 17 nhóm cũ, gồm "Khách sạn", không đúng với DB thật; ảnh chụp 8b ghi 16 nhóm vì bỏ "Ẩn" và có thể dựng tab theo `UngDung`: nhóm "Khách sạn" nếu hiện thì do mã, không phải dòng `SCONFIGGROUP`.) (nhóm không hiện: cờ nội bộ như `CoTienGio`, `SuDungGiaTheoKhach`, `BatBuocNhapKhachHang`, `CoGiaoHang`…), và một số khóa **nội bộ** không có `CAPTION` (`BATCH_ID`, `LAST_EXPORT`, `Profile`, `DatabaseVersion`, `Application`, `SharedCode`, `CompliedCode`, `AppAutoVersion`, `ChonNgayLamViecKhiDangNhap`, `TraDoThucHienAm`, `TinhLuongSuDungMayChamCong` (giá trị NULL), hai khóa dài kiểu số làm bản quyền…) — **không hiển thị, không sửa**.

**[Sửa theo DB thật]** **205 khóa** (không phải ~190); danh sách đầy đủ lấy từ DB lúc chạy (spec không chép từng khóa). Một số khóa gần như chắc chắn ảnh hưởng nghiệp vụ tiền/giá/kho và là nền cho các spec sau, ghi lại để các spec đó dùng: `CoTienGio`, `GopChungGiamGiaTienGioVaTienHangLamMot`, `LamTronTien`, `CoThueSuat`/`MacDinhThueSuat`, `CoPhiDichVu`/`MacDinhPhiDichVu`, `ChoPhepKhachNo`, `QuyDoi1DiemSangTien`, `DoanhSoTuongUngVoi1Diem`, `CachTinhDiem`, `TuDongNangCapThanhVienKhiDatHanMuc`, `ChoPhepTrungTenKhachHang`, `SuDungNhieuKho`, `TuDongTinhGiaVon`, `SoHoaDonQuayVongTheo`, `ThoiGianChoPhepHuyBill`, `SoFileSaoLuu`/`TuDongSaoLuuDuLieu`/`SoNgaySaoLuu`/`DuongDanSaoLuu`.

Phụ thuộc hiển thị đọc từ mã: `SuDungGia2/3/4` bật thì có ô "Diễn giải giá"; `InHoaDonTheoKhuVuc` và các khóa in có thể lệ thuộc `SuDungChucNangInXuongBep`; (còn lại chờ ảnh). Lưu: ghi từng khóa đã đổi; khóa chưa có dòng thì tạo mới (kiểm tra bằng ảnh/Q7).

Cấu hình gắn với giao diện: `GiaoDienChuongTrinh` (Office 2010 Blue/Silver/Black) và `DoToChuHienThi` (100–200%) thuộc khung giao diện cũ — bản mới dùng `ThemeManager` + DPI nên **không áp dụng nguyên xi** (Q8).

Lớp `SystemConfig` (Data): đọc cấu hình có kiểu, cache trong phiên, làm mới sau khi lưu; tên thuộc tính theo tên khóa (ví dụ `SystemConfig.ChoPhepTrungTenKhachHang` trả bool).

### 4.5 Thư viện ảnh — `SIMAGE`, hộp Chọn ảnh

- `SIMAGE`: `ID`, `NAME`, `NOTE`, `SORTORDER` (số), `IMAGE` (blob). Hiện 272 ảnh (icon menu, ảnh nhóm, ảnh mặt hàng…). Nhiều bảng trỏ tới bằng `SIMAGEID`.
- Hộp **Chọn ảnh** (đọc từ mã): nút chọn file (`jpg;gif;bmp;png;jpeg`, và "All Files"), xem trước; OK trả ảnh đã chọn; Hủy; **Bỏ ảnh** (trả "không ảnh" và đóng). Lỗi tải file → *"Không thể tải file, có thể file không tồn tại hoặc không đúng định dạng!"*.
- Màn **Thư viện ảnh** (quản lý): thêm/sửa tên/xóa ảnh — chi tiết chờ ảnh (Q9: xóa ảnh đang được dùng thì sao).

### 4.6 Đổi mật khẩu đăng nhập

Menu "Hệ thống ▸ Đổi mật khẩu đăng nhập" (không gắn chức năng, ai đăng nhập cũng thấy). Ghi vào `SUSER.PASSWORD` của người đăng nhập. Ô nhập, kiểm tra mật khẩu cũ, ràng buộc độ dài, thông báo: **chưa biết** (Q10). Admin có mật khẩu rỗng → đổi từ rỗng được không (Q10).

### 4.7 Cơ sở dữ liệu: Sao lưu / Phục hồi / Tạo mới

Ba chức năng (`SFUNCTION` "Cơ sở dữ liệu": Tạo mới / Sao lưu / Phục hồi, mỗi cái có cờ Xem) + chức năng "Cho phép sao chép dữ liệu". Cấu hình liên quan: `DuongDanSaoLuu` (đường dẫn), `SoFileSaoLuu` (hiện 10, số file giữ lại), `TuDongSaoLuuDuLieu` (hiện 30 = bật), `SoNgaySaoLuu` (hiện 1 = cách bao nhiêu ngày sao lưu tự động). Thư mục app gốc có `Ionic.Zip.Reduced.DLL` nên sao lưu có thể nén zip; **cách làm thật (zip hay gbak, tên file, đúng khi DB đang mở, phục hồi ghi đè hay hỏi, "tạo mới" sinh DB trống từ đâu) không có mã** → phải chờ ảnh/trả lời (Q11–Q12). Tạo mới CSDL: tạo file `DATA.FDB` mới với schema chuẩn — nguồn schema chưa rõ (có thể từ bản sao trống hoặc DDL trong framework) → Q12.

## 5. Bố cục UI

Theo CLAUDE.md: token + `ThemeManager`, high-DPI, hạn chế cuộn dọc, cảm ứng, không SQL trong View/ViewModel. Tất cả mở thành **tab tài liệu** (thay tab tạm), riêng hộp thoại nhỏ (đổi mật khẩu, chọn ảnh, sao lưu/phục hồi) là dialog.

- **Người dùng và phân quyền**: một màn 2 tab con — *Người dùng* (lưới + Thêm/Sửa/Xóa, dialog thêm/sửa gọn gồm các trường 4.1 chia 2 cột, lưới cửa hàng có ô tích) và *Nhóm & phân quyền* (trái danh sách nhóm; phải bảng chức năng gom theo nhóm chức năng, cột Xem/Sửa/Thêm/Xóa, có ô "chọn tất cả" theo cột/nhóm).
- **Cấu hình toàn hệ thống**: tab cấp 1 theo nhóm, nội dung kiểu biểu mẫu 2 cột, ô tích/ô số/ô chữ/combo/ảnh; nút Lưu/Hủy ở chân màn; ô có giá trị đã đổi được tô nhấn; tìm nhanh theo nhãn (mới, không bắt buộc giống gốc).
- **Thư viện ảnh**: lưới ô ảnh thu nhỏ + tên, nút Thêm/Đổi/Xóa; **Chọn ảnh** dùng lại ở mọi nơi cần ảnh.
- **Đổi mật khẩu / Sao lưu / Phục hồi / Tạo mới**: dialog đơn giản, có thanh tiến trình cho thao tác dài.

## 6. Acceptance criteria

Kiểm trên **bản sao** `DATA.FDB`; so sánh với app gốc bằng ảnh Chiến chụp hoặc chạy cùng thao tác.

**H1 Người dùng**
- [ ] Thêm người dùng đủ trường → ghi `SUSER` đúng (kể cả `STATUS=30`, `USERCREATEDID`, `TIMECREATED`) và `TNGUOIDUNGTHEOCUAHANG` đúng số cửa hàng được tích; mặc định tích tất cả cửa hàng.
- [ ] Trống Họ tên/Tài khoản/Nhóm → báo lỗi; trùng Họ tên hoặc Tài khoản → báo lỗi; sửa chính người đó không báo trùng.
- [ ] Chọn nhân viên → Họ tên bị khóa và bằng tên nhân viên; bỏ chọn → nhập tay được.
- [ ] Bỏ tích hết cửa hàng → hiện câu hỏi đúng nguyên văn; Không = không lưu, Có = lưu và không có dòng cửa hàng.
- [ ] Sửa người dùng: các cột không có trên form (`ISADMIN`, `DEFAULTFUNCID`, SMTP…) giữ nguyên; `USERID`/`CARDCODE` (có trên form, dialog gốc 400×371) nhập/sửa được và lưu đúng; `TNGUOIDUNGTHEOCUAHANG` ghi lại đúng.
- [ ] Người dùng mới đăng nhập được ngay bằng tài khoản vừa tạo và chỉ thấy các cửa hàng được tích.

**H2 Phân quyền**
- [ ] Đổi quyền của nhóm Thu ngân trên một chức năng rồi đăng nhập tài khoản thuộc nhóm đó: menu ẩn/hiện đúng; nút Thêm/Sửa/Xóa theo cờ bit (kiểm với 0x10, 0x30, 0x50, 0xF0).
- [ ] Lưu quyền nhóm: số dòng `SGROUPROLE` và `MODE` khớp đúng thao tác; nhóm khác không bị ảnh hưởng.
- [ ] Mở màn phân quyền với dữ liệu hiện có (Quản lý 81 dòng, Thủ kho 12 dòng): hiển thị đúng các cờ so với DB.
- [ ] Chức năng chỉ-có-Xem (nếu Q5 xác nhận) không cho tích Sửa/Thêm/Xóa.

**H3 Cấu hình**
- [ ] Mở màn: các nhóm/tab/ô đúng với app gốc (đối chiếu ảnh từng tab); khóa nội bộ không hiện.
- [ ] Đổi từng loại ô (tích, số, chữ, combo, ảnh logo) → lưu → mở lại đúng giá trị; ghi đúng cột (`INTVALUE`/`TEXTVALUE`/`BLOBVALUE`); ô tích ghi 30/0.
- [ ] Không có dòng `SCONFIG` nào bị xóa; khóa nội bộ không bị đổi; không DDL.
- [ ] `SystemConfig` trả đúng giá trị 10 khóa mẫu (bool/int/text) so với DB và cập nhật sau khi lưu mà không cần khởi động lại.
- [ ] Đổi `ChoPhepTrungTenKhachHang` rồi kiểm tra bằng truy vấn: giá trị đọc ra đổi theo.

**H4 Ảnh**
- [ ] Chọn ảnh: chọn `jpg/png/gif/bmp`, xem trước, OK/Hủy/Bỏ ảnh đúng; file hỏng báo đúng thông báo.
- [ ] Thư viện: thêm ảnh → ghi `SIMAGE`; hiển thị đủ 272 ảnh hiện có không lỗi; xóa ảnh đang dùng theo Q9.

**H5 Đổi mật khẩu**
- [ ] Đổi mật khẩu → `SUSER.PASSWORD` đổi; đăng xuất rồi đăng nhập bằng mật khẩu mới được, cũ không được; kiểm tra mật khẩu cũ/độ dài đúng như ảnh.

**H6 Sao lưu/Phục hồi/Tạo mới**
- [ ] Sao lưu bản sao DB khi đang mở: tạo file, số file giữ lại đúng `SoFileSaoLuu`, đường dẫn đúng `DuongDanSaoLuu`.
- [ ] Phục hồi file vừa sao lưu vào một bản sao khác: số dòng các bảng chính trùng nguồn (so sánh `COUNT(*)` từng bảng).
- [ ] Tạo mới CSDL: DB mới mở được, danh sách bảng/cột **giống hệt** DB chuẩn, có tài khoản đăng nhập ban đầu theo Q12.
- [ ] Thao tác lỗi (đĩa đầy, file không phải DB) báo lỗi, không làm hỏng DB đang dùng.
- [ ] Không thao tác nào chạm vào `KAZUKO TEA/Database/DATA.FDB` gốc.

**Chung / UI**
- [ ] Không có SQL trong View/ViewModel; không hard-code màu/cỡ chữ (grep + thử một bảng màu khác); 100/125/150% DPI; thao tác chính dùng được bằng cảm ứng.
- [ ] Người dùng không có quyền (hoặc không phải admin) không mở được các màn quản trị qua menu hoặc phím tắt.

## 7. Kế hoạch implement (sau khi duyệt)

Chia nhỏ, mỗi bước là một patch theo `kazuko-patch`, theo thứ tự phụ thuộc:
1. **Data + `SystemConfig`**: `ConfigRepository`, `SystemConfig` có kiểu + cache; (mở khóa cho mọi spec sau, gồm khách hàng).
2. **Cấu hình toàn hệ thống** (màn + ViewModel + dựng ô theo `CONTROLTYPE`).
3. **Ảnh**: `ImageRepository`, hộp Chọn ảnh, màn Thư viện ảnh.
4. **Người dùng**: `UserAdminRepository`, màn + dialog + lưới cửa hàng.
5. **Nhóm & phân quyền**: `RoleRepository`, màn ma trận quyền; nối `FunctionPermissions` cho nút Thêm/Sửa/Xóa (dùng lại ở các màn danh mục).
6. **Đổi mật khẩu**.
7. **CSDL**: sao lưu / phục hồi / tạo mới (+ tác vụ tự động), sau khi Chiến cho biết cơ chế gốc.

File/vùng chạm dự kiến: `Kazuko_FnB/Data/` (repository mới), `Kazuko_FnB/Services/` (`SystemConfig`, `BackupService`), `Kazuko_FnB/UI/` (màn + dialog), `MainMenuBuilder`/chỗ mở tab, `Kazuko_FnB.csproj` (thêm file thủ công). **Không migration.**

## 8. Ảnh cần chụp từ app gốc (Chiến chụp, trên **bản sao**)

1. **Hệ thống ▸ Cơ sở dữ liệu**: ba hộp thoại *Tạo mới*, *Sao lưu*, *Phục hồi* (lúc mở, khi đang chạy, khi xong, khi lỗi); file sao lưu sinh ra tên gì/đuôi gì (chụp thư mục).
2. **Đổi mật khẩu đăng nhập**: hộp thoại, nhập sai mật khẩu cũ, mật khẩu mới không khớp/để trống, thành công.
3. **Quản trị ▸ Người dùng và phân quyền**: màn tổng; dialog *Thêm* và *Sửa* người dùng; từng lỗi (trống tên, trống tài khoản, trùng tên, trùng tài khoản); câu hỏi "chưa phân quyền cửa hàng nào"; chọn nhân viên (ô tên bị khóa); xóa người dùng (thường và người dùng đã có hóa đơn).
4. **Nhóm người dùng**: thêm/sửa/xóa nhóm (xóa nhóm đang có người dùng).
5. **Phân quyền**: màn chọn nhóm + bảng chức năng (chụp nhiều đoạn cuộn nếu dài), một chức năng "View" và một chức năng có Xem/Sửa/Thêm/Xóa; thử tích Sửa khi chưa Xem; nút chọn tất cả (nếu có).
6. **Cấu hình toàn hệ thống**: **mỗi tab** một ảnh (Thông tin chung, In HĐ và in chế biến, Bán hàng, Giờ trên bill, Thanh toán, Tích điểm, Kho hàng, Mặt hàng, Thiết bị khác, Cảnh báo, Quản trị, Cảm ứng, Tùy chọn khác, Ứng dụng, Khách sạn); chỗ có ô thuộc `TAB = 30` (nút "nâng cao"/tab phụ nếu có); nhóm **"Ẩn"** có truy cập được không và bằng cách nào; thử bật `Sử dụng giá 2` (hiện ô diễn giải); lỗi khi nhập giá trị sai; hộp thoại khi bấm Lưu/Đóng có thay đổi.
7. **Thư viện ảnh**: màn tổng, thêm ảnh, đổi tên, xóa ảnh thường và xóa ảnh đang được dùng (thông báo).
8. **Chọn ảnh** (từ một chỗ như ảnh nhóm): lúc mở, đã chọn ảnh, file lỗi.
9. (Nếu có) **Đăng ký bản quyền**/**Lịch sử tương tác hệ thống**: chỉ cần ảnh màn tổng để quyết định phạm vi.

## 8b. Kết quả đối chiếu app gốc (đã chụp 2026-11-19, ảnh trong `docs/specs/img/he-thong/`)

Chụp tự động từ app gốc chạy quyền thường, tài khoản Admin, DB gốc (không có dữ liệu); chỉ xem, không lưu/xóa gì. Các điều xác nhận thêm (cập nhật cho mục 4):

- **Menu Hệ thống ▸ Cơ sở dữ liệu**: ba mục *Tạo mới / Sao lưu / Phục hồi cơ sở dữ liệu* (`menu-he-thong-csdl.png`).
  - **Sao lưu CSDL**: hộp "Sao lưu cơ sở dữ liệu", dòng đỏ **"Chú ý: Bạn chỉ có thể sao lưu khi ứng dụng chạy trên máy chủ"**, ô "Sao lưu tới" + nút chọn thư mục, nút *Thực hiện* (mờ khi chưa chọn) / *Đóng* (`sao-luu-csdl.png`). → Q11: sao lưu chỉ có tác dụng ở chế độ máy chủ; ở chế độ embedded (như bản clone) chưa rõ có làm được không.
  - **Tạo mới CSDL**: mở ngay hộp lưu file Windows (bộ lọc `Database file (*.fdb)`, tên thư mục mặc định Documents) — tức là chọn nơi lưu `.fdb` mới. Chưa thấy các bước sau (chưa chụp để khỏi tạo file).
- **Cấu hình toàn hệ thống** là một hộp thoại modal (780×640): danh sách nhóm ở trái (16 nhóm: Thông tin chung, In HĐ và in chế biến, **Mẫu hóa đơn**, **Số phiếu**, Bán hàng, Giờ trên bill, Thanh toán, Tích điểm, Kho hàng, Mặt hàng, Thiết bị khác, Cảnh báo, Quản trị, Cảm ứng, Tùy chọn khác, Khách sạn), nội dung nhóm ở phải; nút **Tìm kiếm (Ctrl+F)**, **Ghi dữ liệu** (mờ cho tới khi có thay đổi), **Thoát**. Nhóm "Ẩn" không hiển thị. Nhóm dài (Quản trị…) có **cuộn dọc**. Các khóa `TAB = 30` là **ô phụ nằm ngay dưới ô cha** (ví dụ "Mật khẩu" dưới "Giảm đồ, xóa đồ nhập mật khẩu"; "Lưu vết trong (ngày)" dưới "Kích hoạt lưu vết hoạt động"), không phải tab riêng (`cau-hinh-*.png`). Ô số nằm cạnh ô tích (ví dụ "Mặc định thuế suất (%)" cạnh "Có thuế suất"). Giá trị ô tích khớp DB (30 = bật). "Giao diện chương trình" hiện Office 2010 – Blue và "Độ to chữ hiển thị" có chú thích *"Bạn cần thoát chương trình ra vào lại để áp dụng"* (Q8 vẫn giữ). Tab "Số phiếu", "Mẫu hóa đơn" là nhóm có thêm bố cục riêng (xem ảnh).
- **Người dùng và phân quyền** là một hộp thoại "Quản lý người sử dụng" gồm: cây **nhóm** (Tất cả + 5 nhóm, nút Thêm nhóm/Sửa/Xóa), danh sách **tài khoản** của nhóm đang chọn (Thêm tài khoản/Sửa/Xóa), và vùng **phân quyền** bên phải; chưa chọn nhóm thì hiện *"MỜI BẠN CHỌN NHÓM NGƯỜI DÙNG BÊN PHÍA TRÁI ĐỂ PHÂN QUYỀN"*. Chân hộp có chú ý *"Tài khoản hệ thống Admin không được hiển thị trong danh sách"* (`nguoi-dung-phan-quyen.png`).
  - **Hai tab quyền**: *Quyền sử dụng* và **Quyền xem báo cáo** (bảng `SREPORTROLE`, chưa nằm trong spec cũ → thêm vào H2). Tab quyền sử dụng: cây nhóm chức năng (Bán hàng, Báo cáo, Cơ sở dữ liệu, Công nợ, Danh mục, Hệ thống, Hóa đơn bán hàng, Kho hàng, Nhân sự, Quản trị, Quỹ), ô **Lọc**, hai nút **"Khóa tất cả" / "Cho phép tất cả"**, bảng cột *Khóa | Xem | Thêm | Sửa | Xóa | Tất cả*; **Khóa và Xem loại trừ nhau**; chức năng chỉ-xem (ví dụ Báo cáo, Cấu hình toàn hệ thống, Chuyển bàn) **chỉ có ô Khóa và Xem**, không có Thêm/Sửa/Xóa → xác nhận Q5. Nhóm *Quản lý* tích đủ; nhóm *Thu ngân* khóa phần lớn (`phan-quyen-quan-ly.png`, `phan-quyen-thu-ngan.png`). Tab báo cáo: cây báo cáo + bảng *Không được xem | Được phép xem* theo từng báo cáo, cũng có Lọc/Khóa tất cả/Cho phép tất cả (`phan-quyen-xem-bao-cao.png`).
  - **Dialog "Tài khoản người dùng – Thêm mới"**: thanh công cụ *Phím tắt, Trước (F10), Sau (F11), Tạo mới*; trường Nhân viên, Họ và tên (khóa/mờ khi chưa chọn nhân viên), Tài khoản, Mật khẩu, Email, Nhóm người dùng (mặc định = nhóm đang chọn), lưới **Cửa hàng truy cập** (tích sẵn); nút **Lưu, Lưu & Mới, Lưu & thoát, Thoát**; các nút Lưu mờ cho tới khi có thay đổi (`them-tai-khoan.png`). Bấm *Thoát* khi đã sửa → hỏi *"Dữ liệu của bạn đã thay đổi, bạn có muốn hủy bỏ các thay đổi để thực hiện tiếp không?"* (Yes/No) (`xac-nhan-huy-thay-doi.png`). Thông báo lỗi bắt buộc/trùng **chưa bắt được** (cần thêm ảnh khi lưu thiếu Họ tên/trùng tài khoản).
- **Đổi mật khẩu**: tiêu đề "Đổi mật khẩu cho tài khoản: Admin", ba ô *Mật khẩu cũ / Mật khẩu mới / Nhập lại*, nút *Ghi dữ liệu* / *Thoát* (`doi-mat-khau.png`). Ghi với mật khẩu mới để trống → hỏi *"Mật khẩu mới của bạn để trống, chúng tôi khuyên bạn không nên dùng mật khẩu này. Bạn có muốn tiếp tục không?"* (Yes/No) → trả lời Q4: **cho phép mật khẩu trống sau khi xác nhận** (`doi-mat-khau-de-trong.png`). Các lỗi sai mật khẩu cũ / không khớp chưa chụp (cần nhập mật khẩu).
- **Thư viện ảnh**: hộp "Quản lý thư viện ảnh", ảnh xếp theo **nhóm** (Số, Người, Ưu tiên…), nút **Thêm mới / Thay thế / Xóa / Thoát** (`thu-vien-anh.png`).

**Bổ sung từ Chiến (2026-11-19, ảnh `nhom-nguoi-dung-cay.png`, `them-nhom-nguoi-dung.png`, `khoi-phuc-csdl.png`):**
- **Lưu tài khoản thiếu dữ liệu**: app gốc **không cho lưu** (nút Lưu không dùng được, không hiện thông báo lỗi). Bản mới giữ đúng: nút Lưu/Lưu & Mới/Lưu & thoát chỉ bật khi đủ Tài khoản + Họ tên + Nhóm và không trùng; vẫn nên báo lý do cạnh ô sai (cải tiến UI, không đổi nghiệp vụ).
- **Dialog "Nhóm người dùng – Thêm mới"**: *Tên nhóm người dùng* (bắt buộc, không trùng — theo `SGROUPUSER`), *Ảnh* (combo chọn từ Thư viện ảnh, mặc định có ngôi sao), *Ghi chú* (nhiều dòng); nút **Ghi dữ liệu** (mờ khi chưa hợp lệ) / **Thoát**. Sửa nhóm dùng cùng dialog. Sửa/xóa nhóm nằm ở thanh Thêm nhóm/Sửa/Xóa phía trên cây nhóm.
- **Tạo mới CSDL**: mở hộp lưu file của Windows để chọn vị trí đặt file `.fdb` mới.
- **Khôi phục CSDL**: hộp "Khôi phục cơ sở dữ liệu", chú ý đỏ *"Bạn chỉ có thể khôi phục khi ứng dụng chạy trên máy chủ"*, hai ô: **File sao lưu** (chọn bằng hộp Open, bộ lọc **`Database backup file (*.gbk)`**) và **File khôi phục** (đường dẫn đích); nút Thực hiện (mờ tới khi đủ hai ô) / Đóng. Suy ra: sao lưu/phục hồi dùng **gbak, file `.gbk`**.
- **Đổi mật khẩu — ràng buộc đề xuất (Chiến yêu cầu bổ sung; thông báo do bản mới đặt, chưa phải của app gốc, chờ Chiến duyệt):** (1) *Mật khẩu cũ* phải khớp `SUSER.PASSWORD` hiện tại, sai → "Mật khẩu cũ không đúng." và không ghi; (2) *Mật khẩu mới* và *Nhập lại* phải giống nhau, khác → "Mật khẩu nhập lại không khớp." và không ghi; (3) mật khẩu mới để trống → giữ đúng hộp xác nhận của app gốc (Yes mới ghi); (4) mật khẩu mới trùng mật khẩu cũ → vẫn cho ghi (không báo lỗi) — hoặc chặn, Chiến chọn; (5) không giới hạn độ dài tối thiểu (app gốc cho trống; nếu cần ≥ 4 ký tự thì Chiến nói). Mật khẩu vẫn lưu chữ thường như Q3.

**Quyết định của Chiến (2026-11-19):**
- **Sao lưu / Phục hồi / Tạo mới CSDL (H6): để sau**, không làm trong lần này (sau này mới quyết có hỗ trợ embedded hay chờ chế độ máy chủ). Menu giữ tab "sẽ được bổ sung". Mục 3 và kế hoạch bước 7 hiểu là **hoãn**.
- **Tạo mới CSDL (khi làm sau)**: dùng một file DB trống kèm app làm nguồn schema.
- **Mật khẩu**: giữ **chữ thường** như app gốc; mật khẩu mới **trùng** mật khẩu cũ → **chặn**; **không** đặt độ dài tối thiểu (cho trống sau xác nhận).
- **`GiaoDienChuongTrinh`, `DoToChuHienThi`**: không cần làm (không hiển thị, không map sang `ThemeManager`).
- **Phạm vi**: Lịch sử tương tác, Xóa dữ liệu, Dữ liệu ban đầu, Công cụ nhà phát triển → spec riêng sau.
- **Xóa nhóm người dùng còn tài khoản**: hộp **"Cảnh báo"** *"Bạn không thể xóa nhóm người dùng 'Thu ngân' vì còn (1) dữ liệu khác liên kết tới 'Thu ngân'"* (nút Ok), **không xóa** (`xoa-nhom-con-tai-khoan.png`). Mẫu chung: *"Bạn không thể xóa <loại> '<tên>' vì còn (<n>) dữ liệu khác liên kết tới '<tên>'"* — áp dụng làm quy tắc xóa chung cho các danh mục (đếm bản ghi tham chiếu; không có FK nên do chương trình kiểm). Xóa tài khoản đã có thao tác: chưa chụp, mặc định áp mẫu này nếu có bản ghi tham chiếu.

Còn thiếu ảnh (chưa chụp vì cần dữ liệu thử hoặc có thể tạo file): hộp Tạo mới/Phục hồi sau bước chọn file, lỗi bắt buộc/trùng của tài khoản, thêm/sửa/xóa nhóm người dùng, xóa tài khoản/nhóm đã có dữ liệu, các tab Cấu hình còn lại (Mẫu hóa đơn, Cảnh báo, Cảm ứng, Khách sạn…; đã chụp được và lưu cùng thư mục các tab chính), xóa ảnh đang dùng.

## 9. Rủi ro / câu hỏi mở

**Rủi ro**
- **Mật khẩu `SUSER.PASSWORD` và mật khẩu trong `SCONFIG` (`PasswordGiamDo`) lưu chữ thường**; `Admin` đang để trống. Giữ như vậy để cùng DB với app gốc; nếu muốn băm phải bàn riêng vì app gốc sẽ không đăng nhập được. Mã gốc còn một mật khẩu cứng trong code (màn xác nhận xóa lưu vết) — nếu làm chức năng đó sau, cần quyết định giữ hay bỏ.
- Phân quyền sai = người dùng thấy/sửa được thứ không được phép, hoặc bị khóa nhầm: kiểm bằng ma trận ca thử, không dựa vào cảm giác.
- Cấu hình có ~190 khóa, nhiều khóa ảnh hưởng **tiền/giá/kho**; mỗi khóa phải được spec của chức năng dùng nó xác nhận. Spec này chỉ đảm bảo đọc/ghi đúng, không đảm bảo ý nghĩa nghiệp vụ.
- Sao lưu/Phục hồi/Tạo mới là thao tác **phá hủy dữ liệu** nếu làm sai: mặc định chạy trên bản sao, có xác nhận 2 bước khi phục hồi đè DB đang dùng, không bao giờ chạm file gốc ở `KAZUKO TEA/Database/`.
- Cơ chế gốc của sao lưu/phục hồi/tạo mới và màn Cấu hình/Phân quyền nằm trong `No1Lib.*` — chỉ tin được sau khi có ảnh.
- Khóa nội bộ của `SCONFIG` (`SharedCode`, `CompliedCode`, các khóa dài kiểu bản quyền…) có thể chứa mã/bản quyền: **không hiển thị, không sửa, không in ra log**.

**Câu hỏi cho Chiến**
- **Q1.** Phạm vi H1–H6 như trên có đúng ý "nhóm Hệ thống" không? Thứ tự làm đề xuất: Cấu hình → Ảnh → Người dùng → Phân quyền → Đổi mật khẩu → CSDL. Muốn đổi thứ tự không?
- **Q2.** Xóa người dùng đã có thao tác (hóa đơn, phiếu…) thì chặn hay vẫn xóa? Xóa nhóm đang có người dùng? Xóa hẳn hay đổi `STATUS`?
- **Q3.** Mật khẩu giữ chữ thường như app gốc, đúng không? Admin mật khẩu rỗng có giữ không?
- **Q4.** Đổi mật khẩu: có hỏi mật khẩu cũ, có yêu cầu độ dài tối thiểu, có cho đổi sang rỗng?
- **Q5.** Chức năng có `NOTE='View'` chỉ có quyền Có/Không (chỉ cột Xem) đúng không? Tích Sửa/Thêm/Xóa có tự tích Xem? Có nút chọn nhanh cả nhóm chức năng?
- **Q6.** `PhanQuyenTruyCapTheoKhuVuc`: bật thì phân quyền khu vực nằm ở đâu (cột/bảng nào)? Làm trong spec này hay để sau?
- **Q7.** Màn Cấu hình lưu một lần (nút Lưu) hay lưu ngay từng ô? Khóa chưa có dòng trong `SCONFIG` thì app gốc tạo mới hay bỏ qua?
- **Q8.** `GiaoDienChuongTrinh` (Office Blue/Silver/Black) và `DoToChuHienThi`: bản mới thay bằng bảng màu `ThemeManager` + DPI; ẩn hai ô này hay map sang theme gần nhất?
- **Q9.** Xóa ảnh đang được dùng (menu, nhóm, mặt hàng): chặn hay cho xóa rồi để trống? Ảnh có giới hạn kích thước/định dạng khi thêm?
- **Q10.** Quy tắc đổi mật khẩu chi tiết và thông báo lỗi (xem ảnh mục 8.2).
- **Q11.** Sao lưu của app gốc ra file gì (zip/`.fbk`/copy `.fdb`), đặt tên thế nào, thực hiện khi DB đang mở bằng cách nào? "Sao lưu tự động" chạy khi nào (lúc mở/đóng app, mỗi `SoNgaySaoLuu` ngày)?
- **Q12.** "Tạo mới CSDL" tạo DB trống từ đâu (file mẫu, hay chạy DDL trong code)? DB mới có sẵn tài khoản/nhóm/cấu hình/menu mặc định không (và có `Admin`)?
- **Q13.** "Lịch sử tương tác hệ thống / Lưu vết hoạt động" và "Xóa dữ liệu" / "Dữ liệu ban đầu": làm trong nhóm này hay spec riêng sau? (Tôi đề xuất để sau.)
