# Brief: thử và vá chốt chặn tools/agy (25/11/2026)

## Đã làm (Chiến "go" 25/11, base `5694936`)
- Thử chốt chặn trong sân tách riêng (clone + remote giả, build và selftest thật, `agy` giả); tìm thấy lỗ hổng rồi vá.
- `New-AgyFeature`: trạng thái phải bắt đầu `**ĐÃ DUYỆT` (phân biệt hoa thường); mốc lưu tên ca selftest, mốc cũ trong `_cache` được tạo lại.
- `Test-AgyBuild`: mất ca đạt ở mốc (so theo tên) → không đạt. `Publish`: build phải chạy trên commit nền hiện tại.
- `Test-AgyScope`: cấm file cấu hình MSBuild (cả file git bỏ qua), `.csproj` mới, `Include` ra ngoài project; so `Kazuko_FnB/lib`, hook và cấu hình git thư mục chính; so ảnh chụp file ngoài worktree (thêm, sửa, xóa).
- `core.quotepath=false` cho tên có dấu; sửa thông báo `Remove-AgyFeature`. `workflow-agy.md` §3, §10.1.1 cập nhật.
- Thử trên đúng file diff: build 7 ca đúng; publish chặn đúng, build lại thì push được vào remote giả; phạm vi đúng trừ ca `bin` bên dưới.
- Sau khi áp: nội dung khớp bản đã thử (khác CRLF), 8 script không lỗi cú pháp, còn BOM.

## Còn lại
- Không soi `bin`, `obj`, `.git`, `.vs` ngoài worktree (riêng hook, cấu hình git repo chính có soi).
- Ca selftest giữ tên nhưng bị làm rỗng vẫn qua: Claude vẫn phải đọc diff.
- Chạy thử trọn một chức năng với `agy` thật trong phiên Auto.

## Chưa kiểm
- Chưa chạy với `agy` thật. Chưa thử push bị từ chối do `main` đổi đúng lúc push, bước Claude tự sửa, quyền của `agy`.
- Có thể chặn nhầm khi lúc `agy` chạy có thay đổi cấu hình git, thêm/xóa file trong thư mục được quét, hoặc cập nhật `lib/` khi đang dở chức năng.
- Chụp khoảng 91.000 file mất chừng 7 giây một lần (hai lần mỗi lượt `agy`).
