# Workflow Antigravity CLI → Claude → `main`

Ngày 25/11/2026, thay cho bản Muse → Drive. Áp dụng cho repo `D:\Working\FnB`, trên máy Windows của Chiến.

**Chiến chốt 25/11:**
- Bên viết code là **Antigravity CLI** (`agy`, cài ở `%LOCALAPPDATA%\agy\bin\agy.exe`, bản 1.5.16), chạy trên chính máy này. Không dùng Drive.
- Claude điều phối: giao việc cho `agy`, kiểm, rồi **push code thẳng lên `main`**, không qua nhánh hay pull request.

Vì code lên `main` mà không ai khác xem, **Claude là người review** và các bước kiểm ở §4 là cổng chặn duy nhất. Code lỗi thì gỡ bằng `git revert` (§6). Các bước máy làm được đã viết thành script trong [`tools/agy/`](../tools/agy/).

## 1. Nguyên tắc

- **Người viết khác người kiểm:** `agy` viết code bằng model Gemini; Claude không viết phần nghiệp vụ, chỉ kiểm và sửa nhỏ (§4 bước 9).
- Chỉ làm chức năng có spec **ĐÃ DUYỆT**. Mỗi lần một chức năng.
- `agy` làm trong **worktree riêng** của chức năng và chỉ thấy worktree đó cùng thư mục chứa prompt (§4.1).
- `agy` không commit, không push. Chỉ Claude commit và push.
- Push chỉ **fast-forward**: không force-push, không merge, không sửa lịch sử `main`.
- **Claude đọc hết diff, kể cả selftest**, trước khi push. Selftest do agy viết qua không chứng minh được gì nếu ca test không kiểm đúng điều tiêu chí nói.
- Mỗi chức năng có một issue nhãn `agy-patch` ghi kết quả từng vòng, kể cả khi dừng.

## 2. Vai trò

| Bên | Làm | Không làm |
|---|---|---|
| **Antigravity CLI** | Đọc spec, viết code và selftest trong worktree; sửa theo báo cáo lỗi của Claude | Commit, push; đọc/sửa ngoài worktree; chạy app; sửa spec, `CLAUDE.md`, `docs/`, `tools/` |
| **Claude** | Chạy script; viết prompt; đọc hết diff; chụp màn có dữ liệu; đối chiếu tiêu chí nghiệm thu; push `main`; báo cáo; revert khi được yêu cầu | Viết phần nghiệp vụ thay `agy`; sửa ngoài spec để cho qua; sửa thư mục chính trong lúc agy đang chạy |
| **Chiến** | Duyệt spec; ra lệnh "làm <chức năng>"; bấm thử trên `main`; yêu cầu revert khi cần; cập nhật `QUEUE.md` | — |

## 3. Script và thư mục

| Script (`tools/agy/`) | Bước | Làm |
|---|---|---|
| `New-AgyFeature.ps1 -Feature -Spec` | 1–2 | Kiểm spec ĐÃ DUYỆT trên `origin/main`; tạo worktree; chép `lib/` và `TEMPLATE.FDB`; build + selftest mốc (lưu theo SHA ở `_cache`); sinh file quyền và prompt mẫu |
| `Invoke-Agy.ps1 -Feature -Round [-Conversation -Message -Mode plan]` | 4–5 | Chạy agy, lưu kết quả JSON, rồi gọi `Test-AgyScope.ps1` |
| `Test-AgyScope.ps1` | 5–6 | Lỗi chặn: thư mục chính đổi, worktree có commit, file cấm, `.csproj` ngoài `<Compile Include>`, cấu hình chung của agy bị sửa, file ngoài worktree bị sửa. Cảnh báo: file dùng chung, mẫu code đáng ngờ, file `.log`/`.tmp` bên ngoài đổi |
| `Test-AgyBuild.ps1 -Feature -Round` | 7 | Build; so cảnh báo với mốc; selftest trên bản chép DB; so ca lỗi với mốc; bắt buộc có ca mới; lưu dấu vân tay code đã kiểm |
| `Publish-AgyFeature.ps1 -Feature -Round -Title -ReviewedFullDiff` | 11 | Từ chối nếu code đổi sau lần kiểm, `main` đổi code, thiếu cờ đã đọc diff. Commit có dòng ghi công, push fast-forward, ghi `state.txt` |
| `Remove-AgyFeature.ps1 -Feature [-Discard]` | 13 | Xóa worktree, giữ `_tmp` |

| Đường dẫn | Dùng cho |
|---|---|
| `D:\Working\fnb-agy-work\<feature>\` | Worktree của chức năng |
| `D:\Working\fnb-agy-work\_tmp\<feature>\agy-in\` | **Chỉ** prompt (và bản build agy tự chạy). Agy đọc và ghi được |
| `D:\Working\fnb-agy-work\_tmp\<feature>\` | Còn lại: file quyền, log từng lượt, build, selftest, báo cáo. Agy không thấy |
| `D:\Working\fnb-agy-work\_cache\<sha>\` | Build và selftest mốc theo commit |
| `D:\Working\fnb-agy-work\state.txt` | Mỗi dòng: `<feature> r<N> <ok|fail> <commit> <thời điểm> <mã hội thoại agy>` |

## 4. Một vòng làm chức năng (Claude)

Bắt đầu khi Chiến bảo "làm <chức năng>". **Dừng ở bước nào cũng làm bước 12 (báo cáo)**. Trong lúc agy chạy (bước 4), Claude **không sửa thư mục chính**: `Test-AgyScope.ps1` coi mọi thay đổi ở đó là lỗi chặn.

1–2. `New-AgyFeature.ps1`. Mốc selftest hiện tại (bản chép `POS TAP/Database/DATA.fdb`): 7 ca lỗi cũ, không tính là lỗi của chức năng.
3. **Viết prompt:** điền phần `<CLAUDE GHI CHI TIẾT VIỆC Ở ĐÂY>` trong `agy-in\prompt-r<N>.md` (mẫu `tools/agy/prompt-template.md`). Ghi rõ tiêu chí nào cần ca test kiểm điều gì.
4. **Giao việc:** chạy nền `Invoke-Agy.ps1` (vòng 1 khoảng 7–10 phút).
   - Chế độ `-p` không hỏi được người dùng: mọi việc ngoài quyền bị **từ chối và dừng cả lượt**. Script báo "lượt bị dừng"; hỏi agy lệnh gì (`-Mode plan -Conversation <mã> -Message ...`), sửa quy tắc nếu lệnh vô hại, rồi cho làm tiếp trong cùng hội thoại. Không dùng `--dangerously-skip-permissions`.
   - Lời nhắn `-Message` không được có dấu `"` (PowerShell 5.1 cắt sai đối số).
5. **Kiểm phạm vi:** `Test-AgyScope.ps1` chạy tự động sau bước 4.
6. **Đọc hết diff**, kể cả selftest, và lập bảng *tiêu chí → ca test → ca đó kiểm gì*. **Dừng chờ Chiến** nếu lệch spec ở phần **tiền, giá, kho, công nợ**. Lỗi khác (ghép chuỗi SQL, `double` cho tiền, màu cứng, SQL trong View, ca test kiểm lỏng…) → đưa vào prompt vòng sau.
7. **Build và selftest:** `Test-AgyBuild.ps1`. **Chụp màn có dữ liệu mẫu** (`--render-screen`, dữ liệu chèn vào bản chép DB) và xem ảnh: cắt chữ, màu, bố cục. Ảnh không có dữ liệu không đủ.
8. **Trả lỗi cho agy, tối đa 2 vòng:** `prompt-r<N+1>.md` liệt kê từng lỗi (file, lý do, trích spec), chạy lại bước 4 (cùng hoặc khác hội thoại), rồi 5–7. Hết 2 vòng vẫn lỗi → dừng.
9. **Claude sửa nhỏ (nếu còn), tối đa 2 lần:** chỉ lỗi biên dịch hoặc lỗi spec chỉ rõ cách đúng, không đụng logic tiền; commit riêng `fix(claude): …`.
10. **Đối chiếu tiêu chí nghiệm thu** (§7).
11. **Push:** `Publish-AgyFeature.ps1 ... -ReviewedFullDiff`. `main` chỉ đổi trong `docs/` thì script chuyển worktree lên `main` mới và bắt chạy lại `Test-AgyBuild.ps1`; đổi code thì dừng, giao lại từ bước 1.
12. **Báo cáo** lên issue `agy/<feature>` (nhãn `agy-patch`), nội dung §7.
13. **Dọn dẹp:** `Remove-AgyFeature.ps1`.

### 4.1 Quyền của agy (file `agy-settings.json` do `New-AgyFeature.ps1` sinh, chỉ dùng cho phiên)

- **Đọc/ghi:** mặc định agy chỉ tự đọc/ghi trong thư mục làm việc (worktree) và thư mục `--add-dir` (`agy-in`); ngoài đó bị từ chối. **Không** dùng `read_file(*)` (lần đầu dùng quy tắc này đã mở quyền đọc cả máy).
- **Chặn trong worktree:** đọc `POS TAP/`; ghi `docs/`, `CLAUDE.md`, `.gitignore`, `.git`, `Kazuko_FnB/lib/`, `.slnx`, `tools/`. Chặn cả repo chính (`D:/Working/FnB/`).
- **Cách viết đường dẫn:** phải có ký tự ổ đĩa (`D:/Working/...`) hoặc tương đối theo worktree (`POS TAP/`). Dạng bỏ ổ đĩa như tài liệu Antigravity ghi **không có tác dụng** (đã thử 25/11).
- **Lệnh:** chỉ `Get-Content`, `Get-ChildItem`, `Select-String`, `Test-Path`, `dir`, `ls`, `pwd`, `git status|diff|log|show|ls-files`, MSBuild; không `| ; & < >` và dấu backtick. Chặn git ghi, lệnh nhắc `.fdb`/`POS TAP`, xóa/chép/ghi file, tải mạng, `Start-Process`.
- **Đã thử 25/11:** đọc ngoài worktree, đọc repo chính, ghi ra ngoài, lệnh nhắc `DATA.fdb` đều bị chặn; đọc và ghi `agy-in` được.
- `Test-AgyScope.ps1` còn so băm file cấu hình chung `~/.gemini/antigravity-cli/settings.json` trước/sau, để bắt agy tự nới quyền.

## 5. Prompt

Mẫu ở `tools/agy/prompt-template.md`. Phần cố định: chỉ đọc trong thư mục hiện tại, mỗi lần một lệnh đơn, tài liệu phải đọc, các điều bắt buộc, file dùng chung phải liệt kê lý do, chỗ spec không rõ ghi `AGY-NOTES.md` (không commit; `Publish` chuyển ra `_tmp`). Selftest phải kiểm **điều tiêu chí nói** (ví dụ màu thật của ô, không chỉ hàm chọn màu).

## 6. Sau khi lên `main`

1. Chiến `git pull` ở thư mục làm việc chính, bấm thử trong app trên bản sao CSDL.
2. Lỗi nặng (sai tiền, hỏng dữ liệu, không build được) → Chiến yêu cầu, Claude `git revert` các commit của chức năng **theo thứ tự mới trước** (không xóa lịch sử), build lại, push, báo trên issue. Đã thử 25/11 trên bản clone: revert 2 commit của Lưu vết hoạt động đưa `Kazuko_FnB/` về đúng như trước chức năng, không xung đột.
3. Đạt → Chiến cập nhật trạng thái spec và `QUEUE.md`, đóng issue.
4. Chức năng chưa được Chiến bấm thử thì không dùng ở quán, dù đã nằm trên `main`.

## 7. Nội dung báo cáo

```
feature / vòng / base / model agy / mã hội thoại agy / commit trên main (nếu đã push)
Kết quả: ĐÃ PUSH | DỪNG ở bước <n>: <lý do>
Phạm vi: file đã sửa; THAY ĐỔI DÙNG CHUNG (theme, SelfTest.cs, ScreenFactory, Controls, DbHelper…) kèm lý do
Build: <số lỗi>, cảnh báo mới: <…>
Selftest: mốc <…> → <…>; ca mới <n>; ca lỗi mới <…>
Claude tự sửa: <commit + lý do, hoặc không>
Ghi chú của agy (AGY-NOTES.md): <…>
Tiêu chí nghiệm thu:
| Tiêu chí | Đạt / Đạt một phần / Không đạt / Chưa kiểm | Ca test kiểm thật sự gì, hoặc ảnh |
Điểm yếu còn lại của test; đánh đổi cần Chiến xem
Việc còn cho Chiến: bấm thử <luồng>, trên bản sao DB
```

Tiêu chí chỉ dựa vào tên ca test, chưa đọc ca đó, không được ghi Đạt. Tiêu chí cần nhìn màn mà chưa có ảnh có dữ liệu ghi **Chưa kiểm**.

## 8. Quy ước nghiệp vụ đã chốt (không hỏi lại)

Xóa cứng (không thùng rác) · tiền `decimal`, `LamTron` bội 1000 AwayFromZero · F&B thuần COFFEE + POS, không tính tiền giờ · không dùng cột `LOAIDINHLUONG` · khóa chính GUID, `STATUS = 30` · SQL tham số hóa, không ghép chuỗi · không SQL trong View · màu, cỡ chữ lấy từ design token và `ThemeManager` · không đổi cấu trúc DB · UI và luồng như app gốc, chỗ khác ghi rõ để Chiến quyết · quỹ theo [`specs/quy-tien.md`](specs/quy-tien.md) · spec và code commit riêng.

## 9. Lịch sử chạy

- **25/11, Lưu vết hoạt động** (issue #6):
  - Vòng 1 (`3bec043`, chạy tay): đạt; đọc lại sau đó thấy báo cáo ghi "Đạt" quá mức và một lỗi cắt chữ dòng đậm.
  - Vòng 2 (`f9dd6bc`, chạy bằng script, quyền thu hẹp): sửa lỗi cắt chữ, thêm ca test màu thật, `Track` tĩnh, ngày mặc định.
  - Vướng: quy tắc lệnh thiếu dạng không tham số; agy nhầm thư mục prompt là repo (lời gọi nay ghi rõ đường dẫn); một app khác trong `D:\Working\FnB` ghi file log (nay chỉ cảnh báo).
- Chưa thử: Claude tự sửa (bước 9); `main` đổi giữa chừng; 125% DPI.
