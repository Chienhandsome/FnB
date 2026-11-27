# Spec: Tồn kho (màn xem tồn theo kho)

- Trạng thái: **ĐÃ DUYỆT** (Claude tự duyệt 27/11/2026 theo workflow-agy §10.1.1; điểm **TẠM** ở §5).
- Ngày: 2026-11-27. Loại: **nhẹ về ghi** (chỉ đọc), **nặng về công thức tồn**. Nguồn: `docs/deep/kho-hang.md` §A.2, §B.2.
- Menu: KHO HÀNG ▸ Tồn kho. Không ghi dữ liệu.

## 1. Màn

Bộ lọc: Kho (mặc định kho của cửa hàng đăng nhập; không phải admin chỉ chọn kho thuộc cửa hàng mình), Loại/Nhóm mặt hàng, ô Tìm (mã, tên, không phân biệt hoa thường và dấu), tích "Chỉ hiện hàng dưới mức tồn tối thiểu", tích "Ẩn hàng tồn 0". Ngày chốt: hiện tại.

Lưới (mỗi mặt hàng một dòng; mặt hàng kiêm vật tư và nguyên liệu, đang dùng): Mã, Tên, ĐVT, **Tồn kho** (số lượng theo ĐVT cơ sở), Tồn hiển thị 2 ĐVT khi mặt hàng có đơn vị chẵn ("N thùng M chai": `N = (int)(tồn / QUYDOI)`, `M = tồn − N × QUYDOI`), Tồn tối thiểu, Giá nhập, Giá vốn, Giá trị vốn (= tồn × giá vốn), Giá trị bán (= tồn × giá bán), hàng tổng cộng giá trị. Tô vàng dòng tồn ≤ tồn tối thiểu (khi `TONTOITHIEU > 0`); tô đỏ dòng tồn âm.

Không có quyền xem giá nhập: ẩn Giá nhập, Giá vốn, Giá trị vốn, Giá trị bán và tắt xuất. Xuất Excel qua `GridExcelExporter` (đúng cột đang hiện).

## 2. Công thức tồn

`Σ(SLNHAP − SLXUAT)` theo kho, đúng công thức chuẩn ở `phieu-xuat-chuyen-kho.md` §4 (STATUS = 30 và (`LOAI <> 0` hoặc `LOAI = 0` đã thanh toán)). **Chỉ một công thức**, dùng chung hàm tồn với bán hàng, xuất, chuyển.

## 3. Quyền

Chức năng "Tồn kho" (Xem). Không có Thêm/Sửa/Xóa.

## 4. Nghiệm thu

1. Mặt hàng nhập 10, bán 3 (đã thanh toán), xuất khác 2, chuyển đi 1: tồn = 4; bill chưa thanh toán không tính; phiếu STATUS khác 30 không tính.
2. Mặt hàng 2 ĐVT: tồn 50, quy đổi 24 hiển thị "2 thùng 2 chai".
3. Lọc kho, loại, tìm đúng; "dưới tồn tối thiểu" lọc đúng; người không phải admin không thấy kho của cửa hàng khác.
4. Ẩn cột giá khi không có quyền xem giá nhập; xuất Excel đúng cột.
5. Không đổi cấu trúc CSDL; ca selftest cũ giữ tên.

## 5. Điểm TẠM

- **T1** Tồn theo ngày và tồn nhiều kho cạnh nhau làm sau (ngoài 2 ngày).
- **T2** Ba báo cáo xuất nhập tồn của bản gốc có công thức hơi khác nhau; bản mới chỉ dùng công thức chuẩn ở trên.
