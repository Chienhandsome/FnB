using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật thuần của màn "Theo dõi đặt phòng" (spec §3): nhãn thứ, cắt thanh vào cửa sổ,
    /// 4 hàng tổng, đổi số ngày xem, đổi tháng, danh sách thay đổi khi kéo thả.
    /// Không chạm DB, không chạm WinForms — kiểm được bằng selftest.
    /// </summary>
    internal static class BookingCalendarRules
    {
        public const int MinDays = 7;
        public const int MaxDays = 90;

        /// <summary>
        /// Nhãn thứ của một ngày, dạng "25.T5". GIỮ Y HỆT BUG CỦA APP GỐC: Thứ Tư hiện "T5",
        /// Thứ Năm hiện "T4" (ảnh 01 spec: 25/11/2026 Thứ Tư hiện "25.T5", 26/11 Thứ Năm hiện "26.T4").
        /// Quyết định của Chiến 23/11 (§5.1): không sửa bug này.
        /// </summary>
        public static string WeekdayLabel(DateTime date)
        {
            string thu;
            switch (date.DayOfWeek)
            {
                case DayOfWeek.Sunday: thu = "CN"; break;
                case DayOfWeek.Monday: thu = "T2"; break;
                case DayOfWeek.Tuesday: thu = "T3"; break;
                case DayOfWeek.Wednesday: thu = "T5"; break; // Bug app gốc: Thứ Tư hiện T5.
                case DayOfWeek.Thursday: thu = "T4"; break;  // Bug app gốc: Thứ Năm hiện T4.
                case DayOfWeek.Friday: thu = "T6"; break;
                default: thu = "T7"; break;
            }
            return date.Day.ToString(CultureInfo.InvariantCulture) + "." + thu;
        }

        /// <summary>
        /// Rộng một cột ngày (pixel, 96 DPI): (rộng lưới − 90 − 109 − 25 − 40) / số ngày,
        /// tối thiểu 40 (spec §1.1). 90 = cột loại phòng, 109 = cột phòng, 40 = cột Tổng,
        /// 25 = trừ hao thanh cuộn.
        /// </summary>
        public static int DayColumnWidth(int gridWidth, int dayCount)
        {
            if (dayCount <= 0)
                return 40;
            int w = (gridWidth - 90 - 109 - 25 - 40) / dayCount;
            return w < 40 ? 40 : w;
        }

        /// <summary>
        /// Cắt một thanh đặt phòng vào cửa sổ xem [from, from + days − 1].
        /// Trả về chỉ số ngày (0-based) của ô đầu/cuối ĐÃ VẼ, tính theo ngày (TUNGAY/DENNGAY
        /// chuẩn hóa về 00:00 — mặc định tạm Q3). Thanh nằm ngoài hẳn cửa sổ → false.
        /// Khoảng vẽ là [TUNGAY, DENNGAY] (bao cả ngày cuối), giống app gốc.
        /// </summary>
        public static bool TryClipBar(DateTime tuNgay, DateTime denNgay, DateTime from, int days,
            out int firstDay, out int lastDay)
        {
            firstDay = 0;
            lastDay = -1;
            if (days <= 0)
                return false;
            int start = (int)(tuNgay.Date - from.Date).TotalDays;
            int end = (int)(denNgay.Date - from.Date).TotalDays;
            if (end < 0 || start > days - 1)
                return false;
            firstDay = start < 0 ? 0 : start;
            lastDay = end > days - 1 ? days - 1 : end;
            return firstDay <= lastDay;
        }

        /// <summary>
        /// Số ngày của một thanh đã cắt (dùng cho cột Tổng của phòng).
        /// GIỮ Y HỆT BUG CỦA APP GỐC: tổng = kết thúc − bắt đầu, THIẾU +1
        /// (phiếu 1 ngày cho 0). Quyết định của Chiến 23/11 (§5.4): không sửa.
        /// </summary>
        public static int ClippedBarDays(int firstDay, int lastDay)
        {
            return lastDay - firstDay;
        }

        /// <summary>Thanh có phần vượt ngoài cửa sổ xem không (bắt đầu trước ngày đầu hoặc kết thúc sau ngày cuối).</summary>
        public static bool IsOutOfWindow(DateTime tuNgay, DateTime denNgay, DateTime from, int days)
        {
            return tuNgay.Date < from.Date || denNgay.Date > from.Date.AddDays(days - 1);
        }

        /// <summary>
        /// 4 hàng tổng của lưới, tính từ các thanh ĐÃ TẢI (truy vấn đã loại phiếu ngoài cửa sổ
        /// theo điều kiện BETWEEN của app gốc). Đếm theo PHÒNG (một phòng có 2 phiếu trùng ngày
        /// vẫn chỉ tính 1 phòng đã bán ngày đó). Công suất làm tròn 1 chữ số thập phân.
        /// </summary>
        public sealed class CalendarTotals
        {
            /// <summary>Số phòng đã đặt mỗi ngày (theo phòng, không theo phiếu).</summary>
            public int[] SoldPerDay;
            /// <summary>Số phòng còn lại mỗi ngày = Tổng số phòng − Đã bán.</summary>
            public int[] RemainingPerDay;
            /// <summary>Công suất mỗi ngày (%) = Đã bán × 100 / Tổng số phòng.</summary>
            public double[] CapacityPerDay;
            /// <summary>Số phòng đã đặt mỗi ngày, theo từng loại phòng (hàng nhóm).</summary>
            public Dictionary<string, int[]> SoldPerDayByType = new Dictionary<string, int[]>();
            /// <summary>Cột Tổng của từng phòng: Σ (kết thúc − bắt đầu) của các thanh (thiếu +1, bug app gốc).</summary>
            public Dictionary<string, int> TotalDaysByRoom = new Dictionary<string, int>();
            public int TotalSoldDays;
            public int TotalRemainingDays;
            /// <summary>Công suất tổng = trung bình các ngày (spec §3.1).</summary>
            public double TotalCapacity;
        }

        public static CalendarTotals BuildTotals(CalendarData data, DateTime from, int days)
        {
            var t = new CalendarTotals
            {
                SoldPerDay = new int[days],
                RemainingPerDay = new int[days],
                CapacityPerDay = new double[days]
            };
            if (days <= 0)
                return t;
            // Phòng nào thuộc loại nào (để tính hàng nhóm); phòng không có loại vẫn tính vào tổng chung.
            var roomType = new Dictionary<string, string>();
            foreach (CalendarRoom room in data.Rooms)
                roomType[room.Id] = room.TypeId;
            foreach (CalendarRoomType type in data.Types)
                t.SoldPerDayByType[type.Id] = new int[days];
            // Mỗi (phòng, ngày) chỉ đếm 1 lần dù có nhiều phiếu trùng.
            var booked = new HashSet<string>();
            foreach (CalendarBookingBar bar in data.Bars)
            {
                int first, last;
                if (!TryClipBar(bar.TuNgay, bar.DenNgay, from, days, out first, out last))
                    continue;
                int total;
                t.TotalDaysByRoom.TryGetValue(bar.RoomId, out total);
                t.TotalDaysByRoom[bar.RoomId] = total + ClippedBarDays(first, last);
                for (int d = first; d <= last; d++)
                {
                    string key = bar.RoomId + "|" + d.ToString(CultureInfo.InvariantCulture);
                    if (!booked.Add(key))
                        continue;
                    t.SoldPerDay[d]++;
                    string typeId;
                    int[] byType;
                    if (roomType.TryGetValue(bar.RoomId, out typeId) &&
                        t.SoldPerDayByType.TryGetValue(typeId, out byType))
                        byType[d]++;
                }
            }
            for (int d = 0; d < days; d++)
            {
                t.RemainingPerDay[d] = data.TotalRooms - t.SoldPerDay[d];
                t.TotalSoldDays += t.SoldPerDay[d];
                t.TotalRemainingDays += t.RemainingPerDay[d];
                t.CapacityPerDay[d] = data.TotalRooms == 0
                    ? 0
                    : Math.Round(t.SoldPerDay[d] * 100.0 / data.TotalRooms, 1, MidpointRounding.AwayFromZero);
            }
            if (days > 0 && data.TotalRooms > 0)
            {
                double sum = 0;
                for (int d = 0; d < days; d++)
                    sum += t.CapacityPerDay[d];
                t.TotalCapacity = Math.Round(sum / days, 1, MidpointRounding.AwayFromZero);
            }
            return t;
        }

        /// <summary>
        /// Đổi số ngày xem theo nút /4, /2, 2x, 4x (spec §1.1): chia/làm tròn lên rồi kẹp [7, 90].
        /// Làm tròn AwayFromZero (cùng quy ước làm tròn của LamTron).
        /// </summary>
        public static int ChangeDaysCount(int current, string op)
        {
            double next;
            switch (op)
            {
                case "/4": next = current / 4.0; break;
                case "/2": next = current / 2.0; break;
                case "2x": next = current * 2.0; break;
                case "4x": next = current * 4.0; break;
                default: return current;
            }
            int days = (int)Math.Round(next, MidpointRounding.AwayFromZero);
            if (days < MinDays) days = MinDays;
            if (days > MaxDays) days = MaxDays;
            return days;
        }

        /// <summary>
        /// Cửa sổ xem theo tháng: ngày đầu tháng của "tháng này" ± offset, số ngày = số ngày của tháng đó
        /// (spec §2.1, menu "Số ngày xem": Tháng này / Tháng trước / Tháng sau).
        /// </summary>
        public static void MonthViewRange(DateTime today, int offsetMonths, out DateTime from, out int days)
        {
            DateTime month = new DateTime(today.Year, today.Month, 1).AddMonths(offsetMonths);
            from = month;
            days = DateTime.DaysInMonth(month.Year, month.Month);
        }

        /// <summary>
        /// Kiểm tra vị trí thả sau khi kéo: hàng đích phải là một hàng phòng, cột đích trong cửa sổ xem.
        /// Sai → thanh trở về vị trí cũ, không hỏi gì (spec §3.3).
        /// </summary>
        public static bool ValidateDrop(int roomRowIndex, int roomCount, int startDay, int endDay, int days)
        {
            return roomRowIndex >= 0 && roomRowIndex < roomCount &&
                   startDay >= 0 && endDay < days && startDay <= endDay;
        }

        private static string DateText(DateTime d)
        {
            return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Các dòng thay đổi cho hộp xác nhận kéo thả (spec §2.2.2), chỉ ghi dòng nào đổi,
        /// theo thứ tự gốc: Từ ngày, Đến ngày, Phòng.
        /// </summary>
        public static List<string> BuildDragChanges(string oldRoomName, string newRoomName,
            DateTime oldTuNgay, DateTime oldDenNgay, DateTime newTuNgay, DateTime newDenNgay)
        {
            var lines = new List<string>();
            if (newTuNgay.Date != oldTuNgay.Date)
                lines.Add("Từ ngày: " + DateText(oldTuNgay) + " --> " + DateText(newTuNgay));
            if (newDenNgay.Date != oldDenNgay.Date)
                lines.Add("Đến ngày: " + DateText(oldDenNgay) + " --> " + DateText(newDenNgay));
            if (!string.Equals(newRoomName, oldRoomName, StringComparison.Ordinal))
                lines.Add("Phòng: " + oldRoomName + " --> " + newRoomName);
            return lines;
        }

        /// <summary>Hộp xác nhận kéo thả (spec §2.2.2, ảnh 07): câu hỏi + các dòng thay đổi.</summary>
        public static string BuildDragConfirmMessage(string tenKhach, List<string> changes)
        {
            return "Bạn có muốn cập nhật thay đổi phiếu đặt phòng của '" + tenKhach + "'\n" +
                   string.Join("\n", changes.ToArray());
        }
    }
}
