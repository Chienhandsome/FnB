# CLAUDE.md — KAZUKO TEA (bản viết lại, repo FnB)

Đọc trước khi làm bất cứ việc gì: spec trong `docs/specs/` liên quan tới task. Các quyết định trong `docs/` là chuẩn của dự án.

Trước khi viết/sửa spec, đối chiếu app gốc, hỏi nghiệp vụ, build hoặc test: đọc [`docs/kinh-nghiem.md`](docs/kinh-nghiem.md) và làm theo. Rút thêm được kinh nghiệm thì bổ sung vào file đó.

## Dự án

Viết lại nội bộ phần mềm quản lý F&B **KAZUKO F&B** (sản phẩm của chính công ty, sếp giao làm lại). Stack: **WinForms** desktop (Windows-only), **.NET Framework 4.8**, build **x86**, **Firebird 2.5 Embedded** (`fbembed.dll` 32-bit), SQL viết tay (không ORM), FastReport, MVP/MVVM nhẹ. Nghiệp vụ giữ **y hệt** app gốc; UI làm lại hiện đại hơn, có tính năng đổi bảng màu; dùng chung schema `DATA.FDB` (không đổi cấu trúc).

Trong repo:
- `Kazuko_FnB/` — solution đang viết lại (WinForms net48 x86), tách 5 project classic (đã tách 19/11/2026): `Kazuko_FnB.UI` (exe, WinForms + theme), `Kazuko_FnB.Services` (luật nghiệp vụ), `Kazuko_FnB.Data` (repository Firebird), `Kazuko_FnB.Domain` (DTO/entity), `Kazuko_FnB.Infrastructure` (cấu hình). Tham chiếu một chiều UI → Services → Data → Domain. Mở `Kazuko_FnB/Kazuko_FnB.slnx`, startup = `Kazuko_FnB.UI`. `bin/obj/.vs` đã gitignore.
- `POS TAP/` — app gốc (binary + `Database/DATA.FDB`): chỉ tham chiếu, **không sửa**.
- `POS_TAP_SRC_EXPORT/` — mã form/báo cáo xuất từ DB để đọc (gitignored); có đồ thị graphify (`graphify-out/`) để tra cứu chức năng.
- `docs/specs/` — spec. `DATA.FDB` ở thư mục gốc là bản copy để dev/test.

## Làm việc với Chiến

- Trước mọi task và trước mọi câu trả lời về repo: `git fetch origin main` và xác nhận commit mới nhất. Không làm việc trên base cũ.
- Chạy app bằng F5 trong Visual Studio. Trước khi build/run phải tắt hẳn app đang chạy (đóng cửa sổ + Shift+F5 nếu đang debug), nếu không DLL bị lock (MSB3021/MSB3027). Nếu luồng bị chặn vì app đang chạy thì tự tắt đúng tiến trình của workspace này, không chờ Chiến tắt tay.
- Kiểm tra biên dịch bằng **MSBuild hoặc Visual Studio** ở cấu hình **x86** (`Kazuko_FnB` là .NET Framework classic, không phải SDK-style).
- Sửa code thì làm theo skill `kazuko-patch`: 1 patch/task, build trên đúng base, verify `git apply --check` trên main sạch, strip BOM, ghi rõ base commit.
- Không commit, không push khi chưa được Chiến bảo. **Ngoại lệ (Chiến duyệt 25/11/2026), chỉ khi chạy vòng nhận patch theo [`docs/workflow-muse.md`](docs/workflow-muse.md):**
  - Được commit và push lên `main` (chỉ fast-forward) patch của Muse đã qua đủ các bước kiểm ở §5 của file đó, cùng các commit `fix(claude)` của vòng đó, không cần hỏi.
  - Được tạo issue nhãn `muse-patch` và comment lên đó; được tạo/xóa worktree trong `D:\Working\fnb-muse-work\`.
  - Được `git revert` commit của một chức năng khi Chiến yêu cầu.
  - Vẫn cấm: force-push, sửa lịch sử `main`, sửa `DATA.FDB` gốc, commit file DB. Patch có điểm phải dừng theo §5 thì dừng, không push.
- Không sửa `DATA.FDB` gốc (trong `KAZUKO TEA/Database/`); không commit file DB (có thể chứa dữ liệu thật). Dev/test dùng bản copy.

## Quy trình spec-driven

Idea → viết spec (lệnh `/spec`) → Chiến duyệt spec → plan → implement → verify theo acceptance criteria trong spec. Không code khi spec chưa được duyệt. Nghiệp vụ tiền/giá/kho/công nợ là lõi: chưa rõ thì hỏi, không đoán.

## UI

Dialog gọn, hạn chế vertical scroll, high-DPI, hỗ trợ cảm ứng cho quầy bán hàng. Không hard-code màu/cỡ chữ trong màn hình nghiệp vụ: dùng design token + `ThemeManager` (tự vẽ, có đổi bảng màu lúc chạy). Không đưa SQL vào View/ViewModel.

## Quy trình làm việc

1. Trước khi implement: gọi subagent `researcher` (Haiku) tìm tài liệu/code liên quan.
2. Chỉ code sau khi đã có kết quả tìm hiểu.
