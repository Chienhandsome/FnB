# Brief: Mục nhỏ còn lại (Thông báo sửa chữa, mật khẩu wifi, Tồn nhiều kho)
- Spec: `docs/specs/quan-tri-nho.md` (Claude tự duyệt 28/11). Chia P-A (đã xong) và P-B (Tồn nhiều kho).
- **28/11 P-A đã code, push `cfff63f`** (#63): `RepairNotice*` (`TSUACHUA`, số phiếu `SC`, bàn tự điền loại phòng, lọc chưa sửa xong/ngày; trạng thái bàn "Sửa chữa" đã có sẵn trong code), `WifiPassword*` (`DMATKHAUWIFI`, cây nhóm `DNHOMWIFI`). Chưa kiểm bấm thật; chưa gán mật khẩu wifi lên hóa đơn (T4).
- **28/11 P-B đã code, push `8d26868`** (#64): Tồn nhiều kho: `InventoryView` chế độ tất cả các kho (mục "Tất cả các kho" mặc định, tổng tồn mọi kho được xem), bảng chi tiết theo kho; tồn dùng đúng `LoadStockBalancesByWarehouseIn` từng kho rồi cộng. Tồn kho thường không đổi. Chưa kiểm bấm thật.
