# Spec: Gói demo UI (desktop, POS TAP)

- Trạng thái: **ĐÃ DUYỆT** (Chiến, 2026-11-19: D1 bốn bảng màu, D2 B1 chỉ xem, D4 dùng DEMO.FDB, D5 tính sau). **A và B1 đã code (chưa commit)**; C (kịch bản demo) chưa chạy trọn vẹn; DPI 125%/150% và phân quyền ẩn menu chưa kiểm.
- Ngày: 2026-11-19
- Base: `origin/main` @ `b0d15db` (đã `git fetch origin main`, không có commit mới hơn); nhánh `main` local đang ở `20aaee2`, đi trước origin
- Spec tổng: `docs/specs/kazuko-tea-clone.md` (mục 0 quyết định đã chốt, câu hỏi mở 11)
- Nguồn đã đọc: DB POS TAP (bản copy `DATA.fdb` và `DEMO.FDB`, chỉ đọc): `SMENU`, `STABLEDESC`, `SCOLUMN`, `SFORM`, `SUSER`, số dòng các bảng danh mục; ảnh tham chiếu `docs/specs/img/theme-tham-khao/`; mã đã có trong `Kazuko_FnB` (`Theme/`, `MainForm`, `ScreenFactory`, `AppSettings`); spec `danh-muc-khach-hang.md`.

## 1. Bối cảnh và mục tiêu

Sếp muốn **thấy UI mới càng sớm càng tốt** (Chiến, 2026-11-19), chưa có ngày cụ thể. Hiện khung chính, đăng nhập, phân quyền và nhóm Hệ thống đã chạy, nhưng mọi menu nghiệp vụ còn lại chỉ mở tab "sẽ được bổ sung", và DB đang dùng gần như rỗng.

Mục tiêu của gói này: **trong thời gian ngắn nhất có một bản chạy được, nhìn thấy UI mới trên dữ liệu thật**, theo kịch bản ở mục 6. Không phải nghiệp vụ lõi (tiền/giá/kho): phần đó vẫn làm sau theo thứ tự menu gốc.

Gói gồm ba việc, làm theo thứ tự:

- **A. Màn "Giao diện chương trình"** (đổi bảng màu toàn hệ thống).
- **B. Khung danh mục dùng chung**, mở được 16 menu danh mục bằng một màn dựng từ metadata của DB.
- **C. DB demo và kịch bản demo.**

## 2. Điều đã đo trên DB POS TAP

**Menu và màn hình** (`SMENU` đang bật: 77 mục):

| Loại | Số menu | Ghi chú |
| --- | --- | --- |
| Dựng từ bảng nhập liệu (`STABLEDESC`) | 24 | 8 mục `TABLETYPE = 0`, 9 mục `TABLETYPE = 2` (một mục là "Gửi tin nhắn tới khách hàng", không làm), 7 mục `TABLETYPE = 3` (phiếu thu/chi, nhập/xuất kho, khách đặt hàng, sửa chữa, thưởng phạt: **không** thuộc gói này) |
| Form riêng (`SFORM`) | 34 | Không thuộc gói này |
| Báo cáo | 2 | Không thuộc gói này |

`TABLETYPE = 2` là "cây nhóm bên trái + lưới bên phải" (đã xác minh ở spec Danh mục khách hàng). `TABLETYPE = 0` là danh mục phẳng và `3` là giao dịch: **suy từ tên menu, chưa xác minh bằng mã**.

**16 menu danh mục thuộc gói này** (cả 16 đều có metadata cột trong `SCOLUMN`, từ 2 đến 48 cột; số dòng đo trên DEMO.FDB):

| Kiểu | Danh mục (bảng) | Số dòng ở DEMO.FDB (tổng, chưa lọc `STATUS`) |
| --- | --- | --- |
| Phẳng (0) | Bảng giá (`DBANGGIA`) | 2 |
| | Ca làm việc (`DCALAMVIEC`) | 3 |
| | Cửa hàng (`DCUAHANG`) | 1 |
| | Kho hàng (`DKHOHANG`) | 1 |
| | Loại phòng (`DLOAIPHONG`) | 0 |
| | Lý do thu chi (`DLYDOTHUCHI`) | 23 |
| | Nhân viên (`DNHANVIEN`) | 13 |
| | Tài khoản ngân hàng (`DTAIKHOANNGANHANG`) | 3 |
| Cây + lưới (2) | Bàn, khu vực (`DBAN`) | 19 |
| | Đợt khuyến mại (`DDOTKHUYENMAI`) | 0 |
| | Khách hàng (`DKHACHHANG`) | 3 |
| | Mặt hàng (`DMATHANG`) | 581 |
| | Mật khẩu wifi (`DMATKHAUWIFI`) | 0 |
| | Nhà cung cấp (`DNHACUNGCAP`) | 1 |
| | Thẻ trả trước (`DTHETRATRUOC`) | 1 |
| | Voucher (`DVOUCHER`) | 0 |

**Hai DB mẫu của POS TAP rất khác nhau:** `DATA.fdb` gần như rỗng (0 khách, 0 nhân viên, 1 mặt hàng, 0 bàn), còn `DEMO.FDB` có dữ liệu (581 mặt hàng, 13 nhân viên, 19 bàn, 3 khách, 4 đơn hàng, 3 phiếu thu chi). Cả hai có tài khoản `Admin` (quản trị) với mật khẩu rỗng; DEMO có thêm `phucvu1`. App đang trỏ vào `DATA.fdb`, nên nếu chiếu cho sếp lúc này mọi màn danh mục sẽ trống: **demo phải dùng DEMO.FDB.**

## 3. Phạm vi

### Làm

**A. Màn "Giao diện chương trình"**
- Mục mới trong menu **Quản trị**, mở hộp thoại modal theo ảnh tham chiếu `docs/specs/img/theme-tham-khao/gold-man-chon-mau.webp`: lưới thẻ (tên, nhãn phong cách, đối tượng dùng, 4 chấm màu, mô tả), nút *Khôi phục mặc định*, *Đóng*, *Áp dụng & Lưu*.
- Bấm một thẻ = **xem thử ngay** trên toàn bộ app (menu, thanh công cụ, tab, lưới, hộp thoại đang mở). *Đóng* không lưu thì hoàn lại bảng màu cũ. *Áp dụng & Lưu* lưu và hiện thông báo ngắn (toast).
- **4 bảng màu, đều nền sáng** (Chiến chốt 4, bỏ 10; mặc định Cam Nắng; còn Cà Phê, Matcha, Xanh Biển); bỏ bảng "Nền tối" và cờ `IsDark` khỏi `BuiltInThemes.cs`. Tên, nhãn và mô tả tự đặt theo F&B (không dùng tên/mô tả của sản phẩm tham chiếu). Mỗi bảng đạt tương phản chữ/nền tối thiểu mức AA (kiểm bằng công cụ tính tương phản).
- Lưu theo người dùng Windows ở `%APPDATA%\KazukoFnB\settings.txt` (khóa `Theme`, đã có sẵn), tự nạp lại lần mở sau.

**B. Khung danh mục dùng chung (giai đoạn B1: chỉ xem)**
- Một màn dùng chung cho **16 menu ở mục 2**, thay tab "sẽ được bổ sung", đăng ký qua cơ chế `ScreenFactory` hiện có (id menu → màn).
- Bố cục: với danh mục kiểu cây, cây nhóm bên trái (có mục "Tất cả") và lưới bên phải; với danh mục phẳng, chỉ lưới. Có ô tìm (F3 nhảy vào ô tìm), nút Làm mới.
- Cột và nhãn đọc từ metadata (`SCOLUMN`) lúc chạy; chỉ hiện dòng đang dùng (`STATUS = 30`).
- **Chỉ đọc:** không có Thêm/Sửa/Xóa ở B1. Quyền Xem theo `SGROUPROLE` như khung hiện tại (menu bị khóa thì ẩn).
- Tầng `Data` mới cho metadata và đọc danh mục (SQL tham số hóa, không để SQL trong View/ViewModel).

**C. DB demo và kịch bản demo**
- Dùng bản copy `DEMO.FDB` đã có ở `Kazuko_FnB\Database\` (đã `.gitignore`). Mặc định mở DEMO khi chạy demo; phần dev/selftest vẫn dùng `DATA.fdb`.
- Chạy được kịch bản ở mục 6 trọn vẹn, không lỗi.

### KHÔNG làm (để gói/giai đoạn khác)
- **Thêm/Sửa/Xóa danh mục (B2):** ghi dữ liệu có quy tắc riêng từng danh mục (mã tự sinh, cấu hình bật/tắt cột, kiểm trùng, kiểm tham chiếu khi xóa). Làm sau, bắt đầu từ Khách hàng bằng spec `danh-muc-khach-hang.md` viết lại theo POS TAP.
- 34 menu form riêng, 7 menu giao dịch (`TABLETYPE = 3`), báo cáo, nghiệp vụ tiền/giá/kho/công nợ.
- Màn quản lý bán hàng nhà hàng (sơ đồ bàn, gọi món): chưa rõ sếp có cần trong demo không (câu hỏi D5). Nếu cần sẽ là gói riêng; DEMO có 19 bàn và 4 đơn để dùng.
- Bản POS cảm ứng, màn "CHỌN ỨNG DỤNG", sao lưu/phục hồi, đổi schema (không DDL).

## 4. Acceptance criteria

Kiểm trên **bản copy** DB, không đụng DB gốc.

**A. Giao diện chương trình**
- [x] Menu Quản trị có mục "Giao diện chương trình"; mở được hộp thoại với đúng 4 thẻ.
- [x] Không còn bảng "Nền tối" hay `IsDark` trong mã; cả 4 bảng đều nền sáng.
- [x] Bấm thẻ đổi màu ngay trên mọi cửa sổ đang mở (menu, toolbar, tab, lưới, hộp thoại), không cần khởi động lại.
- [x] *Đóng* không lưu thì giữ nguyên bảng màu cũ; *Áp dụng & Lưu* lưu; mở lại app vẫn đúng bảng màu; *Khôi phục mặc định* hoạt động.
- [x] Mọi bảng màu đạt tương phản chữ/nền tối thiểu AA (có kết quả kiểm kèm theo).
- [ ] Hiển thị đúng ở 100%, 125%, 150% DPI.

**B1. Khung danh mục dùng chung (xem)**
- [x] 16 menu ở mục 2 mở ra màn danh mục thật, không còn tab "sẽ được bổ sung".
- [x] Với mỗi danh mục: số dòng hiển thị bằng `SELECT COUNT(*) ... WHERE STATUS = 30` trên cùng DB (số 581 ở mục 2 là tổng dòng chưa lọc `STATUS`, nên con số hiển thị có thể nhỏ hơn).
- [x] Tìm lọc đúng trên các cột đang hiện; chọn nhóm trên cây thì lọc lưới theo nhóm; chọn "Tất cả" thì không lọc.
- [ ] Người dùng không có quyền Xem thì menu ẩn (giữ hành vi hiện tại).
- [x] Không có câu lệnh ghi nào chạy khi mở/duyệt/tìm (kiểm bằng selftest: số dòng và `TIMEMODIFIED` không đổi), không DDL.
- [ ] Mở màn Mặt hàng (573 dòng đang dùng) và cuộn mượt, không treo giao diện. Đã đo: đọc dữ liệu 21 ms (selftest); chưa thử cuộn bằng tay trên máy thật.

**C. Demo**
- [ ] Mở app trên DEMO.FDB, đăng nhập `Admin`, đi hết kịch bản mục 6 không lỗi, không cảnh báo bất thường.
- [ ] Chụp ảnh các màn chính bằng công cụ `--render-screen` kèm theo báo cáo để Chiến xem trước khi chiếu.

## 5. Kế hoạch implement (sau khi duyệt)

Mỗi bước là một patch nhỏ theo quy trình `kazuko-patch`: base `origin/main` mới nhất, build MSBuild (`Kazuko_FnB` là .NET Framework classic), `git apply --check` trên cây sạch, không commit khi chưa được Chiến bảo.

1. **A:** 4 bảng màu trong `Theme/BuiltInThemes.cs` (bỏ nền tối), hộp thoại chọn màu (theo `ThemedDialog`), đăng ký menu qua `ScreenFactory`, lưu `Theme` qua `AppSettings`; kiểm tương phản; chụp ảnh.
2. **B1:** tầng `Data` đọc metadata (`SCOLUMN`) và danh mục; màn danh mục dùng chung (cây + lưới + tìm); đăng ký 16 menu; mở rộng `SelfTest`.
3. **C:** chọn DEMO.FDB làm DB demo, chạy kịch bản, chụp ảnh, sửa lỗi phát hiện.

Vùng chạm dự kiến: `Kazuko_FnB/Theme/*`, `Kazuko_FnB/UI/Dialogs/*` (thêm màn mới), `Kazuko_FnB/Services/MainMenuBuilder.cs`, `Kazuko_FnB/Services/AppSettings.cs`, thư mục `Data/` (thêm lớp đọc), `Data/SelfTest.cs`, csproj (thêm `<Compile Include>`). Không migration DB.

## 6. Kịch bản demo (dự kiến)

1. Mở app, đăng nhập `Admin` trên DEMO.FDB.
2. Trang chủ "Truy cập nhanh" với giao diện mới.
3. Quản trị → Giao diện chương trình: bấm vài bảng màu, thấy đổi ngay toàn app; lưu một bảng.
4. Mở **Mặt hàng** (hàng trăm dòng, cây nhóm + lưới, tìm).
5. Mở **Khách hàng**, **Nhân viên**, **Bàn, khu vực**, **Nhà cung cấp**.
6. Mở nhóm Hệ thống: Người dùng và phân quyền, Cấu hình toàn hệ thống (đã có).
7. Đăng xuất / đăng nhập lại: bảng màu đã chọn vẫn còn.

## 7. Rủi ro

- **Metadata lưới (`SGRID`):** truy vấn nối `SGRID.GRIDID` với `STABLEDESC.ID` cho 0 dòng ở cả 16 bảng, nên chưa biết cách lấy thứ tự/độ rộng cột của app gốc. B1 có thể phải dùng thứ tự `SCOLUMN` và độ rộng tự co giãn; cần xác minh trước khi hứa "giống app gốc".
- **`TABLETYPE = 0` và `3` chưa xác minh bằng mã:** nếu danh mục phẳng có hành vi khác (ví dụ ràng buộc riêng), màn chung có thể không đủ.
- **Chất lượng dữ liệu DEMO:** là DB mẫu đi kèm POS TAP, chưa kiểm xem có dữ liệu thật của khách nào không; Chiến nên xem lướt tên nhân viên/khách trước khi chiếu cho sếp.
- **Phạm vi "toàn bộ UI mới":** gói này chỉ phủ khung, đổi màu và 16 danh mục (khoảng một phần ba số menu); 34 menu form riêng chưa có UI. Nếu sếp kỳ vọng thấy cả bán hàng, cần gói thêm (D5).
- **Bảng màu đẹp mà thiếu tương phản:** giảm thiểu bằng kiểm AA bắt buộc ở tiêu chí A.

## 8. Câu hỏi cần Chiến trả lời để duyệt

- **D1. Bảng màu (đã chốt):** Chiến chọn 4 bảng màu và để tôi đề xuất: Cam Nắng (mặc định, đã làm sẵn theo cam), Cà Phê, Matcha, Xanh Biển.
- **D2. B1 chỉ xem:** đồng ý làm Thêm/Sửa/Xóa ở giai đoạn sau (bắt đầu từ Khách hàng) để tránh ghi sai quy tắc vào DB, hay muốn có ngay trong demo? (Tôi đề xuất chỉ xem trước.)
- **D3. Quyền mở màn Giao diện:** mọi người dùng nhìn thấy menu Quản trị đều đổi được (vì chỉ lưu cục bộ theo máy, không đụng DB), hay phải gắn quyền riêng? (Tôi đề xuất không gắn quyền.)
- **D4. DB demo:** đồng ý dùng DEMO.FDB (581 mặt hàng, 13 nhân viên...) cho buổi demo?
- **D5. Màn bán hàng:** sếp có cần thấy sơ đồ bàn / gọi món trong demo không? Câu này quyết định có làm thêm gói thứ hai hay không.
