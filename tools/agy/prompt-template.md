Bạn viết code cho repo KAZUKO F&B ở {WORKTREE} (thư mục hiện tại; một git worktree, solution Kazuko_FnB/Kazuko_FnB.slnx, WinForms .NET Framework 4.8, Firebird 2.5, SQL viết tay, không ORM).

Cách dùng công cụ:
- Đọc và tìm file bằng công cụ đọc/tìm của bạn. Chỉ đọc trong thư mục hiện tại; mọi file ngoài thư mục này sẽ bị từ chối và làm dừng lượt chạy.
- Thư mục POS TAP/ bị chặn đọc. Không cần đọc nó.
- Lệnh shell: mỗi lần MỘT lệnh đơn, không dùng | ; && > và dấu backtick ở BẤT KỲ đâu, kể cả trong chuỗi mẫu tìm. Lệnh git có thể báo not a git repository, bỏ qua. Lệnh được phép: Get-Content, Get-ChildItem, Select-String, Test-Path, git status/diff/log/show/ls-files, MSBuild ở dưới. Lệnh khác bị từ chối và làm dừng lượt chạy, nên đừng thử.

Đọc trước, theo thứ tự:
1. CLAUDE.md
2. docs/kinh-nghiem.md
3. {SPEC} (spec ĐÃ DUYỆT, là yêu cầu chính) và các tài liệu spec đó trỏ tới nếu cần
4. docs/workflow-agy.md §8 (quy ước nghiệp vụ)
5. Code hiện có để theo đúng cách làm (màn, repository, service tương tự đã có; Dialogs/ScreenFactory.cs; Diagnostics/SelfTest.cs; ThemeManager và ThemeTokens).

Việc: implement chức năng "{FEATURE}" đúng spec, gồm selftest trong Kazuko_FnB/Kazuko_FnB.UI/Diagnostics/SelfTest.cs.
<CLAUDE GHI CHI TIẾT VIỆC Ở ĐÂY>

Selftest phải kiểm được điều tiêu chí nói, không chỉ kiểm hàm phụ: ví dụ tiêu chí về màu trên lưới thì kiểm màu thật của ô sau khi tô, không chỉ kiểm hàm chọn tông. Mỗi tiêu chí §7 kiểm được bằng code phải có ít nhất một ca; tên ca ghi rõ tiêu chí. Dữ liệu mẫu tự tạo trên DB được truyền vào và dọn sau khi chạy.

Bắt buộc:
- Chỉ sửa file trong Kazuko_FnB/. Không sửa docs/, CLAUDE.md, tools/, .slnx, Kazuko_FnB/lib/, .gitignore.
- Không git commit, không git push, không tạo nhánh.
- Không đổi cấu trúc DB, không chạy app.
- Không thêm thư viện, gói NuGet, script. File .csproj chỉ được thêm dòng <Compile Include> cho file mới.
- Không SQL trong View; tiền dùng decimal; SQL tham số hóa; không hard-code màu, cỡ chữ (dùng ThemeTokens, ThemeManager).
- Sửa file dùng chung (Theme/, SelfTest.cs, ScreenFactory.cs, Controls/, DbHelper.cs) chỉ khi thật cần, và liệt kê trong AGY-NOTES.md kèm lý do.
- Chỗ spec không rõ: KHÔNG đoán. Ghi vào file AGY-NOTES.md ở gốc thư mục này.
- Có thể build để tự kiểm (tùy chọn):
  & 'D:\VisualStudioInstallation\MSBuild\Current\Bin\MSBuild.exe' Kazuko_FnB\Kazuko_FnB.slnx -nologo -v:m -p:OutDir={BUILD_OUT}\

Xong thì tóm tắt: file đã thêm/sửa, phần chưa làm, chỗ không chắc.
