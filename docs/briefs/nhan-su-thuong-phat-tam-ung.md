# Brief: Nhân sự P2 (Thưởng phạt, Tạm ứng lương)
- Spec: `docs/specs/nhan-su-thuong-phat-tam-ung.md` (Claude tự duyệt 28/11).
- **28/11 đã code, push `4f40fbd`** (#60): `RewardPenalty*`: lưới lọc ngày/nhân viên, hàng tổng, số phiếu `TP` + yy, chặn hai số tiền cùng 0, giới hạn tháng (tham số cho Tính lương gọi). `SalaryAdvance*`: phiếu chi `LATAMUNG = 30` dùng hộp `PhieuThuDialog` ở chế độ tạm ứng, lưu qua `VoucherRepository`, kiểm chi vượt quỹ bằng `VoucherService.CheckCashOverdraftIn` (tách dùng chung, Phiếu chi cũ giữ nguyên).
- **Chưa kiểm:** bấm thật, 125% DPI, ảnh app gốc. Điểm TẠM T1–T6 trong spec (nhất là T6: trừ tạm ứng chỉ tính `CHI`, Chiến chưa chốt R1).
