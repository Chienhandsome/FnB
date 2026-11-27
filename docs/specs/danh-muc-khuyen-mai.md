# Spec: Danh mục đợt khuyến mại (DDOTKHUYENMAI) và áp dụng khi bán

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1). Chiến chốt 27/11: Mua x tặng y theo công thức bán lẻ; chặn lưu đợt trùng; cho chạy hai loại gốc không chạy (Tặng theo trị giá, Giảm tiền theo sản phẩm). Điểm **TẠM** ở §9.
- Ngày: 2026-11-27. Loại: **nặng** (giảm giá, tặng hàng ảnh hưởng tiền bán). Nguồn: `docs/deep/khach-hang.md` §4 (khuyến mại), `su-dung-dich-vu.md` (cách bán hàng tính tiền, `OrderCalculator`).
- Menu: KHÁCH HÀNG ▸ Danh mục đợt khuyến mại (menu có sẵn trong `SMENU`).
- Chia **hai patch**: (P1) Danh mục đợt khuyến mại (xem, thêm, sửa, xóa, chặn trùng); (P2) Áp dụng khi bán ở Sử dụng dịch vụ (tính giảm giá, dòng tặng, hiển thị).

## 1. Phạm vi

| Làm | Không làm |
|---|---|
| P1: danh mục đợt, hộp thêm/sửa theo từng loại, chặn đợt trùng. P2: áp dụng 9 loại khi bán ở Sử dụng dịch vụ (tự tính lại khi đổi dòng) | Khuyến mại theo giờ hát/tiền giờ (`TILEGIAMGIATIENGIO`, `KHUYENMAIGIOHAT`, `TILEGIAMGIAGIODAU`: bỏ cùng tính tiền giờ), khung giờ trong ngày (`TUGIO`/`DENGIO`: lưu nhưng v1 không áp dụng, T3), báo cáo hàng khuyến mại, khuyến mại ở Báo giá/Khách đặt hàng/Quản lý bán hàng (sau) |

## 2. Loại khuyến mại (`DLOAIHINHKHUYENMAI`, nhận diện theo TÊN không dấu, không phân biệt hoa thường)

| Loại (từ khóa trong tên) | Cấu hình của đợt | Chi tiết (`DDOTKHUYENMAICHITIET`) |
|---|---|---|
| **Giảm giá tổng bill** ("tong bill") | Tỉ lệ giảm % ở đầu đợt (`TILEGIAMGIA`) | không có |
| **Giảm giá theo nhóm hàng** ("nhom hang") | không | mỗi dòng: Nhóm mặt hàng (`DNHOMMATHANGID`, chọn trên cây nhóm), `TILEGIAMGIA` % |
| **Giảm theo giá trị đơn hàng** ("gia tri don hang", không có "tang") | không | `GIATRIDONHANG` (từ mức), `TILEGIAMGIA` % |
| **Giảm theo số lượng mặt hàng** ("so luong", không có "mua"/"tang") | không | `SOLUONGMATHANG` (tổng số lượng từ), `TILEGIAMGIA` % |
| **Mua x tặng y** ("mua" và "tang") | không | `DMATHANGID` (mặt hàng mua), `SOLUONGMUA`, `SOLUONGTANG`, `DMATHANGTANGID` (mặt hàng tặng; để trống = tặng cùng món) |
| **Đồng giá** ("dong gia") | không | `DMATHANGID`, `GIABAN` (giá khuyến mại) |
| **Giảm giá % theo sản phẩm** ("%" hoặc "phan tram" và "san pham") | không | `DMATHANGID`, `TILEGIAMGIA` % |
| **Giảm giá tiền theo sản phẩm** ("tien theo san pham") | không | `DMATHANGID`, số tiền giảm **mỗi đơn vị** lưu ở `GIABAN` (T1) |
| **Tặng theo trị giá** ("tang theo tri gia") | không | `GIATRIDONHANG` (từ mức), `SOLUONGTANG`, `DMATHANGTANGID` |

Các loại có thể chưa có dòng trong `DLOAIHINHKHUYENMAI` của DB (DB mẫu mới có 5/9): hộp chỉ cho chọn loại hiện có, không tự thêm dòng loại. Selftest tự chèn loại cần thiết rồi dọn. Mọi `TILEGIAMGIA` trong khoảng (0; 100].

## 3. P1: màn Danh mục đợt khuyến mại

Khung `CatalogView` (lưới danh sách đợt): cột Tên đợt, Loại hình, Từ ngày, Đến ngày, Trạng thái ("Đang áp dụng", "Chưa tới", "Hết hạn", "Ngừng áp dụng"), Ghi chú; lọc theo loại và trạng thái; tìm không dấu; xuất Excel; quyền "Danh mục đợt khuyến mại" (Xem, Thêm, Sửa, Xóa).
- **Hộp thêm/sửa:** Tên đợt (bắt buộc, không trùng), Loại hình (bắt buộc; không đổi sau khi lưu nếu đã có chi tiết), Từ ngày, Đến ngày (không trước Từ ngày), tích Ngừng áp dụng (`NGUNGAPDUNG`), Ghi chú; phần dưới đổi theo loại (§2): ô tỉ lệ cho Giảm tổng bill, hoặc lưới chi tiết (thêm/xóa dòng, chọn mặt hàng/nhóm, kiểm theo loại). Mỗi dòng chi tiết: đủ trường bắt buộc của loại, số > 0; mặt hàng/nhóm trong một đợt không lặp; Mua x tặng y: số lượng mua, tặng > 0.
- **Chặn đợt trùng (Chiến chốt):** không cho lưu đợt **không ngừng áp dụng** có cùng loại, khoảng ngày giao nhau với một đợt khác đang không ngừng áp dụng: "Đợt 'X' cùng loại đã áp dụng trong khoảng ngày này". Đợt không có Đến ngày xem như không giới hạn. Ngừng áp dụng một đợt không bị kiểm trùng; bật lại thì kiểm.
- **Xóa:** xóa cứng đợt và chi tiết trong một transaction (đợt đã dùng cho hóa đơn cũ vẫn xóa được vì hóa đơn lưu sẵn kết quả; không bảng nào tham chiếu `DDOTKHUYENMAI`, kiểm lại bằng schema), ghi lưu vết. Một transaction cho đầu đợt và chi tiết; lỗi rollback; chuỗi ghi vào cột giới hạn cắt cho vừa.

## 4. P2: áp dụng khi bán (Sử dụng dịch vụ)

Đợt **đang hiệu lực** khi `NGUNGAPDUNG` ≠ 30, ngày bán nằm trong [Từ ngày, Đến ngày] (so theo ngày, thiếu mốc nào thì không giới hạn phía đó). Mỗi khi đổi dòng món, tính lại. Cùng lúc chỉ có tối đa một đợt hiệu lực cho mỗi loại (đã chặn trùng ở §3); nhiều loại khác nhau cùng hiệu lực thì áp **từng loại độc lập**, nhưng **mỗi dòng/hóa đơn chỉ nhận mức giảm lớn nhất** trong các loại giảm % áp lên nó (không cộng dồn).

Cách áp từng loại (theo bán lẻ):
1. **Giảm giá % theo sản phẩm** và **theo nhóm hàng**: dòng của mặt hàng (hoặc thuộc nhóm, kể cả nhóm con) nhận `TILEGIAMGIA` của đợt vào giảm giá dòng.
2. **Giảm giá tiền theo sản phẩm** (T1): đơn giá hiệu dụng của dòng trừ số tiền giảm mỗi đơn vị (không dưới 0); quy ra giảm giá dòng theo % bằng số tiền giảm / đơn giá.
3. **Đồng giá:** đơn giá hiệu dụng của dòng = `GIABAN` của đợt khi nhỏ hơn đơn giá đang bán (quy ra giảm giá dòng).
4. **Giảm giá tổng bill, theo giá trị đơn hàng, theo số lượng:** giảm giá **tổng hóa đơn** theo %: tổng bill dùng `TILEGIAMGIA` của đợt; theo giá trị lấy dòng chi tiết có `GIATRIDONHANG` lớn nhất ≤ tiền hàng (sau giảm giá dòng); theo số lượng lấy dòng có `SOLUONGMATHANG` lớn nhất ≤ tổng số lượng mặt hàng trên bill. Chỉ áp khi lớn hơn giảm giá tổng người dùng đã nhập tay (người dùng nhập tay giữ quyền ưu tiên nếu lớn hơn).
5. **Mua x tặng y:** với mỗi dòng của mặt hàng mua, số tặng = phần nguyên(SL / `SOLUONGMUA`) × `SOLUONGTANG` (**công thức bán lẻ, Chiến chốt**); thêm dòng tặng 0 đồng của `DMATHANGTANGID` (hoặc cùng món nếu để trống) đánh dấu là dòng khuyến mại; đổi số lượng mua thì cập nhật hoặc xóa dòng tặng; dòng tặng vẫn **trừ kho** như dòng bán (cùng chặn âm kho) và không tính tiền.
6. **Tặng theo trị giá** (Chiến chốt chạy): tiền hàng ≥ mức `GIATRIDONHANG` lớn nhất đạt thì thêm dòng tặng 0 đồng `SOLUONGTANG` × `DMATHANGTANGID`; dưới mức thì bỏ dòng tặng.
- Dòng và hóa đơn hiển thị rõ nguồn: cột/nhãn "KM: tên đợt" hoặc biểu tượng trên dòng nhận khuyến mại; người dùng sửa tay giảm giá dòng thì giữ mức lớn hơn giữa tay và khuyến mại. Quyền "Giảm giá" hiện có vẫn áp cho phần tay.
- Công thức tiền vẫn theo `OrderCalculator` (§3.6): khuyến mại chỉ đổi đầu vào (giảm giá dòng, giảm giá tổng, thêm dòng 0 đồng), không thêm công thức tiền thứ hai.
- Hóa đơn đã thanh toán **không** tự tính lại khi đợt đổi (kết quả lưu sẵn trên dòng).

## 5. Nghiệm thu P1 (selftest trên bản sao DB, tự tạo và dọn dữ liệu)

1. Thêm đợt từng loại ở §2 với chi tiết hợp lệ: có `DDOTKHUYENMAI` và `DDOTKHUYENMAICHITIET` đúng cột; thiếu tên, thiếu loại, Đến ngày trước Từ ngày, tỉ lệ ngoài (0;100], dòng chi tiết thiếu mặt hàng hoặc lặp mặt hàng: chặn có thông báo.
2. Chặn trùng: hai đợt cùng loại giao ngày bị chặn; khác loại, hoặc hai khoảng ngày rời nhau, hoặc đợt thứ hai ngừng áp dụng thì lưu được; sửa chính nó không tự báo trùng; bật lại đợt đang ngừng làm trùng thì chặn.
3. Sửa đợt: chi tiết cũ ghi lại đủ, người và giờ sửa cập nhật; không đổi được loại khi đã có chi tiết.
4. Xóa đợt: mất đợt và chi tiết trong một transaction, có lưu vết; lỗi giữa chừng rollback (hook).
5. Trạng thái hiển thị đúng (đang áp dụng, chưa tới, hết hạn, ngừng); lọc, tìm không dấu, xuất Excel đúng cột; quyền chỉ Xem thì hộp chỉ đọc; số lượng tên 255 ký tự không làm lưu vết tràn; menu mở màn thật; không đổi cấu trúc CSDL; ca selftest cũ giữ nguyên tên.

## 6. Nghiệm thu P2 (bằng số cụ thể)

1. Giảm % theo sản phẩm 10% cho món A (giá 50.000, SL 2): giảm giá dòng 10%, thành tiền 90.000; món khác không đổi. Theo nhóm: món thuộc nhóm con cũng nhận.
2. Giảm tiền theo sản phẩm 5.000/đơn vị: món A SL 2 giảm 10.000 tổng; không xuống dưới 0. Đồng giá 30.000 cho món 50.000: thành tiền 60.000 cho SL 2; đồng giá cao hơn giá bán không tác dụng.
3. Giảm tổng bill 10%: tổng hóa đơn giảm 10% tiền hàng; theo giá trị đơn hàng với mức 200.000 (5%) và 500.000 (8%): tiền hàng 520.000 → 8%, 250.000 → 5%, 150.000 → không giảm; theo số lượng tương tự. Người dùng nhập tay giảm giá tổng lớn hơn thì giữ tay.
4. Mua 2 tặng 1: mua 5 được tặng 2 (dòng 0 đồng, đúng món tặng); mua 1 không tặng; giảm số lượng từ 5 xuống 3 thì dòng tặng còn 1; xóa món mua thì xóa dòng tặng; dòng tặng trừ kho (kho không cho âm thì chặn khi không đủ tồn).
5. Tặng theo trị giá: tiền hàng 300.000 ≥ 200.000 tặng 1 món; 150.000 không tặng; vượt cả hai mức thì chọn mức lớn nhất đạt.
6. Chọn mức giảm lớn nhất giữa các loại giảm % áp cùng dòng; không cộng dồn; đợt ngừng, hết hạn, chưa tới ngày không áp dụng; hóa đơn đã thanh toán không đổi khi đợt đổi.
7. Công thức tiền vẫn qua `OrderCalculator`: tổng, thuế, phí dịch vụ đúng theo spec bán hàng sau khi có khuyến mại; ca selftest cũ của bán hàng không đổi kết quả khi không có đợt nào hiệu lực.

## 7. Giao diện

Hộp đợt gọn, hạn chế cuộn dọc, cảm ứng; lưới chi tiết theo loại, tiêu đề cột đủ rộng; màu qua `ThemeManager`; dòng nhận khuyến mại có nhãn rõ nhưng không làm lệch cột.

## 8. Quyền và dữ liệu

Không đổi cấu trúc CSDL. Bảng: `DDOTKHUYENMAI` (`NAME`, `NOTE`, `DLOAIHINHKHUYENMAIID`, `TUNGAY`, `DENNGAY`, `NGUNGAPDUNG`, `TILEGIAMGIA`, cột chuẩn), `DDOTKHUYENMAICHITIET` (`DDOTKHUYENMAIID`, `DNHOMMATHANGID`, `TILEGIAMGIA`, `DMATHANGID`, `GIABAN`, `GIATRIDONHANG`, `SOLUONGMATHANG`, `SOLUONGMUA`, `SOLUONGTANG`, `DMATHANGTANGID`, cột chuẩn), `DLOAIHINHKHUYENMAI`.

## 9. Điểm TẠM

- **T1** "Giảm giá tiền theo sản phẩm": gốc không có tab nhập và không áp dụng; bản mới lưu số tiền giảm mỗi đơn vị vào `GIABAN` của dòng chi tiết (chưa biết gốc định nghĩa cột nào).
- **T2** Nhận diện loại theo từ khóa trong TÊN (gốc có hằng số GUID, DB mẫu chỉ có 5 loại); nếu tên trong DB thật khác từ khóa thì đổi bảng từ khóa.
- **T3** Khung giờ `TUGIO`/`DENGIO` lưu nhưng v1 chưa áp dụng (gốc chỉ có ở nhà hàng).
- **T4** Mỗi dòng/hóa đơn nhận mức giảm % lớn nhất giữa các loại, không cộng dồn (gốc: lấy Max khi đợt trùng).
- **T5** Chưa áp dụng ở Báo giá, Khách đặt hàng, Quản lý bán hàng; hóa đơn đã thanh toán không tự tính lại.
