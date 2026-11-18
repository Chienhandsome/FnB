# Plan G1: Đăng nhập → Phân quyền → Khung màn hình chính

- Spec: `docs/specs/kazuko-tea-clone.md` (đã duyệt). Ưu tiên của Chiến: **đăng nhập trước**.
- Trạng thái: **chờ Chiến duyệt plan**.
- Base: commit G0 của Chiến trên `main`.

## Điều đã biết từ app gốc (đã xác minh khi làm G1.1)

- Đăng nhập: `SELECT ID, "PASSWORD", ISADMIN, NAME FROM SUSER WHERE USERNAME = @Name`; phải có **đúng 1 dòng** và mật khẩu **so trực tiếp dạng chữ thường** (cột `PASSWORD` không mã hóa). DES (`CryptService`) chỉ dùng cho việc khác.
- App gốc có **"mật khẩu chủ" ẩn** đăng nhập được mọi tài khoản. Bản viết lại **không làm lại**.
- `ISADMIN = 1` là admin. Không phải admin thì cần quyền chức năng "Đăng nhập" (`SFUNCTION.ID = '1'`) có `SGROUPROLE.MODE` khác 0, nếu không báo "Bạn không có quyền đăng nhập".
- `SGROUPROLE.MODE` là cờ bit: 0 khóa, 0x10 xem, 0x20 sửa, 0x40 thêm, 0x80 xóa.
- `TEMPLATE.FDB` (DB mẫu) có 103 bảng, nhiều hơn `DATA.FDB` 3 bảng.
- (Ghi chú cũ, đã sai) ~~Mật khẩu mã hóa DES.~~
- Người dùng gắn với cửa hàng qua `TNGUOIDUNGTHEOCUAHANG`; phân quyền qua `SGROUPUSER`, `SGROUPROLE`, `SFUNCTION`, `SREPORTROLE`.
- Chưa biết: khóa DES lấy từ đâu, điều kiện tài khoản hợp lệ (cột `STATUS`?), app gốc ghi gì khi đăng nhập (lưu vết, lần đăng nhập cuối).

## G1.1 Đăng nhập (làm trước)

1. **Tìm hiểu (đọc, không sửa gì):** đọc `LoginTs` và `CryptService` (mã decompile ở thư mục tạm) để chốt: cột `SUSER` dùng khi đăng nhập, cách so mật khẩu, nguồn khóa DES, điều kiện tài khoản bị khóa, dữ liệu ghi ra khi đăng nhập. Ghi kết quả bằng lời vào mục 4 của spec.
2. **Cấu hình DB:** đường dẫn `DATA.FDB` đặt trong `App.config` (khóa `DatabasePath`), không hard-code.
3. **Lớp Data:** `UserRepository` — lấy người dùng theo tên đăng nhập, lấy danh sách cửa hàng của người dùng. Chỉ đọc; SQL nằm trong lớp Data.
4. **Mật khẩu:** `PasswordService` tương thích app gốc (cùng thuật toán + khóa) để tài khoản cũ đăng nhập được. Mật khẩu chỉ giải mã/so trong bộ nhớ, **không ghi log, không hiển thị**.
5. **Nghiệp vụ:** `LoginService.Login(tên, mật khẩu)` → trả về phiên (`UserId`, tên, `IsAdmin`, cửa hàng) hoặc lỗi rõ ràng (sai tên/mật khẩu, tài khoản bị khóa).
6. **Giao diện:** `LoginForm` tự vẽ theo theme: ô tên, mật khẩu, nút Đăng nhập, chọn cửa hàng nếu người dùng thuộc nhiều cửa hàng; Enter để đăng nhập; dùng được bằng cảm ứng; high-DPI.
7. **Luồng khởi động:** `Program` mở `LoginForm` trước; đăng nhập thành công mới vào màn hình chính; hủy thì thoát.

**Acceptance G1.1**
- [ ] Đăng nhập được bằng tài khoản có sẵn trong `SUSER` của bản copy DB (Chiến tự nhập mật khẩu khi test).
- [ ] Sai mật khẩu / sai tên / tài khoản không hợp lệ bị từ chối với thông báo đúng.
- [ ] Không ghi gì vào DB ngoài những gì app gốc ghi khi đăng nhập (đối chiếu bước 1).
- [ ] Không có mật khẩu nào xuất hiện trong log, file hay màn hình.
- [ ] Build Any CPU (Prefer32Bit) xanh; chạy F5 vào được form đăng nhập.

## G1.2 Phân quyền

- Nạp quyền của người dùng khi đăng nhập (`SGROUPUSER` → `SGROUPROLE` → `SFUNCTION`; báo cáo qua `SREPORTROLE`); admin (`ISADMIN`) thấy tất cả.
- Một lớp `Permission` để màn hình hỏi "người này có quyền X không".
- Acceptance: cùng một tài khoản, chức năng ẩn/hiện giống app gốc.

## G1.3 Khung màn hình chính

- Menu ngang + thanh công cụ icon + tab tài liệu theo style tham chiếu, tự vẽ theo theme; menu dựng theo `SMENU` và lọc theo quyền G1.2.
- Chưa làm chức năng bên trong; mở tab trống cho mục chưa làm.

## G1.4 – G1.5 (sau)

- Cấu hình hệ thống (`SCONFIG`), rồi CRUD danh mục `D*`. Lập plan chi tiết khi xong G1.1–G1.3.

## Kiểm thử

- Mỗi bước build bằng MSBuild; chạy thử trên bản copy DB.
- Đề xuất thêm project test `Kazuko_FnB.Tests` (MSTest, net48) cho `PasswordService` và `LoginService`. Cần Chiến đồng ý vì thêm gói NuGet.

## Quyết định của Chiến (2026-11-18)

- **Project test:** chưa thêm; kiểm bằng tay + lệnh kiểm tra dòng lệnh. Thêm khi tới G2/G3.
- **Mật khẩu:** giữ DES như app gốc (dễ test, dùng chung DB).
- **Ghi nhớ đăng nhập + Tự động đăng nhập:** có. Mật khẩu ghi nhớ lưu trên máy, mã hóa bằng DPAPI (chỉ đúng tài khoản Windows đọc được).
- **Tab "Cơ sở dữ liệu":** có, chỉ chế độ **máy này** (embedded): chọn file DB, kiểm tra kết nối, **tạo DB mới từ `TEMPLATE.FDB`** như app gốc. Chế độ máy chủ Firebird để sau.
- **Giao diện màn đăng nhập** (theo ảnh mẫu KAZUKO GOLD): **toàn màn hình**, **appbar tự vẽ** phía trên; ảnh nền lớn phía sau (**tạm dùng nền gradient theo theme**, có ảnh thì thay); khung đăng nhập bên phải gồm tiêu đề "Đăng nhập", 2 tab (Đăng nhập / Cơ sở dữ liệu), ô tài khoản, mật khẩu (có nút hiện mật khẩu), 2 ô tick, nút Đăng nhập, link "Thoát", số phiên bản ở chân. Bỏ dòng mô tả, nhãn "Hệ thống nội bộ", dòng "Cổng bảo mật".
- Màu nút/tab lấy từ theme (đổi bảng màu thì màn đăng nhập đổi theo).
