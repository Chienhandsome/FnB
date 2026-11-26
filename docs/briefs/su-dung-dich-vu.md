# Brief: Sử dụng dịch vụ

- Spec: `docs/specs/su-dung-dich-vu.md` (đã duyệt; §6.6 Chiến chốt 26/11: âm kho mặc định chặn, chỉ khách có hồ sơ được nợ, in hóa đơn và in bếp). Chia 4 patch: (a) tính tổng, sơ đồ bàn, mở bàn, gọi món; (b) thanh toán; (c) chuyển/gộp bàn, hủy bill; (d) in hóa đơn và in bếp.
- **Patch (a) đã code, push `91a57c7`** (#34): `OrderCalculator` (công thức §3.6, LamTron 1000), cấp số hóa đơn 4 kiểu, khóa bàn nhiều máy, sơ đồ bàn 7 trạng thái, mở bàn, gọi món, combo, giảm/xóa/sửa dòng, chặn âm kho. Nút Thanh toán, In chế biến báo "chưa hỗ trợ".
- **Chưa kiểm (a):** bấm thật, 125% DPI, hai máy cùng mở một bàn, mở bàn từ phiếu đặt, combo mở, giới hạn dùng thử 30 ngày (chưa có cờ `IsTrial`); khóa `ChoPhepAmKho` chưa có trong `SCONFIG` nên mặc định chặn.
- Còn: patch (b), (c), (d).
- **Patch (b) đã code, push `56f2036`** (#35): hộp thanh toán thật (tiền mặt, thẻ, chuyển khoản, voucher, thẻ trả trước, trừ điểm, đặt trước), thanh toán chung nhóm bàn, nợ chỉ cho khách có hồ sơ, trừ kho và xuất vật tư, chặn âm kho mặc định, phiếu chi trả cọc dư, tự nâng nhóm khách, ghi trong một transaction. Chưa in (patch d): nút "Thanh toán & In" vẫn đóng bill, chưa gọi máy in.
- **Chưa kiểm (b):** bấm thật, nhiều hình thức thanh toán cùng lúc, thanh toán chung 3 bàn, trừ điểm, voucher, 125% DPI. Còn (c) chuyển/gộp bàn, hủy bill; (d) in.
