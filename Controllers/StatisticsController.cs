using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HTicket.Models;

namespace HTicket.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StatisticsController : Controller
    {
        private readonly TestDbContext _context;

        public StatisticsController(TestDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchEvent) 
        {
            // 1. Doanh thu theo tháng (Dựa trên cột TotalAmount và OrderDate thực tế)
            var rawRevenue = await _context.Orders
                .Where(o => o.Status == "Thành công")
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            ViewBag.MonthlyLabels = rawRevenue.Select(x => $"{x.Year}-{x.Month:D2}").ToList();
            ViewBag.MonthlyData = rawRevenue.Select(x => x.Total).ToList();

            // 2. Thống kê Dòng tiền tổng quát (Sửa để khớp với các trạng thái trong ảnh Order)
            var allOrders = await _context.Orders.ToListAsync();

            // Tổng tiền từ đơn "Thành công" (Ví dụ: Id 3, 4, 6...)
            ViewBag.TotalRevenue = allOrders.Where(o => o.Status == "Thành công").Sum(o => o.TotalAmount);

            // Tiền "Om" (Ví dụ: Các đơn Chờ thanh toán nếu có)
            ViewBag.PendingCash = allOrders.Where(o => o.Status == "Chờ thanh toán").Sum(o => o.TotalAmount);

            // 3. Chi tiết từng sự kiện & Tìm kiếm
            var eventDetails = await _context.Events
                .Select(e => new {
                    EventId = e.Id,
                    EventName = e.Name,
                    // Sử dụng cột TotalQuantity
                    TotalTickets = e.Tickets.Sum(t => t.TotalQuantity),
                    // Đã bán = Tổng số lượng vé trong đơn "Thành công"
                    SoldTickets = _context.Orders
                        .Where(o => o.Ticket.EventId == e.Id && o.Status == "Thành công")
                        .Sum(o => o.Quantity),
                    // Vé "Om" = Tổng số lượng vé trong đơn "Chờ thanh toán"
                    HoldingTickets = _context.Orders
                        .Where(o => o.Ticket.EventId == e.Id && o.Status == "Chờ thanh toán")
                        .Sum(o => o.Quantity),
                    // Số người mua (Distinct CustomerId)
                    TotalBuyers = _context.Orders
                        .Where(o => o.Ticket.EventId == e.Id && o.Status == "Thành công")
                        .Select(o => o.CustomerId).Distinct().Count(),
                    // Dòng tiền thực tế của riêng sự kiện này
                    ActualCash = _context.Orders
                        .Where(o => o.Ticket.EventId == e.Id && o.Status == "Thành công")
                        .Sum(o => o.TotalAmount)
                }).ToListAsync();

            // Xử lý tìm kiếm sự kiện
            if (!string.IsNullOrEmpty(searchEvent))
            {
                eventDetails = eventDetails
                    .Where(x => x.EventName.Contains(searchEvent, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            // Lấy tất cả vé và đơn hàng để tính toán
            var allTickets = await _context.Tickets.ToListAsync();
            

            // 1. Tổng số vé đã phát hành (TotalCapacity)
            int totalCapacity = allTickets.Sum(t => t.TotalQuantity);

            // 2. Tổng số vé đã bán thực tế (Đơn thành công)
            int totalSold = allOrders.Where(o => o.Status == "Thành công").Sum(o => o.Quantity);

            // 3. Tính tỷ lệ lấp đầy (%)
            double occupancyRate = totalCapacity > 0 ? (double)totalSold / totalCapacity * 100 : 0;

            ViewBag.OccupancyRate = Math.Round(occupancyRate, 1); // Làm tròn 1 chữ số thập phân
            ViewBag.TotalSold = totalSold;
            ViewBag.TotalCapacity = totalCapacity;
            ViewBag.EventTable = eventDetails;

            // 4. Dữ liệu Top 5 sự kiện (Lấy từ danh sách đã tính toán)
            var top5 = eventDetails.OrderByDescending(x => x.SoldTickets).Take(5).ToList();
            ViewBag.EventLabels = top5.Select(x => x.EventName).ToList();
            ViewBag.EventData = top5.Select(x => x.SoldTickets).ToList();

            return View();
        }
    }
}