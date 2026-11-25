# Brief: Chi tiết hoạt động ngày (25/11/2026)

Spec: [`../specs/chi-tiet-hoat-dong-ngay.md`](../specs/chi-tiet-hoat-dong-ngay.md). Báo cáo đầy đủ và bảng tiêu chí: issue #13.

## Đã làm
- Code trên `main`: `ffc119b` (base `730b921`; agy 1.5.17, 3 vòng; Claude không tự sửa code). Selftest 645 → 679 đạt, vẫn 7 lỗi cũ.
- File chính: `DailyActivityRepository`, `DailyActivitySummary`, `DailyActivityView`, `DailyActivitySections(.Layout)`; ca test ở 4 file `Diagnostics/SelfTest.DailyActivity*.cs`; chụp bằng `--render-screen <png> preview.dailyactivity <theme> <db> [bottom]`.
- r2 siết ca test lỏng. r3 sửa 2 lỗi chỉ lộ khi chụp màn: khối THU-CHI bị che khi bố cục lúc màn ẩn; vùng cuộn tự nhảy về lưới đang focus.
- Chạy trên CSDL do app gốc tạo: khớp số ở spec §0. Bộ đó không có phiếu nhập, chuyển khoản, voucher, nợ.

## Còn lại
- Chiến bấm thử (xem `QUEUE.md`). Mẫu in: "chưa hỗ trợ" (Q5).
- `tools/agy/` đã sửa sau khi Chiến duyệt (25/11): chặn `%TEMP%`, thêm `-CoAuthor` cho `Publish`, đường dẫn tuyệt đối trong `prompt-template.md`. Chưa chạy một vòng agy với bản sửa.

## Chưa kiểm
- 125% DPI; bấm chuột/chạm thật khi cuộn; đăng nhập bằng user từng nhóm quyền; ca "ngày bắt buộc"; ca gộp theo riêng `TENHANG`.
- Hai chốt `de1bea6` không gặp ca đáng bị chặn trong lần chạy này. `MSB1025` khi agy build: chưa rõ do quy tắc chặn Temp mới hay không.
- Các thử quyền ghi ở `workflow-agy.md` §4.1 là trên agy 1.5.16.
