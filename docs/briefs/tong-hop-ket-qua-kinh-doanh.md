# Brief: Tổng hợp kết quả kinh doanh

Spec: [`../specs/tong-hop-ket-qua-kinh-doanh.md`](../specs/tong-hop-ket-qua-kinh-doanh.md). Duyệt 25/11 (issue #14, C1–C11 theo đề xuất; spec commit `be46ebb`).

## Đã làm (25/11)
- Code `c6c6c42` trên `main` (Antigravity, 2 vòng, base `be46ebb`). Báo cáo: [issue #15](https://github.com/Chienhandsome/FnB/issues/15).
- Màn `BusinessResultView` + `BusinessResultRepository` + `BusinessResultCalculator`; preview `preview.businessresult`.
- Build 0 lỗi, 0 cảnh báo mới; selftest 711 đạt, 7 lỗi cũ; 32 ca mới. Ảnh có dữ liệu mẫu đạt.
- Vòng 2 sửa ca tỉ lệ kiểm lỏng và bỏ màu xanh/đỏ của ▲/▼. Claude không sửa code chức năng.
- C9 làm rõ: hệ thống chỉ 1 cửa hàng thì admin cũng được chọn sẵn (Chiến: "Giữ chọn sẵn").
- Vòng 1 bị chặn nhầm vì `.claude/scheduled_tasks.lock` (do phiên vòng lặp ghi). Chiến cho `tools/agy` bỏ qua đúng file đó; đã thử file mồi vẫn bị chặn.

## Còn
- Chiến bấm thử trên bản sao DB. Mẫu in chưa làm (C10).
- Phần sửa `tools/agy/AgyCommon.ps1`, `Test-AgyScope.ps1` commit riêng ngày 25/11.

## Chưa kiểm
- 125% DPI. Chưa đối chiếu số với app gốc; chưa thấy app gốc vẽ báo cáo khi có số liệu.
- Chưa có ca test riêng cho admin khi hệ thống chỉ 1 cửa hàng. Thứ tự lý do trong cùng cấp chưa so với thứ tự DB.
- DB thật của quán có bill `LOAI` 5/6 không.

## Nguồn đã dùng khi viết spec
`code.cs` của báo cáo; mẫu `.frx` và script, `SGROUPROLE`, `DLYDOTHUCHI`, trigger trong bản chép `DATA.fdb`.
