# Đưa cho Claude Code đọc — hướng dẫn bàn giao

## Đọc theo thứ tự này

1. `tong-hop-ra-soat-vong-2.md` — tổng hợp vòng 2 (mới nhất, gồm bảng điểm, đính chính vòng 1, phát hiện mới, chỗ Claude sai, 7 câu chặn).
2. `tong-hop-ra-soat-pos-tap.md` — tổng hợp vòng 1 (8 nhóm, 241 câu hỏi gốc).
3. `deep/*.md` (7 file) — chi tiết từng nhóm: đối chiếu Claude, công thức/luồng đào sâu, đáp án từng câu hỏi, nghi vấn giống bug.
4. `work/frx-extract.md` — trích hàng loạt cột `.config` của 73 báo cáo tự sinh + đối chiếu `.frx` của 36 báo cáo có SQL.

## Lưu ý quan trọng về nguồn dữ liệu

- Bản rà này đọc **DB DEMO.fdb** (FnB DEMO trong repo). Claude đọc **POS_TAP_SRC_EXPORT** (dữ liệu POS TAP thật).
- Chỗ nào khác nhau thì **dữ liệu thật của Claude mới là chuẩn**: DEMO thừa 1 form / 1 báo cáo / 3 function / 7 config + bảng `DNGANHANG` so với POS TAP; giá trị `SCONFIG` của Claude mới là chuẩn.
- Thư mục `raw/` (dữ liệu xuất thô: code form, SQL, .frx, AELAYOUT…) **không kèm trong gói này** vì Claude đã có `POS_TAP_SRC_EXPORT` trên máy. Trong các file `deep/` mọi khẳng định đều trích tên method/SQL/cột làm bằng chứng nên Claude đối chiếu được trực tiếp với export của mình.

## Nhờ Claude làm 4 việc (dán nguyên đoạn dưới vào Claude Code)

> Đọc 2 file tổng hợp + 7 file deep trong thư mục này, rồi đối chiếu với docs/pos-tap-nghiep-vu*.md của mày:
> 1. Sửa những chỗ mày sai đã được chứng minh bằng code (liệt kê ở mục 4 của tong-hop-ra-soat-vong-2.md + mục (a) mỗi file deep). Sửa xong ghi rõ đã sửa gì ở đâu.
> 2. Bổ sung các phát hiện mới của vòng 2 vào tài liệu nghiệp vụ (đặc biệt: phiếu thu công nợ không trừ nợ theo code, 73 báo cáo là tự sinh, TANGTHEOTRIGIA nhập được nhưng không chạy, LUONGTHEOCA là cột chết, bản tổng hợp NCC sai dấu).
> 3. Lập checklist test bằng app gốc cho 55 câu còn lại (mục 5 của tong-hop-ra-soat-vong-2.md + mục (d) mỗi file deep) — mỗi câu ghi thao tác test cụ thể trên app POS TAP + DB cần đọc sau test.
> 4. Lưu ý: bản rà này đọc DEMO.fdb, mày đọc POS_TAP_SRC_EXPORT thật — chỗ nào khác nhau thì dữ liệu thật của mày là chuẩn, ghi rõ khi gặp.
> Không sửa code Kazuko_FnB trong đợt này, chỉ sửa tài liệu + lập checklist.
