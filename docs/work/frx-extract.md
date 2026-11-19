# Trích xuất mẫu báo cáo FastReport (.frx) + cấu hình cột (.config) — vòng 2

Nguồn: `~/workspace/fnb/pos-tap-review/raw/sreport_template/` (manifest 156 dòng) + `raw/sreport_sql/` (128 file).
Trích bằng script `work/frx_extract.py` + `work/frx_analyze2.py` (nối manifest + file đĩa với `SREPORT`/`SFORM`).

## 0. Kết luận về cơ chế (quan trọng — sửa giả thuyết vòng 1)

- **0/36 file `.frx` chứa `SelectCommand`.** Giả thuyết vòng 1 "SQL của 73 báo cáo nằm trong `.frx`" là **SAI**.
- 73/77 báo cáo lá có `SQLMODE=0` đều có file **`.config`** (XML `<DocumentElement>`) + dòng manifest với **`AUTOGENREPORT=30`** (118/156 dòng manifest) → đây là **báo cáo tự sinh (auto-generated)**: app dựng báo cáo lúc chạy từ danh sách cột trong `.config` (cột nào hiện, gom nhóm theo cột nào, cột nào cộng tổng nhóm/tổng cộng). **WHERE / bảng nguồn / JOIN không có trong `.config`** — cơ chế sinh SQL nằm trong `No1Lib` (không có trong dữ liệu đã xuất; grep `SREPORTTEMPLATE`/`AUTOGENREPORT` trong `sform_code/` không ra code chạy báo cáo).
- 35/36 báo cáo có SQL có file `.frx` trên đĩa. `.frx` chỉ chứa **layout + `<Total>`** (cộng tổng cột hiển thị trên band tổng), **không chứa SQL, không chứa biểu thức tính toán** (trừ vài trường hợp đặc biệt, xem §3). Vì vậy `.frx` chỉ xác minh được: cột nào hiển thị, tổng nào được cộng, biểu đồ bind cột nào — **không xác minh được công thức lấy số liệu**. Mọi công thức trong §3 dưới đây đều lấy từ **SQL trong `SREPORT`** (36 báo cáo) hoặc **CODE C#** (17 báo cáo), `.frx` chỉ đóng vai trò đối chiếu hiển thị.
- Quy ước cờ trong `.config`: `HIENTHI=30` hiện cột; `NHOM1`/`NHOM2=30` gom nhóm cấp 1/2; `TONGNHOM1`/`TONGNHOM2=30` cộng tổng theo nhóm; `TONGCONG=30` cộng tổng toàn báo cáo. `CONTROLTYPE=NotShow` = cột ẩn (có trong dữ liệu, không in ra). `DATAFIELD` dạng `BANG_TENCOT` (vd `TDONHANG_LOAI`) = cột của bảng JOIN; dạng `TENCOT` trần = cột của bảng chính; dạng `BANG_NAME` (vd `DKHACHHANG_NAME`) = tên lookup qua FK.

## 1. Thống kê đối chiếu

| Hạng mục | Số lượng |
|---|---|
| Báo cáo trong `SREPORT` | 128 |
| — có SQL thật (`SQLMODE=30`) | 36 (17 có thêm CODE C#) |
| — không SQL, tự sinh từ `.config` (`SQLMODE=0`) | 73 |
| — nút nhóm cha (không SQL, không template) | 19 |
| File `.frx` trên đĩa | 36 (35 của báo cáo có SQL + 1 của form Công nợ NCC) |
| File `.config` trên đĩa | 96 (80 của báo cáo + 2 của form + 14 mồ côi) |
| Dòng manifest | 156 (140 trỏ `SREPORT`, 2 trỏ `SFORM`, 14 mồ côi) |
| `SREPORT` không có dòng manifest nào | 20 (19 nút cha + 1 báo cáo SQL: CHI TIẾT BÁN HÀNG THEO THU NGÂN) |
| `AUTOGENREPORT=30` / `=0` / trống | 118 / 14 / 24 dòng |

## 2. 73 báo cáo tự sinh — cột trong `.config` (toàn bộ)

Bảng chính suy ra từ tiền tố `DATAFIELD` (vd `TDONHANG_*` → `TDONHANG`; cột trần `NAME/NGAY/TONGCONG` → `TDONHANG` hoặc `TTHUCHI` theo nhóm báo cáo). Cột `SOTIENCON` của thẻ trả trước và các cột `*_NAME` là cột tính/lookup, không có sẵn trong schema.

### BÁO CÁO BÁN HÀNG THEO KHÁCH HÀNG — 23 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME] <NHÓM1>
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN]
- Tiền giảm giá [TIENGIAMGIA]
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN] fmt=HH:mm
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Đổi trả [DOITRA]
- Tiền giờ [TIENGIO] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá tổng [TIENGIAMGIATONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### BÁO CÁO BÁN HÀNG THEO NGÀY — 32 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Khách hàng [DKHACHHANG_NAME]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN]
- Tiền giảm giá [TIENGIAMGIA]
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN]
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME] <NHÓM2>
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Đổi trả [DOITRA] <TỔNG-CỘNG>
- Voucher [VOUCHER] <TỔNG-CỘNG>
- Thẻ tt [THETRATRUOC] <TỔNG-CỘNG>
- Trừ tích lũy [TRUTICHLUY] <TỔNG-CỘNG>
- Tiền mặt [TIENMAT] <TỔNG-CỘNG>
- Chuyển khoản [CHUYENKHOAN] <TỔNG-CỘNG>
- Thẻ [THE] <TỔNG-CỘNG>
- Bắt đầu [BATDAU]
- Kết thúc [KETTHUC]
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Tiền giảm giá tổng [TIENGIAMGIATONG] <TỔNG-CỘNG>
- Công nợ [CONGNO] <TỔNG-CỘNG>

### BÁO CÁO BÁN HÀNG THEO NHÂN VIÊN — 25 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN]
- Tiền giảm giá [TIENGIAMGIA]
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME] <NHÓM1>
- Giờ thanh toán [GIOTHANHTOAN]
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Đổi trả [DOITRA]
- Tiền giờ [TIENGIO] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá tổng [TIENGIAMGIATONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đặt trước [DATTRUOC]
- Công nợ [CONGNO]

### BÁO CÁO BÁN HÀNG THEO THU NGÂN — 19 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN]
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME] <NHÓM1>
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-CỘNG>

### BÁO CÁO CHI TIẾT HÀNG KHUYẾN MẠI — 12 cột
- Số phiếu [TDONHANG_NAME]
- Ngày [TDONHANG_NGAY]
- Tên khách hàng [DKHACHHANG_NAME]
- Địa chỉ [DKHACHHANG_DIACHI]
- Nhân viên xuất [DNHANVIEN_NAME]
- Thu ngân [SUSER_NAME]
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Mặt hàng [DMATHANG_NAME]
- ĐVT [DDONVITINH_NAME]
- Mã hàng [DMATHANG_CODE]
- Giảm giá % [TILEGIAMGIA]
- Số lượng [SLXUAT]

### BÁO CÁO XUẤT BÁN HÀNG THEO NGÀY — 6 cột
- Ngày [TDONHANG_NGAY] <NHÓM1>
- Vật tư [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-CỘNG>
- ĐVT [DDONVITINH_NAME]
- Số lượng [SLXUATCHUAQUYDOI] <TỔNG-CỘNG>
- Đơn giá [GIAVON]

### BÁO CÁO XUẤT BÁN HÀNG THEO ĐƠN — 8 cột
- Số phiếu [TDONHANG_NAME]
- Ngày [TDONHANG_NGAY] <NHÓM1>
- Loại [TDONHANG_LOAI] (ẩn)
- Vật tư [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-CỘNG>
- ĐVT [DDONVITINH_NAME]
- Số lượng [SLXUATCHUAQUYDOI] <TỔNG-CỘNG>
- Đơn giá [GIAVON]

### BÁO CÁO XUẤT ĐỊNH LƯỢNG THEO ĐƠN — 8 cột
- Số phiếu [TDONHANG_NAME] <NHÓM1>
- Ngày [TDONHANG_NGAY]
- Vật tư [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- ĐVT [DDONVITINH_NAME]
- Số lượng [SLXUATCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Xuất vật tư [XUATVATTU] (ẩn)
- Đơn giá [GIAVON]

### DANH SÁCH HÓA ĐƠN THEO BÀN — 21 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN]
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Bàn/Phòng [DBAN_NAME] <NHÓM1>
- Khu vực [DKHUVUC_NAME]
- Tiền giờ [TIENGIO] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH HÓA ĐƠN THEO KHU VỰC — 21 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN]
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Bàn/Phòng [DBAN_NAME]
- Khu vực [DKHUVUC_NAME] <NHÓM1>
- Tiền giờ [TIENGIO] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH HÓA ĐƠN THEO NHÓM HIỂN THỊ — 21 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN]
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Bàn/Phòng [DBAN_NAME]
- Nhóm hiển thị [DNHOMHIENTHI_NAME] <NHÓM1>
- Tiền giờ [TIENGIO] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH KHÁCH HÀNG THEO NHÂN VIÊN — 7 cột
- Tên khách hàng [NAME]
- Nhóm khách hàng [DNHOMKHACHHANG_NAME]
- Mã khách [MAKHACH]
- Địa chỉ [DIACHI]
- Điện thoại [DIENTHOAI]
- Email [EMAIL]
- Nhân viên [DNHANVIEN_NAME] <NHÓM1>

### DANH SÁCH KHÁCH HÀNG THEO NHÓM — 7 cột
- Tên khách hàng [NAME]
- Nhóm khách hàng [DNHOMKHACHHANG_NAME] <NHÓM1>
- Mã khách [MAKHACH]
- Địa chỉ [DIACHI]
- Điện thoại [DIENTHOAI]
- Email [EMAIL]
- Nhân viên [DNHANVIEN_NAME]

### DANH SÁCH MÓN XÓA, GIẢM, TRẢ LẠI — 11 cột
- Nội dung [NOTE]
- Giờ [GIO] fmt=HH:mm
- Ngày [NGAY]
- Số đơn hàng [SODONHANG]
- Tài khoản [TAIKHOAN]
- Thiết bị [THIETBI]
- Hành động [BAN]
- Số lượng [SOLUONG] <TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Thành tiền [THANHTIEN] <TỔNG-CỘNG>
- Tên hàng [TENHANG]

### DANH SÁCH MẶT HÀNG THEO HÃNG SẢN XUẤT — 8 cột
- Tên mặt hàng [NAME]
- Nhóm mặt hàng [DNHOMMATHANG_NAME]
- Giá bán [GIABAN]
- Đơn vị tính [DDONVITINH_NAME]
- Mã hàng [CODE]
- Tồn tối thiểu [TONTOITHIEU]
- Tồn tối đa [TONTOIDA]
- Hãng sản xuất [DHANGSANXUAT_NAME] <NHÓM1>

### DANH SÁCH MẶT HÀNG THEO NHÓM — 7 cột
- Tên mặt hàng [NAME]
- Nhóm mặt hàng [DNHOMMATHANG_NAME] <NHÓM1>
- Giá bán [GIABAN]
- Đơn vị tính [DDONVITINH_NAME]
- Mã hàng [CODE]
- Tồn tối thiểu [TONTOITHIEU]
- Tồn tối đa [TONTOIDA]

### DANH SÁCH NHÀ CUNG CẤP THEO NHÓM — 6 cột
- Tên nhà cung cấp [NAME]
- Nhóm nhà cung cấp [DNHOMNHACUNGCAP_NAME] <NHÓM1>
- Mã nhà cung cấp [MANHACUNGCAP]
- Địa chỉ [DIACHI]
- Điện thoại [DIENTHOAI]
- Email [EMAIL]

### DANH SÁCH PHIẾU CHI THEO LÝ DO THU CHI — 9 cột
- Số phiếu [NAME]
- Ngày [NGAY]
- Tên đối tượng [TENDOITUONG]
- Địa chỉ [DIACHI]
- Loại phiếu [LOAI] (ẩn)
- Lý do thu chi [DLYDOTHUCHI_NAME]
- Diễn giải [DIENGIAI]
- Chứng từ gốc [CHUNGTUGOC]
- Số tiền chi [CHI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH PHIẾU CHI THEO NGÀY — 9 cột
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Tên đối tượng [TENDOITUONG]
- Địa chỉ [DIACHI]
- Loại phiếu [LOAI] (ẩn)
- Lý do thu chi [DLYDOTHUCHI_NAME]
- Diễn giải [DIENGIAI]
- Chứng từ gốc [CHUNGTUGOC]
- Số tiền chi [CHI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH PHIẾU CHUYỂN KHO THEO NGÀY — 9 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Kho nhập [DKHOHANG_NAME]
- Nhân viên xuất [DNHANVIEN_NAME]
- Nhân viên nhập [DNHANVIEN_NAME]
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU CHUYỂN KHO THEO NHÂN VIÊN NHẬP — 8 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên xuất [DNHANVIEN_NAME] <NHÓM1>
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU CHUYỂN KHO THEO NHÂN VIÊN XUẤT — 8 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên xuất [DNHANVIEN_NAME] <NHÓM1>
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU KIỂM KÊ THEO NGÀY — 7 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Loại [LOAI] (ẩn)
- Kho hàng [DKHOHANG_NAME]
- Nhân viên [DNHANVIEN_NAME]
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU KIỂM KÊ THEO NHÂN VIÊN — 7 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Kho hàng [DKHOHANG_NAME]
- Nhân viên [DNHANVIEN_NAME] <NHÓM1>
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU NHẬP KHO THEO NGÀY — 15 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Nhà cung cấp [DNHACUNGCAP_NAME]
- Kho nhập [DKHOHANG_NAME]
- Nhân viên nhập [DNHANVIEN_NAME]
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU NHẬP KHO THEO NHÀ CUNG CẤP — 15 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Nhà cung cấp [DNHACUNGCAP_NAME] <NHÓM1>
- Kho nhập [DKHOHANG_NAME]
- Nhân viên nhập [DNHANVIEN_NAME]
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU NHẬP KHO THEO NHÂN VIÊN NHẬP — 15 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Nhà cung cấp [DNHACUNGCAP_NAME]
- Kho nhập [DKHOHANG_NAME]
- Nhân viên nhập [DNHANVIEN_NAME] <NHÓM1>
- Diễn giải [DIENGIAI]

### DANH SÁCH PHIẾU THU THEO LÝ DO THU CHI — 9 cột
- Số phiếu [NAME]
- Ngày [NGAY]
- Tên đối tượng [TENDOITUONG]
- Địa chỉ [DIACHI]
- Loại phiếu [LOAI] (ẩn)
- Lý do thu chi [DLYDOTHUCHI_NAME]
- Diễn giải [DIENGIAI]
- Chứng từ gốc [CHUNGTUGOC]
- Số tiền thu [THU] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH PHIẾU THU THEO NGÀY — 9 cột
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Tên đối tượng [TENDOITUONG]
- Địa chỉ [DIACHI]
- Loại phiếu [LOAI] (ẩn)
- Lý do thu chi [DLYDOTHUCHI_NAME]
- Diễn giải [DIENGIAI]
- Chứng từ gốc [CHUNGTUGOC]
- Số tiền thu [THU] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH PHIẾU XUẤT KHO THEO NGÀY — 13 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên xuất [DNHANVIEN_NAME]

### DANH SÁCH PHIẾU XUẤT KHO THEO NHÂN VIÊN XUẤT — 13 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên xuất [DNHANVIEN_NAME]

### DANH SÁCH THẺ TRẢ TRƯỚC — 4 cột
- Mã thẻ [NAME]
- Nhóm [DNHOMTHETRATRUOC_NAME] <NHÓM1>
- Ngày hết hạn [NGAYHETHAN]
- Số tiền còn [SOTIENCON] fmt=n0

### DANH SÁCH ĐẶT HÀNG THEO KHÁCH HÀNG — 13 cột
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME] <NHÓM1>
- Địa chỉ [DIACHI] <NHÓM1>
- Điện thoại [DIENTHOAI]
- Email [EMAIL]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH ĐẶT HÀNG THEO NGÀY — 13 cột
- Số phiếu [NAME]
- Ngày [NGAY] <NHÓM1>
- Khách hàng [DKHACHHANG_NAME]
- Địa chỉ [DIACHI]
- Điện thoại [DIENTHOAI]
- Email [EMAIL]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### DANH SÁCH ĐỢT KHUYẾN MẠI — 4 cột
- Đợt khuyến mại [NAME]
- Loại hình [DLOAIHINHKHUYENMAI_NAME]
- Từ ngày [TUNGAY]
- Đến ngày [DENNGAY]

### KHÁCH HÀNG ĐẾN NGÀY SINH NHẬT — 6 cột
- Khách hàng [NAME]
- Nhóm khách [DNHOMKHACHHANG_NAME] <NHÓM1>
- Mã khách [MAKHACH]
- Địa chỉ [DIACHI]
- Điện thoại [DIENTHOAI]
- Email [EMAIL]

### TỔNG HỢP BÁN HÀNG THEO BÀN PHÒNG — 10 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Bàn/Phòng [DBAN_NAME]
- Khu vực [DKHUVUC_NAME] <NHÓM1>
- Tiền giờ [TIENGIO] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP BÁN HÀNG THEO KHU VỰC — 9 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Khu vực [DKHUVUC_NAME]
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-CỘNG>

### TỔNG HỢP BÁN HÀNG THEO NGÀY — 9 cột
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Tiền giảm giá tổng [TIENGIAMGIATONG] <TỔNG-CỘNG>

### TỔNG HỢP BÁN HÀNG THEO NHÓM HIỂN THỊ — 9 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Nhóm hiển thị [DNHOMHIENTHI_NAME]
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-CỘNG>

### TỔNG HỢP BÁN THEO KHÁCH HÀNG — 13 cột
- Tên khách hàng [DKHACHHANG_NAME]
- Mã khách [DKHACHHANG_MAKHACH]
- Địa chỉ [DKHACHHANG_DIACHI]
- Điện thoại [DKHACHHANG_DIENTHOAI]
- Email [DKHACHHANG_EMAIL]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Tiền giảm giá tổng [TIENGIAMGIATONG] <TỔNG-CỘNG>

### TỔNG HỢP BÁN THEO NHÂN VIÊN — 9 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Nhân viên bán [DNHANVIEN_NAME]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-CỘNG>

### TỔNG HỢP BÁN THEO THU NGÂN — 9 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Thu ngân [SUSER_NAME]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Giảm giá [TIENGIAMGIATONG] <TỔNG-CỘNG>

### TỔNG HỢP DOANH THU CHƯA THANH TOÁN — 30 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN]
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN]
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Đã thanh toán [DATHANHTOAN] (ẩn)
- Thẻ tt [THETRATRUOC]
- Trừ tích lũy [TRUTICHLUY]
- Tiền mặt [TIENMAT]
- Chuyển khoản [CHUYENKHOAN]
- Thẻ [THE]
- Bàn/Phòng [DBAN_NAME]
- Bắt đầu [BATDAU] fmt=HH:mm
- Kết thúc [KETTHUC] fmt=HH:mm
- Tiền giờ [TIENGIO] <TỔNG-CỘNG>
- Phí dịch vụ [PHIDICHVU] <TỔNG-CỘNG>
- Tiền giảm giá tổng [TIENGIAMGIATONG] <TỔNG-CỘNG>
- Công nợ [CONGNO] <TỔNG-CỘNG>

### TỔNG HỢP HOA HỒNG THEO NVKD — 10 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhân viên bán [DNHANVIEN_NAME] <NHÓM1>
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Mặt hàng [DMATHANG_NAME]
- Mã hàng [DMATHANG_CODE]
- Hoa hồng [DMATHANG_HOAHONG]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tên đơn vị tính [DDONVITINH_NAME]
- Số lượng nhập chẵn [SLNHAPCHUAQUYDOI]
- Số lượng xuất chẵn [SLXUATCHUAQUYDOI]

### TỔNG HỢP MẶT HÀNG BÁN THEO BÀN — 13 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Thu ngân [SUSER_NAME]
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Bàn/Phòng [DBAN_NAME] <NHÓM1>
- Khu vực [DKHUVUC_NAME]
- Mã hàng [DMATHANG_CODE]
- Giảm giá % [TILEGIAMGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng xuất chẵn [SLXUATCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Xuất vật tư [XUATVATTU] (ẩn)
- Tên hàng [TENHANG]

### TỔNG HỢP MẶT HÀNG BÁN THEO KHU VỰC — 12 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Thu ngân [SUSER_NAME]
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Khu vực [DKHUVUC_NAME] <NHÓM1>
- Mã hàng [DMATHANG_CODE]
- Giảm giá % [TILEGIAMGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng xuất chẵn [SLXUATCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Xuất vật tư [XUATVATTU] (ẩn)
- Tên hàng [TENHANG]

### TỔNG HỢP MẶT HÀNG BÁN THEO KHÁCH HÀNG — 10 cột
- Khách hàng [DKHACHHANG_NAME] <NHÓM1>
- Loại [TDONHANG_LOAI] (ẩn)
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- ĐVT [DDONVITINH_NAME]
- Giảm giá % [TILEGIAMGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Số lượng xuất [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Xuất vật tư [XUATVATTU] (ẩn)
- Tên hàng [TENHANG]

### TỔNG HỢP MẶT HÀNG BÁN THEO NGÀY — 10 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Nhóm hàng [DNHOMMATHANG_NAME] <NHÓM1>
- Giảm giá % [TILEGIAMGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng xuất chẵn [SLXUATCHUAQUYDOI]
- Xuất vật tư [XUATVATTU] (ẩn)
- Tên hàng [TENHANG]

### TỔNG HỢP MẶT HÀNG BÁN THEO NHÂN VIÊN — 11 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhân viên bán [DNHANVIEN_NAME] <NHÓM1>
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Nhóm hàng [DNHOMMATHANG_NAME] <NHÓM2>
- Giảm giá % [TILEGIAMGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Tên đơn vị tính [DDONVITINH_NAME]
- Số lượng xuất chẵn [SLXUATCHUAQUYDOI]
- Xuất vật tư [XUATVATTU] (ẩn)
- Tên hàng [TENHANG]

### TỔNG HỢP MẶT HÀNG BÁN THEO THU NGÂN — 11 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Thu ngân [SUSER_NAME] <NHÓM1>
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Nhóm hàng [DNHOMMATHANG_NAME] <NHÓM2>
- Giảm giá % [TILEGIAMGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng xuất chẵn [SLXUATCHUAQUYDOI]
- Xuất vật tư [XUATVATTU] (ẩn)
- Tên hàng [TENHANG]

### TỔNG HỢP MẶT HÀNG CHUYỂN KHO THEO NGÀY — 8 cột
- Ngày [TDONHANG_NGAY] <NHÓM1>
- Loại [TDONHANG_LOAI] (ẩn)
- Mặt hàng [DMATHANG_NAME]
- Mã hàng [DMATHANG_CODE]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Tên đơn vị tính [DDONVITINH_NAME]
- Số lượng [SLNHAPCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG CHUYỂN KHO THEO NHÂN VIÊN CHUYỂN — 8 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhân viên xuất [DNHANVIEN_NAME] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- Mã hàng [DMATHANG_CODE]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng [SLNHAPCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG CHUYỂN KHO THEO NHÂN VIÊN NHẬN — 8 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhân viên nhập [DNHANVIEN_NAME] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- Mã hàng [DMATHANG_CODE]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng [SLNHAPCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG NHẬP KHO THEO NGÀY — 8 cột
- Ngày [TDONHANG_NGAY] <NHÓM1>
- Loại [TDONHANG_LOAI] (ẩn)
- Mặt hàng [DMATHANG_NAME]
- Mã hàng [DMATHANG_CODE]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- ĐVT [DDONVITINH_NAME]
- Số lượng nhập [SLNHAPCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG PHIẾU KIỂM KÊ THEO NGÀY — 8 cột
- Ngày [TDONHANG_NGAY] <NHÓM1>
- Loại [TDONHANG_LOAI] (ẩn)
- Mặt hàng [DMATHANG_NAME]
- ĐVT [DDONVITINH_NAME]
- Số lượng nhập [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng xuất [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng thực tế [SLTHUCTE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng hệ thống [SLHETHONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG PHIẾU KIỂM KÊ THEO NHÂN VIÊN — 8 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhân viên [DNHANVIEN_NAME] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- ĐVT [DDONVITINH_NAME]
- Số lượng nhập [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng xuất [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng thực tế [SLTHUCTE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng hệ thống [SLHETHONG] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG PHIẾU NHẬP THEO NHÀ CUNG CẤP — 8 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhà cung cấp [DNHACUNGCAP_NAME] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- Mã hàng [DMATHANG_CODE]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- ĐVT [DDONVITINH_NAME]
- Số lượng nhập [SLNHAPCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG PHIẾU NHẬP THEO NHÂN VIÊN NHẬP — 8 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhân viên nhập [DNHANVIEN_NAME] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- Mã hàng [DMATHANG_CODE]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- ĐVT [DDONVITINH_NAME]
- Số lượng nhập [SLNHAPCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG THEO NHÓM HIỂN THỊ — 12 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Thu ngân [SUSER_NAME]
- Đã thanh toán [TDONHANG_DATHANHTOAN] (ẩn)
- Nhóm hiển thị [DNHOMHIENTHI_NAME] <NHÓM1>
- Mã hàng [DMATHANG_CODE]
- Giảm giá % [TILEGIAMGIA] <TỔNG-NHÓM1>
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng xuất chẵn [SLXUATCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Xuất vật tư [XUATVATTU] (ẩn)
- Tên hàng [TENHANG]

### TỔNG HỢP MẶT HÀNG XUẤT THEO NGÀY — 7 cột
- Ngày [TDONHANG_NGAY] <NHÓM1>
- Loại [TDONHANG_LOAI] (ẩn)
- Mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng xuất [SLXUATCHUAQUYDOI] <NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG XUẤT THEO NHÂN VIÊN XUẤT — 7 cột
- Loại [TDONHANG_LOAI] (ẩn)
- Nhân viên xuất [DNHANVIEN_NAME] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- ĐVT [DDONVITINH_NAME]
- Số lượng xuất [SLXUATCHUAQUYDOI] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP MẶT HÀNG ĐẶT THEO KHÁCH HÀNG — 5 cột
- Tên mặt hàng [DMATHANG_NAME]
- Số lượng [SOLUONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tên khách hàng [DKHACHHANG_NAME] <NHÓM1>

### TỔNG HỢP MẶT HÀNG ĐẶT THEO NGÀY — 6 cột
- Ngày [TDATHANG_NGAY] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- ĐVT [DDONVITINH_NAME]
- Số lượng [SOLUONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>

### TỔNG HỢP NHẬP THEO NGÀY — 7 cột
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>

### TỔNG HỢP NHẬP THEO NHÀ CUNG CẤP — 11 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Tên nhà cung cấp [DNHACUNGCAP_NAME]
- Mã nhà cung cấp [DNHACUNGCAP_MANHACUNGCAP]
- Địa chỉ [DNHACUNGCAP_DIACHI]
- Điện thoại [DNHACUNGCAP_DIENTHOAI]
- Email [DNHACUNGCAP_EMAIL]

### TỔNG HỢP NHẬP THEO NHÂN VIÊN NHẬP — 7 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG] <TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-CỘNG>
- Tiền thuế [TIENTHUE] <TỔNG-CỘNG>
- Tiền hàng [TIENHANG] <TỔNG-CỘNG>
- Nhân viên nhập [DNHANVIEN_NAME]

### TỔNG HỢP THU CHI THEO LÝ DO — 3 cột
- Lý do thu chi [DLYDOTHUCHI_NAME]
- Số tiền thu [THU] <TỔNG-CỘNG>
- Số tiền chi [CHI] <TỔNG-CỘNG>

### TỔNG HỢP THU CHI THEO NGÀY — 3 cột
- Ngày [NGAY]
- Số tiền thu [THU] <TỔNG-CỘNG>
- Số tiền chi [CHI] <TỔNG-CỘNG>

### TỔNG HỢP XUẤT KHÁC THEO NGÀY — 7 cột
- Ngày [NGAY]
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG]
- Phí vận chuyển [PHIVANCHUYEN]
- Tiền giảm giá [TIENGIAMGIA]
- Tiền thuế [TIENTHUE]
- Tiền hàng [TIENHANG]

### TỔNG HỢP XUẤT KHÁC THEO NHÂN VIÊN XUẤT — 7 cột
- Loại [LOAI] (ẩn)
- Tổng cộng [TONGCONG]
- Phí vận chuyển [PHIVANCHUYEN]
- Tiền giảm giá [TIENGIAMGIA]
- Tiền thuế [TIENTHUE]
- Tiền hàng [TIENHANG]
- Nhân viên xuất [DNHANVIEN_NAME]

### TỔNG HỢP ĐẶT HÀNG THEO KHÁCH HÀNG — 10 cột
- Tên khách hàng [DKHACHHANG_NAME]
- Mã khách [DKHACHHANG_MAKHACH]
- Địa chỉ [DKHACHHANG_DIACHI]
- Điện thoại [DKHACHHANG_DIENTHOAI]
- Email [DKHACHHANG_EMAIL]
- Tiền hàng [TIENHANG]
- Tiền thuế [TIENTHUE]
- Tiền giảm giá [TIENGIAMGIA]
- Phí vận chuyển [PHIVANCHUYEN]
- Tổng cộng [TONGCONG]

### TỔNG HỢP ĐẶT HÀNG THEO NGÀY — 6 cột
- Ngày [NGAY]
- Tiền hàng [TIENHANG]
- Tiền thuế [TIENTHUE]
- Tiền giảm giá [TIENGIAMGIA]
- Phí vận chuyển [PHIVANCHUYEN]
- Tổng cộng [TONGCONG]

## 3. 36 báo cáo có SQL — đối chiếu `.frx` (layout + Total)

Cột `TableDataSource` trong `.frx` phải khớp cột `SELECT` của SQL; `Total` là tổng cộng trên band in (ReportSummary1 / GroupFooter1), chỉ cộng cột hiển thị — không đổi dấu, không tính toán lại.

### BIỂU ĐỒ DOANH SỐ THEO NHÓM
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NAME, TONGCONG; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: FromDate, ToDate

### BIỂU ĐỒ DOANH THU NGÀY TRONG THÁNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NGAY, THANG1, THANG2, THANG3, THANG4, THANG5, THANG6, THANG7, THANG8, THANG9, THANG10, THANG11, THANG12; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: Year

### BIỂU ĐỒ DOANH THU THÁNG TRONG NĂM
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột THANG, NAMNAY, NAMNGOAI; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: Year

### BIỂU ĐỒ THEO NHÂN VIÊN KINH DOANH
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NAME, TONGCONG; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: FromDate, ToDate

### BÁO CÁO 20 MẶT HÀNG BÁN CHẠY NHẤT
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột TENHANG, CODE, DVT, SOLUONG, GIABAN; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: DNHOMMATHANGID, FromDate, ToDate

### BÁO CÁO BÁN HÀNG THEO GIỜ
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột GIOVAO, TONGCONG, SOHOADON, ADD; SelectCommand: KHÔNG (0/36 file có)
- Total `TSOHOADON` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONGCONG` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### BÁO CÁO CHI TIẾT BÁN HÀNG THEO NGÀY
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột ID, NGAY, NAME, TONGCONG, KHACHHANG, TIENGIAMGIA, TIENHANG, TIENTHANHTOAN, TIENGIO, PHIDICHVU, TIENTHUE, CONGNO; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `Table1`: cột PARENTID, TENHANG, SOLUONG, DONGIA, THANHTIEN, TILEGIAMGIA, TIENGIAMGIA; SelectCommand: KHÔNG (0/36 file có)
- Total `TTongCong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTienHang` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGiamGia` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TThanhToan` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TThue` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TPhiDichVu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTienGio` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TConNo` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSoLuong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTienGiamMatHang` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TThanhTien` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### BÁO CÁO CHI TIẾT BÁN HÀNG THEO THU NGÂN
- Có `.frx`: KHÔNG (không có dòng manifest, không có file trên đĩa); có `.config`: không

### BÁO CÁO CHI TIẾT PHÂN QUYỀN HỆ THỐNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NHOM, MODE, NAME, PHANLOAI, NOTE, BIKHOA, XEM, THEM, SUA, XOA, GROUPNAME; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: SGROUPUSERID, USERID

### BÁO CÁO CÔNG NỢ KHÁCH HÀNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột TENKHACH, NHOMKHACH, MAKHACH, DIACHI, DIENTHOAI, EMAIL, NODAU, MUA, THANHTOAN, NOCUOI; SelectCommand: KHÔNG (0/36 file có)
- Total `TNODAU` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TMUA` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHANHTOAN` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TNOCUOI` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### BÁO CÁO CÔNG NỢ NHÀ CUNG CẤP
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột TENKHACH, NHOMKHACH, MAKHACH, DIACHI, DIENTHOAI, EMAIL, NODAU, MUA, THANHTOAN, NOCUOI; SelectCommand: KHÔNG (0/36 file có)
- Total `TNODAU` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TMUA` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHANHTOAN` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TNOCUOI` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### BÁO CÁO CẤU HÌNH BÀN KHU VỰC
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột ID, NAME; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `Table1`: cột ID, PARENTID, TUGIO, DENGIO, SOTIEN, BANGGIA; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `dtBan`: cột ID, KHUVUC, DKHUVUCID, TENBAN, NOTE, CACHTINHGIO, DONGIA, DBANGGIAID; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)

### BÁO CÁO HÀNG TỒN KHO
- Có `.frx`: có; có `.config`: có
- TableDataSource `Table0`: cột NHOMHANG, MAHANG, TENHANG, DVT, TON, TONTOITHIEU, TONTOIDA, GIAVON, GIATRIVON, GIABAN, GIATRIBAN; SelectCommand: KHÔNG (0/36 file có)
- Total `TTON_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRIVON_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRI_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTON_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRIVON_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRI_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DKHOHANGID, ToDate

### BÁO CÁO HÀNG TỒN KHO CÓ HẠN DÙNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NHOMHANG, MAHANG, TENHANG, DVT, TON, HANSUDUNG; SelectCommand: KHÔNG (0/36 file có)
- Total `TTON_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTON_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DKHOHANGID, ToDate

### BÁO CÁO HÀNG ĐÃ HẾT HẠN
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NHOMHANG, MAHANG, TENHANG, DVT, TON, HANSUDUNG; SelectCommand: KHÔNG (0/36 file có)
- Total `TTON_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTON_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DKHOHANGID, ToDate

### BÁO CÁO KẾT QUẢ KINH DOANH
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột LOAI, NOIDUNG, THANGNAY, TILEDT, TILE, TILE2, BIEUTUONG, TANGGIAM, THANGTRUOC, DT; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: FromDate, ToDate

### BÁO CÁO THEO HẠN DÙNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NHOMHANG, MAHANG, TENHANG, DVT, TON, HANSUDUNG; SelectCommand: KHÔNG (0/36 file có)
- Total `TTON_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTON_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DKHOHANGID, FromDate, ToDate

### BÁO CÁO TỒN QUỸ
- Có `.frx`: có; có `.config`: có
- TableDataSource `Table0`: cột SOPHIEU, NGAY, DIENGIAI, TAOBOI, THOIGIAN, THU, CHI, TON, LOAI; SelectCommand: KHÔNG (0/36 file có)
- Total `TTHU` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TCHI` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTON` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### BÁO CÁO TỔNG HỢP GIÁ TRỊ BÁN THEO THÁNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột CODE, NAME, DVT, D01, D02, D03, D04, D05, D06, D07, D08, D09, D10, D11, D12, D13, D14, D15, D16, D17, D18, D19, D20, D21, D22, D23, D24, D25, D26, D27, D28, D29, D30, D31, NHOM; SelectCommand: KHÔNG (0/36 file có)
- Total `T01` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T02` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T03` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T04` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T05` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T06` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T07` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T08` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T09` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T10` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `t11` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T12` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T13` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T14` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T15` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T16` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T17` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T18` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T19` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T20` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T21` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T22` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T23` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T24` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T26` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T27` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T28` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T29` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T30` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T31` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T25` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN01` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN02` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN03` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN04` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN05` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN06` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN07` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN08` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN09` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN10` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN11` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN12` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN13` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN14` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN15` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN16` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN17` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN18` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN19` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN20` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN21` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN22` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN23` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN24` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN25` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN26` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN27` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN28` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN29` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN30` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN31` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: MONTH, YEAR

### BÁO CÁO TỔNG HỢP SỐ LƯỢNG BÁN THEO THÁNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột CODE, NAME, DVT, D01, D02, D03, D04, D05, D06, D07, D08, D09, D10, D11, D12, D13, D14, D15, D16, D17, D18, D19, D20, D21, D22, D23, D24, D25, D26, D27, D28, D29, D30, D31, NHOM; SelectCommand: KHÔNG (0/36 file có)
- Total `T01` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T02` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T03` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T04` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T05` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T06` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T07` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T08` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T09` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T10` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `t11` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T12` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T13` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T14` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T15` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T16` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T17` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T18` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T19` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T20` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T21` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T22` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T23` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T24` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T26` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T27` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T28` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T29` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T30` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T31` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `T25` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN01` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN02` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN03` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN04` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN05` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN06` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN07` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN08` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN09` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN10` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN11` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN12` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN13` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN14` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN15` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN16` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN17` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN18` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN19` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN20` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN21` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN22` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN23` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN24` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN25` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN26` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN27` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN28` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN29` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN30` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TN31` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: MONTH, YEAR

### BÁO CÁO TỔNG HỢP XUẤT NHẬP TỒN
- Có `.frx`: có; có `.config`: có
- TableDataSource `Table0`: cột NHOMHANG, MAHANG, TENHANG, DVT, GIAVON, TONDAU, GIATRITONDAU, NHAP, GIATRINHAP, XUAT, GIATRIXUAT, TON, GIATRITON; SelectCommand: KHÔNG (0/36 file có)
- Total `TTONDAU_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRITONDAU_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TNHAP_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRINHAP_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TXUAT_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTON_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRITON_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONDAU_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRITONDAU_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TNHAP_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRINHAP_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TXUAT_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRIXUAT_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTON_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRITON_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIATRIXUAT_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DKHOHANGID, DNHOMHANGID, FromDate, ToDate

### BÁO CÁO TỔNG HỢP XUẤT NHẬP TỒN CHI TIẾT
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột NHOM, CODE, NAME, DVT, TONDAU, SLNHAPMUA, SLNHAPDIEUCHINH, SLNHAPCHUYENKHO, SLXUATBAN, SLXUATDIEUCHINH, SLXUATCHUYENKHO, SLXUATKHAC, TONKHO, SLXUATVATTU, TONGNHAP, TONGXUAT; SelectCommand: KHÔNG (0/36 file có)
- Total `TSLNHAPMUA_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLNHAPMUA` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLNHAPDIEUCHINH_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLNHAPDIEUCHINH` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLNHAPCHUYENKHO_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLNHAPCHUYENKHO` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATBAN_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATBAN` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATDIEUCHINH_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATDIEUCHINH` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATCHUYENKHO_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATCHUYENKHO` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATKHAC_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSLXUATKHAC` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONKHO_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONKHO` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONDAU_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONDAU` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTNHAP_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTXUAT_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTXUATVATTU_NHOM` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTNHAP` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTXUAT` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTXUATVATTU` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DKHOHANGID

### CHI TIẾT BÁN HÀNG THEO HÓA ĐƠN
- Có `.frx`: có; có `.config`: có
- TableDataSource `Table0`: cột TENHANG, GIOTHANHTOAN, NHANVIEN, THUNGAN, DVT, SOLUONG, DONGIA, TILEGIAMGIA, THANHTIEN, TOTAL, TONGCONG, CKDONHANG, SOPHIEU, TIENGIAMGIA; SelectCommand: KHÔNG (0/36 file có)
- Total `Total` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### CHI TIẾT HOẠT ĐỘNG TRONG NGÀY
- Có `.frx`: có; có `.config`: có
- TableDataSource `Table0`: cột NAME, TENKHACH, TIENHANG, TIENGIAMGIA, TIENTHUE, PHIVANCHUYEN, TONGCONG, GIOTHANHTOAN, TIENMAT, CHUYENKHOAN, TIENTHE, CONNO, VOUCHER, THETRATRUOC, TRUTICHLUY, DATTRUOC; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `Table1`: cột CODE, DVT, SOLUONG, DONGIA, TILEGIAMGIA, THANHTIEN, TENHANG; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `Table2`: cột NAME, DIENGIAI, TENDOITUONG, THUTM, THUCK, CHITM, CHICK; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `Table3`: cột NAME, NHACUNGCAP, TONGCONG, TIENTHANHTOAN, CONNO; SelectCommand: KHÔNG (0/36 file có)
- Total `TTIENHANG` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TGIAMGIA` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHUE` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TPHIVANCHUYEN` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONGCONG` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSOLUONG` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHANHTIEN` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHU` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TCHI` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTIENMAT` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TCHUYENKHOAN` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTIENTHE` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TCONNO` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONGCONGNHAP` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTONGTHANHTOANNHAP` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TCONNONHAP` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHUCK` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TCHICK` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TONGTIENHANGCHUAGIAMGIA` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TONGGIAMGIATIENHANG` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TVOUCHER` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHETRATRUOC` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTRUTICHLUY` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate, USERID

### CHI TIẾT LÃI THEO HÓA ĐƠN (GIÁ NHẬP)
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột TENHANG, NHOM, SOLUONG, DONGIA, GIANHAP, DOANHTHU, LOINHUAN, GIAMGIA, THUCTHU, DOITRA, GIATRIVON, DVT, LOAI, CODE; SelectCommand: KHÔNG (0/36 file có)
- Total `SumDoanhThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuanNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumDoanhThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuan` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaGroup` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGia` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSLNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSoLuong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTraNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTra` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVonNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVon` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaTong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### CHI TIẾT LÃI THEO HÓA ĐƠN (GIÁ VỐN)
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột TENHANG, NHOM, SOLUONG, DONGIA, GIANHAP, DOANHTHU, LOINHUAN, GIAMGIA, THUCTHU, DOITRA, GIATRIVON, DVT, LOAI, CODE; SelectCommand: KHÔNG (0/36 file có)
- Total `SumDoanhThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuanNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumDoanhThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuan` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaGroup` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGia` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSLNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSoLuong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTraNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTra` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVonNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVon` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaTong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### CÔNG THỨC ĐỊNH LƯỢNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột MATHANG, VATTU, SOLUONG, DVT, DVT_DINHLUONG, NHOM; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: DNHOMMATHANGID

### PHÂN TÍCH TÌNH HÌNH BÁN HÀNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột SOLUONG, DONGIA, THANHTIEN, NAME; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `Table1`: cột SOLUONG, THANHTIEN, NAME, TILE; SelectCommand: KHÔNG (0/36 file có)
- TableDataSource `Table2`: cột SOLUONG, DONGIA, SUM, TILE; SelectCommand: KHÔNG (0/36 file có)
- Total `TSOLUONG_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHANHTIEN_TOTAL` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSOLUONG_TOTAL1` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHANHTIEN_TOTAL1` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TSOLUONG_TOTAL2` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TTHANHTIEN_TOTAL2` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### THẺ KHO
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột SOPHIEU, NGAYTHANG, DIENGIAI, SLNHAP, SLXUAT, SLTON, MODE; SelectCommand: KHÔNG (0/36 file có)
- Total `TTON` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TNHAP` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TXUAT` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TONDAUKY` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DKHOHANGID, DMATHANGID, FromDate, ToDate

### TỔNG HỢP CÔNG NỢ KHÁCH HÀNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột MAKHACH, TENKHACH, DIACHI, DIENTHOAI, EMAIL, TONGNO, NHOMKHACH; SelectCommand: KHÔNG (0/36 file có)
- Total `TONGNO` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DNHOMKHACHID

### TỔNG HỢP CÔNG NỢ NHÀ CUNG CẤP
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột MAKHACH, TENKHACH, DIACHI, DIENTHOAI, EMAIL, TONGNO, NHOMKHACH; SelectCommand: KHÔNG (0/36 file có)
- Total `TONGNO` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: DNHOMNHACUNGCAPID

### TỔNG HỢP DOANH THU THEO LOẠI ĐỒ
- Có `.frx`: có; có `.config`: có
- TableDataSource `Table0`: cột NGAY, DOAN, DOUONG, DICHVU, DOKHAC, TONGCONG; SelectCommand: KHÔNG (0/36 file có)
- Total `TDOAN` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TDOUONG` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TDICHVU` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TDOKHAC` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `Total` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### TỔNG HỢP LÃI GỘP THEO MẶT HÀNG (GIÁ NHẬP)
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột TENHANG, NHOM, SOLUONG, DONGIA, GIANHAP, DOANHTHU, LOINHUAN, GIAMGIA, THUCTHU, DOITRA, LOAI, MAHANG, DVT, GIATRIVON; SelectCommand: KHÔNG (0/36 file có)
- Total `SumDoanhThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuanNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumDoanhThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuan` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaGroup` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGia` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSLNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSoLuong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTraNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTra` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVonNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVon` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaTong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### TỔNG HỢP LÃI GỘP THEO MẶT HÀNG (GIÁ VỐN)
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột TENHANG, NHOM, SOLUONG, DONGIA, GIANHAP, DOANHTHU, LOINHUAN, GIAMGIA, THUCTHU, DOITRA, GIATRIVON, MAHANG, DVT, LOAI; SelectCommand: KHÔNG (0/36 file có)
- Total `SumDoanhThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuanNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumDoanhThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumLoiNhuan` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaGroup` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThuNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGia` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumThucThu` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSLNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalSoLuong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTraNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `TotalDoiTra` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVonNhom` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiaTriVon` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Total `SumGiamGiaTong` = `None` (in trên (không ghi — tổng toàn báo cáo))
- Tham số trong `.frx`: FromDate, ToDate

### ĐỐI CHIẾU CÔNG NỢ KHÁCH HÀNG
- Có `.frx`: có; có `.config`: không
- TableDataSource `Table0`: cột LOAI, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NGAY, SO, TONGCONG, THANHTOAN, THU, CHI, TILEGIAMGIA; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: DKHACHHANGID, FromDate, ToDate

### ĐỐI CHIẾU CÔNG NỢ NHÀ CUNG CẤP
- Có `.frx`: có; có `.config`: có
- TableDataSource `Table0`: cột LOAI, MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NGAY, SO, TONGCONG, THANHTOAN, THU, CHI; SelectCommand: KHÔNG (0/36 file có)
- Total: không có (mọi tổng — nếu có — đều do SQL/CODE tính, không phải mẫu in)
- Tham số trong `.frx`: DNHACUNGCAPID, FromDate, ToDate

## 4. 14 file `.config` mồ côi (trỏ ID không có trong `SREPORT` lẫn `SFORM`)

Các mẫu này không gắn với báo cáo nào còn tồn tại — khả năng cao là tàn dư của báo cáo đã xóa, hoặc mẫu của biến thể khác. Ghi lại để đối chiếu khi có `DATA.fdb` POS TAP thật.

### `029cd807-2ed1-4010-b314-906ef8ca0a12.config` — 2 cột
- Ngày [NGAY]
- Số tiền [CHI]

### `07e3f3f1-1d2f-433d-b323-c2faadb8936f.config` — 6 cột
- Tên mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- SL trả [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tên khách hàng [DKHACHHANG_NAME] <NHÓM1>

### `0e4e3adf-238b-424f-ac29-72137632a63f.config` — 2 cột
- Tên lý do [DLYDOTHUCHI_NAME]
- Số tiền [THU]

### `2e234360-274d-4cf5-b2c1-0a71623cca89.config` — 7 cột
- Giờ thanh toán [GIOTHANHTOAN]
- Tổng cộng [TONGCONG]
- Phí vận chuyển [PHIVANCHUYEN]
- Tiền giảm giá [TIENGIAMGIA]
- Tiền thuế [TIENTHUE]
- Tiền hàng [TIENHANG]
- Đổi trả [DOITRA]

### `3a81bdd6-dda7-438a-9a68-e1bcf083a677.config` — 6 cột
- Tên mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- SL trả [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Giờ thanh toán [TDONHANG_GIOTHANHTOAN] <NHÓM1>

### `3be0533c-098e-4a8a-977b-c02a148a27c2.config` — 6 cột
- Tên mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- SL trả [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Ngày [TDONHANG_NGAY] <NHÓM1>

### `5045ef64-0d27-44d3-8af1-cec0d4fe87e2.config` — 7 cột
- Loại [TDONHANG_LOAI]
- Nhân viên bán [DNHANVIEN_NAME] <NHÓM1>
- Mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Số lượng nhập [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng xuất [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>

### `65f422be-8166-4ecc-8769-775a54cb26f5.config` — 2 cột
- Tên nhân viên [DNHANVIEN_NAME]
- Tổng cộng [TONGCONG]

### `6bb432f4-4c44-42be-99d1-eee7d04ab864.config` — 2 cột
- Ngày [NGAY]
- Tổng cộng [TONGCONG]

### `880ca126-bc60-4432-b50c-0971bdf0de92.config` — 6 cột
- Tên mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- SL trả [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tài khoản [SUSER_NAME] <NHÓM1>

### `9a4a54c5-a151-4bed-b9b9-962950912890.config` — 7 cột
- Ngày [TDONHANG_NGAY] <NHÓM1>
- Loại [TDONHANG_LOAI]
- Mặt hàng [DMATHANG_NAME] <NHÓM2>
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- Số lượng nhập [SLNHAP] <TỔNG-CỘNG>
- Số lượng xuất [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>

### `ec24094a-527a-45bd-bcd3-056d545f28db.config` — 2 cột
- Tên nhân viên [DNHANVIEN_NAME]
- Tổng cộng [TONGCONG]

### `ec6836ed-33d0-4d83-866e-1e9366174d06.config` — 6 cột
- Tên mặt hàng [DMATHANG_NAME]
- Thành tiền [THANHTIEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Đơn giá [DONGIA]
- SL trả [SLNHAP] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Số lượng [SLXUAT] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tên nhân viên [DNHANVIEN_NAME] <NHÓM1>

### `f73767bb-cfe5-475a-890e-815efe8279da.config` — 18 cột
- Ghi chú [NOTE]
- Số phiếu [NAME]
- Ngày [NGAY]
- Khách hàng [DKHACHHANG_NAME]
- Tổng cộng [TONGCONG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Phí vận chuyển [PHIVANCHUYEN] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tiền giảm giá [TIENGIAMGIA] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ giảm giá [TILEGIAMGIA]
- Tiền thuế [TIENTHUE] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Tỉ lệ thuế [TILETHUE]
- Tiền hàng [TIENHANG] <TỔNG-NHÓM1,TỔNG-CỘNG>
- Kho xuất [DKHOHANG_NAME]
- Nhân viên bán [DNHANVIEN_NAME]
- Giờ thanh toán [GIOTHANHTOAN] <NHÓM1>
- Khách đưa [KHACHDUA]
- Trả lại [TRALAI]
- Thu ngân [SUSER_NAME]
- Đổi trả [DOITRA] <TỔNG-NHÓM1,TỔNG-CỘNG>
## 5. Mẫu của form (không phải báo cáo)

- Form **"Công nợ nhà cung cấp"**: có 1 file `.frx` (mẫu A4) + 2 file `.config`. Form **"Công nợ khách hàng"**: không có file mẫu nào.
- 7 báo cáo có SQL đồng thời có `.config` trên đĩa (cấu hình cột hiển thị dự phòng): xem §3 từng báo cáo; SQL vẫn là nguồn chính thức.
