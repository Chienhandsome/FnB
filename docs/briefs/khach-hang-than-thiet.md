# Brief: Khách hàng thân thiết
- Spec: `docs/specs/khach-hang-than-thiet.md` (Claude tự duyệt 27/11; Chiến chốt: công thức điểm riêng của màn, chỉ nâng nhóm không hạ, chặn giảm điểm vượt điểm có).
- **27/11 đã code, push `911b294`** (#54): `Loyalty*`: lưới khách có cây nhóm, Doanh số, Số hóa đơn, Điểm tích lũy, hàng tổng cộng, tab Hóa đơn, tab Tặng điểm - tặng quà, hộp tặng/giảm điểm (số phiếu TD yy/00000), chặn giảm vượt điểm và xóa làm điểm âm, tự nâng nhóm sau khi tặng điểm bằng `ServicePaymentRepository.TryUpgradeCustomerGroupIn`, xuất Excel, quyền, menu.
- **Chưa kiểm:** bấm thật, 125% DPI. Điểm màn này có thể lệch điểm lúc thanh toán (giữ như gốc).
