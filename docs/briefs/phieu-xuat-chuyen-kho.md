# Brief: Xuất khác và Chuyển kho
- Spec: `docs/specs/phieu-xuat-chuyen-kho.md` (Claude tự duyệt 27/11; Chiến chốt thuế/phí chuyển kho giữ như gốc).
- **27/11 đã code, push `477c605`** (#44): hai lưới quản lý và hộp phiếu (`StockDocument*`), dòng xuất đối ứng của chuyển kho, kiểm tồn theo số đã quy đổi, kiểm cả kho đích cũ khi sửa/xóa chuyển kho, một transaction, lưu vết, xuất Excel, menu. Dùng hàm tồn chung `GetStockBalanceIn`.
- **Chưa kiểm:** bấm thật, 125% DPI, `THANHTIEN` xuất/chuyển (T1 suy đoán), số phiếu PX/CK (T5).
