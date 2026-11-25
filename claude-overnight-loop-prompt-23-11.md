# Prompt vòng lặp qua đêm — FnB (dán vào Claude Code, phiên mới)

Làm spec từng chức năng mục Hoạt động, 1 spec = 1 commit, push main. Chạy tới 6h30 sáng 24/11 hoặc khi hết việc (hết quota thì chờ hồi rồi chạy tiếp).

## 0. Chuẩn bị (làm 1 lần đầu phiên)
- Thư mục làm việc: repo FnB, nhánh main. Kiểm tra `git log --oneline -1` — main hiện tại là `ab440dd` (23/11 23:37, đã gồm patch Theo dõi đặt phòng + file prompt này).
- Computer-use đã bật sẵn trong setting (giữ luôn cho mọi phiên mới) — dùng khi cần chụp màn hình app gốc POS TAP.
- Model: Sonnet mặc định.

## 1. Tạo file QUEUE.md (chỉ lần đầu, nếu chưa có)
- Tạo `docs/specs/QUEUE.md` với đúng nội dung ở cuối prompt này, commit riêng `docs: add QUEUE.md`, push.
- Từ sau đó, mỗi spec xong thì cập nhật QUEUE.md trong cùng commit spec (đánh dấu xong + ghi commit hash).

## 2. Vòng lặp cho mỗi chức năng (lặp lại)
1. `git pull` lấy main mới nhất.
2. Đọc QUEUE.md, lấy chức năng tiếp theo có trạng thái CHƯA LÀM (theo thứ tự trong file).
3. KIỂM TRA REVIEW TRƯỚC: chạy `gh issue list --label spec-review --state open` (repo Chienhandsome/FnB).
   - Có issue mở cho spec trước: đọc `gh issue view <số>`, sửa theo review trong file spec (commit riêng `fix: spec <tên> theo review`), đóng issue bằng `gh issue close <số>` khi sửa xong. Rồi mới làm spec tiếp.
   - Không có issue mở: làm spec tiếp luôn (review của Muse sẽ tới sau, xử lý ở vòng sau).
4. Khám phá DB TRƯỚC (rẻ): tra bảng liên quan trong DATA.fdb thật của POS TAP (KHÔNG dùng DEMO.fdb). Chỉ dùng computer-use chụp màn hình app gốc cho chỗ DB không cho biết, tối đa 5–8 ảnh/spec.
5. Viết DELTA spec vào `docs/specs/<tên-file>.md` (chỉ viết phần khác biệt so với spec khuôn Danh mục mặt hàng + các spec trước, không viết lại). Đủ các phần theo mẫu cũ (§1 metadata + bảng, §2 UI/flow, §3 dữ liệu, §4 quyền, §5 câu hỏi chờ quyết, §6 quyết định đã chốt, §7 nghiệm thu, §8 gợi ý implement).
6. Áp các quyết định đã chốt, không hỏi lại: xóa cứng cả app (không thùng rác), LamTron 1000 AwayFromZero, tiền decimal NUMERIC(18,2), F&B thuần chỉ COFFEE (0) + POS (1), bán lẻ tự phục vụ. Chỗ nào khác app gốc thì ghi rõ trong spec để Chiến quyết, không tự sửa lén.
7. Cập nhật QUEUE.md (đánh dấu xong + commit hash), commit RIÊNG `docs: spec <tên chức năng>`, push main. 1 spec = 1 commit, không gộp 2 chức năng vào 1 commit.
8. Quay lại bước 2.1 cho chức năng tiếp theo.

## 3. Chạy bằng /loop (dynamic) + hết quota thì chờ hồi, không dừng
- Chạy prompt này qua `/loop` không interval. Sau mỗi spec xong (hoặc khi gặp giới hạn quota), gọi `ScheduleWakeup` với `delaySeconds` 1800–3600 và truyền lại đúng prompt này.
- Hết quota cửa sổ 5h: KHÔNG dừng hẳn. Tiếp tục đặt `ScheduleWakeup` (3600s) cho tới khi quota hồi rồi làm tiếp.
- Mỗi lần thức dậy, việc đầu tiên: `git status` (chỉ chấp nhận file spec/QUEUE), `git pull`, đọc QUEUE.md, làm spec CHƯA LÀM đầu tiên. Spec dở dang chưa commit thì làm lại từ đầu.

## 3b. Dừng hẳn khi (ghi vào QUEUE.md dòng cuối, gọi `ScheduleWakeup` với `stop: true`)
- Hết chức năng trong QUEUE.md.
- `git push` conflict / lỗi không tự sửa được.
- App gốc POS TAP lỗi / hết hạn dùng thử.
- Tới 6h30 sáng 24/11 (kiểm tra giờ hệ thống mỗi lần thức dậy; quá 6h30 thì không bắt đầu spec mới).

## 4. Không làm
- Không sửa code (chỉ spec + QUEUE.md). Không sửa spec của chức năng đã implement xong, trừ khi issue review yêu cầu. Không commit/push gì khác ngoài spec + QUEUE.md + fix theo review.

## Nội dung QUEUE.md ban đầu (copy nguyên văn)

```markdown
# QUEUE — Spec + Implement FnB (mục Hoạt động)

Thứ tự theo menu Hoạt động thật của POS TAP. 1 spec = 1 commit. Cập nhật trạng thái sau mỗi spec.

## Đã xong (spec + implement)
- [x] Danh mục mặt hàng — spec `6a57cd3`, patch 1 đã giao
- [x] Danh mục bàn khu vực — spec `6a87224`, patch 2 đã giao
- [x] Khách đặt hàng — spec `ce52532`, patch 3 đã giao
- [x] Theo dõi đặt phòng — spec `13d90f8`, patch đã apply + commit ở `ab440dd`; Q6 + 5 mục §5 đã chốt "giữ y hệt hết" (23/11)

## Spec xong, chờ implement (Muse)
- [ ] Sử dụng dịch vụ — spec `0e37e01`, review xong (issue #1), chờ Muse implement
- [ ] Quản lý bán hàng — spec `295df3f` (gộp chung file với Điều chỉnh hóa đơn, giữ nguyên file gộp; lúc implement tách thành 2 patch)
- [ ] Điều chỉnh hóa đơn — spec `295df3f` (chung file như trên)

## Tiếp theo (Claude làm spec)
- [ ] Báo giá bán hàng — LÀM TIẾP THEO (Chiến chốt 23/11, giữ đúng thứ tự menu)
- [ ] Lưu vết hoạt động
- [ ] Thống kê doanh thu
- [ ] Thống kê mặt hàng bán
- [ ] Tổng hợp kết quả kinh doanh
- [ ] Chi tiết hoạt động ngày
- [ ] Danh mục hóa đơn hủy

## Ghi chú
- Quyết định chốt áp cho mọi spec: xóa cứng cả app, LamTron 1000, tiền decimal, F&B thuần COFFEE + POS, bán lẻ tự phục vụ.
- Sử dụng dịch vụ (Chiến chốt 23/11): Q1 = chỉ VNĐ + LamTron 1000, bỏ nhánh USD; P3 = giữ trigger gốc — lấy thân trigger TDONHANG_TRIGGER từ DATA.fdb trước khi implement phần liên kết bàn–bill.
- Review của Muse nằm ở GitHub Issues, label `spec-review` — kiểm tra trước mỗi spec mới.
- Dừng: hết queue / push conflict / app gốc lỗi / 6h30 sáng 24/11. Hết quota 5h thì chờ hồi, không dừng.
```
