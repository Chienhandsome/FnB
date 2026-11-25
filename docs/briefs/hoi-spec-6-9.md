# Brief: hỏi gộp câu mở của spec 6–9 (25/11/2026)

## Đã làm
- Spec 6 `bao-gia-ban-hang.md` §5.0: Chiến chốt Q1/P1, P2, P3, Q4. Phần làm tròn và gộp dòng đã đối chiếu `TBAOGIAAe` `code.cs`.
- Spec 7 `danh-muc-khach-hang.md` §9.0: Chiến chốt Q2, Q5, Q6/A3, Q9, Q10, A1/Q11, A2. Chưa đọc lại code gốc cho spec này.
- Spec 8 `su-dung-dich-vu.md` §6.4: chốt Q2–Q12, phạm vi, P1, P4, P5, P7–P9.
- Spec 9 `quan-ly-ban-hang-dieu-chinh-hoa-don.md` §6.3: chốt Q1, Q2, Q4–Q8, P1–P4, P7, cọc dư, hủy có cọc. Đã tự đọc `GetDatTruoc`, `TraLaiKhach`, đoạn đóng bill.

## Xác minh 25/11
- Mã dùng chung ở `SCONFIG` `SharedCode`. Hỏi lại Chiến 4 câu spec 8 (ngày bán, số hóa đơn, kiểm bill, giữ bàn): §6.5.
- Cọc dư khi sửa bill mâu thuẫn `quy-tien.md` → chốt "như gốc, thêm cảnh báo"; đã ghi ngoại lệ vào `quy-tien.md`.
- Kết quả ở spec 6 §5.0.1, spec 7 §9.0.1, spec 8 §6.5, spec 9 §6.4; đã thêm tiêu chí nghiệm thu. `kinh-nghiem.md` §1 thêm 3 dòng.

## Còn lại
- Cần thử trên app gốc: spec 7 Q3, Q4, Q10 (chỉ đọc), phần còn lại của Q7, Q8, Q12; spec 6 Q5 (hiển thị). Không cần chụp trước (Chiến 25/11: bản dùng thử xin mở lại được).
- Chưa xác minh: spec 6 Q2; spec 8 các mục cuối §6.5; spec 9 voucher; thiết kế gắn phiếu chi với bill.
- Thân spec còn câu cũ (nhất là "trong DLL"); mục chốt mới thắng. Dọn khi duyệt.
- Cả bốn spec vẫn NHÁP, chờ Chiến duyệt.
- Sau này: cấu hình toàn hệ thống cho phép số lượng âm.

## Chưa kiểm
- Chưa chạy app gốc, chưa build.
