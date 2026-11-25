# Brief: mode vắng mặt cho workflow agy (25/11/2026)

## Đã làm (Chiến "go" 25/11, base `7259c8e`, chưa commit)
- `CLAUDE.md`: ngoại lệ tự duyệt spec theo `workflow-agy.md` §10.1, §10.1.1.
- §10.1.1: spec nặng tự chốt khi có bằng chứng, chỉ khi có `fnb-agy-work\auto.on` (chỉ Chiến tạo/xóa; xóa không dừng vòng lặp).
- §10.2: comment của Claude có dòng đầu `<!-- claude -->`; bỏ qua chức năng bị chặn.
- §10.2.1: chặn chức năng, spec phụ thuộc, `agy.stop` khi agy vượt rào, thử lại tối đa 3 nhịp, không lách khi bị chặn quyền, báo `PushNotification`.
- §10.3, §10.4: giới hạn mới; phiên vòng lặp dùng chế độ Auto + quy tắc `deny` force-push do Chiến đặt.
- `git apply --check` đạt; nội dung sau khi áp khớp bản đã trình.

## Còn lại
- Chiến: tạo `.claude/settings.local.json` (deny force-push, không commit); cân nhắc bảo vệ nhánh `main` trên GitHub.
- Thử các chốt chặn bằng ca đáng bị chặn (gồm "`main` đổi giữa chừng", Claude tự sửa).
- Bước đọc diff lần hai cho phần tiền/kho: mới đề xuất, chưa vào workflow.
- Chụp app gốc cho spec còn lại, trước khi bản dùng thử hết hạn (khoảng 08/12/2026).
- Chạy thử trọn một chức năng trong phiên Auto, Chiến theo dõi.

## Chưa kiểm
- Luật mới chỉ là chữ: không script nào trong `tools/agy` kiểm `auto.on`, `agy.stop`, số lần thử lại, dấu comment.
- Chưa biết chế độ Auto có chặn `agy` hoặc script `tools/agy` không.
- Quyền điều khiển máy giữ được bao lâu; scheduled task (chưa đưa vào workflow).
- Không giới hạn số chức năng chờ bấm thử (Chiến chốt 25/11); đang chờ 6.
