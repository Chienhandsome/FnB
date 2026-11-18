# Spec: Bản clone KAZUKO TEA (WinForms, .NET Framework 4.8, Firebird 2.5)

- Trạng thái: **ĐÃ DUYỆT** (Chiến, 2026-11-18)
- Ngày: 2026-11-18
- Base: `main` @ `26641ad` (đã `git fetch origin main`, không có commit mới hơn)
- Nguồn tham chiếu: thư mục `KAZUKO TEA/` (app gốc "PHẦN MỀM QUẢN LÝ TRÀ SỮA", VERSION 7.0.0.0, release 20-11-2018)

## 1. Bối cảnh

KAZUKO TEA là phần mềm quản lý quán trà sữa / F&B chạy trên WinForms .NET Framework, dữ liệu Firebird 2.5.5 embedded (`fbembed.dll`, DB ODS 11.x). **Đây là sản phẩm của chính công ty Chiến; sếp giao Chiến viết lại.** Vì vậy đây là viết lại nội bộ (internal rewrite), không phải clone sản phẩm của bên khác: được đọc và dùng lại mã nghiệp vụ, câu SQL, mẫu báo cáo trong DB. Mục tiêu: **nghiệp vụ giống y hệt**, **UI làm lại hiện đại hơn**, vẫn WinForms, **dùng lại `Database/DATA.FDB`**.

Lưu ý quyền sở hữu cần xác nhận (mục 9): framework `No1Lib.*` và các thư viện thương mại (FastReport, Aspose.Cells, Krypton) có thể là của bên thứ ba mà công ty mua giấy phép, chứ không thuộc sở hữu công ty. Việc dùng lại hai nhóm này khác với việc dùng lại mã nghiệp vụ do công ty viết trong DB.

Khảo sát đã làm (chỉ đọc, trên bản sao DB trong thư mục tạm, không sửa file gốc):

- App gốc chạy theo metadata trong DB: menu (`SMENU`), form (`SFORM`), cột lưới (`SCOLUMN`), cấu hình (`SCONFIG`), phân quyền (`SGROUPROLE`), báo cáo (`SREPORT` 126 báo cáo đang bật, `SREPORTTEMPLATE` 155 mẫu in). Thư mục `Report/` trống vì mẫu báo cáo nằm trong DB.
- `DATA.FDB`: 100 bảng. `D*` là danh mục (54 bảng), `T*` là giao dịch (20 bảng), `S*` là hệ thống/metadata (20 bảng). Không có view, không có stored procedure.
- `ONLINE.FDB` chỉ chứa 20 bảng hệ thống `S*`, không có dữ liệu bán hàng. Nhiều khả năng là DB cấu hình cho bản web/mobile (menu có cột `VIEWONMOBI`, `VIEWONWEB`; thư mục có `WebService.exe`, `app.apk`). **Chưa xác nhận.**
- **Mã nghiệp vụ và UI của từng chức năng nằm trong DB, đọc được.** Bảng `SFORM` (110 form) lưu mã C# của form (`CODE`, 104 form, ~1,2 MB), khai báo control (`DESIGNCODE`, 97 form), bố cục XML (`AELAYOUT`, 109 form, ~2,3 MB), bản cảm ứng (`TSCODE`, `TSLAYOUT`, `TSDESIGNCODE`, 23 form). Form lớn nhất: "Hóa đơn nhà hàng" (~213 KB mã), "Dịch vụ máy tính bảng" (~165 KB), "Hóa đơn bán hàng" (~106 KB). `STABLEDESC` (bảng nhập liệu) và `SREPORT` (báo cáo: `SQL`, `CODE`) cũng chứa mã. Đây là nguồn chính để chốt quy tắc nghiệp vụ.
- `No1Lib.Sys.DLL` là framework chung (lưới, form động, engine báo cáo, tồn kho, công nợ, tồn quỹ); `No1Lib.Sys.Ts.DLL` là control cảm ứng; mã trong DB gọi vào các lớp `No1Lib.*`. `No1Lib.Run.dll` (bị packer) và `No1Lib.Core.Dll` (không phải PE chuẩn); phần logic nằm riêng trong hai file này, nếu có, chưa được biết.
- Vì là app của công ty, **được dùng lại trực tiếp mã nghiệp vụ và câu SQL** trong DB (sao chép logic, công thức tính tiền/kho/công nợ, SQL báo cáo). Tuy nhiên bản viết lại vẫn **viết code mới, không nạp/biên dịch mã từ DB lúc chạy và không phụ thuộc `No1Lib.*`** — vì lý do kỹ thuật: UI phải hiện đại, bỏ engine form-động cũ, và `No1Lib.*` có thể là thư viện bên thứ ba (xem mục 9). Các quy tắc chưa đọc tới vẫn **chưa được xác minh** và sẽ được đối chiếu bằng cách chạy app gốc và ảnh UI Chiến gửi (làm sau). Spec không đoán các quy tắc đó; chúng nằm ở mục 9 (câu hỏi mở).

## 2. Mục tiêu

1. Bản clone chạy trên .NET Framework 4.8, WinForms, đọc/ghi **cùng schema** `DATA.FDB` (Firebird 2.5) mà không đổi cấu trúc dữ liệu.
2. Có đủ các nhóm chức năng của app gốc (mục 4) với kết quả nghiệp vụ giống app gốc trên cùng dữ liệu.
3. UI hiện đại hơn: bố cục gọn, theo style nhất quán, hỗ trợ màn hình cảm ứng cho quầy bán hàng, high-DPI. Style tham chiếu: ảnh chụp KAZUKO GOLD V8.0 do Chiến cung cấp (menu ngang + thanh công cụ icon, tab tài liệu, thẻ bo góc, lưới tiêu đề màu). Chỉ lấy bố cục/phong cách; logo, icon, hotline, tên và mô tả bảng màu của sản phẩm đó không dùng lại.
3a. Có tính năng **đổi màu giao diện toàn hệ thống** (như màn hình "Giao diện & Phối màu" trong ảnh tham chiếu): chọn bảng màu, xem thử trực tiếp, "Áp dụng & Lưu", "Khôi phục mặc định", có ít nhất một bảng màu nền tối.
4. In và xuất các báo cáo hiện có **giữ nguyên nội dung** (dùng lại SQL và mẫu in trong `SREPORT`, `SREPORTTEMPLATE`).

## 3. Phạm vi

### Làm

- Project **`Kazuko_FnB`** (đã tạo trong repo `FnB`): WinForms .NET Framework 4.8, build x86, Firebird 2.5 embedded.
- Đăng nhập, người dùng, nhóm người dùng, phân quyền theo `SUSER`, `SGROUPUSER`, `SGROUPROLE`, `SREPORTROLE`.
- Cấu hình hệ thống (`SCONFIG`, `SCONFIGGROUP`).
- Các nhóm chức năng theo menu gốc: Hoạt động, Khách hàng, Kho hàng, Công nợ, Quỹ, Nhân sự, Quản trị (mục 4).
- Màn hình bán hàng cảm ứng ("Màn cảm ứng" / "Quản lý bán hàng nhà hàng").
- Báo cáo: chạy 126 báo cáo từ `SREPORT`, in/xuất qua FastReport dùng mẫu trong `SREPORTTEMPLATE`.
- Sao lưu / phục hồi DB (app gốc có `BackupDatabase`, `RestoreDatabase`).
- Màn hình chọn bảng màu giao diện và cơ chế đổi theme lúc chạy (mục 3a ở trên).

### KHÔNG làm (giai đoạn này)

- Không đổi schema `DATA.FDB` (không DDL), không migrate sang Firebird 3/4/5.
- Không đụng vào `KAZUKO TEA/Database/DATA.FDB` gốc khi dev/test: dùng bản sao. (`git status` đang báo file này bị sửa; không commit nó.)
- Chưa phân tích và chưa làm ở các giai đoạn đầu: `WebService.exe`, `app.apk` / dịch vụ máy tính bảng (MTB), `ONLINE.FDB`. Chiến đã xác nhận các phần này **thuộc phạm vi clone**, nên sẽ có giai đoạn riêng (G6) sau khi phân tích; hiện chưa có đủ thông tin để viết acceptance criteria.
- Không đụng vào `KAZUKO_ERP_JEWELRY` (vẫn .NET 8 / Firebird 5.0 theo CLAUDE.md).
- Chưa có công cụ sinh code tự động từ metadata.

## 4. Nghiệp vụ chi tiết (khung theo menu gốc)

Danh sách dưới đây lấy từ `SMENU` và tên bảng. Cột "Xác minh" là việc cần làm trước khi viết chi tiết quy tắc: đọc mã form tương ứng trong `SFORM.CODE` (và `STABLEDESC.CODE`, `SREPORT.SQL`), rồi đối chiếu bằng app gốc.

### 4.1 Hoạt động
| Chức năng | Bảng chính | Xác minh |
| --- | --- | --- |
| Bảng giá (giờ, ngày/đêm, theo bàn, theo khu vực) | `DBANGGIA`, `DBANGGIAGIO(+CHITIET)`, `DBANGGIANGAYDEM`, `DBANGGIATHEOBAN`, `DBANGGIATHEOKHUVUC` | Thứ tự ưu tiên khi có nhiều bảng giá |
| Cửa hàng, mặt hàng, loại phòng, bàn/khu vực | `DCUAHANG`, `DMATHANG`, `DLOAIPHONG`, `DBAN`, `DKHUVUC` | Trường bắt buộc, ràng buộc |
| Khách đặt hàng, theo dõi đặt phòng | `TDATHANG(+CHITIET)` | Trạng thái đặt, cọc |
| Quản lý bán hàng nhà hàng, điều chỉnh hóa đơn | `TDONHANG(+CHITIET)`, `TDONHANGGIO`, `TCHITIETTHANHTOAN`, `TSOHOADON` | Mở bàn, gọi món, chuyển/gộp/tách bàn, thanh toán, in bill, số hóa đơn |
| Chế biến (in chế biến, trạng thái) | `TINCHEBIEN`, `TSOINCHEBIEN`, `DTRANGTHAICHEBIEN` | Luồng in bếp/bar |
| Hóa đơn hủy, món xóa/giảm/trả | `TDONHANGHUY(+CHITIET)`, `DLYDOHUY` | Quyền hủy, lý do |
| Kiểm soát order, lưu vết | `TLUUVET`, `TLUUTAM(+CHITIET)`, `STRACKING` | Nội dung lưu vết |
| Thông báo sửa chữa | `TSUACHUA` | Luồng xử lý |
| Thống kê doanh thu, mặt hàng bán, kết quả kinh doanh, chi tiết hoạt động ngày | báo cáo | Công thức |

### 4.2 Khách hàng
Khách hàng, nhóm khách, đợt khuyến mại (`DDOTKHUYENMAI`, `DLOAIHINHKHUYENMAI`), voucher (`DVOUCHER`, `DNHOMVOUCHER`), thẻ trả trước (`DTHETRATRUOC`, `DNHOMTHETRATRUOC`), khách hàng thân thiết / tăng giảm điểm (`TTANGGIAMDIEM`), nhắn tin tới khách. Xác minh: quy tắc tích điểm, áp dụng khuyến mại/voucher, trừ thẻ trả trước.

### 4.3 Kho hàng
Danh mục kho, nhà cung cấp; nhập kho, xuất khác, chuyển kho, kiểm kê; tính lại giá vốn; xuất lại định lượng (`DDINHLUONG`); tồn kho (một kho / nhiều kho); báo cáo hạn dùng (HSD). Dùng chung bảng `TDONHANG(+CHITIET)` cho phiếu nhập/xuất (menu trỏ tới `TDONHANG`). Xác minh: phương pháp tính giá vốn, trừ kho theo định lượng khi bán, cho phép âm kho (`CHOPHEPAMKHO` xuất hiện trong schema).

### 4.4 Công nợ
Công nợ khách hàng, nhà cung cấp; đối chiếu công nợ; công nợ ban đầu. Xác minh: cách ghi nhận khi thanh toán một phần (`TCHITIETTHANHTOAN`), `CHITIETCONGNOEX`.

### 4.5 Quỹ
Lý do thu chi, tài khoản ngân hàng, phiếu thu/chi (`TTHUCHI`), tồn quỹ.

### 4.6 Nhân sự
Nhân viên, ca làm việc, tạm ứng lương, thưởng phạt (`TTHUONGPHAT`), chấm công và tính lương (`TBANGLUONG`, `TBANGLUONGCHITIET`, `TBANGLUONGTONGHOP`; có `ExcelSalaryEngine` trong framework gốc nên bảng lương có thể dùng công thức kiểu Excel). Xác minh: cách tính lương.

### 4.7 Quản trị
Dữ liệu ban đầu (công nợ, tồn kho đầu kỳ), nhập dữ liệu từ hệ thống cũ, cấu hình máy in hóa đơn theo khu vực, thiết kế giao diện màn cảm ứng, phân quyền, sao lưu/phục hồi.

### 4.8 Báo cáo (126)
Nhóm: bán hàng (theo ngày/giờ/khu vực/bàn/nhân viên/thu ngân/khách hàng/mặt hàng/nhóm hiển thị), nhập-xuất-tồn, thẻ kho, công nợ, thu chi, lãi gộp (giá nhập / giá vốn), kiểm kê, chuyển kho, đặt hàng, biểu đồ doanh thu, hoa hồng NVKD, sinh nhật khách. Danh sách đầy đủ lưu trong `SREPORT.NAME`.

## 5. Quy tắc kiến trúc

- Solution mới, tách lớp: `UI` (WinForms), `Application/ViewModel` (MVP/MVVM nhẹ, không logic trong Form), `Data` (SQL viết tay, không ORM), `Reporting`.
- SQL không đặt trong Form/UI. Mọi truy cập DB qua lớp `Data`. Transaction rõ ràng cho đơn hàng, kho, công nợ, quỹ.
- Kết nối Firebird 2.5 embedded qua `FirebirdSql.Data.FirebirdClient` bản tương thích FB 2.5 và .NET 4.8 (phiên bản cụ thể cần xác minh khi dựng project). `fbembed.dll` là bản **x86** nên project mặc định build **x86**.
- Theme: mỗi bảng màu là một bộ token (nền, chữ, màu chính, màu nhấn, viền, trạng thái). Một `ThemeManager` giữ theme hiện tại và phát sự kiện `ThemeChanged`; toàn bộ control tự viết, renderer của menu/toolbar, tab, lưới đọc màu từ token và vẽ lại khi theme đổi. Xem thử = áp dụng tạm, đóng không lưu thì hoàn lại theme cũ. Không dùng thư viện skin không hỗ trợ đổi theme lúc chạy. Icon phải đổi màu được (SVG tô màu theo token hoặc bộ icon trung tính) để dùng được trên nền tối.
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
- [ ] Màn hình bán hàng dùng được hoàn toàn bằng cảm ứng.
- [ ] Đổi bảng màu có tác dụng ngay trên mọi màn hình đang mở (menu, toolbar, tab, lưới, dialog) mà không cần khởi động lại; xem thử rồi đóng không lưu thì giữ nguyên theme cũ.
- [ ] Theme được lưu theo người dùng/máy và tự nạp lại lần mở sau; "Khôi phục mặc định" hoạt động.
- [ ] Tất cả bảng màu đạt độ tương phản chữ/nền đọc được (kiểm bằng công cụ tính tương phản WCAG, mục tiêu tối thiểu AA cho chữ thường), kể cả bảng màu nền tối.

## 7. Kế hoạch implement (sau khi duyệt)

1. G0: hoàn thiện project `Kazuko_FnB` (net48, x86), lớp truy cập Firebird, bộ test đọc DB (trên bản sao), khung theme.
2. G1: **đăng nhập trước** (Chiến chốt ưu tiên), rồi phân quyền, cấu hình, danh mục.
3. Đọc `SFORM.CODE`, `STABLEDESC.CODE`, `SREPORT.SQL` của từng chức năng để rút quy tắc nghiệp vụ, ghi lại bằng lời vào mục 4; sau đó chạy app gốc cùng Chiến và nhận ảnh UI để đối chiếu → cập nhật spec.
4. G2 → G3 → G4 → G5 theo thứ tự ưu tiên Chiến chọn.
5. Mỗi giai đoạn là một hoặc nhiều patch nhỏ theo quy trình của repo; không commit/push khi chưa được Chiến bảo.

Files/vùng chạm: toàn bộ solution mới (chưa tồn tại). Không có migration DB.

## 8. Rủi ro

- **Quy tắc nghiệp vụ phải suy ra từ mã trong DB và hành vi app gốc**, có thể còn phần nằm trong `No1Lib.Run.dll`/`No1Lib.Core.Dll`; rủi ro sai lệch ở các phép tính tiền/kho/công nợ. Giảm thiểu bằng bộ ca kiểm thử đối chiếu với app gốc.
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
- **9. Đổi màu:** **10 bảng màu** là được. Theme lưu ở file cấu hình cục bộ theo người dùng Windows (không đụng DB) — chưa có ý kiến khác nên giữ đề xuất này.
- **10. CLAUDE.md:** sửa CLAUDE.md của repo `FnB` sang stack .NET 4.8, Firebird 2.5, WinForms (đang làm).

Còn lại chưa xác định:

4. Quyền sở hữu / giấy phép: `No1Lib.*` do công ty tự viết hay mua của bên thứ ba? FastReport/Aspose/Krypton/EASendMail công ty có giấy phép hợp lệ để dùng trong bản viết lại không, hay phải thay bằng thư viện khác? Công ty có sẵn **mã nguồn gốc đầy đủ** (ngoài phần nằm trong DB) không?
8. Danh sách ảnh UI và các kịch bản chạy app gốc cần quay lại để chốt quy tắc ở mục 4 (Chiến gửi sau).
