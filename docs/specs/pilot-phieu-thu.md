# Spec: Bản thử "Phiếu thu" — dựng màn hình từ layout của app gốc

- Trạng thái: **ĐÃ DUYỆT làm bản thử** (Chiến, 2026-11-19: "Làm thử Phiếu thu"). Bản thử **không ghi dữ liệu**. **Đã code (chưa commit)**; xem mục 7 về kết quả.
- Ngày: 2026-11-19
- Base: `origin/main` @ `b0d15db` (đã `git fetch origin main`); nhánh `main` local đang ở `9a233c9`
- Liên quan: `docs/specs/kazuko-tea-clone.md` (mục 4.5 Quỹ), `docs/specs/goi-demo-ui.md`
- Nguồn đã đọc: `SFORM` "Phiếu thu" (`AELAYOUT`, `CODE`), `SCOLUMN` của `TTHUCHI`, `DLYDOTHUCHI`, `SMENU` ("Tạo phiếu thu"), `SFUNCTION`; dữ liệu mẫu `TTHUCHI` trong DEMO.FDB.

## 1. Mục đích

Kiểm chứng đường làm: **`SFORM.AELAYOUT` (XML) → mô tả màn hình → trình hiển thị chung có theme**, trên một form nhỏ nhưng đủ loại: ô nhập chữ, số tiền, ngày, ô chọn có danh sách, ô tra cứu (lookup), ô tích, luật bật/tắt ô theo lựa chọn. Nếu đạt, cùng đường này dùng lại cho hàng chục hộp thoại nhập liệu khác. **Không** nhằm thay thế chức năng lập phiếu thu thật.

## 2. Cách làm

1. Đọc `AELAYOUT` của form "Phiếu thu" thẳng từ DB, tách thành mô tả: danh sách control (loại, tên, chữ, vị trí gốc, thuộc tính), bảng ánh xạ trường (`DataField`, nhãn, ô nhập, `AllowEmpty`), bảng đích (`TTHUCHI`).
2. Trình hiển thị chung dựng ô nhập theo từng loại, **xếp lại theo hàng** (gom các control cùng hàng theo tọa độ gốc, giữ thứ tự trái → phải và tỷ lệ độ rộng) thay vì chép tọa độ tuyệt đối; màu/cỡ chữ theo theme và DPI.
3. Luật của riêng form (bật/tắt ô, điền tên/địa chỉ, lọc danh sách) viết tay trong một lớp thuần để kiểm thử, theo `CODE` của form (mục 3).

## 3. Luật đọc từ code của form (không suy đoán)

- **Lý do thu:** chỉ liệt kê `DLYDOTHUCHI` có `LALYDOTHU = 30`.
- **Loại đối tượng** (ô chọn 4 mục: Đối tượng khác, Nhân viên, Khách hàng, Nhà cung cấp): mục 0 bật "Tên đối tượng" và "Địa chỉ"; mục 1 bật "Nhân viên"; mục 2 bật "Khách hàng"; mục 3 bật "Nhà cung cấp"; các ô còn lại tắt. Ô Nhân viên, Khách hàng, Nhà cung cấp lúc đầu tắt.
- **Chọn nhân viên:** "Tên đối tượng" = tên nhân viên, "Địa chỉ" để trống. **Chọn khách hàng / nhà cung cấp:** "Tên đối tượng" = tên, "Địa chỉ" = địa chỉ của họ. Bỏ chọn thì xóa cả hai ô.
- **Chuyển khoản:** tích "Chuyển vào tài khoản" thì bật ô "Tài khoản ngân hàng" (và mở danh sách); bỏ tích thì tắt ô đó.
- **Kiểm tra khi lưu:** tích chuyển khoản mà chưa chọn tài khoản ngân hàng thì báo "Mời bạn chọn tài khoản ngân hàng" và không lưu. Ba trường **bắt buộc** theo ánh xạ: Số phiếu, Số tiền, Cửa hàng.
- **Phiếu mới:** loại đối tượng = 0 (Đối tượng khác); Cửa hàng = cửa hàng hiện tại. Cửa hàng chỉ liệt kê các cửa hàng của người dùng nếu không phải admin.
- **Ngày:** bị khóa nếu người dùng không có quyền xem chức năng "Thay đổi ngày trong phiếu thu chi" (admin luôn được).

## 4. Phạm vi

**Làm:** mở được hộp thoại từ menu **Quỹ ▸ Tạo phiếu thu**; hiển thị đủ trường theo layout gốc; đủ luật mục 3; nút *Lưu* chạy kiểm tra (mục 3) rồi báo "bản thử, chưa ghi dữ liệu"; nút *Đóng*.

**KHÔNG làm** (thuộc phiếu thu thật, nghiệp vụ tiền/công nợ, chưa phân tích):
- Ghi vào `TTHUCHI`, tự sinh **Số phiếu** (mẫu dữ liệu: `PT20/00001`; định dạng nằm ở nhóm cấu hình "Số phiếu" đang ẩn), cập nhật công nợ/thẻ trả trước, ý nghĩa "Không thay đổi công nợ".
- Phiếu chi (`Tạo phiếu chi`), danh sách phiếu thu chi, các chế độ mở từ form khác (nạp thẻ, tạm ứng theo đơn hàng).

## 5. Tiêu chí nghiệm thu

- [x] Layout đọc từ DB cho ra đúng 21 dòng ánh xạ, bảng đích `TTHUCHI`, 3 trường bắt buộc, 4 mục của ô "Loại đối tượng".
- [x] Mọi trường có ô nhập trong layout đều hiện trên màn (đủ 17 ô nhập và ô tích; thiếu/thừa là lỗi).
- [x] Các luật ở mục 3 đúng theo kiểm thử (lớp luật thuần) và khi thao tác trên màn.
- [x] Danh sách lý do thu bằng đúng số dòng `LALYDOTHU = 30` trong DB; danh sách cửa hàng của người không phải admin chỉ gồm cửa hàng được gán.
- [x] Không có câu lệnh ghi nào chạy; không DDL.
- [ ] Đổi bảng màu có tác dụng ngay trên màn đang mở; ảnh chụp bằng `--render-screen` để Chiến so với form gốc.

## 6. Câu hỏi mở (không chặn bản thử)

1. Ngày mặc định của phiếu mới (bản thử dùng hôm nay).
2. Số phiếu tự sinh theo định dạng nào và khi nào (khi mở hay khi lưu) — cần spec nhóm cấu hình "Số phiếu".
3. "Không thay đổi công nợ" và "Loại phiếu thu công nợ" tác động thế nào tới công nợ — cần đọc form công nợ.

## 7. Kết quả bản thử (2026-11-19)

**Đường làm chạy được trên form này.** `SFORM.AELAYOUT` đọc thẳng từ DB, tách thành mô tả (21 dòng ánh xạ, 3 trường bắt buộc, 17 ô nhập, 4 mục của ô chọn, bảng đích `TTHUCHI`), rồi dựng thành màn có theme. Mở được từ menu Quỹ ▸ Tạo phiếu thu ("Tạo phiếu chi" vẫn là tab tạm). Selftest: 211 đạt, 3 lỗi cũ do dữ liệu POS TAP, không phải lỗi mới.

**Kích thước công việc (đo, không ước):** bộ tách layout 281 dòng, trình hiển thị chung 507 dòng, đọc layout/danh sách chọn từ DB 138 dòng; riêng cho form Phiếu thu: luật thuần 76 dòng + hộp thoại nối luật vào màn 254 dòng. Phần tái dùng được cho form khác là bộ tách và trình hiển thị; phần phải viết tay cho mỗi form là luật và nối dữ liệu. `code.cs` của form gốc dài 171 dòng, ra khoảng 9 luật.

**Điều phát hiện, ảnh hưởng tới các form khác:**
- Tọa độ gốc không dùng nguyên được: cỡ chữ gốc nhỏ hơn nên nhãn dài và ô tích bị cắt. Phải đo chữ thật và đẩy cột ô nhập, đẩy ô kế bên; đã làm trong `DynamicForm`.
- Ô tra cứu (`lue*`) không ghi tên bảng trong layout; lấy từ `SCOLUMN` (cột Lookup → bảng tham chiếu). Bộ lọc riêng (lý do thu `LALYDOTHU = 30`, cửa hàng theo người dùng) nằm trong `code.cs`, phải viết tay dưới dạng điều kiện cố định, không ghép chuỗi từ dữ liệu.
- Control lạ trong layout được ghi vào danh sách `Skipped` (với form này rỗng); form khác (Krypton Navigator, lưới, ToolStrip) sẽ cần thêm loại.
- Thông báo lỗi của framework gốc (trường bắt buộc) chưa biết nguyên văn: bản thử dùng câu tự đặt "Mời bạn nhập: …"; riêng thông báo tài khoản ngân hàng lấy nguyên văn từ code gốc.

**Chưa kiểm:** thao tác bằng chuột/bàn phím trên máy thật (chỉ có ảnh chụp và kiểm thử); đổi bảng màu khi hộp thoại đang mở; hiển thị ở 125%/150% DPI; người dùng không phải admin (khóa ô Ngày, lọc cửa hàng) mới được kiểm ở tầng dữ liệu, chưa thử trên màn; chỉ thử đọc layout của đúng form Phiếu thu, chưa thử 132 layout còn lại.
