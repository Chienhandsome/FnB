# Brief: Quỹ, phiếu thu/chi
- Spec: `docs/specs/phieu-thu-chi.md` (Claude tự duyệt 26/11; T1–T4 tạm).
- **27/11 đã code, push `66e5b4c`** (#39): Tạo phiếu thu/chi ghi thật (`VoucherService`, một transaction, chặn chi vượt quỹ qua `FundLedgerRules`), Danh mục phiếu thu/chi, Danh mục lý do thu chi, Danh mục tài khoản ngân hàng, menu. Kèm A1 chặn hủy bill có phiếu thu cọc và A2 bật Lưu & In khi phiếu đã lưu.
- **Chưa kiểm:** bấm thật, 125% DPI, in phiếu (chưa làm), xóa phiếu thu làm quỹ âm (chưa chặn), định dạng số phiếu (T1).
