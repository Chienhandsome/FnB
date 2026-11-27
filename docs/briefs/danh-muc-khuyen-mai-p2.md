# Brief: Khuyến mại P2, áp dụng khi bán
- Spec: `docs/specs/danh-muc-khuyen-mai.md` mục 4 và 6. Brief P1: `danh-muc-khuyen-mai.md`.
- **27/11 đã code, push `13d858e`** (#53): `PromotionApplier` (lớp tính thuần), nối `TableService.*` (mọi đổi dòng đều tính lại, bill đã thanh toán không tính lại), dòng giảm KM và dòng tặng đánh dấu `TDONHANGCHITIET.KHUYENMAI = 30`, giảm tổng bill KM đánh dấu `TDONHANG.GIAMTONGTHEOTIEN = 30`, nhãn KM trên lưới Sử dụng dịch vụ, dòng tặng trừ kho và chặn âm kho. Công thức tiền vẫn qua `OrderCalculator`.
- **Chưa kiểm:** bấm thật, 125% DPI, in hóa đơn có dòng tặng. **Cần lưu ý:** cột `GIAMTONGTHEOTIEN` dùng làm cờ KM (app gốc có thể hiểu là giảm theo tiền); chưa áp dụng ở Báo giá, Khách đặt hàng, Quản lý bán hàng; khung giờ chưa dùng.
