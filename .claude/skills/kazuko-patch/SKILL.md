---
name: kazuko-patch
description: Quy trình làm patch cho KAZUKO ERP. Dùng khi Chiến yêu cầu sửa code, làm tính năng, hoặc fix bug trong repo này. Bao gồm fetch main mới nhất, build patch trên đúng base, strip BOM, verify git apply --check, kèm lệnh apply và migration.
---

# Skill: làm patch KAZUKO ERP

## 1. Xác định base

```powershell
git fetch origin main
git log --oneline -1 origin/main
```

Mọi patch build trên commit mới nhất của `origin/main`. Không build trên base cũ. Nếu worktree local lệch main thì reset về đúng HEAD của Chiến trước khi làm — nhưng hỏi Chiến trước nếu worktree có commit local chưa push (reset sẽ làm mất code đó).

## 2. Sửa code

- Đọc `AGENTS.md` + docs liên quan trước khi sửa.
- 1 patch = 1 task. Không gộp nhiều task vào 1 patch.
- Không sửa `data/data.fdb`. Không commit, không push khi chưa được bảo.

## 3. BOM và line ending

- File XAML trong repo KHÔNG có BOM (Visual Studio lưu kèm BOM mặc định). Trước khi tạo patch, kiểm tra và strip BOM ở mọi file đã sửa/thêm.
- Giữ line ending như repo.

## 4. Migration (nếu đổi schema)

- File mới: `database/migrations/NNNN__ten_mo_ta.sql` (NNNN = số tiếp theo).
- Migration mới phải có version, checksum và tài liệu rollback (theo `docs/04-DEVELOPMENT-WORKFLOW.md`).

## 5. Verify patch

Trên một worktree main sạch tại đúng base:

```powershell
git apply --check <ten-patch>.patch
```

Patch phải pass `git apply --check` thì mới được gửi.

## 6. Gửi patch

Tin nhắn gửi patch luôn gồm:

- Dòng: `patch này apply trên <sha đầy đủ của origin/main>`.
- Lệnh apply: `git apply <ten-patch>.patch`
- Nếu có migration, kèm lệnh chạy migration SAU lệnh apply, theo đúng thứ tự, tắt app trước:

```powershell
.\database\tools\Invoke-KazukoMigration.ps1 .\database\migrations\NNNN__ten_file.sql -Database "D:\Working\KAZUKO_ERP_JEWELRY\data\data.fdb"
```

- Checklist test ngắn để Chiến kiểm thử bằng tay.
