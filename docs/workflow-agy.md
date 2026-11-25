# Workflow Antigravity CLI → Claude → `main`

Ngày 25/11/2026, thay cho bản Muse → Drive. Áp dụng cho repo `D:\Working\FnB`, trên máy Windows của Chiến.

**Chiến chốt 25/11:**
- Bên viết code là **Antigravity CLI** (`agy`, cài ở `%LOCALAPPDATA%\agy\bin\agy.exe`, bản 1.5.16), chạy trên chính máy này. Không dùng Drive.
- Claude điều phối: giao việc cho `agy`, kiểm, rồi **push code thẳng lên `main`**, không qua nhánh hay pull request.

Vì code lên `main` mà không ai khác xem, **Claude là người review** và các bước kiểm ở §4 là cổng chặn duy nhất. Code lỗi thì gỡ bằng `git revert` (§6).

## 1. Nguyên tắc

- **Người viết khác người kiểm:** `agy` viết code bằng model Gemini; Claude không viết phần nghiệp vụ, chỉ kiểm và sửa nhỏ (§4 bước 9).
- Chỉ làm chức năng có spec **ĐÃ DUYỆT**. Mỗi lần một chức năng.
- `agy` làm trong **worktree riêng** của chức năng; không đụng thư mục làm việc chính của Chiến, không đụng worktree khác.
- `agy` không commit, không push. Chỉ Claude commit và push.
- Push chỉ **fast-forward**: không force-push, không merge, không sửa lịch sử `main`.
- Mỗi chức năng có một issue nhãn `agy-patch` ghi kết quả từng vòng, kể cả khi dừng.

## 2. Vai trò

| Bên | Làm | Không làm |
|---|---|---|
| **Antigravity CLI** | Đọc spec, viết code và selftest trong worktree; sửa theo báo cáo lỗi của Claude | Commit, push; sửa ngoài worktree; chạy app lên DB thật; sửa spec, `CLAUDE.md`, `docs/` |
| **Claude** | Chuẩn bị worktree; giao việc cho `agy`; đọc diff; build; selftest; đối chiếu tiêu chí nghiệm thu; commit, push `main`; báo cáo; revert khi được yêu cầu | Viết phần nghiệp vụ thay `agy`; sửa ngoài spec để cho qua |
| **Chiến** | Duyệt spec; ra lệnh "làm <chức năng>"; bấm thử trên `main`; yêu cầu revert khi cần; cập nhật `QUEUE.md` | — |

## 3. Thư mục

| Đường dẫn | Dùng cho |
|---|---|
| `D:\Working\fnb-agy-work\<feature>\` | Worktree của chức năng |
| `D:\Working\fnb-agy-work\_tmp\<feature>\` | Prompt, log `agy`, DB chép, file build, kết quả selftest |
| `D:\Working\fnb-agy-work\state.txt` | Mỗi dòng: `<feature> r<N> <ok|fail> <commit nếu có> <thời điểm> <mã hội thoại agy>` |

## 4. Một vòng làm chức năng (Claude)

Bắt đầu khi Chiến bảo "làm <chức năng>". **Dừng ở bước nào cũng làm bước 12 (báo cáo)** và ghi `fail` vào `state.txt`.

1. **Kiểm spec.** `git fetch origin`. Spec trên `origin/main` phải có trạng thái **ĐÃ DUYỆT**. Không → dừng, báo Chiến.
2. **Chuẩn bị worktree.** `git worktree add --detach D:\Working\fnb-agy-work\<feature> origin/main`. Chép vào worktree hai thứ không có trong git: `Kazuko_FnB/lib/` và `POS TAP/Data/TEMPLATE.FDB`. Ghi lại base (SHA của `origin/main`).
3. **Viết prompt** vào `_tmp\<feature>\prompt-r<N>.md` (mẫu ở §5).
4. **Giao việc.** Chạy trong worktree:
   ```powershell
   & "$env:LOCALAPPDATA\agy\bin\agy.exe" -p "Doc file <_tmp>\prompt-r1.md (UTF-8) bang cong cu doc file va lam dung theo noi dung file do." --add-dir <_tmp> --settings D:\Working\fnb-agy-work\_tmp\agy-settings.json --mode accept-edits --sandbox --log-file <_tmp>\agy-r1.log --print-timeout 60m --output-format json
   ```
   - Không truyền nguyên nội dung prompt vào `-p`: PowerShell 5.1 cắt sai đối số khi chuỗi có dấu `"`. Chạy nền vì có thể lâu (vòng thử đầu: khoảng 10 phút).
   - **Quyền lệnh:** chế độ `-p` không hỏi được người dùng nên **từ chối mọi lệnh shell chưa cho phép, và dừng cả lượt** ở lệnh đầu tiên bị từ chối. `agy-settings.json` (chỉ dùng cho phiên, không ghi vào cấu hình chung) cho phép hẹp: `read_file(*)`; `Get-Content`, `Get-ChildItem`, `Select-String`, `Test-Path`, `git status|diff|log|show|ls-files` và MSBuild, **không** có `| ; & < >` và dấu backtick; chặn `git` ghi (commit, push, checkout, reset…), mọi lệnh nhắc `DATA.fdb` hay `POS TAP`, xóa file, tải mạng, `Start-Process`. Prompt phải dặn agy dùng công cụ đọc/tìm của nó và mỗi lần một lệnh đơn.
   - Lượt dừng vì lệnh bị từ chối: hỏi agy lệnh đó là gì (`--conversation <mã> --mode plan`), rồi cho nó làm tiếp trong cùng hội thoại với hướng dẫn tránh lệnh đó. Không chuyển sang `--dangerously-skip-permissions`.
   - Kết quả JSON có `denied_actions` và `conversation_id`; ghi cả hai vào báo cáo.
   Model: mặc định của `agy` trừ khi Chiến chỉ định (`--model`); ghi model vào báo cáo. Không dùng `--dangerously-skip-permissions`. Vòng sửa dùng `--continue` hoặc `--conversation <mã>` để `agy` giữ ngữ cảnh.
5. **Kiểm phạm vi.** `git -C D:\Working\FnB status` không được đổi so với trước bước 4; `git -C <worktree> status` chỉ có file trong phạm vi. `agy` đã commit hoặc sửa ngoài worktree → dừng, báo Chiến.
6. **Đọc diff** (`git -C <worktree> diff` + file mới). **Dừng chờ Chiến** nếu diff:
   - đụng `.slnx`, `.gitignore`, `.github/`, `.claude/`, `CLAUDE.md`, `docs/`, `Kazuko_FnB/lib/`, file `.ps1` `.bat` `.cmd`, file nhị phân;
   - sửa `.csproj` ngoài việc thêm/bớt `<Compile Include>` (ví dụ thêm `Reference`, `HintPath`, `Target`, `Exec`, gói NuGet);
   - có code chạy tiến trình, gọi mạng, xóa file ngoài thư mục tạm, đổi cấu trúc DB (DDL), ghi vào `POS TAP/`;
   - lệch spec ở phần **tiền, giá, kho, công nợ**.
   Lỗi khác (ghép chuỗi SQL, `double` cho tiền, màu cứng, SQL trong View, lệch spec ngoài phần tiền…) → đưa vào báo cáo lỗi cho `agy` ở bước 8.
7. **Build và selftest.**
   - `D:\VisualStudioInstallation\MSBuild\Current\Bin\MSBuild.exe Kazuko_FnB\Kazuko_FnB.slnx -p:OutDir=<_tmp>\build\` (cấu hình mặc định). Đạt: 0 lỗi, không thêm cảnh báo so với base (MSB3270 có sẵn).
   - Chép mới `DATA.FDB` vào `_tmp` rồi `Kazuko_FnB.exe --selftest <bản chép> <kết quả.txt>`; chạy cả bản build của base (lưu theo SHA, tái dùng). Đạt: không có ca lỗi mới, ca mới của chức năng đều qua. Không bao giờ chạy lên DB thật.
8. **Trả lỗi cho `agy`, tối đa 2 vòng.** Còn lỗi ở bước 6–7 → viết `prompt-r<N+1>.md` liệt kê từng lỗi (file, dòng, lý do, trích spec), chạy lại bước 4 với `--conversation`, rồi bước 5–7. Hết 2 vòng vẫn lỗi → dừng.
9. **Claude sửa nhỏ (nếu còn), tối đa 2 lần.** Chỉ lỗi biên dịch hoặc lỗi mà spec chỉ rõ cách đúng, không đụng logic tiền. Mỗi lần một commit riêng `fix(claude): …`.
10. **Đối chiếu tiêu chí nghiệm thu** của spec, lập bảng §7.
11. **Commit và push `main`.**
    - Commit code của `agy`: `feat: <feature> (Antigravity <model>, base <sha ngắn>)`; commit `fix(claude)` nếu có đi sau.
    - `git fetch origin`. `origin/main` đã đổi kể từ bước 2: đổi chỉ trong `docs/` thì `git rebase origin/main` trong worktree rồi **build và selftest lại**; đổi code → dừng, giao lại từ bước 2.
    - `git push origin HEAD:refs/heads/main` (không `--force`).
12. **Báo cáo** lên issue `agy/<feature>` (nhãn `agy-patch`; chưa có thì tạo), nội dung §7. Báo Chiến trong phiên.
13. **Dọn dẹp.** Ghi `state.txt`. Đạt → `git worktree remove` worktree. Dừng → giữ worktree để xem lại.

## 5. Mẫu prompt cho `agy` (vòng 1)

```
Bạn viết code cho repo KAZUKO F&B trong thư mục hiện tại (một git worktree).
Đọc trước: CLAUDE.md, docs/kinh-nghiem.md, docs/specs/<spec>.md và các tài liệu spec đó trỏ tới.
Việc: implement chức năng <tên> đúng spec, gồm selftest cho phần logic (Kazuko_FnB.UI/Diagnostics/SelfTest.cs).

Bắt buộc:
- Chỉ sửa file trong thư mục này, trong Kazuko_FnB/. Không sửa docs/, CLAUDE.md, .slnx, lib/, .gitignore.
- Không git commit, không git push, không tạo nhánh.
- Không đổi cấu trúc DB, không mở DATA.FDB thật, không chạy app lên DB thật.
- Không thêm thư viện, gói NuGet, script; .csproj chỉ thêm dòng <Compile Include> cho file mới.
- Theo quy ước ở §8 của docs/workflow-agy.md.
- Chỗ spec không rõ: KHÔNG đoán, ghi vào file AGY-NOTES.md ở gốc worktree.
Xong thì tóm tắt: file đã sửa, chỗ chưa làm, chỗ không chắc.
```

`AGY-NOTES.md` không được commit; Claude chép nội dung vào báo cáo rồi xóa.

## 6. Sau khi lên `main`

1. Chiến `git pull` ở thư mục làm việc chính, bấm thử trong app trên bản sao CSDL.
2. Lỗi nặng (sai tiền, hỏng dữ liệu, không build được) → Chiến yêu cầu, Claude `git revert` các commit của chức năng (không xóa lịch sử), build lại, push, báo trên issue. Làm lại từ §4 bước 2.
3. Đạt → Chiến cập nhật trạng thái spec và `QUEUE.md`, đóng issue.
4. Chức năng chưa được Chiến bấm thử thì không dùng ở quán, dù đã nằm trên `main`.

## 7. Nội dung báo cáo

```
feature / vòng / base / model agy / mã hội thoại agy / commit trên main (nếu đã push)
Kết quả: ĐÃ PUSH | DỪNG ở bước <n>: <lý do>
Số vòng agy sửa: <n>; lỗi từng vòng: <…>
Build: <số lỗi>, cảnh báo mới: <…>
Selftest: base <x lỗi> → chức năng <y lỗi>; ca lỗi mới: <…>
Claude tự sửa: <commit + lý do, hoặc không>
Ghi chú của agy (AGY-NOTES.md): <…>
Tiêu chí nghiệm thu (theo spec):
| Tiêu chí | Đạt / Không đạt / Chưa kiểm | Bằng chứng |
Việc còn cho Chiến: bấm thử <luồng>, trên bản sao DB
```

Tiêu chí cần thao tác tay hoặc nhìn màn ghi **Chưa kiểm**, không ghi Đạt.

## 8. Quy ước nghiệp vụ đã chốt (không hỏi lại)

Xóa cứng (không thùng rác) · tiền `decimal`, `LamTron` bội 1000 AwayFromZero · F&B thuần COFFEE + POS, không tính tiền giờ · không dùng cột `LOAIDINHLUONG` · khóa chính GUID, `STATUS = 30` · SQL tham số hóa, không ghép chuỗi · không SQL trong View · màu, cỡ chữ lấy từ design token và `ThemeManager` · không đổi cấu trúc DB · UI và luồng như app gốc, chỗ khác ghi rõ để Chiến quyết · quỹ theo [`specs/quy-tien.md`](specs/quy-tien.md) · spec và code commit riêng.

## 9. Bài test đầu tiên

- **Đã chạy 25/11:** Lưu vết hoạt động, vòng 1 đạt, commit `3bec043`, báo cáo ở issue #6. Agy chỉ sửa trong worktree, không commit; build 0 lỗi; selftest không ca lỗi mới, 28 ca mới đạt. Mốc selftest với bản chép `POS TAP/Database/DATA.fdb`: 7 ca lỗi cũ.
- Chưa thử: vòng sửa (gửi lỗi lại cho agy), `git revert`.
- Chức năng: **một**, spec đã duyệt. Gợi ý: Lưu vết hoạt động (spec còn NHÁP, cần Chiến duyệt).
- Trước khi chạy: tạo nhãn `agy-patch`; chạy selftest của `main` một lần để có mốc (đang ghi nhận 2 ca lỗi cũ ở phần xóa nhóm người dùng).
- **Thành công** = commit lên `main` fast-forward, báo cáo đủ §7, thư mục chính của Chiến không bị đụng, Chiến bấm thử. Thử thêm một lần `git revert` để chắc đường gỡ lỗi chạy được.
- Patch gộp 7 chức năng Muse đã làm trên `02ba7a7`: không dùng trong workflow này.
