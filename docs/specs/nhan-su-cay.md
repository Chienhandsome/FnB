# Danh mục nhân viên dạng cây: thư mục, thư mục con, phân cách, Thùng rác

- Trạng thái: **ĐÃ DUYỆT** (Chiến trả lời trong chat 28/11/2026: "Danh mục nhân viên làm dạng cây như app gốc"; xem `chien-chot-28-11.md` mục 3)
- Mở rộng `nhan-su-danh-muc.md` (các luật nhân viên, Cách tính lương, trùng tên/mã, chặn xóa khi đang dùng **giữ nguyên**). Chỉ thêm cấu trúc cây và Thùng rác. Không đổi cấu trúc DB.

## 1. Dữ liệu (đã đọc DB bản chép app gốc sau khi Chiến tạo thử, 28/11)
Bảng `DNHANVIEN` dùng các cột cây có sẵn:

| Loại dòng | `ITEMTYPE` | `NAME` | `PARENTID` | `PARENTDIR` | `SORTORDER` | `STATUS` |
|---|---|---|---|---|---|---|
| Nhân viên | `NULL` (giữ như cũ) | tên | id thư mục cha, rỗng/`NULL` nếu ở gốc | `<PARENTDIR của cha><AUTOID của cha>:`, rỗng nếu ở gốc | xếp sau anh em cùng cha (`ZZZb`, `ZZZc`...) | 30 |
| Thư mục | **1** | tên | như trên | như trên | như trên | 30 |
| Phân cách | **2** | `___________` (11 dấu gạch dưới) | như trên | như trên | như trên | 30 |
| Đã xóa vào Thùng rác | giữ nguyên | giữ nguyên | giữ nguyên | giữ nguyên | giữ nguyên | **0** |

Ví dụ thật của gốc: Thư mục 1 (`ITEMTYPE 1`, `AUTOID 1`, `PARENTDIR ''`); Thư mục 2 con của nó (`PARENTID` = id Thư mục 1, `PARENTDIR '1:'`); nhân viên nằm trong Thư mục 1 (`PARENTDIR '1:'`, `STATUS 0` khi đã vào Thùng rác). `AUTOID` = MAX + 1 toàn bảng. Làm đúng cách các repository dạng cây hiện có đã làm (`BookingMethodRepository`, `CustomerGroupRepository`) cho `PARENTDIR`, `SORTORDER`, `AUTOID`; không tự chế định dạng khác.

## 2. Màn Danh mục nhân viên
- Trái: cây **Tất cả** → thư mục → thư mục con (chỉ `ITEMTYPE = 1`, `STATUS = 30`) và nút **Thùng rác** (hiện khi có quyền Xóa). Chọn một thư mục hiện nhân viên của nó **và của thư mục con**; chọn Tất cả hiện toàn bộ nhân viên (kể cả nhân viên chưa có thư mục).
- Phải: lưới hiện tại (Mã, Tên, Điện thoại, ...) giữ nguyên cột và luật hiện có. Phân cách (`ITEMTYPE = 2`) hiện thành một dòng gạch trong lưới của thư mục chứa nó, không nhập liệu được, chỉ xóa được.
- Thanh công cụ: **Thêm mới ▾** gồm *Thêm nhân viên*, *Thêm thư mục*, *Thêm thư mục con* (mờ khi chưa chọn thư mục), *Thêm phân cách*; **Chỉnh sửa**, **Xóa**, **Làm mới** (như hiện tại). Nhân viên mới nằm trong thư mục đang chọn (Tất cả = ở gốc). Hộp Thêm/Sửa thư mục chỉ có ô **Tên** (bắt buộc, ≤ 255, không trùng tên thư mục anh em cùng cha, không phân biệt hoa thường và dấu).
- Mọi nơi khác của ứng dụng chọn/lọc nhân viên (ô chọn nhân viên ở phiếu chi, Tạm ứng, Thưởng phạt, Tính lương, Chấm công, Chi lương, Nhập kho, Sử dụng dịch vụ, báo cáo...) **chỉ lấy** `ITEMTYPE IS NULL OR ITEMTYPE = 0` và `STATUS = 30`: không bao giờ hiện thư mục, phân cách hay nhân viên trong Thùng rác. Phải tìm hết các truy vấn `DNHANVIEN` trong `Kazuko_FnB.Data` để sửa.

## 3. Xóa mềm và Thùng rác
- **Xóa** (phím Del): hỏi `Bạn có muốn chuyển (N) dữ liệu đang chọn tới thùng rác không? Lưu ý: Dữ liệu chưa bị xóa ra khỏi hệ thống, để xóa hẳn ra khỏi hệ thống bạn bấm Shift + Delete` (Có/Không). Có → `STATUS = 0` (nhân viên và phân cách; không đổi cột khác). Nhân viên đang được dùng ở dữ liệu khác **vẫn được** chuyển vào Thùng rác (không làm mất dữ liệu liên quan).
- **Thư mục:** xóa mềm chỉ khi thư mục rỗng (không còn mục `STATUS = 30` bên trong, kể cả thư mục con); còn thì chặn, nêu "Thư mục còn N mục bên trong".
- **Shift + Delete:** xóa hẳn khỏi hệ thống (hỏi xác nhận nêu rõ không khôi phục được); áp dụng luật chặn xóa khi nhân viên đang được dùng của `nhan-su-danh-muc.md` (thông báo nêu từng nơi dùng và số lượng); phân cách và thư mục rỗng xóa hẳn được.
- **Thùng rác:** nút mở danh sách `STATUS = 0` của bảng (nhân viên, thư mục, phân cách), có **Khôi phục** (về `STATUS = 30` tại vị trí cũ; nếu thư mục cha cũ đã nằm trong Thùng rác hoặc đã bị xóa hẳn thì khôi phục lên gốc) và **Xóa vĩnh viễn** (cùng luật chặn như Shift + Delete). Khôi phục bị trùng Tên hoặc Mã với nhân viên đang dùng (`STATUS = 30`) thì chặn và nêu rõ.
- Trùng Tên/Mã khi thêm hoặc sửa chỉ so với nhân viên `STATUS = 30` (nhân viên trong Thùng rác không tính).

## 4. Quyền
Dùng quyền hiện có của chức năng "Danh mục nhân viên": Thêm (thư mục, phân cách, nhân viên), Sửa (đổi tên thư mục, sửa nhân viên, Khôi phục), Xóa (Xóa, Shift+Del, Thùng rác, Xóa vĩnh viễn).

## 5. Tiêu chí selftest (mỗi ý một ca, tên ghi rõ)
1. Thêm thư mục, thư mục con, phân cách, nhân viên trong thư mục: đúng `ITEMTYPE` (1 / 1 / 2 / NULL), `PARENTID`, `PARENTDIR`, `SORTORDER`, `AUTOID` = MAX + 1, `STATUS 30`.
2. Chọn thư mục cha hiện nhân viên của cả thư mục con; Tất cả hiện hết; phân cách hiện thành dòng riêng.
3. Mọi truy vấn lấy nhân viên cho ô chọn/lọc ở nơi khác không trả thư mục, phân cách, nhân viên `STATUS 0`.
4. Xóa mềm: `STATUS = 0`, dữ liệu khác nguyên vẹn, nhân viên đang được dùng vẫn vào Thùng rác được; thư mục không rỗng bị chặn.
5. Thùng rác: Khôi phục đúng vị trí cũ, khôi phục lên gốc khi cha không còn, chặn khi trùng Tên/Mã; Xóa vĩnh viễn và Shift+Del chặn khi nhân viên đang được dùng, xóa được khi không dùng.
6. Trùng Tên/Mã không tính nhân viên trong Thùng rác; trùng tên thư mục anh em bị chặn.
7. Giao diện: màn mở được ở 1366x768 không cuộn dọc, cây và lưới đúng theo tiêu chí 2, nút Thêm mới ▾ đủ 4 mục, đổi theme không lỗi.
