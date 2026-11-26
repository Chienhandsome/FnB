# Spec: In, xuất Excel và FastReport cho mọi chỗ còn thiếu

- Trạng thái: **NHÁP**, Q1 đã chốt; Q3 đã đối chiếu app gốc; chờ Chiến trả lời Q2, Q4–Q8 ở [`nghiem-thu.md`](nghiem-thu.md) và duyệt.
- Ngày: 2026-11-26. Base: `origin/main` @ `9e7c6f1`.
- Nguồn quyết định: Chiến yêu cầu 26/11: viết spec cho **tất cả chỗ thiếu**, **dùng các DLL FastReport** của dự án.
- Loại: **nặng** (mẫu in có số tiền).

## Bối cảnh

`Kazuko_FnB` đã tham chiếu FastReport nhưng chưa có dòng code nào dùng. Mọi nút In, Xuất Excel đang báo "chưa hỗ trợ". App gốc in bằng mẫu `.frx` lưu trong CSDL.

## Mục tiêu

Các nút đang báo "chưa hỗ trợ" in và xuất được như app gốc, dùng đúng mẫu trong CSDL, không đổi cấu trúc CSDL.

## Phạm vi

| Nhóm | Chỗ thiếu | Nguồn mẫu |
|---|---|---|
| A. Báo cáo | In báo cáo: Thống kê doanh thu, Thống kê mặt hàng bán | `STEMPLATE` của form (4 và 3 mẫu) |
| A. Báo cáo | In báo cáo: Chi tiết hoạt động ngày, Tổng hợp kết quả kinh doanh | `SREPORTTEMPLATE` (2 và 1 mẫu) |
| B. Phiếu | In Báo giá; In Khách đặt hàng | `STEMPLATE` (2 mẫu "Báo giá", 5 mẫu "Mẫu đặt hàng") |
| C. Lưới | "In danh sách", nút In và Excel của danh sách (Báo giá, Đặt hàng, Danh mục, lưới chi tiết), Xuất excel trong hộp sửa, xuất định lượng | chưa xác minh (câu Q4) |
| D. Mã vạch | In mã vạch ở Danh mục | `STEMPLATE` có `BARCODE = 30` (6 mẫu) |
| E. Nhập Excel | Thêm nhanh, cập nhật nhanh, nhập Excel ở Danh mục; nhập Excel ở Đặt hàng | không dùng FastReport (câu Q6) |
| F. Ctrl+P | Hộp sửa mặt hàng, bàn, khu vực | chưa xác minh app gốc in gì (câu Q7) |

**Không làm:** trình thiết kế hay sửa mẫu in; in hóa đơn bán hàng, in bếp, chuyển bàn, phiếu thu chi và phiếu kho (màn tương ứng chưa có hoặc chưa có nút in trong app mới; làm cùng spec của màn đó); đổi cấu trúc CSDL.

Danh sách chỗ thiếu rà bằng tìm chữ trong code ngày 26/11, **chưa so từng màn với app gốc**.

## Các file

| File | Nội dung |
|---|---|
| [`nghiep-vu.md`](nghiep-vu.md) | Điều đã kiểm về engine in và cách đổ dữ liệu |
| [`giao-dien.md`](giao-dien.md) | Màn hình và luồng người dùng |
| [`nghiem-thu.md`](nghiem-thu.md) | Tiêu chí, chia patch, rủi ro, câu hỏi |
