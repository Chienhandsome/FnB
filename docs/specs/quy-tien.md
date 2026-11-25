# Spec: Quỹ tiền (quỹ tiền mặt và quỹ tài khoản)

- Trạng thái: **NHÁP — chờ Chiến duyệt** (chưa code)
- Ngày: 2026-11-25. Base: `main` @ `954f133`
- Vai trò: **nguồn chuẩn** về việc giao dịch nào làm quỹ nào thay đổi. Các spec bán hàng, nhập hàng, thu chi, báo cáo theo spec này.

## 0. Nguồn và giới hạn

- **[CODE]** `POS_TAP_SRC_EXPORT`: form `Xác nhận thanh toán`, `Hóa đơn nhà hàng`, `Hóa đơn bán hàng`, `Phiếu thu`, `Phiếu chi`, `Phiếu nhập kho`, `Tồn quỹ`; tài liệu [`../deep/cong-no-quy.md`](../deep/cong-no-quy.md) (§b.7, §b.8, §b.10).
- **[DB]** bản sao `DATA.fdb`: `DLOAIQUY` có 3 loại (Tiền mặt, Ngân hàng, Tiền thẻ); `DTAIKHOANNGANHANG` có 3 dòng; `TTHUCHI` 0 dòng. `TDONHANG` và `TTHUCHI` đều có `CHUYENKHOAN`, `DTAIKHOANNGANHANGID`.
- **[CHƯA XÁC MINH]**: những gì khung app gốc (DLL) hoặc trigger DB làm ngầm với các cột tiền; chưa chạy thử giao dịch thật trên app gốc.

## 1. Bối cảnh

App gốc đã có màn **Tồn quỹ** chia Tiền mặt / Ngân hàng (theo tài khoản) / Quẹt thẻ. Không có bảng lưu số dư: số dư tính từ giao dịch. Nhưng việc ghi hình thức thanh toán chưa đầy đủ (§5), nên số quỹ có thể sai.

## 2. Mục tiêu

Mỗi đồng tiền thật ra hoặc vào được tính **đúng một lần** vào **đúng quỹ**; sửa hoặc xóa giao dịch thì số dư tự đúng theo.

## 3. Phạm vi

**Làm:** quy tắc ghi quỹ cho mọi giao dịch có tiền; chọn hình thức trả ở phiếu nhập; chuyển quỹ (nộp/rút); số dư đầu kỳ; màn Tồn quỹ.

**Không làm:** đổi cấu trúc DB; lưu số dư thành cột/bảng; đối soát sao kê ngân hàng; phí quẹt thẻ/phí chuyển khoản; sửa dữ liệu cũ tự động; thiết kế lại phiếu thu/chi, công nợ, lương ngoài phần hình thức tiền.

## 4. Nghiệp vụ

### 4.1 Hai quỹ

| Quỹ | Gồm | Xem chi tiết |
|---|---|---|
| **Tiền mặt** | tiền mặt | — |
| **Tài khoản** | chuyển khoản **và quẹt thẻ** | theo từng tài khoản `DTAIKHOANNGANHANG`; trong mỗi tài khoản xem riêng phần chuyển khoản và quẹt thẻ |

**Đã chốt (25/11):** quẹt thẻ thuộc quỹ Tài khoản (app gốc để quỹ riêng). Mọi giao dịch có chuyển khoản hoặc quẹt thẻ **bắt buộc chọn tài khoản**. Hóa đơn chỉ có một cột `DTAIKHOANNGANHANGID`, nên một hóa đơn vừa chuyển khoản vừa quẹt thẻ dùng chung một tài khoản.

**Số dư** = Σ thu − Σ chi của quỹ đó, tính từ **mốc số dư đầu kỳ** của quỹ (§4.4), gồm chính giao dịch số dư đầu kỳ. Tổng quỹ Tài khoản = Σ các tài khoản + dòng **"Chưa xác định tài khoản"** (giao dịch không ghi tài khoản, ví dụ hóa đơn chỉ quẹt thẻ ghi bởi app gốc: app gốc chỉ bắt chọn tài khoản khi có chuyển khoản [CODE]).

**Giao dịch được tính** (Q4): bản ghi đã xóa mềm (`STATUS = -30`) **không** tính, áp cho cả hóa đơn, phiếu nhập/xuất trả và phiếu thu/chi.

### 4.2 Giao dịch nào làm quỹ thay đổi

| Giao dịch | Tiền mặt | Tài khoản | Ghi chú |
|---|---|---|---|
| Hóa đơn bán đã thanh toán (`LOAI = 0`, `DATHANHTOAN = 30`) | + `TIENMAT` | + `CHUYENKHOAN` + `THE` | `TIENMAT` có thể âm (§4.3) |
| Phiếu thu (`TTHUCHI.THU`) | + nếu `CHUYENKHOAN` = 0/NULL | + nếu `CHUYENKHOAN` ≠ 0 | gồm thu nợ khách, đặt trước/tạm ứng của khách, nạp thẻ trả trước |
| Phiếu chi (`TTHUCHI.CHI`) | − nếu `CHUYENKHOAN` = 0/NULL | − nếu ≠ 0 | gồm trả nợ nhà cung cấp, chi lương, trả lại tiền đặt trước |
| Phiếu nhập (`LOAI = 1`) | − `TIENTHANHTOAN` nếu trả tiền mặt | − `TIENTHANHTOAN` nếu chuyển khoản | §4.5 |
| Xuất trả hàng nhà cung cấp (`LOAI = 2`, có `DNHACUNGCAPID`) | + `TIENTHANHTOAN` nếu tiền mặt | + `TIENTHANHTOAN` nếu chuyển khoản | **tiền vào** (Q3); chọn và đọc hình thức như §4.5 |
| Bill `LOAI` 5, 6 | tạm không tính | tạm không tính | chưa biết là loại gì (Q5); xem lại khi có dữ liệu thật, nếu chúng có thu tiền thì số quỹ đang thiếu |
| Chuyển quỹ | một bên − | bên kia + | §4.6 |
| Voucher, thẻ trả trước, trừ tích lũy, ghi nợ | không đổi | không đổi | không có tiền thật lúc đó |

**Chỉ tính một lần, lúc nhận tiền:** tiền đặt trước vào quỹ qua phiếu thu; khi đóng hóa đơn app gốc trừ số này khỏi số phải trả (`TONGTIEN = tổng + nợ cũ − đặt trước` [CODE]) nên không cộng lại. Nợ cũ thu ngay trên hóa đơn nằm trong `TIENMAT`/`CHUYENKHOAN`/`THE` của hóa đơn đó, không lập thêm phiếu thu.

**Hóa đơn âm** (đặt trước lớn hơn tiền phải trả): hóa đơn không đổi quỹ phần trả lại; app gốc mở phiếu chi "Trả lại tiền đặt trước" [CODE `TraLaiKhach`] và phiếu chi đó trừ quỹ. Nếu người dùng đóng phiếu chi mà không lưu, v1 **cảnh báo rõ và hỏi xác nhận**; xác nhận bỏ qua thì quỹ chưa trừ khoản này (Q7).

**Sửa / xóa:** số dư tính từ giao dịch nên tự đúng. Hủy hóa đơn xóa cứng bill nên tiền của bill rời quỹ; phiếu thu/chi liên quan **không** tự xóa.

### 4.3 Khách chuyển khoản dư, quán trả lại tiền mặt (đã chốt)

Ví dụ hóa đơn 480.000, khách chuyển 500.000, quán trả lại 20.000 tiền mặt → Tài khoản **+500.000**, Tiền mặt **−20.000**.

- Ghi: `CHUYENKHOAN = 500.000`, `TIENMAT = −20.000`, `TRALAI = 20.000`. Công thức chung: `TIENMAT = tiền khách đưa − tiền trả lại` (**cho phép âm**).
- App gốc: `TienMat = MAX(0, khách đưa − trả lại)` [CODE] và chỉ lập phiếu chi trong trường hợp hóa đơn âm, nên 20.000 này không được trừ ở đâu trong code đọc được.
- Hóa đơn cũ đã ghi theo cách gốc **không sửa** (Q8).
- **Q2 (đã chốt):** **không cho quẹt thẻ quá số phải trả** (thẻ ≤ số phải trả sau khi trừ các hình thức khác). Phần dư của voucher, thẻ trả trước, trừ tích lũy **không trả lại tiền mặt**. Tiền trả lại chỉ phát sinh từ tiền mặt khách đưa và chuyển khoản.

### 4.4 Số dư đầu kỳ (đã chốt: nhập một lần, không tính là doanh thu)

- Nhập cho quỹ Tiền mặt (**mỗi cửa hàng một số, một mốc**) và cho từng tài khoản (**mỗi tài khoản một số, một mốc chung cho mọi cửa hàng**, Q13), kèm **thời điểm mốc**. Sửa số dư đầu kỳ là sửa chính giao dịch đó. Số dư đầu kỳ của tài khoản không bị bộ lọc cửa hàng chia nhỏ.
- Số nhập phải là **số tiền thực có tại thời điểm mốc** (đếm két, xem sao kê). **Quỹ chỉ cộng giao dịch phát sinh sau mốc**; giao dịch trước mốc không tính vào số dư nên không cần sửa bù (Q8).
- "Phát sinh sau mốc" so theo thời điểm tiền đổi tay: hóa đơn theo `GIOTHANHTOAN`; chứng từ khác theo ngày giờ chứng từ (`NGAY`), nếu `NGAY` không có giờ thì theo `TIMECREATED` [cần kiểm trên dữ liệu thật khi implement].
- Quỹ **chưa nhập mốc**: tính từ giao dịch đầu tiên và màn Tồn quỹ ghi rõ "chưa nhập số dư đầu kỳ".
- Lưu bằng `TTHUCHI` (số dương → `THU`, âm → `CHI`) với **lý do dành riêng** "Số dư đầu kỳ" trong `DLYDOTHUCHI` (`LOAILYDO = -1`, ID cố định). Thêm dòng dữ liệu, không đổi cấu trúc.
- Loại khỏi doanh thu, chi phí, "thu khác", "chi khác" và công nợ.

### 4.5 Phiếu nhập hàng (đã chốt)

- Form Phiếu nhập kho gốc chỉ có ô "tiền thanh toán"; không thấy chỗ chọn hình thức trong code và layout của form.
- v1: khi `TIENTHANHTOAN ≠ 0` **bắt buộc chọn** Tiền mặt hoặc Chuyển khoản (một hình thức cho mỗi phiếu); chuyển khoản phải chọn tài khoản. Phiếu xuất trả hàng nhà cung cấp (`LOAI = 2`) dùng cùng cách chọn và ghi (Q3).
- Ghi: tiền mặt → `TIENMAT = TIENTHANHTOAN`, `CHUYENKHOAN = 0`; chuyển khoản → `CHUYENKHOAN = TIENTHANHTOAN`, `TIENMAT = 0`, `DTAIKHOANNGANHANGID`.
- Đọc: `CHUYENKHOAN ≠ 0` → quỹ Tài khoản; **mọi trường hợp còn lại, gồm phiếu cũ không ghi gì → Tiền mặt**, số tiền lấy `TIENTHANHTOAN`.
- Không dùng `LOAITHANHTOAN` để suy hình thức (hộp thanh toán bán hàng gốc luôn trả 0).
- Trả nợ nhà cung cấp sau đó đi qua phiếu chi, không sửa `TIENTHANHTOAN` để tránh trừ hai lần (cần kiểm với luồng công nợ khi làm spec công nợ).

### 4.6 Chuyển quỹ (đã chốt: cặp phiếu chi và thu)

- Các chiều: Tiền mặt → Tài khoản (nộp), Tài khoản → Tiền mặt (rút), Tài khoản → Tài khoản khác. Nguồn và đích khác nhau; số tiền > 0.
- Lưu trong **một transaction**: 1 phiếu chi ở nguồn và 1 phiếu thu ở đích, cùng ngày, số tiền, cửa hàng; dùng **lý do dành riêng** "Chuyển quỹ" (`LOAILYDO = -1`, ID cố định); `CHUNGTUGOC` của hai phiếu mang cùng một mã cặp.
- Sửa/xóa luôn tác động **cả cặp**; màn phiếu thu/chi không cho sửa/xóa lẻ.
- Loại khỏi doanh thu, chi phí, KQKD, "thu khác", "chi khác", công nợ. **Vẫn tính** vào số dư từng quỹ. Tổng hai quỹ không đổi.
- Phí chuyển khoản (nếu có) lập phiếu chi thường.

### 4.7 Màn Tồn quỹ

- Cây trái: Tất cả → Tiền mặt, Tài khoản → từng tài khoản (+ "Chưa xác định tài khoản" khi có).
- Bộ lọc Từ/Đến (tính trọn ngày Đến), Cửa hàng (theo ràng buộc user, giống Q13a).
- Hiện **tồn đầu kỳ**, từng dòng thu/chi kèm tồn cộng dồn, **tồn cuối kỳ**. Chọn một tài khoản thì thấy thêm tổng chuyển khoản và tổng quẹt thẻ.
- Tồn đầu kỳ và phát sinh trong kỳ **dùng chung một công thức** (§4.2), chỉ khác khoảng ngày.
- Quyền xem: theo quyền Tồn quỹ của app gốc (Kế toán, Quản lý; xem `deep/cong-no-quy.md` §b.12). **Nhập/sửa số dư đầu kỳ và chuyển quỹ: Quản lý và Kế toán** (Q10).
- Cả hai quỹ lọc theo **cửa hàng ghi trên giao dịch** (`DCUAHANGID`; phiếu nhập/xuất trả theo cửa hàng của kho khi bill không có `DCUAHANGID`) (Q6).
- **Tài khoản dùng chung nhiều cửa hàng (Q13, đã chốt 25/11):** tồn đầu kỳ, tồn cộng dồn và tồn cuối kỳ của tài khoản (và tổng quỹ Tài khoản) **chỉ hiện khi xem tất cả cửa hàng**; lọc một cửa hàng thì chỉ hiện thu/chi phát sinh của cửa hàng đó. Nút "Tất cả" của cây (gộp hai quỹ) theo cùng quy tắc vì có chứa quỹ Tài khoản. Người bị giới hạn cửa hàng nên không thấy tồn tài khoản. Hệ thống chỉ có **một** cửa hàng thì lọc cửa hàng đó coi như xem tất cả. Quỹ Tiền mặt luôn hiện đủ tồn theo cửa hàng đang xem.

### 4.8 Thanh toán gộp nhiều bàn (Q1, đã chốt 25/11)

Khách trả chung một lần; mỗi bill vẫn phải ghi `TIENMAT`, `CHUYENKHOAN`, `THE`, `DTAIKHOANNGANHANGID` của mình.

- Số phải trả của từng bill = `MAX(0, TONGCONG − đặt trước của bill đó)`.
- Duyệt bill theo **thứ tự xử lý nhóm**, cố định và lặp lại được (v1: giờ vào, rồi số phiếu). Lấp số phải trả của mỗi bill bằng **tiền thẻ trước, rồi chuyển khoản, cuối cùng tiền mặt**; hết nguồn này mới sang nguồn kế.
- Chuyển khoản còn dư sau bill cuối ghi hết vào **bill cuối**; tiền trả lại trừ vào `TIENMAT` của bill cuối (có thể âm, như §4.3).
- Mọi bill trong nhóm dùng chung một tài khoản đã chọn.
- Bất biến: Σ `THE`, Σ `CHUYENKHOAN`, Σ `TIENMAT` của nhóm lần lượt bằng tiền thẻ, tiền chuyển khoản, tiền mặt thực giữ lại (khách đưa − trả lại). Toàn bộ ghi trong **một transaction**.
- Số trên từng bill là **cách phân bổ để ghi sổ**, không phải khách trả riêng từng bàn.

Ví dụ chia: bill 1 = 300.000, bill 2 = 200.000; khách trả 350.000 chuyển khoản + 150.000 tiền mặt → bill 1: chuyển khoản 300.000; bill 2: chuyển khoản 50.000 + tiền mặt 150.000.

- **Nợ cũ thu kèm, đã chốt 25/11:** phần tiền trả nợ cũ cộng vào **bill cuối** (tăng `TIENMAT`/`CHUYENKHOAN`/`THE` của bill đó theo cùng thứ tự phân bổ); không lập thêm phiếu thu. Đây chỉ là quy tắc ghi tiền vào quỹ; việc giảm công nợ của khách do spec công nợ quy định. Nhánh gộp bàn của app gốc xử lý nợ cũ thế nào [CHƯA XÁC MINH]. Ví dụ: bill 300.000 + 200.000, nợ cũ 100.000, trả 600.000 tiền mặt → bill 1 ghi 300.000, bill 2 ghi 300.000.
- **Trả thiếu (ghi nợ), đã chốt 25/11:** các bill đầu được lấp đủ theo thứ tự trên, khoản thiếu nằm ở **bill cuối** (`CONGNO`). Thiếu nhiều hơn số phải trả của bill cuối thì phần còn lại nằm ở bill ngay trước, lùi dần. Σ `CONGNO` của nhóm = số thiếu; quỹ chỉ tăng đúng số tiền thực thu. Ví dụ: bill 300.000 + 200.000, khách trả 400.000 tiền mặt → bill 1 tiền mặt 300.000; bill 2 tiền mặt 100.000, nợ 100.000.
- **Người đứng tên nợ, đã chốt 25/11:** nhóm trả thiếu thì **bắt chọn một khách hàng** đứng tên khoản nợ trước khi đóng, kể cả khi các bill đang gắn khách khác nhau; khách đó được gán (`DKHACHHANGID`) cho **mọi bill mang nợ**. Bill không mang nợ giữ khách đang có. Chưa chọn thì không đóng được và không bill nào được ghi.

## 5. Lỗi / chỗ thiếu của app gốc liên quan quỹ

1. **Thanh toán gộp nhiều bàn (`NHOMGUID`) không ghi hình thức tiền:** nhánh nhóm trong `Hóa đơn nhà hàng` chỉ ghi `TIENTHANHTOAN`, `TRALAI`, `KHACHDUA`…, **không ghi `TIENMAT`, `CHUYENKHOAN`, `THE`, `DTAIKHOANNGANHANGID`** [CODE]. Nếu không có gì khác ghi ngầm [CHƯA XÁC MINH] thì tiền các bill gộp **không vào quỹ nào**. v1 ghi theo §4.8.
2. Khách chuyển khoản dư, trả lại tiền mặt: không trừ quỹ tiền mặt (§4.3).
3. Phiếu nhập không chọn được hình thức trả (§4.5).
4. Báo cáo Tồn quỹ **đảo dấu** phiếu `LOAI = 2` có nhà cung cấp: trong kỳ tính thu, tồn đầu kỳ tính chi. v1: luôn là tiền vào (Q3).
5. Báo cáo Tồn quỹ không lọc `STATUS`, tính cả phiếu đã xóa mềm. v1: không tính (Q4).
6. Phiếu thu công nợ tick chuyển khoản được nhưng không chọn được tài khoản → v1 bắt buộc chọn.
7. SQL của màn Tồn quỹ khác SQL báo cáo Tồn quỹ → v1 dùng một công thức (§4.7).

## 6. Quyết định đã chốt (25/11/2026)

1. Hai quỹ: Tiền mặt và Tài khoản; giao dịch bằng phương thức nào đổi quỹ đó.
2. Phiếu nhập bắt chọn tiền mặt hoặc chuyển khoản, một hình thức mỗi phiếu; phiếu cũ không ghi gì tính là tiền mặt.
3. Quẹt thẻ vào quỹ Tài khoản, vẫn xem riêng được; phải chọn tài khoản nhận.
4. Xem số dư từng tài khoản và tổng; giao dịch chuyển khoản bắt buộc chọn tài khoản.
5. Có chức năng chuyển quỹ bằng cặp phiếu chi/thu, không tính vào doanh thu, chi phí, thu khác, chi khác.
6. Chuyển khoản dư trả lại tiền mặt: Tài khoản cộng đủ, Tiền mặt trừ phần trả lại.
7. Voucher, thẻ trả trước, trừ tích lũy, nợ không đổi quỹ; tiền thật chỉ tính một lần lúc nhận; số dư đầu kỳ nhập một lần, không là doanh thu.
8. Báo cáo Chi tiết hoạt động ngày chia tiền nhập hàng đúng quỹ (thay cách của app gốc).
9. Gộp bàn: chia lần lượt theo thứ tự bill, tiền thẻ → chuyển khoản → tiền mặt (§4.8).
10. Gộp bàn trả thiếu: khoản thiếu ghi nợ vào bill cuối (§4.8).
11. Gộp bàn trả thiếu: bắt chọn khách hàng đứng tên nợ trước khi đóng (§4.8).
12. Gộp bàn có nợ cũ thu kèm: tiền ghi vào bill cuối (§4.8).
13. (Q2) Không cho quẹt thẻ quá số phải trả; phần dư voucher/thẻ trả trước/tích lũy không trả tiền mặt.
14. (Q3) Xuất trả hàng nhà cung cấp là tiền vào quỹ, chọn hình thức như phiếu nhập.
15. (Q4) Bản ghi đã xóa mềm không tính vào quỹ.
16. (Q5) Bill `LOAI` 5, 6 tạm không tính, chờ dữ liệu thật.
17. (Q6) Cả hai quỹ lọc theo cửa hàng ghi trên giao dịch.
18. (Q7) Phiếu chi trả lại tiền đặt trước bị bỏ: cảnh báo rõ, hỏi xác nhận.
19. (Q8) Không tính bù hóa đơn cũ; số dư đầu kỳ đếm theo tiền thực có, quỹ chỉ cộng giao dịch sau mốc.
20. (Q9) Tạm ứng lương chỉ tiền mặt, như form gốc.
21. (Q10) Quản lý và Kế toán được nhập số dư đầu kỳ và chuyển quỹ.
22. (Q13) Tồn của tài khoản ngân hàng chỉ hiện khi xem tất cả cửa hàng; mỗi tài khoản một mốc đầu kỳ chung (§4.4, §4.7).

## 7. Tiêu chí nghiệm thu (trên bản sao DB, selftest tự seed)

- [ ] Hóa đơn 480.000 trả: (a) tiền mặt 500.000 → Tiền mặt +480.000; (b) chuyển khoản 480.000 → đúng tài khoản +480.000; (c) quẹt thẻ 480.000 → bắt chọn tài khoản, +480.000, hiện ở phần quẹt thẻ; (d) kết hợp tiền mặt + chuyển khoản → mỗi quỹ đúng phần.
- [ ] Chuyển khoản 500.000 cho bill 480.000 → Tài khoản +500.000, Tiền mặt −20.000.
- [ ] Voucher / thẻ trả trước / tích lũy / ghi nợ không làm đổi quỹ.
- [ ] Đặt trước 200.000 (tiền mặt) rồi đóng bill 480.000 trả tiền mặt phần còn lại → tổng Tiền mặt tăng đúng 480.000, không phải 680.000.
- [ ] Đặt trước lớn hơn tiền bill → phiếu chi trả lại trừ đúng quỹ theo hình thức trên phiếu, không trừ hai lần.
- [ ] Phiếu thu/chi: `CHUYENKHOAN` NULL/0 → Tiền mặt; ≠ 0 → đúng tài khoản; thiếu tài khoản không lưu được (gồm phiếu thu công nợ).
- [ ] Phiếu nhập có thanh toán không chọn hình thức thì không lưu được; mỗi hình thức trừ đúng quỹ; phiếu cũ (`TIENMAT = 0`, `CHUYENKHOAN = 0`) trừ Tiền mặt.
- [ ] Chuyển quỹ: hai phiếu tạo/sửa/xóa cùng nhau trong một transaction; tổng hai quỹ không đổi; Thống kê doanh thu, KQKD, công nợ không đổi.
- [ ] Số dư đầu kỳ vào đúng quỹ, không vào doanh thu/chi phí/thu khác/chi khác.
- [ ] Tồn đầu kỳ của ngày D+1 = tồn cuối kỳ ngày D với mọi loại giao dịch ở §4.2.
- [ ] Σ số dư các tài khoản + "Chưa xác định" = số dư quỹ Tài khoản.
- [ ] Sửa hoặc hủy giao dịch thì số dư đổi theo ngay.
- [ ] Quẹt thẻ quá số phải trả bị chặn; voucher/thẻ trả trước/tích lũy lớn hơn bill không sinh tiền trả lại.
- [ ] Phiếu `LOAI = 2` có nhà cung cấp làm quỹ **tăng**, giống nhau khi nằm trong kỳ và khi đã vào tồn đầu kỳ.
- [ ] Bản ghi `STATUS = -30` và bill `LOAI` 5, 6 không làm đổi số dư.
- [ ] Mốc số dư đầu kỳ: giao dịch trước mốc không đổi số dư, giao dịch sau mốc (kể cả cùng ngày) có đổi; hai tài khoản mốc khác nhau tính độc lập; quỹ chưa có mốc hiện ghi chú.
- [ ] Lọc cửa hàng: giao dịch cửa hàng khác không vào số của cửa hàng đang chọn, ở cả hai quỹ.
- [ ] Q13: hai cửa hàng chung tài khoản, đầu kỳ 10.000.000, A thu 2.000.000, B thu 3.000.000 → xem tất cả: tồn cuối 15.000.000; lọc A: chỉ thu 2.000.000, không có tồn đầu/cộng dồn/cuối (cả nút "Tất cả" của cây); Tiền mặt vẫn có tồn theo A. Hệ thống một cửa hàng: lọc cửa hàng đó vẫn thấy tồn tài khoản.
- [ ] Đóng phiếu chi trả lại tiền đặt trước không lưu: có cảnh báo và hỏi xác nhận.
- [ ] Tạm ứng lương chỉ trừ Tiền mặt. Tài khoản không phải Quản lý/Kế toán không nhập được số dư đầu kỳ, không chuyển quỹ được.
- [ ] Không đổi cấu trúc DB; không SQL trong View; tiền dùng `decimal`.
- [ ] Gộp bàn, ví dụ §4.8: từng bill ghi đúng bảng ví dụ; Tài khoản +350.000, Tiền mặt +150.000.
- [ ] Gộp ≥ 3 bill, đủ thẻ + chuyển khoản + tiền mặt, có bill có đặt trước: các bất biến §4.8 đúng; đặt trước không bị tính lại; chạy lại cùng dữ liệu cho cùng kết quả.
- [ ] Gộp bàn có chuyển khoản dư: phần dư nằm ở bill cuối, `TIENMAT` bill cuối trừ tiền trả lại. Lỗi giữa chừng thì không bill nào được ghi.
- [ ] Gộp bàn trả thiếu: ví dụ §4.8 ra đúng; ca thiếu 250.000 → bill 2 nợ 200.000, bill 1 nợ 50.000; Σ `CONGNO` = số thiếu.
- [ ] Gộp bàn trả thiếu khi chưa chọn khách đứng tên nợ: không đóng được, không bill nào đổi. Hai bill khác khách, chọn khách A: mọi bill mang nợ gắn A, bill không nợ giữ khách cũ.
- [ ] Gộp bàn thu kèm nợ cũ: ví dụ §4.8 ra đúng; quỹ tăng đúng 600.000, không có phiếu thu thêm.

## 8. Kế hoạch implement

Mỗi bước một patch. Bước 1 làm trước; bước 3, 4 đi cùng hoặc sau spec bán hàng đã duyệt.

1. `FundRules` (Services, thuần, selftest): từ một giao dịch → số tiền vào/ra từng quỹ (§4.2, §4.3, §4.5).
2. `FundRepository` (Data, truy vấn tham số) + màn Tồn quỹ `FundBalanceView` (UI), chỉ đọc.
3. Phần chọn hình thức/tài khoản trong thanh toán bán hàng (gồm chia tiền gộp bàn §4.8) và phiếu nhập.
4. Chuyển quỹ + số dư đầu kỳ (thêm 2 lý do dành riêng vào `DLYDOTHUCHI`); các báo cáo loại 2 lý do này.
5. Cập nhật spec liên quan: [`su-dung-dich-vu.md`](su-dung-dich-vu.md), [`quan-ly-ban-hang-dieu-chinh-hoa-don.md`](quan-ly-ban-hang-dieu-chinh-hoa-don.md), [`thong-ke-doanh-thu.md`](thong-ke-doanh-thu.md), [`tong-hop-ket-qua-kinh-doanh.md`](tong-hop-ket-qua-kinh-doanh.md), [`chi-tiet-hoat-dong-ngay.md`](chi-tiet-hoat-dong-ngay.md) (đã cập nhật Q12).

Migration: không. Chỉ thêm 2 dòng danh mục ở bước 4.

## 9. Rủi ro và câu hỏi mở

**Rủi ro:** app gốc nếu còn chạy chung DB sẽ coi phiếu chuyển quỹ và số dư đầu kỳ là thu/chi thường (ví dụ tính vào "thu khác", "chi khác"). Số quỹ và một số báo cáo của bản viết lại sẽ khác app gốc.

Q1–Q13 đã chốt hết ngày 25/11. Việc còn phải kiểm khi implement: hành vi ngầm của khung/trigger gốc với cột tiền; bill `LOAI` 5, 6; giờ của cột `NGAY` (§4.4).

| # | Câu hỏi | Mặc định |
|---|---|---|
| Q1 | ~~Gộp bàn: chia tiền vào từng bill thế nào?~~ **Đã chốt 25/11: theo §4.8** | — |
| Q2 | ~~Tiền thừa do quẹt thẻ/voucher/thẻ trả trước/tích lũy~~ **Đã chốt 25/11** → §4.3 | — |
| Q3 | ~~Chiều tiền của phiếu `LOAI = 2` có nhà cung cấp~~ **Đã chốt 25/11** → tiền vào, §4.2 | — |
| Q4 | ~~Phiếu xóa mềm~~ **Đã chốt 25/11** → không tính, §4.1 | — |
| Q5 | ~~Bill `LOAI` 5, 6~~ **Đã chốt 25/11** → tạm chưa tính, xem lại khi có dữ liệu thật | — |
| Q6 | ~~Quỹ theo cửa hàng~~ **Đã chốt 25/11** → lọc theo cửa hàng của giao dịch, §4.7 | — |
| Q7 | ~~Bỏ phiếu chi trả lại tiền đặt trước~~ **Đã chốt 25/11** → cảnh báo, hỏi xác nhận, §4.2 | — |
| Q8 | ~~Hóa đơn cũ chuyển dư~~ **Đã chốt 25/11** → không tính bù; dùng mốc số dư đầu kỳ, §4.4 | — |
| Q9 | ~~Tạm ứng lương~~ **Đã chốt 25/11** → chỉ tiền mặt | — |
| Q10 | ~~Quyền nhập số dư đầu kỳ, chuyển quỹ~~ **Đã chốt 25/11** → Quản lý, Kế toán, §4.7 | — |
| Q11 | ~~Gộp bàn có nợ cũ thu kèm: gắn vào bill nào?~~ **Đã chốt 25/11: bill cuối** (§4.8) | — |
| Q12 | ~~Gộp bàn trả thiếu: nợ đứng tên ai?~~ **Đã chốt 25/11: bắt chọn khách** (§4.8) | — |
| Q13 | ~~Tài khoản dùng chung nhiều cửa hàng~~ **Đã chốt 25/11** → tồn chỉ hiện khi xem tất cả cửa hàng, §4.7 | — |
