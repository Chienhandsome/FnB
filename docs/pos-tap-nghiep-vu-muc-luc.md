# POS TAP — mục lục nghiệp vụ rút từ mã nguồn export

Nguồn: `POS_TAP_SRC_EXPORT/` (gitignored). Đây là **ghi chú đầu vào cho `/spec`**, chưa phải spec đã duyệt. Mọi công thức đều trích từ mã gốc; chỗ nào nghi là lỗi của mã gốc đã ghi rõ để Chiến quyết "giữ y hệt" hay sửa.

## 1. Các tài liệu

| Tài liệu | Nội dung |
|---|---|
| [pos-tap-nghiep-vu.md](pos-tap-nghiep-vu.md) | Quy ước chung (cờ 30/0, cấu hình), **form `Hóa đơn bán hàng`** (bán lẻ): tính tiền, giá, khuyến mại, thanh toán, vật tư, điểm tích lũy, lưu tạm; hộp thoại nhỏ |
| [pos-tap-nghiep-vu-nha-hang.md](pos-tap-nghiep-vu-nha-hang.md) | **Hóa đơn nhà hàng / karaoke**: mở bàn, số hóa đơn, tiền giờ, giá theo khu vực và giờ, khuyến mại (mua 1 tặng 1), tổng bill, thanh toán gộp nhóm, in |
| [pos-tap-nghiep-vu-may-tinh-bang.md](pos-tap-nghiep-vu-may-tinh-bang.md) | **Máy chủ HTTP cho tablet** (cổng 6868): các API, quyền, chuyển/gộp/hủy, thanh toán đơn giản, bếp |
| [pos-tap-nghiep-vu-thanh-toan-quan-ly.md](pos-tap-nghiep-vu-thanh-toan-quan-ly.md) | Form `Xác nhận thanh toán`, `Quản lý bán hàng` (sửa/hủy bill sau thanh toán) |
| [pos-tap-nghiep-vu-ban-khu-vuc.md](pos-tap-nghiep-vu-ban-khu-vuc.md) | Màn hình sử dụng dịch vụ, chuyển/gộp bàn trên desktop, trạng thái bàn, kiểm soát order |
| [pos-tap-nghiep-vu-dat-phong-in-bep.md](pos-tap-nghiep-vu-dat-phong-in-bep.md) | Đặt phòng/đặt hàng, lịch Gantt, in xuống bếp/pha chế |
| [pos-tap-nghiep-vu-kho.md](pos-tap-nghiep-vu-kho.md) | Nhập / xuất / chuyển / kiểm kê, tồn kho, tồn đầu, tính lại giá vốn |
| [pos-tap-nghiep-vu-thu-chi-cong-no.md](pos-tap-nghiep-vu-thu-chi-cong-no.md) | Phiếu thu / chi / thu công nợ, công nợ đầu, tồn quỹ, lý do thu chi |
| [pos-tap-nghiep-vu-khach-hang-the.md](pos-tap-nghiep-vu-khach-hang-the.md) | Thẻ trả trước, tích lũy điểm, voucher, nhóm khách |
| [pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md](pos-tap-nghiep-vu-danh-muc-gia-khuyen-mai.md) | Loại mặt hàng, định lượng, bảng giá tiền giờ, đợt khuyến mại |
| [pos-tap-nghiep-vu-khu-vuc-ban-bao-gia.md](pos-tap-nghiep-vu-khu-vuc-ban-bao-gia.md) | Khai báo khu vực, bàn, báo giá |
| [pos-tap-nghiep-vu-luong.md](pos-tap-nghiep-vu-luong.md) | Tính lương theo công và theo máy chấm công, chi lương |
| [pos-tap-nghiep-vu-bao-cao.md](pos-tap-nghiep-vu-bao-cao.md) | 36 báo cáo (SQL), thống kê trên form, lãi gộp, kết quả kinh doanh, kho, quỹ, công nợ |
| [pos-tap-nghiep-vu-quan-tri-tien-ich.md](pos-tap-nghiep-vu-quan-tri-tien-ich.md) | Nhập dữ liệu từ DB cũ, đánh lại mã hàng, xuất lại vật tư, import định lượng, lưu vết, tài khoản |

## 2. Mô hình dữ liệu cốt lõi (rút ra)

- `TDONHANG` / `TDONHANGCHITIET` chứa **mọi chứng từ** theo `LOAI`: `0` bán hàng, `1` nhập hàng, `2` xuất khác, `3` chuyển kho, `4` kiểm kê, `97` tồn đầu, `98` công nợ đầu nhà cung cấp, `99` công nợ đầu khách; `5`, `6` xuất hiện trong báo cáo doanh thu (chưa rõ nguồn).
- Số lượng dòng có cặp "chưa quy đổi" (đơn vị người dùng) và "đã quy đổi" (đơn vị cơ sở); bán = `SLXUAT`, trả/nhập = `SLNHAP`.
- Bàn → bill đang mở qua `DBAN.TDONHANGID`; nhóm gộp phòng qua `TDONHANG.NHOMGUID`; combo cha–con qua `COMBOID`/`COMBOPARENTID`; vật tư xuất tự sinh có `XUATVATTU=30`; dòng khuyến mại tự sinh có `DMATHANGID='KM'`.
- Công nợ khách = Σ(`TONGCONG − TIENTHANHTOAN`) bill (`LOAI 0` đã thanh toán, `99`) − Σ(`THU − CHI`) phiếu không đánh `KHONGTHAYDOICONGNO`; nhà cung cấp tương tự với `LOAI 1, 2, 98` và `CHI − THU`.
- Tồn kho = Σ(`SLNHAP − SLXUAT`) theo kho, chỉ tính `LOAI ≠ 0` hoặc bill bán đã thanh toán.
- Loại mặt hàng `DLOAIMATHANGID`: `0` kiêm vật tư, `1` định lượng, `2` nguyên liệu, `3` mặt hàng mở, `4` combo, `5` combo mở, `7` dịch vụ theo giờ. Loại đồ `DLOAIDOID`: `0` ăn, `1` uống, `2` dịch vụ, `3` khác, `4` nguyên liệu.

## 3. Phạm vi đã đọc

Đã đọc kỹ: toàn bộ các form bán hàng (bán lẻ, nhà hàng, tablet), thanh toán, quản lý bán hàng, kho (5 loại phiếu + tồn + giá vốn), thu chi / công nợ / quỹ, thẻ / tích lũy / voucher, khuyến mại, bảng giá giờ, đặt phòng, in bếp, chuyển / gộp bàn, lương (2 cách), 36 báo cáo, các form khai báo chính trong `STABLEDESC` (mặt hàng, khu vực, bàn, bảng giá, báo giá, đợt KM, thẻ trả trước, nhân viên, nhóm khách…), tiện ích quản trị.

**Chưa đọc (chủ yếu giao diện hoặc hộp thoại nhỏ, ít nghiệp vụ):** `Thiết kế giao diện khu vực` (1463 dòng) và `…bàn`, `Thêm bàn vào giao diện`, `Thêm nhanh bàn`, các bản cảm ứng `ts_code.cs` (đã kiểm: bản cảm ứng của `Hóa đơn nhà hàng` dùng lại cùng hàm tính toán và ghi dữ liệu như bản desktop, khác phần giao diện), `Màn cảm ứng`, `Màn hình phụ`, `Chọn bàn`, `Chọn loại giá`, `Chọn máy in`, `Cấu hình máy in theo khu vực`, `Chọn phòng`, `Chọn kích thước nhập kho`, `Nhập hạn sử dụng`, `Chọn hóa đơn tạm`, `Tìm kiếm đặt trước / mặt hàng`, `Thay đổi giờ vào`, `Lịch sử mua bán`, `Phân tích kiểm kê`, các form danh sách `Quản lý nhập / xuất / chuyển kho / kiểm kê`, `Sinh dữ liệu`, `Bản quyền máy tính bảng`, `Sửa dòng hóa đơn`, `Giảm giá theo nhóm`, `Đặt số lượng / ghi chú`, `Nhập lý do`, `Nhập mật khẩu giảm đồ`, `Chi tiết combo`, `Chi tiết bán hàng theo mặt hàng`, một số `STABLEDESC` nhỏ (`DCUAHANG`, `DNHOMMATHANG`, `DLOAIPHONG`, `DCALAMVIEC`, `TSUACHUA`), toàn bộ `designcode.cs` và `layout.xml` (bố cục giao diện và mẫu in FastReport).

**Không có trong export — nằm trong exe/DLL, cần decompile nếu muốn đọc:** `Shared.TinhGia` (công thức tiền giờ, quan trọng nhất), `Shared.DuLieuHoaDonOk`, `DbUtils` (sinh số hóa đơn / mã hàng, kiểm quyền), định nghĩa `SystemConfig` (tên, mặc định từng khóa cấu hình), các enum (`LoaiMatHang`, `LoaiDo`, `CachTinhGiaGio`, `GiaMatHang`, `Functions` = danh sách quyền, `LoaiLuuVet`…), phân quyền nhóm người dùng, `Application.exe` / `ApplicationTs.exe` (khung ứng dụng, menu, đăng nhập, bản quyền), `No1Lib.*` (engine form động, theo ghi nhớ không có nghiệp vụ).

## 4. Điểm lệch / nghi lỗi của mã gốc (cần Chiến quyết giữ hay sửa)

1. **Làm tròn không thống nhất**: bán lẻ và các phiếu kho dùng `Math.Round` mặc định (làm tròn chẵn — banker's), nhà hàng dùng `AwayFromZero` cho tổng bill và tiền giờ.
2. **Giảm giá tổng khi có tiền giờ**: chia tiền giảm cho hàng bằng `TIENHANG × TIENGIAMGIATONG / TIENGIO` (mẫu số là tiền giờ, không phải tổng) — có vẻ sai.
3. **Chuyển bàn** trên tablet: chỉ "nâng giá" và tính thành tiền bằng đơn giá cũ; desktop tuân theo `GiaMatHangKhiChuyenBan` và tính đúng.
4. **Thanh toán tablet** bỏ qua chuyển khoản, thẻ, voucher, điểm, công nợ, số thứ tự, ngày giao dịch (chỉ tiền mặt).
5. **Tablet tính lại bill**: không áp làm tròn phút dịch vụ theo giờ; dùng chung cờ giảm theo tiền cho hàng và giờ; mục "giảm giá tiền giờ" ghi vào ô %.
6. Form tăng / giảm điểm (`TTANGGIAMDIEM`) chặn nhầm phiếu chỉ có điểm tăng (điều kiện viết nhầm `DIEMGIAM` hai lần).
7. Bill hủy lưu `TIENGIAMGIA = TILEGIAMGIA` (%); `DatGioChoMatHang.GioTinhLuong` trả giờ bắt đầu.
8. Báo cáo: biểu đồ tháng trong năm thiếu tháng 12 và đảo nhãn năm; lãi gộp giá nhập nhân `QUYDOI` ở bản này, chia ở bản kia; công nợ dùng dấu `CHI − THU` ở bản tổng hợp, `THU − CHI` ở bản theo kỳ (khách hàng); "Thực lãi" cộng thêm thuế / phí / tiền giờ; "Doanh thu" kết quả kinh doanh là `TONGCONG` (gồm thuế).
9. Kiểm tồn chuyển kho so sánh số lượng chưa quy đổi với tồn đơn vị cơ sở; thêm đơn vị chẵn vào dòng chuyển kho có sẵn nhân sai.
10. Bảo mật tablet: mật khẩu qua query string, SQL ghép chuỗi, quyền kiểm ở client — không nên sao chép.
11. Mật khẩu cố định trong mã tiện ích (`6789` nhập dữ liệu, `4321` đánh lại mã, `<mật khẩu admin>6666` xuất lại vật tư) và `SYSDBA/masterkey`.

## 5. Câu hỏi cần Chiến xác nhận trước khi viết spec

- Có viết lại **cả ba**: bán lẻ, nhà hàng/karaoke, tablet? (Memory ghi app đích là POS TAP với bản desktop + POS cảm ứng; tablet là máy chủ HTTP riêng.)
- Công thức tiền giờ (`Shared.TinhGia`) không đọc được: lấy từ app gốc bằng test đối chiếu hay decompile `Application.exe`?
- Loại phiếu `LOAI = 5, 6` là gì (ngoài những loại đã biết)? Nghiệp vụ "tặng theo trị giá" (`TANGTHEOTRIGIA`) áp dụng ở đâu?
- Giữ hay sửa các điểm lệch ở mục 4 (tiền / giá / kho / công nợ là lõi, theo CLAUDE.md không được đoán).

## 6. Phạm vi đã chốt (Chiến, 2026-11-19)

- **Bỏ** karaoke và toàn bộ tính tiền giờ: `TDONHANGGIO`, bảng giá giờ (`DBANGGIA`), `Shared.TinhGia`, dịch vụ theo giờ (`DLOAIMATHANGID=7`), combo hát, tiền mở bàn, ngắt giờ, giá theo khung giờ. Các mục tương ứng trong các file trên chỉ để tham khảo.
- **Để sau**: app Android / máy chủ tablet ([pos-tap-nghiep-vu-may-tinh-bang.md](pos-tap-nghiep-vu-may-tinh-bang.md)).
- **Làm**: bán lẻ, nhà hàng theo bàn (không giờ), kho, thu chi/công nợ, khuyến mại, thẻ/tích lũy, báo cáo, lương.
