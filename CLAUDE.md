# CLAUDE.md — KAZUKO ERP

Đọc trước khi làm bất cứ việc gì: `AGENTS.md` (luật bắt buộc) + tài liệu trong `docs/` liên quan tới task. Các quyết định trong `docs/` là chuẩn của dự án.

## Dự án

ERP tiệm vàng KAZUKO. WPF desktop (Windows-only), .NET 8 (global.json pin SDK 8.0.100), Firebird 5.0 Embedded, SQL viết tay (không ORM), FastReport, MVVM tự viết, modular monolith.

Cấu trúc: `src/Kazuko.App` (shell), `src/Kazuko.Core`, `src/Kazuko.Infrastructure`, `src/Kazuko.Shared.UI`, `src/Modules/*`, `database/migrations/*`, `tests/*`.

## Làm việc với Chiến

- Trước mọi task và trước mọi câu trả lời về repo: `git fetch origin main` và xác nhận commit mới nhất. Không làm việc trên base cũ.
- Chạy app bằng F5 trong Visual Studio. Trước khi build/run phải tắt hẳn app đang chạy (đóng cửa sổ + Shift+F5 nếu đang debug), nếu không DLL bị lock (MSB3021/MSB3027). Nếu luồng bị chặn vì app đang chạy thì tự tắt đúng tiến trình ERP của workspace này, không chờ Chiến tắt tay.
- Kiểm tra biên dịch bằng `dotnet build KAZUKO_ERP_JEWELRY.sln` (theo `docs/04-DEVELOPMENT-WORKFLOW.md`).
- Sửa code thì làm theo skill `kazuko-patch`: 1 patch/task, build trên đúng base, verify `git apply --check` trên main sạch, strip BOM, ghi rõ base commit, kèm lệnh apply + migration.
- Không commit, không push khi chưa được Chiến bảo.
- Không sửa `data/data.fdb` (file này lọt vào repo public do nhầm, không đụng tới).

## Quy trình spec-driven

Idea → viết spec (lệnh `/spec`) → Chiến duyệt spec → plan → implement → verify theo acceptance criteria trong spec. Không code khi spec chưa được duyệt. Nghiệp vụ vàng/tiền/kho/công nợ là lõi: chưa rõ thì hỏi, không đoán.

## UI

Dialog gọn, hạn chế vertical scroll, hợp style app hiện tại. Chi tiết xem `docs/02-UI-AND-ACCESSIBILITY.md`. Không hard-code màu/cỡ chữ trong màn hình nghiệp vụ, không đưa SQL vào View/ViewModel.
