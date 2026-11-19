# Spec: Bản clone KAZUKO TEA (WinForms, .NET Framework 4.8, Firebird 2.5)

- Trạng thái: **ĐÃ DUYỆT** (Chiến, 2026-11-18); **cập nhật 2026-11-19**: đổi app đích sang POS TAP và ghi các quyết định mới (mục 0). Các số liệu ở mục 1 là của KAZUKO TEA, đã chú thích số tương ứng của POS TAP.
- Ngày: 2026-11-18 (cập nhật 2026-11-19)
- Base: `origin/main` @ `b0d15db` (đã `git fetch origin main` ngày 2026-11-19, không có commit mới hơn); nhánh `main` local đi trước origin
- App đích: thư mục `POS TAP/` (cùng dòng sản phẩm với KAZUKO TEA, xem mục 0 và mục 1.1)
- Nguồn tham chiếu phụ: thư mục `KAZUKO TEA/` (app gốc "PHẦN MỀM QUẢN LÝ TRÀ SỮA", VERSION 7.0.0.0, release 20-11-2018), chỉ để đọc, không sửa

## 0. Quyết định đã chốt (Chiến, 2026-11-19)

| Chủ đề | Quyết định |
| --- | --- |
| **App đích** | **POS TAP** (`D:\Working\FnB\POS TAP`): `Application.exe` = bản desktop quản lý, `ApplicationTs.exe` = bản POS cảm ứng. Ban đầu sếp đưa nhầm KAZUKO TEA; KAZUKO TEA và `KAZUKO_TEA_SRC_EXPORT/` chỉ còn là tham chiếu cùng dòng. |
| **Mục tiêu gần nhất** | **Demo cho sếp duyệt**, chưa có hạn cứng. Chiến chốt phạm vi demo là **toàn bộ UI mới** (xem câu hỏi mở 11 về mức độ nghiệp vụ chạy thật). |
| **Bản làm trước** | **Desktop quản lý** trước; bản POS cảm ứng làm sau. |
| **Thứ tự làm** | **Theo thứ tự menu gốc** (Hoạt động, Khách hàng, Kho hàng, Công nợ, Quỹ, Nhân sự, Quản trị, Báo cáo). Trong mỗi nhóm làm phần nhẹ (danh mục) trước, phần nặng (luồng tiền/giá/kho) sau, để không trễ demo vì nhóm Hoạt động nặng nhất. |
| **Đối chiếu nghiệp vụ** | Chạy được **app gốc + DB mẫu**: so kết quả hai bên trên cùng dữ liệu; phần tiền/giá/kho luôn có bộ kiểm thử đối chiếu. |
| **Loại hình kinh doanh** | Giữ **COFFEE** (ID 0) và **POS** (ID 1). **Bỏ** BIDA, KARAOKE, HOTEL, MASSAGE, PS3, SPA (và HOTEL-KARAOKE). Hệ quả: không làm bảng giá giờ/ngày-đêm, tính tiền giờ, đặt phòng theo giờ; chi tiết ở mục 4.7. |
| **Cơ sở dữ liệu** | Dùng **schema POS TAP**, không đổi cấu trúc. DB dev/test là **bản copy** ở `Kazuko_FnB\Database\` (đã `.gitignore`, không commit): `DATA.fdb` (copy từ `POS TAP\Database\DATA.fdb`), `DEMO.FDB`, `ONLINE.FDB`. Không đụng file gốc trong `POS TAP\Database\`. DB `DATA.FDB` ở gốc repo là schema TEA, không dùng nữa. |
| **Thư viện** | Thư viện bên thứ ba (FastReport, Krypton, Aspose.Cells, Newtonsoft.Json, SourceGrid, FastColoredTextBox, Ionic.Zip, TaskScheduler) đã được bê vào `Kazuko_FnB\lib` (thư mục đã `.gitignore`; giấy phép xem mục 9, câu 4). **Không dùng `No1Lib.*`** (xem mục 1.2). Firebird client dùng bản giống POS TAP. |
| **Giao diện** | Bỏ dark mode; **4 bảng màu** đều sáng (Chiến đổi từ 10 xuống 4, 2026-11-19: Cam Nắng mặc định, Cà Phê, Matcha, Xanh Biển); đổi màu ở menu Quản trị, mục "Giao diện chương trình" (xem mục 2, 3a). |
| **Làm sau** | Sao lưu / phục hồi / tạo mới CSDL; Lịch sử tương tác, Xóa dữ liệu, Dữ liệu ban đầu, Công cụ nhà phát triển (mỗi mục một spec riêng); bản POS cảm ứng; G6 (web/mobile). |

### Tiến độ (2026-11-19)

| Phần | Trạng thái |
| --- | --- |
| Khung chính, menu, đăng nhập, phân quyền | Đã code. |
| Nhóm Hệ thống (cấu hình, thư viện ảnh, người dùng/nhóm/phân quyền, đổi mật khẩu) | Đã code; selftest 86/89 trên bản copy DB POS TAP (3 ca lỗi do dữ liệu khác bản TEA, không phải lỗi code). Spec: `he-thong.md`. |
| Danh mục khách hàng | Spec nháp viết theo schema TEA, chưa duyệt; cần viết lại theo POS TAP (`danh-muc-khach-hang.md`). |
| Đổi bảng màu (theme) | Đã code (gói demo UI, việc A): 4 bảng màu sáng (Cam Nắng mặc định, Cà Phê, Matcha, Xanh Biển), màn "Giao diện chương trình" trong menu Quản trị. Chiến đã thử tay. Spec: `goi-demo-ui.md`. |
| 16 màn danh mục dùng chung (chỉ xem) | Đã code (gói demo UI, việc B1), chạy trên DEMO.FDB: danh mục phẳng và cây nhóm + lưới, tìm không dấu. Chưa có Thêm/Sửa/Xóa (B2). Chưa commit. |
| Hoạt động, Khách hàng (ngoài danh mục), Kho, Công nợ, Quỹ, Nhân sự, Báo cáo | **Chưa phân tích nghiệp vụ**; mục 4 mới là khung và danh sách việc cần xác minh. Mã form POS TAP đã xuất ra `POS_TAP_SRC_EXPORT/` (gitignore) để đọc. |

## 1. Bối cảnh

KAZUKO TEA là phần mềm quản lý quán trà sữa / F&B chạy trên WinForms .NET Framework, dữ liệu Firebird 2.5.5 embedded (`fbembed.dll`, DB ODS 11.x). **Đây là sản phẩm của chính công ty Chiến; sếp giao Chiến viết lại.** Vì vậy đây là viết lại nội bộ (internal rewrite), không phải clone sản phẩm của bên khác: được đọc và dùng lại mã nghiệp vụ, câu SQL, mẫu báo cáo trong DB. Mục tiêu: **nghiệp vụ giống y hệt**, **UI làm lại hiện đại hơn**, vẫn WinForms, **dùng lại `Database/DATA.FDB`**.

Lưu ý quyền sở hữu cần xác nhận (mục 9): framework `No1Lib.*` và các thư viện thương mại (FastReport, Aspose.Cells, Krypton) có thể là của bên thứ ba mà công ty mua giấy phép, chứ không thuộc sở hữu công ty. Việc dùng lại hai nhóm này khác với việc dùng lại mã nghiệp vụ do công ty viết trong DB.

Khảo sát đã làm (chỉ đọc, trên bản sao DB trong thư mục tạm, không sửa file gốc):

- App gốc chạy theo metadata trong DB: menu (`SMENU`), form (`SFORM`), cột lưới (`SCOLUMN`), cấu hình (`SCONFIG`), phân quyền (`SGROUPROLE`), báo cáo (`SREPORT` 126 báo cáo đang bật, `SREPORTTEMPLATE` 155 mẫu in). Thư mục `Report/` trống vì mẫu báo cáo nằm trong DB.
- `DATA.FDB`: 100 bảng. `D*` là danh mục (54 bảng), `T*` là giao dịch (20 bảng), `S*` là hệ thống/metadata (20 bảng). Không có view, không có stored procedure.
- `ONLINE.FDB` chỉ chứa 20 bảng hệ thống `S*`, không có dữ liệu bán hàng. Nhiều khả năng là DB cấu hình cho bản web/mobile (menu có cột `VIEWONMOBI`, `VIEWONWEB`; thư mục có `WebService.exe`, `app.apk`). **Chưa xác nhận.**
- **Mã nghiệp vụ và UI của từng chức năng nằm trong DB, đọc được.** Bảng `SFORM` (110 form) lưu mã C# của form (`CODE`, 104 form, ~1,2 MB), khai báo control (`DESIGNCODE`, 97 form), bố cục XML (`AELAYOUT`, 109 form, ~2,3 MB), bản cảm ứng (`TSCODE`, `TSLAYOUT`, `TSDESIGNCODE`, 23 form). Form lớn nhất: "Hóa đơn nhà hàng" (~213 KB mã), "Dịch vụ máy tính bảng" (~165 KB), "Hóa đơn bán hàng" (~106 KB). `STABLEDESC` (bảng nhập liệu) và `SREPORT` (báo cáo: `SQL`, `CODE`) cũng chứa mã. Đây là nguồn chính để chốt quy tắc nghiệp vụ.
- `No1Lib.Sys.DLL` là framework chung (lưới, form động, engine báo cáo, tồn kho, công nợ, tồn quỹ); `No1Lib.Sys.Ts.DLL` là control cảm ứng; mã trong DB gọi vào các lớp `No1Lib.*`. `No1Lib.Run.dll` (bị packer) và `No1Lib.Core.Dll` (không phải PE chuẩn). **Đã decompile ngày 2026-11-19: No1Lib không chứa logic nghiệp vụ tiền/giá/kho** (xem mục 1.2).
- Vì là app của công ty, **được dùng lại trực tiếp mã nghiệp vụ và câu SQL** trong DB (sao chép logic, công thức tính tiền/kho/công nợ, SQL báo cáo). Tuy nhiên bản viết lại vẫn **viết code mới, không nạp/biên dịch mã từ DB lúc chạy và không phụ thuộc `No1Lib.*`** — vì lý do kỹ thuật: UI phải hiện đại, bỏ engine form-động cũ, và `No1Lib.*` có thể là thư viện bên thứ ba (xem mục 9). Các quy tắc chưa đọc tới vẫn **chưa được xác minh** và sẽ được đối chiếu bằng cách chạy app gốc và ảnh UI Chiến gửi (làm sau). Spec không đoán các quy tắc đó; chúng nằm ở mục 9 (câu hỏi mở).

### 1.1 POS TAP khác KAZUKO TEA ở đâu (đo trên hai bản copy DB, 2026-11-19)

Hai bản cùng framework No1Lib và cùng **100 bảng**, nhưng khác như sau.

- **Bảng chỉ có ở TEA (3):** `DBANGGIAGIO`, `DBANGGIAGIOCHITIET`, `DBANGGIANGAYDEM` (bảng giá theo giờ, theo ngày/đêm).
- **Bảng chỉ có ở POS TAP (3):** `DMATKHAUWIFI`, `DNHOMWIFI`, `TVAORA`.
- **Cột chỉ có ở TEA:** `DBANGGIA` (15 cột về giá giờ/ngày/đêm, phụ thu, thêm người/trẻ em/giường), `DKHACHHANG.SOCMND/NGAYCAP/NOICAP`, `DBAN.ROOMADDRESS`, `TDONHANG` (`DIACHI`, `DIENTHOAI`, `TENKHACH`, `SOCMND`, `GIATHOATHUAN`, `THOATHUANTHEONGAY`, `GIUONGTHEM`, `NGUOILONTHEM`, `TREEMTHEM`), `TDONHANGCHITIET.GIOGOI`, `TSUACHUA.NGAYSUAXONG`.
- **Cột chỉ có ở POS TAP:** `DKHACHHANG.DTHETRATRUOCID`, `DNHANVIEN` (`CODE`, `DCALAMVIECID`, `LUONGTHEOCA`), `DCALAMVIEC` (`TUGIO`, `DENGIO`), `DBANQUYEN` (`KETNOILUC`, `MADANGKY`), `DMATHANG.GIOHATCOMBO`, `TBANGLUONGTONGHOP` (`LUONGTHEOCA`, `SOGIOLAM`, `SOGIOTANGCA`), `TDONHANG` (`GIOTRADO`, `PASSWIFI`), `TDONHANGCHITIET.GIOTINHLUONG`, `TINCHEBIEN.DONGIA`.
- **Quy mô metadata (POS TAP so với TEA):** `SFORM` 111 (110), `SREPORT` 127, `SMENU` 78, `SFUNCTION` 86 (81), `SCONFIG` 205 (196); `SCONFIG.UngDung` = 1 (POS), `CoTienGio` = 0.
- **Chức năng mới ở POS TAP:** In lại bill; Xóa/giảm món; Xóa/giảm món sau khi in chế biến; Danh mục mật khẩu wifi; Bản quyền máy tính bảng; Điều chỉnh giờ tính lương dịch vụ theo giờ. **Bỏ:** Danh mục bảng giá phòng.

Mọi nội dung "đã xác minh" trong các spec con viết trước 2026-11-19 là trên bản TEA; phải đối chiếu lại với POS TAP bằng `POS_TAP_SRC_EXPORT/` trước khi dùng.

### 1.2 Engine No1Lib (decompile 2026-11-19)

Chi tiết ở `docs/reference/no1lib-engine.md`. Kết luận chính:

- **No1Lib không chứa logic nghiệp vụ** (quét toàn bộ mã decompile, 0 kết quả cho từ khóa tiền/giá/kho/công nợ). Logic nghiệp vụ nằm trong mã form đã xuất (`SFORM`, `STABLEDESC`, `SREPORT`).
- `No1Lib.Sys` (5,7 MB) bị obfuscate; `No1Lib.Core` và `No1Lib.Run` decompile ra rỗng. Chỉ `No1Lib.Db` và `No1Lib.Utils` đọc được.
- **Quy ước lưu/xóa của engine, bản viết lại phải giữ đúng:** khóa chính `ID` là GUID; `STATUS = 30` là bản ghi hoạt động, `STATUS = -30` là xóa mềm (thùng rác); engine tự điền `USERCREATEDID`, `TIMECREATED` khi thêm và `USERMODIFIEDID`, `TIMEMODIFIED` khi sửa. Engine gốc nối `ID` thẳng vào chuỗi SQL; bản viết lại phải tham số hóa.
- `ConvertTo.Long` trả -1 khi null/lỗi (khác các kiểu số khác trả 0): cần giữ khi đọc cột kiểu này.

## 2. Mục tiêu

1. Bản clone chạy trên .NET Framework 4.8, WinForms, đọc/ghi **cùng schema** `DATA.FDB` (Firebird 2.5) mà không đổi cấu trúc dữ liệu.
2. Có đủ các nhóm chức năng của app gốc (mục 4) với kết quả nghiệp vụ giống app gốc trên cùng dữ liệu.
3. UI hiện đại hơn: bố cục gọn, theo style nhất quán, hỗ trợ màn hình cảm ứng cho quầy bán hàng, high-DPI. Style tham chiếu: ảnh chụp KAZUKO GOLD V8.0 do Chiến cung cấp (menu ngang + thanh công cụ icon, tab tài liệu, thẻ bo góc, lưới tiêu đề màu). Chỉ lấy bố cục/phong cách; logo, icon, hotline, tên và mô tả bảng màu của sản phẩm đó không dùng lại.
3a. Có tính năng **đổi màu giao diện toàn hệ thống** (như màn hình "Giao diện & Phối màu" trong ảnh tham chiếu): chọn bảng màu, xem thử trực tiếp, "Áp dụng & Lưu", "Khôi phục mặc định", **4 bảng màu, đều nền sáng; không có chế độ nền tối (Chiến bỏ dark mode, 2026-11-19)**. Theme đổi cả màu thanh tiêu đề lẫn màu nền nhạt vùng làm việc, viền tab/thẻ (icon và dải tiêu đề thẻ giữ màu riêng). Mở màn bằng **mục menu mới "Giao diện chương trình" trong menu Quản trị** (Chiến chốt, 2026-11-19). Màn chọn theo ảnh tham chiếu trong `docs/specs/img/theme-tham-khao/`: lưới thẻ (tên, nhãn phong cách, đối tượng, 4 chấm màu, mô tả), bấm thẻ = xem thử ngay, Đóng không lưu = hoàn lại, toast sau khi lưu. Tên/mô tả bảng màu tự đặt theo F&B.
4. In và xuất các báo cáo hiện có **giữ nguyên nội dung** (dùng lại SQL và mẫu in trong `SREPORT`, `SREPORTTEMPLATE`).

## 3. Phạm vi

### Làm

- Project **`Kazuko_FnB`** (đã tạo trong repo `FnB`): WinForms .NET Framework 4.8, build x86, Firebird 2.5 embedded.
- Đăng nhập, người dùng, nhóm người dùng, phân quyền theo `SUSER`, `SGROUPUSER`, `SGROUPROLE`, `SREPORTROLE`.
- Cấu hình hệ thống (`SCONFIG`, `SCONFIGGROUP`).
- Các nhóm chức năng theo menu gốc: Hoạt động, Khách hàng, Kho hàng, Công nợ, Quỹ, Nhân sự, Quản trị (mục 4).
- Màn hình "Quản lý bán hàng nhà hàng" trên bản desktop. Bản POS cảm ứng (`ApplicationTs.exe`, "Màn cảm ứng") làm **sau** bản desktop (mục 0).
- Báo cáo: chạy các báo cáo từ `SREPORT` (126 ở TEA, 127 ở POS TAP), in/xuất qua FastReport dùng mẫu trong `SREPORTTEMPLATE`.
- Sao lưu / phục hồi DB (app gốc có `BackupDatabase`, `RestoreDatabase`).
- Màn hình chọn bảng màu giao diện và cơ chế đổi theme lúc chạy (mục 3a ở trên).

### KHÔNG làm (giai đoạn này)

- Không đổi schema `DATA.FDB` (không DDL), không migrate sang Firebird 3/4/5.
- Không đụng vào DB gốc (`POS TAP/Database/*`, `KAZUKO TEA/Database/DATA.FDB`) khi dev/test: dùng bản sao ở `Kazuko_FnB\Database\`. (`git status` đang báo `DATA.FDB` ở gốc repo bị sửa do chạy thử; không commit nó và không commit file DB nào.)
- Không làm các loại hình kinh doanh đã bỏ (BIDA, KARAOKE, HOTEL, MASSAGE, PS3, SPA, HOTEL-KARAOKE) và các tính năng chỉ phục vụ chúng: tính tiền theo giờ, bảng giá giờ/ngày-đêm, đặt phòng (mục 4.7).
- Chưa làm bản POS cảm ứng (`ApplicationTs.exe`) ở các giai đoạn đầu.
- Chưa phân tích và chưa làm ở các giai đoạn đầu: `WebService.exe`, `app.apk` / dịch vụ máy tính bảng (MTB), `ONLINE.FDB`. Chiến đã xác nhận các phần này **thuộc phạm vi clone**, nên sẽ có giai đoạn riêng (G6) sau khi phân tích; hiện chưa có đủ thông tin để viết acceptance criteria.
- Không đụng vào `KAZUKO_ERP_JEWELRY` (vẫn .NET 8 / Firebird 5.0 theo CLAUDE.md).
- Chưa có công cụ sinh code tự động từ metadata.

## 4. Nghiệp vụ chi tiết (khung theo menu gốc)

**Thanh menu (đã xác minh khi làm G1.3):** gồm 7 nhóm từ `SMENU` (Hoạt động, Khách hàng, Kho hàng, Công nợ, Quỹ, Nhân sự, Quản trị) cộng các menu **cố định trong mã app gốc** (`MenuTs`): "Hệ thống" (CSDL, tạo mới/sao lưu/phục hồi, người dùng và phân quyền, thư viện ảnh, cấu hình toàn hệ thống, đổi mật khẩu, đăng xuất, thoát, đăng ký bản quyền), "Báo cáo" và "Trợ giúp". Thanh công cụ icon lấy từ `SMENU.TOOLBAR = 30` (11 mục); phím tắt lưu dạng số của `Keys`. Bản viết lại chưa làm "Báo cáo" (G4) và "Đăng ký bản quyền". Mục "Truy cập nhanh" là phần mới theo UI mẫu, dựng từ các nhóm `SMENU`.

Danh sách dưới đây lấy từ `SMENU` và tên bảng (khung viết trên bản TEA, **chưa đối chiếu với POS TAP**; khác biệt ở mục 1.1). Cột "Xác minh" là việc cần làm trước khi viết chi tiết quy tắc: đọc mã form tương ứng trong `SFORM.CODE` (và `STABLEDESC.CODE`, `SREPORT.SQL`) trong `POS_TAP_SRC_EXPORT/`, rồi đối chiếu bằng app gốc chạy trên DB mẫu. Việc đọc form hóa đơn, nhập kho để rút công thức tiền/giá/kho đã được lên lịch làm khi tới nhóm tương ứng.

### 4.1 Hoạt động
| Chức năng | Bảng chính | Xác minh |
| --- | --- | --- |
| Bảng giá (theo bàn, theo khu vực). **Bảng giá giờ và ngày/đêm không còn ở POS TAP** (không có `DBANGGIAGIO(+CHITIET)`, `DBANGGIANGAYDEM`; bỏ menu "Danh mục bảng giá phòng") | `DBANGGIA`, `DBANGGIATHEOBAN`, `DBANGGIATHEOKHUVUC` | Thứ tự ưu tiên khi có nhiều bảng giá |
| Cửa hàng, mặt hàng, loại phòng, bàn/khu vực | `DCUAHANG`, `DMATHANG`, `DLOAIPHONG`, `DBAN`, `DKHUVUC` | Trường bắt buộc, ràng buộc |
| Khách đặt hàng, theo dõi đặt phòng | `TDATHANG(+CHITIET)` | Trạng thái đặt, cọc |
| Quản lý bán hàng nhà hàng, điều chỉnh hóa đơn | `TDONHANG(+CHITIET)`, `TDONHANGGIO`, `TCHITIETTHANHTOAN`, `TSOHOADON` | Mở bàn, gọi món, chuyển/gộp/tách bàn, thanh toán, in bill, số hóa đơn |
| Chế biến (in chế biến, trạng thái) | `TINCHEBIEN`, `TSOINCHEBIEN`, `DTRANGTHAICHEBIEN` | Luồng in bếp/bar |
| Hóa đơn hủy, món xóa/giảm/trả | `TDONHANGHUY(+CHITIET)`, `DLYDOHUY` | Quyền hủy, lý do |
| Kiểm soát order, lưu vết | `TLUUVET`, `TLUUTAM(+CHITIET)`, `STRACKING` | Nội dung lưu vết |
| Thông báo sửa chữa | `TSUACHUA` | Luồng xử lý |
| Thống kê doanh thu, mặt hàng bán, kết quả kinh doanh, chi tiết hoạt động ngày | báo cáo | Công thức |
| **Mới ở POS TAP:** In lại bill; Xóa/giảm món; Xóa/giảm món sau khi in chế biến | Chưa xác định (có thể liên quan `TDONHANGHUY(+CHITIET)`, `TINCHEBIEN`; `TINCHEBIEN` có thêm cột `DONGIA` so với TEA) | Đọc form tương ứng trong `POS_TAP_SRC_EXPORT/`; quyền, lý do, ghi lưu vết; ảnh hưởng chế biến đã in |

### 4.2 Khách hàng
Khách hàng, nhóm khách, đợt khuyến mại (`DDOTKHUYENMAI`, `DLOAIHINHKHUYENMAI`), voucher (`DVOUCHER`, `DNHOMVOUCHER`), thẻ trả trước (`DTHETRATRUOC`, `DNHOMTHETRATRUOC`), khách hàng thân thiết / tăng giảm điểm (`TTANGGIAMDIEM`), nhắn tin tới khách. Xác minh: quy tắc tích điểm, áp dụng khuyến mại/voucher, trừ thẻ trả trước. Ở POS TAP `DKHACHHANG` bỏ `SOCMND`, `NGAYCAP`, `NOICAP` và thêm `DTHETRATRUOCID` (khách gắn thẻ trả trước); spec Danh mục khách hàng cần viết lại theo schema này.

### 4.3 Kho hàng
Danh mục kho, nhà cung cấp; nhập kho, xuất khác, chuyển kho, kiểm kê; tính lại giá vốn; xuất lại định lượng (`DDINHLUONG`); tồn kho (một kho / nhiều kho); báo cáo hạn dùng (HSD). Dùng chung bảng `TDONHANG(+CHITIET)` cho phiếu nhập/xuất (menu trỏ tới `TDONHANG`). Xác minh: phương pháp tính giá vốn, trừ kho theo định lượng khi bán, cho phép âm kho (`CHOPHEPAMKHO` xuất hiện trong schema).

### 4.4 Công nợ
Công nợ khách hàng, nhà cung cấp; đối chiếu công nợ; công nợ ban đầu. Xác minh: cách ghi nhận khi thanh toán một phần (`TCHITIETTHANHTOAN`), `CHITIETCONGNOEX`.

### 4.5 Quỹ
Lý do thu chi, tài khoản ngân hàng, phiếu thu/chi (`TTHUCHI`), tồn quỹ.

### 4.6 Nhân sự
Nhân viên, ca làm việc, tạm ứng lương, thưởng phạt (`TTHUONGPHAT`), chấm công và tính lương (`TBANGLUONG`, `TBANGLUONGCHITIET`, `TBANGLUONGTONGHOP`; có `ExcelSalaryEngine` trong framework gốc nên bảng lương có thể dùng công thức kiểu Excel). Xác minh: cách tính lương. POS TAP thêm lương theo ca (`DNHANVIEN.LUONGTHEOCA`, `DNHANVIEN.DCALAMVIECID`, `DCALAMVIEC.TUGIO/DENGIO`, `TBANGLUONGTONGHOP.LUONGTHEOCA/SOGIOLAM/SOGIOTANGCA`) và chức năng "Điều chỉnh giờ tính lương dịch vụ theo giờ" (`TDONHANGCHITIET.GIOTINHLUONG`); cần xác minh chức năng này còn ý nghĩa khi đã bỏ các loại hình tính tiền theo giờ.

### 4.7 Quản trị
Dữ liệu ban đầu (công nợ, tồn kho đầu kỳ), nhập dữ liệu từ hệ thống cũ, cấu hình máy in hóa đơn theo khu vực, thiết kế giao diện màn cảm ứng, phân quyền, sao lưu/phục hồi. Mới ở POS TAP: Danh mục mật khẩu wifi (`DMATKHAUWIFI`, `DNHOMWIFI`; `TDONHANG.PASSWIFI`) và Bản quyền máy tính bảng (`DBANQUYEN.KETNOILUC`, `MADANGKY`; thuật toán mã máy/DES xem `docs/reference/no1lib-engine.md`).

**Loại hình kinh doanh / "Ứng dụng" (thông tin mới của Chiến, 2026-11-19; ảnh `docs/specs/img/pos/`).** Bản POS (máy cảm ứng, `ApplicationTs.exe`) mở màn **"CHỌN ỨNG DỤNG"** liệt kê các loại hình kinh doanh, nút *Thêm / Sửa / Xóa trống / Thoát*. Danh sách nằm ở bảng `DUNGDUNG` (9 dòng): `0` COFFEE, `1` POS, `2` KARAOKE, `3` BIDA, `4` HOTEL, `5` MASSAGE, `6` HOTEL-KARAOKE, `7` PS3, `8` SPA (cột `ID`, `NAME`, `SORTORDER`, `SIMAGEID`…). Loại hình đang chọn lưu ở `SCONFIG.UngDung` (`TEXTVALUE` = ID; bản sao hiện = `4` HOTEL; nhóm cấu hình "Ứng dụng" đang bị ẩn trên màn Cấu hình) và mã app gốc đọc qua `Shared.UngDung` để đổi giao diện/hành vi (ví dụ SPA: chọn nhân viên theo từng dòng mặt hàng; HOTEL: ẩn ô "từ giờ" ở phiếu đặt hàng; còn các cờ như `CoTienGio`, `SuDungGiaTheoGio`).

**Đã chốt (Chiến, 2026-11-19): giữ COFFEE (0) và POS (1); bỏ BIDA, KARAOKE, HOTEL, MASSAGE, PS3, SPA, HOTEL-KARAOKE.** Còn mở: bản FnB có màn "CHỌN ỨNG DỤNG" hay không và nó hoạt động thế nào (Chiến nói tôi hiểu sai về màn này vì app đích có hai bản desktop và POS cảm ứng; chưa rõ chi tiết, cần hỏi lại), và có đổi `SCONFIG.UngDung` trong DB dev hay không (Chiến: "từ từ đã"). DB POS TAP đang có `UngDung` = 1 (POS).

### 4.8 Báo cáo (126 ở TEA, 127 ở POS TAP)
Nhóm: bán hàng (theo ngày/giờ/khu vực/bàn/nhân viên/thu ngân/khách hàng/mặt hàng/nhóm hiển thị), nhập-xuất-tồn, thẻ kho, công nợ, thu chi, lãi gộp (giá nhập / giá vốn), kiểm kê, chuyển kho, đặt hàng, biểu đồ doanh thu, hoa hồng NVKD, sinh nhật khách. Danh sách đầy đủ lưu trong `SREPORT.NAME`.

## 5. Quy tắc kiến trúc

- Solution mới, tách lớp: `UI` (WinForms), `Application/ViewModel` (MVP/MVVM nhẹ, không logic trong Form), `Data` (SQL viết tay, không ORM), `Reporting`.
- SQL không đặt trong Form/UI. Mọi truy cập DB qua lớp `Data`. Transaction rõ ràng cho đơn hàng, kho, công nợ, quỹ.
- Quy ước dữ liệu phải giữ y hệt app gốc (từ `No1Lib.Db`, mục 1.2): `ID` là GUID, `STATUS` 30/-30, tự điền cột audit; mọi SQL dùng tham số, kể cả `ID`.
- DB dev/test: `Kazuko_FnB\Database\DATA.fdb` (bản copy POS TAP). Đường dẫn mặc định khi chưa chọn: tự tìm `Database\DATA.fdb` cạnh exe hoặc ở thư mục cha; người dùng đổi ở tab "Cơ sở dữ liệu" của màn đăng nhập.
- Kết nối Firebird 2.5 embedded qua `FirebirdSql.Data.FirebirdClient` bản tương thích FB 2.5 và .NET 4.8 (phiên bản cụ thể cần xác minh khi dựng project). `fbembed.dll` là bản **x86** nên project mặc định build **x86**.
- Theme: mỗi bảng màu là một bộ token (nền, chữ, màu chính, màu nhấn, viền, trạng thái). Một `ThemeManager` giữ theme hiện tại và phát sự kiện `ThemeChanged`; toàn bộ control tự viết, renderer của menu/toolbar, tab, lưới đọc màu từ token và vẽ lại khi theme đổi. Xem thử = áp dụng tạm, đóng không lưu thì hoàn lại theme cũ. Không dùng thư viện skin không hỗ trợ đổi theme lúc chạy. Icon phải đổi màu được (SVG tô màu theo token hoặc bộ icon trung tính).
- UI: bộ design token (màu, cỡ chữ, khoảng cách) tập trung, không hard-code trong màn hình nghiệp vụ; dialog gọn, hạn chế cuộn dọc; high-DPI; chế độ cảm ứng cho quầy bán hàng.
- Báo cáo: nạp SQL + mẫu từ `SREPORT` / `SREPORTTEMPLATE`; không sao chép lại logic.

## 6. Acceptance criteria

Chia theo giai đoạn; mỗi giai đoạn chỉ được coi là xong khi các mục sau đạt, kiểm trên **bản sao** `DATA.FDB`.

**G0 Nền tảng**
- [ ] Solution build được bằng `dotnet build` hoặc MSBuild với target `net48`, x86, không cảnh báo lỗi.
- [ ] Mở được bản sao `DATA.FDB` qua Firebird 2.5 embedded, đọc đúng số dòng các bảng (khớp bảng thống kê khảo sát).
- [ ] Không có DDL nào chạy lên DB; kiểm bằng so sánh danh sách bảng/cột trước và sau.

**G1 Hệ thống & danh mục**
- [ ] Đăng nhập bằng tài khoản có sẵn trong `SUSER`; phân quyền theo `SGROUPROLE` ẩn/hiện đúng chức năng so với app gốc.
- [ ] CRUD các danh mục `D*` giống app gốc về trường, ràng buộc, thông báo lỗi (đối chiếu từng màn hình).

**G2 Bán hàng**
- [ ] Luồng mở bàn → gọi món → thanh toán → in bill cho kết quả giống app gốc (cùng dữ liệu đầu vào cho cùng tổng tiền, giảm giá, số hóa đơn).
- [ ] Bảng giá giờ/ngày-đêm/khuyến mại/voucher/thẻ trả trước áp dụng đúng theo bộ ca kiểm thử đối chiếu với app gốc.
- [ ] Hủy món/hóa đơn cần quyền và ghi lý do, lưu vết đúng.

**G3 Kho, công nợ, quỹ, nhân sự**
- [ ] Tồn kho, giá vốn, công nợ, tồn quỹ, bảng lương cho kết quả trùng báo cáo tương ứng của app gốc trên cùng dữ liệu.

**G4 Báo cáo**
- [ ] 126 báo cáo `SREPORT` mở được; với mỗi báo cáo, số liệu trùng app gốc trên cùng dữ liệu và kỳ lọc.
- [ ] Mẫu in từ `SREPORTTEMPLATE` hiển thị và in được; xuất Excel/PDF nếu app gốc có.

**G5 UI**
- [ ] Mọi màn hình có độ phủ tương đương menu gốc; kiểm 100%, 125%, 150% DPI.
- [ ] Màn hình bán hàng dùng được hoàn toàn bằng cảm ứng (áp dụng cho bản POS cảm ứng, làm sau bản desktop; bản desktop chỉ cần thao tác chạm cơ bản được).
- [ ] Đổi bảng màu có tác dụng ngay trên mọi màn hình đang mở (menu, toolbar, tab, lưới, dialog) mà không cần khởi động lại; xem thử rồi đóng không lưu thì giữ nguyên theme cũ.
- [ ] Theme được lưu theo người dùng/máy và tự nạp lại lần mở sau; "Khôi phục mặc định" hoạt động.
- [ ] Tất cả bảng màu đạt độ tương phản chữ/nền đọc được (kiểm bằng công cụ tính tương phản WCAG, mục tiêu tối thiểu AA cho chữ thường).

## 7. Kế hoạch implement (sau khi duyệt)

1. G0: hoàn thiện project `Kazuko_FnB` (net48, x86), lớp truy cập Firebird, bộ test đọc DB (trên bản sao), khung theme.
2. G1: **đăng nhập trước** (Chiến chốt ưu tiên), rồi phân quyền, cấu hình, danh mục.
3. Đọc `SFORM.CODE`, `STABLEDESC.CODE`, `SREPORT.SQL` của từng chức năng (trong `POS_TAP_SRC_EXPORT/`) để rút quy tắc nghiệp vụ, ghi lại bằng lời vào mục 4; sau đó chạy app gốc cùng Chiến và nhận ảnh UI để đối chiếu → cập nhật spec. Dùng ripgrep (công cụ Grep) để tìm trong mã xuất: `grep` của Git Bash báo sai với các file có dòng rất dài.
4. Thứ tự theo menu gốc (mục 0), cho **bản desktop trước**. Bước tiếp theo đã thống nhất: (a) viết lại spec Danh mục khách hàng theo schema POS TAP; (b) làm một màn danh mục dùng chung dựng từ metadata `STABLEDESC` (app gốc dựng các danh mục từ cùng một màn "bảng nhập liệu"; cần kiểm bao nhiêu danh mục dùng được màn này trước khi cam kết) và lấy Khách hàng làm mẫu; (c) tới nhóm Hoạt động: danh mục nhẹ trước, rồi đọc form hóa đơn/nhập kho để rút công thức tiền/giá/kho, mỗi phần có bộ kiểm thử đối chiếu với app gốc.
5. G2 → G3 → G4 → G5 theo thứ tự trên; bản POS cảm ứng sau.
6. Mỗi giai đoạn là một hoặc nhiều patch nhỏ theo quy trình của repo; không commit/push khi chưa được Chiến bảo.

Files/vùng chạm: toàn bộ solution mới (chưa tồn tại). Không có migration DB.

## 8. Rủi ro

- **Quy tắc nghiệp vụ phải suy ra từ mã form trong DB và hành vi app gốc** (No1Lib không chứa logic nghiệp vụ, mục 1.2; chỉ còn `No1Lib.Run`/`No1Lib.Core` chưa đọc được); rủi ro sai lệch ở các phép tính tiền/kho/công nợ. Giảm thiểu bằng bộ ca kiểm thử đối chiếu với app gốc chạy trên DB mẫu.
- **Spec cũ viết theo schema TEA**: các spec con và khung mục 4 chưa đối chiếu với POS TAP (mục 1.1); dùng nhầm sẽ sai ở khách hàng, bảng giá, lương, đơn hàng.
- **Phạm vi demo "toàn bộ UI mới" rất rộng**: nếu mọi màn đều phải có UI mới mà nghiệp vụ lõi (bán hàng, kho, công nợ) chưa phân tích xong thì demo dễ chỉ là vỏ. Cần chốt mức nghiệp vụ chạy thật (câu hỏi mở 11).
- **Dùng chung schema với app gốc**: bản clone dùng bản copy riêng của `DATA.FDB`, không mở cùng file với app gốc (Firebird embedded thường chỉ cho một tiến trình mở DB). Nếu sau này cần chuyển dữ liệu qua lại thì phải bảo toàn schema.
- **Phạm vi mở rộng sang web/mobile (G6)**: `WebService.exe`, `app.apk`, `ONLINE.FDB` chưa được phân tích; khối lượng có thể lớn.
- **Giấy phép thư viện**: app là của công ty nên mã nghiệp vụ dùng lại được; nhưng FastReport, Aspose.Cells, Krypton, EASendMail, và có thể cả `No1Lib.*`, là thư viện thương mại / bên thứ ba. Cần xác nhận công ty có giấy phép hợp lệ để dùng chúng trong bản viết lại, hoặc thay bằng thư viện khác (xem mục 9).
- **Phiên bản driver Firebird**: cần bản tương thích đồng thời FB 2.5 và .NET 4.8.
- **`fbembed.dll` là x86**: ràng buộc kiến trúc build.
- Dữ liệu `DATA.FDB` có thể chứa thông tin cửa hàng/người dùng thật: không đưa vào repo công khai.

## 9. Câu hỏi mở (cần Chiến trả lời hoặc xác minh bằng app gốc)

Đã trả lời (Chiến, 2026-11-18; thứ tự trả lời "có / có" được hiểu theo thứ tự câu 2 và 3, cần Chiến xác nhận nếu hiểu sai):

1. ~~Chạy đồng thời với app gốc?~~ **Dùng chung schema, không chạy chung file DB**: Chiến sẽ tự copy `DATA.FDB` ra dùng cho bản clone. Clone không được đổi schema để vẫn đọc được DB của app gốc.
2. ~~`ONLINE.FDB`, `WebService.exe`, `app.apk`, "Dịch vụ MTB" có trong phạm vi?~~ **Có.** Cần phân tích riêng, xem G6 ở mục 3.
3. ~~Giữ SMS qua cổng COM (USB 3G) và máy in mã vạch (TSC TTP-244, Bartender)?~~ **Có.**

Đã trả lời thêm (Chiến, 2026-11-18):

- **5. Thư viện UI:** **tự vẽ theo design token** (không dùng thư viện UI bên thứ ba).
- **6. Thứ tự ưu tiên:** **đăng nhập trước**, rồi tới các phần sau.
- **7. Project:** đã tạo **`Kazuko_FnB`** trong repo `FnB` (WinForms net48 x86); `bin/obj/.vs` đã gitignore.
- **9. Đổi màu:** **10 bảng màu** là được (đã đổi thành **4 bảng màu**, 2026-11-19). Theme lưu ở file cấu hình cục bộ theo người dùng Windows (không đụng DB) — chưa có ý kiến khác nên giữ đề xuất này.
- **10. CLAUDE.md:** sửa CLAUDE.md của repo `FnB` sang stack .NET 4.8, Firebird 2.5, WinForms (đang làm).

Đã trả lời thêm (Chiến, 2026-11-19): xem mục 0 (app đích POS TAP, loại hình COFFEE/POS, demo, desktop trước, thứ tự menu gốc, đối chiếu bằng app gốc + DB mẫu).

Còn lại chưa xác định:

4. Quyền sở hữu / giấy phép: `No1Lib.*` do công ty tự viết hay mua của bên thứ ba? FastReport/Aspose/Krypton/EASendMail công ty có giấy phép hợp lệ để dùng trong bản viết lại không, hay phải thay bằng thư viện khác? Công ty có sẵn **mã nguồn gốc đầy đủ** (ngoài phần nằm trong DB) không? (Các DLL bên thứ ba đã được bê vào `Kazuko_FnB\lib` nhưng thư mục này `.gitignore`, chưa commit.)
8. Danh sách ảnh UI và các kịch bản chạy app gốc cần quay lại để chốt quy tắc ở mục 4 (Chiến gửi sau).
11. **Mức nghiệp vụ trong demo:** phạm vi demo là "toàn bộ UI mới". Chiến (2026-11-19): **sếp muốn thấy UI mới, càng sớm càng tốt**, chưa có ngày cụ thể. Hệ quả: ưu tiên thứ nhìn thấy được (khung, đổi bảng màu, các màn danh mục chạy trên dữ liệu thật) trước nghiệp vụ lõi tiền/giá/kho. Còn mở: có cần luồng bán hàng chạy thật trong demo không (chưa rõ), và ngày demo.
12. **Màn "CHỌN ỨNG DỤNG"** và việc đổi `SCONFIG.UngDung` trong DB dev (xem mục 4.7). Theme mặc định (tạm "Cam").
