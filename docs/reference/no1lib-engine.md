# Tham chiếu: engine No1Lib (POS TAP)

> Tài liệu đọc-hiểu, dựng từ việc decompile các DLL `No1Lib.*` trong `D:\Working\FnB\POS TAP` (ilspycmd 11.2). Mục đích: hiểu engine gốc để **viết lại bằng code của mình**, KHÔNG link lại các DLL này vào app mới (chưa rõ bản quyền, và No1Lib.Sys đã bị obfuscate).

## 0. Kết luận quan trọng nhất

**No1Lib KHÔNG chứa logic nghiệp vụ (tiền / giá / kho / công nợ).** Đã quét toàn bộ mã decompile: 0 kết quả cho `ThanhTien`, `DonGia`, `ChietKhau`, `CongNo`, `TonKho`, `Round`… No1Lib chỉ là **khung chạy tổng quát**: truy cập DB, GUI, designer, báo cáo.

Logic nghiệp vụ thật nằm trong **mã form đã xuất** ở `D:\Working\FnB\POS_TAP_SRC_EXPORT\` (SFORM/STABLEDESC/SREPORT). Ví dụ `SFORM\Hóa đơn nhà hàng__f3f7bb77\code.cs` (219 KB) có 416 chỗ dùng từ khóa tiền/giá. Đó mới là nơi cần đọc khi làm tính tiền, giá, kho.

→ Khi cần tính tiền/giá/kho: đọc `POS_TAP_SRC_EXPORT`, không đọc No1Lib. No1Lib chỉ cho biết **API mà các form đó gọi**.

## 1. Tình trạng decompile từng DLL

| DLL | KB | Kết quả | Dùng được |
|---|---|---|---|
| No1Lib.Db | 44 | Sạch, tên gốc đầy đủ | **Có** — tầng truy cập DB |
| No1Lib.Utils | 24 | Sạch | **Có** — ép kiểu, mã hóa, bản quyền |
| No1Lib.Sys | 5784 | **Obfuscate** (namespace/tên lớp ngẫu nhiên) | Rất ít — chỉ vài API public |
| No1Lib.Sys.Ts | 580 | Obfuscate một phần | Ít |
| No1Lib.Designer | 128 | Đọc được một phần | Tham khảo designer |
| No1Lib.PluginHelper | 20 | Nhỏ | Ít |
| No1Lib.Core | 79 | Decompile ra rỗng (0 KB) | Không |
| No1Lib.Run | 1040 | Decompile ra rỗng (0 KB) | Không |

Bản decompile đầy đủ nằm ở scratchpad (tạm): `…\scratchpad\no1lib_src\`. Không commit vào repo.

## 2. No1Lib.Db — tầng truy cập dữ liệu (quan trọng để viết lại)

### 2.1 Lớp `Database` (bọc Firebird `FbConnection`)
Các hàm tiện ích mà mọi form dùng:
- `DataTable GetTable(sql)` / `GetTable(table, where)` / `GetTable(table, where, orderBy)`
- `DataRow GetFirstRow(sql)` / `GetFirstRow(table, where)`
- `object GetFirstField(sql)` và bản ép kiểu: `GetFirstFieldInt`, `GetFirstFieldDec`, `GetFirstFieldString` (có overload `(Table, Field, ID)` = lấy 1 cột theo ID).
- `void ExecSql(sql | FbCommand)`, `FbCommand GetCommand(sql)`.
- Giao dịch: `BeginTransaction()`, `Commit()`, `Rollback()`.
- Kết nối: `TryConnect(out error)`, `ReConnect()`, `Copy()` (clone connection), `Disconnect()`.
- Có `OnExecuteSqlHandler` (event bắn mỗi câu SQL — hữu ích để log/đồng bộ).

`SQLDatabase` là bản song song cho SQL Server (có `DbDate`/`DbDateTime` = `getdate()`); app POS dùng Firebird nên bỏ qua.

### 2.2 `BaseRow<I>` — ORM nhẹ, quy ước LƯU/XÓA phải giữ y hệt

Đây là phần **bắt buộc tái hiện đúng** khi viết lại, vì toàn bộ dữ liệu theo quy ước này:

- **Khóa chính `ID`**: mặc định `Guid.NewGuid().ToString()`. Có event `CustomGenID(ref ID)` để form tự sinh mã (ví dụ số chứng từ) → ghi đè GUID.
- **`STATUS`**: mặc định **30** khi thêm mới (= bản ghi hoạt động). Xóa mềm = đặt **STATUS = -30** (thùng rác). Row mới tạo trong bộ nhớ cũng gán `Status = 30`.
- **Cột audit** (engine tự điền, KHÔNG cho form ghi đè các cột này):
  - Thêm mới: `USERCREATEDID`, `TIMECREATED = current_timestamp`.
  - Sửa: `USERMODIFIEDID`, `TIMEMODIFIED = current_timestamp`.
  - Khi build câu lệnh, engine bỏ qua `ID, USERCREATEDID, TIMEMODIFIED, TIMECREATED, USERMODIFIEDID` trong updateDic.
- **Tên cột**: viết hoa hết (`field.ToUpper()`); tên bảng bọc nháy kép `"TABLE"`.
- **`Update()`**: nếu row mới → `INSERT`, nếu có ID → `UPDATE ... WHERE ID = '<id>'`. Có overload `Update(Database db, string userID)` để chạy trong transaction + ghi đúng người sửa.
- **`Delete()`**: xóa hẳn (`DELETE FROM`). **`MoveToRecycle()`**: xóa mềm (`STATUS = -30`). Có `Update()` không tham số = mặc định chuyển vào recycle khi row bị Delete.
- **Lazy load**: truy cập `Row`/`this[field]` lần đầu sẽ `Fill()` = `SELECT FIRST 1 * FROM "<table>" WHERE ID = '<id>'`.
- `SetNull(field)` / `IsNullValue` / `IsEmpty` cho cột null.

⚠️ **Cảnh báo an toàn khi viết lại**: engine gốc **nối thẳng `ID` vào chuỗi SQL** (`WHERE ID = '" + ID + "'`) — có nguy cơ SQL injection. Giá trị cột thì đã tham số hóa (`@p0…`). Bản viết lại nên **tham số hóa cả ID**. Quy ước STATUS/audit/GUID thì giữ nguyên.

### 2.3 Khác
- `BaseStoreProc(name, isFunction)` + `BaseStoreParam(val, FbDbType)` → gọi stored proc/function, trả `FirstDataTable`.
- `BaseFunction.GetObject(name, isScalar, params…)` → gọi function vô hướng.
- Enum `RowStatus`, `TranStatus`, `Logical` (kiểu dữ liệu logic của khung).

## 3. No1Lib.Utils — tiện ích (đọc được hết)

### 3.1 `ConvertTo` — ép kiểu an toàn từ giá trị DB (mọi form dùng dày đặc)
Quy ước giá trị mặc định cần giữ đúng khi viết lại:
- `Int(obj)` → 0 nếu null/lỗi. `Short` → 0. `Decimal` → 0. `Float` → `(float)Decimal`.
- `Long(obj)` → **-1** nếu null/lỗi (khác 0!).
- `String(obj)` → "" nếu null; nếu là `byte[]` thì giải mã UTF-8.
- `Bool` → false. `Date` → `DateTime.MinValue`.
- `ByteArr(val)` = UTF-8 bytes; `StringEx(byte[])` = UTF-8 string; `Image(obj)` từ byte[].
- `DataTableToArray` / `DataTable(obj, dt)` = serialize DataTable qua XML (dùng cho cột blob lưu bảng con).

### 3.2 `Security` — mã máy + mã hóa (liên quan "Bản quyền máy tính bảng")
- `GetMachineCode()`: lấy `SerialNumber` từ `Win32_BIOS`, mã hóa rồi quy về một số → đây là **mã máy** để khóa bản quyền.
- `Encrypt/Decrypt(text, key)`: **DES** với IV cố định `{18,52,86,120,144,171,205,239}`, key < 8 ký tự thì đệm `"ccccccccc"`, lấy 8 byte đầu. Base64.
- `Environments.InitEnviroment(product)`: ghi file license vào `System32`, hết hạn **30 ngày** kể từ ngày tạo file (`CreationTime`) → `NotYet`.

→ Khi làm tính năng bản quyền máy tính bảng, cần đối chiếu đúng thuật toán này nếu muốn tương thích license cũ; nếu làm license mới thì chỉ tham khảo.

### 3.3 `Hash` — băm có salt (SHA1/256/384/512/MD5)
`ComputeHash` tự sinh salt 4–8 byte, nối salt vào cuối hash, Base64. `VerifyHash` tách salt ra kiểm. Lưu ý: mật khẩu người dùng app gốc **để chữ thường dạng thô** (xem handoff), không chắc dùng Hash này — cần kiểm cột mật khẩu thực tế trong DB trước khi quyết.

## 4. No1Lib.Sys (obfuscate) — không khai thác được

Namespace thật còn lại: `No1Lib.Sys`, `.Framework`, `.Framework.Designer/Management/Report`, `.GUI`, `.NEW`, `.Properties`; phần còn lại là chuỗi ngẫu nhiên. Đây là nơi engine nạp SFORM từ DB, dựng UI runtime, chạy báo cáo FastReport. Không có tên gợi nghĩa để lần logic. → Hiểu hành vi qua **cách các form gọi** (`using No1Lib.Sys;`) trong `POS_TAP_SRC_EXPORT`, không qua đọc No1Lib.Sys.

## 5. Ghi chú kỹ thuật khi đọc mã đã xuất

- File `.cs` trong `POS_TAP_SRC_EXPORT` là UTF-8, nhiều dòng **rất dài**. `grep` của Git Bash báo sai (0 kết quả) với các file này. **Dùng ripgrep / công cụ Grep** để tìm — đã kiểm chứng (bash grep ra 0, ripgrep ra 416).
- Decompiler: `ilspycmd` đã cài global (`~/.dotnet/tools`). Lệnh: `ilspycmd <dll> -o <outdir> -r "D:\Working\FnB\POS TAP"`.

## 6. Việc nên làm tiếp (gợi ý)
1. Khi dựng tầng Data cho app mới: tái hiện đúng quy ước `BaseRow` (GUID ID, STATUS 30/-30, audit USERCREATED/MODIFIED + TIME*), nhưng tham số hóa toàn bộ SQL.
2. Đọc `SFORM\Hóa đơn nhà hàng`, `Hóa đơn bán hàng`, `Phiếu nhập kho`, các SREPORT lãi gộp/giá vốn trong `POS_TAP_SRC_EXPORT` để rút công thức tiền/giá/kho/công nợ — đây là bước đối chiếu nghiệp vụ trong handoff.
