# Brief: Khách đặt hàng, số lượng 2 đơn vị tính

- Spec: `docs/specs/khach-dat-hang.md` (mục cuối); luật ở `bao-gia-ban-hang.md` §5.0.2.
- **Đã push `cc928e9` ngày 26/11.** Build 0 lỗi; selftest 798 đạt, 7 lỗi cũ (mốc 773). Báo cáo và bảng tiêu chí: issue #23.
- Agy 1 vòng; Claude không tự sửa. Luật nằm ở `BookingItemRules`. Dialog dùng chung với Theo dõi đặt phòng.
- Sự cố: kiểm phạm vi chặn vì thư mục `KAZUKO TEA` bị xóa lúc agy chạy (Chiến tự xóa); kiểm lại đạt.
- Chiến chốt 26/11: commit việc xóa 5 file `KAZUKO TEA` cùng đợt `docs/`. `CLAUDE.md` còn nhắc `KAZUKO TEA/Database/` (Claude không được sửa).
- Chưa kiểm: bấm thử bản mới và app gốc (mới đọc mã `TDATHANG/code.cs`); đoạn mở hộp Nhập số lượng thật chỉ kiểm bằng đọc code.
- Còn khác Báo giá, ngoài phạm vi việc này, chờ Chiến: sửa trên lưới chi tiết vẫn cho số lượng 0 (số âm bị đưa về 0).
- Khác app gốc có từ trước: mặt hàng 1 đơn vị, khi `NhapMotMatHangNhieuLanTrongPhieu = 30` thì bản mới thêm dòng mới; `OnAddSameItem` gốc không đọc cờ này. Cấu hình hiện = 0 nên chưa thấy khác.
- 26/11: hai điểm "còn khác" ở trên đã xử lý, xem `khach-dat-hang-sl-gop.md`.
