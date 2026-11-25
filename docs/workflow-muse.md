# Workflow Muse → Drive → Claude → `main`

Bản viết lại ngày 25/11/2026 từ `workflow-muse-drive-cho-claude.md`. Áp dụng cho repo `D:\Working\FnB`. **Chưa có hiệu lực** cho tới khi ngoại lệ ở §9 được thêm vào `CLAUDE.md`.

**Chiến chốt 25/11:** Claude **push code thẳng lên `main`**, không qua nhánh hay pull request. Code lên `main` trước, review sau. Vì vậy các bước kiểm ở §5 là cổng chặn duy nhất trước `main`; code lỗi thì gỡ bằng `git revert` (§7).

## 1. Mục tiêu và nguyên tắc

- Chiến không phải tải file hay apply tay. Chiến xem review, bấm thử trên `main`, báo lỗi nếu có.
- Claude chỉ push lên `main` patch đã qua **mọi bước kiểm** ở §5. Push chỉ **fast-forward**: không force-push, không merge, không rebase lịch sử đã có.
- Claude làm trong **worktree riêng**, không đụng thư mục làm việc chính của Chiến (thường có file đang sửa dở).
- Mỗi vòng có **kết quả gửi lên GitHub**, kể cả khi dừng sớm, để Muse biết mà gửi tiếp.
- Mỗi patch là **một chức năng** có spec **ĐÃ DUYỆT**.

## 2. Vai trò

| Bên | Làm | Không làm |
|---|---|---|
| **Muse** (cloud) | Code theo spec đã duyệt; tự kiểm `git apply --check` trên đúng base; đẩy patch + manifest lên Drive; review commit trên `main` | Push GitHub; sửa file của Chiến; gửi patch mới khi vòng trước chưa có kết quả |
| **Claude** (máy Windows) | Nhận patch; **đọc diff trước khi chạy**; build; selftest; đối chiếu tiêu chí nghiệm thu; commit và push `main`; báo cáo; revert khi được yêu cầu | Force-push; sửa nghiệp vụ ngoài spec để cho qua |
| **Chiến** | Duyệt spec; bấm thử trên `main`; yêu cầu revert khi cần; cập nhật trạng thái spec và `QUEUE.md` | — |

## 3. File và thư mục

| Đường dẫn | Dùng cho |
|---|---|
| `G:\My Drive\fnb-muse\<feature>-r<N>.patch` | Patch vòng N. Không ghi đè: vòng sửa là `r2`, `r3`… |
| `G:\My Drive\fnb-muse\<feature>-r<N>.manifest.txt` | Thông tin patch (§4) |
| `G:\My Drive\fnb-muse\<feature>-r<N>.result.txt` | Claude ghi kết quả vòng đó (bản sao của báo cáo trên GitHub) |
| `D:\Working\fnb-muse-work\wt\` | Worktree của Claude, **ngoài repo chính** |
| `D:\Working\fnb-muse-work\state.txt` | Mỗi dòng: `<patch_sha256> <feature> r<N> <ok|fail> <commit nếu có> <thời điểm>` |
| `D:\Working\fnb-muse-work\tmp\` | DB chép, file build, kết quả selftest |

Thư mục `fnb-muse` trên Drive: chỉ tài khoản Chiến và Muse được **ghi**. SHA256 trong manifest chỉ chứng minh file **không hỏng**, không chứng minh ai gửi. Vì code lên thẳng `main`, việc đọc diff ở bước 5 là bắt buộc.

## 4. Manifest

```
feature: luu-vet-hoat-dong
round: 1
spec: docs/specs/luu-vet-hoat-dong.md @ <SHA commit chứa bản spec đã duyệt>
base: <SHA đầy đủ của origin/main lúc Muse làm>
patch_sha256: <SHA256 của file .patch>
status: ready
note: <tùy chọn: chỗ spec chưa rõ, việc chưa làm>
```

- Mọi vòng, kể cả vòng sửa, `base` đều là `origin/main` lúc Muse làm (vòng sửa đã chứa commit của vòng trước).
- Muse chỉ đặt `status: ready` sau khi đã upload xong cả hai file.

## 5. Vòng nhận patch (Claude)

Làm theo thứ tự. **Dừng ở bước nào cũng phải làm bước 12 (báo cáo)** và ghi `fail` vào `state.txt`, để không chạy lại cùng patch.

1. **Tìm việc.** Đọc mọi `*.manifest.txt` có `status: ready` mà `patch_sha256` chưa có trong `state.txt`. Nhiều file thì lấy cũ nhất. Không có thì kết thúc vòng.
2. **Kiểm file.** `Get-FileHash -Algorithm SHA256` khớp `patch_sha256`. Lệch thì chờ 60 giây, đọc lại một lần. Vẫn lệch → dừng.
3. **Kiểm spec.** `git show <SHA spec>:<đường dẫn>` phải có trạng thái **ĐÃ DUYỆT**. Không → dừng.
4. **Kiểm base.** `git fetch origin`. Chấp nhận khi `base` = `origin/main`, hoặc khi mọi commit từ `base` tới `origin/main` **chỉ sửa trong `docs/`** và patch không đụng `docs/`: khi đó áp lên `origin/main` và ghi base thật vào báo cáo. Trường hợp khác → dừng, báo "cần rebase lên `<sha>`". Không thử `--3way`.
5. **Đọc diff trước khi chạy.** Xem toàn bộ patch. **Dừng chờ Chiến** nếu patch:
   - đụng `.slnx`, `.gitignore`, `.github/`, `.claude/`, `CLAUDE.md`, `Kazuko_FnB/lib/`, file `.ps1` `.bat` `.cmd`, file nhị phân;
   - sửa `.csproj` ngoài việc thêm/bớt `<Compile Include>` của file trong patch (ví dụ thêm `Reference`, `HintPath`, `Target`, `Exec`, gói NuGet);
   - có code chạy tiến trình, gọi mạng, xóa file ngoài thư mục tạm, đổi cấu trúc DB (DDL), hoặc ghi vào `POS TAP/`;
   - sửa file ngoài phạm vi spec (trừ `SelfTest.cs`, `ScreenFactory.cs`, menu, `.csproj` để nối chức năng vào).
   Ghi luôn chỗ nghi ngờ nghiệp vụ (lệch spec, ghép chuỗi SQL, `double` cho tiền, màu cứng…) vào báo cáo. **Lệch spec ở phần tiền, giá, kho, công nợ thì dừng**, không push; lỗi khác không chặn.
6. **Chuẩn bị worktree.** Chưa có thì `git worktree add --detach D:\Working\fnb-muse-work\wt origin/main`; có rồi thì `git -C wt checkout --detach origin/main` và `git -C wt clean -fdx` (chỉ trong worktree). Chép vào worktree hai thứ không có trong git: `Kazuko_FnB/lib/` và `POS TAP/Data/TEMPLATE.FDB`.
7. **Áp patch.** `git apply --check` rồi `git apply`. Lỗi → dừng.
8. **Build.** `D:\VisualStudioInstallation\MSBuild\Current\Bin\MSBuild.exe Kazuko_FnB\Kazuko_FnB.slnx -p:OutDir=D:\Working\fnb-muse-work\tmp\build\` (cấu hình mặc định; ra thư mục tạm nên không đụng app Chiến đang chạy). Đạt: **0 lỗi, không thêm cảnh báo** so với build của base (MSB3270 có sẵn).
9. **Selftest.** Mỗi lần chép mới `DATA.FDB` vào `tmp\` rồi chạy `Kazuko_FnB.exe --selftest <bản chép> <kết quả.txt>`. Chạy cả trên bản build của base (lưu theo SHA, tái dùng). Đạt: **không có ca lỗi mới** so với base, và ca mới của patch đều qua. Không bao giờ chạy lên DB thật.
10. **Sửa nhỏ (nếu cần), tối đa 2 vòng.** Chỉ sửa lỗi biên dịch hoặc selftest mà spec đã chỉ rõ cách đúng. Mỗi lần sửa là **một commit riêng** `fix(claude): …` để Muse thấy. Lỗi do spec không rõ hoặc ngoài spec → dừng, không sửa.
11. **Commit và push `main`.**
    - Commit patch: `feat: <feature> r<N> (Muse, base <sha ngắn>)`, kèm dòng `Patch-SHA256: <…>`. Commit `fix(claude)` nếu có đi ngay sau.
    - `git fetch origin`. Nếu `origin/main` đã đổi kể từ bước 6: đổi chỉ trong `docs/` thì `git rebase origin/main` trong worktree rồi **build và selftest lại**; đổi khác → dừng.
    - `git push origin HEAD:refs/heads/main` (không `--force`). Bị từ chối vì `main` vừa đổi → quay lại gạch đầu dòng trên, tối đa 2 lần.
12. **Báo cáo.** Comment lên issue `muse/<feature>` (nhãn `muse-patch`; chưa có thì tạo), nội dung theo §6. Ghi cùng nội dung vào `<feature>-r<N>.result.txt` trên Drive.
13. **Ghi `state.txt`** (`ok` + commit, hoặc `fail`). Quay lại bước 1.

## 6. Nội dung báo cáo

```
feature / round / patch_sha256 / base / commit trên main (nếu đã push)
Kết quả: ĐÃ PUSH | DỪNG ở bước <n>: <lý do>
Build: <số lỗi>, cảnh báo mới: <danh sách hoặc không>
Selftest: base <x lỗi> → patch <y lỗi>; ca lỗi mới: <…>
Claude tự sửa: <commit + lý do, hoặc không>
Nghi ngờ khi đọc diff: <…>
Tiêu chí nghiệm thu (theo spec §7):
| Tiêu chí | Đạt / Không đạt / Chưa kiểm | Bằng chứng (ca selftest, ảnh, truy vấn) |
Việc còn cho Chiến: bấm thử <luồng>, trên bản sao DB
```

Tiêu chí cần thao tác tay hoặc nhìn màn thì ghi **Chưa kiểm**, không ghi Đạt.

## 7. Review sau khi lên `main`, và gỡ lỗi

1. Muse review các commit của chức năng trên `main`. Cần sửa → gửi vòng `r<N+1>` với base là `origin/main` mới nhất.
2. Chiến `git pull` ở thư mục làm việc chính, bấm thử trong app trên bản sao CSDL.
3. Lỗi nặng (sai tiền, hỏng dữ liệu, không build được) → Chiến yêu cầu, Claude `git revert` các commit của chức năng đó (không xóa lịch sử), build lại, push `main`, báo trên issue. Muse làm lại từ `main` sau revert.
4. Đạt → Chiến cập nhật trạng thái spec và `QUEUE.md`, đóng issue.
5. Chức năng nào **chưa được Muse review và Chiến bấm thử** thì không dùng ở quán, dù đã nằm trên `main`.

## 8. Chạy vòng lặp

- Phiên Claude không tự thức dậy. Bật bằng `/loop 15m` trong một phiên Claude Code riêng mở ở `D:\Working\FnB` (hoặc tác vụ theo lịch). Máy phải bật, Drive phải đồng bộ.
- Cấp sẵn quyền chạy để không kẹt ở hộp xin phép: `git`, MSBuild, `Kazuko_FnB.exe --selftest`, `gh`, `Get-FileHash`, đọc/ghi `G:\My Drive\fnb-muse\` và `D:\Working\fnb-muse-work\`.
- Không có patch mới thì vòng kết thúc ngay, không làm việc khác.
- Sau khi Claude push, thư mục làm việc chính của Chiến **chưa có** code mới: Chiến `git pull` trước khi làm tiếp.

## 9. Ngoại lệ cần thêm vào `CLAUDE.md`

`CLAUDE.md` hiện yêu cầu Chiến bảo mới commit/push. Workflow này cần:
- Claude được commit và push **lên `main`** (chỉ fast-forward) patch của Muse đã qua §5 cùng các commit `fix(claude)` của vòng đó, **không cần hỏi**.
- Claude được tạo issue nhãn `muse-patch` và comment lên đó; được tạo/xóa worktree trong `D:\Working\fnb-muse-work\`.
- Claude được `git revert` commit của một chức năng khi Chiến yêu cầu.
- Vẫn cấm: force-push, sửa lịch sử `main`, sửa `DATA.FDB` gốc, commit file DB.

## 10. Lỗi thường gặp

- Không thấy file trên Drive: chờ đồng bộ; quá 5 phút thì bỏ qua vòng này (không báo lỗi, vì chưa có patch).
- Thiếu `Kazuko_FnB\lib\Aspose.Cells.DLL`: dừng, báo Chiến chép từ thư mục cài POS TAP. Không tải DLL trên mạng.
- `main` liên tục đổi (phiên khác cũng push tài liệu): chỉ commit trong `docs/` được bỏ qua theo bước 4 và 11; đổi code thì Muse phải rebase.
- Selftest của base lỗi khác lần trước: chạy lại một lần; vẫn khác thì dừng, báo môi trường.

## 11. Quy ước nghiệp vụ đã chốt (không hỏi lại)

Xóa cứng (không thùng rác) · tiền `decimal`, `LamTron` bội 1000 AwayFromZero · F&B thuần COFFEE + POS, không tính tiền giờ · không dùng cột `LOAIDINHLUONG` · khóa chính GUID, `STATUS = 30` · SQL tham số hóa, không ghép chuỗi · không SQL trong View · không đổi cấu trúc DB · UI và luồng như app gốc, chỗ khác ghi rõ để Chiến quyết · quỹ theo [`specs/quy-tien.md`](specs/quy-tien.md) · spec và code commit riêng.

## 12. Bài test đầu tiên

- **Một** chức năng, spec đã duyệt, base = `origin/main` lúc làm. Gợi ý: Lưu vết hoạt động (cần Chiến duyệt spec trước).
- Patch gộp 7 chức năng trên `02ba7a7` **không dùng**: `main` đã đi trước (sửa `SelfTest.cs`, `ScreenFactory.cs`, các `.csproj`), và cả 7 spec còn NHÁP. Muốn giữ phần code đó thì Muse tách thành từng patch, gửi sau khi từng spec được duyệt.
- Trước khi chạy: thêm §9 vào `CLAUDE.md`; tạo nhãn `muse-patch`; chạy selftest của `main` một lần để có mốc (đang ghi nhận 2 ca lỗi cũ ở phần xóa nhóm người dùng).
- **Thành công** = commit lên `main` fast-forward, báo cáo đủ §6 trên issue, Muse review xong, Chiến bấm thử. Thử thêm một lần `git revert` để chắc đường gỡ lỗi chạy được.
