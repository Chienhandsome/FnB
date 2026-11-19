# Hướng dẫn kiến trúc & UI cho Claude — Kazuko_FnB

Tài liệu này tóm gọn mọi quyết định đã chốt để Claude đọc và tuân thủ khi code bản clone. Nguồn sự thật về nghiệp vụ: `docs/pos-tap-nghiep-vu*.md` + rà soát vòng 1 + vòng 2 (`docs/tong-hop-ra-soat-*.md`, `docs/deep/`). Nếu tài liệu nghiệp vụ và DB thật khác nhau thì **DB thật (`POS TAP\Database\DATA.fdb`) là chuẩn**; số đo trực tiếp ở `docs/reference/db-pos-tap-do-truc-tiep.md`, nhật ký sửa ở `docs/doi-chieu-vong-2.md`.

## 1. Bối cảnh — không làm lại

- **App đích: POS TAP** (`Application.exe` desktop quản lý, `ApplicationTs.exe` POS cảm ứng). TEA chỉ còn là tham chiếu.
- **F&B thuần:** chỉ giữ COFFEE (0) + POS (1). Bỏ BIDA/KARAOKE/HOTEL/MASSAGE/PS3/SPA và toàn bộ tính tiền theo giờ, bảng giá giờ/ngày-đêm, đặt phòng theo giờ. Sau này sẽ bổ sung nhà hàng, trà sữa, quán ăn,... nhưng cũng cùng là 1 dạng thôi.
- **Bán lẻ CÓ trong phạm vi** (Chiến, 19/11) — loại hình tự phục vụ (khách tới quầy tự order/thanh toán, không phục vụ tại bàn). Lưu ý theo `docs/reference/db-pos-tap-do-truc-tiep.md` §5: cột `DMATHANG.LOAIDINHLUONG` không tồn tại trong DB thật và form `Hóa đơn bán hàng` không có menu trong POS TAP — bản clone làm luồng bán lẻ **KHÔNG được tham chiếu cột đó**. Ghi rõ điểm này để spec bán hàng xử lý sau, không tự bịa cách giải quyết.
- **Stack:** WinForms **.NET Framework 4.8**, x86, Firebird 2.5 Embedded, **schema POS TAP giữ nguyên, không DDL**. Không dùng `No1Lib.*` (logic nghiệp vụ không nằm trong No1Lib mà nằm trong mã form lưu ở DB). Thư viện bên thứ ba bê vào `Kazuko_FnB\lib`.
- **DB dev/test** là bản copy ở `Kazuko_FnB\Database\` (gitignore). **DB chuẩn = `DATA.fdb` hiện tại** (Chiến chốt 19/11): 100 bảng, 111 form, 128 dòng báo cáo (127 bật + 1 tắt), 86 function, 205 khóa `SCONFIG`, `DKHACHHANG` 21 cột, không có `DNGANHANG`. Các con số "DEMO thừa 1 form/1 báo cáo/3 function/7 config, chỉ khác `DNGANHANG`" của bản rà vòng 2 là của `FNB_DEMO.fdb`, không áp dụng. `DEMO.FDB` trong thư mục cài chỉ dùng làm dữ liệu mẫu cho demo.
- **Sếp đã cho phép viết lại app mới (19/11/2026)** — được dùng lại nghiệp vụ/SQL/mẫu báo cáo trong DB. Thư viện thương mại (FastReport) cần chốt giấy phép riêng; `No1Lib.*` không dùng lại.
- **Mốc gần nhất:** demo toàn bộ UI mới cho sếp duyệt. Desktop trước, POS cảm ứng sau. Thứ tự theo menu gốc: Hoạt động (danh mục trước, luồng tiền/giá/kho sau) → Khách hàng → Kho → Công nợ → Quỹ → Nhân sự → Báo cáo.
- **Quy ước engine phải giữ:** PK `ID` là GUID; `STATUS = 30` đang dùng, `-30` xóa mềm; tự điền `USERCREATEDID`/`TIMECREATED` khi thêm, `USERMODIFIEDID`/`TIMEMODIFIED` khi sửa; `ConvertTo.Long` trả -1 khi null/lỗi (kiểu số khác trả 0).

## 2. Kiến trúc: UI → BE → DB, trong cùng 1 process

BE là **một lớp trong app desktop**, chưa phải server riêng.

```
UI — WinForms (chỉ binding + hiển thị)
  CẤM: SQL, tính tiền, kiểm quyền chỉ ở UI
BE — Services (luật nghiệp vụ, 1 chỗ duy nhất)
  SalesService, InventoryService, DebtService...
  + Transaction + 1 money engine + kiểm quyền
Data — Repositories (SQL tham số hóa)
  + Entities map đúng cột TDONHANG...
DB — Firebird 2.5 Embedded, schema giữ nguyên
```

### Luật từng lớp (lấy từ đúng lỗi app cũ)

- **UI mỏng.** Form không chứa SQL, không tính tiền, không tự quyết quyền. App cũ sai ở đây: SQL + công thức nằm trong `SFORM.CODE`, cùng một quy tắc viết lại ở nhiều form rồi lệch nhau (4 công thức tồn kho, 2 bộ công thức tiền).
- **BE là nơi duy nhất chứa luật.** Mọi công thức tiền/kho/nợ chỉ viết một lần trong Services, UI và báo cáo đều gọi qua đó.
- **Thanh toán + trừ kho = 1 transaction.** `FbTransaction` bao toàn bộ: ghi bill, trừ kho, ghi điểm, ghi công nợ — commit cùng lúc hoặc rollback hết. App cũ có 0 transaction, đây là lỗi số 1 cần sửa.
- **SQL tham số hóa 100%.** Không ghép chuỗi — sửa luôn lỗ SQL injection của app cũ.
- **Kiểm quyền trong Services, không chỉ ở UI.** App cũ kiểm quyền ở client nên MTB gọi thẳng là qua mặt được.
- **Lưu vết chỉ ghi thêm, không được xóa.** `TLUUVET` append-only; bỏ hẳn nút xóa toàn bảng + mật khẩu cứng 9876. Bỏ mọi mật khẩu cứng (`6789`/`9876`/`4321`/`6666`).

### Chia project (modular monolith, giữ đơn giản) — ĐÃ CHỐT tách 5 project (Chiến, 19/11)

**Đã tách (19/11/2026, base `5822006`, đã commit tại `9313697`):** `Kazuko_FnB.Domain` (DTO/entity), `Kazuko_FnB.Data`, `Kazuko_FnB.Services`, `Kazuko_FnB.Infrastructure` (`AppSettings`), `Kazuko_FnB.UI` (exe: toàn bộ WinForms, `Theme/`, `Shell/` = `MainMenuBuilder`/`IconCatalog`/`BrandLogo`/`GlyphMap`, `Diagnostics/SelfTest`). Build MSBuild Debug sạch; selftest 210 đạt / 3 lỗi, **trùng hệt** bản trước khi tách (3 lỗi cũ: nhóm còn tài khoản ×2 và "đủ 81 chức năng" — DB thật có 86 nên kỳ vọng 81 trong selftest đã lỗi thời). Việc còn lại: `UI` vẫn tham chiếu `FirebirdSql.Data.FirebirdClient` chỉ để `catch (FbException)` ở 11 chỗ (nên bọc thành exception riêng trong `Data`/`Services`); `Services` có `DatabaseService` dùng `FbException`; các spec cũ vẫn ghi đường dẫn `Kazuko_FnB/Data/`, `Kazuko_FnB/UI/`, `Kazuko_FnB/Services/MainMenuBuilder.cs`… → đọc là `Kazuko_FnB.Data/`, `Kazuko_FnB.UI/`, `Kazuko_FnB.UI/Shell/MainMenuBuilder.cs`. Quy tắc tách ban đầu: mỗi project là `.csproj` classic, đều build **x86**, `TargetFramework v4.8`, thêm file bằng `Compile Include`; chuỗi tham chiếu một chiều `UI → Services → Data → Domain` (`Infrastructure` được `UI`/`Services`/`Data` dùng); `FirebirdSql.Data.FirebirdClient` + `fbembed.dll` chỉ ở `Data` và project khởi chạy; `lib\` dùng chung; selftest hiện có chuyển sang project test hoặc giữ ở `UI`.

```
Kazuko_FnB/
  Kazuko_FnB.UI/            WinForms, form + binding
  Kazuko_FnB.Services/      luật nghiệp vụ (Services)
  Kazuko_FnB.Data/          repositories + Firebird
  Kazuko_FnB.Domain/        entities / DTOs
  Kazuko_FnB.Infrastructure/ config, log, in ấn (FastReport)
```

### Vì sao chưa tách BE thành server riêng

1. Firebird Embedded chỉ cho 1 process mở file — tách API server riêng + app desktop riêng thì 2 process giành nhau file DB.
2. App phải chạy offline trong 1 quán — tách server không thêm được gì, chỉ thêm deploy 2 thứ, auth UI–API, xử lý mất mạng.
3. Việc trước mắt là clone đúng nghiệp vụ, không phải làm nền tảng đa chi nhánh.

### Thiết kế sao cho sau này tách được

Services **không dính gì tới WinForms** (không nhận `Form`, `DataGridView`, `Control` làm tham số; chỉ nhận DTO/entity, trả kết quả). Repositories nằm sau interface. Ngày nào cần đa chi nhánh/web/mobile, chỉ việc khoác một lớp ASP.NET Core API lên trên Services, không viết lại luật nghiệp vụ. Bản POS cảm ứng sau này là **đầu UI thứ hai** dùng chung Services/Data, không phải app riêng — khác cách app cũ làm MTB (server HTTP tự chế trong app desktop, 28/38 route không kiểm quyền).

### 3 quyết định còn chờ Chiến chốt

1. **Một luật làm tròn duy nhất cho tiền — ĐÃ CHỐT (Chiến, 19/11):** dùng luật nhà hàng cho cả bán lẻ: `LamTron(tiền, LamTronTien)` làm tròn tới bội của `LamTronTien` (hiện 1000), `AwayFromZero`. App cũ có 2 luật (bán lẻ làm tròn thường) — bản clone chỉ giữ một.
2. **Dapper hay ADO.NET thuần cho lớp Data.** Đề xuất Dapper: nhỏ, chạy tốt trên .NET Framework 4.8, tham số hóa mặc định. Nếu đã code theo hướng khác thì giữ hướng đó, đừng đổi giữa chừng.
3. **Test cho công thức tiền/kho/nợ.** Bản rà soát cho sẵn "đáp án" (ví dụ: mua chịu 10M trả 4M thì nợ phải ra 6M, không phải 14M). Mỗi công thức viết xong phải có test khóa lại.

## 3. Dựng lại UI y hệt nhưng restyle — nguồn dữ liệu trong DB

App gốc metadata-driven: UI + logic nằm trong DB. Việc dựng lại dựa vào đó là **khả thi (~90%)**, đã chứng minh bằng script parse thử: form Hóa đơn bán hàng trích được **82 control + 34 event wiring** trong vài giây.

| Cần gì để dựng lại | Lấy ở đâu trong DB |
|---|---|
| Cây control + vị trí + kích thước + event | `SFORM.AELAYOUT` (XML; 111 form — trong `POS_TAP_SRC_EXPORT` là `layout.xml`) |
| Danh sách control (loại + tên) | `SFORM.DESIGNCODE` (`designcode.cs`) |
| Logic + handler làm gì | `SFORM.CODE` (C#, 111 form — `code.cs`) |
| UI cảm ứng | `TSCODE` + `TSLAYOUT` + `TSDESIGNCODE` (26 form mỗi loại — `ts_*.cs/xml`) |
| Menu + thứ tự + quyền | `SMENU` (78 mục, 77 đang bật; chỉ ~30 form có menu trỏ tới, còn lại là hộp thoại mở từ form khác) |
| Ảnh/icon | `SIMAGE` (272 ảnh PNG) |
| Mẫu in | `SREPORTTEMPLATE` (156 dòng manifest: 36 `.frx` + `.config` của báo cáo tự sinh) |
| Cột lưới | `AELAYOUT` FieldMap + `SGRID` (chỉ 2 dòng: Mặt hàng, Khách hàng; 14 danh mục còn lại dùng thứ tự `SCOLUMN`) |
| Metadata UI khác | `SFORM` còn có `FILTERCONFIG`, `REFCONFIG`, `CLIENTCODE`/`SERVERCODE` (2 form), `TSLAYOUT` — đã xuất hết vào `pos-tap-review/raw/` |

### Quy trình 4 bước

1. **Trích spec UI tự động theo form.** Script parse `AELAYOUT` + `CODE` + `DESIGNCODE` ra 1 file spec cho mỗi form: cây control, event wiring, handler làm gì, binding dữ liệu, cột lưới, luồng thao tác (mở → nhập → lưu → điều hướng), context menu. Cách này đã được chứng minh: bản thử Phiếu thu dựng từ `AELAYOUT` đạt 211/211.
2. **Bảng map control (làm 1 lần).** `AELAYOUT` dùng control của `No1Lib` + `Krypton` mà bản clone không dùng `No1Lib` nữa, nên map sang WinForms chuẩn: `DataSearch` → `DataGridView`, `No1NumericUpDown` → `NumericUpDown`, `KryptonSplitContainer` → `SplitContainer`, `KryptonNavigator` → `TabControl`... Chốt bảng này một lần, dùng cho cả 112 form.
3. **Dựng lại từng form theo spec**, áp theme mới qua một lớp style chung (mục 4), không hard-code màu từng control. Cấu trúc + luồng giữ y hệt, chỉ khác skin.
4. **Kiểm chứng:** script đối chiếu cây control (bản dựng vs spec) + Chiến test tay theo luồng trong spec.

### Quick menu (menu chuột phải) — toàn bộ chỉ có 4 cái

Không có đối tượng nào tên "QuickMenu" trong DB. Menu chuột phải định nghĩa tường minh chỉ có ở **4/112 form** — spec UI của mỗi form phải có mục "Context menu" ghi: gắn vào control nào, hiện khi nào, từng mục gồm chữ hiển thị + điều kiện bật/tắt + handler Click làm gì.

1. **Khu vực control** (`mnu`) — menu thao tác chính trên sơ đồ bàn: Mở phòng · Mở phòng từ đặt trước · Thanh toán · Refresh · Chuyển phòng · **Ngắt giờ (BỎ — tính giờ không làm)** · Gộp phòng · Hủy hóa đơn. Lưu ý: chữ "phòng" trong app gốc = bàn.
2. **Theo dõi đặt phòng** (`mnuPopUp`): Tạo đặt phòng (Insert) · Sửa đặt phòng (F4) · Xóa đặt phòng (Delete) · Refresh (F5) · Xuất excel · Số ngày xem (30/60/90/7/15 ngày, Tháng này/trước/sau).
3. **Dịch vụ máy tính bảng** (`mnuTray`): Hiển thị cửa sổ cấu hình · Thoát khỏi hệ thống. Đây là menu chuột phải của **icon khay hệ thống**, không phải menu trên form.
4. **Thiết kế giao diện bàn** (`mnuContext`): Thêm bàn · Thêm ảnh · Lưới 2/4/8/16/24px · 800×600 / 1024×768 / 1280×800 · Copy / Dán / Xóa · Đem lên phía trước / Đẩy về phía sau. Kèm logic `Opening` bật/tắt mục theo trạng thái đang chọn. **Lưu ý: dòng hiện menu trong `designer_MouseUp` đang bị comment (`// mnuContext.Show(Cursor.Position)`) — menu này có thể không hiện trên app thật, cần xác minh khi test; spec ghi rõ, không dựng lại cái đã bị vô hiệu hóa.**

Ngoài 4 cái trên, các lưới (`DataSearch`/`GridMapper`) có menu chuột phải riêng của engine qua event `OnContextMenuShowing` (lọc, sắp xếp, xuất...). Cái này **không định nghĩa trong DB** — là hành vi có sẵn của control `No1Lib`. Khi clone sang `DataGridView`, tự làm lại menu lưới này (hoặc quyết định có cần không).

#### Mẫu in: sửa bằng FastReport Designer nhúng trong app (đã xác minh bằng ảnh chụp màn hình 19/11)

- App cũ **CÓ** trình sửa mẫu nhúng trong app — kết luận "không có bằng chứng" trước đây là **sai**: tính năng này nằm trong engine `No1Lib`, không có dòng `SFORM` nào nên tìm trong DB không thấy.
- Luồng: chức năng **"Cấu hình toàn hệ thống"** (`SFUNCTION` ID 7, nhóm Hệ thống) → hộp thoại cấu hình → "Phần này dành cho nhà phát triển, mời bạn nhập mật khẩu" → mật khẩu **`no1admin`** (cứng trong engine, không có trong DB) → bấm **Mở khóa** → chọn nhóm mẫu + mẫu (vd: Mẫu 80, Mẫu A4 nằm ngang, Mẫu A4 thẳng đứng) → bấm **"Sửa mẫu"** → FastReport Designer mở ngay trong app dưới dạng tab (kéo-thả sửa trên canvas, có nút Preview) → bấm **"Cập nhật vào cơ sở dữ liệu"** để ghi file `.frx` đã sửa trở lại `SREPORTTEMPLATE.TEMPLATE`.
- **Sếp đã cho phép viết lại app mới (19/11) → chốt làm B luôn.** Bản clone gồm: (1) **render** bill/báo cáo từ `.frx` trong DB; (2) màn **quản lý mẫu in**: danh sách nhóm mẫu + mẫu từ `SREPORTTEMPLATE`, xuất/nhập `.frx`, xem trước; (3) **Designer nhúng trong app**: cổng mật khẩu nhà phát triển (đổi được, không hard-code `no1admin`), mở Designer (`Report.Design()`) dưới dạng tab, nút **"Cập nhật vào cơ sở dữ liệu"** ghi `.frx` về `SREPORTTEMPLATE.TEMPLATE`, nút Preview, nút khôi phục mẫu gốc. Không cần dựng lại cả hộp thoại "Cấu hình toàn hệ thống" 13 mục — mấy mục đó là màn cài đặt, làm riêng. **Điều kiện duy nhất còn lại: chốt giấy phép FastReport.** Mức B (Designer nhúng) cần license FastReport .NET thương mại; bản Open Source chỉ dùng cho render (mức A) — chưa kiểm chứng Open Source có Designer nhúng không. Việc chốt license hỏi sếp. **+ test mở 1 file `.frx` thật** (mẫu tạo bằng FastReport 1.2.47, năm 2014–2016) trong Designer mới trước khi code.
- **Khác app cũ ở 1 điểm:** mật khẩu nhà phát triển KHÔNG hard-code `no1admin` nữa — để trong cấu hình đổi được (mật khẩu cứng là lỗi bảo mật đã ghi trong rà soát).

## 4. Đồng nhất style toàn app

**Nguyên tắc duy nhất: không một form nào được tự set màu/chữ cho control của nó.** Mọi style nằm ở một chỗ trung tâm, form chỉ xài đúng loại control.

### Bộ 4 món (làm một lần, dùng cho cả 112 form)

1. **Design tokens — bảng màu theo theme.** 4 theme đã chốt, đều sáng, không dark mode: **Cam Nắng (mặc định), Cà Phê, Matcha, Xanh Biển**. Mỗi theme là một bảng màu cùng bộ tên: `Primary`, `PrimaryDark`, `Surface`, `Text`, `TextMuted`, `Border`, `Success`, `Warning`, `Danger`. Form không bao giờ xài mã màu trực tiếp, chỉ xài tên này. Đổi theme = đổi bảng màu, không đụng form nào. (Màn "Giao diện chương trình" trong Quản trị + cơ chế đổi theme đã code.)
2. **`ThemeManager.ApplyTheme(form)` — hàm duy nhất tô màu.** Duyệt đệ quy cây control của form và tô theo loại: `Button` → kiểu nút, `TextBox` → kiểu ô nhập, `DataGridView` → header + dòng + màu chọn... Mọi form gọi đúng 1 dòng này trong `Load`. Đổi theme lúc đang chạy thì gọi lại hàm này cho các form đang mở.
3. **Control riêng của app (`KzButton`, `KzTextBox`, `KzComboBox`...).** Kế thừa từ control chuẩn WinForms, style nhét sẵn trong constructor (kiểu nút phẳng, viền, padding, font). Form dùng `KzButton` thay vì `Button` là tự đẹp ngay từ lúc kéo thả trong designer. Lớp chống trôi thứ hai: kể cả ai đó quên gọi `ApplyTheme` thì nút vẫn đúng kiểu.
4. **`BaseForm` — form cha cho mọi form.** Tất cả form kế thừa nó thay vì `Form` trực tiếp. Nó lo: gọi `ApplyTheme` khi mở, set font/icon chung, phím tắt Enter/Escape, giới hạn kích thước.

### Thành phần dùng chung đã có trong code (`Kazuko_FnB.UI`) — màn mới PHẢI dùng, không tự tạo

Tên thật trong code khác tên `Kz*` ghi ở trên; khi viết spec/màn mới dùng tên thật:

| Vai trò | Lớp thật | Quy tắc |
|---|---|---|
| Nút | `Controls/FlatButton` | Nút duy nhất của app; cấm `new Button()` (nếu lỡ có, `ThemeApplier` vẫn tô theme nhưng phải đổi sang `FlatButton`). Kiểu nút qua `Tag`: `"primary"`, `"danger"`, null. |
| Ô nhập | `Controls/InputBox` (bo góc, icon, hiện mật khẩu) | `ThemeApplier` tự tô; không gán màu tay. `TextBox` thường vẫn được tô (viền đơn) cho chỗ không cần bo góc. |
| Menu chuột phải | `Controls/ThemedContextMenu` | Dùng `AddItem(text, [phím tắt], onClick)` / `AddSeparator()`, gán `control.ContextMenuStrip = menu`. Cấm tự tạo `ContextMenuStrip` thô. Cùng renderer với menu chính. |
| Tô màu theme | `ThemeApplier.Apply(root, tokens)` | Hàm duy nhất tô cây control (tương đương `ThemeManager.ApplyTheme(form)` ở trên). Kiểu control qua `Control.Tag`. |
| Form cha | `BaseForm` (`ThemedDialog` kế thừa cho hộp thoại) | Mọi form kế thừa `BaseForm` (font, icon, đăng ký/hủy `ThemeChanged`); chỉ ghi đè `ApplyFormTheme()`. Hộp thoại dùng `ThemedDialog` (có tiêu đề/thân/chân nút). |

### Quy tắc kích thước (ghi thành bảng, không để mỗi form tự chọn) - này chỉ là kích thước chọn trước thôi, có thể tôi sẽ yêu cầu chỉnh sửa lại cho phù hợp

Nút chuẩn 90×32, nút chính 110×32; ô nhập cao 28; label 9pt, tiêu đề 10pt đậm; lề form 12px, khoảng cách giữa các control 8px; lưới: header cao 28, dòng cao 26. Các số này nằm chung trong `ThemeManager` dưới dạng hằng số. Font: **một** font cho toàn app (đề xuất Segoe UI 9pt), `AutoScaleMode` giống nhau ở mọi form.

### Bẫy WinForms phải nhớ

- `TextBox` không đổi được màu viền trực tiếp — phải bọc bằng `Panel` hoặc tự vẽ viền.
- `DataGridView` muốn tô header thì phải tắt `EnableHeadersVisualStyles` trước.
- `ToolStrip`/`MenuStrip` tô qua `ToolStripRenderer` + bảng màu riêng, không set màu từng nút.

### Chống trôi về sau

- **Runtime thắng design-time:** `ApplyTheme` chạy lúc mở form sẽ ghi đè mọi màu set trong designer.
- **Script quét:** quét toàn bộ `*.Designer.cs`, form nào còn hard-code `Color.FromArgb` hay `Font = new Font(...)` thì báo ra danh sách.
- **Chụp màn hình đối chiếu:** mỗi form dựng xong chụp 1 ảnh, đặt cạnh nhau xem nút/ô nhập có giống hệt nhau không.

### Phân công

Spec UI trích ra chỉ cần ghi **loại control** (nút, ô nhập, lưới, lookup...), không cần ghi màu sắc cũ. Claude lo **cấu trúc + luồng**, theme engine lo **ngoại hình**. Restyle sau này chỉ sửa một chỗ.

## 5. Luật cứng từ rà soát nghiệp vụ (nhắc lại ngắn)

- **1 money engine + transaction** cho thanh toán/trừ kho (mục 2). Không mang sang bản clone: nhiều bộ công thức tiền/số/tồn song song; mô hình DB không bảo vệ gì (0 FK/CHECK — đã chốt giữ schema thì bù bằng transaction + validation ở app + test khóa công thức); mật khẩu cứng/chữ thường, SQL ghép chuỗi, server không auth.
- **Giữ lại:** phủ tính năng + quy ước dữ liệu đã đúng (BIGINT cho tiền, xóa mềm STATUS, tự điền người tạo/sửa, một bảng chứng từ `TDONHANG`); tinh thần metadata-driven ở mức dữ liệu — nhưng code phải ở trong repo, không nằm trong DB nữa.
- Công thức tiền/kho/nợ đã rà soát 2 vòng (241 câu hỏi, trả lời thêm 186) — giữ đúng hoặc sửa theo danh sách bug đã chứng minh bằng code (ví dụ: bản tổng hợp NCC sai dấu, KQKD lệch `−2×ChiPhiThangTruoc`, 2 báo cáo tháng cộng nhầm cột...). Chi tiết trong `pos-tap-review/tong-hop-ra-soat-vong-2.md`.
- **Câu chặn còn lại — chỉ trả lời được bằng app gốc, chưa viết code thanh toán/công nợ trước khi có đáp án** (thao tác test cụ thể ở `docs/checklist-test-app-goc.md` phần A): (1) trừ kho ở đâu + transaction khi thanh toán; (2) phiếu thu công nợ có trừ nợ thật không; (3) `LOAI 5/6` là gì; (4) bản tổng hợp NCC 10M/4M + Tồn quỹ `LOAI=2` + lỗi `@TODATE` + đặt trước; (5) thuật toán sinh `DeviceId`/`MADANGKY` bản quyền MTB (MTB làm sau).
- **Đã trả lời bằng đo `DATA.fdb` thật (19/11):** giá trị `SCONFIG` (không có khóa "thừa"); báo cáo "thừa" chỉ là 1 báo cáo bị tắt (`STATUS=0`); `Tính lương bằng máy` có trong DB nhưng không có menu; `DMATHANG.LOAIDINHLUONG` không tồn tại (nên bán lẻ clone trừ vật tư theo `DLOAIMATHANGID='1'`).
- **Bán lẻ được giữ lại** (Chiến, 19/11), phù hợp mô hình tự phục vụ; hai việc cần chốt khi làm: có thêm in phiếu/số thứ tự cho pha chế không, và định dạng số hóa đơn bán lẻ (`GenSoHoaDon` trong `No1Lib`, cần test app gốc).

## 6. Thứ tự làm việc

1. Viết lại spec **Danh mục khách hàng theo schema POS TAP** — số liệu đã sửa theo DB thật trong `docs/specs/danh-muc-khach-hang.md` (15 trường, dialog 466×383); còn câu hỏi A1–A3, Q2–Q12 chờ Chiến. Bảng E1–E20 ở `deep/khach-hang.md` chỉ tham khảo (một phần dựa DEMO 26 cột).
2. Làm theo thứ tự menu gốc: Hoạt động (danh mục trước, luồng tiền/giá/kho sau) → Khách hàng → Kho → Công nợ → Quỹ → Nhân sự → Báo cáo.
3. Chiến chốt các nghi vấn giống bug (giữ hay sửa) theo từng nhóm — danh sách trong mục (e) mỗi file `deep/*.md` + §12 của `quan-tri.md`.
4. Chạy app gốc POS TAP trên DB mẫu để trả lời 7 câu chặn (mục 5) rồi mới viết code thanh toán/công nợ.
