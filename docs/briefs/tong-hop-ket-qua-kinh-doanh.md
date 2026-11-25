# Brief: Tổng hợp kết quả kinh doanh (bước duyệt spec)

Spec: [`../specs/tong-hop-ket-qua-kinh-doanh.md`](../specs/tong-hop-ket-qua-kinh-doanh.md). **ĐÃ DUYỆT 25/11**, loại nặng: [issue #14](https://github.com/Chienhandsome/FnB/issues/14). Rà 25/11 trên `32dbf41`. Đã ghi quyết định vào spec; chưa code, chưa commit.

## Chiến trả lời 25/11 (hỏi từng câu trong phiên): cả 11 câu theo đề xuất
- C1 sửa Tăng/giảm LÃI LỖ. C2 "Đến ngày" là cuối tháng thì kỳ trước tới cuối tháng trước. C3 dòng nhóm kỳ trước tính cả ngày trùng.
- C4 mẫu số % như gốc (cộng cả Giảm giá). C5 hiện dòng con như gốc. C6 giữ bill `LOAI` 5/6.
- C7 ▲/▼ mọi dòng. C8 ẩn Tiền giờ. C9 chọn sẵn cửa hàng khi user có đúng 1. C10 In "chưa hỗ trợ". C11 không Excel.

## Đã ghi vào spec, Chiến duyệt 25/11 (chưa commit)
- C1–C11, mục Quyền §2.0, mô tả đúng biểu tượng, phần LÃI LỖ theo `.frx`, tiêu chí nghiệm thu kèm ca số; §8 đổi theo `BillReportScopeSql` đã có trên `main`.
- Duyệt kèm hệ quả C2: xem một ngày 30/04 thì kỳ trước là 30/03–31/03. Còn: commit `docs/`, comment và đóng issue #14, code.

## Nguồn đã dùng
- `code.cs` của báo cáo (đọc hết); phần tính bill, thêm dòng món, `XuatVatTu` trong form Hóa đơn nhà hàng; `CapNhatGiaVon` của `DMATHANG`.
- Bản chép `DATA.fdb`/`DEMO.FDB`: `.frx` + script, `PARAMS`, `SGROUPROLE`, `DLYDOTHUCHI`, trigger; quét chuỗi trong file DB (`LOAI` 5/6, enum `LoaiPhieu`); chạy thử `DATEADD` và `CASE`.
- Font Wingdings trên máy; issue #3, #10, #11.

## Chưa xác minh
- Báo cáo app gốc khi có số liệu và phần LÃI LỖ trên màn: đã mở bản chép app (`DEMO.FDB`), **quyền điều khiển máy bị từ chối**; đã đóng, mã băm 3 CSDL gốc không đổi.
- DB thật của quán có bill `LOAI` 5/6 không. Chưa rà `THANHTIEN` dòng vật tư/combo con ở các màn bán khác.
