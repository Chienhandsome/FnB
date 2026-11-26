# Brief: Sử dụng dịch vụ

- Spec: `docs/specs/su-dung-dich-vu.md` (đã duyệt; §6.6 Chiến chốt 26/11: âm kho mặc định chặn, chỉ khách có hồ sơ được nợ, in hóa đơn và in bếp). Chia 4 patch: (a) tính tổng, sơ đồ bàn, mở bàn, gọi món; (b) thanh toán; (c) chuyển/gộp bàn, hủy bill; (d) in hóa đơn và in bếp.
- **Patch (a) đã code, push `91a57c7`** (#34): `OrderCalculator` (công thức §3.6, LamTron 1000), cấp số hóa đơn 4 kiểu, khóa bàn nhiều máy, sơ đồ bàn 7 trạng thái, mở bàn, gọi món, combo, giảm/xóa/sửa dòng, chặn âm kho. Nút Thanh toán, In chế biến báo "chưa hỗ trợ".
- **Chưa kiểm (a):** bấm thật, 125% DPI, hai máy cùng mở một bàn, mở bàn từ phiếu đặt, combo mở, giới hạn dùng thử 30 ngày (chưa có cờ `IsTrial`); khóa `ChoPhepAmKho` chưa có trong `SCONFIG` nên mặc định chặn.
- Còn: patch (b), (c), (d).
- **Patch (b) đã code, push `56f2036`** (#35): hộp thanh toán thật (tiền mặt, thẻ, chuyển khoản, voucher, thẻ trả trước, trừ điểm, đặt trước), thanh toán chung nhóm bàn, nợ chỉ cho khách có hồ sơ, trừ kho và xuất vật tư, chặn âm kho mặc định, phiếu chi trả cọc dư, tự nâng nhóm khách, ghi trong một transaction. Chưa in (patch d): nút "Thanh toán & In" vẫn đóng bill, chưa gọi máy in.
- **Chưa kiểm (b):** bấm thật, nhiều hình thức thanh toán cùng lúc, thanh toán chung 3 bàn, trừ điểm, voucher, 125% DPI. Còn (c) chuyển/gộp bàn, hủy bill; (d) in.
- **Patch (c) đã code, push `35c0878`** (#36): chuyển bàn, gộp bàn, chuyển mặt hàng (tách một phần, combo), hủy bill (ghi `TDONHANGHUY` và chi tiết, lưu vết, xóa cứng). Hướng an toàn: không gộp bill nguồn mở từ phiếu đặt hoặc có phiếu thu gắn bill; không hủy bill có tạm ứng trực tiếp còn hiệu lực. Chưa có kéo-thả chuột trên ô bàn.
- **Chưa kiểm (c):** bấm thật; hủy bill đã có cọc từ phiếu đặt (agy không chặn, chờ Chiến chốt); gộp bàn có NHOMGUID. Còn (d) in hóa đơn và in bếp.
- **Patch (d) đã code, push `ac6c750`** (#37): in hóa đơn thanh toán (10 mẫu hóa đơn bán hàng dựng được), in tạm tính (tăng `SOLANINTAMTINH`, tôn trọng số lần tối đa), in lại bill, in thử bill (xem trước), in bếp/chế biến (mẫu `STEMPLATE` "Mẫu 80mm" của màn "In chế biến", ghi `TINCHEBIEN` sau khi in thành công, thiết lập máy in theo máy trạm lưu file JSON cục bộ). "Thanh toán & In" đóng bill rồi in thẳng.
- **Chưa kiểm (d):** bấm thật; in thật; mẫu 80mm kiểm đồ và 2 ngôn ngữ; in hóa đơn theo khu vực (nhiều máy in); thiết lập máy in bếp lưu ở `%LocalAppData%\Kazuko_FnB\kitchen-printers.json` (app gốc lưu Registry).
- **Sử dụng dịch vụ đã đủ 4 patch (a–d) trên main.**
