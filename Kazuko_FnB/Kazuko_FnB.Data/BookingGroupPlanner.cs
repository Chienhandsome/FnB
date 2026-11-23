using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>Kế hoạch đoàn thuần (chỉ dùng kiểu Domain) — để Data không phải tham chiếu Services. BookingRules.PlanGroupChanges gọi lại hàm này.</summary>
    public static class BookingGroupPlanner
    {
        /// <summary>
        /// Lập kế hoạch thay đổi đoàn từ danh sách bàn mới chọn. Thuần, không đụng DB.
        /// - Dòng đang sửa giữ bàn cũ nếu còn được chọn, ngược lại lấy bàn đầu tiên (BeforeSave #2);
        ///   nếu bàn đầu đã có dòng khác giữ thì dòng kia bị xóa (một bàn chỉ một dòng trong đoàn).
        /// - Dòng của bàn bị bỏ được tái dùng cho bàn mới (giữ số phiếu cũ); hết thì tạo dòng mới.
        /// </summary>
        public static GroupChangePlan Plan(List<GroupRowRef> existing, string currentId, List<string> roomIds)
        {
            if (roomIds == null || roomIds.Count == 0)
                throw new ArgumentException("Phiếu đặt phải có ít nhất một bàn.", "roomIds");
            if (existing == null)
                existing = new List<GroupRowRef>();
            string currentOldRoom = null;
            foreach (GroupRowRef r in existing)
            {
                if (string.Equals(r.Id, currentId, StringComparison.Ordinal))
                {
                    currentOldRoom = r.RoomId;
                    break;
                }
            }
            string currentRoom = currentOldRoom != null && roomIds.Contains(currentOldRoom)
                ? currentOldRoom
                : roomIds[0];

            var plan = new GroupChangePlan
            {
                CurrentRoomId = currentRoom,
                Keep = new List<GroupRowRef>(),
                Reuse = new List<GroupRowRef>(),
                DeleteIds = new List<string>(),
                CreateRoomIds = new List<string>()
            };
            var covered = new HashSet<string>(StringComparer.Ordinal);
            var spare = new List<GroupRowRef>();
            // Phiếu mới (dòng đang sửa chưa có trong đoàn): dòng này đã chiếm bàn đầu
            // (SaveBooking chèn nó trước), nên đánh dấu bàn đó đã có dòng — nếu không,
            // vòng lặp dưới sẽ tạo thừa một dòng đoàn nữa cho đúng bàn đó.
            bool currentInExisting = false;
            foreach (GroupRowRef r in existing)
            {
                if (string.Equals(r.Id, currentId, StringComparison.Ordinal))
                {
                    currentInExisting = true;
                    break;
                }
            }
            if (!currentInExisting)
            {
                plan.Keep.Add(new GroupRowRef { Id = currentId, RoomId = currentRoom, Name = null });
                covered.Add(currentRoom);
            }
            foreach (GroupRowRef r in existing)
            {
                if (string.Equals(r.Id, currentId, StringComparison.Ordinal))
                {
                    plan.Keep.Add(new GroupRowRef { Id = r.Id, RoomId = currentRoom, Name = r.Name });
                    covered.Add(currentRoom);
                }
                else if (!string.IsNullOrEmpty(r.RoomId) && roomIds.Contains(r.RoomId)
                    && !string.Equals(r.RoomId, currentRoom, StringComparison.Ordinal))
                {
                    plan.Keep.Add(new GroupRowRef { Id = r.Id, RoomId = r.RoomId, Name = r.Name });
                    covered.Add(r.RoomId);
                }
                else
                {
                    spare.Add(r);
                }
            }
            foreach (string roomId in roomIds)
            {
                if (covered.Contains(roomId))
                    continue;
                if (spare.Count > 0)
                {
                    GroupRowRef reused = spare[0];
                    spare.RemoveAt(0);
                    plan.Reuse.Add(new GroupRowRef { Id = reused.Id, RoomId = roomId, Name = reused.Name });
                    covered.Add(roomId);
                }
                else
                {
                    plan.CreateRoomIds.Add(roomId);
                }
            }
            foreach (GroupRowRef r in spare)
                plan.DeleteIds.Add(r.Id);
            return plan;
        }
    }
}
