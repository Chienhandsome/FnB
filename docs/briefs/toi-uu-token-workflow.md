# Brief: tối ưu token cho workflow agy (25/11/2026)

## Đã làm
- `workflow-agy.md`: đọc diff theo delta, siết việc Claude tự sửa, chụp màn khi cần, chụp app gốc, `/compact` (`02a8bc6`, `d1eb362`).
- Thử dịch ngược để lấy thứ tự cột từ `ViewConfig`: **không ra** (ghi ở `kinh-nghiem.md` §1). REA không đưa vào workflow.
- Vá 2 chốt an toàn trong `tools/agy` bị hỏng do mảng lồng (`kinh-nghiem.md` §4): "main đổi code" và "thư mục chính đổi" (`de1bea6`).
- Mục 1–6 (Chiến "go" 25/11), nằm trong commit chứa file brief này:
  - Luật file `.cs` tối đa 500 dòng, `Test-AgyBuild.ps1` kiểm.
  - Spec mới dạng thư mục (`/spec`).
  - Brief ở `docs/briefs/`.
  - `Get-AgyDelta.ps1` (mới).
  - `researcher` chỉ gọi khi cần, phải trích `file:dòng`.
  - Sửa `CLAUDE.md`, gồm luật trình diff, chờ "go".
  - Chọn model ghi ở §10.4.
- Chiến bỏ ý dùng advisor.

## Còn lại
- Tách `SelfTest.cs` (5.193 dòng) theo chức năng: task code riêng. Khi tách phải sửa `SharedPrefixes` trong `AgyCommon.ps1`.
- Script chuẩn bị bản chép app gốc: chưa làm, chưa kiểm định dạng `conn.dat`.
- Prompt mẫu cho `agy` tự rà trước khi trả bài.

## Chưa kiểm
- Chưa chạy trọn một vòng `agy` sau khi sửa script.
- `Spec -Spec .../index.md` mới kiểm bằng đọc `New-AgyFeature.ps1`.
- Chưa đo token tiết kiệm được.
