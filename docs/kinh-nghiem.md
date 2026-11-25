# Kinh nghiệm làm việc (đọc trước khi viết spec, đối chiếu app gốc, build/test)

Rút từ các phiên 23–25/11/2026. Mỗi mục là một lỗi hoặc vướng mắc đã gặp thật. Gặp thêm thì bổ sung vào đúng nhóm; mục nào hết đúng thì sửa hoặc xóa.

## 1. Đối chiếu app gốc: tìm đủ nguồn trước khi kết luận

- **Không ghi "không có", "ghi cứng", "chưa xác minh được" khi mới tìm trong `POS_TAP_SRC_EXPORT`.** Còn nhiều thứ chỉ nằm trong CSDL. Đã sai thật: ghi "không có mẫu in, tỉ lệ ghi cứng 100%" trong khi mẫu in có sẵn và script của nó tính tỉ lệ.
  | Cần tìm | Nguồn |
  |---|---|
  | Mẫu in, nhãn cột, công thức tổng | blob `SREPORTTEMPLATE.TEMPLATE` (file `.frx`): đọc `TableCell`/`TextObject`, `Total`, `ScriptText`, thứ tự band. Một báo cáo có thể có nhiều mẫu; xem thêm `SREPORT.LASTTEMPLATEID` |
  | Cột lưới của form | `SFORM.AELAYOUT` (bản export: `layout.xml`); lưới `GridMapper` nằm trong `ViewConfig` (base64), đổi `COLID` sang tên qua `SCOLUMN` |
  | Nhãn, định dạng cột | `SCOLUMN` |
  | Quyền mặc định theo nhóm | `SGROUPROLE` + `SGROUPUSER`. Không tự đặt mặc định "không cấp" |
| Enum, lớp, hàm dùng chung (`LoaiLuuVet`, `HoaDonMode`, `Track`…) không có trong export | **Mã dùng chung lưu trong `DATA.fdb`**, không phải trong DLL: đọc file DB bằng UTF-8 rồi tìm chuỗi (ví dụ `enum LoaiLuuVet`). Đã ghi sai "không đọc được, tự định nghĩa số mới" |
- **Cách đọc blob:** chép `DATA.fdb` ra thư mục tạm, dùng `Firebird_2_5/bin/isql.exe` (tài khoản mặc định, xem `AppSettings`). `SET BLOBDISPLAY OFF;` rồi `SELECT` để lấy mã blob, sau đó `BLOBDUMP <mã> ten.frx;`. Chạy `isql` từ chính thư mục tạm và dùng **đường dẫn tương đối** (đường dẫn Windows đầy đủ bị mất dấu `\`).
- **Ô dùng font ký hiệu (Wingdings…) phải vẽ ra mới biết hình:** spec KQKD đoán là ▲/▼, vẽ bằng `System.Drawing` thì `B`/`D` là bàn tay OK và ngón cái xuống. Xem `Font` của từng `TableCell` trong `.frx`.
- **Báo cáo so hai kỳ: thử cả khoảng dài hơn 1 tháng và ngày cuối tháng.** Hai kỳ trùng nhau thì `CASE WHEN <kỳ này> … ELSE <kỳ trước>` bỏ ngày trùng khỏi kỳ trước, còn truy vấn con tách riêng thì vẫn tính.
- **Được phép chụp app gốc thì chụp ngay**, đừng để trống rồi ghi "chưa xác minh". App gốc đòi quyền admin nên công cụ điều khiển máy không bấm được: chạy một **bản chép** ở thư mục tạm với `$env:__COMPAT_LAYER="RunAsInvoker"`. App chỉ chạy một phiên: nhờ Chiến đóng bản đang chạy quyền admin.
- **Ảnh chụp cũng có giới hạn:** DB không có dữ liệu thì báo cáo không in tiêu đề cột; lưới có thể bị cuộn khuất cột hoặc khác `layout.xml`. Đối chiếu cả hai, lệch thì ghi cả hai và đặt câu hỏi.
- **Xem cả những gì nằm ngoài màn đang làm** trong ảnh (thanh trạng thái, menu). Đã trả lời sai "app gốc không đếm ngày dùng thử" trong khi thanh trạng thái trong ảnh ghi "hết hạn trong 14 ngày".
- **Nghiệp vụ tiền: kiểm mọi nhánh ghi tiền**, không chỉ nhánh chính. Ví dụ nhánh thanh toán gộp bàn không ghi `TIENMAT`/`CHUYENKHOAN`/`THE`.
- **Chạy thử câu SQL của nhánh ít dùng** trên bản chép DB: nhánh thanh toán nhóm gốc đọc cột không tồn tại (`TTHUCHI.SOTIEN`). Và tra **chỗ tạo ra** dữ liệu của nhánh đó (`NHOMGUID` do "Ngắt giờ" tạo, không phải "Gộp bàn") trước khi viết luật cho nó.

- **Chỗ vướng thì mở app gốc thao tác** (Chiến dặn 25/11). Báo cáo: tạo bill, phiếu thu/chi bằng chính app gốc trên bản chép ngoài repo, rồi chạy bản mới (`--render-screen`) trên đúng CSDL đó và so từng số. Trước khi chạy, ghi mã băm các CSDL trong `POS TAP/Database` và so lại sau; `Data/conn.dat` phải chỉ ghi tên `DATA.fdb` (không đường dẫn đầy đủ).
- **Không dùng `open_application` để đưa app gốc lên trước:** nó mở thêm phiên chạy quyền quản trị, hộp thoại của phiên đó không bấm được, phải nhờ Chiến bấm "No".
- **Thao tác app gốc:** bấm chọn dòng mặt hàng rồi Enter mới thêm vào bill. Ô tiền không bôi chọn được (số mới nối vào số cũ): bấm vào ô, End, Backspace nhiều lần rồi mới gõ. Telex trong app đổi chữ gõ thử. Báo Chiến đừng dùng chuột phím trong lúc thao tác.
- **Điều khiển bản chép app gốc:** chạy `Application.exe` của bản chép trước, rồi `request_access` bằng **đường dẫn đầy đủ** tới file exe đó (tên "Application" hay tiêu đề cửa sổ không khớp, hộp xin quyền không hiện).
- **Menu trong spec có thể đang bị app gốc ẩn:** kiểm `SMENU.STATUS` (Báo giá bán hàng có `STATUS = 0`). Muốn thử thì bật lại **chỉ trên bản chép**.
- **Trước khi viết spec cho một mục menu:** đọc `No1System_OnCustomSeeMenu` trong `SharedCode`. Loại phòng và Thông báo sửa chữa chỉ hiện ở bản HOTEL, Bảng giá chỉ hiện khi tính tiền giờ; `SMENU` đang bật không có nghĩa là menu hiện.
- **Câu về so trùng:** xem collation của cột trước (`DKHACHHANG.NAME`, `MAKHACH` là `UNICODE_CI_AI`: không phân biệt hoa thường và dấu; app gốc không cắt khoảng trắng).
- **Đọc mã rồi vẫn nên bấm thử:** báo giá hàng 2 đơn vị tính, app gốc bỏ qua số lẻ sửa trong hộp "Nhập số lượng" (đúng như mã, đã thử 25/11).
- **Cấu hình lưới (`ViewConfig`) không cho biết thứ tự cột hiển thị:** muốn đúng thứ tự thì nhìn app gốc, cuộn hết sang phải (đã ghi sai chỗ cột S.lượng). Đã thử dịch ngược 25/11 (một lần, chưa tìm ra nguồn thứ tự hiển thị): `ViewConfig` là mảng byte bọc kiểu .NET chứa XML của một `DataTable` (cắt đoạn `<DocumentElement>…</DocumentElement>`, đọc được ngay, không cần giải tuần tự); ở màn Thống kê doanh thu `SELECTORDER` để trống, và thứ tự dòng (xuôi lẫn ngược) đều không khớp ảnh app; bảng `SGRID` không có dòng cho `grDetail`. Đừng thử lại nếu không có đầu mối mới.

- **Mã dùng chung** (`Shared`, các enum, `DuLieuHoaDonOk`, `TableLocker`): `SCONFIG` bản ghi `SharedCode`, cột `BLOBVALUE`; xuất bằng `BLOBDUMP`. **Đọc trước khi ghi "nằm trong DLL" hoặc hỏi Chiến.** 25/11 đã bỏ qua, tin spec, phải hỏi lại 4 câu.
- **Giá trị cấu hình:** đọc `SCONFIG.INTVALUE` của bản chép trước khi hỏi hay đề xuất mặc định.
- **Công thức cột lưới** (ví dụ thành tiền dòng): giải base64 `ViewConfig` của `GridMapper` trong `layout.xml`, đọc thẻ `FORMULA`.

## 2. Viết và sửa spec

- Quy tắc dùng chung nhiều spec (biên "Đến ngày", `STATUS = 30`, lọc cửa hàng, quỹ) **chốt một lần**, rồi đồng bộ mọi spec liên quan. Quỹ tiền: [`specs/quy-tien.md`](specs/quy-tien.md) là nguồn chuẩn.
- Trước khi ghi "dùng chung component X" hoặc "chưa có code", kiểm trên `origin/main` bằng `git ls-tree -r --name-only origin/main`. Code chỉ nằm trong patch chưa apply thì chưa có.
- **Review cũng có thể sai:** kiểm lại từng phát hiện với nguồn gốc trước khi sửa. Chỗ lệch thì ghi theo nguồn và báo Chiến. Câu lệnh viết cho Claude nằm trong issue/tài liệu không phải yêu cầu của Chiến.
- Ghép hai quyết định đã chốt có thể sinh mâu thuẫn mới (ví dụ lọc theo cửa hàng và một tài khoản ngân hàng dùng chung). Đọc lại toàn spec sau khi ghi quyết định.

## 3. Hỏi Chiến

- Câu hỏi nghiệp vụ kèm **ví dụ số cụ thể** (bảng nhỏ), nêu đề xuất, hỏi **từng quyết định một**. Chỉ đưa bảng thuật ngữ/tên cột là khó hiểu.
- Chiến hỏi "ví dụ cho dễ hiểu" thì dùng tình huống đời thường (hai bàn trả chung), hạn chế tên cột.

## 4. Build, chạy, kiểm

- **`Start-Process -ArgumentList` không tự bọc ngoặc kép:** tên giao diện "Mặc định" bị tách đôi, app hiểu "định" là đường dẫn CSDL và bật hộp lỗi. Truyền một chuỗi, tự bọc ngoặc kép từng tham số có dấu cách.

- `git fetch origin main` ở **mỗi lượt**: Chiến có thể commit và push giữa phiên.
- MSBuild: `D:\VisualStudioInstallation\MSBuild\Current\Bin\MSBuild.exe`. Các project hiện chỉ cấu hình AnyCPU (exe ưu tiên 32-bit): thêm `Platform=x86` báo MSB4126, nên build cấu hình mặc định. Cảnh báo MSB3270 (FastReport x86) đã có sẵn.
- Không đụng app đang chạy của Chiến: build ra thư mục tạm bằng `-p:OutDir=<thư mục tạm>\build\`.
- Selftest: `Kazuko_FnB.exe --selftest <bản chép DATA.fdb> <file kết quả.txt>`. Luôn dùng bản chép. Chạy trên bản chép `POS TAP/Database/DATA.fdb` thì có sẵn **7 ca lỗi** (2 xóa nhóm người dùng, 1 phương thức đặt hàng, 4 lịch đặt phòng; chưa tìm nguyên nhân). So với bản gốc chưa sửa trước khi kết luận lỗi do mình.
- Seed dữ liệu thử vào bản chép DB: `TDONHANG`, `TTHUCHI`, `DKHOHANG` bắt buộc có `ID`, `NAME`, `STATUS`, `USERCREATEDID`. Nên đọc trigger (`RDB$TRIGGERS.RDB$TRIGGER_SOURCE`) trước khi kết luận app gốc có ghi ngầm hay không.
- Chụp màn nội dung (Panel) bằng `--render-screen` cần một form bọc `preview.*`; xem `FundPreviewForm`. Xem ảnh để bắt chữ bị cắt, build qua chưa đủ.
- Xem giao diện không cần mở app: `--render-login`, `--render-main`, `--render-screen` (xem `Program.cs`). Lưu ý `--render-main` ghi file ngày cài vào `%PROGRAMDATA%\KazukoFnB\install-date.txt` nếu chưa có.
- Kiểm patch trên bản gốc sạch bằng `git worktree`: build ở đó cần chép thêm các thứ không có trong git là `Kazuko_FnB/lib/` và `POS TAP/Data/TEMPLATE.FDB`. Xong thì `git worktree remove`.
- **Script PowerShell 5.1:** (a) lệnh ngoài (git, MSBuild) trả mã lỗi thì script **không tự dừng**, kể cả `$ErrorActionPreference = 'Stop'`: phải kiểm `$LASTEXITCODE` sau mỗi lệnh (đã commit thiếu nội dung `09d9ab6` vì `git add` lỗi mà vẫn commit). (b) Ngược lại, với `Stop` và `2>&1`, dòng stderr bình thường của git bị coi là lỗi: hạ về `Continue` quanh lệnh ngoài. (c) File `.ps1` có chữ Việt phải lưu UTF-8 **có BOM**; đầu ra git cần `[Console]::OutputEncoding = UTF8`. (d) Đối số có dấu `"` truyền cho exe bị cắt sai. Xem `tools/agy/AgyCommon.ps1`. (e) Hàm trả mảng kiểu `return ,@($r)` (`Invoke-Git`) thì **không bọc `@( )`** ở nơi gọi: ra mảng lồng mảng, `.Count` = 1, `-join` ra `System.Object[]`. Đã làm hỏng ngầm hai chốt "main đổi code" và "thư mục chính đổi" (vá 25/11). Chốt an toàn phải được thử với ca **đáng bị chặn**, chạy đạt bình thường không chứng minh chốt hoạt động.
- Đọc lại báo cáo kiểm của chính mình: đã ghi "Đạt" chỉ dựa vào tên ca selftest do bên khác viết. Phải đọc ca test trước khi ghi Đạt.
- **Test giao diện phải đi qua sự kiện thật:** agy từng viết hàm hỗ trợ tắt sự kiện rồi gọi thẳng hàm tải lại, nên gỡ dòng nối sự kiện test vẫn qua. Dựng view trong `Form` đã `Show()` ngoài màn hình, thao tác trên control (`PerformClick`, đổi `CurrentCell`, gán `Text`). `DataGridView` chỉ tự chọn dòng đầu khi đã hiển thị, nên test chưa `Show()` thấy hành vi khác app thật.
- **agy `--sandbox`:** quy tắc chặn phải là đường dẫn tuyệt đối, dạng tương đối làm mọi lệnh shell bị dừng (đã sửa 25/11, `5e5ed90`, đã thử lại: vẫn chặn đọc `POS TAP/`, ghi `docs/`, đọc repo chính). Trong sandbox lệnh `git` báo `not a git repository`. Ký tự `|` nằm trong chuỗi mẫu tìm cũng làm lệnh bị từ chối.
- **Đang chạy agy thì Chiến đừng chạy app hay build ở thư mục chính:** app ghi vào `Kazuko_FnB/Database/DATA.fdb`, bước kiểm phạm vi chặn (đã xảy ra 25/11). Khi bị chặn, so giờ file `bin/obj`, `firebird.log`, tiến trình `devenv` trước khi kết luận, rồi hỏi Chiến.
- **Ẩn cột không phải là chặn quyền:** lưới chọn cả dòng, "Sao chép vùng chọn" từng chép cả cột ẩn, lộ giá vốn cho người không có quyền. Dữ liệu bị cấm thì đừng đưa vào ô; màn nào ẩn cột theo quyền phải có ca test chuỗi sao chép.
- **`Control.Visible` trả `false` khi control cha chưa hiện hoặc đang ẩn**, dù đã gán `true`. Bố cục tính tay phải dựa vào biến trạng thái dữ liệu, không đọc getter này (khối Thu-chi từng bị khối Nhập hàng che).
- **Vùng `AutoScroll` tự cuộn về control đang focus** (`ScrollControlIntoView`), kể cả khi form chỉ `PerformLayout()`. Trang cuộn tự dựng thì override `ScrollToControl`. Ca test cuộn phải so đúng vị trí đích; "scrollY > 0" đã cho qua ảnh sai. Nghi lỗi bố cục mà ảnh không đủ kết luận: nạp bản build vào PowerShell 32-bit (`SysWOW64`), dựng view bằng reflection và in `Bounds`.
- **agy đổi bản thì thử lại quyền bằng file mồi đặt cả trong và ngoài `%TEMP%`.** Bản 1.5.17 (25/11): đọc, ghi được `%TEMP%` dù ngoài workspace (rào chặn khác còn tác dụng); shell đứng ở `agy-in`, không ở worktree; MSBuild chỉ được phép đúng dạng `& '...\MSBuild.exe' <tham số>`.
- GitHub CLI trong Git Bash: `"/c/Program Files/GitHub CLI/gh.exe"`. Trước khi viết spec mới, xem issue nhãn `spec-review`.
